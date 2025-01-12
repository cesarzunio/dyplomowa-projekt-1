using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static SoilsUtility;

[BurstCompile]
public unsafe struct SoilsJob : IJobParallelFor
{
    const int LAND_FORM_COUNT = 30;

    public Fields Fields;
    public FieldsMap FieldsMap;
    public int2 LeftBotMap;
    public int2 RightTopMap;
    public SoilsTextureOriginal SoilsTextureOriginal;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public RawArray<SoilType> FieldToSoilType;

    [BurstCompile]
    public void Execute(int index)
    {
        if (!Fields.IsLand[index])
            return;

        var textureSizeMap = (RightTopMap + 1) - LeftBotMap;
        var fieldPixelCoords = FieldToPixelCoords[index];
        int count = fieldPixelCoords.Count;
        var soilTypeToCount = stackalloc uint[LAND_FORM_COUNT];

        for (int i = 0; i < LAND_FORM_COUNT; i++)
        {
            soilTypeToCount[i] = 0;
        }

        for (int i = 0; i < count; i++)
        {
            var leftBotMap = fieldPixelCoords[i];
            var rightTopMap = fieldPixelCoords[i] + 1;

            if (leftBotMap.y < LeftBotMap.y || rightTopMap.y > RightTopMap.y)
                continue;

            leftBotMap -= LeftBotMap;
            rightTopMap -= LeftBotMap;

            var leftBotUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(leftBotMap, textureSizeMap);
            var rightTopUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(rightTopMap, textureSizeMap);

            var leftBot = TexUtilities.ClampPixelCoord((int2)math.round(leftBotUv * SoilsTextureOriginal.TextureSize), SoilsTextureOriginal.TextureSize);
            var rightTop = TexUtilities.ClampPixelCoord((int2)math.round(rightTopUv * SoilsTextureOriginal.TextureSize), SoilsTextureOriginal.TextureSize);

            for (int y = leftBot.y; y <= rightTop.y; y++)
            {
                for (int x = leftBot.x; x <= rightTop.x; x++)
                {
                    long flat = TexUtilities.PixelCoordToFlatLong(x, y, SoilsTextureOriginal.TextureSize.x);
                    var soilType = SoilsTextureOriginal.Array[flat];

                    if (soilType != SoilType.None)
                    {
                        soilTypeToCount[(byte)soilType]++;
                    }
                }
            }
        }

        var soilTypeCommonest = SoilType.None;
        float soilTypeWeightMax = 0f;

        for (int i = 0; i < LAND_FORM_COUNT; i++)
        {
            var soilType = (SoilType)i;
            float weight = SoilTypeToWeight(soilType) * soilTypeToCount[i];

            var pairOld = (soilTypeCommonest, soilTypeWeightMax);
            var pairNew = (soilType, weight);

            (soilTypeCommonest, soilTypeWeightMax) = (weight > soilTypeWeightMax) ? pairNew : pairOld;
        }

        FieldToSoilType[index] = soilTypeCommonest;
    }

    static float SoilTypeToWeight(SoilType soilType) => soilType switch
    {
        _ => 1f,
    };
}
