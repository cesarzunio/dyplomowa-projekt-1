using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public unsafe struct SurfacesJob : IJobParallelFor
{
    public RawArray<double> YToPixelSurface;
    public RawArray<RawBag<int2>> FieldToPixelCoords;
    public RawArray<double> FieldToSurface;

    [BurstCompile]
    public void Execute(int index)
    {
        ref var pixelCoords = ref FieldToPixelCoords[index];
        double surface = 0.0;

        for (int i = 0; i < pixelCoords.Count; i++)
        {
            surface += YToPixelSurface[pixelCoords[i].y];
        }

        FieldToSurface[index] = surface;
    }
}