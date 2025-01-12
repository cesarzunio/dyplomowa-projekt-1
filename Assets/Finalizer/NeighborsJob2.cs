using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using System.Runtime.CompilerServices;
using System;

[BurstCompile]
public unsafe struct NeighborsJob2 : IJobParallelForBatch
{
    const int NEIGHBORS = 4;

    [NativeSetThreadIndex]
    public int ThreadIndex;

    public int2 TextureSize;
    public RawArray<int> FieldsMap;
    public RawArray<int> RiversIndexesMap;
    public RawArray<int4> FieldsCenters;
    public RawArray<RawGeoQueueHeuristic<HeuristicGeo>> Queues;
    public RawArray<UnsafeHashSet<int2>> ClosedSets;
    public RawArray<UnsafeHashMap<int2, int2>> HashMaps;
    public RawArray<NeighborType> NeighborTypes;
    public RawArray<double2> NeighborDistances;
    public RawArray<int2> RiversCrossPixelCoords;

    [BurstCompile]
    public void Execute(int startIndex, int count)
    {
        for (int i = startIndex; i < startIndex + count; i++)
        {
            var startPixelCoord = FieldsCenters[i].xy;
            var endPixelCoord = FieldsCenters[i].zw;

            if (TryFindPath(
                startPixelCoord, endPixelCoord, TextureSize, FieldsMap, RiversIndexesMap, false,
                ref Queues[ThreadIndex], ref ClosedSets[ThreadIndex], ref HashMaps[ThreadIndex], out double neighborsDistance, out _))
            {
                NeighborTypes[i] = NeighborType.IsByLand;
                NeighborDistances[i] = new double2(neighborsDistance, 0.0);
                RiversCrossPixelCoords[i] = default;

                continue;
            }

            if (TryFindPath(
                startPixelCoord, endPixelCoord, TextureSize, FieldsMap, RiversIndexesMap, true,
                ref Queues[ThreadIndex], ref ClosedSets[ThreadIndex], ref HashMaps[ThreadIndex], out neighborsDistance, out var end))
            {
                double distanceEnd = Queues[ThreadIndex].GetCost(endPixelCoord);

                NeighborTypes[i] = NeighborType.IsByRiver;
                NeighborDistances[i] = new double2(neighborsDistance, distanceEnd - neighborsDistance);
                RiversCrossPixelCoords[i] = GetRiverCrossPixelCoord(end, ref HashMaps[ThreadIndex], RiversIndexesMap, TextureSize.x);

                continue;
            }

            NeighborTypes[i] = NeighborType.IsNot;
            NeighborDistances[i] = default;
            RiversCrossPixelCoords[i] = default;
        }
    }

    static bool TryFindPath(
        int2 startPixelCoord, int2 endPixelCoord, int2 textureSize, RawArray<int> fieldsMap, RawArray<int> riversIndexesMap, bool allowRivers,
        ref RawGeoQueueHeuristic<HeuristicGeo> queue, ref UnsafeHashSet<int2> closedSet, ref UnsafeHashMap<int2, int2> hashMap, out double neighborDistance, out int2 end)
    {
        int startingColor = fieldsMap[TexUtilities.PixelCoordToFlat(startPixelCoord, textureSize.x)];
        int endColor = fieldsMap[TexUtilities.PixelCoordToFlat(endPixelCoord, textureSize.x)];

        neighborDistance = default;
        end = default;

        queue.Clear(new HeuristicGeo(textureSize, endPixelCoord));
        closedSet.Clear();
        hashMap.Clear();

        var neighbors = stackalloc int2[NEIGHBORS];

        queue.Add(startPixelCoord, 0.0);

        while (queue.TryPop(out var currentPixelCoord))
        {
            closedSet.Add(currentPixelCoord);

            int currentFlat = TexUtilities.PixelCoordToFlat(currentPixelCoord, textureSize.x);

            if (!allowRivers && riversIndexesMap[currentFlat] != -1)
                continue;

            double currentCost = queue.GetCost(currentPixelCoord);

            if (math.all(currentPixelCoord == endPixelCoord))
            {
                neighborDistance = currentCost;
                end = currentPixelCoord;
                return true;
            }

            var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(currentPixelCoord, textureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);

            TexUtilities.GetNeighbors4(currentPixelCoord, textureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];

                if (closedSet.Contains(neighbor))
                    continue;

                int neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
                var neighborColorFields = fieldsMap[neighborFlat];

                if (neighborColorFields != startingColor && neighborColorFields != endColor)
                {
                    closedSet.Add(neighbor);
                    continue;
                }

                var neighborUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbor, textureSize);
                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborUv);
                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere);
                double costNew = currentCost + distance;

                if (!queue.TryGetCost(neighbor, out double cost) || costNew < cost)
                {
                    queue.AddOrUpdate(neighbor, costNew);
                    hashMap[neighbor] = currentPixelCoord;
                }
            }
        }

        return false;
    }

    static int2 GetRiverCrossPixelCoord(int2 end, ref UnsafeHashMap<int2, int2> childToParent, RawArray<int> riversIndexesMap, int textureSizeX)
    {
        var current = end;

        do
        {
            if (FoundRiver(current, riversIndexesMap, textureSizeX))
                return current;
        }
        while (childToParent.TryGetValue(current, out current));

        throw new Exception("NeighborsJob2 :: GetRiverCrossPixelCoord :: Cannot find any pixelCoord!");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool FoundRiver(int2 current, RawArray<int> riversIndexesMap, int textureSizeX) => riversIndexesMap[TexUtilities.PixelCoordToFlat(current, textureSizeX)] != -1;
}
