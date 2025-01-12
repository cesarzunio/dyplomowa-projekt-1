using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static CentersUtilities;
using static FinalizerSaves;

public static unsafe class CentersShiftUtility
{
    const int NEIGHBORS = 4;
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static void GenerateShiftedCenters(
        Fields fields, FieldsMap fieldsMap, RawArray<int> regionsMap, RawArray<int> riversMap, int iterations, Allocator allocator)
    {
        var fieldsMapTemp = InitializeFieldsMapTemp(fieldsMap);
        var fieldsSpots = InitializeFieldsSpots(fields);
        var closed = new RawArray<bool>(ALLOCATOR, fieldsMapTemp.Length);
        var fieldToCenterGeoCoord = new RawArray<double2>(ALLOCATOR, fieldsMapTemp.Length);

        for (int i = 0; i < iterations; i++)
        {
            GenerateFieldsSpots(fieldsSpots, fieldsMapTemp, closed, fieldsMap.TextureSize);

            var job = new CentersShifterFieldsJob
            {
                TextureSize = fieldsMap.TextureSize,
            };
        }
    }

    static RawArray<int> InitializeFieldsMapTemp(FieldsMap fieldsMap)
    {
        var fieldsMapTemp = new RawArray<int>(ALLOCATOR, fieldsMap.TextureSize.x * fieldsMap.TextureSize.y);

        for (int i = 0; i < fieldsMapTemp.Length; i++)
        {
            fieldsMapTemp[i] = (int)fieldsMap.Fields[i];
        }

        return fieldsMapTemp;
    }

    static RawArray<int> InitializeCentersMap(Fields fields, int2 textureSize)
    {
        var centersMap = new RawArray<int>(ALLOCATOR, -1, textureSize.x * textureSize.y);

        for (int i = 0; i < fields.Length; i++)
        {
            var uv = GeoUtilitiesDouble.GeoCoordsToPlaneUv(fields.CenterGeoCoords[i]);
            var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, textureSize);
            int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);

            centersMap[flat] = i;
        }

        return centersMap;
    }

    static RawArray<RawBag<RawBag<int2>>> InitializeFieldsSpots(Fields fields)
    {
        var fieldsSpots = new RawArray<RawBag<RawBag<int2>>>(ALLOCATOR, fields.Length);

        for (int i = 0; i < fieldsSpots.Length; i++)
        {
            fieldsSpots[i] = new RawBag<RawBag<int2>>(ALLOCATOR, 4);
        }

        return fieldsSpots;
    }

    static void FillFieldsMapTempBlanks(RawArray<int> fieldsMapTemp, FieldsMap fieldsMap)
    {
        for (int i = 0; i < fieldsMapTemp.Length; i++)
        {
            fieldsMapTemp[i] = fieldsMapTemp[i] == -1 ? (int)fieldsMap.Fields[i] : fieldsMapTemp[i];
        }
    }

    static void GenerateFieldsSpots(RawArray<RawBag<RawBag<int2>>> fieldsSpots, RawArray<int> fieldsMapTemp, RawArray<bool> closed, int2 textureSize)
    {
        closed.Set(false);

        for (int i = 0; i < fieldsSpots.Length; i++)
        {
            for (int j = 0; j < fieldsSpots[i].Count; j++)
            {
                fieldsSpots[i][j].Dispose();
            }

            fieldsSpots[i].Clear();
        }

        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                var flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);

                if (fieldsMapTemp[flat] == -1 || closed[flat])
                    continue;

                int field = fieldsMapTemp[flat];

                fieldsSpots[field].Add(FloodFill2(pixelCoord, field, fieldsMapTemp, closed, textureSize));
            }
        }
    }

    const int STACK_SIZE = 1 << 15;
    const int FLOOD_SIZE = 64;

    static RawBag<int2> FloodFill2(int2 pixelCoordStart, int fieldStart, RawArray<int> fieldsMapTemp, RawArray<bool> closed, int2 textureSize)
    {
        var neighbors = stackalloc int2[NEIGHBORS];
        var stackPtr = stackalloc int2[STACK_SIZE];
        var stack = new RawStackStackalloc<int2>(stackPtr, STACK_SIZE);

        var flood = new RawBag<int2>(ALLOCATOR, FLOOD_SIZE);

        stack.Add(pixelCoordStart);
        closed[TexUtilities.PixelCoordToFlat(pixelCoordStart, textureSize.x)] = true;

        while (stack.TryPop(out var currentPixelCoord))
        {
            flood.Add(currentPixelCoord);

            TexUtilities.GetNeighbors4(currentPixelCoord, textureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbors[i], textureSize.x);
                int neighborField = fieldsMapTemp[neighborFlat];

                if (closed[neighborFlat])
                    continue;

                if (neighborField != fieldStart)
                    continue;

                stack.Add(neighbors[i]);
                closed[neighborFlat] = true;
            }
        }

        return flood;
    }
}
