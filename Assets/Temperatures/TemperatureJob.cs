using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static TemperatureUtility;

[BurstCompile]
public unsafe struct TemperatureJob : IJobParallelFor
{
    const int NO_DATA_VALUE = int.MinValue;

    public Fields Fields;
    public FieldsMap FieldsMap;
    public TemperatureTextureOriginal TemperatureTexture;
    public int2 LeftBotMap;
    public int2 RightTopMap;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public int MonthIndex;
    public RawArray<FieldTemperatures> FieldToTemperatures;

    [BurstCompile]
    public void Execute(int index)
    {
        //if (!Fields.IsLand[index])
        //{
        //    FieldToTemperatures[index].MonthToTemperature[MonthIndex] = default;
        //    FieldToTemperatures[index].MonthToSet[MonthIndex] = false;
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
                    var temperature = TemperatureTexture.Array[flat];

                    if (temperature != NO_DATA_VALUE)
                    {
                        temperatureSum += temperature;
                        temperatureCount++;
                    }
                }
            }
        }

        if (temperatureCount > 0)
        {
            FieldToTemperatures[index].MonthToTemperature[MonthIndex] = ToCelcius(temperatureSum, temperatureCount);
            FieldToTemperatures[index].MonthToSet[MonthIndex] = true;
        }
        else
        {
            FieldToTemperatures[index].MonthToTemperature[MonthIndex] = default;
            FieldToTemperatures[index].MonthToSet[MonthIndex] = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float ToCelcius(long kelvinsOriginal, int temperatureCount) => (kelvinsOriginal / (10f * temperatureCount)) - 273f;
}