using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class FieldsGeneratorUtilities
{
    public static RawArray<Color32> CreateAdditionals(RawArray<int> fieldsMap, int fieldsCountMax, int2 additionalsSize, Allocator allocator)
    {
        var usedColors = GetUsedColors(fieldsMap, fieldsCountMax);
        var additionals = new RawArray<Color32>(allocator, additionalsSize.x * additionalsSize.y);

        var random = new Unity.Mathematics.Random(2);

        for (int i = 0; i < additionals.Length; i++)
        {
            additionals[i] = CesColorUtilities.GetRandomColor(usedColors, ref random).ToColor32();
        }

        return additionals;
    }

    public static void Fill(
        RawArray<int> fieldsMap, RawArray<int> regionsMap, int2 textureSize,
        int fieldsCountMax, int2 additionalsSize, Allocator allocator,
        out RawArray<Color32> filled, out RawArray<Color32> additionals)
    {
        var usedColors = GetUsedColors(fieldsMap, fieldsCountMax);
        var random = new Unity.Mathematics.Random(2);
        filled = new RawArray<Color32>(allocator, default, fieldsMap.Length);

        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                var currentPixelCoord = new int2(x, y);
                var currentFlat = TexUtilities.PixelCoordToFlat(currentPixelCoord, textureSize.x);

                if (fieldsMap[currentFlat] != -1)
                    continue;

                int newColor = CesColorUtilities.GetRandomColor(usedColors, ref random);

                FillSpot(currentPixelCoord, newColor, fieldsMap, regionsMap, filled, textureSize);
            }
        }

        additionals = new RawArray<Color32>(allocator, additionalsSize.x * additionalsSize.y);

        for (int i = 0; i < additionals.Length; i++)
        {
            additionals[i] = CesColorUtilities.GetRandomColor(usedColors, ref random).ToColor32();
        }
    }

    public static HashSet<int> GetUsedColors(RawArray<int> colors, int fieldsCountMax)
    {
        var usedColors = new HashSet<int>(fieldsCountMax);

        for (int i = 0; i < colors.Length; i++)
        {
            usedColors.Add(colors[i]);
        }

        return usedColors;
    }

    const int STACK_SIZE = 8192;
    const int NEIGHBORS = 4;

    static unsafe void FillSpot(int2 pixelCoord, int fieldColorToSet, RawArray<int> fieldsMap, RawArray<int> regionsMap, RawArray<Color32> filled, int2 textureSize)
    {
        var stackPtr = stackalloc int2[STACK_SIZE];
        var stack = new RawStackStackalloc<int2>(stackPtr, STACK_SIZE);
        var neighbors = stackalloc int2[NEIGHBORS];
        int regionColor = regionsMap[TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x)];

        AddToStack(pixelCoord);

        while (stack.TryPop(out var currentPixelCoord))
        {
            TexUtilities.GetNeighbors4(currentPixelCoord, textureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                AddToStack(neighbors[i]);
            }
        }

        // ---

        void AddToStack(int2 pixelCoordToAdd)
        {
            int flatToAdd = TexUtilities.PixelCoordToFlat(pixelCoordToAdd, textureSize.x);
            int regionColorToAdd = regionsMap[flatToAdd];

            if (fieldsMap[flatToAdd] != -1)
                return;

            if (regionColorToAdd != regionColor)
                return;

            fieldsMap[flatToAdd] = fieldColorToSet;
            filled[flatToAdd] = new Color32(255, 0, 0, 255);

            stack.Add(pixelCoordToAdd);
        }
    }
}
