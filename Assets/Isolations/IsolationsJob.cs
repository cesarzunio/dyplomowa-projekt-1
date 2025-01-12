using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct IsolationsJob : IJobParallelFor
{
    const int NEIGHBORS = 256;

    public FieldsMap FieldsMap;
    public RawArray<Color32> IsolationsMap;
    public int N;

    [BurstCompile]
    public void Execute(int index)
    {
        uint field = FieldsMap.Fields[index];
        var pixelCoord = TexUtilities.FlatToPixelCoordInt2(index, FieldsMap.TextureSize.x);

        var neighborsPtr = stackalloc int2[NEIGHBORS];
        var neighbors = new RawListStackalloc<int2>(neighborsPtr, NEIGHBORS);

        TexUtilities.GetNeighborsNxN(pixelCoord, FieldsMap.TextureSize, ref neighbors, N);

        float differentNeighborsCount = 0f;

        for (int i = 0; i < neighbors.Count; i++)
        {
            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbors[i], FieldsMap.TextureSize.x);
            uint neighborField = FieldsMap.Fields[neighborFlat];

            differentNeighborsCount += neighborField != field ? 1f : 0f;
        }

        float ratio = differentNeighborsCount / neighbors.Count;
        byte b = CesColorUtilities.Float01ToByte(ratio);

        IsolationsMap[index] = new Color32(b, b, b, 255);
    }
}
