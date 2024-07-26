// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Text;

namespace BruTile.MbTiles.Vector;

internal static class Utils
{
    public static double ConvertRange(double oldValue, double oldMin, double oldMax, double newMin, double newMax, bool clamp = false)
    {
        double newValue;
        var oldRange = (oldMax - oldMin);
        if (oldRange == 0)
        {
            newValue = newMin;
        }
        else
        {
            var newRange = (newMax - newMin);
            newValue = (((oldValue - oldMin) * newRange) / oldRange) + newMin;
        }

        if (clamp)
        {
            newValue = Math.Min(Math.Max(newValue, newMin), newMax);
        }

        return newValue;
    }

    public static string Sha256(string randomString)
    {
        var crypt = System.Security.Cryptography.SHA256.Create();
        var hash = new System.Text.StringBuilder();
        var crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString));
        foreach (var theByte in crypto)
        {
            hash.Append(theByte.ToString("x2"));
        }
        return hash.ToString();
    }
}
