namespace BruTile.MbTiles.Vector.VectorTileRenderer.Sources;

public interface IVectorTileSource
{
    Task<VectorTile> GetVectorTile();
}