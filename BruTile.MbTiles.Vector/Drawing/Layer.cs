using System.Collections.Generic;

namespace BruTile.MbTiles.Vector.Drawing;

public class Layer
{
    public int Index { get; set; } = -1;
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public Source? Source { get; set; } = null;
    public string SourceLayer { get; set; } = string.Empty;
    public Dictionary<string, object> Paint { get; set; } = new();
    public Dictionary<string, object> Layout { get; set; } = new();
    public object[] Filter { get; set; } = [];
    public double? MinZoom { get; set; } = null;
    public double? MaxZoom { get; set; } = null;
}
