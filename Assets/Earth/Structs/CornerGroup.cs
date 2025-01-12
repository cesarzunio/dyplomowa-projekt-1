using System.Collections.Generic;
using Unity.Mathematics;

public readonly struct CornerGroup
{
    public readonly double3 Vertex;
    public readonly List<IndiceLoc> IndicesLocs;

    public CornerGroup(double3 vertex, IndiceLoc indiceLoc)
    {
        Vertex = vertex;
        IndicesLocs = new List<IndiceLoc> { indiceLoc };
    }

    public double3 GetAverageVertex(MeshData[] meshDatas)
    {
        var sum = double3.zero;

        for (int i = 0; i < IndicesLocs.Count; i++)
        {
            sum += meshDatas.GetVertex(IndicesLocs[i]);
        }

        return sum / IndicesLocs.Count;
    }
}
