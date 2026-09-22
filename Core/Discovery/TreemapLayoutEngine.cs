using System;
using System.Collections.Generic;
using System.Linq;

namespace WinTempCleaner.Core.Discovery;

public interface ITreemapItem
{
    string Id { get; }
    string Title { get; }
    long SizeBytes { get; }
    string Category { get; }
}

public class TreemapNode<T> where T : class
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public T Item { get; set; }
    public double NormalizedArea { get; set; }

    public TreemapNode(T item, double x, double y, double width, double height)
    {
        Item = item;
        X = Math.Max(0, x);
        Y = Math.Max(0, y);
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
    }
}

public static class TreemapLayoutEngine
{
    public static List<TreemapNode<T>> CalculateLayout<T>(
        IEnumerable<T> items,
        Func<T, long> sizeSelector,
        double viewportWidth,
        double viewportHeight,
        int maxTiles = 150) where T : class
    {
        var result = new List<TreemapNode<T>>();
        if (viewportWidth <= 0 || viewportHeight <= 0) return result;

        var itemList = items
            .Where(x => sizeSelector(x) > 0)
            .OrderByDescending(sizeSelector)
            .Take(maxTiles)
            .ToList();

        if (itemList.Count == 0) return result;

        double totalSize = itemList.Sum(x => (double)sizeSelector(x));
        if (totalSize <= 0) return result;

        double totalArea = viewportWidth * viewportHeight;
        var normalizedItems = itemList.Select(item => (
            Item: item,
            Area: ((double)sizeSelector(item) / totalSize) * totalArea
        )).ToList();

        LayoutRow(normalizedItems, 0, 0, viewportWidth, viewportHeight, result);
        return result;
    }

    private static void LayoutRow<T>(
        List<(T Item, double Area)> items,
        double x, double y,
        double width, double height,
        List<TreemapNode<T>> output) where T : class
    {
        if (items.Count == 0 || width <= 1 || height <= 1) return;

        if (items.Count == 1)
        {
            output.Add(new TreemapNode<T>(items[0].Item, x, y, width, height));
            return;
        }

        bool isHorizontal = width >= height;
        double sideLength = isHorizontal ? height : width;

        var currentRow = new List<(T Item, double Area)> { items[0] };
        double currentWorst = CalculateWorstAspectRatio(currentRow, sideLength);

        int i = 1;
        while (i < items.Count)
        {
            var testRow = new List<(T Item, double Area)>(currentRow) { items[i] };
            double testWorst = CalculateWorstAspectRatio(testRow, sideLength);

            if (testWorst <= currentWorst)
            {
                currentRow.Add(items[i]);
                currentWorst = testWorst;
                i++;
            }
            else
            {
                break;
            }
        }

        // Layout the current row along the side
        double rowArea = currentRow.Sum(r => r.Area);
        double rowThickness = rowArea / sideLength;

        if (rowThickness > 0)
        {
            double offset = 0;
            foreach (var elem in currentRow)
            {
                double elemLength = elem.Area / rowThickness;
                if (isHorizontal)
                {
                    output.Add(new TreemapNode<T>(elem.Item, x, y + offset, rowThickness, elemLength));
                }
                else
                {
                    output.Add(new TreemapNode<T>(elem.Item, x + offset, y, elemLength, rowThickness));
                }
                offset += elemLength;
            }

            // Recurse with the remaining rectangle and items
            var remainingItems = items.Skip(currentRow.Count).ToList();
            if (remainingItems.Count > 0)
            {
                if (isHorizontal)
                {
                    LayoutRow(remainingItems, x + rowThickness, y, width - rowThickness, height, output);
                }
                else
                {
                    LayoutRow(remainingItems, x, y + rowThickness, width, height - rowThickness, output);
                }
            }
        }
    }

    private static double CalculateWorstAspectRatio<T>(List<(T Item, double Area)> row, double sideLength)
    {
        if (row.Count == 0 || sideLength <= 0) return double.MaxValue;

        double rowArea = row.Sum(r => r.Area);
        if (rowArea <= 0) return double.MaxValue;

        double sideLengthSq = sideLength * sideLength;
        double maxArea = row.Max(r => r.Area);
        double minArea = row.Min(r => r.Area);

        double ratio1 = (sideLengthSq * maxArea) / (rowArea * rowArea);
        double ratio2 = (rowArea * rowArea) / (sideLengthSq * minArea);

        return Math.Max(ratio1, ratio2);
    }
}
