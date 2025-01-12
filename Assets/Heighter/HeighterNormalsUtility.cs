using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static unsafe class HeighterNormalsUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static Color[] CreateNormals(NativeArray<byte> landMap, NativeArray<byte> heightTextureLand, NativeArray<byte> heightTextureWater, int2 textureSize, double scale)
    {
        var heightMap = CreateHeightMap(landMap, heightTextureLand, heightTextureWater);
        var normals = new Color[landMap.Length];

        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);
                var neighbors = new Neighbors4(pixelCoord, textureSize);
                var neighborsHeights = new Neighbors4Heights(neighbors, textureSize.x, heightMap);

                double dx = (neighborsHeights.Right - neighborsHeights.Left) / Distance(neighbors.Right, neighbors.Left, textureSize);
                double dy = (neighborsHeights.Down - neighborsHeights.Up) / Distance(neighbors.Up, neighbors.Down, textureSize);

                dx *= scale;
                dy *= scale;

                var normal = DeltaHeightsToNormal(dx, dy);

                normals[flat] = NormalToColor(normal);
            }
        }

        heightMap.Dispose();

        return normals;
    }

    static RawArray<double> CreateHeightMap(NativeArray<byte> landMap, NativeArray<byte> heightTextureLand, NativeArray<byte> heightTextureWater)
    {
        var heightMap = new RawArray<double>(ALLOCATOR, landMap.Length);

        for (int i = 0; i < heightMap.Length; i++)
        {
            heightMap[i] = landMap[i] switch
            {
                0 => math.lerp(ConstData.EARTH_LOWEST, 0.0, heightTextureWater[i] / 255.0),
                _ => math.lerp(0.0, ConstData.EARTH_HIGHEST, heightTextureLand[i] / 255.0)
            };
        }

        return heightMap;
    }

    static double Distance(int2 pixelCoordA, int2 pixelCoordB, int2 textureSize)
    {
        var uvA = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordA, textureSize);
        var uvB = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordB, textureSize);

        var unitSphereA = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvA);
        var unitSphereB = GeoUtilitiesDouble.PlaneUvToUnitSphere(uvB);

        double distanceRadians = GeoUtilitiesDouble.Distance(unitSphereA, unitSphereB);
        double distanceNormalized = distanceRadians / (2 * math.PI_DBL);

        return distanceNormalized * ConstData.EARTH_PERIMETER;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static double3 DeltaHeightsToNormal(double dx, double dy)
    {
        return math.normalize(new double3(dx, dy, 1.0));
    }

    static Color NormalToColor(double3 normal)
    {
        normal = (normal + 1.0) * 0.5;

        return new Color
        {
            r = (float)normal.x,
            g = (float)normal.y,
            b = (float)normal.z,
            a = 1f
        };
    }

    readonly struct Neighbors4Heights
    {
        public readonly double Left;
        public readonly double Right;
        public readonly double Down;
        public readonly double Up;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Neighbors4Heights(Neighbors4 neighbors4, int textureSizeX, RawArray<double> heightMap)
        {
            int leftFlat = TexUtilities.PixelCoordToFlat(neighbors4.Left, textureSizeX);
            int rightFlat = TexUtilities.PixelCoordToFlat(neighbors4.Right, textureSizeX);
            int downFlat = TexUtilities.PixelCoordToFlat(neighbors4.Down, textureSizeX);
            int upFlat = TexUtilities.PixelCoordToFlat(neighbors4.Up, textureSizeX);

            Left = heightMap[leftFlat];
            Right = heightMap[rightFlat];
            Down = heightMap[downFlat];
            Up = heightMap[upFlat];
        }
    }
}