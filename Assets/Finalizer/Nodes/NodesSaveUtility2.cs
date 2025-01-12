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

public static unsafe class NodesSaveUtility2
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    public static RawArray<NodeSerialized> LoadNodes(string path, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fileStream);

        int length = BinarySaveUtility.ReadValue<int>(fileStream);
        var nodes = new RawArray<NodeSerialized>(allocator, length);

        var geoCoords = BinarySaveUtility.ReadArraySimple<double2>(fileStream, length, ALLOCATOR);
        var owners = BinarySaveUtility.ReadArraySimple<NodeOwner>(fileStream, length, ALLOCATOR);
        var edges = CesMemoryUtility.Allocate<RawArray<uint>>(length, ALLOCATOR);

        for (int i = 0; i < length; i++)
        {
            edges[i] = BinarySaveUtility.ReadRawArray<uint>(fileStream, reader, allocator);
        }

        for (int i = 0; i < length; i++)
        {
            nodes[i] = new NodeSerialized(geoCoords[i], owners[i], edges[i]);
        }

        UnsafeUtility.Free(geoCoords, ALLOCATOR);
        UnsafeUtility.Free(owners, ALLOCATOR);
        UnsafeUtility.Free(edges, ALLOCATOR);

        return nodes;
    }

    public static RawArray<EdgeSerialized> LoadEdges(string path, Allocator allocator)
    {
        return BinarySaveUtility.ReadRawArray<EdgeSerialized>(path, allocator);
    }

    public struct NodeSerialized : IDisposable
    {
        public double2 GeoCoord;
        public NodeOwner Owner;
        public RawArray<uint> Edges;

        public NodeSerialized(double2 geoCoord, NodeOwner owner, RawArray<uint> edges)
        {
            GeoCoord = geoCoord;
            Owner = owner;
            Edges = edges;
        }

        public void Dispose()
        {
            Edges.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NodeOwner
    {
        public NodeOwnerType Type;
        public uint Index;
        public int IndexSecondary;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NodeOwner(NodeOwnerType type, uint index, int indexSecondary)
        {
            Type = type;
            Index = index;
            IndexSecondary = indexSecondary;
        }
    }

    public enum NodeOwnerType : uint
    {
        Field = 0,
        River = 10,
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct EdgeSerialized
    {
        public readonly uint NodeA;
        public readonly uint NodeB;

        public readonly double DistanceGround;
        public readonly double DistanceAir;

        public readonly int CrossedRiverNodeIndex;

        public EdgeSerialized(uint nodeA, uint nodeB, double distanceGround, double distanceAir, int crossedRiverNodeIndex)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            DistanceGround = distanceGround;
            DistanceAir = distanceAir;
            CrossedRiverNodeIndex = crossedRiverNodeIndex;
        }

        //public readonly uint GetOtherNodeIndex(uint nodeIndex) => NodeA ^ NodeB ^ nodeIndex;
        public readonly uint GetOtherNodeIndex(uint nodeIndex)
        {
            if (nodeIndex != NodeA && nodeIndex != NodeB)
                throw new Exception("None is matching, check ur fucking algos bruh!");

            return (NodeA == nodeIndex) ? NodeB : NodeA;
        }
    }
}
