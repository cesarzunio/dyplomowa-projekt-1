using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static NodesSaveUtility2;

public sealed unsafe class ClimaterUtilities
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    public const int CLIMATES_COUNT = 31;

    public static ClimatesMapOriginal LoadClimatesMapOriginal(string path, int2 textureSize, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        var array = BinarySaveUtility.ReadArraySimple<Climate>(fileStream, textureSize.x * textureSize.y, allocator);

        for (int y = 0; y < textureSize.y / 2; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                int flatBot = TexUtilities.PixelCoordToFlat(x, y, textureSize.x);
                int flatTop = TexUtilities.PixelCoordToFlat(x, textureSize.y - y - 1, textureSize.x);

                (array[flatBot], array[flatTop]) = (array[flatTop], array[flatBot]);
            }
        }

        return new ClimatesMapOriginal(textureSize, array, allocator);
    }

    public static RawArray<Climate> CreateClimates(
        ClimatesMapOriginal climatesMapOriginal, FieldsMap fieldsMap, Fields fields,
        RawArray<uint> fieldsNodesIndexes, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, Allocator allocator)
    {
        var fieldToClimate = new RawArray<Climate>(allocator, fields.Length);
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);

        var job = new ClimatesJob
        {
            ClimatesMapOriginal = climatesMapOriginal,
            Fields = fields,
            FieldsMap = fieldsMap,
            FieldToPixelCoords = fieldToPixelCoords,
            FieldToClimate = fieldToClimate,
        };

        job.Schedule(fields.Length, 64).Complete();

        ExtendClimates(fields, fieldToClimate, fieldsNodesIndexes, nodes, edges);

        fieldToPixelCoords.DisposeDepth1();

        return fieldToClimate;
    }

    static void ExtendClimates(Fields fields, RawArray<Climate> fieldToClimateFinal, RawArray<uint> fieldsNodesIndexes, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges)
    {
        var climateChanges = new Climate[fieldToClimateFinal.Length];

        while (ExtendClimatesIterate(fields, fieldToClimateFinal, fieldsNodesIndexes, nodes, edges, climateChanges)) { }
    }

    static bool ExtendClimatesIterate(Fields fields, RawArray<Climate> fieldToClimateFinal, RawArray<uint> fieldsNodesIndexes, RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges, Climate[] climateChanges)
    {
        for (int i = 0; i < fieldToClimateFinal.Length; i++)
        {
            if (!fields.IsLand[i] || fieldToClimateFinal[i] != Climate.None)
                continue;

            climateChanges[i] = GetCommonestNeighborClimate((uint)i, fieldToClimateFinal, fieldsNodesIndexes, nodes, edges);
        }

        int changes = 0;

        for (int i = 0; i < fieldToClimateFinal.Length; i++)
        {
            if (!fields.IsLand[i] || fieldToClimateFinal[i] != Climate.None || climateChanges[i] == Climate.None)
                continue;

            fieldToClimateFinal[i] = climateChanges[i];
            changes++;
        }

        return changes != 0;
    }

    static Climate GetCommonestNeighborClimate(
        uint field, RawArray<Climate> fieldToClimateOut, RawArray<uint> fieldsNodesIndexes,
        RawArray<NodeSerialized> nodes, RawArray<EdgeSerialized> edges)
    {
        var neighborClimateToCount = stackalloc int[CLIMATES_COUNT];

        for (int i = 0; i < CLIMATES_COUNT; i++)
        {
            neighborClimateToCount[i] = 0;
        }

        uint nodeIndex = fieldsNodesIndexes[field];
        var edgesIndexes = nodes[nodeIndex].Edges;

        for (int i = 0; i < edgesIndexes.Length; i++)
        {
            var owner = nodes[edges[edgesIndexes[i]].GetOtherNodeIndex(nodeIndex)].Owner;

            if (owner.Type == NodeOwnerType.River)
                continue;

            var climate = fieldToClimateOut[owner.Index];

            if (climate == Climate.None)
                continue;

            neighborClimateToCount[(uint)climate]++;
        }

        int countHighestIndex = -1;
        int countHighest = int.MinValue;

        for (int i = 0; i < CLIMATES_COUNT; i++)
        {
            if (countHighest < neighborClimateToCount[i])
            {
                countHighest = neighborClimateToCount[i];
                countHighestIndex = i;
            }
        }

        return countHighestIndex == -1 ? Climate.None : (Climate)(uint)countHighestIndex;
    }

    public readonly unsafe struct ClimatesMapOriginal
    {
        public readonly int2 TextureSize;

        [NativeDisableUnsafePtrRestriction]
        public readonly Climate* Array;

        readonly Allocator _allocator;

        public ClimatesMapOriginal(int2 textureSize, Climate* array, Allocator allocator)
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

    public enum Climate : byte
    {
        None = 0,

        Af = 1,
        Am = 2,
        Aw = 3,

        BWh = 4,
        BWk = 5,
        BSh = 6,
        BSk = 7,

        Csa = 8,
        Csb = 9,
        Csc = 10,
        Cwa = 11,
        Cwb = 12,
        Cwc = 13,
        Cfa = 14,
        Cfb = 15,
        Cfc = 16,

        Dsa = 17,
        Dsb = 18,
        Dsc = 19,
        Dsd = 20,
        Dwa = 21,
        Dwb = 22,
        Dwc = 23,
        Dwd = 24,
        Dfa = 25,
        Dfb = 26,
        Dfc = 27,
        Dfd = 28,

        ET = 29,
        EF = 30
    }

    public static Color32 ClimateToColor(Climate climate) => climate switch
    {
        Climate.None => new Color32(0, 0, 0, 255),
        Climate.Af => new Color32(0, 0, 255, 255),
        Climate.Am => new Color32(0, 120, 255, 255),
        Climate.Aw => new Color32(70, 170, 250, 255),
        Climate.BWh => new Color32(255, 0, 0, 255),
        Climate.BWk => new Color32(255, 150, 150, 255),
        Climate.BSh => new Color32(245, 165, 0, 255),
        Climate.BSk => new Color32(255, 220, 100, 255),
        Climate.Csa => new Color32(255, 255, 0, 255),
        Climate.Csb => new Color32(200, 200, 0, 255),
        Climate.Csc => new Color32(150, 150, 0, 255),
        Climate.Cwa => new Color32(150, 255, 150, 255),
        Climate.Cwb => new Color32(100, 200, 100, 255),
        Climate.Cwc => new Color32(50, 150, 50, 255),
        Climate.Cfa => new Color32(200, 255, 80, 255),
        Climate.Cfb => new Color32(100, 255, 80, 255),
        Climate.Cfc => new Color32(50, 200, 0, 255),
        Climate.Dsa => new Color32(255, 0, 255, 255),
        Climate.Dsb => new Color32(200, 0, 200, 255),
        Climate.Dsc => new Color32(150, 50, 150, 255),
        Climate.Dsd => new Color32(150, 100, 150, 255),
        Climate.Dwa => new Color32(170, 175, 255, 255),
        Climate.Dwb => new Color32(90, 120, 220, 255),
        Climate.Dwc => new Color32(75, 80, 180, 255),
        Climate.Dwd => new Color32(50, 0, 135, 255),
        Climate.Dfa => new Color32(0, 255, 255, 255),
        Climate.Dfb => new Color32(55, 200, 255, 255),
        Climate.Dfc => new Color32(0, 125, 125, 255),
        Climate.Dfd => new Color32(0, 70, 95, 255),
        Climate.ET => new Color32(178, 178, 178, 255),
        Climate.EF => new Color32(102, 102, 102, 255),

        _ => throw new Exception($"Cannot match Climate: {climate}"),
    };


}