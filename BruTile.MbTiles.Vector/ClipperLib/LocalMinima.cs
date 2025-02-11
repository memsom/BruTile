#nullable disable

namespace BruTile.MbTiles.Vector.ClipperLib;

internal class LocalMinima
{
    public long Y;

    public TEdge leftBound;

    public TEdge rightBound;

    public LocalMinima next;

    public LocalMinima()
    {
    }
}