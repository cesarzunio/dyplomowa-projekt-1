using Unity.Mathematics;

public static class MeshDatasUtilities
{
    public static void Normalize(MeshData[] meshDatas)
    {
        for (int i = 0; i < meshDatas.Length; i++)
        {
            Normalize(meshDatas[i]);
        }
    }

    public static void Normalize(MeshData meshData)
    {
        for (int i = 0; i < meshData.Vertices.Length; i++)
        {
            meshData.Vertices[i] = math.normalize(meshData.Vertices[i]);
        }
    }

    public static double3 GetVertex(this MeshData[] meshDatas, IndiceLoc indiceLoc) => meshDatas[indiceLoc.MeshDataIndex].Vertices[indiceLoc.Indice];
    public static void SetVertex(this MeshData[] meshDatas, IndiceLoc indiceLoc, double3 vertex) => meshDatas[indiceLoc.MeshDataIndex].Vertices[indiceLoc.Indice] = vertex;

    public static double3 GetNormal(this MeshData[] meshDatas, IndiceLoc indiceLoc) => meshDatas[indiceLoc.MeshDataIndex].Normals[indiceLoc.Indice];
    public static void SetNormal(this MeshData[] meshDatas, IndiceLoc indiceLoc, double3 normal) => meshDatas[indiceLoc.MeshDataIndex].Normals[indiceLoc.Indice] = normal;
}
