using Unity.Mathematics;

public struct MeshDataEdge2
{
    public double3 VerticeStart;
    public double3 VerticeEnd;
    public MeshDataEdgeType EdgeType;
    public int MeshDataIndex;

    public MeshDataEdge2(MeshDataEdgeType edgeType, int rows, double3[] vertices, int meshDataIndex)
    {
        var (indiceStart, indiceEnd) = EarthGeneratorHelpers.GetIndicesBounds(edgeType, rows);

        VerticeStart = vertices[indiceStart];
        VerticeEnd = vertices[indiceEnd];
        EdgeType = edgeType;
        MeshDataIndex = meshDataIndex;
    }

    public readonly IndiceLoc GetIndiceLoc(int rows, bool end)
    {
        var (indiceStart, indiceEnd) = EarthGeneratorHelpers.GetIndicesBounds(EdgeType, rows);

        return new IndiceLoc(MeshDataIndex, end ? indiceEnd : indiceStart);
    }
}
