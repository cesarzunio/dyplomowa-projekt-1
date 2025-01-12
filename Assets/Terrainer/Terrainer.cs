using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static FinalizerSaves;

public sealed unsafe class Terrainer : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] Texture2D _fieldsTexture;
    [SerializeField] int2 _landCoverTextureSize;

    [Header("Save paths")]
    [SerializeField] string _savePathLandCoverOriginal;
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Output")]
    //[SerializeField] string _savePathLandCovers;
    //[SerializeField] string _savePathUrbanRatios;
    //[SerializeField] string _savePathLandCoversMap;
    //[SerializeField] string _savePathUrbanRatiosMap;

    [Header("Output maps")]
    [SerializeField] string _savePathLandCover;
    [SerializeField] string _savePathLandCoverFinal;
    [SerializeField] string _savePathHeat;
    [SerializeField] string _savePathMoisture;
    [SerializeField] string _savePathCultivation;
    [SerializeField] string _savePathVegetation;
    [SerializeField] string _savePathGlaciation;
    [SerializeField] string _savePathDesertification;
    [SerializeField] string _savePathBuildings;

    public void Generate2()
    {
        var landCoverTextureOriginal = LandCoverUtility.LoadLandCoverTextureOriginal(_savePathLandCoverOriginal, _landCoverTextureSize, ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var fieldsNodesIndexes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);
        var nodes = NodesSaveUtility2.LoadNodes(_savePathNodes, ALLOCATOR);
        var edges = NodesSaveUtility2.LoadEdges(_savePathNodeEdges, ALLOCATOR);

        LandCoverUtility.CreateLandCovers(landCoverTextureOriginal, fieldsMap, fields, nodes, edges, fieldsNodesIndexes, ALLOCATOR, out var fieldToLandCoverParams, out var fieldToLandCover);

        TextureSaver.Save(fieldsMap.TextureSize, _savePathLandCover, (i) =>
        {
            uint field = fieldsMap.Fields[i];
            var landCover = fieldToLandCover[field];

            if (landCover == LandCoverUtility.LandCoverOriginal.None)
                return default;

            var b = (byte)((byte)landCover * 10);
            return new Color32(b, b, b, 255);
        });

        TextureSaver.Save(fieldsMap.TextureSize, _savePathLandCoverFinal, (i) =>
        {
            uint field = fieldsMap.Fields[i];

            if (!fields.IsLand[field])
                return default;

            var landCoverFinal = LandCoverUtility.MapToFinal(fieldToLandCoverParams[field]);
            //return LandCoverUtility.LandCoverFinalToColor(landCoverFinal);
            var b = (byte)((byte)landCoverFinal * 10);
            return new Color32(b, b, b, 255);
        });

        LandCoverUtility.SaveLandCoverParams(_savePathHeat, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.HeatCount > 0, p.Heat / p.HeatCount));
        LandCoverUtility.SaveLandCoverParams(_savePathMoisture, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.MoistureCount > 0, p.Moisture / p.MoistureCount));
        LandCoverUtility.SaveLandCoverParams(_savePathCultivation, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Cultivation / p.GeneralCount));
        LandCoverUtility.SaveLandCoverParams(_savePathVegetation, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.VegetationCount > 0, p.Vegetation / p.VegetationCount));
        LandCoverUtility.SaveLandCoverParams(_savePathGlaciation, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Glaciation / p.GeneralCount));
        LandCoverUtility.SaveLandCoverParams(_savePathDesertification, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Desertification / p.GeneralCount));
        LandCoverUtility.SaveLandCoverParams(_savePathBuildings, fields, fieldsMap, fieldToLandCoverParams, (p) => (p.GeneralCount > 0, p.Buildings / (float)p.GeneralCount));

        landCoverTextureOriginal.Dispose();
        fields.Dispose();
        fieldsMap.Dispose();
        fieldToLandCoverParams.Dispose();
        fieldToLandCover.Dispose();
    }

    //public void Generate()
    //{
    //    var landCoverTextureOriginal = TerrainerUtilities4.LoadLandCoverTextureOriginal(_savePathLandCoverOriginal, _landCoverTextureSize, ALLOCATOR);
    //    var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
    //    var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
    //    var nodes = NodesSaveUtility2.LoadNodes(_savePathNodes, ALLOCATOR);
    //    var nodeEdges = NodesSaveUtility2.LoadEdges(_savePathNodeEdges, ALLOCATOR);
    //    var fieldsNodes = BinarySaveUtility.ReadRawArray<uint>(_savePathFieldsNodes, ALLOCATOR);

    //    TerrainerUtilities4.CreateLandCovers(landCoverTextureOriginal, fieldsMap, fields, fieldsNodes, nodes, nodeEdges, ALLOCATOR, out var fieldToLandCoverFinal, out var fieldToUrbanRatio);
    //    TerrainerUtilities4.SaveLandCovers(_savePathLandCovers, _savePathUrbanRatios, fieldToLandCoverFinal, fieldToUrbanRatio);

    //    SaveTextures(fieldsMap, fieldToLandCoverFinal, fieldToUrbanRatio);

    //    landCoverTextureOriginal.Dispose();
    //    fields.Dispose();
    //    fieldsMap.Dispose();
    //    nodes.DisposeDepth1();
    //    nodeEdges.Dispose();
    //    fieldsNodes.Dispose();
    //    fieldToLandCoverFinal.Dispose();
    //    fieldToUrbanRatio.Dispose();
    //}

    //void SaveTextures(FieldsMap fieldsMap, RawArray<LandCoverOriginal> fieldToLandCoverFinal, RawArray<uint2> fieldToUrbanRatio)
    //{
    //    var randomColors = CesColorUtilities.CreateRandomColors(TerrainerUtilities4.LAND_COVERS_COUNT);

    //    TextureSaver.Save(fieldsMap.TextureSize, _savePathLandCoversMap, (i) =>
    //    {
    //        uint field = fieldsMap.Fields[i];
    //        byte b = (byte)((byte)fieldToLandCoverFinal[field] * 10);
    //        return new Color32(b, b, b, 255);
    //    });

    //    TextureSaver.Save(fieldsMap.TextureSize, _savePathUrbanRatiosMap, (i) =>
    //    {
    //        uint field = fieldsMap.Fields[i];
    //        var ratio = fieldToUrbanRatio[field];
    //        float value = ((float)ratio.x) / ratio.y;
    //        byte b = (byte)(value * 255);
    //        return new Color32(b, b, b, 255);
    //    });
    //}
}

[CustomEditor(typeof(Terrainer))]
public sealed class TerrainerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var terrainer = (Terrainer)target;
            terrainer.Generate2();
        }
    }
}
