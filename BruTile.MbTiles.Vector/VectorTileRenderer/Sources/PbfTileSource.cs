#nullable disable

using System.IO.Compression;
using Mapbox.Vector.Tile;

namespace BruTile.MbTiles.Vector.VectorTileRenderer.Sources;

public class PbfTileSource : IVectorTileSource
{
    private readonly Stream stream;

    private PbfTileSource(Stream stream)
    {
        this.stream = stream;
    }

    public PbfTileSource(byte[] bytes) : this(new MemoryStream(bytes))
    {
    }

    public async Task<VectorTile> GetVectorTile()
    {
        if (stream != null)
        {
            return await ProcessStream();
        }

        return null;
    }

    async Task<VectorTile> ProcessStream()
    {
        if (IsGZipped(stream))
        {
            await using var zipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            await zipStream.CopyToAsync(resultStream);
            resultStream.Seek(0, SeekOrigin.Begin);
            return LoadStream(resultStream);
        }

        return LoadStream(stream);
    }

    VectorTile LoadStream(Stream stream)
    {
        var mbLayers = Mapbox.Vector.Tile.VectorTileParser.Parse(stream);

        return BaseTileToVector(mbLayers);
    }

    static VectorTile BaseTileToVector(object baseTile)
    {
        var tile = (List<Mapbox.Vector.Tile.VectorTileLayer>)baseTile;
        var result = new VectorTile();

        foreach (var vectorTileLayer in tile)
        {
            var vectorLayer = new VectorTileLayer
            {
                Name = vectorTileLayer.Name,
            };

            for (var i = 0; i < vectorTileLayer.VectorTileFeatures.Count; i++)
            {
                var feat = vectorTileLayer.VectorTileFeatures[i];

                var vectorFeature = new VectorTileFeature
                {
                    Extent = 1,
                    GeometryType = feat.GeometryType,
                    Attributes = feat.Attributes,
                };

                var vectorGeometry = new List<List<Coordinate>>();

                foreach (var points in feat.Geometry)
                {
                    var vectorPoints = new List<Coordinate>();

                    foreach (var coordinate in points)
                    {
                        var dX = coordinate.X / (double)feat.Extent;
                        var dY = coordinate.Y / (double)feat.Extent;

                        vectorPoints.Add(new Coordinate
                        {
                            X = (long)dX,
                            Y = (long)dY,
                        });
                    }

                    vectorGeometry.Add(vectorPoints);
                }

                vectorFeature.Geometry = vectorGeometry;
                vectorLayer.VectorTileFeatures.Add(vectorFeature);
            }

            result.Layers.Add(vectorLayer);
        }

        return result;
    }

    byte[] ReadTillEnd(Stream input)
    {
        byte[] buffer = new byte[16 * 1024];
        using (MemoryStream ms = new MemoryStream())
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            return ms.ToArray();
        }
    }

    bool IsGZipped(Stream stream)
    {
        return IsZipped(stream, 3, "1F-8B-08");
    }

    bool IsZipped(Stream stream, int signatureSize = 4, string expectedSignature = "50-4B-03-04")
    {
        if (stream.Length < signatureSize) return false;
        byte[] signature = new byte[signatureSize];
        int bytesRequired = signatureSize;
        int index = 0;
        while (bytesRequired > 0)
        {
            int bytesRead = stream.Read(signature, index, bytesRequired);
            bytesRequired -= bytesRead;
            index += bytesRead;
        }

        stream.Seek(0, SeekOrigin.Begin);
        string actualSignature = BitConverter.ToString(signature);
        if (actualSignature == expectedSignature) return true;
        return false;
    }
}