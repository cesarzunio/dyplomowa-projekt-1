using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;
using static NodesSaveUtility4;

public static unsafe class PopsUtility2
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    const int NEIGHBORS_COUNT = 128;
    const float POPULATION_2000 = 6_171_702_993f;

    public static void SaveFieldsPops(string path, RawArray<float> fieldToPops)
    {
        BinarySaveUtility.WriteRawContainerSimple<RawArray<float>, float>(path, fieldToPops);
    }

    public static void SaveFieldsPopsMap(string path, RawArray<float> fieldsPops, FieldsMap fieldsMap)
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

    public static PopsTexture LoadPopsTexture(string path, int2 textureSize, int originalToFieldsMapOffsetY, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        int lengthOriginal = textureSize.x * textureSize.y;
        var popsTop = BinarySaveUtility.ReadArraySimple<float>(fileStream, lengthOriginal / 2, ALLOCATOR);
        var popsBot = BinarySaveUtility.ReadArraySimple<float>(fileStream, lengthOriginal / 2, ALLOCATOR);

        var textureSizeNew = new int2(textureSize.x, textureSize.x / 2);
        int lengthNew = textureSizeNew.x * textureSizeNew.y;
        var pops = CesMemoryUtility.Allocate<float>(lengthNew, allocator, true);

        int it = originalToFieldsMapOffsetY * textureSizeNew.x;

        for (int i = 0; i < lengthOriginal / 2; i++)
        {
            pops[it++] = GetPops(popsTop[i]);
        }

        for (int i = 0; i < lengthOriginal / 2; i++)
        {
            pops[it++] = GetPops(popsBot[i]);
        }

        UnsafeUtility.Free(popsTop, ALLOCATOR);
        UnsafeUtility.Free(popsBot, ALLOCATOR);

        return new PopsTexture(textureSizeNew, pops, allocator);

        // ----

        static float GetPops(float pops) => pops == float.MaxValue ? 0f : pops;
    }

    public static RawArray<float> GenerateFieldsPops(
        Fields fields, FieldsMap fieldsMap, PopsTexture popsTexture,
        NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes, Allocator allocator)
    {
        var fieldToPops = new RawArray<float>(allocator, 0f, fields.Length);
        var fieldToPixelCoords = FinalizerUtilities.GetFieldToPixelCoords(fields.Length, fieldsMap, ALLOCATOR);

        var job = new PopsJob
        {
            FieldsMap = fieldsMap,
            PopsTexture = popsTexture,
            FieldToPixelCoords = fieldToPixelCoords,
            FieldToPops = fieldToPops,
        };

        job.Schedule(fieldToPops.Length, 64).Complete();

        RedistributeWaterPops(fields, fieldToPops, nodes, edges, fieldsNodesIndexes);
        FixPopsAmount(fieldToPops);

        fieldToPixelCoords.DisposeDepth1();

        return fieldToPops;
    }

    static void RedistributeWaterPops(Fields fields, RawArray<float> fieldToPops, NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes)
    {
        var neighborsPtr = stackalloc uint[NEIGHBORS_COUNT];
        var neighbors = new RawListStackalloc<uint>(neighborsPtr, NEIGHBORS_COUNT);

        int invalidEdgeNodesIndexes = 0;

        for (int i = 0; i < fieldToPops.Length; i++)
        {
            if (fields.IsLand[i] || fieldToPops[i] == 0f)
                continue;

            GetLandFieldNeighbors(i, ref neighbors, fields, nodes, edges, fieldsNodesIndexes, ref invalidEdgeNodesIndexes);

            float pops = fieldToPops[i];
            fieldToPops[i] = 0f;

            if (neighbors.Count == 0)
                continue;

            float neighborsPopsSum = GetNeighborsPopSum(neighbors, fieldToPops);

            if (neighborsPopsSum < 1f)
                continue;

            for (int j = 0; j < neighbors.Count; j++)
            {
                ref float neighborsPops = ref fieldToPops[neighbors[j]];
                neighborsPops += pops * (neighborsPops / neighborsPopsSum);
            }
        }

        Debug.Log($"Pops :: InvalidEdgeNodesIndexes: {invalidEdgeNodesIndexes}");
    }

    static void GetLandFieldNeighbors(int fieldIndex, ref RawListStackalloc<uint> neighbors, Fields fields, NodesFinals nodes, EdgesFinals edges, RawArray<uint> fieldsNodesIndexes, ref int invalidEdgeNodesIndexes)
    {
        neighbors.Clear();

        uint nodeIndex = fieldsNodesIndexes[fieldIndex];
        ref readonly var nodeEdgesIndexes = ref nodes.EdgesIndexes[nodeIndex];

        for (int i = 0; i < nodeEdgesIndexes.Length; i++)
        {
            ref readonly var edgeNodesIndexes = ref edges.NodesIndexes[nodeEdgesIndexes[i]];
            uint otherNodeIndex = edgeNodesIndexes.x ^ edgeNodesIndexes.y ^ nodeIndex;
            ref readonly var otherNodeOwner = ref nodes.Owner[otherNodeIndex];

            if (edgeNodesIndexes.x != nodeIndex && edgeNodesIndexes.y != nodeIndex)
                invalidEdgeNodesIndexes++;

            if (otherNodeOwner.Type == NodeOwnerType.River)
                continue;

            uint otherFieldIndex = otherNodeOwner.Index;

            if (fields.IsLand[otherFieldIndex])
            {
                neighbors.Add(otherFieldIndex);
            }
        }
    }

    static float GetNeighborsPopSum(RawListStackalloc<uint> neighbors, RawArray<float> fieldToPops)
    {
        float sum = 0f;

        for (int i = 0; i < neighbors.Count; i++)
        {
            sum += fieldToPops[neighbors[i]];
        }

        return sum;
    }

    static void FixPopsAmount(RawArray<float> fieldToPops)
    {
        float sum = Sum(fieldToPops);
        float multiplier = POPULATION_2000 / sum;

        Debug.Log($"PopsUtility :: FixPopsAmount :: Before: {sum:N2}, Target: {POPULATION_2000:N2}");

        for (int i = 0; i < fieldToPops.Length; i++)
        {
            fieldToPops[i] *= multiplier;
        }

        sum = Sum(fieldToPops);

        Debug.Log($"PopsUtility :: FixPopsAmount :: After: {sum:N2}");
    }

    public static float Sum(RawArray<float> numbers)
    {
        var workingArray = new RawArray<float>(ALLOCATOR, numbers.Length);
        CesMemoryUtility.Copy(numbers.Length, workingArray.Data, numbers.Data);

        for (int stride = 1; stride < numbers.Length; stride *= 2)
        {
            for (int i = 0; i < numbers.Length; i += stride * 2)
            {
                if (i + stride < numbers.Length)
                {
                    workingArray[i] = workingArray[i] + workingArray[i + stride];
                }
            }
        }

        float sum = workingArray[0];
        workingArray.Dispose();

        return sum;
    }
}