//using System;
//using Unity.Mathematics;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Jobs;
//using Unity.Burst;
//using Unity.Collections;
//using static UnwrapperUtilities;

//[BurstCompile]
//public unsafe struct EdgePathJob : IJobParallelForBatch
//{
//    const int NEIGHBORS = 8;

//    public Allocator Allocator;
//    public int2 TextureSize;
//    public RawArray<int> FieldsMap;
//    public RawArray<RiverPointType> RiversPointTypes;

//    public RawArray<RawGeoQueueHeuristic<HeuristicGeo>> Queues;
//    public RawArray<UnsafeHashSet<int2>> ClosedSets;
//    public RawArray<UnsafeHashMap<int2, int2>> HashMaps;
//    public RawArray<uint> SpinLockUses;
//    public RawPtr<CesSpinLock> SpinLock;

//    public RawArray<EdgePathsUtility.EdgePathInput> EdgePathInputs;
//    public RawArray<EdgePathsUtility.EdgePathOutput> EdgePathOutputs;

//    [BurstCompile]
//    public void Execute(int startIndex, int count)
//    {
//        int freeIndex = GetFreeIndex(SpinLockUses, SpinLock);

//        for (int i = startIndex; i < startIndex + count; i++)
//        {
//            var input = EdgePathInputs[i];
//            bool found;

//            if (input.PathType != EdgePathsUtility.EdgePathType.RiverRiver)
//            {
//                bool allowRivers = input.PathType != EdgePathsUtility.EdgePathType.FieldField;
//                found = FindPathDefault(input.Start, input.End, TextureSize, FieldsMap, RiversPointTypes, allowRivers, ref Queues[freeIndex], ref ClosedSets[freeIndex], ref HashMaps[freeIndex]);
//            }
//            else
//            {
//                found = FindPathRiver(input.Start, input.End, TextureSize, RiversPointTypes, ref Queues[freeIndex], ref ClosedSets[freeIndex], ref HashMaps[freeIndex]);
//            }

//            if (found)
//            {
//                EdgePathOutputs[i] = new EdgePathsUtility.EdgePathOutput(GetPath(input.End, ref HashMaps[freeIndex], Allocator));
//            }
//        }

//        ReleaseFreeIndex(SpinLockUses, SpinLock, freeIndex);
//    }

//    static int GetFreeIndex(RawArray<uint> spinLockUses, RawPtr<CesSpinLock> spinLock)
//    {
//        spinLock.Ptr->Lock();
//        bool found = TryFindFree(spinLockUses, out int freeIndex);

//        if (!found)
//        {
//            spinLock.Ptr->Unlock();
//            throw new Exception("EdgePathJob:: GetFreeIndex :: Can't find free index!");
//        }

//        spinLockUses[freeIndex] = 1;
//        spinLock.Ptr->Unlock();

//        return freeIndex;
//    }

//    static bool TryFindFree(RawArray<uint> spinLockUses, out int freeIndex)
//    {
//        for (int i = 0; i < spinLockUses.Length; i++)
//        {
//            if (spinLockUses[i] == 0)
//            {
//                freeIndex = i;
//                return true;
//            }
//        }

//        freeIndex = -1;
//        return false;
//    }

//    static void ReleaseFreeIndex(RawArray<uint> spinLockUses, RawPtr<CesSpinLock> spinLock, int freeIndex)
//    {
//        spinLock.Ptr->Lock();
//        spinLockUses[freeIndex] = 0;
//        spinLock.Ptr->Unlock();
//    }

//    static bool FindPathDefault(
//        int2 startPixelCoord, int2 endPixelCoord, int2 textureSize, RawArray<int> fieldsMap, RawArray<RiverPointType> riversPointTypes, bool allowRivers,
//        ref RawGeoQueueHeuristic<HeuristicGeo> queue, ref UnsafeHashSet<int2> closedSet, ref UnsafeHashMap<int2, int2> hashMap)
//    {
//        int startingColor = fieldsMap[TexUtilities.PixelCoordToFlat(startPixelCoord, textureSize.x)];
//        int endColor = fieldsMap[TexUtilities.PixelCoordToFlat(endPixelCoord, textureSize.x)];

//        queue.Clear(new HeuristicGeo(textureSize, endPixelCoord));
//        closedSet.Clear();
//        hashMap.Clear();

//        var neighbors = stackalloc int2[NEIGHBORS];

//        queue.Add(startPixelCoord, 0.0);

//        while (queue.TryPop(out var currentPixelCoord))
//        {
//            if (math.all(currentPixelCoord == endPixelCoord))
//                return true;

//            closedSet.Add(currentPixelCoord);

//            int currentFlat = TexUtilities.PixelCoordToFlat(currentPixelCoord, textureSize.x);
//            var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(currentPixelCoord, textureSize);
//            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);
//            double currentCost = queue.GetCost(currentPixelCoord);

//            TexUtilities.GetNeighbors8(currentPixelCoord, textureSize, neighbors);

//            for (int i = 0; i < NEIGHBORS; i++)
//            {
//                var neighbor = neighbors[i];

//                if (closedSet.Contains(neighbor))
//                    continue;

//                int neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
//                var neighborColorFields = fieldsMap[neighborFlat];

//                if (neighborColorFields != startingColor && neighborColorFields != endColor)
//                {
//                    closedSet.Add(neighbor);
//                    continue;
//                }

//                if (!allowRivers && riversPointTypes[neighborFlat] != RiverPointType.None)
//                {
//                    closedSet.Add(neighbor);
//                    continue;
//                }

//                var neighborUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbor, textureSize);
//                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborUv);
//                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere);
//                double costNew = currentCost + distance;

//                if (!queue.TryGetCost(neighbor, out double cost) || costNew < cost)
//                {
//                    queue.AddOrUpdate(neighbor, costNew);
//                    hashMap[neighbor] = currentPixelCoord;
//                }
//            }
//        }

//        return false;
//        //throw new Exception("EdgePathJob :: FindPathDefault :: Cannot find end!");
//    }

//    static bool FindPathRiver(
//        int2 startPixelCoord, int2 endPixelCoord, int2 textureSize, RawArray<RiverPointType> riversPointTypes,
//        ref RawGeoQueueHeuristic<HeuristicGeo> queue, ref UnsafeHashSet<int2> closedSet, ref UnsafeHashMap<int2, int2> hashMap)
//    {
//        queue.Clear(new HeuristicGeo(textureSize, endPixelCoord));
//        closedSet.Clear();
//        hashMap.Clear();

//        var neighbors = stackalloc int2[NEIGHBORS];

//        queue.Add(startPixelCoord, 0.0);

//        while (queue.TryPop(out var currentPixelCoord))
//        {
//            if (math.all(currentPixelCoord == endPixelCoord))
//                return true;

//            closedSet.Add(currentPixelCoord);

//            int currentFlat = TexUtilities.PixelCoordToFlat(currentPixelCoord, textureSize.x);
//            var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(currentPixelCoord, textureSize);
//            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);
//            double currentCost = queue.GetCost(currentPixelCoord);

//            TexUtilities.GetNeighbors8(currentPixelCoord, textureSize, neighbors);

//            for (int i = 0; i < NEIGHBORS; i++)
//            {
//                var neighbor = neighbors[i];

//                if (closedSet.Contains(neighbor))
//                    continue;

//                int neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);

//                if (riversPointTypes[neighborFlat] == RiverPointType.None)
//                {
//                    closedSet.Add(neighbor);
//                    continue;
//                }

//                var neighborUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbor, textureSize);
//                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborUv);
//                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere);
//                double costNew = currentCost + distance;

//                if (!queue.TryGetCost(neighbor, out double cost) || costNew < cost)
//                {
//                    queue.AddOrUpdate(neighbor, costNew);
//                    hashMap[neighbor] = currentPixelCoord;
//                }
//            }
//        }

//        return false;
//        //throw new Exception("EdgePathJob :: FindPathRiver :: Cannot find end!");
//    }

//    static RawBag<int2> GetPath(int2 end, ref UnsafeHashMap<int2, int2> hashMap, Allocator allocator)
//    {
//        var path = new RawBag<int2>(allocator);
//        var current = end;

//        do
//        {
//            path.Add(current);
//        }
//        while (hashMap.TryGetValue(current, out current));

//        return path;
//    }
//}
