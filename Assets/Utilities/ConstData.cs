using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class ConstData
{
    public const double EARTH_RADIUS = 6_371;
    public const double EARTH_SURFACE = 4d * math.PI_DBL * EARTH_RADIUS * EARTH_RADIUS;
    public const double EARTH_PERIMETER = 40_075;
    public const double EARTH_HIGHEST = 8.848;
    public const double EARTH_LOWEST = -11.034;
}
