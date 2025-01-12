using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public static class GeoUtilities
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GeoCoordsToUnitSphere(float2 geoCoords)
    {
        float r = math.cos(geoCoords.y);
        float x = -math.sin(geoCoords.x) * r;
        float y = -math.sin(geoCoords.y);
        float z = math.cos(geoCoords.x) * r;

        return new float3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 UnitSphereToGeoCoords(float3 unitSphere)
    {
        float lonRad = math.atan2(-unitSphere.x, unitSphere.z);
        float latRad = math.asin(-unitSphere.y);

        return new float2(lonRad, latRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 GeoCoordsToPlaneUv(float2 geoCoords)
    {
        float u = (geoCoords.x / (2.0f * math.PI)) + 0.5f;
        float v = 0.5f - (geoCoords.y / math.PI);

        return new float2(u, v);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 PlaneUvToGeoCoords(float2 uv)
    {
        float lonRad = (uv.x - 0.5f) * 2f * math.PI;
        float latRad = (0.5f - uv.y) * math.PI;

        return new float2(lonRad, latRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 PlaneUvToUnitSphere(float2 planeUv)
    {
        return GeoCoordsToUnitSphere(PlaneUvToGeoCoords(planeUv));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 UnitSphereToPlaneUv(float3 unitSphere)
    {
        return GeoCoordsToPlaneUv(UnitSphereToGeoCoords(unitSphere));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlaneUvToBoth(float2 planeUv, out float2 geoCoords, out float3 unitSphere)
    {
        geoCoords = PlaneUvToGeoCoords(planeUv);
        unitSphere = GeoCoordsToUnitSphere(geoCoords);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnitSphereToBoth(float3 unitSphere, out float2 geoCoords, out float2 planeUv)
    {
        geoCoords = UnitSphereToGeoCoords(unitSphere);
        planeUv = GeoCoordsToPlaneUv(geoCoords);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 PixelCoordToPlaneUv(int2 pixelCoord, int2 textureSize)
    {
        float x = (pixelCoord.x + 0.5f) / textureSize.x;
        float y = (pixelCoord.y + 0.5f) / textureSize.y;

        return new float2(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 PlaneUvToPixelCoord(float2 uv, int2 textureSize)
    {
        int x = (int)(uv.x * textureSize.x);
        int y = (int)(uv.y * textureSize.y);

        return TexUtilities.ClampPixelCoord(new int2(x, y), textureSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(float3 unitSphereA, float3 unitSphereB)
    {
        float dot = math.dot(unitSphereA, unitSphereB);
        dot = math.clamp(dot, -1f, 1f);

        return math.acos(dot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(float2 geoCoordA, float2 geoCoordB)
    {
        var unitSphereA = GeoCoordsToUnitSphere(geoCoordA);
        var unitSphereB = GeoCoordsToUnitSphere(geoCoordB);

        return Distance(unitSphereA, unitSphereB);
    }
}
