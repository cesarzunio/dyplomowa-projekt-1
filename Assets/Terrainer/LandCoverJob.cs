using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static LandCoverUtility;

[BurstCompile]
public unsafe struct LandCoverJob : IJobParallelFor
{
    public Fields Fields;
    public FieldsMap FieldsMap;
    public LandCoverUtility.LandCoverTextureOriginal LandCoverTextureOriginal;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public RawArray<LandCoverToCount> FieldToLandCoverToCount;

    [BurstCompile]
    public void Execute(int index)
    {
        if (!Fields.IsLand[index])
        {
            FieldToLandCoverToCount[index] = default;
            return;
        }

        var fieldPixelCoords = FieldToPixelCoords[index];
        int count = fieldPixelCoords.Count;
        var landCoverToCount = new LandCoverToCount();

        for (int i = 0; i < count; i++)
        {
            var leftBotMap = fieldPixelCoords[i];
            var rightTopMap = fieldPixelCoords[i] + 1;

            var leftBotUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(leftBotMap, FieldsMap.TextureSize);
            var rightTopUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(rightTopMap, FieldsMap.TextureSize);

            var leftBot = TexUtilities.ClampPixelCoord((int2)math.round(leftBotUv * LandCoverTextureOriginal.TextureSize), LandCoverTextureOriginal.TextureSize);
            var rightTop = TexUtilities.ClampPixelCoord((int2)math.round(rightTopUv * LandCoverTextureOriginal.TextureSize), LandCoverTextureOriginal.TextureSize);

            for (int y = leftBot.y; y <= rightTop.y; y++)
            {
                for (int x = leftBot.x; x <= rightTop.x; x++)
                {
                    long flat = TexUtilities.PixelCoordToFlatLong(x, y, LandCoverTextureOriginal.TextureSize.x);
                    var soilType = LandCoverTextureOriginal.Array[flat];

                    if (soilType != LandCoverUtility.LandCoverOriginal.WaterBodies)
                    {
                        landCoverToCount.Counts[(byte)soilType]++;
                    }
                }
            }
        }

        FieldToLandCoverToCount[index] = landCoverToCount;
    }

    static float LandCoverToWeight(LandCoverUtility.LandCoverOriginal landCover) => landCover switch
    {
        _ => 1f,
    };
}
