#nullable disable

using BruTile.MbTiles.Vector.VectorTileRenderer.Drawing;
using BruTile.MbTiles.Vector.VectorTileRenderer.Enums;
using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.VectorTileRenderer;

public class Renderer
{
    // TODO make it instance based... maybe
    static readonly object cacheLock = new();

    public static async Task<byte[]> RenderCached(IVectorCache cache, VectorStyle style, ICanvas canvas, int x, int y, double zoom, double sizeX = 512, double sizeY = 512, double scale = 1, List<string> whiteListLayers = null)
    {
        var layerString = whiteListLayers == null ? "" : string.Join(",-", whiteListLayers.ToArray());

        var bundle = new
        {
            style.Hash,
            sizeX,
            sizeY,
            scale,
            layerString,
        };

        lock (cacheLock)
        {
            if (!Directory.Exists(cache.CachePath))
            {
                Directory.CreateDirectory(cache.CachePath);
            }
        }

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(bundle);
        var hash = Utils.Sha256(json).Substring(0, 12); // get 12 digits to avoid fs length issues

        var fileName = x + "x" + y + "-" + zoom + "-" + hash + ".png";
        var path = Path.Combine(cache.CachePath, fileName);

        lock (cacheLock)
        {
            if (File.Exists(path))
            {
                return LoadBitmap(path);
            }
        }

        var bitmap = await Render(style, canvas, x, y, zoom, sizeX, sizeY, scale, whiteListLayers);

        // save to file in async fashion
        _ = Task.Run(() =>
        {
            if (bitmap != null)
            {
                try
                {
                    lock (cacheLock)
                    {
                        if (File.Exists(path))
                        {
                            return;
                        }

                        using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                        using var br = new BinaryWriter(fileStream);
                        br.Write(bitmap);
                        br.Flush();
                        br.Close();
                        fileStream.Close();
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }
            }
        });

        lock (cacheLock)
        {
            cache.Refresh();
        }

        return bitmap;
    }

    static byte[] LoadBitmap(string path)
    {
        return File.ReadAllBytes(path);
    }

    public async static Task<byte[]> Render(
        VectorStyle style,
        ICanvas canvas,
        int x, int y, double zoom,
        double sizeX = 512, double sizeY = 512, double scale = 1,
        List<string> whiteListLayers = null)
    {
        var vectorTileCache = new Dictionary<Source, VectorTile>();
        var categorizedVectorLayers = new Dictionary<string, List<VectorTileLayer>>();

        var actualZoom = zoom;

        if (sizeX < 1024)
        {
            var ratio = 1024 / sizeX;
            var zoomDelta = Math.Log(ratio, 2);

            actualZoom = zoom - zoomDelta;
        }

        sizeX *= scale;
        sizeY *= scale;

        canvas.StartDrawing(sizeX, sizeY);

        var visualLayers = new List<VisualLayer>();

        // TODO refactor this messy block
        foreach (var layer in style.Layers)
        {
            if (whiteListLayers != null && layer.Type != "background" && layer.SourceLayer != "")
            {
                if (!whiteListLayers.Contains(layer.SourceLayer))
                {
                    continue;
                }
            }

            if (layer.Source != null)
            {
                if (!vectorTileCache.ContainsKey(layer.Source))
                {
                    var tile = await layer.Source.Provider.GetVectorTile();

                    if (tile == null)
                    {
                        return null;
                    }

                    // magic sauce! :p
                    if (tile.IsOverZoomed)
                    {
                        canvas.ClipOverflow = true;
                    }

                    vectorTileCache[layer.Source] = tile;

                    // normalize the points from 0 to size
                    foreach (var vectorLayer in tile.Layers)
                    {
                        foreach (var feature in vectorLayer.VectorTileFeatures)
                        {
                            foreach (var geometry in feature.Geometry)
                            {
                                for (var i = 0; i < geometry.Count; i++)
                                {
                                    var point = geometry[i];
                                    geometry[i] = new Coordinate
                                    {
                                        X = (long)(point.X / feature.Extent * sizeX),
                                        Y = (long)(point.Y / feature.Extent * sizeY),
                                    };
                                }
                            }
                        }
                    }

                    foreach (var tileLayer in tile.Layers)
                    {
                        if (!categorizedVectorLayers.ContainsKey(tileLayer.Name))
                        {
                            categorizedVectorLayers[tileLayer.Name] = new List<VectorTileLayer>();
                        }

                        categorizedVectorLayers[tileLayer.Name].Add(tileLayer);
                    }
                }


                if (categorizedVectorLayers.TryGetValue(layer.SourceLayer, out var tileLayers))
                {
                    foreach (var tileLayer in tileLayers)
                    {
                        foreach (var feature in tileLayer.VectorTileFeatures)
                        {
                            var attributes = new Dictionary<string, object>(feature.Attributes);

                            attributes["$type"] = feature.GeometryType;
                            attributes["$id"] = layer.ID;
                            attributes["$zoom"] = actualZoom;

                            if (style.ValidateLayer(layer, actualZoom, attributes))
                            {
                                var brush = style.ParseStyle(layer, scale, attributes);

                                if (!brush.Paint.Visibility)
                                {
                                    continue;
                                }

                                visualLayers.Add(new VisualLayer()
                                {
                                    Type = VisualLayerType.Vector,
                                    VectorTileFeature = feature,
                                    Geometry = feature.Geometry,
                                    Brush = brush,
                                });
                            }
                        }
                    }
                }
            }
            else if (layer.Type == "background")
            {
                var brushes = style.GetStyleByType("background", actualZoom, scale);
                foreach (var brush in brushes)
                {
                    canvas.DrawBackground(brush);
                }
            }
        }

        // deferred rendering to preserve text drawing order
        foreach (var layer in visualLayers.OrderBy(item => item.Brush.ZIndex))
        {
            if (layer.Type == VisualLayerType.Vector)
            {
                var feature = layer.VectorTileFeature;
                var geometry = layer.Geometry;
                var brush = layer.Brush;

                if (!brush.Paint.Visibility)
                {
                    continue;
                }

                try
                {
                    if (feature.GeometryType == Tile.GeomType.Point)
                    {
                        foreach (var point in geometry)
                        {
                            canvas.DrawPoint(point[0], brush);
                        }
                    }
                    else if (feature.GeometryType == Tile.GeomType.LineString)
                    {
                        foreach (var line in geometry)
                        {
                            canvas.DrawLineString(line, brush);
                        }
                    }
                    else if (feature.GeometryType == Tile.GeomType.Polygon)
                    {
                        foreach (var polygon in geometry)
                        {
                            canvas.DrawPolygon(polygon, brush);
                        }
                    }
                    else if (feature.GeometryType == Tile.GeomType.Unknown)
                    {
                        canvas.DrawUnknown(geometry, brush);
                    }
                }
                catch (Exception)
                {
                    // an error
                }
            }
            else if (layer.Type == VisualLayerType.Raster)
            {
                canvas.DrawImage(layer.RasterStream, layer.Brush);
                layer.RasterStream.Close();
            }
        }

        foreach (var layer in visualLayers.OrderBy(item => item.Brush.ZIndex).Reverse())
        {
            if (layer.Type == VisualLayerType.Vector)
            {
                var feature = layer.VectorTileFeature;
                var geometry = layer.Geometry;
                var brush = layer.Brush;

                if (!brush.Paint.Visibility)
                {
                    continue;
                }

                if (feature.GeometryType == Tile.GeomType.Point)
                {
                    foreach (var point in geometry)
                    {
                        if (brush.Text != null)
                        {
                            canvas.DrawText(point.First(), brush);
                        }
                    }
                }
                else if (feature.GeometryType == Tile.GeomType.LineString)
                {
                    foreach (var line in geometry)
                    {
                        if (brush.Text != null)
                        {
                            canvas.DrawTextOnPath(line, brush);
                        }
                    }
                }
            }
        }

        return canvas.FinishDrawing();
    }

    static List<List<Coordinate>> LocalizeGeometry(List<List<Coordinate>> coordinates, double sizeX, double sizeY, double extent)
    {
        return coordinates.Select(list =>
        {
            return list.Select(point =>
            {
                var newPoint = new Coordinate
                {
                    X = 0,
                    Y = 0,
                };

                var x = Utils.ConvertRange(point.X, 0, extent, 0, sizeX, false);
                var y = Utils.ConvertRange(point.Y, 0, extent, 0, sizeY, false);

                newPoint.X = (long)x;
                newPoint.Y = (long)y;

                return newPoint;
            }).ToList();
        }).ToList();
    }
}