using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

public readonly struct HeuristicGeo : IHeuristicable
{
    readonly int2 _textureSize;
    readonly double3 _unitSphereTarget;

    public HeuristicGeo(int2 textureSize, int2 pixelCoordTarget)
    {
        _textureSize = textureSize;

        var uvTarget = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordTarget, textureSize);
        _unitSphereTarget = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvTarget);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetH(int2 item)
    {
        var uvItem = GeoUtilitiesDouble.PixelCoordToPlaneUv(item, _textureSize);
        var unitSphereItem = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvItem);

        return GeoUtilitiesDouble.Distance(unitSphereItem, _unitSphereTarget);
    }
}
