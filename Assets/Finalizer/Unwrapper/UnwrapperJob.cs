using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

//[BurstCompile]
public unsafe struct UnwrapperJob : IJob
{
    const int CAPACITY_PIXELS = 2048;
    const int CAPACITY_SOURCES = 128;
    const int NEIGHBORS = 8;
    const Allocator ALLOCATOR = Allocator.TempJob;

    public int2 TextureSize;
    public RawArray<int> RiversMap;
    public RawArray<double> Multipliers;
    public RawBag<int2> MouthsPrimary;
    public RawBag<int2> MouthsSecondary;
    public RawArray<int> RiversIndexesMap;
    public RawPtr<RawBag<RawBag<int2>>> RiversCoords;
    public Allocator Allocator;

    //[BurstCompile]
    public void Execute()
    {
        var riversColors = RiversColorsInts.Create();
        var queue = new RawGeoQueue<int2>(CAPACITY_PIXELS, CAPACITY_PIXELS, ALLOCATOR);
        var closed = new UnsafeHashSet<int2>(CAPACITY_PIXELS, ALLOCATOR);
        var childToParent = new UnsafeHashMap<int2, int2>(CAPACITY_PIXELS, ALLOCATOR);
        var orders = new RawArray<int>(ALLOCATOR, 0, RiversMap.Length);
        var sourcesPtr = stackalloc int2[CAPACITY_SOURCES];
        var sources = new RawListStackalloc<int2>(sourcesPtr, CAPACITY_SOURCES);
        int riverIndex = -1;

        for (int i = 0; i < MouthsPrimary.Count; i++)
        {
            FindSources(MouthsPrimary[i], RiversMap, Multipliers, TextureSize, ref riversColors, ref queue, ref closed, ref childToParent, ref sources);

            for (int j = 0; j < sources.Count; j++)
            {
                DrawRiverPrimary(sources[j], RiversMap, RiversIndexesMap, orders, TextureSize.x, ref riversColors, childToParent, ref riverIndex, ref RiversCoords.Ref, Allocator);
            }
        }

        //for (int i = 0; i < MouthsSecondary.Count; i++)
        //{
        //    var connectionPoint = FindConnectionPoint(MouthsSecondary[i], RiversMap, Multipliers, orders, TextureSize, ref riversColors, ref queue, ref childToParent, ref closed);
        //
        //    DrawRiverSecondary(connectionPoint, RiversMap, RiversIndexesMap, orders, TextureSize.x, ref riversColors, childToParent, ref riverIndex, ref RiversCoords.Ref, Allocator);
        //}

        queue.Dispose();
        closed.Dispose();
        childToParent.Dispose();
        orders.Dispose();
    }

    static int2 Flip(int2 i) => new(i.x, 8192 - i.y - 1);

    // --------------------------------------------------------------------------------------
    // --------------------------------------------------------------------------------------
    // --------------------------------------------------------------------------------------

    static void FindSources(
        int2 mouth, RawArray<int> riverMap, RawArray<double> multipliers, int2 textureSize, ref RiversColorsInts riversColors,
        ref RawGeoQueue<int2> queue, ref UnsafeHashSet<int2> closed, ref UnsafeHashMap<int2, int2> childToParent, ref RawListStackalloc<int2> sources)
    {
        queue.Clear();
        closed.Clear();
        childToParent.Clear();
        sources.Clear();

        var neighbors = stackalloc int2[NEIGHBORS];

        queue.Add(mouth, 0.0);

        while (queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);
            var currentColor = riverMap[currentFlat];
            var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(current, textureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);
            double currentCost = queue.GetCost(current);

            closed.Add(current);

            if (currentColor == riversColors.Source)
            {
                sources.Add(current);
            }

            TexUtilities.GetNeighbors8(current, textureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
                var neighborColor = riverMap[neighborFlat];
                var neighborUv = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborUv);

                if (closed.Contains(neighbor))
                    continue;

                if (riversColors.IsNotRiver(neighborColor))
                {
                    closed.Add(neighbor);
                    continue;
                }

                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere) * RiverUtilities.CalculateMultiplier(currentFlat, neighborFlat, multipliers);
                double costNew = currentCost + distance;

                if (!queue.TryGetCost(neighbor, out double cost) || costNew < cost)
                {
                    queue.AddOrUpdate(neighbor, costNew);
                    childToParent[neighbor] = current;
                }
            }
        }
    }

    // --------------------------------------------------------------------------------------
    // --------------------------------------------------------------------------------------
    // --------------------------------------------------------------------------------------

    static int2 FindConnectionPoint(
        int2 mouthSecondary, RawArray<int> riversMap, RawArray<double> multipliers, RawArray<int> orders, int2 textureSize, ref RiversColorsInts riversColors,
        ref RawGeoQueue<int2> queue, ref UnsafeHashMap<int2, int2> childToParent, ref UnsafeHashSet<int2> closed)
    {
        queue.Clear();
        closed.Clear();
        childToParent.Clear();

        var neighbors = stackalloc int2[NEIGHBORS];

        queue.Add(mouthSecondary, 0.0);

        while (queue.TryPop(out var current))
        {
            closed.Add(current);

            int currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);
            var currentColor = riversMap[currentFlat];
            var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(current, textureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);
            double currentCost = queue.GetCost(current);

            bool currentIsConnection = riversColors.IsConnection(currentColor);

            var neighborsValidData = stackalloc int2[NEIGHBORS];
            var neighborsValidList = new RawListStackalloc<int2>(neighborsValidData, NEIGHBORS);

            TexUtilities.GetNeighbors8(current, textureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];

                if (closed.Contains(neighbor))
                    continue;

                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
                var neighborColor = riversMap[neighborFlat];

                if (riversColors.IsNotRiver(neighborColor))
                {
                    closed.Add(neighbor);
                    continue;
                }

                if (currentIsConnection && riversColors.IsConnectionPoint(neighborColor))
                {
                    childToParent[neighbor] = current;
                    neighborsValidList.Add(neighbor);
                    continue;
                }

                var neighborUv = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborUv);

                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere) * RiverUtilities.CalculateMultiplier(currentFlat, neighborFlat, multipliers);
                double costNew = currentCost + distance;

                if (!queue.TryGetCost(neighbor, out double cost) || costNew < cost)
                {
                    queue.AddOrUpdate(neighbor, costNew);
                    childToParent[neighbor] = current;
                }
            }

            if (TryGetConnectionPoint(neighborsValidList, riversMap, orders, textureSize, out var connectionPoint))
                return connectionPoint;
        }

        //Debug.Log(Flip(mouthSecondary));
        throw new System.Exception("RiverManagerJobSecondary :: Cannot find connection!");
    }

    static bool TryGetConnectionPoint(RawListStackalloc<int2> neighbors, RawArray<int> riversMap, RawArray<int> orders, int2 textureSize, out int2 connectionPoint)
    {
        int lowestOrder = int.MaxValue;
        int lowestOrderIndex = -1;

        for (int i = 0; i < neighbors.Count; i++)
        {
            var neighbor = neighbors[i];

            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
            var neighborColor = riversMap[neighborFlat].ToColor32();
            int order = orders[neighborFlat];

            if (!RiverColors.IsRiver(neighborColor))
                continue;

            if (lowestOrder > order)
            {
                lowestOrder = order;
                lowestOrderIndex = i;
            }
        }

        if (lowestOrderIndex == -1)
        {
            connectionPoint = default;
            return false;
        }

        connectionPoint = neighbors[lowestOrderIndex];
        return true;
    }

    // --------------------------------------------------------------------------------------
    // --------------------------------------------------------------------------------------
    // --------------------------------------------------------------------------------------

    static void DrawRiverPrimary(
        int2 start, RawArray<int> riversMap, RawArray<int> riversIndexesMap, RawArray<int> orders, int textureSizeX, ref RiversColorsInts riversColors,
        UnsafeHashMap<int2, int2> childToParent, ref int riverIndex, ref RawBag<RawBag<int2>> riversCoords, Allocator allocator)
    {
        int bagCapacity = childToParent.Capacity;
        var riverCurrentCoords = new RawBag<int2>(allocator, bagCapacity);
        var current = start;

        int order = 0;
        int currentFlat;
        int parentFlat;

        riverIndex++;

        int drawn = 0;

        while (childToParent.TryGetValue(current, out var parent))
        {
            currentFlat = TexUtilities.PixelCoordToFlat(current, textureSizeX);
            parentFlat = TexUtilities.PixelCoordToFlat(parent, textureSizeX);
            var currentColor = riversMap[currentFlat];
            var parentColor = riversMap[parentFlat];

            if (riversIndexesMap[currentFlat] != -1)
            {
                Debug.Log($"Overdraw: {riverIndex}");
            }

            riversIndexesMap[currentFlat] = riverIndex;
            orders[currentFlat] = order++;
            riverCurrentCoords.Add(current);

            if (riversColors.IsConnection(currentColor) && riversColors.IsConnectionPoint(parentColor))
            {
                riverCurrentCoords.Add(parent);
                AddCoords(ref riverCurrentCoords, ref riversCoords);
                return;
            }

            if (riversColors.IsConnectionPoint(currentColor))
            {
                AddCoords(ref riverCurrentCoords, ref riversCoords, bagCapacity, allocator);
                riverCurrentCoords.Add(current);
                riverIndex++;
            }

            current = parent;
        }

        currentFlat = TexUtilities.PixelCoordToFlat(current, textureSizeX);

        if (riversIndexesMap[currentFlat] != -1)
        {
            Debug.Log($"Overdraw: {riverIndex}");
        }

        riversIndexesMap[currentFlat] = riverIndex;
        orders[currentFlat] = order++;
        riverCurrentCoords.Add(current);

        AddCoords(ref riverCurrentCoords, ref riversCoords);
    }

    static void DrawRiverSecondary(
        int2 start, RawArray<int> riversMap, RawArray<int> riversIndexesMap, RawArray<int> orders, int textureSizeX, ref RiversColorsInts riversColors,
        UnsafeHashMap<int2, int2> childToParent, ref int riverIndex, ref RawBag<RawBag<int2>> riversCoords, Allocator allocator)
    {
        int bagCapacity = childToParent.Capacity;
        var riverCurrentCoords = new RawBag<int2>(allocator, bagCapacity);
        riverCurrentCoords.Add(start);

        if (!childToParent.TryGetValue(start, out var current))
        {
            riverCurrentCoords.Dispose();
            throw new System.Exception("IndexerJob :: DrawRiverSecondary :: River has only one coord, this makes no sense.");
        }

        int order = 0;
        int currentFlat;

        riverIndex++;

        while (childToParent.TryGetValue(current, out var parent))
        {
            currentFlat = TexUtilities.PixelCoordToFlat(current, textureSizeX);
            var currentColor = riversMap[currentFlat];

            riversIndexesMap[currentFlat] = riverIndex;
            orders[currentFlat] = order--;
            riverCurrentCoords.Add(current);

            if (riversColors.IsConnectionPoint(currentColor))
            {
                AddCoords(ref riverCurrentCoords, ref riversCoords, bagCapacity, allocator);
                riverCurrentCoords.Add(current);
                riverIndex++;
            }

            current = parent;
        }

        currentFlat = TexUtilities.PixelCoordToFlat(current, textureSizeX);

        riversIndexesMap[currentFlat] = riverIndex;
        orders[currentFlat] = order--;
        riverCurrentCoords.Add(current);

        AddCoords(ref riverCurrentCoords, ref riversCoords);
    }

    static void AddCoords(ref RawBag<int2> riverCurrentCoords, ref RawBag<RawBag<int2>> riversCoords)
    {
        if (riverCurrentCoords.Count > 0)
        {
            riversCoords.Add(riverCurrentCoords);
        }
        else
        {
            riverCurrentCoords.Dispose();
        }
    }

    static void AddCoords(ref RawBag<int2> riverCurrentCoords, ref RawBag<RawBag<int2>> riversCoords, int bagCapacity, Allocator allocator)
    {
        AddCoords(ref riverCurrentCoords, ref riversCoords);
        riverCurrentCoords = new RawBag<int2>(allocator, bagCapacity);
    }
}
