using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static FinalizerSaves;
using static NodesSaveUtility4;

public static unsafe class LandCoverUtility2
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    const int LAND_COVERS_COUNT = 21;

    const float ZERO = 0f;
    const float LOW = 0.25f;
    const float MEDIUM = 0.5f;
    const float HIGH = 0.75f;
    const float FULL = 1f;

    public static void SaveLandCovers(string path, Fields fields, RawArray<LandCoverParams> fieldToLandCoverParams)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(fieldToLandCoverParams.Length);

        for (int i = 0; i < fieldToLandCoverParams.Length; i++)
        {
            GetLandCoverValues(
                fields.IsLand[i], ref fieldToLandCoverParams[i],
                out float wetness, out float vegetation, out float cultivation, out float glaciation, out float desertification, out float buildings);

            fileStream.WriteValue(wetness);
            fileStream.WriteValue(vegetation);
            fileStream.WriteValue(cultivation);
            fileStream.WriteValue(glaciation);
            fileStream.WriteValue(desertification);
            fileStream.WriteValue(buildings);
        }
    }

    static void GetLandCoverValues(
        bool isLand, ref LandCoverParams landCoverParams,
        out float wetness, out float vegetation, out float cultivation, out float glaciation, out float desertification, out float buildings)
    {
        if (!isLand)
        {
            wetness = 1f;
            vegetation = 0f;
            cultivation = 0f;
            glaciation = 0f;
            desertification = 0f;
            buildings = 0f;
            return;
        }

        if (landCoverParams.GeneralCount == 0)
            throw new Exception("LandCoverUtility2 :: GetLandCoverValues :: Field is neither land nor defined, wtf?");

        wetness = landCoverParams.Wetness / landCoverParams.GeneralCount;
        vegetation = landCoverParams.VegetationCount > 0 ? landCoverParams.Vegetation / landCoverParams.VegetationCount : 0f;
        cultivation = landCoverParams.Cultivation / landCoverParams.GeneralCount;
        glaciation = landCoverParams.Glaciation / landCoverParams.GeneralCount;
        desertification = landCoverParams.Desertification / landCoverParams.GeneralCount;
        buildings = landCoverParams.Buildings / (float)landCoverParams.GeneralCount;
    }

    public static LandCoverTextureOriginal LoadLandCoverTextureOriginal(string path, int2 textureSize, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        var array = BinarySaveUtility.ReadArraySimple<LandCoverOriginal>(fileStream, textureSize.x * textureSize.y, allocator);

        for (int y = 0; y < textureSize.y / 2; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                int flatBot = TexUtilities.PixelCoordToFlat(x, y, textureSize.x);
                int flatTop = TexUtilities.PixelCoordToFlat(x, textureSize.y - y - 1, textureSize.x);

                (array[flatBot], array[flatTop]) = (array[flatTop], array[flatBot]);
            }
        }

        return new LandCoverTextureOriginal(textureSize, array, allocator);
    }

    public static void SaveLandCoverParams(
        string path, Fields fields, FieldsMap fieldsMap, RawArray<LandCoverParams> fieldToLandCoverParams,
        Func<LandCoverParams, (bool Defined, float Value)> landCoverParamsToValue)
    {
        TextureSaver.Save(fieldsMap.TextureSize, path, (i) =>
        {
            uint field = fieldsMap.Fields[i];

            if (!fields.IsLand[field])
                return default;

            var landCoverParams = fieldToLandCoverParams[field];
            (bool defined, float value) = landCoverParamsToValue(landCoverParams);

            if (!defined)
                return new Color32(255, 0, 0, 255);

            byte b = CesColorUtilities.Float01ToByte(value);
            return new Color32(b, b, b, 255);
        });
    }

    public static RawArray<LandCoverParams> CreateLandCovers(
        LandCoverTextureOriginal landCoverTextureOriginal, FieldsMap fieldsMap, Fields fields,
        NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes, Allocator allocator)
    {
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);
        var fieldToLandCoverToCount = new RawArray<LandCoverToCount>(ALLOCATOR, fields.Length);

        var job = new LandCoverJob2
        {
            Fields = fields,
            FieldsMap = fieldsMap,
            LandCoverTextureOriginal = landCoverTextureOriginal,
            FieldToPixelCoords = fieldToPixelCoords,
            FieldToLandCoverToCount = fieldToLandCoverToCount,
        };

        job.Schedule(fieldToLandCoverToCount.Length, 64).Complete();

        var fieldToLandCoverParams = GetFieldToLandCoverParams(fields, fieldToLandCoverToCount, allocator);

        ExtendLandCoverParams(fields, fieldToLandCoverParams, nodes, edges, fieldsNodesIndexes);

        fieldToPixelCoords.DisposeDepth1();
        fieldToLandCoverToCount.Dispose();

        return fieldToLandCoverParams;
    }

    static RawArray<LandCoverParams> GetFieldToLandCoverParams(Fields fields, RawArray<LandCoverToCount> fieldToLandCoverToCount, Allocator allocator)
    {
        var fieldToLandCoverParams = new RawArray<LandCoverParams>(allocator, fields.Length);

        for (int i = 0; i < fieldToLandCoverParams.Length; i++)
        {
            fieldToLandCoverParams[i] = GetLandCoverParams(i, fields, fieldToLandCoverToCount);
        }

        return fieldToLandCoverParams;
    }

    static LandCoverParams GetLandCoverParams(int fieldIndex, Fields fields, RawArray<LandCoverToCount> fieldToLandCoverToCount)
    {
        if (!fields.IsLand[fieldIndex])
            return new LandCoverParams();

        ref var landCoverToCount = ref fieldToLandCoverToCount[fieldIndex];
        var landCoverParams = new LandCoverParams();

        for (int i = 1; i < LAND_COVERS_COUNT; i++)
        {
            for (int j = 0; j < landCoverToCount.Counts[i]; j++)
            {
                AssignParams((LandCover)(byte)i, ref landCoverParams);
            }
        }

        return landCoverParams;
    }

    static void AssignParams(LandCover landCover, ref LandCoverParams landCoverParams)
    {
        if (landCover == LandCover.None || landCover == LandCover.WaterBodies)
            return;

        landCoverParams.GeneralCount++;
        landCoverParams.VegetationCount++;

        switch (landCover)
        {
            case LandCover.BroadleafEvergreenForest:
                landCoverParams.Vegetation += FULL;
                break;
            case LandCover.BroadleafDeciduousForest:
                landCoverParams.Vegetation += FULL;
                break;
            case LandCover.NeedleleafEvergreenForest:
                landCoverParams.Vegetation += FULL;
                break;
            case LandCover.NeedleleafDeciduousForest:
                landCoverParams.Vegetation += FULL;
                break;
            case LandCover.MixedForest:
                landCoverParams.Vegetation += FULL;
                break;
            case LandCover.TreeOpen:
                landCoverParams.Vegetation += HIGH;
                break;
            case LandCover.Shrub:
                landCoverParams.Vegetation += MEDIUM;
                break;
            case LandCover.Herbaceous:
                landCoverParams.Vegetation += MEDIUM;
                break;
            case LandCover.HerbaceousWithSparseTreeShrub:
                landCoverParams.Vegetation += HIGH;
                break;
            case LandCover.SparseVegetation:
                landCoverParams.Vegetation += LOW;
                break;
            case LandCover.Cropland:
                landCoverParams.Vegetation += MEDIUM;
                landCoverParams.Cultivation += FULL;
                break;
            case LandCover.PaddyField:
                landCoverParams.Vegetation += MEDIUM;
                landCoverParams.Cultivation += FULL;
                break;
            case LandCover.CroplandOtherVegetationMosaic:
                landCoverParams.Vegetation += HIGH;
                landCoverParams.Cultivation += HIGH;
                break;
            case LandCover.Mangrove:
                landCoverParams.Vegetation += FULL;
                break;
            case LandCover.Wetland:
                landCoverParams.VegetationCount--;
                landCoverParams.Wetness += HIGH;
                break;
            case LandCover.GravelOrRock:
                landCoverParams.Vegetation += ZERO;
                break;
            case LandCover.Sand:
                landCoverParams.Vegetation += ZERO;
                landCoverParams.Desertification += FULL;
                break;
            case LandCover.Urban:
                landCoverParams.Vegetation += ZERO;
                landCoverParams.Buildings++;
                break;
            case LandCover.SnowIce:
                landCoverParams.Vegetation += ZERO;
                landCoverParams.Glaciation += FULL;
                break;
        }
    }

    static void ExtendLandCoverParams(Fields fields, RawArray<LandCoverParams> fieldsTemperatures, NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes)
    {
        var fieldsTemperaturesChanges = new RawArray<LandCoverParams>(ALLOCATOR, default, fieldsTemperatures.Length);

        int iterationsGeneral = 0;
        int iterationsVegetation = 0;

        while (ExtendLandCoverParamsIterate(fields, fieldsTemperatures, fieldsTemperaturesChanges, nodes, edges, fieldsNodesIndexes, &LandCoverParamsToCountGeneral, ref iterationsGeneral)) { }

        fieldsTemperaturesChanges.Set(default);

        while (ExtendLandCoverParamsIterate(fields, fieldsTemperatures, fieldsTemperaturesChanges, nodes, edges, fieldsNodesIndexes, &LandCoverParamsToCountVegetation, ref iterationsGeneral)) { }

        Debug.Log($"LandCoverUtility :: ExtendLandCoverParams :: IterationsGeneral: {iterationsGeneral}, IterationsVegetation: {iterationsVegetation}");

        fieldsTemperaturesChanges.Dispose();
    }

    static bool ExtendLandCoverParamsIterate(
        Fields fields, RawArray<LandCoverParams> fieldToLandCoverParams, RawArray<LandCoverParams> fieldToLandCoverParamsChanges,
        NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes, delegate*<LandCoverParams, int> landCoverParamsToCountFunc, ref int iterations)
    {
        for (int i = 0; i < fieldToLandCoverParams.Length; i++)
        {
            if (landCoverParamsToCountFunc(fieldToLandCoverParams[i]) > 0)
                continue;

            var neighborsLandCoverParams = GetNeighborsLandCoverParams(i, fieldToLandCoverParams, nodes, edges, fieldsNodesIndexes, landCoverParamsToCountFunc);

            if (neighborsLandCoverParams.GeneralCount > 0)
            {
                fieldToLandCoverParamsChanges[i] = neighborsLandCoverParams;
            }
        }

        for (int i = 0; i < fieldToLandCoverParams.Length; i++)
        {
            if (landCoverParamsToCountFunc(fieldToLandCoverParams[i]) == 0 && landCoverParamsToCountFunc(fieldToLandCoverParamsChanges[i]) > 0)
            {
                fieldToLandCoverParams[i] += fieldToLandCoverParamsChanges[i];
            }
        }

        iterations++;

        return IsAnythingNotSet();

        // ---

        bool IsAnythingNotSet()
        {
            for (int i = 0; i < fieldToLandCoverParams.Length; i++)
            {
                if (fields.IsLand[i] && landCoverParamsToCountFunc(fieldToLandCoverParams[i]) == 0)
                    return true;
            }

            return false;
        }
    }

    static LandCoverParams GetNeighborsLandCoverParams(
        int fieldIndex, RawArray<LandCoverParams> fieldToLandCoverParams, NodesFinals nodes, EdgesFinals edges,
        RawArray<uint> fieldsNodesIndexes, delegate*<LandCoverParams, int> landCoverParamsToCountFunc)
    {
        var landCoverParams = new LandCoverParams();

        uint nodeIndex = fieldsNodesIndexes[fieldIndex];
        ref readonly var nodeEdges = ref nodes.EdgesIndexes[nodeIndex];

        for (int i = 0; i < nodeEdges.Length; i++)
        {
            ref readonly var edgeNodeIndexes = ref edges.NodesIndexes[nodeEdges[i]];
            uint nodeIndexOther = edgeNodeIndexes.x ^ edgeNodeIndexes.y ^ nodeIndex;
            ref readonly var nodeOtherOwner = ref nodes.Owner[nodeIndexOther];

            if (nodeOtherOwner.Type == NodeOwnerType.River)
                continue;

            uint fieldIndexOther = nodeOtherOwner.Index;

            if (landCoverParamsToCountFunc(fieldToLandCoverParams[fieldIndexOther]) == 0)
                continue;

            landCoverParams += fieldToLandCoverParams[fieldIndexOther];
        }

        return landCoverParams;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int LandCoverParamsToCountGeneral(LandCoverParams landCoverParams) => landCoverParams.GeneralCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int LandCoverParamsToCountVegetation(LandCoverParams landCoverParams) => landCoverParams.VegetationCount;

    enum LandCover : byte
    {
        None = 0,
        BroadleafEvergreenForest = 1,
        BroadleafDeciduousForest = 2,
        NeedleleafEvergreenForest = 3,
        NeedleleafDeciduousForest = 4,
        MixedForest = 5,
        TreeOpen = 6,
        Shrub = 7,
        Herbaceous = 8,
        HerbaceousWithSparseTreeShrub = 9,
        SparseVegetation = 10,
        Cropland = 11,
        PaddyField = 12,
        CroplandOtherVegetationMosaic = 13,
        Mangrove = 14,
        Wetland = 15,
        GravelOrRock = 16,
        Sand = 17,
        Urban = 18,
        SnowIce = 19,
        WaterBodies = 20
    }

    public readonly unsafe struct LandCoverTextureOriginal
    {
        public readonly int2 TextureSize;

        [NativeDisableUnsafePtrRestriction]
        public readonly LandCoverOriginal* Array;

        readonly Allocator _allocator;

        public LandCoverTextureOriginal(int2 textureSize, LandCoverOriginal* array, Allocator allocator)
        {
            Array = array;
            TextureSize = textureSize;
            _allocator = allocator;
        }

        public void Dispose()
        {
            UnsafeUtility.Free(Array, _allocator);
        }
    }

    public unsafe struct LandCoverToCount
    {
        [NativeDisableUnsafePtrRestriction]
        public fixed int Counts[LAND_COVERS_COUNT];
    }

    public struct LandCoverParams
    {
        public int GeneralCount;
        public int VegetationCount;

        public float Wetness;
        public float Vegetation;
        public float Cultivation;
        public float Glaciation;
        public float Desertification;
        public int Buildings;

        public static LandCoverParams operator +(LandCoverParams lhs, LandCoverParams rhs) => new LandCoverParams
        {
            GeneralCount = lhs.GeneralCount + rhs.GeneralCount,
            VegetationCount = lhs.VegetationCount + rhs.VegetationCount,
            Wetness = lhs.Wetness + rhs.Wetness,
            Vegetation = lhs.Vegetation + rhs.Vegetation,
            Cultivation = lhs.Cultivation + rhs.Cultivation,
            Glaciation = lhs.Glaciation + rhs.Glaciation,
            Desertification = lhs.Desertification + rhs.Desertification,
            Buildings = lhs.Buildings,
        };
    }
}
