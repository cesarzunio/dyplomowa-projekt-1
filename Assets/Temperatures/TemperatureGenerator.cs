using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed unsafe class TemperatureGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathTemperatureOriginal;
    [SerializeField] int2 _temperatureTextureOriginalSize;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Output")]
    [SerializeField] bool _createTextures;
    [SerializeField] string _savePathFieldsTemperatures;
    [SerializeField] string _savePathFieldsTemperaturesMapPrefix;

    public void Generate()
    {
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var nodes = NodesSaveUtility4.LoadNodes(_savePathNodes, ALLOCATOR);
        var edges = NodesSaveUtility4.LoadEdges(_savePathNodeEdges, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);

        var fieldsTemperatures = TemperatureUtility.GenerateFieldTemperatures(fields, fieldsMap, _savePathTemperatureOriginal, _temperatureTextureOriginalSize, nodes, edges, fieldsNodesIndexes, ALLOCATOR);

        TemperatureUtility.SaveFieldTemperatures(_savePathFieldsTemperatures, fieldsTemperatures);

        if (_createTextures)
        {
            for (int i = 0; i < 12; i++)
            {
                int noSetCount = 0;

                var minMax = GetMinMax(fieldsTemperatures, i);

                TextureSaver.Save(fieldsMap.TextureSize, $"{_savePathFieldsTemperaturesMapPrefix}{i + 1}.png", (j) =>
                {
                    uint field = fieldsMap.Fields[j];

                    if (!fieldsTemperatures[field].MonthToSet[i])
                    {
                        noSetCount++;
                        return default;
                    }

                    float value = math.unlerp(minMax.x, minMax.y, fieldsTemperatures[field].MonthToTemperature[i]);
                    byte b = CesColorUtilities.Float01ToByte(value);

                    return new Color32(b, b, b, 255);
                });

                Debug.Log($"Noset {i}: {noSetCount}, set: {(fieldsMap.TextureSize.x * fieldsMap.TextureSize.y) - noSetCount}");
            }
        }

        fields.Dispose();
        fieldsMap.Dispose();
        fieldsTemperatures.Dispose();
        nodes.Dispose();
        edges.Dispose();
        fieldsNodesIndexes.Dispose();
    }

    static float2 GetMinMax(RawArray<TemperatureUtility.FieldTemperatures> fieldsTemperatures, int monthIndex)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < fieldsTemperatures.Length; i++)
        {
            if (!fieldsTemperatures[i].MonthToSet[monthIndex])
                continue;

            min = math.min(min, fieldsTemperatures[i].MonthToTemperature[monthIndex]);
            max = math.max(max, fieldsTemperatures[i].MonthToTemperature[monthIndex]);
        }

        return new float2(min, max);
    }
}

[CustomEditor(typeof(TemperatureGenerator))]
public sealed class TemperatureGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var sg = (TemperatureGenerator)target;
            sg.Generate();
        }
    }
}