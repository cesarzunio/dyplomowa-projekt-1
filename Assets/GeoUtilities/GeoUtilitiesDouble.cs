using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

public static class GeoUtilitiesDouble
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double3 GeoCoordsToUnitSphere(double2 geoCoords)
    {
        double r = math.cos(geoCoords.y);
        double x = -math.sin(geoCoords.x) * r;
        double y = -math.sin(geoCoords.y);
        double z = math.cos(geoCoords.x) * r;

        return new double3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 UnitSphereToGeoCoords(double3 unitSphere)
    {
        double lonRad = math.atan2(-unitSphere.x, unitSphere.z);
        double latRad = math.asin(-unitSphere.y);

        return new double2(lonRad, latRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 GeoCoordsToPlaneUv(double2 geoCoords)
    {
        double u = (geoCoords.x / (2.0 * math.PI_DBL)) + 0.5;
        double v = 0.5 - (geoCoords.y / math.PI_DBL);

        return new double2(u, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 PlaneUvToGeoCoords(double2 uv)
    {
        double lonRad = (uv.x - 0.5) * 2.0 * math.PI_DBL;
        double latRad = (0.5 - uv.y) * math.PI_DBL;

        return new double2(lonRad, latRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double3 PlaneUvToUnitSphere(double2 planeUv)
    {
        return GeoCoordsToUnitSphere(PlaneUvToGeoCoords(planeUv));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 UnitSphereToPlaneUv(double3 unitSphere)
    {
        return GeoCoordsToPlaneUv(UnitSphereToGeoCoords(unitSphere));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlaneUvToBoth(double2 planeUv, out double2 geoCoords, out double3 unitSphere)
    {
        geoCoords = PlaneUvToGeoCoords(planeUv);
        unitSphere = GeoCoordsToUnitSphere(geoCoords);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnitSphereToBoth(double3 unitSphere, out double2 geoCoords, out double2 planeUv)
    {
        geoCoords = UnitSphereToGeoCoords(unitSphere);
        planeUv = GeoCoordsToPlaneUv(geoCoords);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 PixelCoordToPlaneUv(int2 pixelCoord, int2 textureSize)
    {
        double x = (pixelCoord.x + 0.5) / textureSize.x;
        double y = (pixelCoord.y + 0.5) / textureSize.y;

        return new double2(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 EdgeCoordToPlaneUv(int2 edgeCoord, int2 textureSize)
    {
        double x = edgeCoord.x / (double)textureSize.x;
        double y = edgeCoord.y / (double)textureSize.y;

        return new double2(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 EdgeCoordToPlaneUv(double2 edgeCoord, int2 textureSize)
    {
        double x = edgeCoord.x / textureSize.x;
        double y = edgeCoord.y / textureSize.y;

        return new double2(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 PlaneUvToPixelCoord(double2 uv, int2 textureSize)
    {
        int x = (int)(uv.x * textureSize.x);
        int y = (int)(uv.y * textureSize.y);

        return TexUtilities.ClampPixelCoord(new int2(x, y), textureSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(double3 unitSphereA, double3 unitSphereB)
    {
        double dot = math.dot(unitSphereA, unitSphereB);
        dot = math.clamp(dot, -1.0, 1.0);

        return math.acos(dot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(double2 geoCoordA, double2 geoCoordB)
    {
        var unitSphereA = GeoCoordsToUnitSphere(geoCoordA);
        var unitSphereB = GeoCoordsToUnitSphere(geoCoordB);

        return Distance(unitSphereA, unitSphereB);
    }
}
