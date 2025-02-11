#nullable disable

namespace BruTile.MbTiles.Vector.ClipperLib;

internal class ClipperException : Exception
{
    public ClipperException(string description) : base(description)
    {
    }
}