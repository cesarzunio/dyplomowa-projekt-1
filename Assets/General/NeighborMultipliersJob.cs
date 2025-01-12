using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public unsafe struct NeighborMultipliersJob : IJobParallelFor
{
    const int NEIGHBORS = 8;
    const double MUL_PER_NEIGHBOR = 0.25;

    public int2 TextureSize;
    public RawArray<int> RegionsMap;
    public RawArray<double> Multipliers;

    [BurstCompile]
    public void Execute(int index)
    {
        var currentPixelCoord = TexUtilities.FlatToPixelCoordInt2(index, TextureSize.x);
        var currentColor = RegionsMap[index];

        var neighbors = stackalloc int2[NEIGHBORS];

        TexUtilities.GetNeighbors8(currentPixelCoord, TextureSize, neighbors);

        double multiplier = 1.0;

        for (int i = 0; i < NEIGHBORS; i++)
        {
            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbors[i], TextureSize.x);
            var neighborColor = RegionsMap[neighborFlat];

            if (currentColor != neighborColor)
                multiplier *= MUL_PER_NEIGHBOR;
        }

        Multipliers[index] = multiplier;
    }
}
