using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using static FinalizerSaves;

public static unsafe class AreasGeneratorUtility
{
    public static void SaveAreas(string path, RawArray<int> areasMap, FieldsMap fieldsMap, Fields fields)
    {
        var fieldToAreas = GetFieldToAreas(areasMap, fieldsMap, fields);
        var fieldToAreaFinal = GetFieldToAreaFinal(fieldToAreas);
        var areaToFields = GetAreaToFields(fieldToAreaFinal);

        SaveAreaToFields(path, areaToFields);
    }

    public static RawArray<int> CreateAreas(RawArray<int> areasMap, FieldsMap fieldsMap, Fields fields, Allocator allocator)
    {
        var fieldToAreas = GetFieldToAreas(areasMap, fieldsMap, fields);

        return GetFieldToAreaFinal(fieldToAreas, allocator);
    }

    static FieldAreas[] GetFieldToAreas(RawArray<int> areasMap, FieldsMap fieldsMap, Fields fields)
    {
        var fieldToAreas = new FieldAreas[fields.Length];

        for (int i = 0; i < fieldToAreas.Length; i++)
        {
            fieldToAreas[i] = FieldAreas.New();
        }

        int mapLength = fieldsMap.TextureSize.x * fieldsMap.TextureSize.y;

        for (int i = 0; i < mapLength; i++)
        {
            fieldToAreas[fieldsMap.Fields[i]].Add(areasMap[i]);
        }

        return fieldToAreas;
    }

    static RawArray<int> GetFieldToAreaFinal(FieldAreas[] fieldToAreas, Allocator allocator)
    {
        var fieldToAreaFinal = new RawArray<int>(allocator, fieldToAreas.Length);

        for (int i = 0; i < fieldToAreas.Length; i++)
        {
            fieldToAreaFinal[i] = fieldToAreas[i].GetCommonest();
        }

        return fieldToAreaFinal;
    }

    static int[] GetFieldToAreaFinal(FieldAreas[] fieldToAreas)
    {
        var fieldToAreaFinal = new int[fieldToAreas.Length];

        for (int i = 0; i < fieldToAreas.Length; i++)
        {
            fieldToAreaFinal[i] = fieldToAreas[i].GetCommonest();
        }

        return fieldToAreaFinal;
    }

    static AreaFields[] GetAreaToFields(int[] fieldToAreaFinal)
    {
        var areaColorToIndex = GetAreaColorToIndex(fieldToAreaFinal);
        var areaToFields = new AreaFields[areaColorToIndex.Count];

        for (int i = 0; i < areaToFields.Length; i++)
        {
            areaToFields[i] = AreaFields.New();
        }

        for (int i = 0; i < fieldToAreaFinal.Length; i++)
        {
            uint areaIndex = areaColorToIndex[fieldToAreaFinal[i]];
            areaToFields[areaIndex].Add((uint)i);
        }

        return areaToFields;
    }

    static Dictionary<int, uint> GetAreaColorToIndex(int[] fieldToAreaFinal)
    {
        var areaColorToIndex = new Dictionary<int, uint>(fieldToAreaFinal.Length);
        uint areasIt = 0;

        for (int i = 0; i < fieldToAreaFinal.Length; i++)
        {
            if (areaColorToIndex.ContainsKey(fieldToAreaFinal[i]))
                continue;

            areaColorToIndex[fieldToAreaFinal[i]] = areasIt++;
        }

        return areaColorToIndex;
    }

    static void SaveAreaToFields(string path, AreaFields[] areaToFields)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(areaToFields.Length);

        for (int i = 0; i < areaToFields.Length; i++)
        {
            fileStream.WriteValue(areaToFields[i].Fields.Count);

            Debug.Log($"Areas: {i} {areaToFields[i].Fields.Count}");

            for (int j = 0; j < areaToFields[i].Fields.Count; j++)
            {
                fileStream.WriteValue(areaToFields[i].Fields[j]);
            }
        }
    }

    struct FieldAreas
    {
        Dictionary<int, uint> _areaToCount;

        public static FieldAreas New() => new()
        {
            _areaToCount = new Dictionary<int, uint>()
        };

        public readonly void Add(int area)
        {
            if (_areaToCount.TryGetValue(area, out uint count))
            {
                _areaToCount[area] = count + 1;
            }
            else
            {
                _areaToCount[area] = 1;
            }
        }

        public readonly int GetCommonest()
        {
            if (_areaToCount.Count == 0)
                throw new Exception("FieldAres :: GetCommonets :: Dictionary is empty!");

            int areaHighest = -1;
            uint countHighest = 0;

            foreach (var (area, count) in _areaToCount)
            {
                if (count > countHighest)
                {
                    areaHighest = area;
                    countHighest = count;
                }
            }

            return areaHighest;
        }
    }

    struct AreaFields
    {
        public List<uint> Fields;

        public static AreaFields New() => new()
        {
            Fields = new List<uint>()
        };

        public void Add(uint area) => Fields.Add(area);
    }
}
