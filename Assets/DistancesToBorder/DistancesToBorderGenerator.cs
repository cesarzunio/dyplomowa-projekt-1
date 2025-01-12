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
using UnityEngine;
using static FinalizerSaves;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed unsafe class DistancesToBorderGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] string _savePathFieldsMap;
    [SerializeField] string _savePathBorders;

    [Header("Output")]
    [SerializeField] double _distanceMax;
    [SerializeField] string _savePathDistancesToBorder;
    [SerializeField] bool _createTexture;
    [SerializeField] string _savePathDistancesToBorderMap;

    public void CreateDistances()
    {
        var sw = Stopwatch.StartNew();

        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var borders = FinalizerSaves.LoadBorders(_savePathBorders, ALLOCATOR);
        int fieldsMapLength = fieldsMap.TextureSize.x * fieldsMap.TextureSize.y;

        var coordToParentField = new RawArray<uint2>(ALLOCATOR, fieldsMapLength);
        var closed = new RawArray<bool>(ALLOCATOR, false, fieldsMapLength);
        var queue = new RawGeoQueueTexture(fieldsMapLength, fieldsMap.TextureSize, ALLOCATOR);
        var distances = new RawArray<double>(ALLOCATOR, fieldsMapLength);

        var jobMain = new DistancesToBorderJob
        {
            //FieldsMap = fieldsMap,
            //CoordToParentField = coordToParentField,
            //Closed = closed,
            //Queue = queue,
            //Distances = distances,
        };

        jobMain.Schedule().Complete();

        Debug.Log("DistancesToLand: " + sw.Elapsed.TotalSeconds + " s");
        sw.Restart();

        var fieldsIndexesToBorderIndex = new Dictionary<uint2, uint>(borders.Length);

        for (int i = 0; i < borders.Length; i++)
        {
            fieldsIndexesToBorderIndex[borders.Fields[i]] = (uint)i;
            fieldsIndexesToBorderIndex[Flip(borders.Fields[i])] = (uint)i;
        }

        SaveBorders(_savePathDistancesToBorder, coordToParentField, distances, fieldsIndexesToBorderIndex, _distanceMax);

        if (_createTexture)
        {
            TextureSaver.Save(fieldsMap.TextureSize, _savePathDistancesToBorderMap, (i) =>
            {
                float ratio = (float)math.saturate(distances[i] / _distanceMax);
                byte b = CesColorUtilities.Float01ToByte(1f - ratio);
                return new Color32(b, b, b, 255);
            });
        }

        fieldsMap.Dispose();
        borders.Dispose();
        coordToParentField.Dispose();
        closed.Dispose();
        queue.Dispose();
        distances.Dispose();
    }

    public void CreateDistances2()
    {
        var sw = Stopwatch.StartNew();

        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        int fieldsMapLength = fieldsMap.TextureSize.x * fieldsMap.TextureSize.y;

        var closed = new RawArray<bool>(ALLOCATOR, false, fieldsMapLength);
        var queue = new RawGeoQueueTexture(fieldsMapLength, fieldsMap.TextureSize, ALLOCATOR);
        var distances = new RawArray<double>(ALLOCATOR, math.INFINITY_DBL, fieldsMapLength);

        var closed2 = new RawArray<bool>(ALLOCATOR, false, fieldsMapLength);
        var queue2 = new RawGeoQueueTexture(fieldsMapLength, fieldsMap.TextureSize, ALLOCATOR);
        var distances2 = new RawArray<double>(ALLOCATOR, math.INFINITY_DBL, fieldsMapLength);

        var job = new DistancesToBorderJob3
        {
            FieldsMap = fieldsMap,
            Closed = closed,
            Queue = queue,
            Distances = distances,
        };

        var handle = job.Schedule();

        var job2 = new DistancesToBorderJob
        {
            FieldsMap = fieldsMap,
            Closed = closed2,
            Queue = queue2,
            Distances = distances2,
        };

        var handle2 = job2.Schedule();

        JobHandle.CompleteAll(ref handle, ref handle2);

        Debug.Log("CreateDistances2: " + sw.Elapsed.TotalSeconds + " s");
        sw.Restart();

        SaveTexture(fieldsMap.TextureSize, _savePathDistancesToBorderMap, fieldsMap, distances, distances2, _distanceMax);

        fieldsMap.Dispose();
        closed.Dispose();
        queue.Dispose();
        distances.Dispose();
        closed2.Dispose();
        queue2.Dispose();
        distances2.Dispose();
    }

    static void SaveTexture(int2 textureSize, string path, FieldsMap fieldsMap, RawArray<double> distances, RawArray<double> distances2, double distanceMax)
    {
        //TextureSaver.Save(textureSize, path, (i) =>
        //{
        //    var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, textureSize.x);
        //    var pixelCoordRight = TexUtilities.ClampPixelCoord(pixelCoord + new int2(1, 0), textureSize);
        //    var pixelCoordUp = TexUtilities.ClampPixelCoord(pixelCoord + new int2(0, 1), textureSize);
        //    var pixelCoordRightUp = TexUtilities.ClampPixelCoord(pixelCoord + new int2(1, 1), textureSize);

        //    int flatRight = TexUtilities.PixelCoordToFlat(pixelCoordRight, textureSize.x);
        //    int flatUp = TexUtilities.PixelCoordToFlat(pixelCoordUp, textureSize.x);
        //    int flatRightUp = TexUtilities.PixelCoordToFlat(pixelCoordRightUp, textureSize.x);

        //    float f0 = (float)math.saturate(distances[i] / distanceMax);
        //    float f1 = (float)math.saturate(distances[flatRight] / distanceMax);
        //    float f2 = (float)math.saturate(distances[flatUp] / distanceMax);
        //    float f3 = (float)math.saturate(distances[flatRightUp] / distanceMax);

        //    byte b0 = CesColorUtilities.Float01ToByte(1f - f0);
        //    byte b1 = CesColorUtilities.Float01ToByte(1f - f1);
        //    byte b2 = CesColorUtilities.Float01ToByte(1f - f2);
        //    byte b3 = CesColorUtilities.Float01ToByte(1f - f3);

        //    return new Color32(b0, b1, b2, b3);
        //});

        var neighbors = stackalloc int2[4];

        TextureSaver.Save(textureSize, path, (i) =>
        {
            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, textureSize.x);

            TexUtilities.GetNeighbors4(pixelCoord, textureSize, neighbors);

            var pixelCoordRightUp = TexUtilities.ClampPixelCoord(pixelCoord + new int2(1, 1), textureSize);

            int flatUp = TexUtilities.PixelCoordToFlat(neighbors[0], textureSize.x);
            int flatDown = TexUtilities.PixelCoordToFlat(neighbors[1], textureSize.x);
            int flatLeft = TexUtilities.PixelCoordToFlat(neighbors[2], textureSize.x);
            int flatRight = TexUtilities.PixelCoordToFlat(neighbors[3], textureSize.x);
            int flatRightUp = TexUtilities.PixelCoordToFlat(pixelCoordRightUp, textureSize.x);

            double dLB = distances[i];
            double dRB = distances[flatRight];
            double dLT = distances[flatUp];
            double dRT = distances[flatRightUp];

            uint fieldC = fieldsMap.Fields[i];
            uint fieldU = fieldsMap.Fields[flatUp];
            uint fieldB = fieldsMap.Fields[flatDown];
            uint fieldL = fieldsMap.Fields[flatLeft];
            uint fieldR = fieldsMap.Fields[flatRight];

            bool edgeU = fieldC != fieldU;
            bool edgeB = fieldC != fieldB;
            bool edgeL = fieldC != fieldL;
            bool edgeR = fieldC != fieldR;

            double dU = edgeU ? 0.0 : (dLT + dRT) / 2;
            double dB = edgeB ? 0.0 : (dLB + dRB) / 2;
            double dL = edgeL ? 0.0 : (dLT + dLB) / 2;
            double dR = edgeR ? 0.0 : (dRT + dRB) / 2;
            double dC = distances2[i];

            float fLB = (float)math.saturate(dLB / distanceMax);
            float fRB = (float)math.saturate(dRB / distanceMax);
            float fLT = (float)math.saturate(dLT / distanceMax);
            float fRT = (float)math.saturate(dRT / distanceMax);
            float fU = (float)math.saturate(dU / distanceMax);
            float fB = (float)math.saturate(dB / distanceMax);
            float fL = (float)math.saturate(dL / distanceMax);
            float fR = (float)math.saturate(dR / distanceMax);
            float fC = (float)math.saturate(dC / distanceMax);

            byte bLB = CesColorUtilities.Float01ToByte(1f - fLB);
            byte bRB = CesColorUtilities.Float01ToByte(1f - fRB);
            byte bLT = CesColorUtilities.Float01ToByte(1f - fLT);
            byte bRT = CesColorUtilities.Float01ToByte(1f - fRT);
            byte bU = CesColorUtilities.Float01ToByte(1f - fU);
            byte bB = CesColorUtilities.Float01ToByte(1f - fB);
            byte bL = CesColorUtilities.Float01ToByte(1f - fL);
            byte bR = CesColorUtilities.Float01ToByte(1f - fR);
            byte bC = CesColorUtilities.Float01ToByte(1f - fC);

            return new Color32(bC, bLB, bB, bL);
        });
    }

    static double GetCenterDistance(double dLB, double dRB, double dLT, double dRT, int2 pixelCoord, int2 textureSize)
    {
        var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoord, textureSize);
        var unitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(uv);

        int cornerCase = GetCornerCase(dLB, dRB, dLT, dRT);

        return cornerCase switch
        {
            0 => ReturnDistance(new int2(0, 0)),
            1 => ReturnDistance(new int2(1, 0)),
            2 => ReturnDistance(new int2(0, 1)),
            3 => ReturnDistance(new int2(1, 1)),
            _ => throw new Exception($"GetCenterDistance :: Cannot match case ({cornerCase})!")
        };

        // ---

        double ReturnDistance(int2 offset)
        {
            var pixelCoord2 = TexUtilities.ClampPixelCoord(pixelCoord + offset, textureSize);
            var uv2 = GeoUtilitiesDouble.EdgeCoordToPlaneUv(pixelCoord2, textureSize);
            var unitSphere2 = GeoUtilitiesDouble.PlaneUvToUnitSphere(uv2);

            return GeoUtilitiesDouble.Distance(unitSphere, unitSphere2);
        }
    }

    static int GetCornerCase(double dLB, double dRB, double dLT, double dRT)
    {
        if (dLB < dRB)
        {
            if (dLB < dLT)
            {
                return dLB < dRT ? 0 : 3;
            }

            else // dLT < dLB
            {
                return dLT < dRT ? 2 : 3;
            }
        }

        else // dRB < dLB
        {
            if (dRB < dLT)
            {
                return dRB < dRT ? 1 : 3;
            }

            else // dLT < dRB
            {
                return dLT < dRT ? 2 : 3;
            }
        }
    }

    public void GenerateBordersNew()
    {
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);
        var borders = CreateBorders(fieldsMap);

        BinarySaveUtility.WriteRawContainerSimple<RawArray<byte>, byte>(_savePathDistancesToBorder, borders);

        if (_createTexture)
        {
            TextureSaver.Save(fieldsMap.TextureSize, _savePathDistancesToBorderMap, (i) =>
            {
                byte b = borders[i];
                return new Color32(b, b, b, 255);
            });
        }

        fieldsMap.Dispose();
        borders.Dispose();
    }

    double GetDistanceMax(RawArray<double> distances)
    {
        double distanceMax = double.MinValue;

        for (int i = 0; i < distances.Length; i++)
        {
            distanceMax = math.max(distanceMax, distances[i]);
        }

        return distanceMax;
    }

    double GetDistanceMin(RawArray<double> distances)
    {
        double distanceMin = double.MaxValue;

        for (int i = 0; i < distances.Length; i++)
        {
            distanceMin = math.min(distanceMin, distances[i]);
        }

        return distanceMin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint2 Flip(uint2 u) => new(u.y, u.x);

    static void SaveBorders(string savePathDistancesToBorder, RawArray<uint2> coordToParentField, RawArray<double> distances, Dictionary<uint2, uint> fieldsIndexesToBorderIndex, double distanceMax)
    {
        using var fileStream = new FileStream(savePathDistancesToBorder, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(coordToParentField.Length);

        for (int i = 0; i < coordToParentField.Length; i++)
        {
            uint borderIndex = fieldsIndexesToBorderIndex[coordToParentField[i]];
            double distanceRatio = 1.0 - (distances[i] / distanceMax);
            uint distanceByte = CesColorUtilities.Float01ToByte((float)distanceRatio);

            uint value = (distanceByte << 24) | (borderIndex & 0x00FFFFFF);

            fileStream.WriteValue(value);
        }
    }

    static RawArray<byte> CreateBorders(FieldsMap fieldsMap)
    {
        var borders = new RawArray<byte>(ALLOCATOR, fieldsMap.TextureSize.x * fieldsMap.TextureSize.y);
        var neighbors = stackalloc int2[4];

        for (int y = 0; y < fieldsMap.TextureSize.y; y++)
        {
            for (int x = 0; x < fieldsMap.TextureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                int flat = TexUtilities.PixelCoordToFlat(pixelCoord, fieldsMap.TextureSize.x);

                TexUtilities.GetNeighbors4(new int2(x, y), fieldsMap.TextureSize, neighbors);

                borders[flat] = HasNeighbor(fieldsMap.Fields[flat], fieldsMap, neighbors);
            }
        }

        return borders;
    }

    static byte HasNeighbor(uint fieldIndex, FieldsMap fieldsMap, int2* neighbors)
    {
        for (int i = 0; i < 4; i++)
        {
            int flat = TexUtilities.PixelCoordToFlat(neighbors[i], fieldsMap.TextureSize.x);

            if (fieldsMap.Fields[flat] != fieldIndex)
                return 255;
        }

        return 0;
    }
}

[CustomEditor(typeof(DistancesToBorderGenerator))]
public sealed class DistancesToLandEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var dtl = (DistancesToBorderGenerator)target;
            dtl.CreateDistances();
        }

        if (GUILayout.Button("Generate 2"))
        {
            var dtl = (DistancesToBorderGenerator)target;
            dtl.CreateDistances2();
        }

        if (GUILayout.Button("Generate new"))
        {
            var dtl = (DistancesToBorderGenerator)target;
            dtl.GenerateBordersNew();
        }
    }
}
