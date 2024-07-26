// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Drawing;
using BruTile.MbTiles.Vector.Units;

namespace BruTile.MbTiles.Vector;

public class VectorTileFeature
{
    public double Extent { get; set; }
    public string? GeometryType { get; set; }

    public Dictionary<string, object> Attributes { get; set; } = new();

    public List<List<DoublePoint>> Geometry { get; set; } = new();
}
