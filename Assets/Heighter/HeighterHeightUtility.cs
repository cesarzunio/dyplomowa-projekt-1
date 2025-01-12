using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor.Profiling.Memory.Experimental;
using UnityEngine;
using static FinalizerSaves;

public static unsafe class HeighterHeightUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static void SaveFieldToLatitude(string path, FieldToLatitude[] fieldToLatitude)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(fieldToLatitude.Length);

        for (int i = 0; i < fieldToLatitude.Length; i++)
        {
            fileStream.WriteValue((float)fieldToLatitude[i].Height);
        }
    }

    public static FieldToLatitude[] CreateFieldsLatitudes(NativeArray<byte> landMap, NativeArray<byte> heightTextureLand, NativeArray<byte> heightTextureWater, Fields fields, FieldsMap fieldsMap)
    {
        var heightMap = CreateHeightMap(landMap, heightTextureLand, heightTextureWater);
        var fieldToLatitude = GetFieldToLatitude(heightMap, fields, fieldsMap);

        heightMap.Dispose();

        return fieldToLatitude;
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

    static FieldToLatitude[] GetFieldToLatitude(RawArray<double> heightMap, Fields fields, FieldsMap fieldsMap)
    {
        var fieldToLatitude = new FieldToLatitude[fields.Length];

        for (int i = 0; i < heightMap.Length; i++)
        {
            fieldToLatitude[fieldsMap.Fields[i]].Add(heightMap[i]);
        }

        return fieldToLatitude;
    }

    public struct FieldToLatitude
    {
        double _heightSum;
        int _heightCount;

        public readonly double Height => _heightSum / _heightCount;

        public void Add(double height)
        {
            _heightSum += height;
            _heightCount++;
        }
    }
}
