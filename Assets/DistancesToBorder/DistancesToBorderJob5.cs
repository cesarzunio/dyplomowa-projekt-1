using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct DistancesToBorderJob5 : IJob
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

        var textureSizeBig = FieldsMap.TextureSize * 2;
        var neighbors = stackalloc int2[NEIGHBORS];

        while (Queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, textureSizeBig.x);
            var currentPlaneUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(current, textureSizeBig);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentPlaneUv);
            double currentCost = Queue.GetCost(current);

            Closed[currentFlat] = true;
            Distances[currentFlat] = currentCost;

            TexUtilities.GetNeighbors8(current, textureSizeBig, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSizeBig.x);

                if (Closed[neighborFlat])
                    continue;

                var neighborPlaneUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(neighbor, textureSizeBig);
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
        var textureSizeBig = fieldsMap.TextureSize * 2;
        int fieldsMapLength = textureSizeBig.x * textureSizeBig.y;

        for (int i = 0; i < fieldsMapLength; i++)
        {
            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, textureSizeBig.x);

            if (pixelCoord.x % 2 == 1 && pixelCoord.y % 2 == 1)
                continue;

            var pixelCoordCenter = pixelCoord / 2;
            var pixelCoordBot = TexUtilities.ClampPixelCoord(pixelCoord + new int2(0, -1), textureSizeBig) / 2;
            var pixelCoordLeft = TexUtilities.ClampPixelCoord(pixelCoord + new int2(-1, 0), textureSizeBig) / 2;
            var pixelCoordBotLeft = TexUtilities.ClampPixelCoord(pixelCoord + new int2(-1, -1), textureSizeBig) / 2;

            int flatCenter = TexUtilities.PixelCoordToFlat(pixelCoordCenter, fieldsMap.TextureSize.x);
            int flatBot = TexUtilities.PixelCoordToFlat(pixelCoordBot, fieldsMap.TextureSize.x);
            int flatLeft = TexUtilities.PixelCoordToFlat(pixelCoordLeft, fieldsMap.TextureSize.x);
            int flatBotLeft = TexUtilities.PixelCoordToFlat(pixelCoordBotLeft, fieldsMap.TextureSize.x);

            uint field = fieldsMap.Fields[flatCenter];
            uint fieldBot = fieldsMap.Fields[flatBot];
            uint fieldLeft = fieldsMap.Fields[flatLeft];
            uint fieldBotLeft = fieldsMap.Fields[flatBotLeft];

            bool addCross = pixelCoord.x % 2 == 0 && pixelCoord.y % 2 == 0 && !(field == fieldBot && fieldBot == fieldLeft && fieldLeft == fieldBotLeft);
            bool addVertical = pixelCoord.y % 2 == 0 && field != fieldBot;
            bool addHorizontal = pixelCoord.x % 2 == 0 && field != fieldLeft;

            if (addCross || addVertical || addHorizontal)
            {
                queue.Add(pixelCoord, 0.0);
                closed[i] = true;
            }
        }
    }
}
