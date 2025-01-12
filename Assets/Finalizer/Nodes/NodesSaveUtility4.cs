using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public static unsafe class NodesSaveUtility4
{
    public static NodesFinals LoadNodes(string savePathNodes, Allocator allocator)
    {
        using var fileStream = new FileStream(savePathNodes, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int length = binaryReader.ReadInt32();

        var geoCoord = BinarySaveUtility.ReadArraySimple<double2>(fileStream, length, allocator);
        var owner = BinarySaveUtility.ReadArraySimple<NodeOwner>(fileStream, length, allocator);
        var edgesIndexes = CesMemoryUtility.Allocate<RawArray<uint>>(length, allocator);

        for (int i = 0; i < length; i++)
        {
            edgesIndexes[i] = BinarySaveUtility.ReadRawArray<uint>(fileStream, binaryReader, allocator);
        }

        return new NodesFinals(length, geoCoord, owner, edgesIndexes, allocator);
    }

    public static EdgesFinals LoadEdges(string savePathEdges, Allocator allocator)
    {
        using var fileStream = new FileStream(savePathEdges, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int length = binaryReader.ReadInt32();

        var nodeIndexes = BinarySaveUtility.ReadArraySimple<uint2>(fileStream, length, allocator);
        var distanceGround = BinarySaveUtility.ReadArraySimple<double>(fileStream, length, allocator);
        var distanceAir = BinarySaveUtility.ReadArraySimple<double>(fileStream, length, allocator);
        var crossedRiverNodeIndex = BinarySaveUtility.ReadArraySimple<int>(fileStream, length, allocator);

        return new EdgesFinals(length, nodeIndexes, distanceGround, distanceAir, crossedRiverNodeIndex, allocator);
    }

    public static RawArray<RiverPoint> LoadRiverPoints(string savePathRiverPoints, Allocator allocator)
    {
        using var fileStream = new FileStream(savePathRiverPoints, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int length = binaryReader.ReadInt32();

        var riverPoints = new RawArray<RiverPoint>(allocator, length);

        for (int i = 0; i < riverPoints.Length; i++)
        {
            riverPoints[i] = new RiverPoint
            {
                RiverIndex = binaryReader.ReadUInt32(),
                NodeIndex = binaryReader.ReadUInt32(),
                StartsFromFieldIndex = binaryReader.ReadInt32(),
                
                StartsFrom = BinarySaveUtility.ReadRawArray<uint>(fileStream, binaryReader, allocator),
                EndsInto = BinarySaveUtility.ReadRawArray<uint>(fileStream, binaryReader, allocator),
                NeighborFieldsIndexes = BinarySaveUtility.ReadRawArray<uint>(fileStream, binaryReader, allocator),
            };
        }

        return riverPoints;
    }

    public readonly struct EdgesFinals : IDisposable
    {
        public readonly int Length;

        [NativeDisableUnsafePtrRestriction]
        public readonly uint2* NodesIndexes;

        [NativeDisableUnsafePtrRestriction]
        public readonly double* DistanceGround;

        [NativeDisableUnsafePtrRestriction]
        public readonly double* DistanceAir;

        [NativeDisableUnsafePtrRestriction]
        public readonly int* CrossedRiverPointIndex;

        readonly Allocator _allocator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EdgesFinals(int length, uint2* nodesIndexes, double* distanceGround, double* distanceAir, int* crossedRiverPointIndex, Allocator allocator)
        {
            Length = length;
            NodesIndexes = nodesIndexes;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            CrossedRiverPointIndex = crossedRiverPointIndex;
            _allocator = allocator;
        }

        public void Dispose()
        {
            UnsafeUtility.Free(NodesIndexes, _allocator);
            UnsafeUtility.Free(DistanceGround, _allocator);
            UnsafeUtility.Free(DistanceAir, _allocator);
            UnsafeUtility.Free(CrossedRiverPointIndex, _allocator);
        }
    }

    public readonly struct NodesFinals : IDisposable
    {
        public readonly int Length;

        [NativeDisableUnsafePtrRestriction]
        public readonly double2* GeoCoord;

        [NativeDisableUnsafePtrRestriction]
        public readonly NodeOwner* Owner;

        [NativeDisableUnsafePtrRestriction]
        public readonly RawArray<uint>* EdgesIndexes;

        readonly Allocator _allocator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodesFinals(int length, double2* geoCoord, NodeOwner* owner, RawArray<uint>* edgesIndexes, Allocator allocator)
        {
            Length = length;
            GeoCoord = geoCoord;
            Owner = owner;
            EdgesIndexes = edgesIndexes;
            _allocator = allocator;
        }

        public void Dispose()
        {
            for (int i = 0; i < Length; i++)
            {
                EdgesIndexes[i].Dispose();
            }

            UnsafeUtility.Free(GeoCoord, _allocator);
            UnsafeUtility.Free(Owner, _allocator);
            UnsafeUtility.Free(EdgesIndexes, _allocator);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NodeOwner
    {
        public readonly NodeOwnerType Type;
        public readonly uint Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeOwner(NodeOwnerType type, uint index)
        {
            Type = type;
            Index = index;
        }
    }

    public enum NodeOwnerType : uint
    {
        Field = 0,
        River = 10
    }

    public struct RiverPoint : IDisposable
    {
        public uint RiverIndex;
        public uint NodeIndex;

        public int StartsFromFieldIndex;

        public RawArray<uint> StartsFrom;
        public RawArray<uint> EndsInto;

        public RawArray<uint> NeighborFieldsIndexes;

        public void Dispose()
        {
            StartsFrom.Dispose();
            EndsInto.Dispose();
            NeighborFieldsIndexes.Dispose();
        }
    }
}
