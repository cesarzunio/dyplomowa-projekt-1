using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public sealed unsafe class AreasGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] Texture2D _areasTex;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;

    [Header("Output")]
    [SerializeField] string _savePathAreasTexture;
    [SerializeField] string _savePathAreas;

    public void GenerateTexture()
    {
        var areasMap = _areasTex.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);

        var fieldToArea = AreasGeneratorUtility.CreateAreas(areasMap, fieldsMap, fields, ALLOCATOR);

        TextureSaver.Save(fieldsMap.TextureSize, _savePathAreasTexture, (i) =>
        {
            uint field = fieldsMap.Fields[i];
            return fieldToArea[field].ToColor32();
        });

        areasMap.Dispose();
        fieldsMap.Dispose();
        fields.Dispose();
        fieldToArea.Dispose();
    }

    public void GenerateSave()
    {
        var areasMap = _areasTex.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);

        AreasGeneratorUtility.SaveAreas(_savePathAreas, areasMap, fieldsMap, fields);

        areasMap.Dispose();
        fieldsMap.Dispose();
        fields.Dispose();
    }
}

[CustomEditor(typeof(AreasGenerator))]
public sealed class AreasGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate texture"))
        {
            var ag = (AreasGenerator)target;
            ag.GenerateTexture();
        }

        if (GUILayout.Button("Generate save"))
        {
            var ag = (AreasGenerator)target;
            ag.GenerateSave();
        }
    }
}
