using BruTile.MbTiles.Vector.VectorTileRenderer.Drawing;
using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.VectorTileRenderer;

public class VectorTile
{
    public bool IsOverZoomed { get; set; } = false;
    public List<VectorTileLayer> Layers = new();

    public VectorTile ApplyExtent(Rect extent)
    {
        var newTile = new VectorTile
        {
            IsOverZoomed = IsOverZoomed
        };

        foreach (var layer in Layers)
        {
            var vectorLayer = new VectorTileLayer
            {
                Name = layer.Name
            };

            foreach (var feature in layer.VectorTileFeatures)
            {
                var vectorFeature = new VectorTileFeature
                {
                    Attributes = [..feature.Attributes],
                    Extent = feature.Extent,
                    GeometryType = feature.GeometryType
                };

                var vectorGeometry = new List<List<Coordinate>>();
                foreach (var geometry in feature.Geometry)
                {
                    var vectorPoints = new List<Coordinate>();

                    foreach (var point in geometry)
                    {
                        var newX = Utils.ConvertRange(point.X, extent.Left, extent.Right, 0, vectorFeature.Extent);
                        var newY = Utils.ConvertRange(point.Y, extent.Top, extent.Bottom, 0, vectorFeature.Extent);

                        vectorPoints.Add(
                            new Coordinate
                            {
                                X = (long)newX,
                                Y = (long)newY,
                            });
                    }

                    vectorGeometry.Add(vectorPoints);
                }

                vectorFeature.Geometry = vectorGeometry;
                vectorLayer.VectorTileFeatures.Add(vectorFeature);
            }

            newTile.Layers.Add(vectorLayer);
        }

        return newTile;
    }
}