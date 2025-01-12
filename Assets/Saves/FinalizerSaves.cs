using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static BordersContainers;
using static FinalizerUtilities;

public static unsafe class FinalizerSaves
{
    public static void SaveFieldsMap(string path, RawArray<int> fieldsMap, int2 textureSize, Dictionary<int, int> colorToIndex)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(textureSize.x);
        fileStream.WriteValue(textureSize.y);

        for (int i = 0; i < fieldsMap.Length; i++)
        {
            fileStream.WriteValue((uint)colorToIndex[fieldsMap[i]]);
        }
    }

    public static FieldsMap LoadFieldsMap(string path, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int width = binaryReader.ReadInt32();
        int height = binaryReader.ReadInt32();

        var fields = BinarySaveUtility.ReadArraySimple<uint>(fileStream, width * height, allocator);

        return new FieldsMap(new int2(width, height), fields, allocator);
    }

    public readonly struct FieldsMap
    {
        public readonly int2 TextureSize;

        [NativeDisableUnsafePtrRestriction]
        public readonly uint* Fields;

        public readonly Allocator Allocator;

        public FieldsMap(int2 textureSize, uint* fields, Allocator allocator)
        {
            TextureSize = textureSize;
            Fields = fields;
            Allocator = allocator;
        }

        public void Dispose()
        {
            UnsafeUtility.Free(Fields, Allocator);
        }
    }

    public static void SaveFields(string path, RawArray<int> regionsMap, int2 textureSize, RawArray<double2> indexToCenterGeoCoords, List<int> indexToColor)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        var riversColors = RiversColorsInts.Create();
        int count = indexToCenterGeoCoords.Length;

        fileStream.WriteValue(count);

        for (int i = 0; i < count; i++)
        {
            fileStream.WriteValue(indexToCenterGeoCoords[i]);
        }

        for (int i = 0; i < count; i++)
        {
            var uv = GeoUtilitiesDouble.GeoCoordsToPlaneUv(indexToCenterGeoCoords[i]);
            var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, textureSize);
            int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);

            fileStream.WriteValue(regionsMap[flat] != riversColors.Water);
        }

        for (int i = 0; i < count; i++)
        {
            fileStream.WriteValue(indexToColor[i]);
        }
    }

    public static Fields LoadFields(string path, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int length = binaryReader.ReadInt32();

        var centerGeoCoords = BinarySaveUtility.ReadArraySimple<double2>(fileStream, length, allocator);
        var isLand = BinarySaveUtility.ReadArraySimple<bool>(fileStream, length, allocator);
        var colors = BinarySaveUtility.ReadArraySimple<int>(fileStream, length, allocator);

        return new Fields(length, centerGeoCoords, isLand, colors, allocator);
    }

    public readonly struct Fields
    {
        public readonly int Length;

        [NativeDisableUnsafePtrRestriction]
        public readonly double2* CenterGeoCoords;

        [NativeDisableUnsafePtrRestriction]
        public readonly bool* IsLand;

        [NativeDisableUnsafePtrRestriction]
        public readonly int* Colors;

        public readonly Allocator Allocator;

        public Fields(int length, double2* ceterGeoCoords, bool* isLand, int* color, Allocator allocator)
        {
            Length = length;
            CenterGeoCoords = ceterGeoCoords;
            IsLand = isLand;
            Colors = color;
            Allocator = allocator;
        }

        public void Dispose()
        {
            UnsafeUtility.Free(IsLand, Allocator);
            UnsafeUtility.Free(Colors, Allocator);
        }
    }

    public static void SaveBorders(string path, RawArray<BorderSorted> bordersSorted, Dictionary<int, int> colorToIndex)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        int length = bordersSorted.Length;

        fileStream.WriteValue(length);

        for (int i = 0; i < length; i++)
        {
            var borderSorted = bordersSorted[i];

            uint fieldA = (uint)colorToIndex[borderSorted.FieldColorA];
            uint fieldB = (uint)colorToIndex[borderSorted.FieldColorB];

            fileStream.WriteValue(fieldA);
            fileStream.WriteValue(fieldB);
        }

        for (int i = 0; i < length; i++)
        {
            var borderSorted = bordersSorted[i];

            fileStream.WriteValue(borderSorted.BorderCoords.Count);

            for (int j = 0; j < borderSorted.BorderCoords.Count; j++)
            {
                BinarySaveUtility.WriteRawContainerSimple<RawBag<int2>, int2>(fileStream, borderSorted.BorderCoords[j]);
            }
        }
    }

    public static Borders LoadBorders(string path, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int length = binaryReader.ReadInt32();
        var fields = BinarySaveUtility.ReadArraySimple<uint2>(fileStream, length, allocator);

        var borderCoords = CesMemoryUtility.Allocate<RawArray<RawArray<int2>>>(length, allocator);
        *borderCoords = new RawArray<RawArray<int2>>(allocator, length);

        for (int i = 0; i < length; i++)
        {
            var borderCoordsInnerLength = binaryReader.ReadInt32();
            borderCoords[i] = new RawArray<RawArray<int2>>(allocator, borderCoordsInnerLength);

            for (int j = 0; j < borderCoordsInnerLength; j++)
            {
                borderCoords[i][j] = BinarySaveUtility.ReadRawArray<int2>(fileStream, binaryReader, allocator);
            }
        }

        return new Borders(length, fields, borderCoords, allocator);
    }

    public readonly struct Borders
    {
        public readonly int Length;
        public readonly uint2* Fields;
        public readonly RawArray<RawArray<int2>>* BorderCoords;
        readonly Allocator _allocator;

        public Borders(int length, uint2* fields, RawArray<RawArray<int2>>* borderCoords, Allocator allocator)
        {
            Length = length;
            Fields = fields;
            BorderCoords = borderCoords;
            _allocator = allocator;
        }

        public void Dispose()
        {
            UnsafeUtility.Free(Fields, _allocator);

            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < BorderCoords[i].Length; j++)
                {
                    BorderCoords[i][j].Dispose();
                }

                BorderCoords[i].Dispose();
            }

            UnsafeUtility.Free(BorderCoords, _allocator);
        }
    }

    public readonly struct Rivers
    {
        public readonly int Length;
        public readonly River* RiversArray;
        readonly Allocator _allocator;

        public Rivers(int length, River* riversArray, Allocator allocator)
        {
            Length = length;
            RiversArray = riversArray;
            _allocator = allocator;
        }

        public void Dispose()
        {
            for (int i = 0; i < Length; i++)
            {
                RiversArray[i].Dispose();
            }

            UnsafeUtility.Free(RiversArray, _allocator);
        }
    }

    public readonly struct River
    {
        public readonly RawArray<int2> RiverCoords;
        public readonly RawArray<int2> RiverPaths;

        public River(RawArray<int2> riverCoords, RawArray<int2> riverPaths)
        {
            RiverCoords = riverCoords;
            RiverPaths = riverPaths;
        }

        public void Dispose()
        {
            RiverCoords.Dispose();
            RiverPaths.Dispose();
        }
    }
}
