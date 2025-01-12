using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static ClimaterUtilities;

[BurstCompile]
public unsafe struct ClimatesJob : IJobParallelFor
{
    public Fields Fields;
    public FieldsMap FieldsMap;
    public ClimatesMapOriginal ClimatesMapOriginal;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public RawArray<Climate> FieldToClimate;

    [BurstCompile]
    public void Execute(int index)
    {
        if (!Fields.IsLand[index])
        {
            FieldToClimate[index] = Climate.None;
            return;
        }

        var fieldPixelCoords = FieldToPixelCoords[index];
        int count = fieldPixelCoords.Count;
        var climateToCount = stackalloc int[CLIMATES_COUNT];

        for (int i = 0; i < CLIMATES_COUNT; i++)
        {
            climateToCount[i] = 0;
        }

        for (int i = 0; i < count; i++)
        {
            var leftBotMap = fieldPixelCoords[i];
            var rightTopMap = fieldPixelCoords[i] + 1;

            var leftBotUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(leftBotMap, FieldsMap.TextureSize);
            var rightTopUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(rightTopMap, FieldsMap.TextureSize);

            var leftBot = TexUtilities.ClampPixelCoord((int2)math.round(leftBotUv * ClimatesMapOriginal.TextureSize), ClimatesMapOriginal.TextureSize);
            var rightTop = TexUtilities.ClampPixelCoord((int2)math.round(rightTopUv * ClimatesMapOriginal.TextureSize), ClimatesMapOriginal.TextureSize);

            for (int y = leftBot.y; y <= rightTop.y; y++)
            {
                for (int x = leftBot.x; x <= rightTop.x; x++)
                {
                    long flat = TexUtilities.PixelCoordToFlatLong(x, y, ClimatesMapOriginal.TextureSize.x);
                    var soilType = ClimatesMapOriginal.Array[flat];

                    climateToCount[(byte)soilType]++;
                }
            }
        }

        var climateBest = Climate.None;
        float weightHighest = float.MinValue;

        for (int i = 1; i < CLIMATES_COUNT; i++)
        {
            var climate = (Climate)(byte)i;
            float weight = climateToCount[i] * ClimateToWeight(climate);

            if (weightHighest < weight)
            {
                weightHighest = weight;
                climateBest = climate;
            }
        }

        FieldToClimate[index] = climateBest;
    }

    static float ClimateToWeight(Climate climate) => climate switch
    {
        Climate.Cfc => 2f,
        Climate.Dsd => 2f,
        Climate.ET => 2f,
        Climate.EF => 2f,

        _ => 1f,
    };
}
