#nullable disable

using BruTile.MbTiles.Vector.VectorTileRenderer.Drawing;

namespace BruTile.MbTiles.Vector.VectorTileRenderer;

public class VectorTileFeature
{
    public double Extent { get; set; }
    public string GeometryType { get; set; }

    public Dictionary<string, object> Attributes = new();

    public List<List<TilePoint>> Geometry = new();
}