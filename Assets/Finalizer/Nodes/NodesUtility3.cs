using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static BordersContainers;
using static FinalizerUtilities;
using static UnwrapperUtilities;

public static class NodesUtility3
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static void CreateNodesSave(
        string savePathNodes, string savePathEdges, string savePathFieldsNodesIndexes, string savePathRiversNodesIndexes,
        RawArray<int> fieldsMap, RawArray<int> riversIndexesMap, RawArray<RiverPointType> riversPointTypes, RawArray<BorderSorted> bordersSorted, RawArray<double2> indexToCenterGeoCoord, int2 textureSize,
        Dictionary<int, int> colorToIndex, RawArray<NeighborType> neighborsTypes, RawArray<double2> neighborsDistances, RawArray<int2> riversCrossPixelCoords,
        RawArray<RawBag<RiverPathPoint>> riversPaths)
    {
        var edgesTemp = CreateEdgeTemps(
            fieldsMap, riversIndexesMap, riversPointTypes, bordersSorted, indexToCenterGeoCoord, textureSize,
            colorToIndex, neighborsTypes, neighborsDistances, riversCrossPixelCoords, riversPaths);

        var riversCrossings = CreateRiversCrossings(riversCrossPixelCoords, textureSize);
        var riversSingleEdges = CreateRiversSingleEdges(riversPaths, textureSize);

        ProcessFakeNodes(edgesTemp, riversPointTypes, riversCrossings, riversSingleEdges, textureSize, out var fakeEdges);
        CreateFinalData(edgesTemp, indexToCenterGeoCoord.Length + 16384, fakeEdges, out var nodesFinal, out var edgesFinal, out var idToNodeIndex);

        AssignEdgesToNodes(nodesFinal, edgesFinal);

        AssignNodesToOwners(indexToCenterGeoCoord, riversPaths, textureSize, nodesFinal, idToNodeIndex, out var fieldsNodesIndexes, out var riversNodesIndexes);

        SaveNodesAndEdges(savePathNodes, savePathEdges, nodesFinal, edgesFinal, textureSize, idToNodeIndex);
        SaveNodesOwners(savePathFieldsNodesIndexes, savePathRiversNodesIndexes, fieldsNodesIndexes, riversNodesIndexes);

        riversCrossings.Dispose();
        riversSingleEdges.Dispose();
        fieldsNodesIndexes.Dispose();
        riversNodesIndexes.DisposeDepth1();
    }

    static List<EdgeTemp> CreateEdgeTemps(
        RawArray<int> fieldsMap, RawArray<int> riversIndexesMap, RawArray<RiverPointType> riversPointTypes, RawArray<BorderSorted> bordersSorted, RawArray<double2> indexToCenterGeoCoord, int2 textureSize,
        Dictionary<int, int> colorToIndex, RawArray<NeighborType> neighborsTypes, RawArray<double2> neighborsDistances, RawArray<int2> riversCrossPixelCoords,
        RawArray<RawBag<RiverPathPoint>> riversPaths)
    {
        var edges = new List<EdgeTemp>(bordersSorted.Length + 16384);

        for (int i = 0; i < bordersSorted.Length; i++)
        {
            var neighborType = neighborsTypes[i];
            var neighborDistances = neighborsDistances[i];
            var riverCrossPixelCoord = riversCrossPixelCoords[i];

            if (neighborType == NeighborType.IsNot)
                continue;

            int fieldIndexA = colorToIndex[bordersSorted[i].FieldColorA];
            int fieldIndexB = colorToIndex[bordersSorted[i].FieldColorB];

            var geoCoordA = indexToCenterGeoCoord[fieldIndexA];
            var geoCoordB = indexToCenterGeoCoord[fieldIndexB];
            var distanceAB = GeoUtilitiesDouble.Distance(geoCoordA, geoCoordB);

            var nodeA = new NodeTemp(geoCoordA, NodeOwnerType.Field, fieldIndexA);
            var nodeB = new NodeTemp(geoCoordB, NodeOwnerType.Field, fieldIndexB);

            if (neighborType == NeighborType.IsByRiver)
            {
                var riverUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(riverCrossPixelCoord, textureSize);
                var riverGeoCoord = GeoUtilitiesDouble.PlaneUvToGeoCoords(riverUv);
                var distanceRiverA = GeoUtilitiesDouble.Distance(geoCoordA, riverGeoCoord);
                var distanceRiverB = GeoUtilitiesDouble.Distance(geoCoordB, riverGeoCoord);
                int riverCrossFlat = TexUtilities.PixelCoordToFlat(riverCrossPixelCoord, textureSize.x);
                int riverCrossIndex = riversIndexesMap[riverCrossFlat];
                int secondaryIndex = FindIndexOfRiverPathPoint(ref riversPaths[riverCrossIndex], riverCrossPixelCoord);
                var nodeRiver = new NodeTemp(riverGeoCoord, NodeOwnerType.River, riverCrossIndex, secondaryIndex);

                edges.Add(new EdgeTemp(nodeA, nodeB, neighborDistances.x + neighborDistances.y, distanceAB, riverCrossPixelCoord));
                edges.Add(new EdgeTemp(nodeA, nodeRiver, neighborDistances.x, distanceRiverA));
                edges.Add(new EdgeTemp(nodeRiver, nodeB, neighborDistances.y, distanceRiverB));

                continue;
            }

            edges.Add(new EdgeTemp(nodeA, nodeB, neighborDistances.x, distanceAB));
        }

        for (int i = 0; i < riversPaths.Length; i++)
        {
            var riverStartNode = CreateRiverNodeTemp(riversPaths[i][0].PixelCoord, textureSize, i, 0);
            var riverEndNode = CreateRiverNodeTemp(riversPaths[i][^1].PixelCoord, textureSize, i, riversPaths[i].Count - 1);

            AddEndingRiverEdge(riversPaths[i][0], riverStartNode, edges, fieldsMap, colorToIndex, riversPointTypes, indexToCenterGeoCoord, textureSize);
            AddEndingRiverEdge(riversPaths[i][^1], riverEndNode, edges, fieldsMap, colorToIndex, riversPointTypes, indexToCenterGeoCoord, textureSize);

            for (int j = 0; j < riversPaths[i].Count - 1; j++)
            {
                var riverNodeA = CreateRiverNodeTemp(riversPaths[i][j].PixelCoord, textureSize, i, j);
                var riverNodeB = CreateRiverNodeTemp(riversPaths[i][j + 1].PixelCoord, textureSize, i, j + 1);

                var distanceAir = GeoUtilitiesDouble.Distance(riverNodeA.GeoCoord, riverNodeB.GeoCoord);

                edges.Add(new EdgeTemp(riverNodeA, riverNodeB, riversPaths[i][j + 1].DistanceFromPrevious, distanceAir));
            }
        }

        return edges;
    }

    static int FindIndexOfRiverPathPoint(ref RawBag<RiverPathPoint> riverPath, int2 pixelCoord)
    {
        for (int i = 0; i < riverPath.Count; i++)
        {
            if (math.all(riverPath[i].PixelCoord == pixelCoord))
                return i;
        }

        throw new Exception($"NodesUtility :: FindIndexOfRiverPathPoint :: Cannot find index: {pixelCoord}");
    }

    static NodeTemp CreateRiverNodeTemp(int2 pixelCoord, int2 textureSize, int i, int j)
    {
        var riverUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoord, textureSize);
        var riverGeoCoord = GeoUtilitiesDouble.PlaneUvToGeoCoords(riverUv);
        return new NodeTemp(riverGeoCoord, NodeOwnerType.River, i, j);
    }

    static void AddEndingRiverEdge(
        RiverPathPoint riverPathPoint, NodeTemp riverNode, List<EdgeTemp> edges, RawArray<int> fieldsMap, Dictionary<int, int> colorToIndex,
        RawArray<RiverPointType> riversPointTypes, RawArray<double2> indexToCenterGeoCoords, int2 textureSize)
    {
        int flat = TexUtilities.PixelCoordToFlat(riverPathPoint.PixelCoord, textureSize.x);
        int fieldIndex = colorToIndex[fieldsMap[flat]];
        var pointType = riversPointTypes[flat];

        if (pointType == RiverPointType.Source || pointType == RiverPointType.MouthPrimary || pointType == RiverPointType.MouthSecondary)
        {
            var fieldGeoCoord = indexToCenterGeoCoords[fieldIndex];
            var fieldNode = new NodeTemp(fieldGeoCoord, NodeOwnerType.Field, fieldIndex);
            var distance = GeoUtilitiesDouble.Distance(riverNode.GeoCoord, fieldGeoCoord);

            edges.Add(new EdgeTemp(fieldNode, riverNode, distance, distance));
        }
    }

    static RawArray<bool> CreateRiversCrossings(RawArray<int2> riversCrossPixelCoords, int2 textureSize)
    {
        var riversCrossings = new RawArray<bool>(ALLOCATOR, false, textureSize.x * textureSize.y);

        for (int i = 0; i < riversCrossPixelCoords.Length; i++)
        {
            int flat = TexUtilities.PixelCoordToFlat(riversCrossPixelCoords[i], textureSize.x);
            riversCrossings[flat] = true;
        }

        return riversCrossings;
    }

    static RawArray<bool> CreateRiversSingleEdges(RawArray<RawBag<RiverPathPoint>> riversPaths, int2 textureSize)
    {
        var riversSingleEdges = new RawArray<bool>(ALLOCATOR, false, textureSize.x * textureSize.y);

        for (int i = 0; i < riversPaths.Length; i++)
        {
            if (riversPaths[i].Count != 2)
                continue;

            int flatA = TexUtilities.PixelCoordToFlat(riversPaths[i][0].PixelCoord, textureSize.x);
            int flatB = TexUtilities.PixelCoordToFlat(riversPaths[i][1].PixelCoord, textureSize.x);

            riversSingleEdges[flatA] = true;
            riversSingleEdges[flatB] = true;
        }

        return riversSingleEdges;
    }

    static void ProcessFakeNodes(
        List<EdgeTemp> edges, RawArray<RiverPointType> riversPointTypes, RawArray<bool> riversCrossings,
        RawArray<bool> riversSingleEdges, int2 textureSize, out HashSet<EdgeTemp> fakeEdges)
    {
        var geoCoordToHalfEdges = new Dictionary<double2, List<HalfEdgeTemp>>(50_000);
        fakeEdges = new HashSet<EdgeTemp>(100_000);

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];

            if (TryGetFakeNodeOther(edge, riversPointTypes, riversCrossings, riversSingleEdges, textureSize, out var fakeNodeOther, out var fakeGeoCoord))
            {
                var halfEdge = new HalfEdgeTemp(fakeNodeOther, edge.DistanceGround);

                AddHalfEdge(fakeGeoCoord, halfEdge, geoCoordToHalfEdges);
                fakeEdges.Add(edge);
            }
        }

        foreach (var halfEdges in geoCoordToHalfEdges.Values)
        {
            for (int i = 0; i < halfEdges.Count; i++)
            {
                for (int j = i + 1; j < halfEdges.Count; j++)
                {
                    var halfEdgeA = halfEdges[i];
                    var halfEdgeB = halfEdges[j];

                    var distanceAir = GeoUtilitiesDouble.Distance(halfEdgeA.Node.GeoCoord, halfEdgeB.Node.GeoCoord);

                    edges.Add(new EdgeTemp(halfEdgeA.Node, halfEdgeB.Node, halfEdgeA.DistanceGround + halfEdgeB.DistanceGround, distanceAir));
                }
            }
        }
    }

    static bool TryGetFakeNodeOther(
        EdgeTemp edge, RawArray<RiverPointType> riversPointTypes, RawArray<bool> riversCrossings,
        RawArray<bool> riversSingleEdges, int2 textureSize, out NodeTemp fakeNodeOther, out double2 fakeGeoCoord)
    {
        fakeNodeOther = default;
        fakeGeoCoord = default;

        var nodeA = edge.NodeA;
        var nodeB = edge.NodeB;

        if (nodeA.Owner.OwnerType != NodeOwnerType.River || nodeB.Owner.OwnerType != NodeOwnerType.River)
            return false;

        var uvA = GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
        var uvB = GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

        var pixelCoordA = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvA, textureSize);
        var pixelCoordB = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvB, textureSize);

        var flatA = TexUtilities.PixelCoordToFlat(pixelCoordA, textureSize.x);
        var flatB = TexUtilities.PixelCoordToFlat(pixelCoordB, textureSize.x);

        var isFakeA = !riversCrossings[flatA] && !riversSingleEdges[flatA] && IsFake(riversPointTypes[flatA]);
        var isFakeB = !riversCrossings[flatB] && !riversSingleEdges[flatB] && IsFake(riversPointTypes[flatB]);

        if (!isFakeA && !isFakeB)
            return false;

        if (isFakeA && isFakeB)
        {
            Debug.LogError("NodeUtility :: TryGetFakeNodeOther :: Both nodes are fake?");
            Debug.LogError($"{TexUtilities.FlipY(pixelCoordA, textureSize.y)} / {TexUtilities.FlipY(pixelCoordB, textureSize.y)}");
            return false;
        }

        (fakeNodeOther, fakeGeoCoord) = isFakeA ? (nodeB, nodeA.GeoCoord) : (nodeA, nodeB.GeoCoord);
        return true;
    }

    static bool IsFake(RiverPointType pointType) => pointType switch
    {
        RiverPointType.ConnectionInPoint => true,
        RiverPointType.ConnectionOutPoint => true,
        _ => false
    };

    static void AddHalfEdge(double2 fakeGeoCoord, HalfEdgeTemp halfEdge, Dictionary<double2, List<HalfEdgeTemp>> geoCoordToHalfEdges)
    {
        if (geoCoordToHalfEdges.TryGetValue(fakeGeoCoord, out var fakeNodesOther))
        {
            fakeNodesOther.Add(halfEdge);
            return;
        }

        geoCoordToHalfEdges[fakeGeoCoord] = new List<HalfEdgeTemp>(8) { halfEdge };
    }

    static void CreateFinalData(
        List<EdgeTemp> edges, int nodesCountPredict, HashSet<EdgeTemp> fakeEdges,
        out List<NodeFinal> nodesFinal, out List<EdgeFinal> edgesFinal, out Dictionary<NodeId, uint> idToNodeIndex)
    {
        nodesFinal = new List<NodeFinal>(nodesCountPredict);
        edgesFinal = new List<EdgeFinal>(edges.Count);
        idToNodeIndex = new Dictionary<NodeId, uint>(nodesCountPredict);

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];

            if (fakeEdges != null && fakeEdges.Contains(edge))
                continue;

            var nodeIdA = edge.NodeA.ToId();
            var nodeIdB = edge.NodeB.ToId();

            uint nodeIndexA = GetNodeIndex((uint)i, edge.NodeA, nodeIdA, idToNodeIndex, nodesFinal);
            uint nodeIndexB = GetNodeIndex((uint)i, edge.NodeB, nodeIdB, idToNodeIndex, nodesFinal);

            edgesFinal.Add(new EdgeFinal(nodeIndexA, nodeIndexB, edge.DistanceGround, edge.DistanceAir, edge.RiverCrossingPixelCoord));
        }
    }

    static uint GetNodeIndex(uint edgeIndex, NodeTemp nodeTemp, NodeId nodeTempId, Dictionary<NodeId, uint> idToNodeIndex, List<NodeFinal> nodesFinal)
    {
        if (idToNodeIndex.TryGetValue(nodeTempId, out uint index))
        {
            //var nodeFinal = nodesFinal[(int)index];
            //nodeFinal.Edges.Add(edgeIndex);
            return index;
        }

        uint indexNew = (uint)nodesFinal.Count;
        var nodeFinalNew = nodeTemp.ToFinal();
        //nodeFinalNew.Edges.Add(edgeIndex);

        idToNodeIndex[nodeTempId] = indexNew;
        nodesFinal.Add(nodeFinalNew);

        return indexNew;
    }

    static void AssignEdgesToNodes(List<NodeFinal> nodesFinal, List<EdgeFinal> edgesFinal)
    {
        for (int i = 0; i < nodesFinal.Count; i++)
        {
            var node = nodesFinal[i];
            node.Edges = new List<uint>(8);
            nodesFinal[i] = node;
        }

        for (int i = 0; i < edgesFinal.Count; i++)
        {
            int nodeA = (int)edgesFinal[i].NodeA;
            int nodeB = (int)edgesFinal[i].NodeB;

            nodesFinal[nodeA].Edges.Add((uint)i);
            nodesFinal[nodeB].Edges.Add((uint)i);
        }
    }

    static void AssignNodesToOwners(
        RawArray<double2> indexToCenterGeoCoord, RawArray<RawBag<RiverPathPoint>> riversPaths, int2 textureSize, List<NodeFinal> nodesFinal, Dictionary<NodeId, uint> idToNodeIndex,
        out RawArray<uint> fieldsNodesIndexes, out RawArray<RawBag<uint>> riversNodesIndexes)
    {
        fieldsNodesIndexes = new RawArray<uint>(ALLOCATOR, indexToCenterGeoCoord.Length);

        for (int i = 0; i < fieldsNodesIndexes.Length; i++)
        {
            var id = new NodeId(indexToCenterGeoCoord[i], NodeOwnerType.Field);
            uint nodeIndex = idToNodeIndex[id];

            fieldsNodesIndexes[i] = nodeIndex;
        }

        riversNodesIndexes = new RawArray<RawBag<uint>>(ALLOCATOR, riversPaths.Length);

        for (int i = 0; i < riversPaths.Length; i++)
        {
            riversNodesIndexes[i] = new RawBag<uint>(ALLOCATOR, riversPaths[i].Count);
            int secondaryIndexIt = 0;

            for (int j = 0; j < riversPaths[i].Count; j++)
            {
                var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(riversPaths[i][j].PixelCoord, textureSize);
                var geoCoord = GeoUtilitiesDouble.PlaneUvToGeoCoords(uv);
                var id = new NodeId(geoCoord, NodeOwnerType.River);

                if (idToNodeIndex.TryGetValue(id, out uint nodeIndex))
                {
                    riversNodesIndexes[i].Add(nodeIndex);

                    // set new owner, cos SecondaryIndex is no longer matching
                    var nodeFinal = nodesFinal[(int)nodeIndex];
                    nodeFinal.Owner = new NodeOwner(NodeOwnerType.River, i, secondaryIndexIt++);

                    nodesFinal[(int)nodeIndex] = nodeFinal;
                }
            }
        }
    }

    static void SaveNodesAndEdges(string savePathNodes, string savePathEdges, List<NodeFinal> nodesFinal, List<EdgeFinal> edgesFinal, int2 textureSize, Dictionary<NodeId, uint> idToNodeIndex)
    {
        FileStream fileStream;

        using (fileStream = new FileStream(savePathNodes, FileMode.Create, FileAccess.Write))
        {
            fileStream.WriteValue(nodesFinal.Count);

            for (int i = 0; i < nodesFinal.Count; i++)
            {
                fileStream.WriteValue(nodesFinal[i].GeoCoord);
            }

            for (int i = 0; i < nodesFinal.Count; i++)
            {
                var nodeFinal = nodesFinal[i];

                fileStream.WriteValue(new NodesSaveUtility2.NodeOwner
                {
                    Type = (NodesSaveUtility2.NodeOwnerType)(uint)nodeFinal.Owner.OwnerType,
                    Index = (uint)nodeFinal.Owner.Index,
                    IndexSecondary = nodeFinal.Owner.IndexSecondary,
                });
            }

            for (int i = 0; i < nodesFinal.Count; i++)
            {
                fileStream.WriteValue(nodesFinal[i].Edges.Count);

                for (int j = 0; j < nodesFinal[i].Edges.Count; j++)
                {
                    BinarySaveUtility.WriteValue(fileStream, nodesFinal[i].Edges[j]);
                }
            }
        }

        using (fileStream = new FileStream(savePathEdges, FileMode.Create, FileAccess.Write))
        {
            fileStream.WriteValue(edgesFinal.Count);

            for (int i = 0; i < edgesFinal.Count; i++)
            {
                int riverCrossingNodeIndex = RiverCrossingPixelCoordToNodeIndex(edgesFinal[i].RiverCrossingPixelCoord, textureSize, idToNodeIndex);
                var edge = new NodesSaveUtility2.EdgeSerialized(edgesFinal[i].NodeA, edgesFinal[i].NodeB, edgesFinal[i].DistanceGround, edgesFinal[i].DistanceAir, riverCrossingNodeIndex);

                fileStream.WriteValue(edge);
            }
        }
    }

    static int RiverCrossingPixelCoordToNodeIndex(int2 riverCrossingPixelCoord, int2 textureSize, Dictionary<NodeId, uint> idToNodeIndex)
    {
        if (riverCrossingPixelCoord.x == -1)
            return -1;

        var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(riverCrossingPixelCoord, textureSize);
        var geoCoord = GeoUtilitiesDouble.PlaneUvToGeoCoords(uv);

        var nodeId = new NodeId(geoCoord, NodeOwnerType.River);

        return (int)idToNodeIndex[nodeId];
    }

    static void SaveNodesOwners(string savePathFieldsNodesIndexes, string savePathRiversNodesIndexes, RawArray<uint> fieldsNodesIndexes, RawArray<RawBag<uint>> riversNodesIndexes)
    {
        FileStream fileStream;

        using (fileStream = new FileStream(savePathFieldsNodesIndexes, FileMode.Create, FileAccess.Write))
        {
            BinarySaveUtility.WriteRawContainerSimple<RawArray<uint>, uint>(fileStream, fieldsNodesIndexes);
        }

        using (fileStream = new FileStream(savePathRiversNodesIndexes, FileMode.Create, FileAccess.Write))
        {
            fileStream.WriteValue(riversNodesIndexes.Length);

            for (int i = 0; i < riversNodesIndexes.Length; i++)
            {
                BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, riversNodesIndexes[i]);
            }
        }
    }

    public static void LoadNodeOwners(
        string savePathFieldsNodesIndexes, string savePathRiversNodesIndexes, out RawArray<uint> fieldsNodesIndexes, out RawArray<RawArray<uint>> riversNodesIndexes, Allocator allocator)
    {
        fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(savePathFieldsNodesIndexes, allocator);

        using var fileStream = new FileStream(savePathRiversNodesIndexes, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int riversNodesIndexesLength = binaryReader.ReadInt32();

        riversNodesIndexes = new RawArray<RawArray<uint>>(allocator, riversNodesIndexesLength);

        for (int i = 0; i < riversNodesIndexesLength; i++)
        {
            riversNodesIndexes[i] = BinarySaveUtility.ReadRawArray<uint>(fileStream, binaryReader, allocator);
        }
    }

    // -----------------------------------

    readonly struct EdgeTemp
    {
        public readonly NodeTemp NodeA;
        public readonly NodeTemp NodeB;

        public readonly double DistanceGround;
        public readonly double DistanceAir;

        public readonly int2 RiverCrossingPixelCoord;

        public EdgeTemp(NodeTemp nodeA, NodeTemp nodeB, double distanceGround, double distanceAir)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            RiverCrossingPixelCoord = -1;
        }

        public EdgeTemp(NodeTemp nodeA, NodeTemp nodeB, double distanceGround, double distanceAir, int2 riverCrossingPixelCoord)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            RiverCrossingPixelCoord = riverCrossingPixelCoord;
        }
    }

    readonly struct HalfEdgeTemp
    {
        public readonly NodeTemp Node;
        public readonly double DistanceGround;

        public HalfEdgeTemp(NodeTemp node, double distanceGround)
        {
            Node = node;
            DistanceGround = distanceGround;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct EdgeFinal
    {
        public readonly uint NodeA;
        public readonly uint NodeB;

        public readonly double DistanceGround;
        public readonly double DistanceAir;

        public readonly int2 RiverCrossingPixelCoord;

        public EdgeFinal(uint nodeA, uint nodeB, double distanceGround, double distanceAir, int2 riverCrossingPixelCoord)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            RiverCrossingPixelCoord = riverCrossingPixelCoord;
        }
    }

    readonly struct NodeTemp
    {
        public readonly double2 GeoCoord;
        public readonly NodeOwner Owner;

        public NodeTemp(double2 geoCoord, NodeOwnerType ownerType, int index, int indexSecondary = -1)
        {
            GeoCoord = geoCoord;
            Owner = new NodeOwner(ownerType, index, indexSecondary);
        }

        public readonly NodeId ToId() => new(GeoCoord, Owner.OwnerType);
        public readonly NodeFinal ToFinal() => new(GeoCoord, Owner);
    }

    struct NodeFinal
    {
        public double2 GeoCoord;
        public NodeOwner Owner;
        public List<uint> Edges;

        public NodeFinal(double2 geoCoord, NodeOwner owner)
        {
            GeoCoord = geoCoord;
            Owner = owner;
            Edges = new List<uint>();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly struct NodeOwner
    {
        public readonly NodeOwnerType OwnerType;
        public readonly int Index;
        public readonly int IndexSecondary;

        public NodeOwner(NodeOwnerType type, int index, int indexSecondary)
        {
            OwnerType = type;
            Index = index;
            IndexSecondary = indexSecondary;
        }
    }

    public enum NodeOwnerType : uint
    {
        Field = 0,
        River = 10,
    }

    readonly struct NodeId
    {
        public readonly double2 GeoCoord;
        public readonly NodeOwnerType OwnerType;

        public NodeId(double2 geoCoord, NodeOwnerType ownerType)
        {
            GeoCoord = geoCoord;
            OwnerType = ownerType;
        }
    }
}
