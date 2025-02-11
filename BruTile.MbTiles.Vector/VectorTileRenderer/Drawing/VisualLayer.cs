#nullable disable
using BruTile.MbTiles.Vector.VectorTileRenderer.Enums;
using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.VectorTileRenderer.Drawing;

public class VisualLayer
{
    public VisualLayerType Type { get; set; }

    public Stream RasterStream { get; set; } = null;

    public VectorTileFeature VectorTileFeature { get; set; } = null;

    public List<List<Coordinate>> Geometry { get; set; } = null;

    public Brush Brush { get; set; } = null;
}