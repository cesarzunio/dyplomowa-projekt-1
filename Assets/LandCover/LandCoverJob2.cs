using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct LandCoverJob2 : IJobParallelFor
{
    public Fields Fields;
    public FieldsMap FieldsMap;
    public LandCoverUtility2.LandCoverTextureOriginal LandCoverTextureOriginal;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public RawArray<LandCoverUtility2.LandCoverToCount> FieldToLandCoverToCount;

    [BurstCompile]
    public void Execute(int index)
    {
        var fieldPixelCoords = FieldToPixelCoords[index];
        int count = fieldPixelCoords.Count;
        var landCoverToCount = new LandCoverUtility2.LandCoverToCount();

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

                    landCoverToCount.Counts[(byte)soilType]++;
                }
            }
        }

        FieldToLandCoverToCount[index] = landCoverToCount;
    }
}
