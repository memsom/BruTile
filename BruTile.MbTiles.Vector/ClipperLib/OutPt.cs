#nullable disable

using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.ClipperLib;

public class OutPt
{
    public int idx;

    public Coordinate pt;

    public OutPt next;

    public OutPt prev;

    public OutPt()
    {
    }
}