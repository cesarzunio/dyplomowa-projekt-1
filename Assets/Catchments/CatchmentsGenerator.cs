using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

public sealed unsafe class CatchmentsGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathEdges;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathFieldsNodesIndexes;
    [SerializeField] string _savePathRiverPoints;
    [SerializeField] string _savePathFieldsElevations;

    [Header("Params")]
    [SerializeField] float _heightDiffMax;
    [SerializeField] double _distanceMax;

    [Header("Output")]
    [SerializeField] string _savePathCatchments;
    [SerializeField] TextureCreationMode _saveMode;
    [SerializeField] string _savePathCatchmentsMap;

    public void Generate()
    {
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var edges = NodesSaveUtility4.LoadEdges(_savePathEdges, ALLOCATOR);
        var nodes = NodesSaveUtility4.LoadNodes(_savePathNodes, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodesIndexes, ALLOCATOR);
        var riverPoints = NodesSaveUtility4.LoadRiverPoints(_savePathRiverPoints, ALLOCATOR);
        var elevations = BinarySaveUtility.ReadRawArray<float>(_savePathFieldsElevations, ALLOCATOR);

        var nodeIndexToStartNodeIndex = new RawArray<int>(ALLOCATOR, -1, nodes.Length);
        var riverPointIndexToFieldIndexes = new RawArray<RawBag<uint>>(ALLOCATOR, riverPoints.Length);

        for (int i = 0; i < riverPointIndexToFieldIndexes.Length; i++)
        {
            riverPointIndexToFieldIndexes[i] = new RawBag<uint>(ALLOCATOR, 16);
        }

        var job = new CatchmentsJob
        {
            Edges = edges,
            Nodes = nodes,
            FieldsElevations = elevations,
            HeightDiffMax = _heightDiffMax,
            DistanceMax = _distanceMax,
            NodeIndexToStartNodeIndex = nodeIndexToStartNodeIndex,
            RiverPointIndexToFieldIndexes = riverPointIndexToFieldIndexes,
        };

        job.Schedule().Complete();

        SaveCatchments(_savePathCatchments, riverPointIndexToFieldIndexes);

        if (_saveMode == TextureCreationMode.RandomColors)
        {
            var randomColors = CesColorUtilities.CreateRandomColors(nodes.Length);

            TextureSaver.Save(fieldsMap.TextureSize, _savePathCatchmentsMap, (i) =>
            {
                uint field = fieldsMap.Fields[i];
                uint nodeIndex = fieldsNodesIndexes[field];
                int startNodeIndex = nodeIndexToStartNodeIndex[nodeIndex];

                if (startNodeIndex == -1)
                    return default;

                //uint startNodeOwnerIndex = nodes.Owner[startNodeIndex].Index;
                //uint riverIndex = riverPoints[startNodeOwnerIndex].RiverIndex;
                //return randomColors[riverIndex];

                uint startNodeOwnerIndex = nodes.Owner[startNodeIndex].Index;
                return randomColors[startNodeOwnerIndex];
            });
        }

        fields.Dispose();
        fieldsMap.Dispose();
        edges.Dispose();
        nodes.Dispose();
        fieldsNodesIndexes.Dispose();
        riverPoints.Dispose();
        elevations.Dispose();
        nodeIndexToStartNodeIndex.Dispose();
        riverPointIndexToFieldIndexes.DisposeDepth1();
    }

    static void SaveCatchments(string path, RawArray<RawBag<uint>> riverPointsCatchments)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(riverPointsCatchments.Length);

        for (int i = 0; i <  riverPointsCatchments.Length; i++)
        {
            BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, riverPointsCatchments[i]);
        }
    }
}

[CustomEditor(typeof(CatchmentsGenerator))]
public sealed class CatchmentsGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var cg = (CatchmentsGenerator)target;

        if (GUILayout.Button("Generate"))
        {
            cg.Generate();
        }
    }
}
