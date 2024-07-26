// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using BruTile.MbTiles.Vector.Units;

namespace BruTile.MbTiles.Vector;

public class VectorTile
{
    public bool IsOverZoomed { get; set; } = false;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1104:Fields should not have public accessibility", Justification = "<Pending>")]
    public List<VectorTileFeatures> Layers = new List<VectorTileFeatures>();

    public VectorTile ApplyExtent(DoubleRect extent)
    {
        var newTile = new VectorTile
        {
            IsOverZoomed = this.IsOverZoomed
        };

        foreach (var layer in Layers)
        {
            var vectorLayer = new VectorTileFeatures()
            {
                Name = layer.Name
            };

            foreach (var feature in layer.Features)
            {
                var vectorFeature = new VectorTileFeature
                {
                    Attributes = new Dictionary<string, object>(feature.Attributes),
                    Extent = feature.Extent,
                    GeometryType = feature.GeometryType
                };

                var vectorGeometry = new List<List<DoublePoint>>();
                foreach (var geometry in feature.Geometry)
                {
                    var vectorPoints = new List<DoublePoint>();

                    foreach (var point in geometry)
                    {
                        var newX = Utils.ConvertRange(point.X, extent.Left, extent.Right, 0, vectorFeature.Extent);
                        var newY = Utils.ConvertRange(point.Y, extent.Top, extent.Bottom, 0, vectorFeature.Extent);

                        vectorPoints.Add(new DoublePoint(newX, newY));
                    }

                    vectorGeometry.Add(vectorPoints);
                }

                vectorFeature.Geometry = vectorGeometry;
                vectorLayer.Features.Add(vectorFeature);
            }

            newTile.Layers.Add(vectorLayer);
        }

        return newTile;
    }
}
