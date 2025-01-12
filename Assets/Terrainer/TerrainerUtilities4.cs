using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static FinalizerSaves;
using static NodesSaveUtility2;

public static unsafe class TerrainerUtilities4
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    public const int LAND_COVERS_COUNT = 21;

    public static void SaveLandCovers(string pathLandCovers, string pathUrbanRatio, RawArray<LandCoverOriginal> fieldToLandCoverFinal, RawArray<uint2> fieldToUrbanRatio)
    {
        BinarySaveUtility.WriteRawContainerSimple<RawArray<LandCoverOriginal>, LandCoverOriginal>(pathLandCovers, fieldToLandCoverFinal);
        BinarySaveUtility.WriteRawContainerSimple<RawArray<uint2>, uint2>(pathUrbanRatio, fieldToUrbanRatio);
    }

    public static LandCoverTextureOriginal LoadLandCoverTextureOriginal(string path, int2 textureSize, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        var landCover = BinarySaveUtility.ReadArraySimple<LandCoverOriginal>(fileStream, textureSize.x * textureSize.y, allocator);

        return new LandCoverTextureOriginal(landCover, textureSize, allocator);
    }

    public static void CreateLandCovers(
        LandCoverTextureOriginal landCoverTextureOriginal, FieldsMap fieldsMap, Fields fields,
        RawArray<uint> fieldsNodesIndexes, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, Allocator allocator,
        out RawArray<LandCoverOriginal> fieldToLandCoverFinal, out RawArray<uint2> fieldToUrbanRatio)
    {
        var fieldToLandCovers = GetFieldToLandCovers(landCoverTextureOriginal, fieldsMap, fields);
        fieldToLandCoverFinal = GetFieldToLandCoverFinal(fieldToLandCovers, allocator);

        ExtendLandCovers(fieldToLandCoverFinal, fieldsNodesIndexes, nodes, edges);

        fieldToUrbanRatio = GetFieldToUrbanRatio(fields, fieldToLandCovers, allocator);
    }

    static FieldLandCovers[] GetFieldToLandCovers(LandCoverTextureOriginal landCoverTextureOriginal, FieldsMap fieldsMap, Fields fields)
    {
        var fieldToLandCovers = new FieldLandCovers[fields.Length];

        for (int y = 0; y < landCoverTextureOriginal.TextureSize.y; y++)
        {
            for (int x = 0; x < landCoverTextureOriginal.TextureSize.x; x++)
            {
                var originalPixelCoord = new int2(x, y);

                var originalPixelCoordFlipped = TexUtilities.FlipY(originalPixelCoord, landCoverTextureOriginal.TextureSize.y);
                int originalFlat = TexUtilities.PixelCoordToFlat(originalPixelCoordFlipped, landCoverTextureOriginal.TextureSize.x);
                var landCover = landCoverTextureOriginal.LandCover[originalFlat];

                var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(originalPixelCoord, landCoverTextureOriginal.TextureSize);

                var fieldsPixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, fieldsMap.TextureSize);
                int fieldsFlat = TexUtilities.PixelCoordToFlat(fieldsPixelCoord, fieldsMap.TextureSize.x);

                uint field = fieldsMap.Fields[fieldsFlat];

                fieldToLandCovers[field].Add(landCover);
            }
        }

        return fieldToLandCovers;
    }

    static RawArray<LandCoverOriginal> GetFieldToLandCoverFinal(FieldLandCovers[] fieldToLandCovers, Allocator allocator)
    {
        var fieldToLandCoverFinal = new RawArray<LandCoverOriginal>(allocator, fieldToLandCovers.Length);

        for (int i = 0; i < fieldToLandCoverFinal.Length; i++)
        {
            fieldToLandCoverFinal[i] = fieldToLandCovers[i].GetCommonest();
        }

        return fieldToLandCoverFinal;
    }

    static void ExtendLandCovers(RawArray<LandCoverOriginal> fieldToLandCoverFinal, RawArray<uint> fieldsNodesIndexes, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges)
    {
        var landCoverChanges = new LandCoverOriginal[fieldToLandCoverFinal.Length];

        while (ExtendLandCoversIterate(fieldToLandCoverFinal, fieldsNodesIndexes, nodes, edges, landCoverChanges)) { }
    }

    static bool ExtendLandCoversIterate(RawArray<LandCoverOriginal> fieldToLandCoverFinal, RawArray<uint> fieldsNodesIndexes, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, LandCoverOriginal[] landCoverChanges)
    {
        for (int i = 0; i < fieldToLandCoverFinal.Length; i++)
        {
            if (fieldToLandCoverFinal[i] != LandCoverOriginal.None)
                continue;

            landCoverChanges[i] = GetCommonestNeighborLandCover((uint)i, fieldToLandCoverFinal, fieldsNodesIndexes, nodes, edges);
        }

        int changes = 0;

        for (int i = 0; i < fieldToLandCoverFinal.Length; i++)
        {
            if (fieldToLandCoverFinal[i] != LandCoverOriginal.None || landCoverChanges[i] == LandCoverOriginal.None)
                continue;

            fieldToLandCoverFinal[i] = landCoverChanges[i];
            changes++;
        }

        return changes != 0;
    }

    static LandCoverOriginal GetCommonestNeighborLandCover(
        uint field, RawArray<LandCoverOriginal> fieldToLandCoverFinal, RawArray<uint> fieldsNodesIndexes,
        RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges)
    {
        var neighborClimateToCount = stackalloc int[LAND_COVERS_COUNT];

        for (int i = 0; i < LAND_COVERS_COUNT; i++)
        {
            neighborClimateToCount[i] = 0;
        }

        uint nodeIndex = fieldsNodesIndexes[field];
        var edgesIndexes = nodes[nodeIndex].Edges;

        for (int i = 0; i < edgesIndexes.Length; i++)
        {
            var edge = edges[edgesIndexes[i]];
            uint otherNodeIndex = edge.GetOtherNodeIndex(nodeIndex);
            var otherNodeOwner = nodes[otherNodeIndex].Owner;

            if (otherNodeOwner.Type == NodeOwnerType.River)
                continue;

            LandCoverOriginal climate = default;
            //var climate = fieldToLandCoverFinal[owner.Index];

            try
            {
                climate = fieldToLandCoverFinal[otherNodeOwner.Index];
            }
            catch (Exception e)
            {
                Debug.LogError($"Null? {fieldToLandCoverFinal.Length} / {otherNodeOwner.Index} / {otherNodeOwner.IndexSecondary}");
                throw e;
            }

            if (climate == LandCoverOriginal.None)
                continue;

            neighborClimateToCount[(uint)climate]++;
        }

        int countHighestIndex = -1;
        int countHighest = int.MinValue;

        for (int i = 0; i < LAND_COVERS_COUNT; i++)
        {
            if (countHighest < neighborClimateToCount[i])
            {
                countHighest = neighborClimateToCount[i];
                countHighestIndex = i;
            }
        }

        return countHighestIndex == -1 ? LandCoverOriginal.None : (LandCoverOriginal)countHighestIndex;
    }

    static RawArray<uint2> GetFieldToUrbanRatio(Fields fields, FieldLandCovers[] fieldToLandCover, Allocator allocator)
    {
        var fieldToUrbanRatio = new RawArray<uint2>(allocator, fieldToLandCover.Length);

        for (int i = 0; i < fieldToUrbanRatio.Length; i++)
        {
            uint urbanCount = fields.IsLand[i] ? fieldToLandCover[i].UrbanCount : 0;
            uint totalCount = fieldToLandCover[i].TotalCount;

            fieldToUrbanRatio[i] = new uint2(urbanCount, totalCount);
        }

        return fieldToUrbanRatio;
    }

    unsafe struct FieldLandCovers
    {
        fixed uint _landCoverToCount[LAND_COVERS_COUNT];
        uint _urbanCount;
        uint _totalCount;

        public readonly uint UrbanCount => _urbanCount;
        public readonly uint TotalCount => _totalCount;

        public void Add(LandCoverOriginal landCover)
        {
            switch (landCover)
            {
                case LandCoverOriginal.None:
                case LandCoverOriginal.WaterBodies:
                    return;

                case LandCoverOriginal.Urban:
                    _urbanCount++;
                    break;

                default:
                    _landCoverToCount[(byte)landCover]++;
                    break;
            }

            _totalCount++;
        }

        public readonly LandCoverOriginal GetCommonest()
        {
            var landCoverCommonest = LandCoverOriginal.None;
            uint countHighest = 0;

            for (int i = 0; i < LAND_COVERS_COUNT; i++)
            {
                if (countHighest < _landCoverToCount[i])
                {
                    countHighest = _landCoverToCount[i];
                    landCoverCommonest = (LandCoverOriginal)i;
                }
            }

            return landCoverCommonest;
        }
    }

    //static LandCoverMerged MapOriginalToMerged(LandCoverOriginal landCover) => landCover switch
    //{
    //    LandCoverOriginal.None => LandCoverMerged.None,
    //    LandCoverOriginal.WaterBodies => LandCoverMerged.None,
    //    LandCoverOriginal.BroadleafEvergreenForest => LandCoverMerged.Forest,
    //    LandCoverOriginal.BroadleafDeciduousForest => LandCoverMerged.Forest,
    //    LandCoverOriginal.NeedleleafEvergreenForest => LandCoverMerged.Forest,
    //    LandCoverOriginal.NeedleleafDeciduousForest => LandCoverMerged.Forest,
    //    LandCoverOriginal.MixedForest => LandCoverMerged.Forest,
    //    LandCoverOriginal.TreeOpen => LandCoverMerged.ForestSparse,
    //    LandCoverOriginal.Mangrove => LandCoverMerged.Mangrove,
    //    LandCoverOriginal.Shrub => LandCoverMerged.Shrub,
    //    LandCoverOriginal.Herbaceous => LandCoverMerged.Herbaceous,
    //    LandCoverOriginal.HerbaceousWithSparseTreeShrub => LandCoverMerged.Herbaceous,
    //    LandCoverOriginal.SparseVegetation => LandCoverMerged.SparseVegetation,
    //    LandCoverOriginal.Cropland => LandCoverMerged.Cropland,
    //    LandCoverOriginal.CroplandOtherVegetationMosaic => LandCoverMerged.Cropland,
    //    LandCoverOriginal.PaddyField => LandCoverMerged.PaddyField,
    //    LandCoverOriginal.Wetland => LandCoverMerged.Wetland,
    //    LandCoverOriginal.GravelOrRock => LandCoverMerged.Rock,
    //    LandCoverOriginal.Sand => LandCoverMerged.Sand,
    //    LandCoverOriginal.Urban => LandCoverMerged.Urban,
    //    LandCoverOriginal.SnowIce => LandCoverMerged.Ice,

    //    _ => throw new Exception($"Cannot match LandCoverOriginal: {landCover}")
    //};
}

public readonly unsafe struct LandCoverTextureOriginal
{
    public readonly LandCoverOriginal* LandCover;
    public readonly int2 TextureSize;
    readonly Allocator _allocator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LandCoverTextureOriginal(LandCoverOriginal* landCover, int2 textureSize, Allocator allocator)
    {
        LandCover = landCover;
        TextureSize = textureSize;
        _allocator = allocator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        UnsafeUtility.Free(LandCover, _allocator);
    }
}

public enum LandCoverOriginal : byte
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

//public enum LandCoverMerged : byte
//{
//    None, // 0, 20

//    Forest, // 1, 2, 3, 4, 5
//    ForestSparse, // 6
//    Mangrove, // 14

//    Shrub, // 7
//    Herbaceous, // 8, 9
//    SparseVegetation, // 10

//    Cropland, // 11, 13
//    PaddyField, // 12

//    Wetland, // 15,
//    Rock, // 16,
//    Sand, // 17,
//    Urban, // 18,
//    Ice, // 19,
//}