using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;

public static unsafe class PopsUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    const int NEIGHBORS = 8;
    const int HASH_CAPACITY = 8192;

    public static void SaveFieldsPops(string path, float[] fieldsPops)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(fieldsPops.Length);

        for (int i = 0; i < fieldsPops.Length; i++)
        {
            fileStream.WriteValue(fieldsPops[i]);
        }
    }

    public static void SaveFieldsPopsMap(string path, float[] fieldsPops, FieldsMap fieldsMap)
    {
        float max = float.MinValue;

        for (int i = 0; i < fieldsPops.Length; i++)
        {
            max = math.max(max, fieldsPops[i]);
        }

        TextureSaver.Save(fieldsMap.TextureSize, path, (i) =>
        {
            uint field = fieldsMap.Fields[i];
            float pops = fieldsPops[field] / max;
            byte b = CesColorUtilities.Float01ToByte(pops);
            return new Color32(b, b, b, 255);
        });
    }

    public static PopsTextureOriginal LoadPopsTextureOriginal(string path, int2 textureSize, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        int length = textureSize.x * textureSize.y;
        var popsTop = BinarySaveUtility.ReadArraySimple<float>(fileStream, length / 2, allocator);
        var popsBot = BinarySaveUtility.ReadArraySimple<float>(fileStream, length / 2, allocator);

        return new PopsTextureOriginal(textureSize, popsTop, popsBot, allocator);
    }

    public static double CalculateCostMax(int distanceInPixelsOnEquatorMax, int2 textureSize)
    {
        var pixelCoordA = new int2(0, textureSize.y);
        var pixelCoordB = new int2(distanceInPixelsOnEquatorMax, textureSize.y);

        var uvA = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordA, textureSize);
        var uvB = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordB, textureSize);

        var geoCoordA = GeoUtilitiesDouble.PlaneUvToGeoCoords(uvA);
        var geoCoordB = GeoUtilitiesDouble.PlaneUvToGeoCoords(uvB);

        return GeoUtilitiesDouble.Distance(geoCoordA, geoCoordB);
    }

    public static float[] GenerateFieldsPops(PopsTextureOriginal popsTextureOriginal, Fields fields, FieldsMap fieldsMap, double costMax, int originalToFieldsMapOffsetY)
    {
        var fieldsPops = new float[fields.Length];

        var popsTextureOriginalTextureSizeSample = new int2(popsTextureOriginal.TextureSize.x, popsTextureOriginal.TextureSize.x / 2);

        var queue = new RawGeoQueueTexture(HASH_CAPACITY, fieldsMap.TextureSize, ALLOCATOR);
        var closedSet = new HashSet<int2>(HASH_CAPACITY);
        var hashMap = new Dictionary<int2, int2>(HASH_CAPACITY);

        int notFoundAny = 0;

        for (int y = 0; y < popsTextureOriginal.TextureSize.y; y++)
        {
            for (int x = 0; x < popsTextureOriginal.TextureSize.x; x++)
            {
                var pixelCoordOriginal = new int2(x, y);
                float pops = popsTextureOriginal[pixelCoordOriginal];

                if (pops == 0f || pops == float.MaxValue)
                    continue;

                var pixelCoordOriginalOffset = new int2(x, y + originalToFieldsMapOffsetY);
                var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordOriginalOffset, popsTextureOriginalTextureSizeSample);
                var pixelCoordFields = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, fieldsMap.TextureSize);
                int flatFields = TexUtilities.PixelCoordToFlat(pixelCoordFields, fieldsMap.TextureSize.x);

                uint fieldIndex = fieldsMap.Fields[flatFields];

                if (fields.IsLand[fieldIndex])
                {
                    fieldsPops[fieldIndex] += pops;
                }
                else if (TryFindClosestLand(pixelCoordFields, fields, fieldsMap, costMax, ref queue, closedSet, hashMap, out uint fieldIndexClosest))
                {
                    fieldsPops[fieldIndexClosest] += pops;
                }
                else
                {
                    notFoundAny++;
                }
            }
        }

        Debug.Log($"PopsUtility :: GenerateFieldsPops :: {notFoundAny} has not been asigned");

        queue.Dispose();

        return fieldsPops;
    }

    static bool TryFindClosestLand(
        int2 pixelCoord, Fields fields, FieldsMap fieldsMap, double costMax,
        ref RawGeoQueueTexture queue, HashSet<int2> closedSet, Dictionary<int2, int2> hashMap, out uint fieldIndex)
    {
        fieldIndex = default;

        queue.Clear();
        closedSet.Clear();
        hashMap.Clear();

        var neighbors = stackalloc int2[NEIGHBORS];

        queue.Add(pixelCoord, 0.0);

        while (queue.TryPop(out var currentPixelCoord))
        {
            closedSet.Add(currentPixelCoord);

            int currentFlat = TexUtilities.PixelCoordToFlat(currentPixelCoord, fieldsMap.TextureSize.x);
            uint currentField = fieldsMap.Fields[currentFlat];
            double currentCost = queue.GetCost(currentPixelCoord);

            if (currentCost > costMax)
                return false;

            if (fields.IsLand[currentField])
            {
                fieldIndex = currentField;
                return true;
            }

            var currentUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(currentPixelCoord, fieldsMap.TextureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentUv);

            TexUtilities.GetNeighbors8(currentPixelCoord, fieldsMap.TextureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];

                if (closedSet.Contains(neighbor))
                    continue;

                int neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, fieldsMap.TextureSize.x);

                var neighborUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbor, fieldsMap.TextureSize);
                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborUv);
                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere);
                double costNew = currentCost + distance;

                if (!queue.TryGetCost(neighbor, out double cost) || costNew < cost)
                {
                    queue.AddOrUpdate(neighbor, costNew);
                    hashMap[neighbor] = currentPixelCoord;
                }
            }
        }

        return false;
    }

    public readonly struct PopsTextureOriginal
    {
        readonly public int2 TextureSize;
        readonly public float* PopsTop;
        readonly public float* PopsBot;
        readonly Allocator _allocator;

        public PopsTextureOriginal(int2 textureSize, float* popsTop, float* popsBot, Allocator allocator)
        {
            TextureSize = textureSize;
            PopsTop = popsTop;
            PopsBot = popsBot;
            _allocator = allocator;
        }

        public readonly float this[int2 pixelCoord]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (pixelCoord.y < TextureSize.y / 2)
                {
                    return PopsTop[TexUtilities.PixelCoordToFlat(pixelCoord, TextureSize.x)];
                }
                else
                {
                    var pixelCoordHalf = pixelCoord;
                    pixelCoordHalf.y -= TextureSize.y / 2;

                    return PopsBot[TexUtilities.PixelCoordToFlat(pixelCoordHalf, TextureSize.x)];
                }
            }
        }

        public readonly void Dispose()
        {
            UnsafeUtility.Free(PopsTop, _allocator);
            UnsafeUtility.Free(PopsBot, _allocator);
        }
    }
}
