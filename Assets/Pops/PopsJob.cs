using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct PopsJob : IJobParallelFor
{
    public FieldsMap FieldsMap;
    public PopsTexture PopsTexture;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public RawArray<float> FieldToPops;

    [BurstCompile]
    public void Execute(int index)
    {
        var fieldPixelCoords = FieldToPixelCoords[index];
        int count = fieldPixelCoords.Count;
        float pops = 0f;

        for (int i = 0; i < count; i++)
        {
            var leftBotUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(fieldPixelCoords[i], FieldsMap.TextureSize);
            var rightTopUv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(fieldPixelCoords[i] + 1, FieldsMap.TextureSize);

            var leftBot = TexUtilities.ClampPixelCoord((int2)math.round(leftBotUv * PopsTexture.TextureSize), PopsTexture.TextureSize);
            var rightTop = TexUtilities.ClampPixelCoord((int2)math.round(rightTopUv * PopsTexture.TextureSize), PopsTexture.TextureSize);

            for (int y = leftBot.y; y <= rightTop.y; y++)
            {
                for (int x = leftBot.x; x <= rightTop.x; x++)
                {
                    var pixelCoord = new int2(x, y);
                    int flat = TexUtilities.PixelCoordToFlat(pixelCoord, PopsTexture.TextureSize.x);
                    pops += PopsTexture.Pops[flat];
                }
            }
        }

        FieldToPops[index] = pops;
    }
}
