using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public unsafe struct FieldsGeneratorJobInverseSimple : IJob
{
    const int NEIGHBORS = 4;

    public RawGeoQueueTexture Queue;
    public RawArray<int> CoordToColorField;
    public RawArray<bool> Closed;
    public int2 TextureSize;
    public RawArray<int> CentersMap;
    public RawArray<int> FieldsMap;

    [BurstCompile]
    public void Execute()
    {
        AddStartings(ref Queue, CoordToColorField, CentersMap, TextureSize);

        var neighbors = stackalloc int2[NEIGHBORS];

        while (Queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, TextureSize.x);
            var currentColorField = CoordToColorField[currentFlat];

            Closed[currentFlat] = true;
            FieldsMap[currentFlat] = currentColorField;

            var currentPlaneUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(current, TextureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentPlaneUv);

            double currentCost = Queue.GetCost(current);

            TexUtilities.GetNeighbors4(current, TextureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, TextureSize.x);

                if (Hint.Unlikely(Closed[neighborFlat]))
                    continue;

                var neighborPlaneUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbor, TextureSize);
                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborPlaneUv);

                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere);
                double costNew = currentCost + distance;

                if (!Queue.TryGetCost(neighbor, out double cost) || costNew < cost)
                {
                    Queue.AddOrUpdate(neighbor, costNew);
                    CoordToColorField[neighborFlat] = currentColorField;
                }
            }
        }
    }

    static void AddStartings(ref RawGeoQueueTexture queue, RawArray<int> coordToColor, RawArray<int> centersMap, int2 textureSize)
    {
        for (int i = 0; i < centersMap.Length; i++)
        {
            if (Hint.Likely(centersMap[i] == -1))
                continue;

            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, textureSize.x);

            queue.Add(pixelCoord, 0.0);
            coordToColor[i] = centersMap[i];
        }
    }
}
