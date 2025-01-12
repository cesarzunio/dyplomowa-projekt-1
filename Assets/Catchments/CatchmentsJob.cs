using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static NodesSaveUtility4;

[BurstCompile]
public unsafe struct CatchmentsJob : IJob
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public EdgesFinals Edges;
    public NodesFinals Nodes;
    public RawArray<float> FieldsElevations;
    public float HeightDiffMax;
    public double DistanceMax;
    public RawArray<int> NodeIndexToStartNodeIndex;
    public RawArray<RawBag<uint>> RiverPointIndexToFieldIndexes;

    [BurstCompile]
    public void Execute()
    {
        var closedSet = new RawArray<bool>(ALLOCATOR, false, Nodes.Length);
        var queue = new RawGeoQueue<uint>(Nodes.Length, Nodes.Length, ALLOCATOR);
        var nodeIndexToIsStarting = new RawArray<bool>(ALLOCATOR, false, Nodes.Length);

        AddStartings(in Nodes, ref queue, ref NodeIndexToStartNodeIndex, ref nodeIndexToIsStarting);

        while (queue.TryPop(out uint currentNodeIndex))
        {
            if (closedSet[currentNodeIndex])
                continue;

            closedSet[currentNodeIndex] = true;

            bool isStarting = nodeIndexToIsStarting[currentNodeIndex];
            var costCurrent = queue.GetCost(currentNodeIndex);
            ref readonly var nodeOwnerCurrent = ref Nodes.Owner[currentNodeIndex];
            var edgesIndexes = Nodes.EdgesIndexes[currentNodeIndex];

            if (costCurrent >= DistanceMax)
                continue;

            for (int i = 0; i < edgesIndexes.Length; i++)
            {
                uint edgeIndex = edgesIndexes[i];
                var nodesIndexes = Edges.NodesIndexes[edgeIndex];

                uint neighborIndex = nodesIndexes.x ^ nodesIndexes.y ^ currentNodeIndex;
                ref readonly var nodeOwnerNeighbor = ref Nodes.Owner[neighborIndex];

                if (closedSet[neighborIndex])
                    continue;

                if (nodeOwnerNeighbor.Type == NodeOwnerType.River)
                {
                    closedSet[neighborIndex] = true;
                    continue;
                }

                if (!isStarting && FieldsElevations[nodeOwnerNeighbor.Index] - FieldsElevations[nodeOwnerCurrent.Index] < HeightDiffMax)
                    continue;

                double newCost = costCurrent + Edges.DistanceAir[edgeIndex];

                if (!queue.TryGetCost(neighborIndex, out double existingCost) || newCost < existingCost)
                {
                    queue.AddOrUpdate(neighborIndex, newCost);
                    NodeIndexToStartNodeIndex[neighborIndex] = NodeIndexToStartNodeIndex[currentNodeIndex];

                }
            }
        }

        for (int i = 0; i < NodeIndexToStartNodeIndex.Length; i++)
        {
            if (NodeIndexToStartNodeIndex[i] == -1)
                continue;

            uint nodeIndex = (uint)i;
            uint nodeIndexStart = (uint)NodeIndexToStartNodeIndex[i];

            ref readonly var nodeOwner = ref Nodes.Owner[nodeIndex];
            ref readonly var nodeOwnerStart = ref Nodes.Owner[nodeIndexStart];

            if (!nodeIndexToIsStarting[i] && nodeOwner.Type != NodeOwnerType.Field)
                throw new Exception("CatchmentsJob :: Node is not field nor starting node!");

            if (nodeOwnerStart.Type != NodeOwnerType.River)
                throw new Exception("CatchmentsJob :: Node start is not river!");

            RiverPointIndexToFieldIndexes[nodeOwnerStart.Index].Add(nodeOwner.Index);
        }

        closedSet.Dispose();
        queue.Dispose();
        nodeIndexToIsStarting.Dispose();
    }

    static void AddStartings(in NodesFinals nodes, ref RawGeoQueue<uint> queue, ref RawArray<int> nodeIndexToStartNodeIndex, ref RawArray<bool> nodeIndexToIsStarting)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes.Owner[i].Type == NodeOwnerType.River)
            {
                queue.Add((uint)i, 0.0);
                nodeIndexToStartNodeIndex[i] = i;
                nodeIndexToIsStarting[i] = true;
            }
        }
    }
}
