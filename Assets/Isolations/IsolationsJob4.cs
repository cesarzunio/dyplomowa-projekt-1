using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct IsolationsJob4 : IJobParallelFor
{
    const int NEIGHBORS = 4;

    public FieldsMap FieldsMap;
    public RawArray<Color32> IsolationsMap;

    [BurstCompile]
    public void Execute(int index)
    {
        uint field = FieldsMap.Fields[index];
        var pixelCoord = TexUtilities.FlatToPixelCoordInt2(index, FieldsMap.TextureSize.x);

        var neighbors = stackalloc int2[NEIGHBORS];

        TexUtilities.GetNeighbors4(pixelCoord, FieldsMap.TextureSize, neighbors);

        float differentNeighborsCount = 0f;

        for (int i = 0; i < NEIGHBORS; i++)
        {
            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbors[i], FieldsMap.TextureSize.x);
            uint neighborField = FieldsMap.Fields[neighborFlat];

            differentNeighborsCount += neighborField != field ? 1f : 0f;
        }

        float ratio = differentNeighborsCount / NEIGHBORS;
        byte b = CesColorUtilities.Float01ToByte(ratio);

        IsolationsMap[index] = new Color32(b, b, b, 255);
    }
}
