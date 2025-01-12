using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static PrecipitationUtility;

[BurstCompile]
public unsafe struct PrecipitationJob : IJobParallelFor
{
    const int NO_DATA_VALUE = int.MinValue;

    public Fields Fields;
    public FieldsMap FieldsMap;
    public PrecipitationTextureOriginal TemperatureTexture;
    public int2 LeftBotMap;
    public int2 RightTopMap;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public int MonthIndex;
    public RawArray<FieldPrecipitations> FieldToPrecipitations;

    [BurstCompile]
    public void Execute(int index)
    {
        //if (!Fields.IsLand[index])
        //{
        //    FieldToPrecipitations[index].MonthToPrecipitation[MonthIndex] = default;
        //    FieldToPrecipitations[index].MonthToSet[MonthIndex] = false;
        //    return;
        //}

        var textureSizeOriginalResized = (RightTopMap + 1) - LeftBotMap;
        var fieldPixelCoords = FieldToPixelCoords[index];
        int count = fieldPixelCoords.Count;

        long temperatureSum = 0;
        int temperatureCount = 0;

        for (int i = 0; i < count; i++)
        {
            var leftBotMap = fieldPixelCoords[i];
            var rightTopMap = fieldPixelCoords[i] + 1;

            if (leftBotMap.y < LeftBotMap.y || rightTopMap.y > RightTopMap.y)
                continue;

            leftBotMap -= LeftBotMap;
            rightTopMap -= LeftBotMap;

            var leftBotUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(leftBotMap, textureSizeOriginalResized);
            var rightTopUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(rightTopMap, textureSizeOriginalResized);

            var leftBot = TexUtilities.ClampPixelCoord((int2)math.round(leftBotUv * TemperatureTexture.TextureSize), TemperatureTexture.TextureSize);
            var rightTop = TexUtilities.ClampPixelCoord((int2)math.round(rightTopUv * TemperatureTexture.TextureSize), TemperatureTexture.TextureSize);

            for (int y = leftBot.y; y <= rightTop.y; y++)
            {
                for (int x = leftBot.x; x <= rightTop.x; x++)
                {
                    long flat = TexUtilities.PixelCoordToFlatLong(x, y, TemperatureTexture.TextureSize.x);
                    var precipitation = TemperatureTexture.Array[flat];

                    if (precipitation != NO_DATA_VALUE)
                    {
                        temperatureSum += precipitation;
                        temperatureCount++;
                    }
                }
            }
        }

        if (temperatureCount > 0)
        {
            FieldToPrecipitations[index].MonthToPrecipitation[MonthIndex] = temperatureSum / (float)temperatureCount;
            FieldToPrecipitations[index].MonthToSet[MonthIndex] = true;
        }
        else
        {
            FieldToPrecipitations[index].MonthToPrecipitation[MonthIndex] = default;
            FieldToPrecipitations[index].MonthToSet[MonthIndex] = false;
        }
    }
}