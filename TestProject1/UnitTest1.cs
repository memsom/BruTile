using BruTile;
using BruTile.MbTiles;
using BruTile.MbTiles.Vector;
using NUnit.Framework;
using SQLite;
using TestProject1.Utilities;

namespace TestProject1;

[TestFixture]
public class UnitTest1
{
    [SetUp]
    public void TestSetUp()
    {
        SQLitePCL.Batteries.Init();
    }

    [Test]
    public async Task FetchTiles()
    {
        // Arrange
        var path = Path.Combine(Paths.AssemblyDirectory, "Resources", "zurich.mbtiles");
        var tileSource = new MbTilesVectorTileSource(new SQLiteConnectionString(path, false, null));
        var extent = tileSource.Schema.Extent;
        var tileInfos = tileSource.Schema.GetTileInfos(extent, 1).ToList();
        tileSource.Attribution = new Attribution("attribution", "url");

        // Act
        var data = await tileSource.GetTileAsync(tileInfos.First()).ConfigureAwait(false);

        // Assert
        Assert.IsNotNull(data);
        Assert.True(data?.Length > 0);
        Assert.AreEqual(MbTilesType.BaseLayer, tileSource.Type);
        Assert.AreEqual("attribution", tileSource.Attribution.Text);
    }
}