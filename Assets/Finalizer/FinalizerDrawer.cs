using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FinalizerSaves;
using static NodesSaveUtility2;
using static UnwrapperUtilities;

public sealed unsafe class FinalizerDrawer : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [Header("Saves")]
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathRivers;
    [SerializeField] string _savePathFieldsNodes;

    [Header("Data")]
    [SerializeField] int2 _textureSize;
    [SerializeField] Color32 _fieldToDraw;
    [SerializeField] int _riverToDraw;
    [SerializeField] int2 _pixelCoordToDraw;
    [SerializeField] int2 _pixelCoordToDraw2;
    [SerializeField] bool _skipPixelCoords;
    [SerializeField] uint _in;
    [SerializeField] DrawMode _drawMode;

    [Header("Plane")]
    [SerializeField] int2 _planeSize;
    [SerializeField] float _drawDuration;

    [Header("Output")]
    [SerializeField] int _count;
    [SerializeField] bool _drawing;

    RawArray<NodeSerialized> _nodes;
    RawArray<EdgeSerialized> _edges;
    RawArray<RiverData> _riversData;

    [Serializable]
    public enum DrawMode { RiversAll, River, PixelCoords, RiversCoords, RiverEdges, RiverEdgesAndNeighboring, RiversDataForward, RiversDataBackward, EdgesWithRiverCrossing };

    public void Test()
    {


    }

    private void Start()
    {
        _nodes = NodesSaveUtility2.LoadNodes(_savePathNodes, ALLOCATOR);
        _edges = NodesSaveUtility2.LoadEdges(_savePathNodeEdges, ALLOCATOR);
        _riversData = UnwrapperUtilities.LoadRiversData(_savePathRivers, ALLOCATOR);
    }

    private void OnDestroy()
    {
        //if (Application.isPlaying)
        //{
        //    for (int i = 0; i < _nodes.Length; i++)
        //    {
        //        _nodes[i].Edges.Dispose();
        //    }

        //    _nodes.Dispose();
        //    _edges.Dispose();
        //}

        //UnwrapperUtilities.DisposeRiversData(_riversData);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        switch (_drawMode)
        {
            case DrawMode.RiversAll:
                OnDrawGizmosRiversAll(); break;

            case DrawMode.River:
                OnDrawGizmosRivers2(); break;

            case DrawMode.PixelCoords:
                OnDrawGizmosRivers3(); break;

            case DrawMode.RiversCoords:
                OnDrawGizmosRiversCoords(); break;

            case DrawMode.RiverEdges:
                OnDrawGizmosRiversEdges(); break;

            case DrawMode.RiverEdgesAndNeighboring:
                OnDrawGizmosRiversEdgesAndNeighbors(); break;

            case DrawMode.RiversDataForward:
                OnDrawGizmosRiversDataForward(); break;

            case DrawMode.RiversDataBackward:
                OnDrawGizmosRiversDataBackward(); break;

            case DrawMode.EdgesWithRiverCrossing:
                OnDrawGizmosEdgesWithRiverCrossing();
                OnDrawGizmosRiversCoords();
                break;
        }
    }

    void OnDrawGizmosRiversCoords()
    {
        for (int i = 0; i < _riversData.Length; i++)
        {
            for (int j = 0; j < _riversData[i].PixelCoords.Length - 1; j++)
            {
                var uvA = GeoUtilities.PixelCoordToPlaneUv(_riversData[i].PixelCoords[j], _textureSize);
                var uvB = GeoUtilities.PixelCoordToPlaneUv(_riversData[i].PixelCoords[j + 1], _textureSize);

                var color = (j % 3) switch
                {
                    0 => Color.red,
                    1 => Color.green,
                    _ => Color.blue,
                };

                DrawLineOnPlane(uvA, uvB, _planeSize, color, _drawDuration);
            }
        }
    }

    void OnDrawGizmosRiversAll()
    {
        for (int i = 0; i < _edges.Length; i++)
        {
            var nodeA = _nodes[_edges[i].NodeA];
            var nodeB = _nodes[_edges[i].NodeB];

            if (nodeA.Owner.Type == NodeOwnerType.River && nodeB.Owner.Type == NodeOwnerType.River)
            {
                var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
                var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

                DrawLineOnPlane(uvA, uvB, _planeSize, Color.green, _drawDuration);
            }
        }
    }

    void OnDrawGizmosRiversEdges()
    {
        for (int i = 0; i < _edges.Length; i++)
        {
            var nodeA = _nodes[_edges[i].NodeA];
            var nodeB = _nodes[_edges[i].NodeB];

            if (nodeA.Owner.Type != NodeOwnerType.River || nodeB.Owner.Type != NodeOwnerType.River)
                continue;

            var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
            var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

            DrawLineOnPlane(uvA, uvB, _planeSize, Color.green, _drawDuration);
        }
    }

    void OnDrawGizmosRiversEdgesAndNeighbors()
    {
        for (int i = 0; i < _edges.Length; i++)
        {
            var nodeA = _nodes[_edges[i].NodeA];
            var nodeB = _nodes[_edges[i].NodeB];

            if (nodeA.Owner.Type != NodeOwnerType.River && nodeB.Owner.Type != NodeOwnerType.River)
                continue;

            var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
            var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

            var color = (nodeA.Owner.Type == NodeOwnerType.River && nodeB.Owner.Type == NodeOwnerType.River ? Color.green : Color.red);

            DrawLineOnPlane(uvA, uvB, _planeSize, color, _drawDuration);
        }
    }

    void OnDrawGizmosRivers2()
    {
        for (int i = 0; i < _edges.Length; i++)
        {
            var nodeA = _nodes[_edges[i].NodeA];
            var nodeB = _nodes[_edges[i].NodeB];

            if (nodeA.Owner.Index != _riverToDraw && nodeB.Owner.Index != _riverToDraw)
                continue;

            if (nodeA.Owner.Type == NodeOwnerType.River && nodeB.Owner.Type == NodeOwnerType.River)
            {
                var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
                var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

                DrawLineOnPlane(uvA, uvB, _planeSize, Color.green, _drawDuration);
            }
        }
    }

    void OnDrawGizmosRivers3()
    {
        int count = 0;
        var pixelCoordToDraw = TexUtilities.FlipY(_pixelCoordToDraw, _textureSize.y);
        var pixelCoordToDraw2 = TexUtilities.FlipY(_pixelCoordToDraw2, _textureSize.y);
        var random = new Unity.Mathematics.Random(1);

        for (int i = 0; i < _edges.Length; i++)
        {
            var nodeA = _nodes[_edges[i].NodeA];
            var nodeB = _nodes[_edges[i].NodeB];

            if (nodeA.Owner.Type == NodeOwnerType.River && nodeB.Owner.Type == NodeOwnerType.River)
            {
                var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
                var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

                var pixelCoordA = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvA, _textureSize);
                var pixelCoordB = GeoUtilitiesDouble.PlaneUvToPixelCoord(uvB, _textureSize);

                //if (math.all(pixelCoordA != pixelCoordToDraw) && math.all(pixelCoordB != pixelCoordToDraw))
                //    continue;

                if ((math.all(pixelCoordA == pixelCoordToDraw) && math.all(pixelCoordB == pixelCoordToDraw2)) ||
                    (math.all(pixelCoordA == pixelCoordToDraw2) && math.all(pixelCoordB == pixelCoordToDraw)))
                {
                    var color = new Color(random.NextFloat(1f), random.NextFloat(1f), random.NextFloat(1f), 1f);
                    DrawLineOnPlane(uvA, uvB, _planeSize, color, _drawDuration);
                    count++;
                }

                //var color = new Color(random.NextFloat(1f), random.NextFloat(1f), random.NextFloat(1f), 1f);
                //DrawLineOnPlane(uvA, uvB, _planeSize, color, _drawDuration);
                //count++;
            }
        }

        _count = count;
    }

    void OnDrawGizmosRiversDataForward()
    {
        if (_riverToDraw < 0 || _riverToDraw >= _riversData.Length)
        {
            Debug.Log(_riverToDraw + " / " + _riversData.Length);
            _drawing = false;
            return;
        }

        _drawing = true;

        DrawRiverFromRiversDataForward((uint)_riverToDraw);
    }

    void DrawRiverFromRiversDataForward(uint index)
    {
        var riverData = _riversData[index];

        for (int i = 0; i < riverData.PixelCoords.Length - 1; i++)
        {
            var uvA = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(riverData.PixelCoords[i], _textureSize);
            var uvB = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(riverData.PixelCoords[i + 1], _textureSize);

            DrawLineOnPlane(uvA, uvB, _planeSize, Color.green, _drawDuration);
        }

        if (!riverData.EndsInto.IsCreated)
            return;

        for (int i = 0; i < riverData.EndsInto.Count; i++)
        {
            DrawRiverFromRiversDataForward(riverData.EndsInto[i]);
        }
    }

    void OnDrawGizmosRiversDataBackward()
    {
        if (_riverToDraw < 0 || _riverToDraw >= _riversData.Length)
        {
            Debug.Log(_riverToDraw + " / " + _riversData.Length);
            _drawing = false;
            return;
        }

        _drawing = true;

        DrawRiverFromRiversDataBackward((uint)_riverToDraw);
    }

    void DrawRiverFromRiversDataBackward(uint index)
    {
        var riverData = _riversData[index];

        for (int i = 0; i < riverData.PixelCoords.Length - 1; i++)
        {
            var uvA = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(riverData.PixelCoords[i], _textureSize);
            var uvB = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(riverData.PixelCoords[i + 1], _textureSize);

            DrawLineOnPlane(uvA, uvB, _planeSize, Color.green, _drawDuration);
        }

        if (!riverData.StartsFrom.IsCreated)
            return;

        for (int i = 0; i < riverData.StartsFrom.Count; i++)
        {
            DrawRiverFromRiversDataBackward(riverData.StartsFrom[i]);
        }
    }

    void OnDrawGizmosEdgesWithRiverCrossing()
    {
        int count = 0;

        for (int i = 0; i < _edges.Length; i++)
        {
            ref var edge = ref _edges[i];

            if (edge.CrossedRiverNodeIndex == -1)
                continue;

            ref var nodeA = ref _nodes[edge.NodeA];
            ref var nodeB = ref _nodes[edge.NodeB];

            var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeA.GeoCoord);
            var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeB.GeoCoord);

            DrawLineOnPlane(uvA, uvB, _planeSize, Color.red, _drawDuration);

            count++;
        }

        _count = count;
    }

    const float DRAWN_LINE_Y = 0.001f;

    static void DrawLineOnPlane(float2 uvA, float2 uvB, int2 planeSize, Color color, float drawDuration)
    {
        var pos = new Vector3(uvA.x * planeSize.x, DRAWN_LINE_Y, uvA.y * planeSize.y);
        var posNext = new Vector3(uvB.x * planeSize.x, DRAWN_LINE_Y, uvB.y * planeSize.y);

        Gizmos.color = color;
        Gizmos.DrawLine(pos, posNext);
    }
}

[CustomEditor(typeof(FinalizerDrawer))]
public sealed class FinalizerDrawerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Test"))
        {
            var fd = (FinalizerDrawer)target;
            fd.Test();
        }
    }
}