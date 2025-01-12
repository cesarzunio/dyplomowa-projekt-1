using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static UnwrapperUtilities;

[BurstCompile]
public unsafe struct UnwrapperJob3 : IJob
{
    const int CAPACITY_PIXELS = 2048;
    const int CAPACITY_SOURCES = 128;
    const int CAPACITY_COORDS = 8192;
    const int NEIGHBORS = 8;
    const Allocator ALLOCATOR = Allocator.Persistent;

    public int2 TextureSize;
    public RawArray<int> RiversMap;
    public RawArray<double> Multipliers;
    public RawBag<int2> MouthsPrimary;
    public RawBag<int2> MouthsSecondary;

    public RawArray<RiverPointType> RiversPointTypes;
    public RawArray<int> RiversIndexesMap;
    public RawPtr<RawBag<RawBag<int2>>> RiversCoords;

    public Allocator Allocator;

    [BurstCompile]
    public void Execute()
    {
        var riversColors = RiversColorsInts.Create();
        var queue = new RawGeoQueue<int2>(CAPACITY_PIXELS, CAPACITY_PIXELS, ALLOCATOR);
        var closed = new UnsafeHashSet<int2>(CAPACITY_PIXELS, ALLOCATOR);
        var childToParent = new UnsafeHashMap<int2, int2>(CAPACITY_PIXELS, ALLOCATOR);
        var orders = new RawArray<int>(ALLOCATOR, 0, RiversMap.Length);
        var sourcesPtr = stackalloc int2[CAPACITY_SOURCES];
        var sources = new RawListStackalloc<int2>(sourcesPtr, CAPACITY_SOURCES);

        for (int i = 0; i < MouthsPrimary.Count; i++)
        {
            FindSources(MouthsPrimary[i], RiversMap, Multipliers, TextureSize, in riversColors, ref queue, ref closed, ref childToParent, ref sources, false);

            for (int j = 0; j < sources.Count; j++)
            {
                DrawPrimaryTypes(sources[j], childToParent, RiversPointTypes, TextureSize);
            }
        }

        for (int i = 0; i < MouthsSecondary.Count; i++)
        {
            FindSources(MouthsSecondary[i], RiversMap, Multipliers, TextureSize, in riversColors, ref queue, ref closed, ref childToParent, ref sources, true);

            if (sources.Count == 0)
                throw new Exception("UnwrapperJob :: Cannot find secondary river source!");

            DrawSecondaryTypes(sources[0], childToParent, RiversPointTypes, TextureSize);
        }

        // -------

        for (int i = 0; i < MouthsPrimary.Count; i++)
        {
            FindSources(MouthsPrimary[i], RiversMap, Multipliers, TextureSize, in riversColors, ref queue, ref closed, ref childToParent, ref sources, false);

            for (int j = 0; j < sources.Count; j++)
            {
                DrawPrimaryIndexes(sources[j], childToParent, ref RiversCoords.Ref, RiversPointTypes, RiversIndexesMap, TextureSize, Allocator);
            }
        }

        for (int i = 0; i < MouthsSecondary.Count; i++)
        {
            FindSources(MouthsSecondary[i], RiversMap, Multipliers, TextureSize, in riversColors, ref queue, ref closed, ref childToParent, ref sources, true);

            if (sources.Count == 0)
                throw new Exception("UnwrapperJob :: Cannot find secondary river source!");

            DrawSecondaryIndexes(sources[0], childToParent, ref RiversCoords.Ref, RiversPointTypes, RiversIndexesMap, TextureSize, Allocator);
        }

        queue.Dispose();
        closed.Dispose();
        childToParent.Dispose();
        orders.Dispose();
    }

    // --------------------------------------------------------------------------------------

    static void FindSources(
        int2 mouth, RawArray<int> riverMap, RawArray<double> multipliers, int2 textureSize, in RiversColorsInts riversColors,
        ref RawGeoQueue<int2> queue, ref UnsafeHashSet<int2> closed, ref UnsafeHashMap<int2, int2> childToParent, ref RawListStackalloc<int2> sources, bool returnAfterFirst)
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

                if (returnAfterFirst)
                    return;
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

    static void DrawPrimaryTypes(int2 source, UnsafeHashMap<int2, int2> childToParent, RawArray<RiverPointType> riversPointTypes, int2 textureSize)
    {
        var previous = source;
        var current = childToParent[source];

        int previousFlat;
        int currentFlat;

        while (true)
        {
            previousFlat = TexUtilities.PixelCoordToFlat(previous, textureSize.x);
            currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);

            if (riversPointTypes[currentFlat] != RiverPointType.None)
            {
                riversPointTypes[previousFlat] = RiverPointType.ConnectionIn;
                riversPointTypes[currentFlat] = RiverPointType.ConnectionInPoint;
                return;
            }

            riversPointTypes[previousFlat] = RiverPointType.River;

            if (!childToParent.TryGetValue(current, out var next))
                break;

            previous = current;
            current = next;
        }

        riversPointTypes[currentFlat] = RiverPointType.MouthPrimary;
    }

    static void DrawSecondaryTypes(int2 source, UnsafeHashMap<int2, int2> childToParent, RawArray<RiverPointType> riversPointTypes, int2 textureSize)
    {
        var previous = source;
        var current = childToParent[source];

        int previousFlat;
        int currentFlat;

        bool isDrawing = false;

        while (true)
        {
            previousFlat = TexUtilities.PixelCoordToFlat(previous, textureSize.x);
            currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);

            if (riversPointTypes[previousFlat] != RiverPointType.None && riversPointTypes[currentFlat] == RiverPointType.None)
            {
                riversPointTypes[previousFlat] = RiverPointType.ConnectionOutPoint;
                riversPointTypes[currentFlat] = RiverPointType.ConnectionOut;
                isDrawing = true;

                if (!childToParent.TryGetValue(current, out current))
                    break;
            }
            else if (isDrawing)
            {
                riversPointTypes[previousFlat] = RiverPointType.River;
            }

            if (!childToParent.TryGetValue(current, out var next))
                break;

            previous = current;
            current = next;
        }

        riversPointTypes[currentFlat] = RiverPointType.MouthPrimary;
    }

    // --------------------------------------------------------------------------------------

    static void DrawPrimaryIndexes(
        int2 source, UnsafeHashMap<int2, int2> childToParent, ref RawBag<RawBag<int2>> riversCoords,
        RawArray<RiverPointType> riversPointTypes, RawArray<int> riversIndexesMap, int2 textureSize, Allocator allocator)
    {
        var previous = source;
        var current = childToParent[source];

        int previousFlat = TexUtilities.PixelCoordToFlat(previous, textureSize.x);
        int currentFlat;

        var coordsPtr = stackalloc int2[CAPACITY_COORDS];
        var coords = new RawListStackalloc<int2>(coordsPtr, CAPACITY_COORDS);

        coords.Add(previous);
        riversIndexesMap[previousFlat] = riversCoords.Count;

        while (true)
        {
            coords.Add(current);

            previousFlat = TexUtilities.PixelCoordToFlat(previous, textureSize.x);
            currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);

            // this river flows in into another main river, end
            if (riversPointTypes[previousFlat] == RiverPointType.ConnectionIn && riversPointTypes[currentFlat] == RiverPointType.ConnectionInPoint)
            {
                AddCoords(ref riversCoords, ref coords, allocator);
                return;
            }

            // this river reached point where another main rivers flows in, split
            bool splitCheckMain = riversPointTypes[previousFlat] != RiverPointType.ConnectionIn && riversPointTypes[currentFlat] == RiverPointType.ConnectionInPoint;

            if (splitCheckMain)
            {
                AddCoords(ref riversCoords, ref coords, allocator);
                coords.Add(current);
            }

            riversIndexesMap[currentFlat] = riversCoords.Count;

            // this river reached point where secondary river flows out, split
            bool splitCheckSecondary = riversPointTypes[currentFlat] == RiverPointType.ConnectionOutPoint;

            if (splitCheckSecondary)
            {
                AddCoords(ref riversCoords, ref coords, allocator);
                coords.Add(current);
            }

            if (!childToParent.TryGetValue(current, out var next))
                break;

            previous = current;
            current = next;
        }

        AddCoords(ref riversCoords, ref coords, allocator);
    }

    static void DrawSecondaryIndexes(
        int2 source, UnsafeHashMap<int2, int2> childToParent, ref RawBag<RawBag<int2>> riversCoords,
        RawArray<RiverPointType> riversPointTypes, RawArray<int> riversIndexesMap, int2 textureSize, Allocator allocator)
    {
        var previous = source;
        var current = childToParent[source];

        int previousFlat = TexUtilities.PixelCoordToFlat(previous, textureSize.x);
        int currentFlat;

        var coordsPtr = stackalloc int2[CAPACITY_COORDS];
        var coords = new RawListStackalloc<int2>(coordsPtr, CAPACITY_COORDS);

        bool isDrawing = false;

        while (true)
        {
            previousFlat = TexUtilities.PixelCoordToFlat(previous, textureSize.x);
            currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);

            if (!isDrawing)
            {
                if (riversIndexesMap[currentFlat] == -1)
                {
                    coords.Add(previous);
                    isDrawing = true;
                }
            }

            if (isDrawing)
            {
                coords.Add(current);
                riversIndexesMap[currentFlat] = riversCoords.Count;

                // this river reached point where another secondary river flows out, split
                if (riversPointTypes[currentFlat] == RiverPointType.ConnectionOutPoint)
                {
                    AddCoords(ref riversCoords, ref coords, allocator);
                    coords.Add(current);
                }
            }

            if (!childToParent.TryGetValue(current, out var next))
                break;

            previous = current;
            current = next;
        }

        AddCoords(ref riversCoords, ref coords, allocator);
    }

    // --------------------------------------------------------------------------------------

    static void AddCoords(ref RawBag<RawBag<int2>> riversCoords, ref RawListStackalloc<int2> coords, Allocator allocator)
    {
        if (coords.Count < 2)
        {
            coords.Clear();
            return;
        }

        var rawBag = new RawBag<int2>(allocator, coords.Count);

        for (int i = 0; i < coords.Count; i++)
        {
            rawBag.Add(coords[i]);
        }

        riversCoords.Add(rawBag);
        coords.Clear();
    }
}
