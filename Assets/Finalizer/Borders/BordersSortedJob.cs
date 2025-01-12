using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct BordersSortedJob : IJobParallelFor
{
    public Allocator Allocator;
    public int2 TextureSize;
    public RawArray<BordersContainers.BorderUnsorted> BordersUnsorted;
    public RawArray<BordersContainers.BorderSorted> BordersSorted;

    [BurstCompile]
    public void Execute(int index)
    {
        var borderUnsorted = BordersUnsorted[index].BorderCoords;
        var paths = new RawBag<RawBag<int2>>(Allocator, 4);

        while (TryPopAny(ref borderUnsorted, out var startBorderCoord))
        {
            var path = new RawBag<int2>(Allocator, 32);

            path.Add(startBorderCoord.xy);
            path.Add(startBorderCoord.zw);

            while (TryPopMatching(path[^1], TextureSize, ref borderUnsorted, out var borderCoordRight))
            {
                path.Add(borderCoordRight);
            }

            while (TryPopMatching(path[0], TextureSize,ref borderUnsorted, out var borderCoordLeft))
            {
                path.Insert(borderCoordLeft, 0);
            }

            paths.Add(path);
        }

        BordersSorted[index] = new BordersContainers.BorderSorted
        {
            FieldColorA = BordersUnsorted[index].FieldA,
            FieldColorB = BordersUnsorted[index].FieldB,
            BorderCoords = paths,
        };
    }

    static bool TryPopAny(ref ProxyBag<int4> borderUnsorted, out int4 borderCoord)
    {
        if (borderUnsorted.Count == 0)
        {
            borderCoord = default;
            return false;
        }

        borderCoord = borderUnsorted[^1];
        borderUnsorted.RemoveAt(borderUnsorted.Count - 1);
        return true;
    }

    static bool TryPopMatching(int2 borderCoord, int2 textureSize, ref ProxyBag<int4> borderUnsorted, out int2 borderCoordOut)
    {
        for (int i = 0; i < borderUnsorted.Count; i++)
        {
            var borderCoordUnsorted = borderUnsorted[i];

            //if (math.all(borderCoord == borderCoordUnsorted.xy))
            if (IsMatching(borderCoord, borderCoordUnsorted.xy, textureSize))
            {
                borderCoordOut = borderCoordUnsorted.zw;
                borderUnsorted.RemoveAt(i);
                return true;
            }

            //if (math.all(borderCoord == borderCoordUnsorted.zw))
            if (IsMatching(borderCoord, borderCoordUnsorted.zw, textureSize))
            {
                borderCoordOut = borderCoordUnsorted.xy;
                borderUnsorted.RemoveAt(i);
                return true;
            }
        }

        borderCoordOut = default;
        return false;
    }

    static bool IsMatching(int2 lhs, int2 rhs, int2 textureSize)
    {
        if (lhs.y == 0 && rhs.y == 0)
            return true;

        if (lhs.y == textureSize.y && rhs.y == textureSize.y)
            return true;

        return math.all(lhs == rhs);
    }
}
