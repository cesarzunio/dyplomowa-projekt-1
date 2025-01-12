using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using System.IO;
using System;
using static FinalizerSaves;
using static NodesSaveUtility2;
using UnityEditor;

public static unsafe class LandCoverUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    const int LAND_COVERS_COUNT = 21;
    const int PATHFINDER_NODES_INDEXES_COUNT = 512;

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

    public static void CreateLandCovers(
        LandCoverTextureOriginal landCoverTextureOriginal, FieldsMap fieldsMap, Fields fields,
         RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, RawArray<uint> fieldsNodesIndexes, Allocator allocator,
        out RawArray<LandCoverParams> fieldToLandCoverParams, out RawArray<LandCoverOriginal> fieldToLandCover)
    {
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);
        var fieldToLandCoverToCount = new RawArray<LandCoverToCount>(ALLOCATOR, fields.Length);

        var job = new LandCoverJob
        {
            Fields = fields,
            FieldsMap = fieldsMap,
            LandCoverTextureOriginal = landCoverTextureOriginal,
            FieldToPixelCoords = fieldToPixelCoords,
            FieldToLandCoverToCount = fieldToLandCoverToCount,
        };

        job.Schedule(fieldToLandCoverToCount.Length, 64).Complete();

        fieldToLandCoverParams = GetFieldToLandCoverParams(fields, fieldToLandCoverToCount, allocator);
        fieldToLandCover = GetFieldToLandCover(fieldToLandCoverToCount, allocator);

        ExtendLandCoverParams(fields, nodes, edges, fieldsNodesIndexes, fieldToLandCoverParams);

        fieldToPixelCoords.DisposeDepth1();
        fieldToLandCoverToCount.Dispose();
    }

    //static RawArray<LandCoverMerged> GetFieldToLandCoverFinal(RawArray<LandCoverToCount> fieldToLandCoverToCount, Allocator allocator)
    //{
    //    var fieldToLandCoverFinal = new RawArray<LandCoverMerged>(allocator, fieldToLandCoverToCount.Length);

    //    for (int i = 0; i < fieldToLandCoverFinal.Length; i++)
    //    {
    //        fieldToLandCoverFinal[i] = fieldToLandCoverToCount[i].GetCommonest();
    //    }

    //    return fieldToLandCoverFinal;
    //}

    static RawArray<LandCoverOriginal> GetFieldToLandCover(RawArray<LandCoverToCount> fieldToLandCoverToCount, Allocator allocator)
    {
        var fieldToLandCover = new RawArray<LandCoverOriginal>(allocator, fieldToLandCoverToCount.Length);

        for (int i = 0; i < fieldToLandCover.Length; i++)
        {
            fieldToLandCover[i] = fieldToLandCoverToCount[i].GetCommonest();
        }

        return fieldToLandCover;
    }

    static RawArray<LandCoverParams> GetFieldToLandCoverParams(Fields fields, RawArray<LandCoverToCount> fieldToLandCoverToCount, Allocator allocator)
    {
        var fieldToLandCoverParams = new RawArray<LandCoverParams>(allocator, fieldToLandCoverToCount.Length);

        for (int i = 0; i < fieldToLandCoverParams.Length; i++)
        {
            var landCoverParams = new LandCoverParams();
            var landCoverToCount = fieldToLandCoverToCount[i];

            if (!fields.IsLand[i])
            {
                fieldToLandCoverParams[i] = default;
                continue;
            }

            for (int j = 1; j < LAND_COVERS_COUNT; j++)
            {
                var landCover = (LandCoverOriginal)(byte)j;
                landCoverParams += CalculateLandCoverParams(landCover) * landCoverToCount.Counts[j];
            }

            fieldToLandCoverParams[i] = landCoverParams;
        }

        return fieldToLandCoverParams;
    }

    static void ExtendLandCoverParams(Fields fields, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, RawArray<uint> fieldsNodesIndexes, RawArray<LandCoverParams> fieldToLandCoverParams)
    {
        var fieldToLandCoverParamsChanges = new RawArray<LandCoverParams>(ALLOCATOR, fieldToLandCoverParams.Length);

        var neighborsIndexesPtr = stackalloc uint[PATHFINDER_NODES_INDEXES_COUNT];
        var neighborsIndexes = new RawListStackalloc<uint>(neighborsIndexesPtr, PATHFINDER_NODES_INDEXES_COUNT);

        while (ExtendLandCoverParamsIterate(fields, nodes, edges, fieldsNodesIndexes, neighborsIndexes, fieldToLandCoverParams, fieldToLandCoverParamsChanges)) { }

        fieldToLandCoverParamsChanges.Dispose();
    }

    static bool ExtendLandCoverParamsIterate(
        Fields fields, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, RawArray<uint> fieldsNodesIndexes,
        RawListStackalloc<uint> neighborsIndexes, RawArray<LandCoverParams> fieldToLandCoverParams, RawArray<LandCoverParams> fieldToLandCoverParamsChanges)
    {
        for (int i = 0; i < fieldToLandCoverParams.Length; i++)
        {
            if (!fields.IsLand[i] || IsSet(fieldToLandCoverParams[i]))
                continue;

            GetNeighbors(i, ref neighborsIndexes, fields, fieldsNodesIndexes, nodes, edges, fieldToLandCoverParams);

            var landCoverParams = fieldToLandCoverParams[i];

            for (int j = 0; j < neighborsIndexes.Count; j++)
            {
                var neighborLandCoverParams = fieldToLandCoverParams[neighborsIndexes[j]];

                if (landCoverParams.Heat == 0 && neighborLandCoverParams.HeatCount > 0)
                {
                    landCoverParams.Heat += neighborLandCoverParams.Heat;
                    landCoverParams.HeatCount += neighborLandCoverParams.HeatCount;
                }

                if (landCoverParams.Moisture == 0 && neighborLandCoverParams.MoistureCount > 0)
                {
                    landCoverParams.Moisture += neighborLandCoverParams.Moisture;
                    landCoverParams.MoistureCount += neighborLandCoverParams.MoistureCount;
                }

                if (landCoverParams.Vegetation == 0 && neighborLandCoverParams.VegetationCount > 0)
                {
                    landCoverParams.Vegetation += neighborLandCoverParams.Vegetation;
                    landCoverParams.VegetationCount += neighborLandCoverParams.VegetationCount;
                }
            }

            fieldToLandCoverParamsChanges[i] = landCoverParams;
        }

        int changes = 0;

        for (int i = 0; i < fieldToLandCoverParams.Length; i++)
        {
            if (!fields.IsLand[i] || IsSet(fieldToLandCoverParams[i]))
                continue;

            changes += ApplyChanges(ref fieldToLandCoverParams[i], fieldToLandCoverParamsChanges[i]);
        }

        return changes != 0;
    }

    static void GetNeighbors(
        int fieldIndex, ref RawListStackalloc<uint> neighborsIndexes, Fields fields, RawArray<uint> fieldsNodesIndexes,
        RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, RawArray<LandCoverParams> fieldToLandCoverParams)
    {
        neighborsIndexes.Clear();

        uint nodeIndex = fieldsNodesIndexes[fieldIndex];
        ref var nodeEdges = ref nodes[nodeIndex].Edges;

        for (int i = 0; i < nodeEdges.Length; i++)
        {
            uint otherNodeIndex = edges[nodeEdges[i]].GetOtherNodeIndex(nodeIndex);
            ref var otherNode = ref nodes[otherNodeIndex];

            if (otherNode.Owner.Type == NodeOwnerType.River)
                continue;

            uint otherFieldIndex = otherNode.Owner.Index;

            if (!fields.IsLand[otherFieldIndex] || !IsSet(fieldToLandCoverParams[otherFieldIndex]))
                continue;

            neighborsIndexes.Add(otherFieldIndex);
        }
    }

    static bool IsSet(LandCoverParams landCoverParams) => landCoverParams.HeatCount != 0 && landCoverParams.MoistureCount != 0 && landCoverParams.VegetationCount != 0;

    static int ApplyChanges(ref LandCoverParams landCoverParams, LandCoverParams landCoverParamsChanges)
    {
        bool changed = false;

        if (landCoverParams.Heat == 0 && landCoverParamsChanges.HeatCount > 0)
        {
            landCoverParams.Heat += landCoverParamsChanges.Heat;
            landCoverParams.HeatCount += landCoverParamsChanges.HeatCount;
            changed = true;
        }

        if (landCoverParams.Moisture == 0 && landCoverParamsChanges.MoistureCount > 0)
        {
            landCoverParams.Moisture += landCoverParamsChanges.Moisture;
            landCoverParams.MoistureCount += landCoverParamsChanges.MoistureCount;
            changed = true;
        }

        if (landCoverParams.Vegetation == 0 && landCoverParamsChanges.VegetationCount > 0)
        {
            landCoverParams.Vegetation += landCoverParamsChanges.Vegetation;
            landCoverParams.VegetationCount += landCoverParamsChanges.VegetationCount;
            changed = true;
        }

        return changed ? 1 : 0;
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
                return default;

            byte b = CesColorUtilities.Float01ToByte(value);
            return new Color32(b, b, b, 255);
        });
    }

    public static LandCoverFinal MapToFinal(LandCoverParams landCoverParams)
    {
        landCoverParams = landCoverParams.Normalized();

        // ---

        float buildingsRatio = landCoverParams.Buildings / (float)landCoverParams.GeneralCount;

        if (buildingsRatio > 0.2f)
        {
            return buildingsRatio > 0.5f ? LandCoverFinal.UrbanDense : LandCoverFinal.UrbanSparse;
        }

        // ---

        if (landCoverParams.Cultivation > 0.5f)
        {
            return LandCoverFinal.Cropland;
        }

        // ----

        if (landCoverParams.Vegetation > 0.8f)
        {
            return landCoverParams.Wetness > 0.5f && landCoverParams.Heat > 0.7f ? LandCoverFinal.Mangrove : LandCoverFinal.Forest;
        }

        if (landCoverParams.Vegetation > 0.6f)
        {
            return LandCoverFinal.ForestSparse;
        }

        if (landCoverParams.Vegetation > 0.4f)
        {
            return landCoverParams.Moisture < 0.3f && landCoverParams.Heat > 0.7f ? LandCoverFinal.Shrub : LandCoverFinal.Herbaceous;
        }

        if (landCoverParams.Vegetation > 0.1f)
        {
            return LandCoverFinal.SparseVegetation;
        }

        // ----

        if (landCoverParams.Wetness > 0.5f)
        {
            return LandCoverFinal.Wetland;
        }

        if (landCoverParams.Desertification > 0.5f)
        {
            return LandCoverFinal.Sand;
        }

        if (landCoverParams.Glaciation > 0.5f)
        {
            return LandCoverFinal.Ice;
        }

        return LandCoverFinal.Rock;
    }

    public static Color32 LandCoverFinalToColor(LandCoverFinal landCover) => landCover switch
    {
        LandCoverFinal.Forest => new Color32(0, 100, 0, 255),          // Ciemnozielony
        LandCoverFinal.Mangrove => new Color32(165, 42, 42, 255),      // Br¹zowy
        LandCoverFinal.ForestSparse => new Color32(154, 205, 50, 255), // ¯ó³tozielony
        LandCoverFinal.Shrub => new Color32(255, 140, 0, 255),         // Ciemnopomarañczowy
        LandCoverFinal.Herbaceous => new Color32(124, 252, 0, 255),    // Jaskrawozielony
        LandCoverFinal.SparseVegetation => new Color32(255, 215, 0, 255), // Z³oty
        LandCoverFinal.Cropland => new Color32(255, 165, 0, 255),      // Pomarañczowy
        LandCoverFinal.PaddyField => new Color32(0, 191, 255, 255),    // G³êboki b³êkitny
        LandCoverFinal.Wetland => new Color32(70, 130, 180, 255),      // Stalowo niebieski
        LandCoverFinal.Rock => new Color32(128, 128, 128, 255),        // Szary
        LandCoverFinal.Sand => new Color32(244, 164, 96, 255),         // Piaskowy
        LandCoverFinal.Ice => new Color32(255, 250, 250, 255),         // Prawie bia³y
        LandCoverFinal.UrbanDense => new Color32(255, 0, 0, 255),
        LandCoverFinal.UrbanSparse => new Color32(255, 100, 128, 255),
        _ => default
    };

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

    public enum LandCoverFinal : byte
    {
        None = 0,

        // vegetations
        Forest = 1,
        Mangrove = 2,

        ForestSparse = 3,

        Shrub = 4,
        Herbaceous = 5,

        SparseVegetation = 6,

        // cultivated
        Cropland = 7,
        PaddyField = 8,

        // natural surface
        Wetland = 9,
        Rock = 10,
        Sand = 11,
        Ice = 12,

        // urban
        UrbanDense = 13,
        UrbanSparse = 14,
    }

    static LandCoverParams CalculateLandCoverParams(LandCoverOriginal landCoverOriginal) => landCoverOriginal switch
    {
        LandCoverOriginal.BroadleafEvergreenForest => new(true)
        {
            Heat = LandCoverParams.HIGH,
            HeatCount = 1,
            Moisture = LandCoverParams.FULL,
            MoistureCount = 1,
            Vegetation = LandCoverParams.FULL,
            VegetationCount = 1,
        },
        LandCoverOriginal.BroadleafDeciduousForest => new(true)
        {
            Heat = LandCoverParams.MEDIUM,
            HeatCount = 1,
            Moisture = LandCoverParams.HIGH,
            MoistureCount = 1,
            Vegetation = LandCoverParams.FULL,
            VegetationCount = 1,
        },
        LandCoverOriginal.NeedleleafEvergreenForest => new(true)
        {
            Heat = LandCoverParams.MEDIUM,
            HeatCount = 1,
            Moisture = LandCoverParams.HIGH,
            MoistureCount = 1,
            Vegetation = LandCoverParams.FULL,
            VegetationCount = 1,
        },
        LandCoverOriginal.NeedleleafDeciduousForest => new(true)
        {
            Heat = LandCoverParams.LOW,
            HeatCount = 1,
            Moisture = LandCoverParams.MEDIUM,
            MoistureCount = 1,
            Vegetation = LandCoverParams.FULL,
            VegetationCount = 1,
        },
        LandCoverOriginal.MixedForest => new(true)
        {
            Heat = LandCoverParams.MEDIUM,
            HeatCount = 1,
            Moisture = LandCoverParams.HIGH,
            MoistureCount = 1,
            Vegetation = LandCoverParams.FULL,
            VegetationCount = 1,
        },
        LandCoverOriginal.TreeOpen => new(true)
        {
            //Heat = LandCoverParams.MEDIUM,
            //HeatCount = 1,
            //Moisture = LandCoverParams.MEDIUM,
            //MoistureCount = 1,
            Vegetation = LandCoverParams.HIGH,
            VegetationCount = 1,
        },
        LandCoverOriginal.Shrub => new(true)
        {
            Heat = LandCoverParams.HIGH,
            HeatCount = 1,
            Moisture = LandCoverParams.LOW,
            MoistureCount = 1,
            Vegetation = LandCoverParams.MEDIUM,
            VegetationCount = 1,
        },
        LandCoverOriginal.Herbaceous => new(true)
        {
            //Heat = LandCoverParams.MEDIUM,
            //HeatCount = 1,
            //Moisture = LandCoverParams.MEDIUM,
            //MoistureCount = 1,
            Vegetation = LandCoverParams.MEDIUM,
            VegetationCount = 1,
        },
        LandCoverOriginal.HerbaceousWithSparseTreeShrub => new(true)
        {
            Heat = LandCoverParams.MEDIUM,
            HeatCount = 1,
            Moisture = LandCoverParams.MEDIUM,
            MoistureCount = 1,
            Vegetation = LandCoverParams.HIGH,
            VegetationCount = 1,
        },
        LandCoverOriginal.SparseVegetation => new(true)
        {
            //Heat = LandCoverParams.HIGH,
            //HeatCount = 1,
            //Moisture = LandCoverParams.LOW,
            //MoistureCount = 1,
            Vegetation = LandCoverParams.LOW,
            VegetationCount = 1,
        },
        LandCoverOriginal.Cropland => new(true)
        {
            Heat = LandCoverParams.MEDIUM,
            HeatCount = 1,
            Moisture = LandCoverParams.MEDIUM,
            MoistureCount = 1,
            Vegetation = LandCoverParams.ZERO,
            VegetationCount = 1,
            Cultivation = LandCoverParams.FULL,
        },
        LandCoverOriginal.PaddyField => new(true)
        {
            Heat = LandCoverParams.HIGH,
            HeatCount = 1,
            Moisture = LandCoverParams.HIGH,
            MoistureCount = 1,
            Vegetation = LandCoverParams.ZERO,
            VegetationCount = 1,
            Cultivation = LandCoverParams.FULL,
        },
        LandCoverOriginal.CroplandOtherVegetationMosaic => new(true)
        {
            Heat = LandCoverParams.MEDIUM,
            HeatCount = 1,
            Moisture = LandCoverParams.MEDIUM,
            MoistureCount = 1,
            Vegetation = LandCoverParams.ZERO,
            VegetationCount = 1,
            Cultivation = LandCoverParams.MEDIUM,
        },
        LandCoverOriginal.Mangrove => new(true)
        {
            Heat = LandCoverParams.HIGH,
            HeatCount = 1,
            //Moisture = LandCoverParams.FULL,
            //MoistureCount = 1,
            Vegetation = LandCoverParams.HIGH,
            VegetationCount = 1,
            Wetness = LandCoverParams.HIGH,
        },
        LandCoverOriginal.Wetland => new(true)
        {
            //Heat = LandCoverParams.MEDIUM,
            //HeatCount = 1,
            //Moisture = LandCoverParams.FULL,
            //MoistureCount = 1,
            //Vegetation = LandCoverParams.HIGH,
            //VegetationCount = 1,
            Wetness = LandCoverParams.HIGH,
        },
        LandCoverOriginal.GravelOrRock => new(true)
        {
            //Heat = LandCoverParams.LOW,
            //HeatCount = 1,
            Moisture = LandCoverParams.LOW,
            MoistureCount = 1,
            Vegetation = LandCoverParams.ZERO,
            VegetationCount = 1,
        },
        LandCoverOriginal.Sand => new(true)
        {
            Heat = LandCoverParams.FULL,
            HeatCount = 1,
            Moisture = LandCoverParams.ZERO,
            MoistureCount = 1,
            Vegetation = LandCoverParams.ZERO,
            VegetationCount = 1,
            Desertification = LandCoverParams.FULL,
        },
        LandCoverOriginal.Urban => new(true)
        {
            //Vegetation = LandCoverParams.ZERO,
            //VegetationCount = 1,
            Buildings = 1,
        },
        LandCoverOriginal.SnowIce => new(true)
        {
            Heat = LandCoverParams.ZERO,
            HeatCount = 1,
            Moisture = LandCoverParams.LOW,
            MoistureCount = 1,
            Vegetation = LandCoverParams.ZERO,
            VegetationCount = 1,
            Glaciation = LandCoverParams.FULL,
        },
        _ => default,
    };

    public unsafe struct LandCoverToCount
    {
        [NativeDisableUnsafePtrRestriction]
        public fixed int Counts[LAND_COVERS_COUNT];

        public readonly LandCoverOriginal GetCommonest()
        {
            var landCoverCommonest = LandCoverOriginal.None;
            int countHighest = int.MinValue;

            for (int i = 1; i < LAND_COVERS_COUNT; i++)
            {
                var landCover = (LandCoverOriginal)(byte)i;
                int count = Counts[i];

                if (countHighest < count)
                {
                    countHighest = count;
                    landCoverCommonest = landCover;
                }
            }

            return landCoverCommonest;
        }
    }

    public struct LandCoverParams
    {
        public const float ZERO = 0f;
        public const float LOW = 0.25f;
        public const float MEDIUM = 0.5f;
        public const float HIGH = 0.75f;
        public const float FULL = 1f;

        public float Heat;
        public int HeatCount;
        public float Moisture;
        public int MoistureCount;
        public float Vegetation;
        public int VegetationCount;

        public float Wetness;
        public float Cultivation;
        public int Buildings;
        public float Glaciation;
        public float Desertification;

        public int GeneralCount;

        public LandCoverParams(bool dummy) : this()
        {
            GeneralCount = 1;
        }

        public static LandCoverParams operator +(LandCoverParams lhs, LandCoverParams rhs) => new LandCoverParams
        {
            Heat = lhs.Heat + rhs.Heat,
            HeatCount = lhs.HeatCount + rhs.HeatCount,
            Moisture = lhs.Moisture + rhs.Moisture,
            MoistureCount = lhs.MoistureCount + rhs.MoistureCount,
            Wetness = lhs.Wetness + rhs.Wetness,
            Buildings = lhs.Buildings + rhs.Buildings,
            Cultivation = lhs.Cultivation + rhs.Cultivation,
            Vegetation = lhs.Vegetation + rhs.Vegetation,
            VegetationCount = lhs.VegetationCount + rhs.VegetationCount,
            Glaciation = lhs.Glaciation + rhs.Glaciation,
            Desertification = lhs.Desertification + rhs.Desertification,
            GeneralCount = lhs.GeneralCount + rhs.GeneralCount,
        };

        public static LandCoverParams operator *(LandCoverParams lhs, int scalar) => new LandCoverParams
        {
            Heat = lhs.Heat * scalar,
            HeatCount = lhs.HeatCount * scalar,
            Moisture = lhs.Moisture * scalar,
            MoistureCount = lhs.MoistureCount * scalar,
            Wetness = lhs.Wetness * scalar,
            Buildings = lhs.Buildings * scalar,
            Cultivation = lhs.Cultivation * scalar,
            Vegetation = lhs.Vegetation * scalar,
            VegetationCount = lhs.VegetationCount * scalar,
            Glaciation = lhs.Glaciation * scalar,
            Desertification = lhs.Desertification * scalar,
            GeneralCount = lhs.GeneralCount * scalar,
        };

        public readonly LandCoverParams Normalized() => new LandCoverParams
        {
            Heat = Heat / HeatCount,
            Moisture = Moisture / MoistureCount,
            Vegetation = Vegetation / VegetationCount,
            Wetness = Wetness / GeneralCount,
            Cultivation = Cultivation / GeneralCount,
            Glaciation = Glaciation / GeneralCount,
            Desertification = Desertification / GeneralCount,
            Buildings = Buildings,
            GeneralCount = GeneralCount,
        };
    }
}
