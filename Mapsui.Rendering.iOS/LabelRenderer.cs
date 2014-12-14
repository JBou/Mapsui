using Mapsui;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using MonoTouch.CoreAnimation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace XamarinRendering
{
	static class LabelRenderer
	{
		public static List<IFeature> RenderStackedLabelLayer(IViewport viewport, LabelLayer layer)
		{
			var renderedFeatures = new List<IFeature> ();
			var canvas = new CALayer ();
			canvas.Opacity = (float)layer.Opacity;

			//todo: take into account the priority 
			var features = layer.GetFeaturesInView(viewport.Extent, viewport.Resolution);
			var margin = viewport.Resolution * 50;

			if(layer.Style != null)
			{
				var clusters = new List<Cluster>();
				//todo: repeat until there are no more merges
				ClusterFeatures(clusters, features, margin, layer.Style, viewport.Resolution);

				foreach (var cluster in clusters)
				{
					var feature = cluster.Features.OrderBy(f => f.Geometry.GetBoundingBox().GetCentroid().Y).FirstOrDefault();
					//SetFeatureOutline (feature, layer.Name, cluster.Features.Count);
					//var bb = RenderBox(cluster.Box, viewport);

					//Zorg dat dit ALTIJD decimal zelfde ISet als ViewChanged is
					//var feature = cluster.Features.FirstOrDefault ();

					var styles = feature.Styles ?? Enumerable.Empty<IStyle>();
					foreach (var style in styles)
					{
						if (feature.Styles != null && style.Enabled)
						{
							var styleKey = layer.Name; //feature.GetHashCode ().ToString ();
							var renderedGeometry = (feature[styleKey] != null) ? (CALayer)feature[styleKey] : null;
							var labelText = layer.GetLabelText(feature);

							if (renderedGeometry == null) {
							//Mapsui.Geometries.Point point, Offset stackOffset, LabelStyle style, IFeature feature, IViewport viewport, string text)
								renderedGeometry = RenderLabel(feature.Geometry as Mapsui.Geometries.Point,
								                               style as LabelStyle, feature, viewport, labelText);

								feature [styleKey] = renderedGeometry;
								feature ["first"] = true;

							} else {
								feature ["first"] = false;
							}
						}
					}
					renderedFeatures.Add (feature);

					/*
					Offset stackOffset = null;

					foreach (var feature in cluster.Features.OrderBy(f => f.Geometry.GetBoundingBox().GetCentroid().Y))
					{
						if (layerStyle is IThemeStyle) style = (layerStyle as IThemeStyle).GetStyle(feature);
						if ((style == null) || (style.Enabled == false) || (style.MinVisible > viewport.Resolution) || (style.MaxVisible < viewport.Resolution)) continue;

						if (stackOffset == null) //first time
						{
							stackOffset = new Offset();
							if (cluster.Features.Count > 1)
								canvas.AddSublayer (RenderBox(cluster.Box, viewport));
						}
						else stackOffset.Y += 18; //todo: get size from text, (or just pass stack nr)

						if (!(style is LabelStyle)) throw new Exception("Style of label is not a LabelStyle");
						var labelStyle = style as LabelStyle;
						string labelText = layer.GetLabel(feature);
						var position = new Mapsui.Geometries.Point(cluster.Box.GetCentroid().X, cluster.Box.Bottom);
						canvas.AddSublayer(RenderLabel(position, stackOffset, labelStyle, feature, viewport, labelText));
					}
					*/
				}
			}

			return renderedFeatures;
		}

		private static CALayer RenderBox(BoundingBox box, IViewport viewport)
		{
			const int margin = 32;

			var p1 = viewport.WorldToScreen(box.Min);
			var p2 = viewport.WorldToScreen(box.Max);

			var rectangle = new RectangleF {Width = (float) (p2.X - p1.X + margin), Height = (float) (p1.Y - p2.Y + margin)};

		    var canvas = new CALayer
		    {
		        Frame = rectangle,
		        BorderColor = new MonoTouch.CoreGraphics.CGColor(0, 0, 0, 1),
		        BorderWidth = 2
		    };

		    return canvas;
		}

		public static CALayer RenderLabelLayer(IViewport viewport, LabelLayer layer, List<IFeature> features)
		{
			var canvas = new CALayer();
			canvas.Opacity = (float)layer.Opacity;

			//todo: take into account the priority 
			var stackOffset = new Offset();

			if (layer.Style != null)
			{
				var style = layer.Style;

				foreach (var feature in features)
				{
					if (style is IThemeStyle) style = (style as IThemeStyle).GetStyle(feature);

					if ((style == null) || (style.Enabled == false) || (style.MinVisible > viewport.Resolution) || (style.MaxVisible < viewport.Resolution)) continue;
					if (!(style is LabelStyle)) throw new Exception("Style of label is not a LabelStyle");
					//var labelStyle = style as LabelStyle;
					string labelText = layer.GetLabelText(feature);

					var label = RenderLabel (feature.Geometry as Mapsui.Geometries.Point,
					                         style as LabelStyle, feature, viewport, labelText);

					canvas.AddSublayer(label);
				}
			}

			return canvas;
		}

		private static void ClusterFeatures(
			IList<Cluster> clusters, 
			IEnumerable<IFeature> features, 
			double minDistance,
			IStyle layerStyle, 
			double resolution)
		{
			var style = layerStyle;
			//this method should repeated several times until there are no more merges
			foreach (var feature in features.OrderBy(f => f.Geometry.GetBoundingBox().GetCentroid().Y))
			{
				if (layerStyle is IThemeStyle) style = (layerStyle as IThemeStyle).GetStyle(feature);
				if ((style == null) || (style.Enabled == false) || (style.MinVisible > resolution) || (style.MaxVisible < resolution)) 
					continue;

				var found = false;
				foreach (var cluster in clusters)
				{
					//todo: use actual overlap of labels not just proximity of geometries.
					if (cluster.Box.Grow(minDistance).Contains(feature.Geometry.GetBoundingBox().GetCentroid()))
					{
						cluster.Features.Add(feature);
						cluster.Box = cluster.Box.Join(feature.Geometry.GetBoundingBox());
						found = true;
						break;
					}
				}

				if (!found)
				{
					var cluster = new Cluster();
					cluster.Box = feature.Geometry.GetBoundingBox().Clone();
					cluster.Features = new List<IFeature>();
					cluster.Features.Add(feature);
					clusters.Add(cluster);
				}
			}
		}

		public static CALayer RenderLabel(Mapsui.Geometries.Point point, LabelStyle style, IViewport viewport)
		{
			//Offset stackOffset,
			//return RenderLabel(point, stackOffset, style, viewport, style.Text);
			return new CALayer ();
		}

		public static CATextLayer RenderLabel(Mapsui.Geometries.Point point, LabelStyle style, IFeature feature, IViewport viewport, string text)
		{
			// Offset stackOffset,
			Mapsui.Geometries.Point p = viewport.WorldToScreen(point);
			//var pointF = new xPointF((float)p.X, (float)p.Y);
			var label = new CATextLayer ();


			var aString = new MonoTouch.Foundation.NSAttributedString (text,
			                                                           new MonoTouch.CoreText.CTStringAttributes(){
				Font = new MonoTouch.CoreText.CTFont("ArialMT", 10)
			});

			var frame = new RectangleF(new System.Drawing.Point((int)p.X, (int)p.Y), GetSizeForText(0, aString));
			//label.Frame = frame;
			//frame.Width = (float)(p2.X - p1.X);// + margin);
			//frame.Height = (float)(p1.Y - p2.Y);

			label.FontSize = 10;
			label.ForegroundColor = new MonoTouch.CoreGraphics.CGColor (0, 0, 255, 150);
			label.BackgroundColor = new MonoTouch.CoreGraphics.CGColor (255, 0, 2, 150);
			label.String = text;

			label.Frame = frame;

			Console.WriteLine ("Pos " + label.Frame.X + ":" + label.Frame.Y + " w " + label.Frame.Width + " h " + label.Frame.Height);

			// = MonoTouch.UIKit.UIScreen.MainScreen.Scale;
			//	label.ContentsScale = scale;


			return label;
			//var border = new Border();
			//var textblock = new TextBlock();

			//Text
			//textblock.Text = text;

			//Colors
			//textblock.Foreground = new SolidColorBrush(style.ForeColor.ToXaml());
			//border.Background = new SolidColorBrush(style.BackColor.Color.ToXaml());

			//Font
			//textblock.FontFamily = new FontFamily(style.Font.FontFamily);
			//textblock.FontSize = style.Font.Size;

			//set some defaults which should be configurable someday
			const double witdhMargin = 3.0;
			const double heightMargin = 0.0;
			//textblock.Margin = new Thickness(witdhMargin, heightMargin, witdhMargin, heightMargin);
			//border.CornerRadius = new CornerRadius(4);
			//border.Child = textblock;
			//Offset

			//var textWidth = textblock.ActualWidth;
			//var textHeight = textblock.ActualHeight;
			#if !SILVERLIGHT && !NETFX_CORE
			// in WPF the width and height is not calculated at this point. So we use FormattedText
			//getTextWidthAndHeight(ref textWidth, ref textHeight, style, text);
			#endif
			//border.SetValue(Canvas.LeftProperty, windowsPoint.X + style.Offset.X + stackOffset.X - (textWidth + 2 * witdhMargin) * (short)style.HorizontalAlignment * 0.5f);
			//border.SetValue(Canvas.TopProperty, windowsPoint.Y + style.Offset.Y + stackOffset.Y - (textHeight + 2 * heightMargin) * (short)style.VerticalAlignment * 0.5f);

			//return border;
			//return null;

		}

		private static SizeF GetSizeForText(int width, MonoTouch.Foundation.NSAttributedString aString)
		{
			var frameSetter = new MonoTouch.CoreText.CTFramesetter (aString);

			MonoTouch.Foundation.NSRange range;
			//CTFramesetterRef framesetter = CTFramesetterCreateWithAttributedString( (CFMutableAttributedStringRef) attributedString); 
			var size = frameSetter.SuggestFrameSize (new MonoTouch.Foundation.NSRange (0, 0), null,
			                                         new System.Drawing.Size (width, Int32.MaxValue), out range);

			//CGSize suggestedSize = CTFramesetterSuggestFrameSizeWithConstraints(framesetter, CFRangeMake(0, 0), NULL, CGSizeMake(inWidth, CGFLOAT_MAX), NULL);
			//CFRelease(framesetter);
			Console.WriteLine ("Size = " + size.Width + ":" + size.Height + "Range = " + range.Length);

			return size;
		}

		#if !SILVERLIGHT && !NETFX_CORE
		private static void getTextWidthAndHeight(ref double width, ref double height, LabelStyle style, string text)
		{
			/*
			var formattedText = new FormattedText(
				text,
				CultureInfo.InvariantCulture,
				FlowDirection.LeftToRight,
				new Typeface(style.Font.FontFamily),
				style.Font.Size,
				new SolidColorBrush(style.ForeColor.ToXaml()));

			width = formattedText.Width;
			height = formattedText.Height;
			*/
		}

		#endif

		private class Cluster
		{
			public BoundingBox Box { get; set; }
			public IList<IFeature> Features { get; set; }
		}
	}
}