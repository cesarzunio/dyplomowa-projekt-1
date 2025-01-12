using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public unsafe struct BordersUnsortedJob : IJob
{
    [ReadOnly] public int2 TextureSize;
    [ReadOnly] public RawArray<Color32> FieldsMap;
    [ReadOnly] public Allocator Allocator;

    [NativeDisableUnsafePtrRestriction]
    public UnsafeHashMap<int2, ProxyBag<int4>>* ColorPairToBorderCoords;

    [BurstCompile]
    public void Execute()
    {
        for (int y = 1; y < TextureSize.y - 1; y++)
        {
            for (int x = 0; x < TextureSize.x; x++)
            {
                var pixelCoordCurrent = new int2(x, y);
                var pixelCoordRight = TexUtilities.ClampPixelCoord(new int2(x + 1, y), TextureSize);
                var pixelCoordUp = TexUtilities.ClampPixelCoord(new int2(x, y + 1), TextureSize);

                int flatCurrent = TexUtilities.PixelCoordToFlat(pixelCoordCurrent, TextureSize.x);
                int flatRight = TexUtilities.PixelCoordToFlat(pixelCoordRight, TextureSize.x);
                int flatUp = TexUtilities.PixelCoordToFlat(pixelCoordUp, TextureSize.x);

                var colorCurrent = FieldsMap[flatCurrent];
                var colorRight = FieldsMap[flatRight];
                var colorUp = FieldsMap[flatUp];

                if (Hint.Unlikely(colorCurrent.a == 0))
                    continue;

                if (colorRight.a != 0 && CesColorUtilities.ColorNotEquals(colorCurrent, colorRight))
                {
                    var from = TexUtilities.ClampPixelCoord(new int2(x + 1, y), TextureSize);
                    var to = TexUtilities.ClampPixelCoord(new int2(x + 1, y + 1), TextureSize);

                    AddBorder(colorCurrent, colorRight, from, to, ref *ColorPairToBorderCoords, Allocator);
                }

                if (colorUp.a != 0 && CesColorUtilities.ColorNotEquals(colorCurrent, colorUp))
                {
                    var from = TexUtilities.ClampPixelCoord(new int2(x, y + 1), TextureSize);
                    var to = TexUtilities.ClampPixelCoord(new int2(x + 1, y + 1), TextureSize);

                    AddBorder(colorCurrent, colorUp, from, to, ref *ColorPairToBorderCoords, Allocator);
                }
            }
        }
    }

    static void AddBorder(Color32 a, Color32 b, int2 from, int2 to, ref UnsafeHashMap<int2, ProxyBag<int4>> colorPairToBorderCoords, Allocator allocator)
    {
        GetColorPairs(a, b, out var pair1, out var pair2);

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

        var list = new ProxyBag<int4>(allocator, 2048);
        list.Add(new int4(from, to));

        colorPairToBorderCoords.Add(pair1, list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void GetColorPairs(Color32 colorA, Color32 colorB, out int2 pair1, out int2 pair2)
    {
        int indexA = colorA.ToIndex();
        int indexB = colorB.ToIndex();

        pair1 = new int2(indexA, indexB);
        pair2 = new int2(indexB, indexA);
    }
}
