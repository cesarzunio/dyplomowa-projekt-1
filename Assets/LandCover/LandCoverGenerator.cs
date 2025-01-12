using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed unsafe class LandCoverGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathLandCoverOriginal;
    [SerializeField] int2 _landCoverTextureSize;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Output")]
    [SerializeField] bool _createTextures;
    [SerializeField] string _savePathFieldsLandCoverParams;
    [SerializeField] string _savePathCultivation;
    [SerializeField] string _savePathVegetation;
    [SerializeField] string _savePathGlaciation;
    [SerializeField] string _savePathDesertification;
    [SerializeField] string _savePathBuildings;
    [SerializeField] string _savePathWater;

    public void Generate2()
    {
        var landCoverTextureOriginal = LandCoverUtility2.LoadLandCoverTextureOriginal(_savePathLandCoverOriginal, _landCoverTextureSize, ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);
        var nodes = NodesSaveUtility4.LoadNodes(_savePathNodes, ALLOCATOR);
        var edges = NodesSaveUtility4.LoadEdges(_savePathNodeEdges, ALLOCATOR);

        var fieldToLandCoverParams = LandCoverUtility2.CreateLandCovers(landCoverTextureOriginal, fieldsMap, fields, nodes, edges, fieldsNodesIndexes, ALLOCATOR);

        LandCoverUtility2.SaveLandCovers(_savePathFieldsLandCoverParams, fields, fieldToLandCoverParams);

        if (_createTextures)
        {
            LandCoverUtility2.SaveLandCoverParams(_savePathCultivation, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Cultivation / p.GeneralCount));
            LandCoverUtility2.SaveLandCoverParams(_savePathVegetation, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.VegetationCount > 0, p.Vegetation / p.VegetationCount));
            LandCoverUtility2.SaveLandCoverParams(_savePathGlaciation, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Glaciation / p.GeneralCount));
            LandCoverUtility2.SaveLandCoverParams(_savePathDesertification, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Desertification / p.GeneralCount));
            LandCoverUtility2.SaveLandCoverParams(_savePathBuildings, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Buildings / (float)p.GeneralCount));
            LandCoverUtility2.SaveLandCoverParams(_savePathWater, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Wetness / p.GeneralCount));
        }

        landCoverTextureOriginal.Dispose();
        fields.Dispose();
        fieldsMap.Dispose();
        fieldToLandCoverParams.Dispose();
    }
}

[CustomEditor(typeof(LandCoverGenerator))]
public sealed class LandCoverGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var terrainer = (LandCoverGenerator)target;
            terrainer.Generate2();
        }
    }
}
