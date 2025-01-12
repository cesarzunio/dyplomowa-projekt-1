using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using static FinalizerSaves;

public static unsafe class EntitiesGeneratorUtility
{
    public static void SaveEntities(string path, RawArray<int> entitiesMap, int2 textureSize, Fields fields)
    {
        GetEntityColorToIndex(entitiesMap, out var entityColorToIndex, out var indexToEntityColor);
        var entityToFields = GetEntityToFields(entitiesMap, textureSize, fields, entityColorToIndex, indexToEntityColor);

        SaveEntityToFields(path, entityToFields);
    }

    static void GetEntityColorToIndex(RawArray<int> entitiesMap, out Dictionary<int, uint> entityColorToIndex, out List<int> indexToEntityColor)
    {
        entityColorToIndex = new Dictionary<int, uint>(1024);
        indexToEntityColor = new List<int>(1024);

        uint entityIt = 0;

        for (int i = 0; i < entitiesMap.Length; i++)
        {
            if (entitiesMap[i] == -1)
                continue;

            if (entityColorToIndex.ContainsKey(entitiesMap[i]))
                continue;

            entityColorToIndex[entitiesMap[i]] = entityIt++;
            indexToEntityColor.Add(entitiesMap[i]);
        }
    }

    static EntityToFields[] GetEntityToFields(RawArray<int> entitiesMap, int2 textureSize, Fields fields, Dictionary<int, uint> entityColorToIndex, List<int> indexToEntityColor)
    {
        var entityToFields = new EntityToFields[entityColorToIndex.Count];
        uint length = (uint)fields.Length;

        for (int i = 0; i < entityToFields.Length; i++)
        {
            entityToFields[i] = EntityToFields.New();
        }

        for (uint i = 0; i < length; i++)
        {
            if (!fields.IsLand[i])
                continue;

            var uv = GeoUtilitiesDouble.GeoCoordsToPlaneUv(fields.CenterGeoCoords[i]);
            var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, textureSize);
            int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);

            int entityColor = entitiesMap[flat];

            if (entityColor == -1)
                continue;

            entityToFields[entityColorToIndex[entityColor]].Add(i);
        }

        for (int i = 0; i < entityToFields.Length; i++)
        {
            if (entityToFields[i].Fields.Count == 0)
                Debug.LogError($"EntitiesGeneratorUtility :: GetEntityToFields :: Entity {i} {indexToEntityColor[i]} no fields!");
        }

        return entityToFields;
    }

    static void SaveEntityToFields(string path, EntityToFields[] entityToFields)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(entityToFields.Length);

        for (int i = 0; i < entityToFields.Length; i++)
        {
            fileStream.WriteValue(entityToFields[i].Fields.Count);

            for (int j = 0; j < entityToFields[i].Fields.Count; j++)
            {
                fileStream.WriteValue(entityToFields[i].Fields[j]);
            }
        }
    }

    struct EntityToFields
    {
        public List<uint> Fields;

        public static EntityToFields New() => new()
        {
            Fields = new List<uint>()
        };

        public readonly void Add(uint field) => Fields.Add(field);
    }
}
