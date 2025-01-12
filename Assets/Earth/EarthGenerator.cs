using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Schema;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed class EarthGenerator : MonoBehaviour
{
    [Header("General")]
    [SerializeField, HideInInspector] List<MeshFilter> _meshes;
    [SerializeField] Material _material;

    [Header("Subdivisions")]
    [SerializeField] int _rowsPre;
    [SerializeField] int _rows;

    [Header("Normalize")]
    [SerializeField] bool _normalize;
    [SerializeField] float _normalizeRadius;

    //[Header("Uvs")]
    //[SerializeField] bool _createUvs;
    //[SerializeField] Texture2D _heightTex;
    //[SerializeField] bool _useHeightMap;
    //[SerializeField] AnimationCurve _heightCurve;
    //[SerializeField] float _heightMultiplier;

    [Header("Height")]
    [SerializeField] bool _useHeightTextures;
    [SerializeField] float _heightFlat;
    [SerializeField] Texture2D _regionsTex;
    [SerializeField] Texture2D _heightLandTex;
    [SerializeField] Texture2D _heightWaterTex;
    [SerializeField] Texture2D _distancesToLandTex;
    [SerializeField, Range(0f, 1f)] float _distancesToLandMul;
    [SerializeField] float3 _heightsRanges;

    [Header("Smoothing")]
    [SerializeField] bool _groupVertices;

    [Header("Save")]
    [SerializeField] int _rowsLoad;

    public void DestroyAllMeshes(bool allowDestroyingAssets)
    {
        if (Application.isPlaying)
            throw new Exception("Cannot destroy meshes in Play Mode!");

        foreach (var mf in _meshes)
        {
            GameObject.DestroyImmediate(mf.sharedMesh, allowDestroyingAssets);
            GameObject.DestroyImmediate(mf.gameObject);
        }

        _meshes.Clear();
    }

    public void Save()
    {
        string directoryPath = DirectoryPath(_rows);

        if (Directory.Exists(directoryPath))
            throw new Exception("EarthGenerator :: Save :: Directory already exists!");

        Directory.CreateDirectory(directoryPath);

        for (int i = 0; i < _meshes.Count; i++)
        {
            AssetDatabase.CreateAsset(_meshes[i].sharedMesh, $"{directoryPath}/face_{i}.asset");
        }

        AssetDatabase.SaveAssets();
    }

    public void Load()
    {
        string directoryPath = DirectoryPath(_rowsLoad);

        if (!Directory.Exists(directoryPath))
            throw new Exception("EarthGenerator :: Load :: Directory does not exists!");

        DestroyAllMeshes(true);

        var assetsGuids = AssetDatabase.FindAssets("", new string[] { directoryPath });
        var transformThis = transform;

        for (int i = 0; i < assetsGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetsGuids[i]);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            AddMesh(mesh, transformThis, i);
        }
    }

    static string DirectoryPath(int rows) => $"Assets/Saves/Faces_{rows}";

    public void GenerateSimple()
    {
        DestroyAllMeshes(false);

        var meshDatas = CreateMeshDatas();

        //if (_normalize)
        //{
        //    Normalize(meshDatas);
        //}

        CreateUvs(meshDatas);

        if (_useHeightTextures)
        {
            SetHeightTex(meshDatas);
        }
        else
        {
            SetHeightFlat(meshDatas);
        }

        CreateNormals(meshDatas);

        if (_rows > 2 && _groupVertices)
        {
            GroupVertices(meshDatas);
        }

        CreateMeshFilters(meshDatas);
    }

    MeshData[] CreateMeshDatas()
    {
        var icosahedron = EarthGeneratorUtilities.GenerateIcosahedron();
        var meshDatas = new List<MeshData>();

        for (int i = 0; i < icosahedron.Indices.Length; i += 3)
        {
            var firstPre = icosahedron.Vertices[icosahedron.Indices[i]];
            var secondPre = icosahedron.Vertices[icosahedron.Indices[i + 1]];
            var thirdPre = icosahedron.Vertices[icosahedron.Indices[i + 2]];

            var subdivPre = EarthGeneratorUtilities.SubdivideTriangle(firstPre, secondPre, thirdPre, _rowsPre);

            for (int j = 0; j < subdivPre.Indices.Length; j += 3)
            {
                var first = subdivPre.Vertices[subdivPre.Indices[j]];
                var second = subdivPre.Vertices[subdivPre.Indices[j + 1]];
                var third = subdivPre.Vertices[subdivPre.Indices[j + 2]];

                var subdiv = EarthGeneratorUtilities.SubdivideTriangle(first, second, third, _rows);

                meshDatas.Add(subdiv);
            }
        }

        return meshDatas.ToArray();
    }

    //void Normalize(MeshData[] meshDatas)
    //{
    //    for (int i = 0; i < meshDatas.Length; i++)
    //    {
    //        for (int j = 0; j < meshDatas[i].Vertices.Length; j++)
    //        {
    //            meshDatas[i].Vertices[j] = math.normalize(meshDatas[i].Vertices[j]);
    //        }
    //    }
    //}

    void CreateNormals(MeshData[] meshDatas)
    {
        for (int i = 0; i < meshDatas.Length; i++)
        {
            meshDatas[i].Normals = EarthGeneratorUtilities.CalculateNormals(meshDatas[i].Vertices, meshDatas[i].Indices);
        }
    }

    void CreateUvs(MeshData[] meshDatas)
    {
        for (int i = 0; i < meshDatas.Length; i++)
        {
            var uvs = new double2[meshDatas[i].Vertices.Length];

            for (int j = 0; j < uvs.Length; j++)
            {
                uvs[j] = GeoUtilitiesDouble.UnitSphereToPlaneUv(math.normalize(meshDatas[i].Vertices[j]));
            }

            meshDatas[i].Uvs = uvs;
        }
    }

    //void SetHeight(MeshData[] meshDatas)
    //{
    //    var textureSize = new int2(_heightTex.width, _heightTex.height);
    //    var heightMap = _heightTex.GetRawTextureData<byte>();

    //    for (int i = 0; i < meshDatas.Length; i++)
    //    {
    //        for (int j = 0; j < meshDatas[i].Vertices.Length; j++)
    //        {
    //            var uv = meshDatas[i].Uvs[j];
    //            var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, textureSize);
    //            int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);

    //            float heightValue = heightMap[flat] / 255.0f;
    //            float height = _heightCurve.Evaluate(heightValue) * _heightMultiplier;

    //            meshDatas[i].Vertices[j] = math.normalize(meshDatas[i].Vertices[j]) * (1f + height);
    //        }
    //    }
    //}

    void SetHeightFlat(MeshData[] meshDatas)
    {
        for (int i = 0; i < meshDatas.Length; i++)
        {
            for (int j = 0; j < meshDatas[i].Vertices.Length; j++)
            {
                meshDatas[i].Vertices[j] = math.normalize(meshDatas[i].Vertices[j]) * _heightFlat;
            }
        }
    }

    void SetHeightTex(MeshData[] meshDatas)
    {
        var textureSize = new int2(_regionsTex.width, _regionsTex.height);
        var regionsMap = _regionsTex.GetRawTextureData<Color32>();
        var heightLandMap = _heightLandTex.GetRawTextureData<byte>();
        var heightWaterMap = _heightWaterTex.GetRawTextureData<byte>();
        var distancesToLandTex = _distancesToLandTex.GetRawTextureData<byte>();

        for (int i = 0; i < meshDatas.Length; i++)
        {
            for (int j = 0; j < meshDatas[i].Vertices.Length; j++)
            {
                var uv = meshDatas[i].Uvs[j];
                var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, textureSize);
                int flat = TexUtilities.PixelCoordToFlat(pixelCoord, textureSize.x);

                var region = regionsMap[flat];
                int regionSum = region.r + region.g + region.b;

                var heightWater = math.lerp(_heightsRanges.x, _heightsRanges.y, heightWaterMap[flat] / 255f);
                var heightLand = math.lerp(_heightsRanges.y, _heightsRanges.z, heightLandMap[flat] / 255f);

                float distanceToLandRatio = math.saturate((distancesToLandTex[flat] / 255f) / _distancesToLandMul);

                //float height = regionSum switch
                //{
                //    0 => math.lerp(heightLand, heightWater, distanceToLandRatio),
                //    _ => heightLand,
                //};

                float height = heightLand;

                meshDatas[i].Vertices[j] = math.normalize(meshDatas[i].Vertices[j]) * height;
            }
        }
    }

    void GroupVertices(MeshData[] meshDatas)
    {
        var indiceLocPairs = EarthGeneratorUtilities.FindOverlapsEdges(meshDatas, _rows);
        var cornersGroups = EarthGeneratorUtilities.FindOverlapsCorners(meshDatas, _rows);

        VerticesGrouper.ApplyGroups(meshDatas, indiceLocPairs, cornersGroups);
    }

    void CreateMeshFilters(MeshData[] meshDatas)
    {
        var transformThis = transform;

        for (int i = 0; i < meshDatas.Length; i++)
        {
            var mesh = meshDatas[i].ToMesh();
            mesh.RecalculateBounds();

            AddMesh(mesh, transformThis, i);
        }
    }

    void AddMesh(Mesh mesh, Transform transformThis, int index)
    {
        var go = new GameObject("Face_" + index);
        go.transform.SetParent(transformThis, false);

        go.AddComponent<MeshRenderer>().sharedMaterial = _material;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        _meshes.Add(mf);
    }

    private void OnValidate()
    {
        _rowsPre = Mathf.Max(_rowsPre, 2);
        _rows = Mathf.Max(_rows, 2);
    }
}

[CustomEditor(typeof(EarthGenerator))]
public sealed class EarthGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Destroy"))
        {
            var eg = (EarthGenerator)target;
            eg.DestroyAllMeshes(false);
        }

        if (GUILayout.Button("Save"))
        {
            var eg = (EarthGenerator)target;
            eg.Save();
        }

        if (GUILayout.Button("Load"))
        {
            var eg = (EarthGenerator)target;
            eg.Load();
        }

        if (GUILayout.Button("Generate simple"))
        {
            var eg = (EarthGenerator)target;
            eg.GenerateSimple();
        }
    }
}
