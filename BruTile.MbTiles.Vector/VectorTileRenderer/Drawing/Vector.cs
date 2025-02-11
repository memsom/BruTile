using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.VectorTileRenderer.Drawing;

// based on Xamarin Forms
public struct Vector
{
    public Vector(double x, double y)
        : this()
    {
        X = x;
        Y = y;
    }

    public double X { private set; get; }
    public double Y { private set; get; }

    public double LengthSquared => X * X + Y * Y;

    public double Length => Math.Sqrt(LengthSquared);

    public Vector Normalized
    {
        get
        {
            double length = Length;

            if (length != 0)
            {
                return new Vector(X / length, Y / length);
            }
            return new Vector();
        }
    }

    public static double AngleBetween(Vector v1, Vector v2)
    {
        return 180 * (Math.Atan2(v2.Y, v2.X) - Math.Atan2(v1.Y, v1.X)) / Math.PI;
    }

    public static explicit operator Coordinate(Vector v)
    {
        return new Coordinate
        {
            X = (long)v.X,
            Y = (long)v.Y

        };
    }

    public override string ToString()
    {
        return $"{X},{Y}";
    }
}