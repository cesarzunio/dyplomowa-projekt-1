using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed unsafe class SoilsGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathSoilsOriginal;
    [SerializeField] int2 _soilsTextureOriginalSize;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;

    [Header("Output")]
    [SerializeField] string _savePathFieldsSoils;
    [SerializeField] TextureCreationMode _saveMode;
    [SerializeField] string _savePathFieldsSoilsMap;

    public void Generate()
    {
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var soilsTextureOriginal = SoilsUtility.LoadSoilsTextureOriginal(_savePathSoilsOriginal, _soilsTextureOriginalSize, ALLOCATOR);

        var fieldToSoilType = SoilsUtility.GenerateSoilTypes(fields, fieldsMap, soilsTextureOriginal, ALLOCATOR);

        BinarySaveUtility.WriteRawContainerSimple<RawArray<SoilsUtility.SoilType>, SoilsUtility.SoilType>(_savePathFieldsSoils, fieldToSoilType);

        if (_saveMode == TextureCreationMode.Bytes)
        {
            TextureSaver.Save(fieldsMap.TextureSize, _savePathFieldsSoilsMap, (i) =>
            {
                uint field = fieldsMap.Fields[i];
                var soilType = fieldToSoilType[field];

                if (soilType == SoilsUtility.SoilType.None)
                    return default;

                var b = (byte)soilType;
                return new Color32(b, b, b, 255);
            });
        }
        else if (_saveMode == TextureCreationMode.RandomColors)
        {
            var randomColors = CesColorUtilities.CreateRandomColors(50);

            TextureSaver.Save(fieldsMap.TextureSize, _savePathFieldsSoilsMap, (i) =>
            {
                uint field = fieldsMap.Fields[i];
                var soilType = fieldToSoilType[field];

                return soilType == SoilsUtility.SoilType.None ? default : randomColors[(byte)soilType];
            });
        }

        fields.Dispose();
        fieldsMap.Dispose();
        soilsTextureOriginal.Dispose();
        fieldToSoilType.Dispose();
    }
}

[CustomEditor(typeof(SoilsGenerator))]
public sealed class SoilsGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var sg = (SoilsGenerator)target;
            sg.Generate();
        }
    }
}