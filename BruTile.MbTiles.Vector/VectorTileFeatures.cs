// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace BruTile.MbTiles.Vector;

public class VectorTileFeatures
{
    public string? Name { get; set; }

    public List<VectorTileFeature> Features { get; } = new();
}
