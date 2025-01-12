using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class VerticesGrouper
{
    public static void ApplyGroups(MeshData[] meshDatas, List<IndiceLocPair> indiceLocPairs, List<CornerGroup> cornersGroups)
    {
        for (int i = 0; i < indiceLocPairs.Count; i++)
        {
            SetPair(meshDatas, indiceLocPairs[i]);
        }

        for (int i = 0; i < cornersGroups.Count; i++)
        {
            SetGroup(meshDatas, cornersGroups[i]);
        }
    }

    static void SetPair(MeshData[] meshDatas, IndiceLocPair pair)
    {
        var vertex = (meshDatas.GetVertex(pair.First) + meshDatas.GetVertex(pair.Second)) / 2.0;
        var normal = math.normalize(meshDatas.GetNormal(pair.First) + meshDatas.GetNormal(pair.Second));

        meshDatas.SetVertex(pair.First, vertex);
        meshDatas.SetVertex(pair.Second, vertex);

        meshDatas.SetNormal(pair.First, normal);
        meshDatas.SetNormal(pair.Second, normal);
    }

    static void SetGroup(MeshData[] meshDatas, CornerGroup group)
    {
        var vertex = double3.zero;
        var normal = double3.zero;

        for (int i = 0; i < group.IndicesLocs.Count; i++)
        {
            vertex += meshDatas.GetVertex(group.IndicesLocs[i]);
            normal += meshDatas.GetNormal(group.IndicesLocs[i]);
        }

        vertex /= group.IndicesLocs.Count;
        normal = math.normalize(normal);

        for (int i = 0; i < group.IndicesLocs.Count; i++)
        {
            meshDatas.SetVertex(group.IndicesLocs[i], vertex);
            meshDatas.SetNormal(group.IndicesLocs[i], normal);
        }
    }
}
