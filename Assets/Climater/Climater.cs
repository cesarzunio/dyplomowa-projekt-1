using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed unsafe class Climater : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathClimatesTextureOriginal;
    [SerializeField] int2 _climatesTextureOriginalSize;
    [SerializeField] string _pathPathFieldsMap;
    [SerializeField] string _pathPathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Output")]
    [SerializeField] string _savePathFieldsClimates;
    [SerializeField] string _savePathFieldsClimatesMap;

    public void Generate()
    {
        var climatesTextureOriginal = ClimaterUtilities.LoadClimatesMapOriginal(_savePathClimatesTextureOriginal, _climatesTextureOriginalSize, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_pathPathFieldsMap, ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_pathPathFields, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);
        var nodes = NodesSaveUtility2.LoadNodes(_savePathNodes, ALLOCATOR);
        var edges = NodesSaveUtility2.LoadEdges(_savePathNodeEdges, ALLOCATOR);

        var fieldToClimate = ClimaterUtilities.CreateClimates(climatesTextureOriginal, fieldsMap, fields, fieldsNodesIndexes, nodes, edges, ALLOCATOR);

        TextureSaver.Save(fieldsMap.TextureSize, _savePathFieldsClimatesMap, (i) =>
        {
            uint field = fieldsMap.Fields[i];
            return fields.IsLand[field] ? ClimaterUtilities.ClimateToColor(fieldToClimate[field]) : default;
        });

        fieldsMap.Dispose();
        fields.Dispose();
        fieldsNodesIndexes.Dispose();
        nodes.Dispose();
        edges.Dispose();
        fieldToClimate.Dispose();
    }
}

[CustomEditor(typeof(Climater))]
public sealed class ClimaterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var climater = (Climater)target;
            climater.Generate();
        }
    }
}