using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile, NoAlias]
public unsafe struct DistancesToBorderJob4 : IJobParallelForBatch
{
    [NoAlias]
    public int2 TextureSize;

    [NoAlias]
    public RawBag<double3> UnitSpheres;

    [NoAlias]
    public RawArray<double4> Distances;

    [BurstCompile]
    public void Execute(int startIndex, int count)
    {
        for (int i = startIndex; i < startIndex + count; i++)
        {
            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, TextureSize.x);
            var pixelCoordRight = TexUtilities.ClampPixelCoord(pixelCoord + new int2(1, 0), TextureSize);
            var pixelCoordUp = TexUtilities.ClampPixelCoord(pixelCoord + new int2(0, 1), TextureSize);

            var uv = GeoUtilitiesDouble.EdgeCoordToPlaneUv(pixelCoord, TextureSize);
            var uvRight = GeoUtilitiesDouble.EdgeCoordToPlaneUv(pixelCoordRight, TextureSize);
            var uvUp = GeoUtilitiesDouble.EdgeCoordToPlaneUv(pixelCoordUp, TextureSize);

            var unitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(uv);
            var unitSphereRight = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvRight);
            var unitSphereUp = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvUp);

            var unitSphereCenter = math.normalize(unitSphereRight + unitSphereUp);
            var unitSphereLeft = math.normalize(unitSphere + unitSphereUp);
            var unitSphereBot = math.normalize(unitSphere + unitSphereRight);

            double distanceLB = GetDistance(unitSphere, in UnitSpheres);
            double distanceC = GetDistance(unitSphereCenter, in UnitSpheres);
            double distanceL = GetDistance(unitSphereLeft, in UnitSpheres);
            double distanceB = GetDistance(unitSphereBot, in UnitSpheres);

            Distances[i] = new double4(distanceLB, distanceC, distanceL, distanceB);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static double GetDistance(double3 unitSphere, in RawBag<double3> unitSpheres)
    {
        var distanceMin = double.MaxValue;

        for (int i = 0; i < unitSpheres.Count; i++)
        {
            distanceMin = math.min(distanceMin, GeoUtilitiesDouble.Distance(unitSphere, unitSpheres[i]));
        }

        return distanceMin;
    }
}
