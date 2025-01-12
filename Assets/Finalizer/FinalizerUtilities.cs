using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using System;
using static FinalizerSaves;

public static unsafe class FinalizerUtilities
{
    public static RawArray<RawBag<int2>> GetFieldToPixelCoords(int fieldsCount, FieldsMap fieldsMap, Allocator allocator)
    {
        var fieldToPixelCoords = new RawArray<RawBag<int2>>(allocator, fieldsCount);

        for (int i = 0; i < fieldToPixelCoords.Length; i++)
        {
            fieldToPixelCoords[i] = new RawBag<int2>(allocator, 64);
        }

        for (int y = 0; y < fieldsMap.TextureSize.y; y++)
        {
            for (int x = 0; x < fieldsMap.TextureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                int flat = TexUtilities.PixelCoordToFlat(x, y, fieldsMap.TextureSize.x);
                uint field = fieldsMap.Fields[flat];

                fieldToPixelCoords[field].Add(pixelCoord);
            }
        }

        return fieldToPixelCoords;
    }

    public static void MapColorsToIndexes(RawArray<int> fieldsMap, int fieldsCountMax, out Dictionary<int, int> colorToIndex, out List<int> indexToColor)
    {
        colorToIndex = new Dictionary<int, int>(fieldsCountMax);
        indexToColor = new List<int>(fieldsCountMax);

        for (int i = 0; i < fieldsMap.Length; i++)
        {
            var color = fieldsMap[i];
            int index = indexToColor.Count;

            if (color == -1)
                continue;

            if (colorToIndex.TryAdd(color, index))
            {
                indexToColor.Add(color);
            }
        }
    }

    const int NEIGHBORS_DATA_HASH_CAPACITY = 8192;

    public static unsafe void CreateNeighbors(
        RawArray<int> fieldsMap, RawArray<int> riversIndexesMap, RawArray<BordersContainers.BorderSorted> bordersSorted,
        RawArray<double2> indexToCenterGeoCoords, Dictionary<int, int> colorToIndex, int2 textureSize, Allocator allocator,
        out RawArray<NeighborType> neighborsTypes, out RawArray<double2> neighborsDistances, out RawArray<int2> riversCrossPixelCoords)
    {
        CreateBordersFieldsCenters(bordersSorted, indexToCenterGeoCoords, textureSize, colorToIndex, allocator, out var bordersfieldsCenters);

        neighborsTypes = new RawArray<NeighborType>(allocator, bordersSorted.Length);
        neighborsDistances = new RawArray<double2>(allocator, bordersSorted.Length);
        riversCrossPixelCoords = new RawArray<int2>(allocator, bordersSorted.Length);

        int processorCount = SystemInfo.processorCount;
        var queues = new RawArray<RawGeoQueueHeuristic<HeuristicGeo>>(allocator, processorCount);
        var closedSets = new RawArray<UnsafeHashSet<int2>>(allocator, processorCount);
        var hashMaps = new RawArray<UnsafeHashMap<int2, int2>>(allocator, processorCount);

        for (int i = 0; i < processorCount; i++)
        {
            queues[i] = new RawGeoQueueHeuristic<HeuristicGeo>(NEIGHBORS_DATA_HASH_CAPACITY, NEIGHBORS_DATA_HASH_CAPACITY, default, allocator);
            closedSets[i] = new UnsafeHashSet<int2>(NEIGHBORS_DATA_HASH_CAPACITY, allocator);
            hashMaps[i] = new UnsafeHashMap<int2, int2>(NEIGHBORS_DATA_HASH_CAPACITY, allocator);
        }

        var job = new NeighborsJob2
        {
            TextureSize = textureSize,
            FieldsMap = fieldsMap,
            RiversIndexesMap = riversIndexesMap,
            FieldsCenters = bordersfieldsCenters,
            Queues = queues,
            ClosedSets = closedSets,
            HashMaps = hashMaps,
            NeighborTypes = neighborsTypes,
            NeighborDistances = neighborsDistances,
            RiversCrossPixelCoords = riversCrossPixelCoords,
        };

        job.Schedule(bordersSorted.Length, 4096).Complete();

        for (int i = 0; i < processorCount; i++)
        {
            queues[i].Dispose();
            closedSets[i].Dispose();
            hashMaps[i].Dispose();
        }

        queues.Dispose();
        closedSets.Dispose();
    }

    static void CreateBordersFieldsCenters(
        RawArray<BordersContainers.BorderSorted> bordersSorted, RawArray<double2> indexToCenterGeoCoords, int2 textureSize,
        Dictionary<int, int> colorToIndex, Allocator allocator, out RawArray<int4> bordersfieldsCenters)
    {
        bordersfieldsCenters = new RawArray<int4>(allocator, bordersSorted.Length);

        for (int i = 0; i < bordersfieldsCenters.Length; i++)
        {
            int fromIndex = colorToIndex[bordersSorted[i].FieldColorA];
            int toIndex = colorToIndex[bordersSorted[i].FieldColorB];

            var fromCenterGeoCoord = indexToCenterGeoCoords[fromIndex];
            var toCenterGeoCoord = indexToCenterGeoCoords[toIndex];

            var fromCenterUv = GeoUtilitiesDouble.GeoCoordsToPlaneUv(fromCenterGeoCoord);
            var toCenterUv = GeoUtilitiesDouble.GeoCoordsToPlaneUv(toCenterGeoCoord);

            var fromCenterPixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(fromCenterUv, textureSize);
            var toCenterPixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(toCenterUv, textureSize);

            bordersfieldsCenters[i] = new int4(fromCenterPixelCoord, toCenterPixelCoord);
        }
    }

    //public static void CreateRiversPaths(
    //    RawArray<int> riversIndexesMap, int textureSizeX, RawBag<RawBag<int2>> riverCoords,
    //    RawArray<NeighborType> neighborsTypes, RawArray<int2> riversCrossPixelCoords,
    //    Allocator allocator, out RawArray<RawPtr<RiverPath>> riversPaths)
    //{
    //    CreateRiversCrossPoints(riversIndexesMap, textureSizeX, riverCoords.Count, neighborsTypes, riversCrossPixelCoords, allocator, out var riversCrossPoints);

    //    riversPaths = new RawArray<RawPtr<RiverPath>>(allocator, riverCoords.Count);

    //    for (int i = 0; i < riversPaths.Length; i++)
    //    {
    //        riversPaths[i] = new RawPtr<RiverPath>(allocator);
    //    }

    //    var job = new NeighborsRiversPathsJob
    //    {
    //        TextureSizeX = textureSizeX,
    //        RiversCoords = riverCoords,
    //        RiversCrossPoints = riversCrossPoints,
    //        RiversPaths = riversPaths,
    //        Allocator = allocator
    //    };

    //    job.Schedule(riversPaths.Length, 16).Complete();

    //    //for (int i = 0; i < riversPaths.Length; i++)
    //    //{
    //    //    job.Execute(i);
    //    //}

    //    // --- dispose ---

    //    for (int i = 0; i < riversCrossPoints.Length; i++)
    //    {
    //        riversCrossPoints[i].Dispose();
    //    }

    //    riversCrossPoints.Dispose();
    //}

    public static RawArray<RawBag<RiverPathPoint>> CreateRiversPaths(
        RawArray<int> riversIndexesMap, int2 textureSize, RawBag<RawBag<int2>> riverCoords,
        RawArray<NeighborType> neighborsTypes, RawArray<int2> riversCrossPixelCoords,
        Allocator allocator)
    {
        CreateRiversCrossPoints(riversIndexesMap, textureSize.x, riverCoords.Count, neighborsTypes, riversCrossPixelCoords, allocator, out var riversCrossPoints);

        var riversPaths = new RawArray<RawBag<RiverPathPoint>>(allocator, riversCrossPoints.Length);

        for (int i = 0; i < riversPaths.Length; i++)
        {
            riversPaths[i] = CreateRiverPath(ref riverCoords[i], ref riversCrossPoints[i], textureSize, allocator);
        }

        for (int i = 0; i < riversCrossPoints.Length; i++)
        {
            riversCrossPoints[i].Dispose();
        }

        riversCrossPoints.Dispose();

        return riversPaths;
    }

    static void CreateRiversCrossPoints(
        RawArray<int> riversIndexesMap, int textureSizeX, int riversCoordsCount,
        RawArray<NeighborType> neighborsTypes, RawArray<int2> neighborsRiverCrossPixelCoord,
        Allocator allocator, out RawArray<RawBag<int2>> riversCrossPoints)
    {
        riversCrossPoints = new RawArray<RawBag<int2>>(allocator, riversCoordsCount);

        for (int i = 0; i < riversCrossPoints.Length; i++)
        {
            riversCrossPoints[i] = new RawBag<int2>(allocator);
        }

        for (int i = 0; i < neighborsTypes.Length; i++)
        {
            if (neighborsTypes[i] != NeighborType.IsByRiver)
                continue;

            int crossFlat = TexUtilities.PixelCoordToFlat(neighborsRiverCrossPixelCoord[i], textureSizeX);
            int riverIndex = riversIndexesMap[crossFlat];

            riversCrossPoints[riverIndex].Add(neighborsRiverCrossPixelCoord[i]);
        }
    }

    static RawBag<RiverPathPoint> CreateRiverPath(ref RawBag<int2> coords, ref RawBag<int2> crossPoints, int2 textureSize, Allocator allocator)
    {
        var pathPoints = new RawBag<RiverPathPoint>(allocator);
        double distanceSum = 0.0;

        TryAddPathPoint(ref pathPoints, coords[0], ref distanceSum);

        for (int i = 1; i < coords.Count - 1; i++)
        {
            var uvA = GeoUtilitiesDouble.PixelCoordToPlaneUv(coords[i - 1], textureSize);
            var uvB = GeoUtilitiesDouble.PixelCoordToPlaneUv(coords[i], textureSize);

            var unitSphereA = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvA);
            var unitSphereB = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvB);

            distanceSum += GeoUtilitiesDouble.Distance(unitSphereA, unitSphereB);

            if (Contains(coords[i], in crossPoints))
            {
                TryAddPathPoint(ref pathPoints, coords[i], ref distanceSum);
            }
        }

        TryAddPathPoint(ref pathPoints, coords[^1], ref distanceSum);

        return pathPoints;
    }

    static bool Contains(int2 pixelCoord, in RawBag<int2> rawBag)
    {
        for (int i = 0; i < rawBag.Count; i++)
        {
            if (math.all(rawBag[i] == pixelCoord))
                return true;
        }

        return false;
    }

    static void TryAddPathPoint(ref RawBag<RiverPathPoint> pathPoints, int2 pixelCoord, ref double distanceSum)
    {
        if (pathPoints.Count > 0 && math.all(pixelCoord == pathPoints[^1].PixelCoord))
            return;

        pathPoints.Add(new RiverPathPoint
        {
            PixelCoord = pixelCoord,
            DistanceFromPrevious = distanceSum,
        });

        distanceSum = 0.0;
    }

    public static void DisposeRiversPaths(RawArray<RawPtr<RiverPath>> riversPaths)
    {
        for (int i = 0; i < riversPaths.Length; i++)
        {
            riversPaths[i].Ref.RiverPoints.Dispose();
            riversPaths[i].Dispose();
        }

        riversPaths.Dispose();
    }

    public static void DisposeRiversPaths(RawArray<RawBag<RiverPathPoint>> riversPaths)
    {
        for (int i = 0; i < riversPaths.Length; i++)
        {
            riversPaths[i].Dispose();
        }

        riversPaths.Dispose();
    }

    // -----

    public struct RiverPath
    {
        public RawBag<RiverPathPoint> RiverPoints;
    }

    public struct RiverPathPoint
    {
        public int2 PixelCoord;
        public double DistanceFromPrevious;
    }
}
