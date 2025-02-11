namespace BruTile.MbTiles.Vector.VectorTileRenderer.Enums;

[Flags]
public enum OutCode
{
    Inside = 0,
    Left = 1,
    Right = 2,
    Bottom = 4,
    Top = 8
}