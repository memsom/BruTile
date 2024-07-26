// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace BruTile.MbTiles.Vector.Units;

public struct DoublePoint(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;

    public override bool Equals(object? obj)
    {
        if (obj is DoublePoint p)
        {
            return this == p;
        }

        return false;
    }

    public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() * 397);

    public override string ToString() => $"{{X={X.ToString(CultureInfo.InvariantCulture)} Y={Y.ToString(CultureInfo.InvariantCulture)}}}";

    public static bool operator ==(DoublePoint p1, DoublePoint p2) => (p1.X == p2.X) && (p1.Y == p2.Y);

    public static bool operator !=(DoublePoint p1, DoublePoint p2) => (p1.X != p2.X) || (p1.Y != p2.Y);

    public double Distance(DoublePoint other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));

    public DoublePoint Offset(double dx, double dy)
    {
        DoublePoint p = this;
        p.X += dx;
        p.Y += dy;
        return p;
    }

    public DoublePoint Round() => new(Math.Round(X), Math.Round(Y));

    public bool IsEmpty => (X == 0) && (Y == 0);

    public static explicit operator DoubleSize(DoublePoint pt) =>
        new DoubleSize(pt.X, pt.Y);

}
