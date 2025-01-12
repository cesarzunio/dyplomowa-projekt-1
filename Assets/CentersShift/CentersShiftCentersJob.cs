using System;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using static FinalizerSaves;

[BurstCompile]
public unsafe struct CentersShiftCentersJob : IJobParallelFor
{
    public int2 TextureSize;
    public RawArray<RawBag<RawBag<int2>>> FieldsSpots;
    public FieldsMap FieldsMap;
    public RawArray<int> RiversMap;
    public RawArray<double2> IndexToCenterGeoCoord;

    [BurstCompile]
    public void Execute(int index)
    {
        int spotIndex = FindBiggestSpot(FieldsSpots[index]);
        var spot = FieldsSpots[index][spotIndex];
        int spotCount = spot.Count;
        uint fieldColor = FieldsMap.Fields[TexUtilities.PixelCoordToFlat(spot[0], TextureSize.x)];

        var unitSphereMean = double3.zero;

        for (int i = 0; i < spotCount; i++)
        {
            var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(spot[i], TextureSize);
            unitSphereMean += GeoUtilitiesDouble.PlaneUvToUnitSphere(uv);
        }

        unitSphereMean = math.normalize(unitSphereMean / spotCount);

        GeoUtilitiesDouble.UnitSphereToBoth(unitSphereMean, out var geoCoordsMean, out var uvMean);
        var pixelCoordMean = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvMean, TextureSize);
        int flatMean = TexUtilities.PixelCoordToFlat(pixelCoordMean, TextureSize.x);

        if (FieldsMap.Fields[flatMean] == fieldColor && RiversMap[flatMean] == -1)
        {
            IndexToCenterGeoCoord[index] = geoCoordsMean;
            return;
        }

        double distanceMin = double.MaxValue;
        int distanceMinIndex = -1;

        for (int i = 0; i < spotCount; i++)
        {
            var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(spot[i], TextureSize);
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

        var pixelCoordBest = spot[distanceMinIndex];
        var uvBest = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordBest, TextureSize);

        IndexToCenterGeoCoord[index] = GeoUtilitiesDouble.PlaneUvToGeoCoords(uvBest);
    }

    static int FindBiggestSpot(RawBag<RawBag<int2>> spots)
    {
        int spotMax = int.MinValue;
        int spotIndex = -1;

        for (int i = 0; i < spots.Count; i++)
        {
            (spotMax, spotIndex) = spotMax < spots[i].Count ? (spots[i].Count, i) : (spotMax, spotIndex);
        }

        return spotIndex;
    }
}
