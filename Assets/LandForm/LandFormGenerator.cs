using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static LandFormUtility;

public sealed unsafe class LandFormGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathLandFormOriginalPrefix;
    [SerializeField] int2 _landFormTextureOriginalSize;
    [SerializeField] int _landFormSaveStripHeight;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Output")]
    [SerializeField] string _savePathFieldsLandForms;
    [SerializeField] TextureCreationMode _saveMode;
    [SerializeField] string _savePathFieldsLandFormsMap;

    public void GenerateMapTest()
    {
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var landFormTextureOriginal = LandFormUtility.LoadLandFormTextureFromStrips(_savePathLandFormOriginalPrefix, _landFormTextureOriginalSize, _landFormSaveStripHeight, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);
        var nodes = NodesSaveUtility4.LoadNodes(_savePathNodes, ALLOCATOR);
        var edges = NodesSaveUtility4.LoadEdges(_savePathNodeEdges, ALLOCATOR);

        var fieldsLandForms = LandFormUtility.GenerateLandForms(fields, fieldsMap, landFormTextureOriginal, fieldsNodesIndexes, nodes, edges, ALLOCATOR);

        BinarySaveUtility.WriteRawContainerSimple<RawArray<LandForm>, LandForm>(_savePathFieldsLandForms, fieldsLandForms);

        if (_saveMode == TextureCreationMode.RandomColors)
        {
            var randomColors = CesColorUtilities.CreateRandomColors(50);
            randomColors[0] = default;

            TextureSaver.Save(fieldsMap.TextureSize, _savePathFieldsLandFormsMap, (i) =>
            {
                uint field = fieldsMap.Fields[i];
                var landForm = fieldsLandForms[field];
                return randomColors[(byte)landForm];
            });
        }
        else if (_saveMode == TextureCreationMode.Bytes)
        {
            TextureSaver.Save(fieldsMap.TextureSize, _savePathFieldsLandFormsMap, (i) =>
            {
                uint field = fieldsMap.Fields[i];
                var landForm = fieldsLandForms[field];
                var b = (byte)landForm;
                return new Color32(b, b, b, 255);
            });
        }

        landFormTextureOriginal.Dispose();
        fields.Dispose();
        fieldsMap.Dispose();
        fieldsNodesIndexes.Dispose();
        nodes.Dispose();
        edges.Dispose();
        fieldsLandForms.Dispose();
    }
}

[CustomEditor(typeof(LandFormGenerator))]
public sealed class LandFormGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate map test"))
        {
            var lfg = (LandFormGenerator)target;
            lfg.GenerateMapTest();
        }
    }
}