using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class HeightToNormal : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Temp;

    [SerializeField] Texture2D _heightTex;
    [SerializeField] float _normalStrength;

    public unsafe void Generate()
    {
        //var sw = Stopwatch.StartNew();

        //var heightMap = _heightTex.GetRawTextureData<byte>();
        //var normalMap = new RawArray<Color32>(ALLOCATOR, heightMap.Length);
        //var textureSize = new int2(_heightTex.width, _heightTex.height);

        //var job = new HeightToNormalJob
        //{
        //    TextureSize = textureSize,
        //    HeightMap = heightMap,
        //    NormalStrength = _normalStrength,
        //    NormalMap = normalMap,
        //};

        //Debug.Log("Prepare: " + sw.Elapsed.TotalMilliseconds);
        //sw.Restart();

        //job.Schedule(normalMap.Capacity, 64).Complete();

        //var normalPixels = new Color32[normalMap.Capacity];

        //for (int i = 0; i < normalPixels.Length; i++)
        //{
        //    normalPixels[i] = normalMap[i];
        //}

        //normalMap.Dispose();

        //Debug.Log("Main: " + sw.Elapsed.TotalMilliseconds);
        //sw.Restart();

        //SaveTextureToFile.SaveFrom(this.gameObject, normalPixels);

        //Debug.Log("Save: " + sw.Elapsed.TotalMilliseconds);
    }
}

[CustomEditor(typeof(HeightToNormal))]
public sealed class HeightToNormalEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var htn = (HeightToNormal)target;
            htn.Generate();
        }
    }
}
