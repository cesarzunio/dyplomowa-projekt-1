using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed unsafe class PrecipitationGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathPrecipitationOriginal;
    [SerializeField] int2 _precipitationTextureOriginalSize;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Output")]
    [SerializeField] bool _createTextures;
    [SerializeField] string _savePathFieldsPrecipitation;
    [SerializeField] string _savePathFieldsPrecipitationMapPrefix;

    public void Generate()
    {
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var nodes = NodesSaveUtility4.LoadNodes(_savePathNodes, ALLOCATOR);
        var edges = NodesSaveUtility4.LoadEdges(_savePathNodeEdges, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);

        var fieldsTemperatures = PrecipitationUtility.GenerateFieldPrecipitations(fields, fieldsMap, _savePathPrecipitationOriginal, _precipitationTextureOriginalSize, nodes, edges, fieldsNodesIndexes, ALLOCATOR);

        PrecipitationUtility.SaveFieldPrecipitations(_savePathFieldsPrecipitation, fieldsTemperatures);

        if (_createTextures)
        {
            for (int i = 0; i < 1; i++)
            {
                int noSetCount = 0;

                var minMax = GetMinMax(fieldsTemperatures, i);

                TextureSaver.Save(fieldsMap.TextureSize, $"{_savePathFieldsPrecipitationMapPrefix}{i + 1}.png", (j) =>
                {
                    uint field = fieldsMap.Fields[j];

                    if (!fieldsTemperatures[field].MonthToSet[i])
                    {
                        noSetCount++;
                        return default;
                    }

                    float value = math.unlerp(minMax.x, minMax.y, fieldsTemperatures[field].MonthToPrecipitation[i]);
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

    static float2 GetMinMax(RawArray<PrecipitationUtility.FieldPrecipitations> fieldsTemperatures, int monthIndex)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < fieldsTemperatures.Length; i++)
        {
            if (!fieldsTemperatures[i].MonthToSet[monthIndex])
                continue;

            min = math.min(min, fieldsTemperatures[i].MonthToPrecipitation[monthIndex]);
            max = math.max(max, fieldsTemperatures[i].MonthToPrecipitation[monthIndex]);
        }

        return new float2(min, max);
    }
}

[CustomEditor(typeof(PrecipitationGenerator))]
public sealed class PrecipitationGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var sg = (PrecipitationGenerator)target;
            sg.Generate();
        }
    }
}

