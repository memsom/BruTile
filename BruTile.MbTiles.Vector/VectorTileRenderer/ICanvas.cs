using BruTile.MbTiles.Vector.VectorTileRenderer.Drawing;
using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.VectorTileRenderer;

public interface ICanvas
{
    bool ClipOverflow { get; set; }

    void StartDrawing(double sizeX, double sizeY);

    void DrawBackground(Brush style);

    void DrawLineString(List<Coordinate> geometry, Brush style);

    void DrawPolygon(List<Coordinate> geometry, Brush style);

    void DrawPoint(Coordinate geometry, Brush style);

    void DrawText(Coordinate geometry, Brush style);

    void DrawTextOnPath(List<Coordinate> geometry, Brush style);

    void DrawImage(Stream imageStream, Brush style);

    void DrawUnknown(List<List<Coordinate>> geometry, Brush style);

    byte[] FinishDrawing();
}