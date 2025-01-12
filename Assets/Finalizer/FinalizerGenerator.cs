using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.IO;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class FinalizerGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] Texture2D _regionsTexture;
    [SerializeField] Texture2D _fieldsTexture;
    [SerializeField] Texture2D _riversTexture;
    [SerializeField] int _fieldsCountMax;
    [SerializeField] int _bordersCountMax;

    [Header("Save paths")]
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathBorders;
    [SerializeField] string _savePathRivers;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathRiverPoints;
    [SerializeField] string _savePathFieldsNodes;

    public void Generate()
    {
        var textureSize = new int2(_fieldsTexture.width, _fieldsTexture.height);

        var sw = Stopwatch.StartNew();

        var regionsMap = _regionsTexture.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);
        var riversMap = _riversTexture.GetRawTextureData<Color32>().ColorsToIntsCheckAlpha(ALLOCATOR);
        var fieldsMap = _fieldsTexture.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);

        FinalizerUtilities.MapColorsToIndexes(fieldsMap, _fieldsCountMax, out var colorToIndex, out var indexToColor);
        FinalizerSaves.SaveFieldsMap(_savePathFieldsMap, fieldsMap, textureSize, colorToIndex);

        Debug.Log($"Prepare: {sw.Elapsed.TotalSeconds} s");
        sw.Restart();

        CentersUtilities.GenerateCenters(fieldsMap, riversMap, textureSize, colorToIndex, indexToColor, ALLOCATOR, out var indexToCenterGeoCoords);
        FinalizerSaves.SaveFields(_savePathFields, regionsMap, textureSize, indexToCenterGeoCoords, indexToColor);

        Debug.Log($"Centers: {sw.Elapsed.TotalSeconds} s");
        sw.Restart();

        BordersUtilities.GenerateBorders(fieldsMap, textureSize, _bordersCountMax, ALLOCATOR, out var bordersSorted);
        FinalizerSaves.SaveBorders(_savePathBorders, bordersSorted, colorToIndex);

        Debug.Log($"Borders: {sw.Elapsed.TotalSeconds} s");
        sw.Restart();

        UnwrapperUtilities.CreateRiversData(regionsMap, riversMap, textureSize, ALLOCATOR, out var riversPointTypes, out var riversIndexesMap, out var riversCoords);
        var riversDataFinal = UnwrapperUtilities.CreateRiversDataFinal(riversIndexesMap, riversCoords, textureSize, ALLOCATOR);

        Debug.Log($"Rivers data: {sw.Elapsed.TotalSeconds} s");
        sw.Restart();

        FinalizerUtilities.CreateNeighbors(
            fieldsMap, riversIndexesMap, bordersSorted, indexToCenterGeoCoords, colorToIndex, textureSize, ALLOCATOR,
            out var neighborsTypes, out var neighborsDistances, out var riversCrossPixelCoords);

        Debug.Log($"Neighbors: {sw.Elapsed.TotalSeconds} s");
        sw.Restart();

        NodesUtility4.CreateNodesSave(
            _savePathNodes, _savePathNodeEdges, _savePathRiverPoints, _savePathFieldsNodes, _savePathRivers,
            fieldsMap, riversIndexesMap, bordersSorted, indexToCenterGeoCoords, textureSize, colorToIndex,
            neighborsTypes, neighborsDistances, riversCrossPixelCoords, riversDataFinal);

        Debug.Log($"Nodes: {sw.Elapsed.TotalSeconds} s");
        sw.Restart();

        regionsMap.Dispose();
        fieldsMap.Dispose();
        riversMap.Dispose();
        indexToCenterGeoCoords.Dispose();
        bordersSorted.DisposeDepth1();
        neighborsTypes.Dispose();
        neighborsDistances.Dispose();
        riversCrossPixelCoords.Dispose();
        riversPointTypes.Dispose();
        riversIndexesMap.Dispose();
        riversCoords.DisposeDepth1();
        riversDataFinal.DisposeDepth1();
    }

    public void GenerateOnlyBorders()
    {
        var textureSize = new int2(_fieldsTexture.width, _fieldsTexture.height);

        var sw = Stopwatch.StartNew();

        var fieldsMap = _fieldsTexture.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);

        FinalizerUtilities.MapColorsToIndexes(fieldsMap, _fieldsCountMax, out var colorToIndex, out _);

        BordersUtilities.GenerateBorders(fieldsMap, textureSize, _bordersCountMax, ALLOCATOR, out var bordersSorted);
        FinalizerSaves.SaveBorders(_savePathBorders, bordersSorted, colorToIndex);

        Debug.Log($"Borders: {sw.Elapsed.TotalSeconds} s");

        for (int i = 0; i < bordersSorted.Length; i++)
        {
            int fieldA = colorToIndex[bordersSorted[i].FieldColorA];
            int fieldB = colorToIndex[bordersSorted[i].FieldColorB];

            if ((fieldA == 0 && fieldB == 1) || (fieldA == 1 && fieldB == 0))
            {
                Debug.Log($"All: {bordersSorted[i].BorderCoords.Count}");

                for (int j = 0; j < bordersSorted[i].BorderCoords.Count; j++)
                {
                    Debug.Log($"{j}: {bordersSorted[i].BorderCoords[j].Count}");
                }

                return;
            }
        }

        fieldsMap.Dispose();
        bordersSorted.DisposeDepth1();
    }
}

[CustomEditor(typeof(FinalizerGenerator))]
public sealed class NeighborsGeneratorEditor2 : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var ng = (FinalizerGenerator)target;
            ng.Generate();
        }

        if (GUILayout.Button("Generate only borders"))
        {
            var ng = (FinalizerGenerator)target;
            ng.GenerateOnlyBorders();
        }
    }
}
