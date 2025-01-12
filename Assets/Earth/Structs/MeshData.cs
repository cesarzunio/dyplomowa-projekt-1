using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public struct MeshData
{
    public double3[] Vertices;
    public int[] Indices;
    public double3[] Normals;
    public double2[] Uvs;

    public readonly Mesh ToMesh()
    {
        var mesh = new Mesh
        {
            indexFormat = Vertices.Length > (1 << 16) ? IndexFormat.UInt32 : IndexFormat.UInt16,
            vertices = EarthGeneratorHelpers.Double3sToVector3s(Vertices),
            triangles = Indices,
            normals = EarthGeneratorHelpers.Double3sToVector3s(Normals),
        };

        if (Uvs != null)
        {
            mesh.uv = EarthGeneratorHelpers.Double2sToVector2s(Uvs);
        }

        return mesh;
    }
}
