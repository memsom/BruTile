﻿// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System;
using BruTile.Predefined;
using BruTile.Web;
using BruTile.Wmsc;

namespace BruTile.Samples.Common.Samples;

public static class TileSourceForWmsSample
{
    public static ITileSource Create()
    {
        const string url = "http://geodata.nationaalgeoregister.nl/omgevingswarmte/wms?SERVICE=WMS&VERSION=1.1.1";
        // You need to know the schema. This can be a problem. Usually it is GlobalSphericalMercator
        var schema = new WkstNederlandSchema { Format = "image/png" };
        var wmscUrlBuilder = new WmscUrlBuilder(new Uri(url), schema, ["koudegeslotenwkobuurt"], []);
        return new HttpTileSource(schema, wmscUrlBuilder);
    }
}
