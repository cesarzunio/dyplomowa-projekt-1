using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;

public static class SurfacesUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static void SaveFieldsSurfaces(string path, RawArray<double> fieldToSurface)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(fieldToSurface.Length);

        for (int i = 0; i < fieldToSurface.Length; i++)
        {
            fileStream.WriteValue((float)fieldToSurface[i]);
        }
    }

    public static RawArray<double> GenerateFieldsSurfaces(Fields fields, FieldsMap fieldsMap, Allocator allocator)
    {
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);
        var yToPixelSurface = GetYToPixelSurface(fieldsMap.TextureSize);
        var fieldToSurface = new RawArray<double>(allocator, fields.Length);

        var job = new SurfacesJob
        {
            FieldToPixelCoords = fieldToPixelCoords,
            YToPixelSurface = yToPixelSurface,
            FieldToSurface = fieldToSurface,
        };

        job.Schedule(fields.Length, 64).Complete();

        FixSurfaces(fieldToSurface);

        fieldToPixelCoords.DisposeDepth1();
        yToPixelSurface.Dispose();

        return fieldToSurface;
    }

    static RawArray<double> GetYToPixelSurface(int2 textureSize)
    {
        var yToPixelSurface = new RawArray<double>(ALLOCATOR, textureSize.y);

        for (int y = 0; y < textureSize.y; y++)
        {
            var edgeCoordLB = new int2(0, y);
            var edgeCoordRB = new int2(1, y);
            var edgeCoordLT = new int2(0, y + 1);
            var edgeCoordRT = new int2(1, y + 1);

            var uvLB = GeoUtilitiesDouble.EdgeCoordToPlaneUv(edgeCoordLB, textureSize);
            var uvRB = GeoUtilitiesDouble.EdgeCoordToPlaneUv(edgeCoordRB, textureSize);
            var uvLT = GeoUtilitiesDouble.EdgeCoordToPlaneUv(edgeCoordLT, textureSize);
            var uvRT = GeoUtilitiesDouble.EdgeCoordToPlaneUv(edgeCoordRT, textureSize);

            double distanceBot = Distance(uvLB, uvRB);
            double distanceTop = Distance(uvLT, uvRT);

            double distanceHorizontal = (distanceBot + distanceTop) / 2;
            double distanceVertical = Distance(uvLB, uvLT);

            yToPixelSurface[y] = distanceHorizontal * distanceVertical;
        }

        return yToPixelSurface;
    }

    static double Distance(double2 uvA, double2 uvB)
    {
        var unitSphereA = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvA);
        var unitSphereB = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvB);

        double distanceRadians = GeoUtilitiesDouble.Distance(unitSphereA, unitSphereB);
        double distanceNormalized = distanceRadians / (2 * math.PI_DBL);

        return distanceNormalized * ConstData.EARTH_PERIMETER;
    }

    static void FixSurfaces(RawArray<double> fieldToSurface)
    {
        double surfaceSum = 0d;

        for (int i = 0; i < fieldToSurface.Length; i++)
        {
            surfaceSum += fieldToSurface[i];
        }

        double surfaceReal = 4d * math.PI_DBL * ConstData.EARTH_RADIUS * ConstData.EARTH_RADIUS;
        double surfaceSumToRealRatio = surfaceReal / surfaceSum;

        Debug.Log($"SurfacesUtility :: FixSurfaces :: Ratio: {surfaceSumToRealRatio}");

        for (int i = 0; i < fieldToSurface.Length; i++)
        {
            fieldToSurface[i] *= surfaceSumToRealRatio;
        }
    }
}
