using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed unsafe class SurfacesGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;

    [Header("Output")]
    [SerializeField] bool _createTexture;
    [SerializeField] string _savePathFieldsSurfaces;
    [SerializeField] string _savePathFieldsSurfacesMap;

    public void Generate()
    {
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);

        var fieldToSurface = SurfacesUtility.GenerateFieldsSurfaces(fields, fieldsMap, ALLOCATOR);

        SurfacesUtility.SaveFieldsSurfaces(_savePathFieldsSurfaces, fieldToSurface);

        if (_createTexture)
        {
            var minMax = GetMinMax(fieldToSurface);

            Debug.Log($"SurfaceGenerator :: MinMax: {minMax}");

            TextureSaver.Save(fieldsMap.TextureSize, _savePathFieldsSurfacesMap, (i) =>
            {
                uint field = fieldsMap.Fields[i];
                double surface = fieldToSurface[field];
                float unlerp = (float)math.unlerp(minMax.x, minMax.y, surface);
                byte b = CesColorUtilities.Float01ToByte(unlerp);
                return new Color32(b, b, b, 255);
            });
        }

        fields.Dispose();
        fieldsMap.Dispose();
        fieldToSurface.Dispose();
    }

    static double2 GetMinMax(RawArray<double> fieldToSurface)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        for (int i = 0; i < fieldToSurface.Length; i++)
        {
            min = math.min(min, fieldToSurface[i]);
            max = math.max(max, fieldToSurface[i]);
        }

        return new double2(min, max);
    }
}

[CustomEditor(typeof(SurfacesGenerator))]
public sealed class SurfacesGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var sg = (SurfacesGenerator)target;
            sg.Generate();
        }
    }
}