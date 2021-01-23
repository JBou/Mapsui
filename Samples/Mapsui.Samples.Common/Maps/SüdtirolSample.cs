using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
using Mapsui.UI;
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

        public void Setup(IMapControl mapControl)
        {
            mapControl.Map = CreateMap();
        }

        public static Map CreateMap()
        {
            var map = new Map {CRS = "EPSG:3857"};
            //map.Layers.Add(OpenStreetMap.CreateTileLayer());
            map.Layers.Add(CreateTileLayer(CreateLuftBildTileSource()));
            map.Layers.Add(CreateTileLayer(CreateParzellenTileSource()));
            map.Layers.Add(CreateTileLayer(CreateParzellenNummernTileSource()));
            map.Layers.Add(CreateTileLayer(CreateKatastralgemeindenTileSource()));
            map.Layers.Add(CreateTileLayer(CreateKlammernTileSource()));

            var bb = new BoundingBox(SphericalMercator.FromLonLat(10, 45.8),
                SphericalMercator.FromLonLat(12.9, 47.5));
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
            return CreateWmsTileSource(CADASTRE_PUBLIC_WMS_URL, "Parzellen", new[] {"Particelle_poligoni_validate_pubb,Particelle_uniche_validate_pubb"},
                new[] {"Particelle_poligoni_orange_nolab_bil_pubb,Particelle_uniche_transparent_pubb"});
        }   
        
        public static ITileSource CreateParzellenNummernTileSource()
        {
            return CreateWmsTileSource(CADASTRE_PUBLIC_WMS_URL, "Parzellennummern", new[] {"Particelle_poligoni_validate_pubb"},
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
        private static WmsProvider CreateWmsProvider(string url, string name, IEnumerable<string> layers, IEnumerable<string> styles)
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

        public static ITileSource CreateWmsTileSource(string url, string name, IEnumerable<string> layers, IEnumerable<string> styles)
        {
            var schema = new GlobalSphericalMercator {Format = "image/png", Srs = "EPSG:3857"};

            var request = new WmscRequest(new Uri(url), schema, layers,
                styles, new Dictionary<string, string> {{"transparent", "true"}});

            var provider = new HttpTileProvider(request);
            return new TileSource(provider, schema) {Name = name};
        }
        
    }
}