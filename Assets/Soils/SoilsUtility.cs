using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static LandFormUtility;

public static unsafe class SoilsUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static RawArray<SoilType> GenerateSoilTypes(Fields fields, FieldsMap fieldsMap, SoilsTextureOriginal soilsTextureOriginal, Allocator allocator)
    {
        var fieldToSoilType = new RawArray<SoilType>(allocator, SoilType.None, fields.Length);
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);

        var job = new SoilsJob
        {
            Fields = fields,
            FieldsMap = fieldsMap,
            LeftBotMap = TexUtilities.FlipY(new int2(0, 6878), fieldsMap.TextureSize.y),
            RightTopMap = TexUtilities.FlipY(new int2(16698, 270), fieldsMap.TextureSize.y),
            SoilsTextureOriginal = soilsTextureOriginal,
            FieldToSoilType = fieldToSoilType,
            FieldToPixelCoords = fieldToPixelCoords,
        };

        job.Schedule(fieldToSoilType.Length, 64).Complete();

        fieldToPixelCoords.DisposeDepth1();

        return fieldToSoilType;
    }

    public static SoilsTextureOriginal LoadSoilsTextureOriginal(string path, int2 textureSize, Allocator allocator)
    {
        long soilsArrayLength = (long)textureSize.x * (long)textureSize.y;
        int soilsArrayPartLength = (int)(soilsArrayLength / 10);
        var soilsArray = BinarySaveUtility.ReadArraySimpleInParts<SoilType>(path, soilsArrayLength, soilsArrayPartLength, allocator);

        for (int y = 0; y < textureSize.y / 2; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                long flatBot = TexUtilities.PixelCoordToFlatLong(x, y, textureSize.x);
                long flatTop = TexUtilities.PixelCoordToFlatLong(x, textureSize.y - y - 1, textureSize.x);

                (soilsArray[flatBot], soilsArray[flatTop]) = (soilsArray[flatTop], soilsArray[flatBot]);
            }
        }

        return new SoilsTextureOriginal(textureSize, soilsArray, allocator);
    }

    public readonly struct SoilsTextureOriginal
    {
        public readonly int2 TextureSize;

        [NativeDisableUnsafePtrRestriction]
        public readonly SoilType* Array;

        readonly Allocator _allocator;

        public SoilsTextureOriginal(int2 textureSize, SoilType* array, Allocator allocator)
        {
            TextureSize = textureSize;
            Array = array;
            _allocator = allocator;
        }

        public void Dispose()
        {
            UnsafeUtility.Free(Array, _allocator);
        }
    }

    public enum SoilType : byte
    {
        Acrisols = 0,
        Albeluvisols = 1,
        Alisols = 2,
        Andosols = 3,
        Arenosols = 4,
        Calcisols = 5,
        Cambisols = 6,
        Chernozems = 7,
        Cryosols = 8,
        Durisols = 9,
        Ferralsols = 10,
        Fluvisols = 11,
        Gleysols = 12,
        Gypsisols = 13,
        Histosols = 14,
        Kastanozems = 15,
        Leptosols = 16,
        Lixisols = 17,
        Luvisols = 18,
        Nitisols = 19,
        Phaeozems = 20,
        Planosols = 21,
        Plinthosols = 22,
        Podzols = 23,
        Regosols = 24,
        Solonchaks = 25,
        Solonetz = 26,
        Stagnosols = 27,
        Umbrisols = 28,
        Vertisols = 29,

        None = 255,
    }
}
