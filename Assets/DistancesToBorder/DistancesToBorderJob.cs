using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct DistancesToBorderJob : IJob
{
    const int NEIGHBORS = 8;

    public FieldsMap FieldsMap;
    public RawGeoQueueTexture Queue;
    public RawArray<bool> Closed;
    public RawArray<double> Distances;

    [BurstCompile]
    public void Execute()
    {
        AddStartings(ref Queue, FieldsMap);

        var neighbors = stackalloc int2[NEIGHBORS];

        while (Queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, FieldsMap.TextureSize.x);
            var currentPlaneUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(current, FieldsMap.TextureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentPlaneUv);
            double currentCost = Queue.GetCost(current);

            Closed[currentFlat] = true;
            Distances[currentFlat] = currentCost;

            TexUtilities.GetNeighbors4(current, FieldsMap.TextureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, FieldsMap.TextureSize.x);

                if (Closed[neighborFlat])
                    continue;

                var neighborPlaneUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbor, FieldsMap.TextureSize);
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

    static void AddStartings(ref RawGeoQueueTexture queue, FieldsMap fieldsMap)
    {
        var neighbors = stackalloc int2[NEIGHBORS];
        int fieldsMapLength = fieldsMap.TextureSize.x * fieldsMap.TextureSize.y;

        for (int i = 0; i < fieldsMapLength; i++)
        {
            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, fieldsMap.TextureSize.x);
            var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoord, fieldsMap.TextureSize);
            var unitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(uv);

            TexUtilities.GetNeighbors4(pixelCoord, fieldsMap.TextureSize, neighbors);

            if (IsBorder(neighbors, unitSphere, fieldsMap.Fields[i], fieldsMap, out double distance))
            {
                queue.Add(pixelCoord, distance);
            }
        }
    }

    static bool IsBorder(int2* neighbors, double3 unitSphere, uint fieldIndex, FieldsMap fieldsMap, out double distance)
    {
        bool found = false;
        distance = double.MaxValue;

        for (int i = 0; i < NEIGHBORS; i++)
        {
            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbors[i], fieldsMap.TextureSize.x);
            uint neighborFieldIndex = fieldsMap.Fields[neighborFlat];
            var neighborUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbors[i], fieldsMap.TextureSize);
            var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborUv);
            var borderUnitSphere = math.normalize(neighborUnitSphere + unitSphere);

            if (neighborFieldIndex != fieldIndex)
            {
                double distanceNew = GeoUtilitiesDouble.Distance(unitSphere, borderUnitSphere);

                if (distance > distanceNew)
                {
                    distance = distanceNew;
                }

                found = true;
            }
        }

        return found;
    }
}
