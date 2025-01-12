using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Video;
using static BordersContainers;
using static FinalizerUtilities;
using static UnwrapperUtilities;

public static unsafe class NodesUtility4
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    const int RIVER_NODES_LENGTH = 1024;
    const int HALF_EDGES_LENGTH = 1024;

    public static void CreateNodesSave(
        string savePathNodes, string savePathEdges, string savePathRiverPoints, string savePathFieldsNodesIndexes, string savePathRivers,
        RawArray<int> fieldsMap, RawArray<int> riversIndexesMap, RawArray<BorderSorted> bordersSorted,
        RawArray<double2> indexToCenterGeoCoord, int2 textureSize, Dictionary<int, int> colorToIndex,
        RawArray<NeighborType> neighborsTypes, RawArray<double2> neighborsDistances,
        RawArray<int2> riversCrossPixelCoords, RawArray<RiverData> riversDatas)
    {
        CreateEdgesTempsFromFields(
            fieldsMap, riversIndexesMap, bordersSorted, indexToCenterGeoCoord, textureSize, colorToIndex,
            neighborsTypes, neighborsDistances, riversCrossPixelCoords, out var edgesTemps, out var riverNodesExistingMap);

        CreateEdgesTempsFromRivers(fieldsMap, in colorToIndex, indexToCenterGeoCoord, riversIndexesMap, textureSize, riversDatas, ref edgesTemps, riverNodesExistingMap);

        CreateFinalData(in edgesTemps, indexToCenterGeoCoord.Length + 32765, out var edgesFinals, out var nodesFinals, out var nodeIdToNodeIndex);
        CreateRiverPoints(ref nodesFinals, in edgesFinals, in nodeIdToNodeIndex, out var riverPoints, out var nodeIdToRiverPointIndex);
        CreateFieldsNodesIndexes(indexToCenterGeoCoord.Length, in nodesFinals, out var fieldsNodesIndexes);
        CreateRivers(in riversDatas, riversIndexesMap, textureSize, in nodeIdToRiverPointIndex, out var rivers);

        //int notcontains = 0;

        //for (int i = 0; i < edgesFinals.Count; i++)
        //{
        //    ref readonly var edge = ref edgesFinals[i];

        //    ref readonly var nodeA = ref nodesFinals[edge.NodeIndexA];
        //    ref readonly var nodeB = ref nodesFinals[edge.NodeIndexB];

        //    if (!Contains(nodeA.EdgesIndexes, (uint)i))
        //        notcontains++;

        //    if (!Contains(nodeB.EdgesIndexes, (uint)i))
        //        notcontains++;
        //}

        //Debug.Log($"NOT CONTAINS: {notcontains}");

        //int notmatching = 0;

        //for (int i = 0; i < nodesFinals.Count; i++)
        //{
        //    ref readonly var node = ref nodesFinals[i];
        //    uint nodeIndex = (uint)i;

        //    for (int j = 0; j < node.EdgesIndexes.Count; j++)
        //    {
        //        ref readonly var edge = ref edgesFinals[node.EdgesIndexes[j]];

        //        if (edge.NodeIndexA != nodeIndex && edge.NodeIndexB != nodeIndex)
        //            notmatching++;
        //    }
        //}

        //Debug.Log($"NOT MATCHING: {notmatching}");

        int invalids = 0;

        for (int i = 0; i < fieldsNodesIndexes.Length; i++)
        {
            var nodeIndex = fieldsNodesIndexes[i];
            var edgesIndexes = nodesFinals[nodeIndex].EdgesIndexes;

            for (int j = 0; j < edgesIndexes.Count; j++)
            {
                var edge = edgesFinals[edgesIndexes[j]];

                if (edge.NodeIndexA != nodeIndex && edge.NodeIndexB != nodeIndex)
                    invalids++;
            }
        }

        Debug.Log($"Nodes :: Invalids :: {invalids}");

        SaveFinalData(savePathNodes, savePathEdges, in nodesFinals, in edgesFinals);
        SaveRiverPoints(savePathRiverPoints, in riverPoints);
        SaveFieldsNodesIndexes(savePathFieldsNodesIndexes, fieldsNodesIndexes);
        SaveRivers(savePathRivers, in rivers, in riversDatas);

        edgesTemps.Dispose();
        riverNodesExistingMap.Dispose();
        edgesFinals.Dispose();
        nodesFinals.DisposeDepth1();
        riverPoints.DisposeDepth1();
        fieldsNodesIndexes.Dispose();
        rivers.DisposeDepth1();
    }

    static bool Contains(in RawBag<uint> set, uint value)
    {
        for (int i = 0; i < set.Count; i++)
        {
            if (set[i] == value)
                return true;
        }

        return false;
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    static void CreateEdgesTempsFromFields(
       RawArray<int> fieldsMap, RawArray<int> riversIndexesMap, RawArray<BorderSorted> bordersSorted, RawArray<double2> indexToCenterGeoCoord, int2 textureSize,
       Dictionary<int, int> colorToIndex, RawArray<NeighborType> neighborsTypes, RawArray<double2> neighborsDistances, RawArray<int2> riversCrossPixelCoords,
       out RawBag<EdgeTemp> edgesTemps, out RawArray<bool> riverNodesExistingMap)
    {
        edgesTemps = new RawBag<EdgeTemp>(ALLOCATOR, bordersSorted.Length + 16384);
        riverNodesExistingMap = new RawArray<bool>(ALLOCATOR, false, fieldsMap.Length);

        for (int i = 0; i < bordersSorted.Length; i++)
        {
            ref readonly var neighborType = ref neighborsTypes[i];
            ref readonly var neighborDistances = ref neighborsDistances[i];
            ref readonly var riverCrossPixelCoord = ref riversCrossPixelCoords[i];

            if (neighborType == NeighborType.IsNot)
                continue;

            uint fieldIndexA = (uint)colorToIndex[bordersSorted[i].FieldColorA];
            uint fieldIndexB = (uint)colorToIndex[bordersSorted[i].FieldColorB];

            ref readonly var geoCoordA = ref indexToCenterGeoCoord[fieldIndexA];
            ref readonly var geoCoordB = ref indexToCenterGeoCoord[fieldIndexB];
            var distanceAirAB = GeoUtilitiesDouble.Distance(geoCoordA, geoCoordB);

            var nodeA = new NodeId(geoCoordA, NodeOwnerType.Field, fieldIndexA);
            var nodeB = new NodeId(geoCoordB, NodeOwnerType.Field, fieldIndexB);

            if (neighborType == NeighborType.IsByRiver)
            {
                var riverUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(riverCrossPixelCoord, textureSize);
                var riverGeoCoord = GeoUtilitiesDouble.PlaneUvToGeoCoords(riverUv);
                var distanceAirRiverA = GeoUtilitiesDouble.Distance(geoCoordA, riverGeoCoord);
                var distanceAirRiverB = GeoUtilitiesDouble.Distance(geoCoordB, riverGeoCoord);
                int riverCrossFlat = TexUtilities.PixelCoordToFlat(riverCrossPixelCoord, textureSize.x);
                int riverCrossIndex = riversIndexesMap[riverCrossFlat];

                if (riverCrossIndex == -1)
                    throw new Exception($"NodesUtility4 :: CreateEdgeTemps :: riverCrossIndex is empty!");

                var nodeRiver = new NodeId(riverGeoCoord, NodeOwnerType.River, (uint)riverCrossIndex);

                edgesTemps.Add(new EdgeTemp(nodeA, nodeB, neighborDistances.x + neighborDistances.y, distanceAirAB, nodeRiver, false));
                edgesTemps.Add(new EdgeTemp(nodeA, nodeRiver, neighborDistances.x, distanceAirRiverA, false));
                edgesTemps.Add(new EdgeTemp(nodeRiver, nodeB, neighborDistances.y, distanceAirRiverB, false));

                //edgesTemps.Add(new EdgeTemp(nodeA, nodeB, neighborDistances.x + neighborDistances.y, distanceAirAB, false));

                riverNodesExistingMap[riverCrossFlat] = true;

                continue;
            }

            edgesTemps.Add(new EdgeTemp(nodeA, nodeB, neighborDistances.x, distanceAirAB, false));
        }
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    static void CreateEdgesTempsFromRivers(
        RawArray<int> fieldsMap, in Dictionary<int, int> colorToIndex, RawArray<double2> indexToCenterGeoCoord,
        RawArray<int> riversIndexesMap, int2 textureSize, RawArray<RiverData> riversDatas, ref RawBag<EdgeTemp> edgesTemps, RawArray<bool> riverNodesExistingMap)
    {
        var riverNodesPtr = stackalloc (NodeId NodeId, int PixelCoordIndex)[RIVER_NODES_LENGTH];
        var riverNodes = new RawListStackalloc<(NodeId NodeId, int PixelCoordIndex)>(riverNodesPtr, RIVER_NODES_LENGTH);

        for (int i = 0; i < riversDatas.Length; i++)
        {
            ref var data = ref riversDatas[i];
            
            if (data.StartsFrom.Count == 0)
            {
                AddFieldRiverEdge(data.PixelCoords[0], fieldsMap, in colorToIndex, indexToCenterGeoCoord, riversIndexesMap, textureSize, ref edgesTemps, false);
            }

            if (data.EndsInto.Count == 0)
            {
                AddFieldRiverEdge(data.PixelCoords[data.PixelCoords.Length - 1], fieldsMap, in colorToIndex, indexToCenterGeoCoord, riversIndexesMap, textureSize, ref edgesTemps, true);
            }

            GetRiverNodesIds(ref riverNodes, in data, riversIndexesMap, riverNodesExistingMap, textureSize);

            for (int j = 0; j < riverNodes.Count - 1; j++)
            {
                ref readonly var nodeDataA = ref riverNodes[j];
                ref readonly var nodeDataB = ref riverNodes[j + 1];

                var distanceGround = CalculateDistanceBetweenPixels(in data.PixelCoords, nodeDataA.PixelCoordIndex, nodeDataB.PixelCoordIndex, textureSize);
                var distanceAir = GeoUtilitiesDouble.Distance(nodeDataA.NodeId.GeoCoord, nodeDataB.NodeId.GeoCoord);

                edgesTemps.Add(new EdgeTemp(nodeDataA.NodeId, nodeDataB.NodeId, distanceGround, distanceAir, true));
            }
        }
    }

    static void AddFieldRiverEdge(
        int2 riverPixelCoord, RawArray<int> fieldsMap, in Dictionary<int, int> colorToIndex, RawArray<double2> indexToCenterGeoCoord,
        RawArray<int> riversIndexesMap, int2 textureSize, ref RawBag<EdgeTemp> edgesTemps, bool riverIntoField)
    {
        int flat = TexUtilities.PixelCoordToFlat(riverPixelCoord, textureSize.x);

        uint indexField = (uint)colorToIndex[fieldsMap[flat]];
        var geoCoordField = indexToCenterGeoCoord[indexField];

        var uvRiver = GeoUtilitiesDouble.PixelCoordToPlaneUv(riverPixelCoord, textureSize);
        var geoCoordRiver = GeoUtilitiesDouble.PlaneUvToGeoCoords(uvRiver);
        int indexRiver = riversIndexesMap[flat];

        double distanceBoth = GeoUtilitiesDouble.Distance(geoCoordRiver, geoCoordField);

        if (indexRiver == -1)
            throw new Exception($"NodesUtility4 :: CreateEdgesTempsFromRivers :: AddFieldRiverEdge :: River ({TexUtilities.FlipY(riverPixelCoord, textureSize.y)}) does not exist!");

        var nodeIdRiver = new NodeId(geoCoordRiver, NodeOwnerType.River, (uint)indexRiver);
        var nodeIdField = new NodeId(geoCoordField, NodeOwnerType.Field, indexField);

        var (nodeIdFirst, nodeIdSecond) = riverIntoField ? (nodeIdRiver, nodeIdField) : (nodeIdField, nodeIdRiver);

        edgesTemps.Add(new EdgeTemp(nodeIdFirst, nodeIdSecond, distanceBoth, distanceBoth, true));
    }

    static void GetRiverNodesIds(
        ref RawListStackalloc<(NodeId NodeId, int PixelCoordIndex)> riversNodes,
        in RiverData data, RawArray<int> riversIndexesMap, RawArray<bool> riverNodesExistingMap, int2 textureSize)
    {
        riversNodes.Clear();

        for (int i = 0; i < data.PixelCoords.Length; i++)
        {
            int flat = TexUtilities.PixelCoordToFlat(data.PixelCoords[i], textureSize.x);

            if (i == 0 || i == data.PixelCoords.Length - 1 || riverNodesExistingMap[flat])
            {
                var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(data.PixelCoords[i], textureSize);
                var geoCoord = GeoUtilitiesDouble.PlaneUvToGeoCoords(uv);
                int index = riversIndexesMap[flat];

                if (index == -1)
                    throw new Exception($"NodesUtility4 :: GetRiverNodesIds :: River ({TexUtilities.FlipY(data.PixelCoords[i], textureSize.y)}) does not exist!");

                var nodeId = new NodeId(geoCoord, NodeOwnerType.River, (uint)index);

                riversNodes.Add((nodeId, i));
            }
        }
    }

    static double CalculateDistanceBetweenPixels(in RawArray<int2> pixelCoords, int startIndex, int endIndex, int2 textureSize)
    {
        double distance = 0;

        for (int i = startIndex; i <= endIndex - 1; i++)
        {
            var uvA = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoords[i], textureSize);
            var uvB = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoords[i + 1], textureSize);

            var geoCoordA = GeoUtilitiesDouble.PlaneUvToGeoCoords(uvA);
            var geoCoordB = GeoUtilitiesDouble.PlaneUvToGeoCoords(uvB);

            distance += GeoUtilitiesDouble.Distance(geoCoordA, geoCoordB);
        }

        return distance;
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    static void CreateFinalData(in RawBag<EdgeTemp> edgesTemps, int nodesCountPredict, out RawBag<EdgeFinal> edgesFinals, out RawBag<NodeFinal> nodesFinals, out Dictionary<NodeId, uint> nodeIdToNodeIndex)
    {
        edgesFinals = new RawBag<EdgeFinal>(ALLOCATOR, edgesTemps.Count);
        nodesFinals = new RawBag<NodeFinal>(ALLOCATOR, nodesCountPredict);
        nodeIdToNodeIndex = new Dictionary<NodeId, uint>(nodesCountPredict);

        for (int i = 0; i < edgesTemps.Count; i++)
        {
            ref readonly var edgeTemp = ref edgesTemps[i];

            uint nodeIndexA = GetNodeIndex(edgeTemp.NodeIdA, ref nodeIdToNodeIndex, ref nodesFinals);
            uint nodeIndexB = GetNodeIndex(edgeTemp.NodeIdB, ref nodeIdToNodeIndex, ref nodesFinals);
            int crossedRiverNodeIndex = edgeTemp.CrossedRiverEnabled ? (int)GetNodeIndex(edgeTemp.CrossedRiverNodeId, ref nodeIdToNodeIndex, ref nodesFinals) : -1;

            edgesFinals.Add(new EdgeFinal(nodeIndexA, nodeIndexB, edgeTemp.DistanceGround, edgeTemp.DistanceAir, crossedRiverNodeIndex, edgeTemp.UseInRiverPoints));
        }

        AssignEdgesIndexesToNodes(in edgesFinals, ref nodesFinals);
    }

    static uint GetNodeIndex(NodeId nodeId, ref Dictionary<NodeId, uint> nodeIdToNodeIndex, ref RawBag<NodeFinal> nodesFinals)
    {
        if (nodeIdToNodeIndex.TryGetValue(nodeId, out uint index))
            return index;

        var nodeFinalNew = nodeId.ToFinal(ALLOCATOR);
        uint indexNew = (uint)nodesFinals.Count;

        nodesFinals.Add(nodeFinalNew);
        nodeIdToNodeIndex[nodeId] = indexNew;

        return indexNew;
    }

    static void AssignEdgesIndexesToNodes(in RawBag<EdgeFinal> edgesFinals, ref RawBag<NodeFinal> nodesFinals)
    {
        uint edgesCount = (uint)edgesFinals.Count;

        for (uint i = 0; i < edgesCount; i++)
        {
            ref readonly var edgeFinal = ref edgesFinals[i];

            nodesFinals[edgeFinal.NodeIndexA].EdgesIndexes.Add(i);
            nodesFinals[edgeFinal.NodeIndexB].EdgesIndexes.Add(i);
        }
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    static void CreateRiverPoints(
        ref RawBag<NodeFinal> nodesFinals, in RawBag<EdgeFinal> edgesFinals, in Dictionary<NodeId, uint> nodeIdToNodeIndex,
        out RawBag<RiverPoint> riverPoints, out Dictionary<NodeId, uint> nodeIdToRiverPointIndex)
    {
        riverPoints = new RawBag<RiverPoint>(ALLOCATOR, GetRiverPointsCount(in nodesFinals));
        nodeIdToRiverPointIndex = new Dictionary<NodeId, uint>(riverPoints.Capacity);

        for (int i = 0; i < nodesFinals.Count; i++)
        {
            ref var nodeFinal = ref nodesFinals[i];

            if (nodeFinal.Owner.Type != NodeOwnerType.River)
                continue;

            var nodeId = nodesFinals[i].ToId();

            uint nodeIndex = nodeIdToNodeIndex[nodeId];
            uint riverIndex = nodeFinal.Owner.Index;
            uint riverPointIndex = (uint)riverPoints.Count;

            nodeIdToRiverPointIndex[nodeId] = riverPointIndex;
            nodeFinal.Owner.Index = riverPointIndex;

            riverPoints.Add(new RiverPoint(riverIndex, nodeIndex));
        }

        // after this step we have created RiverPoint and NodeId -> RiverPoint index map
        // NodeFinal from which RiverPoints was created now points to RiverPoint index (instead of river index, like before)
        // we have a guarantee that NodeIdA flows to NodeIdB for each EdgeFinal, from this we'll calculate connection directions

        for (int i = 0; i < edgesFinals.Count; i++)
        {
            ref readonly var edgeFinal = ref edgesFinals[i];

            ref readonly var nodeA = ref nodesFinals[edgeFinal.NodeIndexA];
            ref readonly var nodeB = ref nodesFinals[edgeFinal.NodeIndexB];

            // normal river -> river flow
            if (nodeA.Owner.Type == NodeOwnerType.River && nodeB.Owner.Type == NodeOwnerType.River)
            {
                riverPoints[nodeA.Owner.Index].EndsInto.Add(nodeB.Owner.Index);
                riverPoints[nodeB.Owner.Index].StartsFrom.Add(nodeA.Owner.Index);
            }

            // source field -> river flow
            else if (edgeFinal.UseInRiverPoints && nodeA.Owner.Type == NodeOwnerType.Field)
            {
                ref int startsFromFieldIndex = ref riverPoints[nodeB.Owner.Index].StartsFromFieldIndex;

                if (startsFromFieldIndex != -1)
                    throw new Exception($"NodesUtility4 :: CreateRiverPoints :: River point ({nodeB.Owner.Index}) already has field assigned ({startsFromFieldIndex})!");

                startsFromFieldIndex = (int)nodeA.Owner.Index;
            }
        }

        for (int i = 0; i < riverPoints.Count; i++)
        {
            ref var point = ref riverPoints[i];
            uint nodeIndex = point.NodeIndex;
            ref readonly var node = ref nodesFinals[nodeIndex];

            for (int j = 0; j < node.EdgesIndexes.Count; j++)
            {
                ref readonly var edge = ref edgesFinals[node.EdgesIndexes[j]];
                uint nodeIndexOther = edge.NodeIndexA ^ edge.NodeIndexB ^ nodeIndex;
                ref readonly var nodeOther = ref nodesFinals[nodeIndexOther];

                if (nodeOther.Owner.Type == NodeOwnerType.River)
                    continue;

                point.NeighborFieldsIndexes.Add(nodeOther.Owner.Index);
            }
        }
    }

    static int GetRiverPointsCount(in RawBag<NodeFinal> nodesFinals)
    {
        int count = 0;

        for (int i = 0; i < nodesFinals.Count; i++)
        {
            count += nodesFinals[i].Owner.Type == NodeOwnerType.River ? 1 : 0;
        }

        return count;
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    static void CreateFieldsNodesIndexes(int fieldsLength, in RawBag<NodeFinal> nodesFinals, out RawArray<uint> fieldsNodesIndexes)
    {
        var fieldsNodesIndexesTemp = new RawArray<int>(ALLOCATOR, -1, fieldsLength);

        for (int i = 0; i < nodesFinals.Count; i++)
        {
            ref readonly var node = ref nodesFinals[i];

            if (node.Owner.Type == NodeOwnerType.River)
                continue;

            uint fieldIndex = node.Owner.Index;

            if (fieldsNodesIndexesTemp[fieldIndex] != -1)
                throw new Exception($"NodesUtility4 :: Field ({fieldIndex}) has already node ({fieldsNodesIndexesTemp[fieldIndex]}) assigned!");

            fieldsNodesIndexesTemp[fieldIndex] = i;
        }

        fieldsNodesIndexes = new RawArray<uint>(ALLOCATOR, fieldsLength);

        for (int i = 0; i < fieldsNodesIndexesTemp.Length; i++)
        {
            int nodeIndex = fieldsNodesIndexesTemp[i];

            if (nodeIndex == -1)
                throw new Exception($"NodesUtility4 :: Field ({i}) has no node assigned!");

            fieldsNodesIndexes[i] = (uint)fieldsNodesIndexesTemp[i];
        }

        fieldsNodesIndexesTemp.Dispose();
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    static void CreateRivers(in RawArray<RiverData> riversDatas, RawArray<int> riversIndexesMap, int2 textureSize, in Dictionary<NodeId, uint> nodeIdToRiverPointIndex, out RawArray<RawBag<uint>> rivers)
    {
        rivers = new RawArray<RawBag<uint>>(ALLOCATOR, riversDatas.Length);

        for (int i = 0; i < riversDatas.Length; i++)
        {
            rivers[i] = new RawBag<uint>(ALLOCATOR, 32);

            ref readonly var data = ref riversDatas[i];
            ref var riverRiverPointsIndexes = ref rivers[i];

            for (int j = 0; j < data.PixelCoords.Length; j++)
            {
                ref readonly var pixelCoord = ref data.PixelCoords[j];

                int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);
                int riverIndex = riversIndexesMap[flat];
                var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoord, textureSize);
                var geoCoord = GeoUtilitiesDouble.PlaneUvToGeoCoords(uv);

                if (riverIndex == -1)
                    throw new Exception($"NodesUtility4 :: CreateRivers :: River index ({riverIndex}) is invalid!");

                var nodeId = new NodeId(geoCoord, NodeOwnerType.River, (uint)riverIndex);

                if (nodeIdToRiverPointIndex.TryGetValue(nodeId, out uint riverPointIndex))
                {
                    riverRiverPointsIndexes.Add(riverPointIndex);
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    static void SaveFinalData(string savePathNodes, string savePathEdges, in RawBag<NodeFinal> nodesFinals, in RawBag<EdgeFinal> edgesFinals)
    {
        FileStream fileStream;

        using (fileStream = new FileStream(savePathNodes, FileMode.Create, FileAccess.Write))
        {
            fileStream.WriteValue(nodesFinals.Count);

            for (int i = 0; i < nodesFinals.Count; i++)
            {
                fileStream.WriteValue(nodesFinals[i].GeoCoord);
            }

            for (int i = 0; i < nodesFinals.Count; i++)
            {
                fileStream.WriteValue(nodesFinals[i].Owner);
            }

            for (int i = 0; i < nodesFinals.Count; i++)
            {
                BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, nodesFinals[i].EdgesIndexes);
            }
        }

        using (fileStream = new FileStream(savePathEdges, FileMode.Create, FileAccess.Write))
        {
            fileStream.WriteValue(edgesFinals.Count);

            for (int i = 0; i < edgesFinals.Count; i++)
            {
                fileStream.WriteValue(edgesFinals[i].NodeIndexA);
                fileStream.WriteValue(edgesFinals[i].NodeIndexB);
            }

            for (int i = 0; i < edgesFinals.Count; i++)
            {
                fileStream.WriteValue(edgesFinals[i].DistanceGround);
            }

            for (int i = 0; i < edgesFinals.Count; i++)
            {
                fileStream.WriteValue(edgesFinals[i].DistanceAir);
            }

            for (int i = 0; i < edgesFinals.Count; i++)
            {
                ref readonly int crossedRiverNodeIndex = ref edgesFinals[i].CrossedRiverNodeIndex;
                int crossedRiverPointIndex = crossedRiverNodeIndex == -1 ? -1 : (int)nodesFinals[crossedRiverNodeIndex].Owner.Index;

                fileStream.WriteValue(crossedRiverPointIndex);
            }
        }
    }

    static void SaveRiverPoints(string savePathRiverPoints, in RawBag<RiverPoint> riverPoints)
    {
        using var fileStream = new FileStream(savePathRiverPoints, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(riverPoints.Count);

        for (int i = 0; i < riverPoints.Count; i++)
        {
            fileStream.WriteValue(riverPoints[i].RiverIndex);
            fileStream.WriteValue(riverPoints[i].NodeIndex);
            fileStream.WriteValue(riverPoints[i].StartsFromFieldIndex);

            BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, riverPoints[i].StartsFrom);
            BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, riverPoints[i].EndsInto);
            BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, riverPoints[i].NeighborFieldsIndexes);
        }
    }

    static void SaveFieldsNodesIndexes(string savePathFieldsNodesIndexes, in RawArray<uint> fieldsNodesIndexes)
    {
        BinarySaveUtility.WriteRawContainerSimple<RawArray<uint>, uint>(savePathFieldsNodesIndexes, fieldsNodesIndexes);
    }

    static void SaveRivers(string savePathRivers, in RawArray<RawBag<uint>> rivers, in RawArray<RiverData> riversDatas)
    {
        using var fileStream = new FileStream(savePathRivers, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(rivers.Length);

        for (int i = 0; i < rivers.Length; i++)
        {
            BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, rivers[i]);
            BinarySaveUtility.WriteRawContainerSimple<RawArray<int2>, int2>(fileStream, riversDatas[i].PixelCoords);
        }
    }

    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------
    // -----------------------------------------------------------------------

    struct EdgeTemp
    {
        public NodeId NodeIdA;
        public NodeId NodeIdB;

        public double DistanceGround;
        public double DistanceAir;

        public NodeId CrossedRiverNodeId;
        public bool CrossedRiverEnabled;

        public bool UseInRiverPoints;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EdgeTemp(NodeId nodeIdA, NodeId nodeIdB, double distanceGround, double distanceAir, NodeId crossedRiverNodeId, bool useInRiverPoints)
        {
            NodeIdA = nodeIdA;
            NodeIdB = nodeIdB;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            CrossedRiverNodeId = crossedRiverNodeId;
            CrossedRiverEnabled = true;
            UseInRiverPoints = useInRiverPoints;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EdgeTemp(NodeId nodeIdA, NodeId nodeIdB, double distanceGround, double distanceAir, bool useInRiverPoints)
        {
            NodeIdA = nodeIdA;
            NodeIdB = nodeIdB;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            CrossedRiverNodeId = default;
            CrossedRiverEnabled = false;
            UseInRiverPoints = useInRiverPoints;
        }
    }

    struct NodeId
    {
        public double2 GeoCoord;
        public NodeOwner Owner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeId(double2 geoCoord, NodeOwnerType ownerType, uint index)
        {
            GeoCoord = geoCoord;
            Owner = new NodeOwner(ownerType, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly NodeFinal ToFinal(Allocator allocator) => new(GeoCoord, Owner, new RawBag<uint>(allocator));
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EdgeFinal
    {
        public uint NodeIndexA;
        public uint NodeIndexB;

        public double DistanceGround;
        public double DistanceAir;

        public int CrossedRiverNodeIndex;

        public bool UseInRiverPoints;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public EdgeFinal(uint nodeIndexA, uint nodeIndexB, double distanceGround, double distanceAir, int crossedRiverNodeIndex, bool useInRiverPoints)
        {
            NodeIndexA = nodeIndexA;
            NodeIndexB = nodeIndexB;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            CrossedRiverNodeIndex = crossedRiverNodeIndex;
            UseInRiverPoints = useInRiverPoints;
        }
    }

    struct NodeFinal : IDisposable
    {
        public double2 GeoCoord;
        public NodeOwner Owner;
        public RawBag<uint> EdgesIndexes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeFinal(double2 geoCoord, NodeOwner owner, RawBag<uint> edgesIndexes)
        {
            GeoCoord = geoCoord;
            Owner = owner;
            EdgesIndexes = edgesIndexes;
        }

        public void Dispose()
        {
            EdgesIndexes.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly NodeId ToId() => new(GeoCoord, Owner.Type, Owner.Index);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NodeOwner
    {
        public NodeOwnerType Type;
        public uint Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeOwner(NodeOwnerType type, uint index)
        {
            Type = type;
            Index = index;
        }
    }

    enum NodeOwnerType : uint
    {
        Field = 0,
        River = 10,
    }

    struct RiverPoint : IDisposable
    {
        public uint RiverIndex;
        public uint NodeIndex;

        public int StartsFromFieldIndex;

        public RawBag<uint> StartsFrom;
        public RawBag<uint> EndsInto;

        public RawBag<uint> NeighborFieldsIndexes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RiverPoint(uint riverIndex, uint nodeIndex)
        {
            RiverIndex = riverIndex;
            NodeIndex = nodeIndex;
            StartsFromFieldIndex = -1;
            StartsFrom = new RawBag<uint>(ALLOCATOR);
            EndsInto = new RawBag<uint>(ALLOCATOR);
            NeighborFieldsIndexes = new RawBag<uint>(ALLOCATOR);
        }

        public void Dispose()
        {
            StartsFrom.Dispose();
            EndsInto.Dispose();
            NeighborFieldsIndexes.Dispose();
        }
    }
}
