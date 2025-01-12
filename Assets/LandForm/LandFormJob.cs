using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using static FinalizerSaves;
using static LandFormUtility;

[BurstCompile]
public unsafe struct LandFormJob : IJobParallelFor
{
    const int LAND_FORM_COUNT = 16;

    public Fields Fields;
    public FieldsMap FieldsMap;
    public int2 LeftBotMap;
    public int2 RightTopMap;
    public LandFormTextureOriginal LandFormTextureOriginal;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public RawArray<LandForm> FieldToLandForm;

    [BurstCompile]
    public void Execute(int index)
    {
        if (!Fields.IsLand[index])
            return;

        var textureSizeMap = (RightTopMap + 1) - LeftBotMap;
        var fieldPixelCoords = FieldToPixelCoords[index];
        int count = fieldPixelCoords.Count;
        var landFormToCount = stackalloc uint[LAND_FORM_COUNT];

        for (int i = 0; i < LAND_FORM_COUNT; i++)
        {
            landFormToCount[i] = 0;
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

            var leftBot = TexUtilities.ClampPixelCoord((int2)math.round(leftBotUv * LandFormTextureOriginal.TextureSize), LandFormTextureOriginal.TextureSize);
            var rightTop = TexUtilities.ClampPixelCoord((int2)math.round(rightTopUv * LandFormTextureOriginal.TextureSize), LandFormTextureOriginal.TextureSize);

            for (int y = leftBot.y; y <= rightTop.y; y++)
            {
                for (int x = leftBot.x; x <= rightTop.x; x++)
                {
                    long flat = TexUtilities.PixelCoordToFlatLong(x, y, LandFormTextureOriginal.TextureSize.x);
                    landFormToCount[(byte)LandFormTextureOriginal.LandForm[flat]]++;
                }
            }
        }

        var landFormCommonest = LandForm.None;
        float landFormWeightMax = 0f;

        for (int i = 1; i < LAND_FORM_COUNT; i++)
        {
            var landForm = (LandForm)i;
            float weight = LandFormToWeight(landForm) * landFormToCount[i];

            var pairOld = (landFormCommonest, landFormWeightMax);
            var pairNew = (landForm, weight);

            (landFormCommonest, landFormWeightMax) = (weight > landFormWeightMax) ? pairNew : pairOld;
        }

        FieldToLandForm[index] = landFormCommonest;
    }

    static float LandFormToWeight(LandForm landForm) => landForm switch
    {
        _ => 1f,
    };
}
