using Mono.Cecil.Cil;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using static FinalizerSaves;

public static unsafe class CentersUtilities
{
    public static void GenerateCenters(
        RawArray<int> fieldsMap, RawArray<int> riversMap, int2 textureSize, Dictionary<int, int> colorToIndex,
        List<int> indexToColor, Allocator allocator, out RawArray<double2> indexToCenterGeoCoords)
    {
        GenerateFieldsSpots(fieldsMap, textureSize, colorToIndex, allocator, out var fieldsSpots);

        indexToCenterGeoCoords = new RawArray<double2>(allocator, colorToIndex.Count);

        var job = new CentersGeneratorJob2
        {
            TextureSize = textureSize,
            FieldsSpots = fieldsSpots,
            FieldsMap = fieldsMap,
            RiversMap = riversMap,
            IndexToCenterGeoCoord = indexToCenterGeoCoords,
        };

        job.Schedule(colorToIndex.Count, 16).Complete();

        DisposeFieldsSpots(fieldsSpots);
    }

    static void GenerateFieldsSpots(RawArray<int> fieldsMap, int2 textureSize, Dictionary<int, int> colorToIndex, Allocator allocator, out RawArray<FieldSpots> fieldsSpots)
    {
        fieldsSpots = new RawArray<FieldSpots>(allocator, default, colorToIndex.Count);
        var closed = new RawArray<bool>(allocator, false, fieldsMap.Length);

        for (int i = 0; i < fieldsSpots.Length; i++)
        {
            fieldsSpots[i].Spots = new RawBag<Spot>(allocator);
        }

        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                var flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);
                var color = fieldsMap[flat];

                if (color == -1 || closed[flat])
                    continue;

                int fieldIndex = colorToIndex[color];

                fieldsSpots[fieldIndex].Spots.Add(new Spot
                {
                    PixelCoords = FloodFill2(pixelCoord, color, textureSize, fieldsMap, closed, allocator)
                });
            }
        }

        closed.Dispose();
    }

    const int STACK_SIZE = 1 << 15;

    static RawBag<int2> FloodFill2(int2 startingPixelCoord, int startingColor, int2 textureSize, RawArray<int> fieldsMap, RawArray<bool> closed, Allocator allocator)
    {
        var neighbors = stackalloc int2[4];
        var stackPtr = stackalloc int2[STACK_SIZE];
        var stack = new RawStackStackalloc<int2>(stackPtr, STACK_SIZE);

        var flood = new RawBag<int2>(allocator);

        stack.Add(startingPixelCoord);
        closed[TexUtilities.PixelCoordToFlat(startingPixelCoord, textureSize.x)] = true;

        while (stack.TryPop(out var currentPixelCoord))
        {
            flood.Add(currentPixelCoord);

            TexUtilities.GetNeighbors4(currentPixelCoord, textureSize, neighbors);

            for (int i = 0; i < 4; i++)
            {
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbors[i], textureSize.x);
                var neighborColor = fieldsMap[neighborFlat];

                if (closed[neighborFlat])
                    continue;

                if (neighborColor != startingColor)
                    continue;

                stack.Add(neighbors[i]);
                closed[neighborFlat] = true;
            }
        }

        return flood;
    }

    static void DisposeFieldsSpots(RawArray<FieldSpots> fieldsSpots)
    {
        for (int i = 0; i < fieldsSpots.Length; i++)
        {
            for (int j = 0; j < fieldsSpots[i].Spots.Count; j++)
            {
                fieldsSpots[i].Spots[j].PixelCoords.Dispose();
            }

            fieldsSpots[i].Spots.Dispose();
        }

        fieldsSpots.Dispose();
    }

    public struct FieldSpots
    {
        public RawBag<Spot> Spots;
    }

    public struct Spot
    {
        public RawBag<int2> PixelCoords;
    }
}
