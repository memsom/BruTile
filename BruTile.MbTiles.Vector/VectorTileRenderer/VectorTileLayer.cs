#nullable disable

namespace BruTile.MbTiles.Vector.VectorTileRenderer;

public class VectorTileLayer
{
    public string Name { get; set; }

    public List<VectorTileFeature> Features { get; } = new();
}