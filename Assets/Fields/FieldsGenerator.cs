using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class FieldsGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [Header("Centers")]
    [SerializeField] int2 _centersTextureSize;
    [SerializeField] int _centersRows;
    [SerializeField] double _centersGeoCoordsOffset;
    [SerializeField] bool _useFieldsTextureAsUsedColors;
    [SerializeField] string _savePathCenters;

    [Header("Draw")]
    [SerializeField] Texture2D _regionsMap;
    [SerializeField] Texture2D _centersMap;
    [SerializeField] Texture2D _riversMap;
    [SerializeField] int _fieldsCountMax;

    [Header("Fields")]
    [SerializeField] Texture2D _fieldsTexture;

    [Header("Centers from fields")]
    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathFields;

    [Header("Output")]
    [SerializeField] SaveTextureToFile _outputCenters;
    [SerializeField] SaveTextureToFile _outputFields;
    [SerializeField] SaveTextureToFile _outputFilled;
    [SerializeField] SaveTextureToFile _outputAdditionals;

    public void CreateCentersToFile()
    {
        var icosahedron = EarthGeneratorUtilities.GenerateIcosahedron();
        var verticesIndexesWithoutEdges = EarthGeneratorUtilities.GetVerticesIndexesWithoutEdges(_centersRows);
        var meshDatas = new MeshData[20];

        for (int i = 0; i < icosahedron.Indices.Length; i += 3)
        {
            var top = icosahedron.Vertices[icosahedron.Indices[i]];
            var botLeft = icosahedron.Vertices[icosahedron.Indices[i + 1]];
            var botRight = icosahedron.Vertices[icosahedron.Indices[i + 2]];

            meshDatas[i / 3] = EarthGeneratorUtilities.SubdivideTriangle(top, botLeft, botRight, _centersRows);
        }

        var overlapsCorners = EarthGeneratorUtilities.FindOverlapsCorners(meshDatas, _centersRows);
        var overlapsEdges = EarthGeneratorUtilities.FindOverlapsEdges(meshDatas, _centersRows);

        using var fileStream = new FileStream(_savePathCenters, FileMode.Create, FileAccess.Write);
        int centersCount = (meshDatas.Length * verticesIndexesWithoutEdges.Count) + overlapsEdges.Count + overlapsCorners.Count;

        fileStream.WriteValue(centersCount);

        for (int i = 0; i < meshDatas.Length; i++)
        {
            var vertices = meshDatas[i].Vertices;

            for (int j = 0; j < verticesIndexesWithoutEdges.Count; j++)
            {
                var vertex = math.normalize(vertices[verticesIndexesWithoutEdges[j]]);
                fileStream.WriteValue(vertex);
            }
        }

        for (int i = 0; i < overlapsEdges.Count; i++)
        {
            var vertex = math.normalize(overlapsEdges[i].GetAverageVertex(meshDatas));
            fileStream.WriteValue(vertex);
        }

        for (int i = 0; i < overlapsCorners.Count; i++)
        {
            var vertex = math.normalize(overlapsCorners[i].GetAverageVertex(meshDatas));
            fileStream.WriteValue(vertex);
        }

        Debug.Log("Centers count: " + centersCount.ToString("### ### ###"));
    }

    public void CreateCenters()
    {
        var icosahedron = EarthGeneratorUtilities.GenerateIcosahedron();
        var verticesIndexesWithoutEdges = EarthGeneratorUtilities.GetVerticesIndexesWithoutEdges(_centersRows);
        var pixels = new Color32[_centersTextureSize.x * _centersTextureSize.y];
        var meshDatas = new MeshData[20];

        for (int i = 0; i < icosahedron.Indices.Length; i += 3)
        {
            var top = icosahedron.Vertices[icosahedron.Indices[i]];
            var botLeft = icosahedron.Vertices[icosahedron.Indices[i + 1]];
            var botRight = icosahedron.Vertices[icosahedron.Indices[i + 2]];

            meshDatas[i / 3] = EarthGeneratorUtilities.SubdivideTriangle(top, botLeft, botRight, _centersRows);
        }

        var overlapsCorners = EarthGeneratorUtilities.FindOverlapsCorners(meshDatas, _centersRows);
        var overlapsEdges = EarthGeneratorUtilities.FindOverlapsEdges(meshDatas, _centersRows);

        double centersGeoCoordsOffsetMag = _centersGeoCoordsOffset / _centersTextureSize.x;
        var colorsUsed = CreateUsedColors(_useFieldsTextureAsUsedColors, meshDatas.Length * meshDatas[0].Vertices.Length, _fieldsTexture);
        int centersCount = (meshDatas.Length * verticesIndexesWithoutEdges.Count) + overlapsEdges.Count + overlapsCorners.Count;
        var random = new Unity.Mathematics.Random(1);

        for (int i = 0; i < meshDatas.Length; i++)
        {
            var vertices = meshDatas[i].Vertices;

            for (int j = 0; j < verticesIndexesWithoutEdges.Count; j++)
            {
                var vertex = vertices[verticesIndexesWithoutEdges[j]];
                SetVertex(vertex, pixels, _centersTextureSize, colorsUsed, ref random, centersGeoCoordsOffsetMag);
            }
        }

        for (int i = 0; i < overlapsEdges.Count; i++)
        {
            var vertex = overlapsEdges[i].GetAverageVertex(meshDatas);
            SetVertex(vertex, pixels, _centersTextureSize, colorsUsed, ref random, centersGeoCoordsOffsetMag);
        }

        for (int i = 0; i < overlapsCorners.Count; i++)
        {
            var vertex = overlapsCorners[i].GetAverageVertex(meshDatas);
            SetVertex(vertex, pixels, _centersTextureSize, colorsUsed, ref random, centersGeoCoordsOffsetMag);
        }

        _outputCenters.Save(pixels);

        Debug.Log("Centers count: " + centersCount.ToString("### ### ###"));
    }

    static HashSet<int> CreateUsedColors(bool useFieldsTextureAsUsedColors, int verticesCount, Texture2D fieldsTexture)
    {
        if (!useFieldsTextureAsUsedColors)
            return new HashSet<int>(verticesCount);

        var fieldsMap = fieldsTexture.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);
        var usedColors = FieldsGeneratorUtilities.GetUsedColors(fieldsMap, verticesCount * 2);
        fieldsMap.Dispose();

        return usedColors;
    }

    static void SetVertex(double3 vertex, Color32[] pixels, int2 textureSize, HashSet<int> colorsUsed, ref Unity.Mathematics.Random random, double geoCoordsOffsetMag)
    {
        var randomColor = CesColorUtilities.GetRandomColor(colorsUsed, ref random);

        var geoCoords = GeoUtilitiesDouble.UnitSphereToGeoCoords(math.normalize(vertex));
        geoCoords = OffsetGeoCoord(geoCoords, ref random, geoCoordsOffsetMag);

        var uv = GeoUtilitiesDouble.GeoCoordsToPlaneUv(geoCoords);
        var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, textureSize);
        int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);

        pixels[flat] = randomColor.ToColor32();
    }

    static double2 OffsetGeoCoord(double2 geoCoords, ref Unity.Mathematics.Random random, double geoCoordsOffsetMag)
    {
        var geoCoordsOffsetRange = new double2(geoCoordsOffsetMag, geoCoordsOffsetMag);
        var geoCoordsOffset = random.NextDouble2(-geoCoordsOffsetRange, geoCoordsOffsetRange);
        double multiplierX = math.cos(geoCoords.y + geoCoordsOffset.y);
        geoCoordsOffset.x /= (multiplierX == 0 ? 1 : multiplierX);

        return geoCoords + geoCoordsOffset;
    }

    public void DrawAdditionals()
    {
        var fieldsMap = _fieldsTexture.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);
        var additionals = FieldsGeneratorUtilities.CreateAdditionals(fieldsMap, _fieldsCountMax, _outputAdditionals.TextureSize, ALLOCATOR);

        _outputAdditionals.Save(additionals);

        fieldsMap.Dispose();
        additionals.Dispose();
    }

    public void DrawFields()
    {
        var fieldsMap = DrawInverse(out var textureSize, out var regionsMap);

        var sw = Stopwatch.StartNew();

        FieldsGeneratorUtilities.Fill(fieldsMap, regionsMap, textureSize, _fieldsCountMax, _outputAdditionals.TextureSize, ALLOCATOR, out var filled, out var additionals);

        Debug.Log($"FloodFill: {sw.Elapsed.TotalSeconds}");
        sw.Restart();

        var fieldsMapColors = fieldsMap.IntsToColors();

        _outputFields.Save(fieldsMapColors);
        _outputFilled.Save(filled);
        _outputAdditionals.Save(additionals);

        fieldsMap.Dispose();
        regionsMap.Dispose();
        filled.Dispose();
        additionals.Dispose();

        Debug.Log($"Dispose and save: {sw.Elapsed.TotalSeconds}");
    }

    public void DrawFieldsWithFieldsMap()
    {
        var fieldsMap = DrawInverse(out _, out var regionsMap);
        var fieldsMapOld = _fieldsTexture.GetRawTextureData<Color32>();
        var fieldsMapOut = new RawArray<Color32>(ALLOCATOR, fieldsMap.Length);

        for (int i = 0; i < fieldsMapOut.Length; i++)
        {
            fieldsMapOut[i] = fieldsMap[i] == -1 ? fieldsMapOld[i] : fieldsMap[i].ToColor32();
        }

        _outputFields.Save(fieldsMapOut);

        fieldsMap.Dispose();
        fieldsMapOut.Dispose();
        regionsMap.Dispose();
    }

    RawArray<int> DrawInverse(out int2 textureSize, out RawArray<int> regionsMap)
    {
        var sw = Stopwatch.StartNew();

        textureSize = new int2(_regionsMap.width, _regionsMap.height);
        var regionsRawData = _regionsMap.GetRawTextureData<Color32>();
        var centersRawData = _centersMap.GetRawTextureData<Color32>();
        var riversRawData = _riversMap.GetRawTextureData<Color32>();

        CreateMaps(regionsRawData, centersRawData, riversRawData, out regionsMap, out var centersMap, out var riversMap);

        var fieldsMap = new RawArray<int>(ALLOCATOR, -1, regionsMap.Length);
        var closed = new RawArray<bool>(ALLOCATOR, false, regionsMap.Length);
        var coordToColorField = new RawArray<int>(ALLOCATOR, regionsMap.Length);
        var queue = new RawGeoQueueTexture(textureSize.x * textureSize.y, textureSize, ALLOCATOR);

        var job = new FieldsGeneratorJobInverse
        {
            TextureSize = textureSize,
            RegionsMap = regionsMap,
            CentersMap = centersMap,
            RiversMap = riversMap,
            FieldsMap = fieldsMap,
            Closed = closed,
            Queue = queue,
            CoordToColorField = coordToColorField,
        };

        Debug.Log($"Prepare: {sw.Elapsed.TotalSeconds}");
        sw.Restart();

        job.Schedule().Complete();

        Debug.Log($"Job: {sw.Elapsed.TotalSeconds}");

        centersMap.Dispose();
        riversMap.Dispose();
        closed.Dispose();
        coordToColorField.Dispose();
        queue.Dispose();

        return fieldsMap;
    }

    public void DrawFieldsSimple()
    {
        var sw = Stopwatch.StartNew();

        var textureSize = new int2(_centersMap.width, _centersMap.height);
        var centersRawData = _centersMap.GetRawTextureData<Color32>();
        var centersMap = centersRawData.ColorsToIntsCheckAlpha(ALLOCATOR);

        var fieldsMap = new RawArray<int>(ALLOCATOR, -1, centersMap.Length);
        var closed = new RawArray<bool>(ALLOCATOR, false, centersMap.Length);
        var coordToColorField = new RawArray<int>(ALLOCATOR, centersMap.Length);
        var queue = new RawGeoQueueTexture(textureSize.x * textureSize.y, textureSize, ALLOCATOR);

        var job = new FieldsGeneratorJobInverseSimple
        {
            TextureSize = textureSize,
            CentersMap = centersMap,
            FieldsMap = fieldsMap,
            Closed = closed,
            Queue = queue,
            CoordToColorField = coordToColorField,
        };

        Debug.Log($"Prepare: {sw.Elapsed.TotalSeconds}");
        sw.Restart();

        job.Schedule().Complete();

        Debug.Log($"Job: {sw.Elapsed.TotalSeconds}");

        _outputFields.Save(fieldsMap, (color) => color.ToColor32());

        centersMap.Dispose();
        fieldsMap.Dispose();
        closed.Dispose();
        coordToColorField.Dispose();
        queue.Dispose();

        Debug.Log($"Dispose and save: {sw.Elapsed.TotalSeconds}");
    }

    public unsafe void CreateCentersFromFields()
    {
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var centersMap = new RawArray<Color32>(ALLOCATOR, default, fieldsMap.TextureSize.x * fieldsMap.TextureSize.y);

        for (int i = 0; i < fields.Length; i++)
        {
            var uv = GeoUtilitiesDouble.GeoCoordsToPlaneUv(fields.CenterGeoCoords[i]);
            var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, fieldsMap.TextureSize);
            int flat = TexUtilities.PixelCoordToFlat(pixelCoord, fieldsMap.TextureSize.x);

            centersMap[flat] = fields.Colors[i].ToColor32();
        }

        _outputCenters.Save(centersMap);

        fieldsMap.Dispose();
        fields.Dispose();
        centersMap.Dispose();
    }

    static void CreateMaps(
        NativeArray<Color32> regionsPixels, NativeArray<Color32> centersPixels, NativeArray<Color32> riversPixels,
        out RawArray<int> regionsMap, out RawArray<int> centersMap, out RawArray<bool> riversMap)
    {
        int length = regionsPixels.Length;

        regionsMap = new RawArray<int>(ALLOCATOR, length);
        centersMap = new RawArray<int>(ALLOCATOR, length);
        riversMap = new RawArray<bool>(ALLOCATOR, length);

        for (int i = 0; i < length; i++)
        {
            var centerColor = centersPixels[i];

            regionsMap[i] = regionsPixels[i].ToIndex();
            centersMap[i] = centerColor.a == 0 ? -1 : centerColor.ToIndex();
            riversMap[i] = riversPixels[i].a != 0;
        }
    }
}

[CustomEditor(typeof(FieldsGenerator))]
public sealed class FieldsPathfinderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Create centers to file"))
        {
            var rp = (FieldsGenerator)target;
            rp.CreateCentersToFile();
        }

        if (GUILayout.Button("Create centers"))
        {
            var rp = (FieldsGenerator)target;
            rp.CreateCenters();
        }

        if (GUILayout.Button("Create centers from fieldsMap"))
        {
            var rp = (FieldsGenerator)target;
            rp.CreateCentersFromFields();
        }

        if (GUILayout.Button("Draw additionals"))
        {
            var rp = (FieldsGenerator)target;
            rp.DrawAdditionals();
        }

        if (GUILayout.Button("Draw fields"))
        {
            var rp = (FieldsGenerator)target;
            rp.DrawFields();
        }

        if (GUILayout.Button("Draw fields simple"))
        {
            var rp = (FieldsGenerator)target;
            rp.DrawFieldsSimple();
        }

        if (GUILayout.Button("Draw fields with fieldsMap"))
        {
            var rp = (FieldsGenerator)target;
            rp.DrawFieldsWithFieldsMap();
        }
    }
}