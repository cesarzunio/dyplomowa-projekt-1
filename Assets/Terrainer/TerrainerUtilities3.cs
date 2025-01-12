//using System.Collections.Generic;
//using System.IO;
//using Unity.Collections;
//using Unity.Mathematics;
//using UnityEngine;
//using static FinalizerSaves;
//using static NodesUtilities;

//public static unsafe class TerrainerUtilities3
//{
//    const Allocator ALLOCATOR = Allocator.Persistent;
//    const int LAND_COVERS_COUNT = 21;

//    public static RawArray<Color32> CreateFieldsLandCoversMap(FieldsMap fieldsMap, RawArray<LandCover> fieldsLandCovers, Allocator allocator)
//    {
//        var fieldsLandCoversMap = new RawArray<Color32>(allocator, fieldsMap.TextureSize.x * fieldsMap.TextureSize.y);
//        var randomColors = CesColorUtilities.CreateRandomColors(LAND_COVERS_COUNT);

//        for (int i = 0; i < fieldsLandCoversMap.Length; i++)
//        {
//            uint field = fieldsMap.Fields[i];
//            var landCover = fieldsLandCovers[field];

//            //fieldsLandCoversMap[i] = landCover == LandCover.None ? default : randomColors[(byte)landCover];
//            fieldsLandCoversMap[i] = randomColors[(byte)landCover];
//        }

//        return fieldsLandCoversMap;
//    }

//    public static RawArray<LandCover> CreateFieldsLandCovers(
//        FieldsMap fieldsMap, Fields savedFields, LandCover* landCoverArray, int2 landCoverTextureSize,
//        RawArray<uint> fieldsNodesIndexes, RawArray<NodeUnmanaged> nodes, RawArray<NodeEdge> edges, int extendIterations, Allocator allocator)
//    {
//        var fieldToLandCover = GetFieldToLandCover(fieldsMap, savedFields, landCoverArray, landCoverTextureSize);
//        var fieldToLandCoverFinal = GetFieldToLandCoverFinal(fieldToLandCover, allocator);

//        ExtendLandCovers(fieldToLandCoverFinal, fieldsNodesIndexes, nodes, edges, extendIterations);

//        return fieldToLandCoverFinal;
//    }

//    static FieldLandCovers[] GetFieldToLandCover(
//        FieldsMap fieldsMap, Fields savedFields,
//        LandCover* landCoverArray, int2 landCoverTextureSize)
//    {
//        var fieldToLandCover = new FieldLandCovers[savedFields.Length];

//        for (int i = 0; i < fieldToLandCover.Length; i++)
//        {
//            fieldToLandCover[i] = FieldLandCovers.New();
//        }

//        for (int y = 0; y < fieldsMap.TextureSize.y; y++)
//        {
//            for (int x = 0; x < fieldsMap.TextureSize.x; x++)
//            {
//                var pixelCoord = new int2(x, y);
//                int flat = TexUtilities.PixelCoordToFlat(pixelCoord, fieldsMap.TextureSize.x);
//                uint index = fieldsMap.Fields[flat];

//                //if (!savedFields.IsLand[index])
//                //    continue;

//                var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoord, fieldsMap.TextureSize).FlipY();
//                var pixelCoordLandCover = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, landCoverTextureSize);
//                int flatLandCover = TexUtilities.PixelCoordToFlat(pixelCoordLandCover, landCoverTextureSize.x);
//                var landCover = landCoverArray[flatLandCover];

//                fieldToLandCover[index].Add(landCover);

//                //if (IsValid(landCover))
//                //{
//                //    fieldToLandCover[index].Add(landCover);
//                //}
//            }
//        }

//        return fieldToLandCover;
//    }

//    static double2 FlipY(this double2 uv) => new(uv.x, 1.0 - uv.y);

//    static RawArray<LandCover> GetFieldToLandCoverFinal(FieldLandCovers[] fieldTolandCover, Allocator allocator)
//    {
//        var fieldToLandCoverFinal = new RawArray<LandCover>(allocator, fieldTolandCover.Length);

//        for (int i = 0; i < fieldToLandCoverFinal.Length; i++)
//        {
//            fieldToLandCoverFinal[i] = fieldTolandCover[i].GetBestLandCover(true, true);
//        }

//        return fieldToLandCoverFinal;
//    }

//    static void ExtendLandCovers(RawArray<LandCover> fieldToLandCoverFinal, RawArray<uint> fieldsNodesIndexes, RawArray<NodeUnmanaged> nodes, RawArray<NodeEdge> edges, int iterations)
//    {
//        var landCoverChanges = new LandCover[fieldToLandCoverFinal.Length];

//        for (int i = 0; i < iterations; i++)
//        {
//            while (ExtendLandCoversIterate(fieldToLandCoverFinal, fieldsNodesIndexes, nodes, edges, landCoverChanges)) { }
//        }
//    }

//    static bool ExtendLandCoversIterate(RawArray<LandCover> fieldToLandCoverFinal, RawArray<uint> fieldsNodesIndexes, RawArray<NodeUnmanaged> nodes, RawArray<NodeEdge> edges, LandCover[] landCoverChanges)
//    {
//        for (int i = 0; i < fieldToLandCoverFinal.Length; i++)
//        {
//            if (fieldToLandCoverFinal[i] != LandCover.None)
//                continue;

//            landCoverChanges[i] = GetCommonestLandCover((uint)i, fieldToLandCoverFinal, fieldsNodesIndexes, nodes, edges);
//        }

//        int changes = 0;

//        for (int i = 0; i < fieldToLandCoverFinal.Length; i++)
//        {
//            if (fieldToLandCoverFinal[i] != LandCover.None || landCoverChanges[i] == LandCover.None)
//                continue;

//            fieldToLandCoverFinal[i] = landCoverChanges[i];
//            changes++;
//        }

//        //if (changes == 0)
//        //{
//        //    Debug.Log($"Changes 0, List {list.Count}");

//        //    for (int i = 0; i < list.Count; i++)
//        //    {
//        //        Debug.Log(i);
//        //    }
//        //}

//        return changes != 0;
//    }

//    static LandCover GetCommonestLandCover(uint field, RawArray<LandCover> fieldToLandCoverFinal, RawArray<uint> fieldsNodesIndexes, RawArray<NodeUnmanaged> nodes, RawArray<NodeEdge> edges)
//    {
//        uint debugIndex = (uint)(new Color32(206, 240, 27, 255).ToIndex());

//        var neighborClimateToCount = stackalloc int[LAND_COVERS_COUNT];

//        for (int i = 0; i < LAND_COVERS_COUNT; i++)
//        {
//            neighborClimateToCount[i] = 0;
//        }

//        uint nodeIndex = fieldsNodesIndexes[field];
//        var edgesIndexes = nodes[nodeIndex].Edges;

//        for (int i = 0; i < edgesIndexes.Length; i++)
//        {
//            var owner = nodes[edges[edgesIndexes[i]].GetOtherNode(nodeIndex)].Owner;

//            if (owner.Type == NodeOwnerType.River)
//                continue;

//            var landCover = fieldToLandCoverFinal[owner.Index];

//            if (landCover == LandCover.None)
//                continue;

//            neighborClimateToCount[(uint)landCover]++;
//        }

//        int countHighestIndex = -1;
//        int countHighest = int.MinValue;

//        for (int i = 0; i < LAND_COVERS_COUNT; i++)
//        {
//            if (countHighest < neighborClimateToCount[i])
//            {
//                countHighest = neighborClimateToCount[i];
//                countHighestIndex = i;
//            }
//        }

//        return countHighestIndex == -1 ? LandCover.None : (LandCover)(uint)countHighestIndex;
//    }

//    static void CreateSave(string path, Fields savedFields, Dictionary<int, LandCover> fieldToLandCoverFinal)
//    {
//        var landCovers = new RawArray<LandCover>(ALLOCATOR, savedFields.Length);

//        for (int i = 0; i < savedFields.Length; i++)
//        {
//            landCovers[i] = GetLandCover(i, fieldToLandCoverFinal);
//        }

//        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

//        BinarySaveUtility.WriteRawContainerSimple<RawArray<LandCover>, LandCover>(fileStream, landCovers);

//        // ---

//        static LandCover GetLandCover(int field, Dictionary<int, LandCover> fieldToLandCoverFinal)
//        {
//            if (fieldToLandCoverFinal.TryGetValue(field, out var landCover))
//                return landCover;

//            return LandCover.None;
//        }
//    }

//    static void GetFinalLandCoverMaps(
//        RawArray<int> fieldsMap, Dictionary<int, LandCover> fieldToLandCoverFinal, Allocator allocator,
//        out RawArray<Color32> landCoverMap, out RawArray<Color32> landCoverMapColorful)
//    {
//        landCoverMap = new RawArray<Color32>(allocator, default, fieldsMap.Length);
//        landCoverMapColorful = new RawArray<Color32>(allocator, default, fieldsMap.Length);
//        var randomColors = CesColorUtilities.CreateRandomColors(20);

//        for (int i = 0; i < fieldsMap.Length; i++)
//        {
//            if (fieldToLandCoverFinal.TryGetValue(fieldsMap[i], out var landCover))
//            {
//                byte landCoverByte = (byte)landCover;
//                int landCoverInt = (int)landCover;

//                landCoverMap[i] = new Color32(landCoverByte, landCoverByte, landCoverByte, 255);
//                landCoverMapColorful[i] = randomColors[landCoverInt - 1];
//            }
//        }
//    }

//    struct FieldLandCovers
//    {
//        public Dictionary<LandCover, float> _landCoverToWeights;

//        public static FieldLandCovers New() => new()
//        {
//            _landCoverToWeights = new Dictionary<LandCover, float>(8)
//        };

//        public readonly void Add(LandCover landCover)
//        {
//            if (_landCoverToWeights.ContainsKey(landCover))
//            {
//                _landCoverToWeights[landCover] += GetWeight(landCover);
//            }
//            else
//            {
//                _landCoverToWeights[landCover] = GetWeight(landCover);
//            }
//        }

//        public readonly bool TryGetLandCoverWeight(LandCover landCover, out float weight) => _landCoverToWeights.TryGetValue(landCover, out weight);

//        public readonly LandCover GetBestLandCover(bool skipEmpty, bool skipUrban)
//        {
//            float weightHighest = float.MinValue;
//            var landCoverHighest = LandCover.None;

//            foreach (var (landCover, weight) in _landCoverToWeights)
//            {
//                bool skip = landCover switch
//                {
//                    LandCover.WaterBodies => true,
//                    LandCover.None => skipEmpty,
//                    LandCover.Urban => skipUrban,

//                    _ => false
//                };

//                if (skip)
//                    continue;

//                if (weightHighest < weight)
//                {
//                    weightHighest = weight;
//                    landCoverHighest = landCover;
//                }
//            }

//            return landCoverHighest;
//        }

//        static float GetWeight(LandCover landCover) => landCover switch
//        {
//            LandCover.Urban => 2f,
//            _ => 1f
//        };
//    }
//}

//public enum LandCover : byte
//{
//    None = 0,
//    BroadleafEvergreenForest = 1,
//    BroadleafDeciduousForest = 2,
//    NeedleleafEvergreenForest = 3,
//    NeedleleafDeciduousForest = 4,
//    MixedForest = 5,
//    TreeOpen = 6,
//    Shrub = 7,
//    Herbaceous = 8,
//    HerbaceousWithSparseTreeShrub = 9,
//    SparseVegetation = 10,
//    Cropland = 11,
//    PaddyField = 12,
//    CroplandOtherVegetationMosaic = 13,
//    Mangrove = 14,
//    Wetland = 15,
//    GravelOrRock = 16,
//    Sand = 17,
//    Urban = 18,
//    SnowIce = 19,
//    WaterBodies = 20
//}