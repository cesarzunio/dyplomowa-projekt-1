using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class EarthGeneratorHelpers
{
    public static void PopOut<T>(this List<T> list, out T element)
    {
        element = list[^1];
        list.RemoveAt(list.Count - 1);
    }

    const double CLOSE_ENOUGH = 1.0 / (1 << 16);

    public static bool IsCloseEnough(double3 lhs, double3 rhs) => math.distancesq(lhs, rhs) < CLOSE_ENOUGH;

    public static (int indiceStart, int indiceEnd) GetIndicesBounds(MeshDataEdgeType edgeType, int rows) => edgeType switch
    {
        MeshDataEdgeType.Left => (0, MeshDataBotLeftIndice(rows)),
        MeshDataEdgeType.Right => (0, MeshDataBotRightIndice(rows)),
        MeshDataEdgeType.Bot => (MeshDataBotLeftIndice(rows), MeshDataBotRightIndice(rows)),

        _ => throw new System.Exception("EarthGeneratorHelpers :: GetIndicesBounds :: Cannot match EdgeType: " + edgeType)
    };

    public static int RowsToVerticesCount(int rows) => rows * (rows + 1) / 2;
    public static int RowsToIndicesCount(int rows) => (rows - 1) * (rows - 1) * 3;

    public static int MeshDataBotLeftIndice(int rows) => CSum(rows - 1);
    public static int MeshDataBotRightIndice(int rows) => CSum(rows) - 1;

    public static int RowAndColumnToVertexIndex(int row, int column) => CSum(row) + column;

    static int CSum(int n)
    {
        int sum = 0;

        for (int i = 01; i <= n; i++)
        {
            sum += i;
        }

        return sum;
    }

    public static Vector3[] Double3sToVector3s(double3[] double3s)
    {
        var vector3s = new Vector3[double3s.Length];

        for (int i = 0; i < double3s.Length; i++)
        {
            vector3s[i] = double3s[i].ToVector3();
        }

        return vector3s;
    }

    static Vector3 ToVector3(this double3 d) => new Vector3
    {
        x = (float)d.x,
        y = (float)d.y,
        z = (float)d.z,
    };

    public static Vector2[] Double2sToVector2s(double2[] double3s)
    {
        var vector2s = new Vector2[double3s.Length];

        for (int i = 0; i < double3s.Length; i++)
        {
            vector2s[i] = double3s[i].ToVector2();
        }

        return vector2s;
    }

    static Vector2 ToVector2(this double2 d) => new Vector2
    {
        x = (float)d.x,
        y = (float)d.y
    };
}
