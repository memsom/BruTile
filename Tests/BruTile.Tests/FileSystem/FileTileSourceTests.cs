﻿// Copyright (c) BruTile developers team. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using BruTile.Cache;
using BruTile.FileSystem;
using NUnit.Framework;

namespace BruTile.Tests.FileSystem;

[TestFixture]
public class FileTileSourceTests
{
    [Test]
    public async Task GetTileWhenTilePresentShouldReturnTile()
    {
        // Arrange
        var tileCache = new FileCache(".\\FileCacheTest", "png", new TimeSpan(long.MaxValue));
        tileCache.Add(new TileIndex(4, 5, 8), new byte[243]);
        var fileTileSource = new FileTileSource(".\\FileCacheTest", "png", new TimeSpan(long.MaxValue));

        // Act
        var tile = await fileTileSource.GetTileAsync(new TileInfo { Index = new TileIndex(4, 5, 8) })
            .ConfigureAwait(false);

        // Assert
        Assert.AreEqual(tile.Length, 243);
    }
}
