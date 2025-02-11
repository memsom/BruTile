#nullable disable

using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.ClipperLib;

internal class IntersectNode
{
    public TEdge edge1;

    public TEdge edge2;

    public Coordinate pt;

    public IntersectNode next;

    public IntersectNode()
    {
    }
}