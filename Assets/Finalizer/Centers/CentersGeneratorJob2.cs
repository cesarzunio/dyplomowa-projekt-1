using System;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using static CentersUtilities;

[BurstCompile]
public struct CentersGeneratorJob2 : IJobParallelFor
{
    public int2 TextureSize;
    public RawArray<FieldSpots> FieldsSpots;
    public RawArray<int> FieldsMap;
    public RawArray<int> RiversMap;
    public RawArray<double2> IndexToCenterGeoCoord;

    [BurstCompile]
    public void Execute(int index)
    {
        int spotIndex = FindBiggestSpot(FieldsSpots[index]);
        var spot = FieldsSpots[index].Spots[spotIndex];
        int spotCount = spot.PixelCoords.Count;
        int fieldColor = FieldsMap[TexUtilities.PixelCoordToFlat(spot.PixelCoords[0], TextureSize.x)];

        var unitSphereMean = double3.zero;

        for (int i = 0; i < spotCount; i++)
        {
            var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(spot.PixelCoords[i], TextureSize);
            unitSphereMean += GeoUtilitiesDouble.PlaneUvToUnitSphere(uv);
        }

        unitSphereMean = math.normalize(unitSphereMean / spotCount);

        GeoUtilitiesDouble.UnitSphereToBoth(unitSphereMean, out var geoCoordsMean, out var uvMean);
        var pixelCoordMean = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvMean, TextureSize);
        int flatMean = TexUtilities.PixelCoordToFlat(pixelCoordMean, TextureSize.x);

        if (FieldsMap[flatMean] == fieldColor && RiversMap[flatMean] == -1)
        {
            IndexToCenterGeoCoord[index] = geoCoordsMean;
            return;
        }

        double distanceMin = double.MaxValue;
        int distanceMinIndex = -1;

        for (int i = 0; i < spotCount; i++)
        {
            var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(spot.PixelCoords[i], TextureSize);
            var unitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(uv);
            var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, TextureSize);
            int flat = TexUtilities.PixelCoordToFlat(pixelCoord, TextureSize.x);
            double distance = GeoUtilitiesDouble.Distance(unitSphereMean, unitSphere);

            if (RiversMap[flat] == -1 && distanceMin > distance)
            {
                distanceMin = distance;
                distanceMinIndex = i;
            }
        }

        if (distanceMinIndex == -1)
            throw new Exception("CentersGeneratorJob :: Execute :: Cannot find any valid center!");

        var pixelCoordBest = spot.PixelCoords[distanceMinIndex];
        var uvBest = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordBest, TextureSize);

        IndexToCenterGeoCoord[index] = GeoUtilitiesDouble.PlaneUvToGeoCoords(uvBest);
    }

    static int FindBiggestSpot(CentersUtilities.FieldSpots fieldSpots)
    {
        int spotMax = int.MinValue;
        int spotIndex = -1;

        for (int i = 0; i < fieldSpots.Spots.Count; i++)
        {
            if (spotMax < fieldSpots.Spots[i].PixelCoords.Count)
            {
                spotMax = fieldSpots.Spots[i].PixelCoords.Count;
                spotIndex = i;
            }
        }

        return spotIndex;
    }
}
