using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public unsafe struct NeighborsRiversPathsJob : IJobParallelFor
{
    public int TextureSizeX;
    public RawBag<RawBag<int2>> RiversCoords;
    public RawArray<RawBag<int2>> RiversCrossPoints;
    public RawArray<RawPtr<FinalizerUtilities.RiverPath>> RiversPaths;
    public Allocator Allocator;

    [BurstCompile]
    public void Execute(int index)
    {
        int length = RiversCrossPoints[index].Count + 2;

        var riverPointsTempPtr = stackalloc FinalizerUtilities.RiverPathPoint[length];
        var riverPointsTemp = new RawListStackalloc<FinalizerUtilities.RiverPathPoint>(riverPointsTempPtr, length);

        AssignCrossPoints(ref riverPointsTemp, RiversCoords[index], RiversCrossPoints[index], TextureSizeX);

        RiversPaths[index].Ref = new FinalizerUtilities.RiverPath
        {
            RiverPoints = GetFinalRiverPathPoints(riverPointsTemp, Allocator),
        };
    }

    static void AssignCrossPoints(ref RawListStackalloc<FinalizerUtilities.RiverPathPoint> riverPointsTemp, RawBag<int2> riversCoords, RawBag<int2> riversCrossPoints, int textureSizeX)
    {
        double distanceAccum = 0;

        riverPointsTemp.Add(new FinalizerUtilities.RiverPathPoint
        {
            PixelCoord = riversCoords[0],
            DistanceFromPrevious = 0
        });

        for (int i = 0; i < riversCoords.Count; i++)
        {
            distanceAccum += GetDistance(i, riversCoords, textureSizeX);

            if (TryFindRiverCrossPoint(riversCoords[i], riversCrossPoints, out int index))
            {
                riverPointsTemp.Add(new FinalizerUtilities.RiverPathPoint
                {
                    PixelCoord = riversCrossPoints[index],
                    DistanceFromPrevious = distanceAccum
                });

                distanceAccum = 0;
            }
        }

        riverPointsTemp.Add(new FinalizerUtilities.RiverPathPoint
        {
            PixelCoord = riversCoords[^1],
            DistanceFromPrevious = distanceAccum
        });
    }

    static double GetDistance(int i, RawBag<int2> riversCoords, int textureSizeX)
    {
        if (i == 0)
            return 0;

        var previousUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(riversCoords[i - 1], textureSizeX);
        var previousUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(previousUv);

        var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(riversCoords[i], textureSizeX);
        var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);

        return GeoUtilitiesDouble.Distance(previousUnitSphere, currentUnitSphere);
    }

    static bool TryFindRiverCrossPoint(int2 riverCoord, RawBag<int2> riversCrossPoints, out int index)
    {
        for (int i = 0; i < riversCrossPoints.Count; i++)
        {
            if (math.all(riversCrossPoints[i] == riverCoord))
            {
                index = i;
                return true;
            }    
        }

        index = default;
        return false;
    }

    static RawBag<FinalizerUtilities.RiverPathPoint> GetFinalRiverPathPoints(RawListStackalloc<FinalizerUtilities.RiverPathPoint> riverPointsTemp, Allocator allocator)
    {
        if (riverPointsTemp.Count == 0)
        {
            Debug.LogError("Is zero!");
            return default;
        }

        var riverPoints = new RawBag<FinalizerUtilities.RiverPathPoint>(allocator, riverPointsTemp.Count);

        riverPoints.Add(riverPointsTemp[0]);

        for (int i = 1; i < riverPointsTemp.Count; i++)
        {
            if (math.all(riverPointsTemp[i].PixelCoord == riverPointsTemp[i - 1].PixelCoord))
                continue;

            riverPoints.Add(riverPointsTemp[i]);
        }

        return riverPoints;
    }
}
