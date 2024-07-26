using System;

namespace BruTile.MbTiles.Vector.Enums;

[Flags]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Critical Code Smell", "S2346:Flags enumerations zero-value members should be named \"None\"", Justification = "Third party")]
public enum OutCode
{
    Inside = 0,
    Left = 1,
    Right = 2,
    Bottom = 4,
    Top = 8
}
