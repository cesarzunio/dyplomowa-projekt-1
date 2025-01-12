using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public unsafe struct RiversGeneratorJobPrimary2 : IJob
{
    const int CAPACITY = 2048;
    const int NEIGHBORS = 8;
    const Allocator ALLOCATOR = Allocator.TempJob;

    public int2 TextureSize;
    public RawArray<int> RiverMap;
    public RawArray<int> RegionMap;
    public RawArray<double> Multipliers;
    public RawBag<int2> MouthsPrimary;
    public RawArray<bool> Closed;
    public RawArray<int> Orders;
    public RawArray<Color32> Output;

    [BurstCompile]
    public void Execute()
    {
        var riversColors = RiversColorsInts.Create();
        var queue = new RawGeoQueue<int2>(CAPACITY, CAPACITY, ALLOCATOR);
        var childToParent = new UnsafeHashMap<int2, int2>(CAPACITY, ALLOCATOR);
        var closed = new UnsafeHashSet<int2>(CAPACITY, ALLOCATOR);
        var drawn = new UnsafeHashSet<int2>(CAPACITY, ALLOCATOR);
        var sources = new RawBag<int2>(ALLOCATOR);

        for (int i = 0; i < MouthsPrimary.Count; i++)
        {
            queue.Clear();
            childToParent.Clear();
            closed.Clear();
            sources.Clear();

            FindSources(MouthsPrimary[i], RiverMap, Multipliers, TextureSize, ref riversColors, ref queue, ref closed, ref childToParent, ref sources);

            while (TryPopHighestCost(ref sources, queue, out var source))
            {
                drawn.Clear();
                DrawRiver(source, RegionMap, Output, Orders, TextureSize, ref riversColors, childToParent, ref drawn);
            }
        }

        queue.Dispose();
        childToParent.Dispose();
        closed.Dispose();
        drawn.Dispose();
        sources.Dispose();
    }

    static void FindSources(
        int2 mouth, RawArray<int> riversMap, RawArray<double> multipliers, int2 textureSize, ref RiversColorsInts riversColors,
        ref RawGeoQueue<int2> queue, ref UnsafeHashSet<int2> closed, ref UnsafeHashMap<int2, int2> childToParent, ref RawBag<int2> sources)
    {
        var neighbors = stackalloc int2[NEIGHBORS];

        queue.Add(mouth, 0.0);

        while (queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);
            var currentColor = riversMap[currentFlat];
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

                if (closed.Contains(neighbor))
                    continue;

                var neighborColor = riversMap[neighborFlat];

                if (riversColors.IsNotRiver(neighborColor))
                {
                    closed.Add(neighbor);
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
        }
    }

    static int2 Flip(int2 i) => new(i.x, 8192 - i.y - 1);

    static bool TryPopHighestCost(ref RawBag<int2> sources, RawGeoQueue<int2> queue, out int2 source)
    {
        double highestCost = double.MinValue;
        int highestCostIndex = -1;

        for (int i = 0; i < sources.Count; i++)
        {
            double cost = queue.GetCost(sources[i]);

            if (highestCost < cost)
            {
                highestCost = cost;
                highestCostIndex = i;
            }
        }

        if (highestCostIndex == -1)
        {
            source = default;
            return false;
        }

        source = sources[highestCostIndex];
        sources.RemoveAt(highestCostIndex);
        return true;
    }

    static bool TryPopLowestCost(ref RawBag<int2> sources, RawGeoQueue<int2> queue, out int2 source)
    {
        double lowestCost = double.MaxValue;
        int lowestCostIndex = -1;

        for (int i = 0; i < sources.Count; i++)
        {
            double cost = queue.GetCost(sources[i]);

            if (lowestCost > cost)
            {
                lowestCost = cost;
                lowestCostIndex = i;
            }
        }

        if (lowestCostIndex == -1)
        {
            source = default;
            return false;
        }

        source = sources[lowestCostIndex];
        sources.RemoveAt(lowestCostIndex);
        return true;
    }

    static void DrawRiver(
        int2 source, RawArray<int> regionsMap, RawArray<Color32> output, RawArray<int> orders,
        int2 textureSize, ref RiversColorsInts riversColors, UnsafeHashMap<int2, int2> childToParent, ref UnsafeHashSet<int2> drawn)
    {
        var previous = source;
        var current = childToParent[source];

        int order = 0;
        bool firstIt = true;

        while (true)
        {
            RiversGenerator.GetRiverParams(previous, current, regionsMap, textureSize, riversColors.Water, out int previousFlat, out bool previousIsWater, out int currentFlat, out bool currentIsWater);

            // going through water
            if (previousIsWater && currentIsWater) { }

            // going through land
            else if (!previousIsWater && !currentIsWater)
            {
                if (firstIt)
                {
                    DrawPixel(previous, previousFlat, RiverColors.Source, output, orders, ref drawn, ref order);
                }
                else
                {
                    DrawPixel(previous, previousFlat, RiverColors.River, output, orders, ref drawn, ref order);
                }
            }

            // water to land
            else if (previousIsWater && !currentIsWater)
            {
                DrawPixel(previous, previousFlat, RiverColors.Source, output, orders, ref drawn, ref order);
                DrawPixel(current, currentFlat, RiverColors.River, output, orders, ref drawn, ref order);
            }

            // land to water
            else if (!previousIsWater && currentIsWater)
            {
                DrawPixel(previous, previousFlat, RiverColors.River, output, orders, ref drawn, ref order);
                DrawPixel(current, currentFlat, RiverColors.Mouth, output, orders, ref drawn, ref order);
            }

            firstIt = false;

            if (childToParent.TryGetValue(current, out var next))
            {
                if (TryFindConnectionInPoint(next, output, orders, drawn, textureSize, out var connectionInPoint))
                {
                    int nextFlat = TexUtilities.PixelCoordToFlat(next, textureSize.x);
                    bool nextIsWater = regionsMap[nextFlat] == riversColors.Water;
                    int connectionInPointFlat = TexUtilities.PixelCoordToFlat(connectionInPoint, textureSize.x);

                    // there is connectlon but we are in the water
                    if (currentIsWater || nextIsWater)
                        return;

                    output[currentFlat] = RiverColors.River;
                    orders[currentFlat] = order++;

                    output[nextFlat] = RiverColors.ConnectionIn;
                    orders[nextFlat] = order++;

                    output[connectionInPointFlat] = RiverColors.ConnectionInPoint;

                    return;
                }

                previous = current;
                current = next;
                continue;
            }

            // inland mouth 
            if (!currentIsWater)
            {
                output[currentFlat] = RiverColors.Mouth;
                orders[currentFlat] = order++;
            }

            return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void DrawPixel(int2 coord, int flat, Color32 color, RawArray<Color32> output, RawArray<int> orders, ref UnsafeHashSet<int2> drawn, ref int order)
    {
        output[flat] = color;
        orders[flat] = order++;
        drawn.Add(coord);
    }

    static bool TryFindConnectionInPoint(int2 next, RawArray<Color32> output, RawArray<int> orders, UnsafeHashSet<int2> drawn, int2 textureSize, out int2 connectionInPoint)
    {
        var neighbors = stackalloc int2[NEIGHBORS];
        TexUtilities.GetNeighbors8(next, textureSize, neighbors);

        int highestOrder = int.MinValue;
        int highestOrderIndex = -1;

        for (int i = 0; i < NEIGHBORS; i++)
        {
            var neighbor = neighbors[i];

            if (drawn.Contains(neighbor))
                continue;

            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
            var neighborColor = output[neighborFlat];

            if (neighborColor.a == 0)
                continue;

            int order = orders[neighborFlat];

            if (highestOrder < order)
            {
                highestOrder = order;
                highestOrderIndex = i;
            }
        }

        if (highestOrderIndex == -1)
        {
            connectionInPoint = default;
            return false;
        }

        connectionInPoint = neighbors[highestOrderIndex];
        return true;
    }
}
