using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static FinalizerSaves;
using static NodesSaveUtility4;

public static unsafe class TemperatureUtility
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static void SaveFieldTemperatures(string path, RawArray<FieldTemperatures> fieldsTemperatures)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(fieldsTemperatures.Length);

        for (int i = 0; i < fieldsTemperatures.Length; i++)
        {
            fixed (float* monthToTemperature = fieldsTemperatures[i].MonthToTemperature)
            {
                BinarySaveUtility.WriteArraySimple(fileStream, monthToTemperature, 12);
            }
        }
    }

    public static RawArray<FieldTemperatures> GenerateFieldTemperatures(
        Fields fields, FieldsMap fieldsMap, string pathPrefix, int2 temperatureTextureOriginalSize,
        NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes, Allocator allocator)
    {
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);
        var fieldsTemperatures = new RawArray<FieldTemperatures>(allocator, fields.Length);

        for (int i = 0; i < 12; i++)
        {
            string path = $"{pathPrefix}{i + 1}.bin";
            var temperatureTextureOriginal = LoadTemperatureTextureOriginal(path, temperatureTextureOriginalSize);

            var job = new TemperatureJob
            {
                Fields = fields,
                FieldsMap = fieldsMap,
                TemperatureTexture = temperatureTextureOriginal,
                LeftBotMap = TexUtilities.FlipY(new int2(0, 8191), fieldsMap.TextureSize.y),
                RightTopMap = TexUtilities.FlipY(new int2(16383, 270), fieldsMap.TextureSize.y),
                FieldToPixelCoords = fieldToPixelCoords,
                FieldToTemperatures = fieldsTemperatures,
                MonthIndex = i,
            };

            job.Schedule(fields.Length, 64).Complete();

            temperatureTextureOriginal.Dispose();
        }

        ExtendFieldTemperatures(fieldsTemperatures, nodes, edges, fieldsNodesIndexes);

        return fieldsTemperatures;
    }

    static TemperatureTextureOriginal LoadTemperatureTextureOriginal(string path, int2 textureSize)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        long length = (long)textureSize.x * (long)textureSize.y;
        var array = BinarySaveUtility.ReadArraySimpleInParts<int>(fileStream, length, (int)(length / 2), ALLOCATOR);

        for (int y = 0; y < textureSize.y / 2; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                long flatBot = TexUtilities.PixelCoordToFlatLong(x, y, textureSize.x);
                long flatTop = TexUtilities.PixelCoordToFlatLong(x, textureSize.y - y - 1, textureSize.x);
        
                (array[flatBot], array[flatTop]) = (array[flatTop], array[flatBot]);
            }
        }

        return new TemperatureTextureOriginal(textureSize, array, ALLOCATOR);
    }

    static void ExtendFieldTemperatures(
        RawArray<FieldTemperatures> fieldsTemperatures,
        NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes)
    {
        var fieldsTemperaturesChanges = new RawArray<FieldTemperatures>(ALLOCATOR, fieldsTemperatures.Length);
        UnsafeUtility.MemClear(fieldsTemperaturesChanges.Data, UnsafeUtility.SizeOf<FieldTemperatures>() * fieldsTemperatures.Length);

        for (int i = 0; i < 12; i++)
        {
            while (ExtendFieldTemperaturesIterate(fieldsTemperatures, fieldsTemperaturesChanges, i, nodes, edges, fieldsNodesIndexes)) { }
        }

        fieldsTemperaturesChanges.Dispose();
    }

    static bool ExtendFieldTemperaturesIterate(
        RawArray<FieldTemperatures> fieldsTemperatures, RawArray<FieldTemperatures> fieldsTemperaturesChanges, int monthIndex,
        NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes)
    {
        for (int i = 0; i < fieldsTemperatures.Length; i++)
        {
            if (fieldsTemperatures[i].MonthToSet[monthIndex])
                continue;

            if (GetNeighborsTemperatureAverage(i, fieldsTemperatures, monthIndex, nodes, edges, fieldsNodesIndexes, out float temperatureAverage))
            {
                fieldsTemperaturesChanges[i].MonthToTemperature[monthIndex] = temperatureAverage;
                fieldsTemperaturesChanges[i].MonthToSet[monthIndex] = true;
            }
        }

        int changes = 0;

        for (int i = 0; i < fieldsTemperatures.Length; i++)
        {
            if (!fieldsTemperatures[i].MonthToSet[monthIndex] && fieldsTemperaturesChanges[i].MonthToSet[monthIndex])
            {
                fieldsTemperatures[i].MonthToTemperature[monthIndex] = fieldsTemperaturesChanges[i].MonthToTemperature[monthIndex];
                fieldsTemperatures[i].MonthToSet[monthIndex] = true;

                changes++;
            }
        }

        return changes != 0;
    }

    static bool GetNeighborsTemperatureAverage(
        int fieldIndex, RawArray<FieldTemperatures> fieldsTemperatures, int monthIndex,
        NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes, out float temperatureAverage)
    {
        temperatureAverage = default;

        float temperatureSum = 0f;
        int temperatureCount = 0;

        uint nodeIndex = fieldsNodesIndexes[fieldIndex];
        ref readonly var nodeEdges = ref nodes.EdgesIndexes[nodeIndex];

        for (int i = 0; i < nodeEdges.Length; i++)
        {
            ref readonly var edgeNodesIndexes = ref edges.NodesIndexes[nodeEdges[i]];
            uint nodeIndexOther = edgeNodesIndexes.x ^ edgeNodesIndexes.y ^ nodeIndex;
            ref var nodeOtherOwner = ref nodes.Owner[nodeIndexOther];

            if (nodeOtherOwner.Type == NodeOwnerType.River)
                continue;

            uint fieldIndexOther = nodeOtherOwner.Index;

            if (!fieldsTemperatures[fieldIndexOther].MonthToSet[monthIndex])
                continue;

            temperatureSum += fieldsTemperatures[fieldIndexOther].MonthToTemperature[monthIndex];
            temperatureCount++;
        }

        if (temperatureCount == 0)
            return false;

        temperatureAverage = temperatureSum / temperatureCount;
        return true;
    }

    public readonly unsafe struct TemperatureTextureOriginal
    {
        public readonly int2 TextureSize;

        [NativeDisableUnsafePtrRestriction]
        public readonly int* Array;

        readonly Allocator _allocator;

        public TemperatureTextureOriginal(int2 textureSize, int* array, Allocator allocator)
        {
            TextureSize = textureSize;
            Array = array;
            _allocator = allocator;
        }

        public readonly void Dispose()
        {
            UnsafeUtility.Free(Array, _allocator);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct FieldTemperatures
    {
        const int LENGTH = 12;

        [NativeDisableUnsafePtrRestriction]
        public fixed float MonthToTemperature[LENGTH];

        [NativeDisableUnsafePtrRestriction]
        public fixed bool MonthToSet[LENGTH];
    }
}
