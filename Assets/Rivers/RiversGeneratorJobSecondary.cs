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
public unsafe struct RiversGeneratorJobSecondary : IJob
{
    const int CAPACITY = 2048;
    const int NEIGHBORS = 8;
    const Allocator ALLOCATOR = Allocator.TempJob;

    public int2 TextureSize;
    public RawArray<int> RiverMap;
    public RawArray<int> RegionMap;
    public RawArray<double> Multipliers;
    public RawBag<int2> MouthsSecondary;
    public RawArray<int> Orders;
    public RawArray<Color32> Output;

    [BurstCompile]
    public void Execute()
    {
        var riversColors = RiversColorsInts.Create();
        var queue = new RawGeoQueue<int2>(CAPACITY, CAPACITY, ALLOCATOR);
        var childToParent = new UnsafeHashMap<int2, int2>(CAPACITY, ALLOCATOR);
        var closed = new UnsafeHashSet<int2>(CAPACITY, ALLOCATOR);

        for (int i = 0; i < MouthsSecondary.Count; i++)
        {
            queue.Clear();
            childToParent.Clear();
            closed.Clear();

            var connectionOutPoint = FindConnection(MouthsSecondary[i], RiverMap, Multipliers, Output, Orders, TextureSize, ref riversColors, ref queue, ref childToParent, ref closed);

            DrawRiver(connectionOutPoint, RegionMap, Output, Orders, TextureSize, ref riversColors, childToParent);
        }

        queue.Dispose();
        childToParent.Dispose();
        closed.Dispose();
    }

    static int2 FindConnection(
        int2 mouth, RawArray<int> riversMap, RawArray<double> multipliers, RawArray<Color32> output, RawArray<int> orders, int2 textureSize,
        ref RiversColorsInts riversColors, ref RawGeoQueue<int2> queue, ref UnsafeHashMap<int2, int2> childToParent, ref UnsafeHashSet<int2> closed)
    {
        var neighbors = stackalloc int2[NEIGHBORS];

        queue.Add(mouth, 0.0);

        while (queue.TryPop(out var current))
        {
            closed.Add(current);

            int currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);
            var currentColorOutput = output[currentFlat];
            var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(current, textureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);
            double currentCost = queue.GetCost(current);

            TexUtilities.GetNeighbors8(current, textureSize, neighbors);

            var neighborsValidData = stackalloc int2[NEIGHBORS];
            var neighborsValidList = new RawListStackalloc<int2>(neighborsValidData, NEIGHBORS);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];

                if (closed.Contains(neighbor))
                    continue;

                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
                var neighborColorRiver = riversMap[neighborFlat];
                var neighborColorOutput = output[neighborFlat];

                if (riversColors.IsNotRiver(neighborColorRiver))
                {
                    closed.Add(neighbor);
                    continue;
                }

                neighborsValidList.Add(neighbor);

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

            if (TryFindRiverNeighbor(neighborsValidList, output, orders, textureSize, out var riverNeighbor))
                return riverNeighbor;
        }

        throw new System.Exception("RiverManagerJobSecondary :: Cannot find connection!");
    }

    static bool TryFindRiverNeighbor(RawListStackalloc<int2> neighbors, RawArray<Color32> output, RawArray<int> orders, int2 textureSize, out int2 riverNeighbor)
    {
        int lowestOrder = int.MaxValue;
        int lowestOrderIndex = -1;

        for (int i = 0; i < neighbors.Count; i++)
        {
            var neighbor = neighbors[i];

            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, textureSize.x);
            var neighborColor = output[neighborFlat];

            if (!RiverColors.IsRiver(neighborColor))
                continue;

            int order = orders[neighborFlat];

            if (lowestOrder > order)
            {
                lowestOrder = order;
                lowestOrderIndex = i;
            }
        }

        if (lowestOrderIndex == -1)
        {
            riverNeighbor = default;
            return false;
        }

        riverNeighbor = neighbors[lowestOrderIndex];
        return true;
    }

    static void DrawRiver(
        int2 connectionOutPoint, RawArray<int> regionsMap, RawArray<Color32> output,
        RawArray<int> orders, int2 textureSize, ref RiversColorsInts riversColors, UnsafeHashMap<int2, int2> childToParent)
    {
        int order = 0;

        var previous = connectionOutPoint;
        var current = childToParent[connectionOutPoint];

        RiversGenerator.GetRiverParams(previous, current, regionsMap, textureSize, riversColors.Water, out int previousFlat, out _, out int currentFlat, out _);

        DrawPixel(previousFlat, RiverColors.ConnectionOutPoint, output, orders, ref order, false);
        DrawPixel(currentFlat, RiverColors.ConnectionOut, output, orders, ref order, false);

        previous = childToParent[current];
        current = childToParent[previous];

        while (true)
        {
            RiversGenerator.GetRiverParams(previous, current, regionsMap, textureSize, riversColors.Water, out previousFlat, out bool previousIsWater, out currentFlat, out bool currentIsWater);

            // going through water
            if (previousIsWater && currentIsWater) { }

            // going through land
            else if (!previousIsWater && !currentIsWater)
            {
                DrawPixel(previousFlat, RiverColors.River, output, orders, ref order);
            }

            // water to land
            else if (previousIsWater && !currentIsWater)
            {
                DrawPixel(previousFlat, RiverColors.Source, output, orders, ref order);
                DrawPixel(currentFlat, RiverColors.River, output, orders, ref order);
            }

            // land to water
            else if (!previousIsWater && currentIsWater)
            {
                DrawPixel(previousFlat, RiverColors.River, output, orders, ref order);
                DrawPixel(currentFlat, RiverColors.MouthSecondary, output, orders, ref order);

                // we return immediatelly because
                // we ignore the case where secondary river can go though water (ex. lake)
                // since we don't know what mouth type it should create
                return;
            }

            if (childToParent.TryGetValue(current, out var next))
            {
                previous = current;
                current = next;
            }
            else
            {
                // inland mouth 
                if (!previousIsWater && !currentIsWater)
                {
                    DrawPixel(previousFlat, RiverColors.MouthSecondary, output, orders, ref order);
                }

                return;
            }
        }
    }

    static void DrawPixel(int flat, Color32 color, RawArray<Color32> output, RawArray<int> orders, ref int order, bool setOrder = false)
    {
        output[flat] = color;

        if (setOrder)
            orders[flat] = order--;
    }
}
