using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static NodesSaveUtility4;

public static unsafe class LandFormUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    const int LAND_FORMS_COUNT = 16;

    public static RawArray<LandForm> GenerateLandForms(
        Fields fields, FieldsMap fieldsMap, LandFormTextureOriginal landFormTextureOriginal,
        RawArray<uint> fieldsNodesIndexes, NodesFinals nodes, EdgesFinals edges, Allocator allocator)
    {
        var fieldToLandForm = new RawArray<LandForm>(allocator, LandForm.None, fields.Length);
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);

        var job = new LandFormJob
        {
            Fields = fields,
            FieldsMap = fieldsMap,
            LeftBotMap = TexUtilities.FlipY(new int2(0, 6800), fieldsMap.TextureSize.y),
            RightTopMap = TexUtilities.FlipY(new int2(16383, 291), fieldsMap.TextureSize.y),
            LandFormTextureOriginal = landFormTextureOriginal,
            FieldToLandForm = fieldToLandForm,
            FieldToPixelCoords = fieldToPixelCoords,
        };

        job.Schedule(fieldToLandForm.Length, 64).Complete();

        ExtendClimates(fields, fieldToLandForm, fieldsNodesIndexes, nodes, edges);

        fieldToPixelCoords.DisposeDepth1();

        return fieldToLandForm;
    }

    public static LandFormTextureOriginal LoadLandFormTextureFromStrips(string pathPrefix, int2 textureSize, int stripHeight, Allocator allocator)
    {
        long totalPixels = (long)textureSize.x * (long)textureSize.y;
        var landFormArray = CesMemoryUtility.Allocate<LandForm>(totalPixels, allocator);

        int fullStrips = textureSize.y / stripHeight;
        int remainingRows = textureSize.y % stripHeight;

        long currentPixelIndex = 0;

        for (int i = 0; i <= fullStrips; i++)
        {
            int startY = i * stripHeight;
            int endY = (i == fullStrips) ? startY + remainingRows : startY + stripHeight;
            int currentStripHeight = endY - startY;

            if (currentStripHeight <= 0)
                break;

            string filePath = $"{pathPrefix}_{startY}_{endY}.bin";
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            int pixelsToRead = textureSize.x * currentStripHeight;
            BinarySaveUtility.ReadArraySimple(fileStream, pixelsToRead, landFormArray + currentPixelIndex);
            currentPixelIndex += pixelsToRead;
        }

        for (int y = 0; y < textureSize.y / 2; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                long flatBot = TexUtilities.PixelCoordToFlatLong(x, y, textureSize.x);
                long flatTop = TexUtilities.PixelCoordToFlatLong(x, textureSize.y - y - 1, textureSize.x);

                (landFormArray[flatBot], landFormArray[flatTop]) = (landFormArray[flatTop], landFormArray[flatBot]);
            }
        }

        return new LandFormTextureOriginal(textureSize, landFormArray, allocator);
    }

    static void ExtendClimates(Fields fields, RawArray<LandForm> fieldToClimateFinal, RawArray<uint> fieldsNodesIndexes, NodesFinals nodes, EdgesFinals edges)
    {
        var climateChanges = new LandForm[fieldToClimateFinal.Length];

        while (ExtendClimatesIterate(fields, fieldToClimateFinal, fieldsNodesIndexes, nodes, edges, climateChanges)) { }
    }

    static bool ExtendClimatesIterate(
        Fields fields, RawArray<LandForm> fieldToClimateFinal, RawArray<uint> fieldsNodesIndexes,
        NodesFinals nodes, EdgesFinals edges, LandForm[] climateChanges)
    {
        for (int i = 0; i < fieldToClimateFinal.Length; i++)
        {
            if (!fields.IsLand[i] || fieldToClimateFinal[i] != LandForm.None)
                continue;

            climateChanges[i] = GetCommonestNeighborClimate((uint)i, fieldToClimateFinal, fieldsNodesIndexes, nodes, edges);
        }

        int changes = 0;

        for (int i = 0; i < fieldToClimateFinal.Length; i++)
        {
            if (!fields.IsLand[i] || fieldToClimateFinal[i] != LandForm.None || climateChanges[i] == LandForm.None)
                continue;

            fieldToClimateFinal[i] = climateChanges[i];
            changes++;
        }

        return changes != 0;
    }

    static LandForm GetCommonestNeighborClimate(
        uint field, RawArray<LandForm> fieldToClimateOut, RawArray<uint> fieldsNodesIndexes,
        NodesFinals nodes, EdgesFinals edges)
    {
        var neighborClimateToCount = stackalloc int[LAND_FORMS_COUNT];

        for (int i = 0; i < LAND_FORMS_COUNT; i++)
        {
            neighborClimateToCount[i] = 0;
        }

        uint nodeIndex = fieldsNodesIndexes[field];
        ref readonly var nodeEdges = ref nodes.EdgesIndexes[nodeIndex];

        for (int i = 0; i < nodeEdges.Length; i++)
        {
            ref readonly var edgeNodesIndexes = ref edges.NodesIndexes[nodeEdges[i]];
            uint nodeIndexOther = edgeNodesIndexes.x ^ edgeNodesIndexes.y ^ nodeIndex;
            ref readonly var nodeOtherOwner = ref nodes.Owner[nodeIndexOther];

            if (nodeOtherOwner.Type == NodeOwnerType.River)
                continue;

            var climate = fieldToClimateOut[nodeOtherOwner.Index];

            if (climate == LandForm.None)
                continue;

            neighborClimateToCount[(uint)climate]++;
        }

        int countHighestIndex = -1;
        int countHighest = int.MinValue;

        for (int i = 0; i < LAND_FORMS_COUNT; i++)
        {
            if (countHighest < neighborClimateToCount[i])
            {
                countHighest = neighborClimateToCount[i];
                countHighestIndex = i;
            }
        }

        return countHighestIndex == -1 ? LandForm.None : (LandForm)(byte)countHighestIndex;
    }

    public readonly struct LandFormTextureOriginal
    {
        public readonly int2 TextureSize;

        [NativeDisableUnsafePtrRestriction]
        public readonly LandForm* LandForm;

        readonly Allocator _allocator;

        public LandFormTextureOriginal(int2 textureSize, LandForm* landForm, Allocator allocator)
        {
            TextureSize = textureSize;
            LandForm = landForm;
            _allocator = allocator;
        }

        public readonly void Dispose()
        {
            UnsafeUtility.Free(LandForm, _allocator);
        }
    }

    public enum LandForm : byte
    {
        None = 0,
        BedrockMountainSteepRough = 1,
        BedrockMountainSteepSmooth = 2,
        BedrockMountainModerateRough = 3,
        BedrockMountainModerateSmooth = 4,
        HillsRough = 5,
        HillsSmooth = 6,
        UpperLargeSlope = 7,
        MiddleLargeSlope = 8,
        DissectedTerraceModeratePlateau = 9,
        TerraceEdgeOrValleyBottomPlain = 10,
        TerraceOrSmoothPlateau = 11,
        AlluvialFanOrPediment = 12,
        UpstreamAlluvialPlain = 13,
        AlluvialOrCoastalPlain = 14,
        DeltaOrMarsh = 15
    }
}
