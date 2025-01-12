//using System.Collections;
//using System.Collections.Generic;
//using Unity.Collections;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Jobs;
//using Unity.Mathematics;
//using UnityEngine;
//using static NodesUtility2;
//using static UnwrapperUtilities;

//public static class EdgePathsUtility
//{
//    const Allocator ALLOCATOR = Allocator.Persistent;
//    const int HASH_CAPACITY = 8192;

//    public static RawArray<EdgePathOutput> GenerateEdgePaths(
//        List<NodeFinal> nodesFinal, List<EdgeFinal> edgesFinal,
//        RawArray<int> fieldsMap, RawArray<RiverPointType> riversPointTypes, int2 textureSize, Allocator allocator)
//    {
//        int processorCount = SystemInfo.processorCount + 8;
//        var queues = new RawArray<RawGeoQueueHeuristic<HeuristicGeo>>(ALLOCATOR, processorCount);
//        var closedSets = new RawArray<UnsafeHashSet<int2>>(ALLOCATOR, processorCount);
//        var hashMaps = new RawArray<UnsafeHashMap<int2, int2>>(ALLOCATOR, processorCount);
//        var spinLockUses = new RawArray<uint>(ALLOCATOR, 0, processorCount);
//        var spinLock = new RawPtr<CesSpinLock>(ALLOCATOR, CesSpinLock.Create());

//        for (int i = 0; i < processorCount; i++)
//        {
//            queues[i] = new RawGeoQueueHeuristic<HeuristicGeo>(HASH_CAPACITY, HASH_CAPACITY, default, ALLOCATOR);
//            closedSets[i] = new UnsafeHashSet<int2>(HASH_CAPACITY, ALLOCATOR);
//            hashMaps[i] = new UnsafeHashMap<int2, int2>(HASH_CAPACITY, ALLOCATOR);
//        }

//        var edgePathInputs = GetEdgePathInputs(nodesFinal, edgesFinal, textureSize);
//        var edgePathOutputDefault = new EdgePathOutput(RawBag<int2>.Null());
//        var edgePathOutputs = new RawArray<EdgePathOutput>(allocator, edgePathOutputDefault, edgePathInputs.Length);

//        var job = new EdgePathJob
//        {
//            TextureSize = textureSize,
//            FieldsMap = fieldsMap,
//            RiversPointTypes = riversPointTypes,
//            Allocator = allocator,
//            Queues = queues,
//            ClosedSets = closedSets,
//            HashMaps = hashMaps,
//            SpinLockUses = spinLockUses,
//            SpinLock = spinLock,
//            EdgePathInputs = edgePathInputs,
//            EdgePathOutputs = edgePathOutputs,
//        };

//        job.Schedule(edgePathInputs.Length, 4096).Complete();

//        for (int i = 0; i < processorCount; i++)
//        {
//            queues[i].Dispose();
//            closedSets[i].Dispose();
//            hashMaps[i].Dispose();
//        }

//        queues.Dispose();
//        closedSets.Dispose();
//        hashMaps.Dispose();
//        spinLockUses.Dispose();
//        spinLock.Dispose();
//        edgePathInputs.Dispose();

//        return edgePathOutputs;
//    }

//    static RawArray<EdgePathInput> GetEdgePathInputs(List<NodeFinal> nodesFinal, List<EdgeFinal> edgesFinal, int2 textureSize)
//    {
//        var edgePathInputs = new RawArray<EdgePathInput>(ALLOCATOR, edgesFinal.Count);

//        for (int i = 0; i < edgePathInputs.Length; i++)
//        {
//            var edge = edgesFinal[i];

//            var nodeA = nodesFinal[(int)edge.NodeA];
//            var nodeB = nodesFinal[(int)edge.NodeB];

//            var uvA = GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
//            var uvB = GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

//            var pixelCoordA = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvA, textureSize);
//            var pixelCoordB = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvB, textureSize);

//            edgePathInputs[i] = new EdgePathInput(pixelCoordA, pixelCoordB, GetEdgePathType(nodeA.Owner.OwnerType, nodeB.Owner.OwnerType));
//        }

//        return edgePathInputs;
//    }

//    static EdgePathType GetEdgePathType(NodeOwnerType ownerTypeA, NodeOwnerType ownerTypeB)
//    {
//        if (ownerTypeA == NodeOwnerType.Field && ownerTypeB == NodeOwnerType.Field)
//            return EdgePathType.FieldField;

//        if (ownerTypeA == NodeOwnerType.Field && ownerTypeB == NodeOwnerType.River)
//            return EdgePathType.FieldRiver;

//        if (ownerTypeA == NodeOwnerType.River && ownerTypeB == NodeOwnerType.Field)
//            return EdgePathType.RiverField;

//        return EdgePathType.RiverRiver;
//    }

//    public readonly struct EdgePathInput
//    {
//        public readonly int2 Start;
//        public readonly int2 End;
//        public readonly EdgePathType PathType;

//        public EdgePathInput(int2 start, int2 end, EdgePathType pathType)
//        {
//            Start = start;
//            End = end;
//            PathType = pathType;
//        }
//    }

//    public enum EdgePathType
//    {
//        FieldField,
//        FieldRiver,
//        RiverField,
//        RiverRiver,
//    }

//    public readonly struct EdgePathOutput
//    {
//        public readonly RawBag<int2> PixelCoords;

//        public EdgePathOutput(RawBag<int2> pixelCoords)
//        {
//            PixelCoords = pixelCoords;
//        }
//    }
//}

