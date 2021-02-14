using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using BruTile;
using BruTile.Predefined;
using BruTile.Web;
using BruTile.Wmsc;
using BruTile.Wmts;
using Mapsui.Desktop.Wms;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Providers.Wfs;
using Mapsui.Providers.Wfs.Utilities;
using Mapsui.Styles;
using Mapsui.UI;
using Mapsui.UI.Wpf;
using Mapsui.Utilities;

namespace Mapsui.Samples.Common.Maps
{
    public class SüdtirolSample : ISample
    {
        public string Name => "Südtirol";
        public string Category => "Demo";

        private static string CADASTRE_PUBLIC_WMS_URL =
            "https://geoservices.buergernetz.bz.it/geoserver/p_bz-cadastre_public/ows?SERVICE=WMS&VERSION=1.3.0";

        private static string CADASTRE_WMS_URL =
            "https://geoservices.buergernetz.bz.it/geoserver/p_bz-cadastre/ows?SERVICE=WMS&VERSION=1.3.0";

        private static string WMTS_URL =
            "https://geoservices.buergernetz.bz.it/mapproxy/service/ows?SERVICE=WMTS&REQUEST=GetCapabilities";

        private static string WFS_URL = "https://geoservices.buergernetz.bz.it/geoserver/ows";

        private static MapControl _mapControl;
        private bool _mapDraggedOnce;
        private bool _mapDragged;
        private WFSProvider _wfsProvider;
        private static MemoryLayer selectedFeatures;

        public void Setup(IMapControl mapControl)
        {
            mapControl.Map = CreateMap();
            _mapControl = (MapControl) mapControl;
            setupMap();
        }

        private void setupMap()
        {
            _wfsProvider = CreateWFSProvider();

            _mapControl.MouseMove += MapControlOnMouseMove;
            //_mapControl.MouseLeftButtonDown += MapControlOnMouseLeftButtonDown;
            _mapControl.MouseLeftButtonUp += MapControlOnMouseLeftButtonUp;
        }

        public static Map CreateMap()
        {
            var map = new Map {CRS = "EPSG:3857"};
            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            map.Layers.Add(CreateTileLayer(CreateLuftBildTileSource()));
            //TODO Only show when zommed in
            map.Layers.Add(CreateTileLayer(CreateParzellenTileSource()));
            map.Layers.Add(CreateTileLayer(CreateParzellenNummernTileSource()));
            //map.Layers.Add(CreateTileLayer(CreateKatastralgemeindenTileSource()));
            //map.Layers.Add(CreateTileLayer(CreateKlammernTileSource()));

            selectedFeatures = new MemoryLayer("Selected") {DataSource = new MemoryProvider()};
            selectedFeatures.Style = new VectorStyle() {Opacity = 0.5f, Fill = new Brush {Color = Color.Red}};
            map.Layers.Add(selectedFeatures);

            var bb = new BoundingBox(SphericalMercator.FromLonLat(10, 45.8), SphericalMercator.FromLonLat(12.9, 47.5));
            map.Limiter = new ViewportLimiterKeepWithin
            {
                PanLimits = bb
            };
            map.Home = n => n.NavigateTo(bb.Centroid, map.Resolutions[0]);

            return map;
        }

        public static HttpTileSource CreateLuftBildTileSource()
        {
            using (var httpClient = new HttpClient())
            using (var response = httpClient.GetStreamAsync(WMTS_URL).Result)
            {
                var tileSources = WmtsParser.Parse(response);
                return tileSources.First(t =>
                    ((WmtsTileSchema) t.Schema).Layer == "P_BZ_OF_2014_2015_2017" && t.Schema.Srs == "EPSG:3857");
            }
        }

        public static ILayer CreateTileLayer(ITileSource tileSource, string name = null)
        {
            return new TileLayer(tileSource) {Name = name ?? tileSource.Name};
        }

        public static ILayer CreateImageLayer(IProvider provider, string name)
        {
            return new ImageLayer(name) {DataSource = provider};
        }

        public static ITileSource CreateParzellenTileSource()
        {
            return CreateWmsTileSource(CADASTRE_PUBLIC_WMS_URL, "Parzellen",
                new[] {"Particelle_poligoni_validate_pubb,Particelle_uniche_validate_pubb"},
                new[] {"Particelle_poligoni_orange_nolab_bil_pubb,Particelle_uniche_transparent_pubb"});
        }

        public static ITileSource CreateParzellenNummernTileSource()
        {
            return CreateWmsTileSource(CADASTRE_PUBLIC_WMS_URL, "Parzellennummern",
                new[] {"Particelle_poligoni_validate_pubb"},
                new[] {"Particelle_poligoni_orange_lab_bil_pubb"});
        }

        public static ITileSource CreateKatastralgemeindenTileSource()
        {
            return CreateWmsTileSource(CADASTRE_WMS_URL, "Katastralgemeinden", new[] {"COMUNI_CATASTALI"},
                new[] {"Comune_catastale_orange_bil"});
        }

        public static ITileSource CreateKlammernTileSource()
        {
            return CreateWmsTileSource(CADASTRE_PUBLIC_WMS_URL, "Klammern", new[] {"Particelle_graffe_validate_pubb"},
                new[] {"Particelle_graffa_orange_bil_pubb"});
        }

        [Obsolete]
        private static WmsProvider CreateWmsProvider(string url, string name, IEnumerable<string> layers,
            IEnumerable<string> styles)
        {
            var provider = new WmsProvider(url)
            {
                ContinueOnError = true,
                //TimeOut = 20000,
                CRS = "EPSG:3857",
            };

            foreach (var layer in layers)
            {
                provider.AddLayer(layer);
            }

            foreach (var style in styles)
            {
                provider.AddStyle(style);
            }

            provider.SetImageFormat(provider.OutputFormats[0]);
            return provider;
        }

        public static ITileSource CreateWmsTileSource(string url, string name, IEnumerable<string> layers,
            IEnumerable<string> styles)
        {
            var schema = new GlobalSphericalMercator {Format = "image/png", Srs = "EPSG:3857"};

            var request = new WmscRequest(new Uri(url), schema, layers,
                styles, new Dictionary<string, string> {{"transparent", "true"}});

            var provider = new HttpTileProvider(request);
            return new TileSource(provider, schema) {Name = name};
        }

        private void MapControlOnMouseMove(object sender, MouseEventArgs args)
        {
            if (args.LeftButton == MouseButtonState.Pressed)
            {
                if (_mapDraggedOnce && !_mapDragged
                ) //Even the first click raises this event, with this we only trigger _mapDragged the second time. 
                {
                    _mapDragged = true;
                    _mapDraggedOnce = false;
                }
                else
                {
                    _mapDraggedOnce = true;
                }
            }
        }

        private void MapControlOnMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
        {
            if (!_mapDragged)
                MapClicked(sender, args);
            _mapDragged = false;
            _mapDraggedOnce = false;
        }

        private void MapClicked(object sender, MouseButtonEventArgs args)
        {
            var infoArgs = _mapControl.GetMapInfo(args.GetPosition(_mapControl).ToMapsui());

            //TODO?? We need to invert the point because the WFS returns inverted points...
            //should be fixed somehow different (Proper coordiante system transformation?)
            //see https://gis.stackexchange.com/q/320671/176232
            //var invertedPoint = new Geometries.Point(infoArgs.WorldPosition.Y, infoArgs.WorldPosition.X);
            var point = infoArgs.WorldPosition;
            
            var intersectingParcells = _wfsProvider.ExecuteIntersectionQuery(infoArgs.WorldPosition.BoundingBox);
            var parcells = intersectingParcells.Where(x => x.Geometry.Contains(point)).ToList();

            parcells.Sort(new InnerGeometryComparer());

            var parcell = parcells.FirstOrDefault();
            if (parcell != null)
            {
                //Get all parcells with the same number (codice)
                //var features = result.Where(x => x["PPOL_CODICE"].Equals(parcell["PPOL_CODICE"]));

                //MessageBox.Show(String.Join(Environment.NewLine, result.Select(y => y["PPOL_CODICE"])));

                var result = SearchByCatastre(parcell["PPOL_CCAT_CODICE"].ToString(), parcell["PPOL_CODICE"].ToString());
                ((MemoryProvider) selectedFeatures.DataSource).ReplaceFeatures(result);
                selectedFeatures.DataHasChanged();
                
                MessageBox.Show(String.Join(Environment.NewLine,
                    parcell.Fields.Select(x => x + ": " + parcell[x])));
            }
        }

        class InnerGeometryComparer : IComparer<IFeature>
        {
            public int Compare(IFeature feature1, IFeature feature2)
            {
                int returnValue = 0;

                if (feature1?.Geometry == null || feature2?.Geometry == null)
                {
                    returnValue = 0;
                }
                //If feature1 completely contains feature2 (like an island in a sea),
                //it's pushed down in the list, pulling the "island" up in the list
                else if (feature1.Geometry.AllVertices().All(feature2.Geometry.Contains))
                {
                    returnValue = -1;
                }
                else if (feature2.Geometry.AllVertices().All(feature1.Geometry.Contains))
                {
                    returnValue = 1;
                }

                return returnValue;
            }
        }

        private IEnumerable<IFeature> SearchByCatastre(string katastralgemeindeNr, string parzellenNummer)
        {
            //TODO We need to use 1_0_0 because else the axis are inverted
            var ogcFilter = new OGCFilterCollection();
            ogcFilter.AddFilter(new PropertyIsEqualToFilter_FE1_1_0("PPOL_CCAT_CODICE", katastralgemeindeNr));
            ogcFilter.AddFilter(new PropertyIsEqualToFilter_FE1_1_0("PPOL_CODICE", parzellenNummer));
            ogcFilter.Junctor = OGCFilterCollection.JunctorEnum.And;
            _wfsProvider.OgcFilter = ogcFilter;

            //var bb = _wfsProvider.FeatureTypeInfo.BBox;
            var features = _wfsProvider.GetFeaturesInView(null, 0);

            _wfsProvider.OgcFilter = null;

            return features;
        }

        private static WFSProvider CreateWFSProvider()
        {
            //We NEED to use WFS version 1.0.0 to make axis order work. see https://gis.stackexchange.com/q/320671/176232

            //var featureTypeInfo = new WfsFeatureTypeInfo(WFS_URL, "p_bz-cadastre_public", null, "Particelle_poligoni_validate_pubb", "PPOL_SHAPE", GeometryTypeEnum.PolygonPropertyType);
            //var statesProvider = new WFSProvider(featureTypeInfo, WFSProvider.WFSVersionEnum.WFS_1_1_0)
            var wfsProvider = new WFSProvider(WFS_URL, "p_bz-cadastre_public", "Particelle_poligoni_validate_pubb",
                WFSProvider.WFSVersionEnum.WFS_1_1_0)
            {
                QuickGeometries = true,
                GetFeatureGetRequest = true,
                CRS = "EPGS:3857",
            };
            foreach (var elementInfo in wfsProvider.FeatureTypeInfo.Elements)
            {
                wfsProvider.Labels.Add(elementInfo.Name);
            }

            return wfsProvider;
        }
    }
}