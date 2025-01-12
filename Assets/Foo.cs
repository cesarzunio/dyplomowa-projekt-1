using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static NodesSaveUtility2;

public sealed class Foo : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathRivers;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;
    [SerializeField] string _savePathRiversNodes;

    public void Test()
    {
        var nodes = NodesSaveUtility2.LoadNodes(_savePathNodes, ALLOCATOR);
        var nodeEdges = NodesSaveUtility2.LoadEdges(_savePathNodeEdges, ALLOCATOR);

        CheckEdges(nodes, nodeEdges);

        nodes.DisposeDepth1();
        nodeEdges.Dispose();
    }

    public void Test2()
    {
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var rivers = UnwrapperUtilities.LoadRiversData(_savePathRivers, ALLOCATOR);
        var nodes = NodesSaveUtility2.LoadNodes(_savePathNodes, ALLOCATOR);
        NodesUtility2.LoadNodeOwners(_savePathFieldsNodes, _savePathRiversNodes, out var fieldsNodesIndexes, out var riversNodesIndexes, ALLOCATOR);

        var minMaxFields = GetMinMax(fieldsNodesIndexes);
        var minMaxRivers = GetMinMax(riversNodesIndexes);
        var minMaxNodeOwners = GetMinMax(nodes);

        Debug.Log(
            $"Fields: {fields.Length:#,##0}, " +
            $"Rivers: {rivers.Length:#,##0}, " +
            $"Nodes: {nodes.Length:#,##0}, " +
            $"FieldsNodesIndexes: {fieldsNodesIndexes.Length:#,##0}, " +
            $"RiversNodesIndexes: {riversNodesIndexes.Length:#,##0}, " +
            $"MinMaxFields: {minMaxFields.y:#,##0}, " +
            $"MinMaxRivers: {minMaxRivers.y:#,##0}, " +
            $"MinMaxNodeOwners {minMaxNodeOwners.y:#,##0}");

        fields.Dispose();
        rivers.DisposeDepth1();
        nodes.DisposeDepth1();
        fieldsNodesIndexes.Dispose();
        riversNodesIndexes.DisposeDepth1();
    }

    static uint2 GetMinMax(RawArray<uint> fieldsNodesIndexes)
    {
        uint min = uint.MaxValue;
        uint max = uint.MinValue;

        for (int i = 0; i < fieldsNodesIndexes.Length; i++)
        {
            min = math.min(min, fieldsNodesIndexes[i]);
            max = math.max(max, fieldsNodesIndexes[i]);
        }

        return new uint2(min, max);
    }

    static uint2 GetMinMax(RawArray<RawArray<uint>> riversNodesIndexes)
    {
        uint min = uint.MaxValue;
        uint max = uint.MinValue;

        for (int i = 0; i < riversNodesIndexes.Length; i++)
        {
            for (int j = 0; j < riversNodesIndexes[i].Length; j++)
            {
                min = math.min(min, riversNodesIndexes[i][j]);
                max = math.max(max, riversNodesIndexes[i][j]);
            }
        }

        return new uint2(min, max);
    }

    static uint2 GetMinMax(RawArray<NodeSerialized> nodes)
    {
        uint min = uint.MaxValue;
        uint max = uint.MinValue;

        for (int i = 0; i < nodes.Length; i++)
        {
            min = math.min(min, nodes[i].Owner.Index);
            max = math.max(max, nodes[i].Owner.Index);
        }

        return new uint2(min, max);
    }

    static void CheckEdges(RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges)
    {
        int checks = 0;

        int min = int.MaxValue;
        int max = int.MinValue;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].Edges.Length == 0)
                Debug.LogError($"Node {i} has no edges!");

            min = math.min(min, nodes[i].Edges.Length);
            max = math.max(max, nodes[i].Edges.Length);

            for (int j = 0; j < nodes[i].Edges.Length; j++)
            {
                ref var edge = ref edges[nodes[i].Edges[j]];

                checks++;

                try
                {
                    edge.GetOtherNodeIndex((uint)i);
                }
                catch (Exception e)
                {
                    Debug.LogError("_________________");
                    Debug.LogError($"Failed on node: {i}, Owner: {nodes[i].Owner}");
                    Debug.LogError(e);
                }
            }
        }

        Debug.Log($"Edges min: {min}, max: {max}");
        Debug.Log($"Checks: {checks}");
    }
}

[CustomEditor(typeof(Foo))]
public sealed class FooEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Test"))
        {
            var f = (Foo)target;
            f.Test();
        }
    }
}