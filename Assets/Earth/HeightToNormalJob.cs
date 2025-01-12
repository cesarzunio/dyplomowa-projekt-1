using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct HeightToNormalJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<byte> HeightMap;

    [ReadOnly]
    public int2 TextureSize;

    [ReadOnly]
    public float NormalStrength;

    [WriteOnly]
    public RawArray<Color32> NormalMap;

    [BurstCompile]
    public unsafe void Execute(int index)
    {
        var neighbors = stackalloc int2[4];
        var pixelCoord = TexUtilities.FlatToPixelCoordInt2(index, TextureSize.x);

        TexUtilities.GetNeighbors4(pixelCoord, TextureSize, neighbors);

        int flatUp = TexUtilities.PixelCoordToFlat(neighbors[0], TextureSize.x);
        int flatDown = TexUtilities.PixelCoordToFlat(neighbors[1], TextureSize.x);
        int flatLeft = TexUtilities.PixelCoordToFlat(neighbors[2], TextureSize.x);
        int flatRight = TexUtilities.PixelCoordToFlat(neighbors[3], TextureSize.x);

        float heightUp = HeightMap[flatUp] / 255f;
        float heightDown = HeightMap[flatDown] / 255f;
        float heightLeft = HeightMap[flatLeft] / 255f;
        float heightRight = HeightMap[flatRight] / 255f;

        float dx = (heightRight - heightLeft) * NormalStrength;
        float dy = (heightDown - heightUp) * NormalStrength;

        var normal = math.normalize(new float3(dx, dy, 1f)) * 0.5f + 0.5f;

        NormalMap[index] = FloatsToColor32(normal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Color32 FloatsToColor32(float3 f) => new Color32
    {
        r = (byte)math.round(f.x * 255f),
        g = (byte)math.round(f.y * 255f),
        b = (byte)math.round(f.z * 255f),
        a = 255
    };
}
