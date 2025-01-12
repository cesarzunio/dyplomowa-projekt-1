using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class RiverUtilities
{
    public static void FindMouths(RawArray<int> riversMap, int2 textureSize, Allocator allocator, out RawBag<int2> mouthsPrimary, out RawBag<int2> mouthsSecondary)
    {
        var riversColors = RiversColorsInts.Create();

        mouthsPrimary = new RawBag<int2>(allocator, 512);
        mouthsSecondary = new RawBag<int2>(allocator, 512);

        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                int flat = TexUtilities.PixelCoordToFlat(x, y, textureSize.x);
                var color = riversMap[flat];

                if (color == riversColors.Mouth)
                {
                    mouthsPrimary.Add(new int2(x, y));
                    continue;
                }

                if (color == riversColors.MouthSecondary)
                {
                    mouthsSecondary.Add(new int2(x, y));
                    continue;
                }
            }
        }
    }

    public static RawBag<int2> FindMouthsSecondary(RawArray<Color32> riversMap, int2 textureSize, Allocator allocator)
    {
        var mouthsSecondary = new RawBag<int2>(allocator, 512);

        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                int flat = TexUtilities.PixelCoordToFlat(x, y, textureSize.x);
                var color = riversMap[flat];

                if (CesColorUtilities.ColorEquals(color, RiverColors.MouthSecondary))
                {
                    mouthsSecondary.Add(new int2(x, y));
                }
            }
        }

        return mouthsSecondary;
    }

    public static RawBag<int2> FindSources(RawArray<Color32> riverMap, int2 textureSize, Allocator allocator)
    {
        var sources = new RawBag<int2>(allocator, 512);

        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                int flat = TexUtilities.PixelCoordToFlat(x, y, textureSize.x);
                var color = riverMap[flat];

                if (CesColorUtilities.ColorEquals(color, RiverColors.Source))
                {
                    sources.Add(new int2(x, y));
                }
            }
        }

        return sources;
    }

    public static RawArray<double> GenerateNeighborMultipliers(RawArray<int> regionsMap, int2 textureSize, Allocator allocator)
    {
        var multipliers = new RawArray<double>(allocator, regionsMap.Length);

        var job = new NeighborMultipliersJob
        {
            TextureSize = textureSize,
            RegionsMap = regionsMap,
            Multipliers = multipliers
        };

        job.Schedule(regionsMap.Length, 16).Complete();

        return multipliers;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateMultiplier(int currentFlat, int neighborFlat, RawArray<double> multipliers)
    {
        return multipliers[currentFlat] * multipliers[neighborFlat];
    }
}
