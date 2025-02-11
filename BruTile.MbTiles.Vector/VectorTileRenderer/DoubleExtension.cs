namespace BruTile.MbTiles.Vector.VectorTileRenderer;

static class DoubleExtension
{
    internal static bool BasicallyEqualTo(this double a, double b)
    {
        return a.BasicallyEqualTo(b, 0.0001);
    }

    internal static bool BasicallyEqualTo(this double a, double b, double precision)
    {
        return Math.Abs(a - b) <= precision;
    }
}