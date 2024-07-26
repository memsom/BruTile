// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace BruTile.MbTiles.Vector.Units;

public struct DoubleSize
{
    private double width;
    private double height;

    public static readonly DoubleSize Zero;

    public DoubleSize(double width, double height)
    {
        if (double.IsNaN(width))
        {
            throw new ArgumentException("NaN is not a valid value for width");
        }

        if (double.IsNaN(height))
        {
            throw new ArgumentException("NaN is not a valid value for height");
        }

        this.width = width;
        this.height = height;
    }

    public bool IsZero => (width == 0) && (height == 0);

    public double Width
    {
        get => width;
        set
        {
            if (double.IsNaN(value))
            {
                throw new ArgumentException("NaN is not a valid value for Width");
            }

            width = value;
        }
    }

    public double Height
    {
        get => height;
        set
        {
            if (double.IsNaN(value))
            {
                throw new ArgumentException("NaN is not a valid value for Height");
            }

            height = value;
        }
    }

    public static DoubleSize operator +(DoubleSize s1, DoubleSize s2) =>
        new DoubleSize(s1.width + s2.width, s1.height + s2.height);


    public static DoubleSize operator -(DoubleSize s1, DoubleSize s2) =>
        new DoubleSize(s1.width - s2.width, s1.height - s2.height);


    public static DoubleSize operator *(DoubleSize s1, double value) =>
        new DoubleSize(s1.width * value, s1.height * value);


    public static bool operator ==(DoubleSize s1, DoubleSize s2) =>
        (s1.width == s2.width) && (s1.height == s2.height);


    public static bool operator !=(DoubleSize s1, DoubleSize s2) =>
        (s1.width != s2.width) || (s1.height != s2.height);


    public static explicit operator DoublePoint(DoubleSize size) =>
        new DoublePoint(size.Width, size.Height);


    public bool Equals(DoubleSize other) => width.Equals(other.width) && height.Equals(other.height);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        return obj is DoubleSize && Equals((DoubleSize)obj);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Bug", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "Third party code")]
    public override int GetHashCode()
    {
        unchecked
        {
            return (width.GetHashCode() * 397) ^ height.GetHashCode();
        }
    }

    public override string ToString()
    {
        return $"{{Width={width.ToString(CultureInfo.InvariantCulture)} Height={height.ToString(CultureInfo.InvariantCulture)}}}";
    }

    public void Deconstruct(out double widthValue, out double heightValue)
    {
        widthValue = Width;
        heightValue = Height;
    }
}
