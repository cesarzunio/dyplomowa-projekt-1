using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed class PopsGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [Header("Input")]
    [SerializeField] string _savePathPopsTextureOriginal;
    [SerializeField] int2 _popsTextureOriginalTextureSize;
    [SerializeField] int _distanceInPixelsOnEquatorMax;
    [SerializeField] int _originalToFieldsMapOffsetY;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Output")]
    [SerializeField] bool _createTexture;
    [SerializeField] string _savePathFieldsPops;
    [SerializeField] string _savePathFieldsPopsMap;

    public void Generate2()
    {
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var popsTexture = PopsUtility2.LoadPopsTexture(_savePathPopsTextureOriginal, _popsTextureOriginalTextureSize, _originalToFieldsMapOffsetY, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);
        var nodes = NodesSaveUtility4.LoadNodes(_savePathNodes, ALLOCATOR);
        var edges = NodesSaveUtility4.LoadEdges(_savePathNodeEdges, ALLOCATOR);

        var fieldToPop = PopsUtility2.GenerateFieldsPops(fields, fieldsMap, popsTexture, nodes, edges, fieldsNodesIndexes, ALLOCATOR);

        PopsUtility2.SaveFieldsPops(_savePathFieldsPops, fieldToPop);

        if (_createTexture)
        {
            PopsUtility2.SaveFieldsPopsMap(_savePathFieldsPopsMap, fieldToPop, fieldsMap);
        }

        fieldsMap.Dispose();
        fields.Dispose();
        popsTexture.Dispose();
        fieldsNodesIndexes.Dispose();
        nodes.Dispose();
        edges.Dispose();
        fieldToPop.Dispose();
    }
}

[CustomEditor(typeof(PopsGenerator))]
public sealed class PopsGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var pg = (PopsGenerator)target;
            pg.Generate2();
        }
    }
}