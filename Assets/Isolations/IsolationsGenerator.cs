using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed unsafe class IsolationsGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathFieldsMap;
    [SerializeField] bool _nearest4;
    [SerializeField] int _n;

    [Header("Output")]
    [SerializeField] string _savePathIsolationsMap;

    public void Generate()
    {
        var sw = Stopwatch.StartNew();

        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var isolationsMap = new RawArray<Color32>(ALLOCATOR, fieldsMap.TextureSize.x * fieldsMap.TextureSize.y);

        if (_nearest4)
        {
            var job = new IsolationsJob4
            {
                FieldsMap = fieldsMap,
                IsolationsMap = isolationsMap,
            };

            job.Schedule(isolationsMap.Length, 64).Complete();
        }
        else
        {
            var job = new IsolationsJob
            {
                FieldsMap = fieldsMap,
                IsolationsMap = isolationsMap,
                N = _n,
            };

            job.Schedule(isolationsMap.Length, 64).Complete();
        }

        TextureSaver.Save(fieldsMap.TextureSize, _savePathIsolationsMap, (i) => isolationsMap[i]);

        fieldsMap.Dispose();
        isolationsMap.Dispose();
    }
}

[CustomEditor(typeof(IsolationsGenerator))]
public sealed class IsolationsGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var ig = (IsolationsGenerator)target;
            ig.Generate();
        }
    }
}
