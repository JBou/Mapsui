﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;

namespace Mapsui.Samples.Common.Maps
{
    public static class AtlasSample
    {
        private const string AtlasLayerName = "Atlas Layer";
        private static int atlasBitmapId;
        private static Random rnd = new Random();


        public static Map CreateMap()
        {
            atlasBitmapId = BitmapRegistry.Instance.Register(typeof(AtlasSample).GetTypeInfo().Assembly.GetManifestResourceStream("Mapsui.Samples.Common.Images.osm-liberty.png"));

            var map = new Map();

            map.Layers.Add(OpenStreetMap.CreateTileLayer());
            map.Layers.Add(CreateAtlasLayer(map.Envelope));
            map.HoverLayers.Add(map.Layers.First(l => l.Name == AtlasLayerName));

            return map;
        }

        private static ILayer CreateAtlasLayer(BoundingBox envelope)
        {
            return new MemoryLayer
            {
                Name = AtlasLayerName,
                DataSource = CreateMemoryProviderWithDiverseSymbols(envelope, 400),
                Style = null
            };
        }

        public static MemoryProvider CreateMemoryProviderWithDiverseSymbols(BoundingBox envelope, int count = 100)
        {
            return new MemoryProvider(CreateAtlasFeatures(PointsSample.GenerateRandomPoints(envelope, count, 3)));
        }

        private static Features CreateAtlasFeatures(IEnumerable<IGeometry> randomPoints)
        {
            var features = new Features();
            var counter = 0;
            foreach (var point in randomPoints)
            {
                var feature = new Feature { Geometry = point, ["Label"] = counter.ToString() };

                var x = 0 + rnd.Next(0, 12) * 21;
                var y = 64 + rnd.Next(0, 6) * 21;
                var bitmapId = BitmapRegistry.Instance.Register(new Atlas(atlasBitmapId, x, y, 21, 21, 1));
                feature.Styles.Add(new SymbolStyle { BitmapId = bitmapId });

                features.Add(feature);
                counter++;
            }
            return features;
        }
    }
}