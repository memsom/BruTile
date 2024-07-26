using System.Threading.Tasks;

namespace BruTile.MbTiles.Vector;

public interface IVectorTileSource: ILocalTileSource
{
    Task<VectorTile> GetVectorTileAsync(int x, int y, int zoom);
}
