using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using static FinalizerSaves;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed unsafe class DistancesToBorderGenerator2 : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathFieldsMap;

    [Header("Output")]
    [SerializeField] double _distanceMax;
    [SerializeField] string _savePathDistancesToBorderMap;

    public void CreateDistances()
    {
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var textureSizeBig = fieldsMap.TextureSize * 2;
        int gridLength = textureSizeBig.x * textureSizeBig.y;

        var closed = new RawArray<bool>(ALLOCATOR, false, gridLength);
        var queue = new RawGeoQueueTexture(gridLength, textureSizeBig, ALLOCATOR);
        var distances = new RawArray<double>(ALLOCATOR, gridLength);

        var job = new DistancesToBorderJob5
        {
            FieldsMap = fieldsMap,
            Closed = closed,
            Queue = queue,
            Distances = distances,
        };

        job.Schedule().Complete();

        TextureSaver.Save(fieldsMap.TextureSize, _savePathDistancesToBorderMap, (i) =>
        {
            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, fieldsMap.TextureSize.x);

            int flatCenter = UpscaleAndFlatten(pixelCoord, new int2(1, 1), textureSizeBig.x);
            int flatLeftBot = UpscaleAndFlatten(pixelCoord, new int2(0, 0), textureSizeBig.x);
            int flatBot = UpscaleAndFlatten(pixelCoord, new int2(1, 0), textureSizeBig.x);
            int flatLeft = UpscaleAndFlatten(pixelCoord, new int2(0, 1), textureSizeBig.x);

            double distanceCenter = distances[flatCenter];
            double distanceLeftBot = distances[flatLeftBot];
            double distanceBot = distances[flatBot];
            double distanceLeft = distances[flatLeft];

            float fCenter = (float)(distanceCenter / _distanceMax);
            float fLeftBot = (float)(distanceLeftBot / _distanceMax);
            float fBot = (float)(distanceBot / _distanceMax);
            float fLeft = (float)(distanceLeft / _distanceMax);

            byte bCenter = CesColorUtilities.Float01ToByte(1f - fCenter);
            byte bLeftBot = CesColorUtilities.Float01ToByte(1f - fLeftBot);
            byte bBot = CesColorUtilities.Float01ToByte(1f - fBot);
            byte bLeft = CesColorUtilities.Float01ToByte(1f - fLeft);

            return new Color32(bCenter, bLeftBot, bBot, bLeft);
        });

        fieldsMap.Dispose();
        closed.Dispose();
        queue.Dispose();
        distances.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int UpscaleAndFlatten(int2 pixelCoord, int2 halfs, int textureSizeBigX)
    {
        var pixelCoordBig = (pixelCoord * 2) + halfs;
        return TexUtilities.PixelCoordToFlat(pixelCoordBig, textureSizeBigX);
    }
}

[CustomEditor(typeof(DistancesToBorderGenerator2))]
public sealed class DistancesToLandEditor2 : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var dtl = (DistancesToBorderGenerator2)target;

        if (GUILayout.Button("Generate"))
        {
            dtl.CreateDistances();
        }
    }
}
