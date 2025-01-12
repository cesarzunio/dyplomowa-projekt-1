using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public unsafe struct FieldsGeneratorJobInverse : IJob
{
    const int NEIGHBORS = 4;

    public RawGeoQueueTexture Queue;
    public RawArray<int> CoordToColorField;
    public RawArray<bool> Closed;
    public int2 TextureSize;
    public RawArray<int> CentersMap;
    public RawArray<int> RegionsMap;
    public RawArray<bool> RiversMap;
    public RawArray<int> FieldsMap;

    [BurstCompile]
    public void Execute()
    {
        AddStartings(ref Queue, CoordToColorField, CentersMap, RiversMap, TextureSize);

        var neighbors = stackalloc int2[NEIGHBORS];

        while (Queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, TextureSize.x);
            var currentColorField = CoordToColorField[currentFlat];
            var currentColorRegion = RegionsMap[currentFlat];

            Closed[currentFlat] = true;
            FieldsMap[currentFlat] = currentColorField;

            bool currentIsRiver = RiversMap[currentFlat];

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

                var neighborColorRegion = RegionsMap[neighborFlat];
                bool neighborIsRiver = RiversMap[neighborFlat];

                if (Hint.Unlikely(currentColorRegion != neighborColorRegion))
                    continue;

                if (Hint.Unlikely(currentIsRiver && !neighborIsRiver))
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

    static void AddStartings(ref RawGeoQueueTexture queue, RawArray<int> coordToColor, RawArray<int> centersMap, RawArray<bool> riversMap, int2 textureSize)
    {
        for (int i = 0; i < centersMap.Length; i++)
        {
            if (Hint.Likely(centersMap[i] == -1 || riversMap[i]))
                continue;

            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, textureSize.x);

            queue.Add(pixelCoord, 0.0);
            coordToColor[i] = centersMap[i];
        }
    }
}
