#nullable disable

using System.Collections.Concurrent;
using System.Text;
using BruTile.MbTiles.Vector.ClipperLib;
using BruTile.MbTiles.Vector.VectorTileRenderer.Drawing;
using BruTile.MbTiles.Vector.VectorTileRenderer.Enums;
using Mapbox.Vector.Tile;
using SkiaSharp;

namespace BruTile.MbTiles.Vector.VectorTileRenderer;

public class SkiaCanvas : ICanvas
{
    int width;
    int height;

    SKSurface surface;
    SKCanvas canvas;

    public bool ClipOverflow { get; set; } = false;
    private Rect clipRectangle;
    List<Coordinate> clipRectanglePath;

    ConcurrentDictionary<string, SKTypeface> fontPairs = new ConcurrentDictionary<string, SKTypeface>();
    private static readonly Object fontLock = new Object();

    List<Rect> textRectangles = new List<Rect>();

    public void StartDrawing(double width, double height)
    {
        this.width = (int)width;
        this.height = (int)height;

        var info = new SKImageInfo(this.width, this.height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        surface = SKSurface.Create(info);
        canvas = surface.Canvas;

        double padding = -5;
        clipRectangle = new Rect(padding, padding, this.width - padding * 2, this.height - padding * 2);

        clipRectanglePath = new List<Coordinate>
        {
            new Coordinate {X = (long)clipRectangle.Top, Y = (long)clipRectangle.Left},
            new Coordinate {X = (long)clipRectangle.Top, Y = (long)clipRectangle.Right},
            new Coordinate {X = (long)clipRectangle.Bottom, Y = (long)clipRectangle.Right},
            new Coordinate {X = (long)clipRectangle.Bottom, Y = (long)clipRectangle.Left},
        };
    }

    public void DrawBackground(Brush style)
    {
        var color = new SKColor(style.Paint.BackgroundColor.Red, style.Paint.BackgroundColor.Green, style.Paint.BackgroundColor.Blue, style.Paint.BackgroundColor.Alpha);
        canvas.Clear(color);
    }

    SKStrokeCap ConvertCap(PenLineCap cap)
    {
        if (cap == PenLineCap.Flat)
        {
            return SKStrokeCap.Butt;
        }
        else if (cap == PenLineCap.Round)
        {
            return SKStrokeCap.Round;
        }

        return SKStrokeCap.Square;
    }

    double Clamp(double number, double min = 0, double max = 1)
    {
        return Math.Max(min, Math.Min(max, number));
    }

    List<List<Coordinate>> ClipPolygon(List<Coordinate> geometry) // may break polygons into multiple ones
    {
        var c = new Clipper();

        var polygon = new List<Coordinate>();

        foreach (var point in geometry)
        {
            polygon.Add(new Coordinate
            {
                X = point.X,
                Y = point.Y
            });
        }

        c.AddPolygon(polygon, PolyType.ptSubject);

        c.AddPolygon(clipRectanglePath, PolyType.ptClip);

        var solution = new List<List<Coordinate>>();

        var success = c.Execute(ClipType.ctIntersection, solution, PolyFillType.pftNonZero, PolyFillType.pftEvenOdd);

        if (success && solution.Count > 0)
        {
            var result = solution.Select(s =>
                s.Select(item =>
                    new Coordinate
                    {
                        X = item.X,
                        Y = item.Y
                    }).ToList()).ToList();
            return result;
        }

        return null;
    }

    List<Coordinate> ClipLine(List<Coordinate> geometry) => LineClipper.ClipPolyline(geometry, clipRectangle);

    SKPath GetPathFromGeometry(List<Coordinate> geometry)
    {
        SKPath path = new SKPath
        {
            FillType = SKPathFillType.EvenOdd,
        };

        var firstPoint = geometry[0];

        path.MoveTo((float)firstPoint.X, (float)firstPoint.Y);
        foreach (var point in geometry.Skip(1))
        {
            var lastPoint = path.LastPoint;
            path.LineTo((float)point.X, (float)point.Y);
        }

        return path;
    }

    public void DrawLineString(List<Coordinate> geometry, Brush style)
    {
        if (ClipOverflow)
        {
            geometry = ClipLine(geometry);
            if (geometry == null)
            {
                return;
            }
        }

        var path = GetPathFromGeometry(geometry);
        if (path == null)
        {
            return;
        }

        var color = style.Paint.LineColor;

        SKPaint fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = ConvertCap(style.Paint.LineCap),
            StrokeWidth = (float)style.Paint.LineWidth,
            Color = new SKColor(color.Red, color.Green, color.Blue, (byte)Clamp(color.Alpha * style.Paint.LineOpacity, 0, 255)),
            IsAntialias = true,
        };

        if (style.Paint.LineDashArray.Count() > 0)
        {
            var effect = SKPathEffect.CreateDash(style.Paint.LineDashArray.Select(n => (float)n).ToArray(), 0);
            fillPaint.PathEffect = effect;
        }

        //Console.WriteLine(style.Paint.LineWidth);

        canvas.DrawPath(path, fillPaint);
    }

    SKTextAlign ConvertAlignment(TextAlignment alignment)
    {
        if (alignment == TextAlignment.Center)
        {
            return SKTextAlign.Center;
        }
        else if (alignment == TextAlignment.Left)
        {
            return SKTextAlign.Left;
        }
        else if (alignment == TextAlignment.Right)
        {
            return SKTextAlign.Right;
        }

        return SKTextAlign.Center;
    }

    SKPaint GetTextStrokePaint(Brush style)
    {
        var color = style.Paint.TextStrokeColor;

        var paint = new SKPaint()
        {
            IsStroke = true,
            StrokeWidth = (float)style.Paint.TextStrokeWidth,
            Color = new SKColor(color.Red, color.Green, color.Blue, (byte)Clamp(color.Alpha * style.Paint.TextOpacity, 0, 255)),
            TextSize = (float)style.Paint.TextSize,
            IsAntialias = true,
            TextEncoding = SKTextEncoding.Utf32,
            TextAlign = ConvertAlignment(style.Paint.TextJustify),
            Typeface = GetFont(style.Paint.TextFont, style),
        };

        return paint;
    }

    SKPaint GetTextPaint(Brush style)
    {
        var color = style.Paint.TextColor;
        var paint = new SKPaint()
        {
            Color = new SKColor(color.Red, color.Green, color.Blue, (byte)Clamp(color.Alpha * style.Paint.TextOpacity, 0, 255)),
            TextSize = (float)style.Paint.TextSize,
            IsAntialias = true,
            TextEncoding = SKTextEncoding.Utf32,
            TextAlign = ConvertAlignment(style.Paint.TextJustify),
            Typeface = GetFont(style.Paint.TextFont, style),
            HintingLevel = SKPaintHinting.Normal,
        };

        return paint;
    }

    string TransformText(string text, Brush style)
    {
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (style.Paint.TextTransform == TextTransform.Uppercase)
        {
            text = text.ToUpper();
        }
        else if (style.Paint.TextTransform == TextTransform.Lowercase)
        {
            text = text.ToLower();
        }

        var paint = GetTextPaint(style);
        text = BreakText(text, paint, style);

        return text;
        //return Encoding.UTF32.GetBytes(newText);
    }

    string BreakText(string input, SKPaint paint, Brush style)
    {
        var restOfText = input;
        var brokenText = string.Empty;
        do
        {
            var lineLength = paint.BreakText(restOfText, (float)(style.Paint.TextMaxWidth * style.Paint.TextSize));

            if (lineLength == restOfText.Length)
            {
                // its the end
                brokenText += restOfText.Trim();
                break;
            }

            var lastSpaceIndex = restOfText.LastIndexOf(' ', (int)(lineLength - 1));
            if (lastSpaceIndex == -1 || lastSpaceIndex == 0)
            {
                // no more spaces, probably ;)
                brokenText += restOfText.Trim();
                break;
            }

            brokenText += restOfText.Substring(0, (int)lastSpaceIndex).Trim() + "\n";

            restOfText = restOfText.Substring((int)lastSpaceIndex, restOfText.Length - (int)lastSpaceIndex);
        } while (restOfText.Length > 0);

        return brokenText.Trim();
    }

    bool TextCollides(Rect rectangle)
    {
        foreach (var rect in textRectangles)
        {
            if (rect.IntersectsWith(rectangle))
            {
                return true;
            }
        }

        return false;
    }

    SKTypeface GetFont(string[] familyNames, Brush style)
    {
        lock (fontLock)
        {
            foreach (var name in familyNames)
            {
                if (fontPairs.ContainsKey(name))
                {
                    return fontPairs[name];
                }


                // check file system for embedded fonts

                if (VectorStyleReader.TryGetFont(name, out var stream))
                {
                    var newType = SKTypeface.FromStream(stream);
                    if (newType != null)
                    {
                        fontPairs[name] = newType;
                        return newType;
                    }
                }
                //}

                var typeface = SKTypeface.FromFamilyName(name);
                if (typeface.FamilyName == name)
                {
                    // gotcha!
                    fontPairs[name] = typeface;
                    return typeface;
                }
            }

            // all options exhausted...
            // get the first one
            var fallback = SKTypeface.FromFamilyName(familyNames.First());
            fontPairs[familyNames.First()] = fallback;
            return fallback;
        }
    }

    SKTypeface QualifyTypeface(string text, SKTypeface typeface)
    {
        var glyphs = new ushort[typeface.CountGlyphs(text)];
        if (glyphs.Length < text.Length)
        {
            var fm = SKFontManager.Default;
            var charIdx = (glyphs.Length > 0) ? glyphs.Length : 0;
            return fm.MatchCharacter(text[glyphs.Length]);
        }

        return typeface;
    }

    void QualifyTypeface(Brush style, SKPaint paint)
    {
        var glyphs = new ushort[paint.Typeface.CountGlyphs(style.Text)];
        if (glyphs.Length < style.Text.Length)
        {
            var fm = SKFontManager.Default;
            var charIdx = (glyphs.Length > 0) ? glyphs.Length : 0;
            var newTypeface = fm.MatchCharacter(style.Text[glyphs.Length]);

            if (newTypeface == null)
            {
                return;
            }

            paint.Typeface = newTypeface;

            glyphs = new ushort[newTypeface.CountGlyphs(style.Text)];
            if (glyphs.Length < style.Text.Length)
            {
                // still causing issues
                // so we cut the rest
                charIdx = (glyphs.Length > 0) ? glyphs.Length : 0;

                style.Text = style.Text.Substring(0, charIdx);
            }
        }
    }

    public void DrawText(Coordinate geometry, Brush style)
    {
        if (style.Paint.TextOptional)
        {
            // TODO check symbol collision
            //return;
        }

        var paint = GetTextPaint(style);
        QualifyTypeface(style, paint);

        var strokePaint = GetTextStrokePaint(style);
        var text = TransformText(style.Text, style);
        var allLines = text.Split('\n');

        //paint.Typeface = qualifyTypeface(text, paint.Typeface);

        // detect collisions
        if (allLines.Length > 0)
        {
            var biggestLine = allLines.OrderBy(line => line.Length).Last();
            var bytes = Encoding.UTF32.GetBytes(biggestLine);

            var width = (int)(paint.MeasureText(bytes));
            int left = (int)(geometry.X - width / 2);
            int top = (int)(geometry.Y - style.Paint.TextSize / 2 * allLines.Length);
            int height = (int)(style.Paint.TextSize * allLines.Length);

            var rectangle = new Rect(left, top, width, height);
            rectangle.Inflate(5, 5);

            if (ClipOverflow)
            {
                if (!clipRectangle.Contains(rectangle))
                {
                    return;
                }
            }

            if (TextCollides(rectangle))
            {
                // collision detected
                return;
            }

            textRectangles.Add(rectangle);

            //var list = new List<Point>()
            //{
            //    rectangle.TopLeft,
            //    rectangle.TopRight,
            //    rectangle.BottomRight,
            //    rectangle.BottomLeft,
            //};

            //var brush = new Brush();
            //brush.Paint = new Paint();
            //brush.Paint.FillColor = Color.FromArgb(150, 255, 0, 0);

            //this.DrawPolygon(list, brush);
        }

        int i = 0;
        foreach (var line in allLines)
        {
            float lineOffset = (float)(i * style.Paint.TextSize) - ((float)(allLines.Length) * (float)style.Paint.TextSize) / 2 + (float)style.Paint.TextSize;
            var position = new SKPoint((float)geometry.X + (float)(style.Paint.TextOffset.X * style.Paint.TextSize), (float)geometry.Y + (float)(style.Paint.TextOffset.Y * style.Paint.TextSize) + lineOffset);

            if (style.Paint.TextStrokeWidth != 0)
            {
                canvas.DrawText(line, position, strokePaint);
            }

            canvas.DrawText(line, position, paint);
            i++;
        }
    }

    double GetPathLength(List<Coordinate> path)
    {
        double distance = 0;
        for (var i = 0; i < path.Count - 2; i++)
        {
            var v = Subtract(path[i], path[i + 1]);
            var length = v.Length;
            distance += length;
        }

        return distance;
    }

    public Drawing.Vector Subtract(Coordinate point1, Coordinate point2)
    {
        return new Drawing.Vector(point1.X - point2.X, point1.Y - point2.Y);
    }

    double GetAbsoluteDiff2Angles(double x, double y, double c = Math.PI)
    {
        return c - Math.Abs((Math.Abs(x - y) % 2 * c) - c);
    }

    bool CheckPathSqueezing(List<Coordinate> path, double textHeight)
    {
        double previousAngle = 0;
        for (var i = 0; i < path.Count - 2; i++)
        {
            var vector = Subtract(path[i], path[i + 1]);

            var angle = Math.Atan2(vector.Y, vector.X);
            var angleDiff = Math.Abs(GetAbsoluteDiff2Angles(angle, previousAngle));

            if (angleDiff > Math.PI / 3)
            {
                return true;
            }

            previousAngle = angle;
        }

        return false;
    }

    void DebugRectangle(Rect rectangle, SKColor color)
    {
        var list = new List<Coordinate>()
        {
            new() {X = (long)rectangle.Top, Y = (long)rectangle.Left},
            new() {X = (long)rectangle.Top, Y = (long)rectangle.Right},
            new() {X = (long)rectangle.Bottom, Y = (long)rectangle.Right},
            new() {X = (long)rectangle.Bottom, Y = (long)rectangle.Left},
        };

        var brush = new Brush
        {
            Paint = new Paint
            {
                FillColor = color
            }
        };

        this.DrawPolygon(list, brush);
    }

    public void DrawTextOnPath(List<Coordinate> geometry, Brush style)
    {
        // buggggyyyyyy
        // requires an amazing collision system to work :/
        // --
        //return;

        //if (ClipOverflow)
        //{
        geometry = ClipLine(geometry);
        if (geometry == null)
        {
            return;
        }
        //}

        var path = GetPathFromGeometry(geometry);
        var text = TransformText(style.Text, style);

        var pathSqueezed = CheckPathSqueezing(geometry, style.Paint.TextSize);

        if (pathSqueezed)
        {
            return;
        }

        //text += " : " + bending.ToString("F");

        var bounds = path.Bounds;

        var left = bounds.Left - style.Paint.TextSize;
        var top = bounds.Top - style.Paint.TextSize;
        var right = bounds.Right + style.Paint.TextSize;
        var bottom = bounds.Bottom + style.Paint.TextSize;

        var rectangle = new Rect(left, top, right - left, bottom - top);


        if (TextCollides(rectangle))
        {
            // collides with other
            return;
        }

        textRectangles.Add(rectangle);

        if (style.Text.Length * style.Paint.TextSize * 0.2 >= GetPathLength(geometry))
        {
            // exceeds estimated path length
            return;
        }

        var offset = new SKPoint((float)style.Paint.TextOffset.X, (float)style.Paint.TextOffset.Y);
        if (style.Paint.TextStrokeWidth != 0)
        {
            // TODO implement this func custom way...
            canvas.DrawTextOnPath(text, path, offset, GetTextStrokePaint(style));
        }

        canvas.DrawTextOnPath(text, path, offset, GetTextPaint(style));


        //canvas.DrawText(Encoding.UTF32.GetBytes(bending.ToString("F")), new SKPoint((float)left + 10, (float)top + 10), getTextStrokePaint(style));
        //canvas.DrawText(Encoding.UTF32.GetBytes(bending.ToString("F")), new SKPoint((float)left + 10, (float)top + 10), getTextPaint(style));
    }

    public void DrawPoint(Coordinate geometry, Brush style)
    {
        if (style.Paint.IconImage != null)
        {
            // draw icon here
        }
    }

    public void DrawPolygon(List<Coordinate> geometry, Brush style)
    {
        List<List<Coordinate>> allGeometries = null;
        if (ClipOverflow)
        {
            allGeometries = ClipPolygon(geometry);
        }
        else
        {
            allGeometries = new List<List<Coordinate>>() {geometry};
        }

        if (allGeometries == null)
        {
            return;
        }

        foreach (var geometryPart in allGeometries)
        {
            var path = GetPathFromGeometry(geometryPart);
            if (path == null)
            {
                return;
            }

            var color = style.Paint.FillColor;
            SKPaint fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                StrokeCap = ConvertCap(style.Paint.LineCap),
                Color = new SKColor(color.Red, color.Green, color.Blue, (byte)Clamp(color.Alpha * style.Paint.FillOpacity, 0, 255)),
                IsAntialias = true,
            };

            canvas.DrawPath(path, fillPaint);
        }
    }

    public void DrawImage(Stream imageStream, Brush style)
    {
        try
        {
            if (imageStream.CanSeek && imageStream.Position != 0)
            {
                imageStream.Seek(0, SeekOrigin.Begin);
            }

            var image = SKBitmap.Decode(imageStream);
            canvas.DrawBitmap(image, new SKPoint(0, 0));
        }
        catch (Exception)
        {
            // something went wrong with the image format
        }
    }

    public void DrawUnknown(List<List<Coordinate>> geometry, Brush style)
    {
    }

    public byte[] FinishDrawing()
    {
        using (var image = surface.Snapshot())
        using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
        using (var result = new MemoryStream())
        {
            data.SaveTo(result);
            return result.ToArray();
        }
    }
}