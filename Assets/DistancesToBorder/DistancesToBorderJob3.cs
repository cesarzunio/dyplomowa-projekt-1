using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct DistancesToBorderJob3 : IJob
{
    const int NEIGHBORS = 8;

    public FieldsMap FieldsMap;
    public RawGeoQueueTexture Queue;
    public RawArray<bool> Closed;
    public RawArray<double> Distances;

    [BurstCompile]
    public void Execute()
    {
        AddStartings(ref Queue, FieldsMap, Closed);

        var neighbors = stackalloc int2[NEIGHBORS];

        while (Queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, FieldsMap.TextureSize.x);
            var currentPlaneUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(current, FieldsMap.TextureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentPlaneUv);
            double currentCost = Queue.GetCost(current);

            Closed[currentFlat] = true;
            Distances[currentFlat] = currentCost;

            TexUtilities.GetNeighbors8(current, FieldsMap.TextureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, FieldsMap.TextureSize.x);

                if (Closed[neighborFlat])
                    continue;

                var neighborPlaneUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(neighbor, FieldsMap.TextureSize);
                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborPlaneUv);

                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere);
                double costNew = currentCost + distance;

                if (!Queue.TryGetCost(neighbor, out double cost) || costNew < cost)
                {
                    Queue.AddOrUpdate(neighbor, costNew);
                }
            }
        }
    }

    static void AddStartings(ref RawGeoQueueTexture queue, FieldsMap fieldsMap, RawArray<bool> closed)
    {
        int fieldsMapLength = fieldsMap.TextureSize.x * fieldsMap.TextureSize.y;

        for (int i = 0; i < fieldsMapLength; i++)
        {
            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, fieldsMap.TextureSize.x);
            var pixelCoordRight = TexUtilities.ClampPixelCoord(pixelCoord + new int2(1, 0), fieldsMap.TextureSize);
            var pixelCoordUp = TexUtilities.ClampPixelCoord(pixelCoord + new int2(0, 1), fieldsMap.TextureSize);
            var pixelCoordRightUp = TexUtilities.ClampPixelCoord(pixelCoord + new int2(1, 1), fieldsMap.TextureSize);

            int flatRight = TexUtilities.PixelCoordToFlat(pixelCoordRight, fieldsMap.TextureSize.x);
            int flatUp = TexUtilities.PixelCoordToFlat(pixelCoordUp, fieldsMap.TextureSize.x);
            int flatRightUp = TexUtilities.PixelCoordToFlat(pixelCoordRightUp, fieldsMap.TextureSize.x);

            uint field = fieldsMap.Fields[i];
            uint fieldRight = fieldsMap.Fields[flatRight];
            uint fieldUp = fieldsMap.Fields[flatUp];
            uint fieldRightUp = fieldsMap.Fields[flatRightUp];

            if (field != fieldRight)
            {
                if (!closed[flatRight])
                {
                    queue.Add(pixelCoordRight, 0.0);
                    closed[flatRight] = true;
                }

                if (!closed[flatRightUp])
                {
                    queue.Add(pixelCoordRightUp, 0.0);
                    closed[flatRightUp] = true;
                }
            }

            if (field != fieldUp)
            {
                if (!closed[flatUp])
                {
                    queue.Add(pixelCoordUp, 0.0);
                    closed[flatUp] = true;
                }

                if (!closed[flatRightUp])
                {
                    queue.Add(pixelCoordRightUp, 0.0);
                    closed[flatRightUp] = true;
                }
            }

            if (field != fieldRightUp)
            {
                if (!closed[flatRightUp])
                {
                    queue.Add(pixelCoordRightUp, 0.0);
                    closed[flatRightUp] = true;
                }
            }
        }
    }
}
