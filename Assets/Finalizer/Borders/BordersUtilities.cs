using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static unsafe class BordersUtilities
{
    public static void GenerateBorders(RawArray<int> fieldsMap, int2 textureSize, int bordersPredict, Allocator allocator, out RawArray<BordersContainers.BorderSorted> bordersSorted)
    {
        CreateBordersUnsorted(fieldsMap, textureSize, bordersPredict, allocator, out var bordersUnsorted);
        CreateBordersSorted(bordersUnsorted, allocator, textureSize, out bordersSorted);

        DisposeBordersUnsorted(bordersUnsorted);

        Debug.Log("BordersUtilities :: GenerateBorders :: Borders count: " + bordersSorted.Length);
    }

    public static void DisposeBordersSorted(RawArray<BordersContainers.BorderSorted> bordersSorted)
    {
        for (int i = 0; i < bordersSorted.Length; i++)
        {
            for (int j = 0; j < bordersSorted[i].BorderCoords.Count; j++)
            {
                bordersSorted[i].BorderCoords[j].Dispose();
            }

            bordersSorted[i].BorderCoords.Dispose();
        }

        bordersSorted.Dispose();
    }

    public static int GetBordersCount(RawArray<int> fieldsMap, int2 textureSize, int bordersPredict)
    {
        var borders = new HashSet<long>(bordersPredict);

        for (int y = 0; y < textureSize.y - 1; y++)
        {
            for (int x = 0; x < textureSize.x ; x++)
            {
                var pixelCoordCurrent = new int2(x, y);
                var pixelCoordRight = TexUtilities.ClampPixelCoord(new int2(x + 1, y), textureSize);
                var pixelCoordUp = TexUtilities.ClampPixelCoord(new int2(x, y + 1), textureSize);

                int flatCurrent = TexUtilities.PixelCoordToFlat(pixelCoordCurrent, textureSize.x);
                int flatRight = TexUtilities.PixelCoordToFlat(pixelCoordRight, textureSize.x);
                int flatUp = TexUtilities.PixelCoordToFlat(pixelCoordUp, textureSize.x);

                int colorCurrent = fieldsMap[flatCurrent];
                int colorRight = fieldsMap[flatRight];
                int colorUp = fieldsMap[flatUp];

                if (colorCurrent == -1)
                    continue;

                long colorCurrentLongHigh = ((long)colorCurrent) << 32;

                if (colorRight != -1 && colorCurrent != colorRight)
                {
                    long right = colorCurrentLongHigh | ((long)colorRight);

                    borders.Add(right);
                }

                if (colorUp != -1 && colorCurrent != colorUp)
                {
                    long up = colorCurrentLongHigh | ((long)colorUp);

                    borders.Add(up);
                }
            }
        }

        return borders.Count;
    }

    const int INNER_BAG_SIZE = 64;

    static void CreateBordersUnsorted(RawArray<int> fieldsMap, int2 textureSize, int bordersPredict, Allocator allocator, out RawArray<BordersContainers.BorderUnsorted> bordersUnsorted)
    {
        var colorPairToBorderCoords = new Dictionary<int2, ProxyBag<int4>>(bordersPredict);

        for (int y = 0; y < textureSize.y - 1; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                var pixelCoordCurrent = new int2(x, y);
                var pixelCoordRight = TexUtilities.ClampPixelCoord(new int2(x + 1, y), textureSize);
                var pixelCoordUp = TexUtilities.ClampPixelCoord(new int2(x, y + 1), textureSize);

                int flatCurrent = TexUtilities.PixelCoordToFlat(pixelCoordCurrent, textureSize.x);
                int flatRight = TexUtilities.PixelCoordToFlat(pixelCoordRight, textureSize.x);
                int flatUp = TexUtilities.PixelCoordToFlat(pixelCoordUp, textureSize.x);

                var colorCurrent = fieldsMap[flatCurrent];
                var colorRight = fieldsMap[flatRight];
                var colorUp = fieldsMap[flatUp];

                if (colorCurrent == -1)
                    continue;

                if (colorRight != -1 && colorCurrent != colorRight)
                {
                    var from = TexUtilities.ClampPixelCoord(new int2(x + 1, y), textureSize);
                    var to = TexUtilities.ClampPixelCoord(new int2(x + 1, y + 1), textureSize);

                    AddBorder(colorCurrent, colorRight, from, to, colorPairToBorderCoords, allocator);
                }

                if (colorUp != -1 && colorCurrent != colorUp)
                {
                    var from = TexUtilities.ClampPixelCoord(new int2(x, y + 1), textureSize);
                    var to = TexUtilities.ClampPixelCoord(new int2(x + 1, y + 1), textureSize);

                    AddBorder(colorCurrent, colorUp, from, to, colorPairToBorderCoords, allocator);
                }
            }
        }

        bordersUnsorted = new RawArray<BordersContainers.BorderUnsorted>(allocator, colorPairToBorderCoords.Count);
        int it = 0;

        foreach (var kvp in colorPairToBorderCoords)
        {
            bordersUnsorted[it++] = new BordersContainers.BorderUnsorted
            {
                FieldA = kvp.Key.x,
                FieldB = kvp.Key.y,
                BorderCoords = kvp.Value
            };
        }

        // ---

        static void AddBorder(int colorA, int colorB, int2 from, int2 to, Dictionary<int2, ProxyBag<int4>> colorPairToBorderCoords, Allocator allocator)
        {
            GetColorPairs(colorA, colorB, out var pair1, out var pair2);

            if (colorPairToBorderCoords.TryGetValue(pair1, out var listOut1))
            {
                listOut1.Add(new int4(from, to));
                return;
            }

            if (colorPairToBorderCoords.TryGetValue(pair2, out var listOut2))
            {
                listOut2.Add(new int4(from, to));
                return;
            }

            var list = new ProxyBag<int4>(allocator, INNER_BAG_SIZE);
            list.Add(new int4(from, to));

            colorPairToBorderCoords.Add(pair1, list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void GetColorPairs(int colorA, int colorB, out int2 pair1, out int2 pair2)
        {
            pair1 = new int2(colorA, colorB);
            pair2 = new int2(colorB, colorA);
        }
    }

    static void CreateBordersSorted(RawArray<BordersContainers.BorderUnsorted> bordersUnsorted, Allocator allocator, int2 textureSize, out RawArray<BordersContainers.BorderSorted> bordersSorted)
    {
        bordersSorted = new RawArray<BordersContainers.BorderSorted>(allocator, bordersUnsorted.Length);

        var job = new BordersSortedJob
        {
            TextureSize = textureSize,
            Allocator = allocator,
            BordersUnsorted = bordersUnsorted,
            BordersSorted = bordersSorted,
        };

        job.Schedule(bordersSorted.Length, 16).Complete();
    }

    static void DisposeBordersUnsorted(RawArray<BordersContainers.BorderUnsorted> bordersUnsorted)
    {
        for (int i = 0; i < bordersUnsorted.Length; i++)
        {
            bordersUnsorted[i].BorderCoords.Dispose();
        }

        bordersUnsorted.Dispose();
    }
}

public static class BordersContainers
{
    public struct BorderUnsorted
    {
        public int FieldA;
        public int FieldB;

        public ProxyBag<int4> BorderCoords;
    }

    public struct BorderSorted : IDisposable
    {
        public int FieldColorA;
        public int FieldColorB;

        public RawBag<RawBag<int2>> BorderCoords;

        public void Dispose()
        {
            BorderCoords.DisposeDepth1();
        }
    }
}