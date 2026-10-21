using System;
using System.Collections.Generic;
using System.Linq;
using WinTempCleaner.Core.Discovery;
using Xunit;

namespace Deltempo.Tests;

public class TreemapLayoutEngineTests
{
    private class TestItem
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    [Fact]
    public void CalculateLayout_EmptyItems_ReturnsEmptyList()
    {
        var items = new List<TestItem>();
        var layout = TreemapLayoutEngine.CalculateLayout(items, x => x.Size, 800, 600);

        Assert.Empty(layout);
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    [InlineData(-100, 600)]
    public void CalculateLayout_InvalidDimensions_ReturnsEmptyList(double w, double h)
    {
        var items = new List<TestItem>
        {
            new() { Name = "file1", Size = 100 }
        };

        var layout = TreemapLayoutEngine.CalculateLayout(items, x => x.Size, w, h);

        Assert.Empty(layout);
    }

    [Fact]
    public void CalculateLayout_SingleItem_OccupiesFullViewport()
    {
        var items = new List<TestItem>
        {
            new() { Name = "single", Size = 1024 }
        };

        var layout = TreemapLayoutEngine.CalculateLayout(items, x => x.Size, 800, 600);

        Assert.Single(layout);
        var node = layout[0];
        Assert.Equal(0, node.X);
        Assert.Equal(0, node.Y);
        Assert.Equal(800, node.Width);
        Assert.Equal(600, node.Height);
    }

    [Fact]
    public void CalculateLayout_MultipleItems_AllNodesContainedWithinBounds()
    {
        double viewportW = 1000;
        double viewportH = 800;

        var items = new List<TestItem>
        {
            new() { Name = "A", Size = 500 },
            new() { Name = "B", Size = 300 },
            new() { Name = "C", Size = 150 },
            new() { Name = "D", Size = 50 }
        };

        var layout = TreemapLayoutEngine.CalculateLayout(items, x => x.Size, viewportW, viewportH);

        Assert.Equal(4, layout.Count);

        foreach (var node in layout)
        {
            Assert.True(node.X >= -0.01, $"Node X ({node.X}) < 0");
            Assert.True(node.Y >= -0.01, $"Node Y ({node.Y}) < 0");
            Assert.True(node.X + node.Width <= viewportW + 1.0, $"Node right ({node.X + node.Width}) > {viewportW}");
            Assert.True(node.Y + node.Height <= viewportH + 1.0, $"Node bottom ({node.Y + node.Height}) > {viewportH}");
            Assert.True(node.Width > 0);
            Assert.True(node.Height > 0);
        }
    }

    [Fact]
    public void CalculateLayout_TotalArea_SumsToTotalViewportArea()
    {
        double viewportW = 500;
        double viewportH = 400;
        double expectedTotalArea = viewportW * viewportH;

        var items = new List<TestItem>
        {
            new() { Name = "X", Size = 600 },
            new() { Name = "Y", Size = 400 }
        };

        var layout = TreemapLayoutEngine.CalculateLayout(items, x => x.Size, viewportW, viewportH);

        double totalCalculatedArea = layout.Sum(n => n.Width * n.Height);
        Assert.Equal(expectedTotalArea, totalCalculatedArea, precision: 1);
    }

    [Fact]
    public void CalculateLayout_MaxTiles_LimitsCountCorrectly()
    {
        var items = Enumerable.Range(1, 100).Select(i => new TestItem
        {
            Name = $"File_{i}",
            Size = i * 10
        }).ToList();

        var layout = TreemapLayoutEngine.CalculateLayout(items, x => x.Size, 800, 600, maxTiles: 20);

        Assert.Equal(20, layout.Count);
        // Largest item (File_100) must be included
        Assert.Contains(layout, n => n.Item.Name == "File_100");
    }
}
