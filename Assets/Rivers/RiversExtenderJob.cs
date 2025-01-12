using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public unsafe struct RiversExtenderJob : IJob
{
    const int NEIGHBORS = 8;
    const double DistanceMax = 0.005 / 2;

    public int2 TextureSize;
    public RawArray<int> RegionsMap;
    public RawArray<int> RiversMap;
    public RawArray<float> HeightMap;
    public RawArray<float> Distances;
    public RawBag<int2> MouthsPrimary;
    public RawBag<int2> MouthsSecondary;

    [BurstCompile]
    public void Execute()
    {
        var riversColors = RiversColorsInts.Create();
        double distanceSum = 0;

        for (int i = 0; i < MouthsPrimary.Count; i++)
        //for (int i = 0; i < 1; i++)
        {
            var current = MouthsPrimary[i];

            while (TryGetNext(current, RegionsMap, RiversMap, HeightMap, Distances, TextureSize, riversColors.Water, out var next))
            {
                var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(current, TextureSize);
                var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);

                var nextUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(next, TextureSize);
                var nextUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(nextUv);

                distanceSum += GeoUtilitiesDouble.Distance(currentUnitSphere, nextUnitSphere);

                if (distanceSum > DistanceMax)
                    break;

                int currentFlat = TexUtilities.PixelCoordToFlat(next, TextureSize.x);
                RiversMap[currentFlat] = riversColors.River;

                current = next;
            }
        }
    }

    static int2 Flip(int2 i) => new(i.x, 8192 - i.y - 1);

    static bool TryGetNext(int2 current, RawArray<int> regionsMap, RawArray<int> riversMap, RawArray<float> heightMap, RawArray<float> distances, int2 textureSize, int waterColor, out int2 next)
    {
        //Debug.Log("Current: " + Flip(current));

        var neighbors = stackalloc int2[NEIGHBORS];
        var neighborNeighbors = stackalloc int2[NEIGHBORS];
        var validNextsPtr = stackalloc ValidNext[NEIGHBORS];
        var validNexts = new RawListStackalloc<ValidNext>(validNextsPtr, NEIGHBORS);

        TexUtilities.GetNeighbors8(current, textureSize, neighbors);

        for (int i = 0; i < NEIGHBORS; i++)
        {
            int neighborFlat = TexUtilities.PixelCoordToFlat(neighbors[i], textureSize.x);
            int neighborRegion = regionsMap[neighborFlat];
            int neighborRiver = riversMap[neighborFlat];
            float neighborHeight = heightMap[neighborFlat];
            float neighborDistance = distances[neighborFlat];

            if (neighborRegion != waterColor)
                continue;

            if (neighborRiver != -1)
                continue;

            //Debug.Log("Passed?");

            if (HasOtherRiverNeighbors(current, neighbors[i], neighborNeighbors, riversMap, textureSize))
                continue;

            validNexts.Add(new ValidNext
            {
                PixelCoord = neighbors[i],
                Height = neighborHeight,
                DistanceToLand = neighborDistance,
            });
        }

        int currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);
        float currentHeight = heightMap[currentFlat];
        float currentDistance = distances[currentFlat];

        float distanceHighest = float.MinValue;
        int distanceHighestIndex = -1;

        for (int i = 0; i < validNexts.Count; i++)
        {
            float distanceDiff = validNexts[i].DistanceToLand - currentDistance;

            if (distanceHighest < distanceDiff)
            {
                distanceHighest = distanceDiff;
                distanceHighestIndex = i;
            }
        }

        //Debug.Log("Valids: " + validNexts.Count);

        if (distanceHighestIndex == -1 || distanceHighest < 0f)
        {
            next = default;
            return false;
        }

        //Debug.Log("Picked: " + heightMinIndex);

        next = validNexts[distanceHighestIndex].PixelCoord;
        return true;
    }

    static bool HasOtherRiverNeighbors(int2 current, int2 neighbor, int2* neighborNeighbors, RawArray<int> riversMap, int2 textureSize)
    {
        TexUtilities.GetNeighbors8(neighbor, textureSize, neighborNeighbors);

        //Debug.Log("Neighbor: " + Flip(neighbor));

        for (int i = 0; i < NEIGHBORS; i++)
        {
            if (math.all(current == neighborNeighbors[i]))
                continue;

            var neighborNeighborFlat = TexUtilities.PixelCoordToFlat(neighborNeighbors[i], textureSize.x);
            var neighborNeighborRiver = riversMap[neighborNeighborFlat];

            if (neighborNeighborRiver != -1)
            {
                //Debug.Log("Neighbor neighbor: " + Flip(neighborNeighbors[i]));
                return true;
            }
        }

        return false;
    }
}

struct ValidNext
{
    public int2 PixelCoord;
    public float Height;
    public float DistanceToLand;
}
