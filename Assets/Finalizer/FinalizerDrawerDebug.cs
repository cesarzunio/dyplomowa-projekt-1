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
using static NodesSaveUtility4;
using static UnwrapperUtilities;

public sealed unsafe class FinalizerDrawerDebug : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [Header("Saves")]
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathNodes;
    [SerializeField] string _savePathNodeEdges;
    [SerializeField] string _savePathRivers;
    [SerializeField] string _savePathFieldsNodes;
    [SerializeField] string _savePathRiverPoints;

    [Header("Data")]
    [SerializeField] int2 _textureSize;
    [SerializeField] DrawMode _drawMode;
    [SerializeField] int _drawIndex;

    [Header("Plane")]
    [SerializeField] int2 _planeSize;

    NodesFinals _nodes;
    EdgesFinals _edges;
    RawArray<RiverData> _riversData;
    RawArray<RiverPoint> _riverPoints;

    [Serializable]
    public enum DrawMode
    {
        RiverDataPixelCoords,
        RiverDataPixelCoordsByIndex,
        RiverDataPixelCoordsByIndexAndNexts,
        RiverDataPixelCoordsByIndexAndPrevious,

        RiverEdges,
        RiverEdgesByIndex,
        RiverEdgesAndCrossings,

        EdgesByNodeIndex,
    };

    private void Start()
    {
        _nodes = NodesSaveUtility4.LoadNodes(_savePathNodes, ALLOCATOR);
        _edges = NodesSaveUtility4.LoadEdges(_savePathNodeEdges, ALLOCATOR);
        //_riversData = UnwrapperUtilities.LoadRiversData(_savePathRivers, ALLOCATOR);
        _riverPoints = NodesSaveUtility4.LoadRiverPoints(_savePathRiverPoints, ALLOCATOR);
    }

    private void OnDestroy()
    {
        _nodes.Dispose();
        _edges.Dispose();
        //_riversData.DisposeDepth1();
        _riverPoints.DisposeDepth1();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        switch (_drawMode)
        {
            case DrawMode.RiverDataPixelCoords:
                //RiverDataPixelCoords(); break;

            case DrawMode.RiverDataPixelCoordsByIndex:
                //RiverDataPixelCoordsByIndex(_drawIndex); break;

            case DrawMode.RiverDataPixelCoordsByIndexAndNexts:
                //RiverDataPixelCoordsByIndexAndNexts(_drawIndex); break;

            case DrawMode.RiverDataPixelCoordsByIndexAndPrevious:
                //RiverDataPixelCoordsByIndexAndPrevious(_drawIndex); break;
                break;

            case DrawMode.RiverEdges:
                RiverEdges(); break;

            case DrawMode.RiverEdgesByIndex:
                RiverEdgesByIndex(); break;

            case DrawMode.RiverEdgesAndCrossings:
                RiverEdgesAndCrossings(); break;

            case DrawMode.EdgesByNodeIndex:
                EdgesByNodeIndex(); break;
        }
    }

    void RiverDataPixelCoords()
    {
        for (int i = 0; i < _riversData.Length; i++)
        {
            ref var data = ref _riversData[i];

            for (int j = 0; j < data.PixelCoords.Length - 1; j++)
            {
                var uvA = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(data.PixelCoords[j], _textureSize);
                var uvB = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(data.PixelCoords[j + 1], _textureSize);

                var color = (j % 3) switch
                {
                    0 => Color.red,
                    1 => Color.green,
                    _ => Color.blue,
                };

                DrawLineOnPlane(uvA, uvB, _planeSize, color);
            }
        }
    }

    void RiverDataPixelCoordsByIndex(int drawIndex)
    {
        if (drawIndex < 0 || drawIndex >= _riversData.Length)
            return;

        ref var data = ref _riversData[drawIndex];

        for (int j = 0; j < data.PixelCoords.Length - 1; j++)
        {
            var uvA = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(data.PixelCoords[j], _textureSize);
            var uvB = (float2)GeoUtilitiesDouble.PixelCoordToPlaneUv(data.PixelCoords[j + 1], _textureSize);

            var color = (j % 3) switch
            {
                0 => Color.red,
                1 => Color.green,
                _ => Color.blue,
            };

            DrawLineOnPlane(uvA, uvB, _planeSize, color);
        }
    }

    void RiverDataPixelCoordsByIndexAndNexts(int drawIndex)
    {
        if (drawIndex < 0 || drawIndex >= _riversData.Length)
            return;

        RiverDataPixelCoordsByIndex(drawIndex);

        ref var data = ref _riversData[drawIndex];

        for (int i = 0; i < data.EndsInto.Count; i++)
        {
            RiverDataPixelCoordsByIndexAndNexts((int)data.EndsInto[i]);
        }
    }

    void RiverDataPixelCoordsByIndexAndPrevious(int drawIndex)
    {
        if (drawIndex < 0 || drawIndex >= _riversData.Length)
            return;

        RiverDataPixelCoordsByIndex(drawIndex);

        ref var data = ref _riversData[drawIndex];

        for (int i = 0; i < data.StartsFrom.Count; i++)
        {
            RiverDataPixelCoordsByIndexAndPrevious((int)data.StartsFrom[i]);
        }
    }

    void RiverEdges()
    {
        for (int i = 0; i < _edges.Length; i++)
        {
            ref readonly var ownerA = ref _nodes.Owner[_edges.NodesIndexes[i].x];
            ref readonly var ownerB = ref _nodes.Owner[_edges.NodesIndexes[i].y];

            if (ownerA.Type != NodeOwnerType.River || ownerB.Type != NodeOwnerType.River)
                continue;

            var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(_nodes.GeoCoord[_edges.NodesIndexes[i].x]);
            var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(_nodes.GeoCoord[_edges.NodesIndexes[i].y]);

            DrawLineOnPlane(uvA, uvB, _planeSize, Color.green);
        }
    }

    void RiverEdgesByIndex()
    {
        if (_drawIndex < 0 || _drawIndex >= _riversData.Length)
            return;

        uint index = (uint)_drawIndex;

        for (int i = 0; i < _edges.Length; i++)
        {
            ref readonly var ownerA = ref _nodes.Owner[_edges.NodesIndexes[i].x];
            ref readonly var ownerB = ref _nodes.Owner[_edges.NodesIndexes[i].y];

            if (ownerA.Type != NodeOwnerType.River || ownerB.Type != NodeOwnerType.River)
                continue;

            if (ownerA.Index != index && ownerB.Index != index)
                continue;

            var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(_nodes.GeoCoord[_edges.NodesIndexes[i].x]);
            var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(_nodes.GeoCoord[_edges.NodesIndexes[i].y]);

            DrawLineOnPlane(uvA, uvB, _planeSize, Color.green);
        }
    }

    void RiverEdgesAndCrossings()
    {
        for (int i = 0; i < _edges.Length; i++)
        {
            ref readonly var ownerA = ref _nodes.Owner[_edges.NodesIndexes[i].x];
            ref readonly var ownerB = ref _nodes.Owner[_edges.NodesIndexes[i].y];

            if (ownerA.Type != NodeOwnerType.River && ownerB.Type != NodeOwnerType.River)
                continue;

            var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(_nodes.GeoCoord[_edges.NodesIndexes[i].x]);
            var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(_nodes.GeoCoord[_edges.NodesIndexes[i].y]);

            var color = ownerA.Type == NodeOwnerType.River && ownerB.Type == NodeOwnerType.River ? Color.green : Color.red;

            DrawLineOnPlane(uvA, uvB, _planeSize, color);
        }
    }

    void RiverPoints()
    {
        for (int i = 0; i < _riverPoints.Length; i++)
        {
            ref readonly var point = ref _riverPoints[i];
            ref readonly var nodeOwner = ref _nodes.Owner[point.NodeIndex];
            ref readonly var nodeGeoCoord = ref _nodes.GeoCoord[point.NodeIndex];

            var uvA = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeGeoCoord);

            for (int j = 0; j < point.StartsFrom.Length; j++)
            {
                ref readonly var pointOther = ref _riverPoints[point.StartsFrom[j]];
                ref readonly var nodeOwnerOther = ref _nodes.Owner[pointOther.NodeIndex];
                ref readonly var nodeGeoCoordOther = ref _nodes.GeoCoord[pointOther.NodeIndex];

                var uvB = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(nodeGeoCoordOther);

                DrawLineOnPlane(uvA, uvB, _planeSize, Color.green);
            }
        }
    }

    void EdgesByNodeIndex()
    {
        if (_drawIndex < 0 || _drawIndex >= _nodes.Length)
            return;

        uint nodeIndex = (uint)_drawIndex;
        ref readonly var edgesIndexes = ref _nodes.EdgesIndexes[_drawIndex];
        ref readonly var geoCoord = ref _nodes.GeoCoord[_drawIndex];
        var uv = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(geoCoord);

        for (int i = 0; i < edgesIndexes.Length; i++)
        {
            ref readonly var nodesIndexes = ref _edges.NodesIndexes[i];

            uint nodeIndexOther = nodesIndexes.x ^ nodesIndexes.y ^ nodeIndex;

            if (nodesIndexes.x != nodeIndex && nodesIndexes.y != nodeIndex)
                Debug.LogError("Bruh!!");

            ref readonly var geoCoordOther = ref _nodes.GeoCoord[nodeIndexOther];
            var uvOther = (float2)GeoUtilitiesDouble.GeoCoordsToPlaneUv(geoCoordOther);

            DrawLineOnPlane(uv, uvOther, _planeSize, Color.red);
        }
    }

    const float DRAWN_LINE_Y = 0.001f;

    static void DrawLineOnPlane(float2 uvA, float2 uvB, int2 planeSize, Color color)
    {
        var pos = new Vector3(uvA.x * planeSize.x, DRAWN_LINE_Y, uvA.y * planeSize.y);
        var posNext = new Vector3(uvB.x * planeSize.x, DRAWN_LINE_Y, uvB.y * planeSize.y);

        Gizmos.color = color;
        Gizmos.DrawLine(pos, posNext);
    }
}

[CustomEditor(typeof(FinalizerDrawerDebug))]
public sealed class FinalizerDrawerDebugEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        var fd = (FinalizerDrawerDebug)target;

        if (GUILayout.Button("Test"))
        {
        }
    }
}