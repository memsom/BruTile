#nullable disable

using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.ClipperLib;

internal class JoinRec
{
    public Coordinate pt1a;

    public Coordinate pt1b;

    public int poly1Idx;

    public Coordinate pt2a;

    public Coordinate pt2b;

    public int poly2Idx;
}