using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public static class EarthGeneratorUtilities
{
    public static MeshData GenerateIcosahedron()
    {
        double phi = (1.0 + math.sqrt(5.0)) / 2.0;

        var vertices = new double3[]
        {
            new(-1, phi, 0),
            new(1, phi, 0),
            new(-1, -phi, 0),
            new(1, -phi, 0),
            new(0, -1, phi),
            new(0, 1, phi),
            new(0, -1, -phi),
            new(0, 1, -phi),
            new(phi, 0, -1),
            new(phi, 0, 1),
            new(-phi, 0, -1),
            new(-phi, 0, 1)
        };

        var indices = new int[]
        {
            0, 11, 5,
            0, 5, 1,
            0, 1, 7,
            0, 7, 10,
            0, 10, 11,
            1, 5, 9,
            5, 11, 4,
            11, 10, 2,
            10, 7, 6,
            7, 1, 8,
            3, 9, 4,
            3, 4, 2,
            3, 2, 6,
            3, 6, 8,
            3, 8, 9,
            4, 9, 5,
            2, 4, 11,
            6, 2, 10,
            8, 6, 7,
            9, 8, 1,
        };

        return new MeshData
        {
            Vertices = vertices,
            Indices = indices,
        };
    }

    public static MeshData SubdivideTriangle(double3 top, double3 botLeft, double3 botRight, int rows) => new MeshData
    {
        Vertices = GetSubdividedVertices(top, botLeft, botRight, rows),
        Indices = GetSubdividedIndices(rows),
    };

    public static double3[] GetSubdividedVertices(double3 top, double3 botLeft, double3 botRight, int rows)
    {
        var vertices = new double3[EarthGeneratorHelpers.RowsToVerticesCount(rows)];
        int verticesIt = 0;

        vertices[verticesIt++] = top;

        for (int r = 1; r < rows; r++)
        {
            int columns = r + 1;

            GetFirstAndLastOfRow(r, rows, top, botLeft, botRight, out var firstOfRow, out var lastOfRow);

            for (int c = 0; c < columns; c++)
            {
                vertices[verticesIt++] = LerpColumn(firstOfRow, lastOfRow, c, columns);
            }
        }

        return vertices;

        // ---

        static void GetFirstAndLastOfRow(int r, int rows, double3 top, double3 botLeft, double3 botRight, out double3 firstOfRow, out double3 lastOfRow)
        {
            if (r == 0)
                throw new System.Exception("EarthGeneratorUtilities :: GetSubdividedVertices :: Row index cannot be 0!");

            if (r == rows - 1)
            {
                firstOfRow = botLeft;
                lastOfRow = botRight;
                return;
            }

            firstOfRow = math.lerp(top, botLeft, r / (rows - 1.0));
            lastOfRow = math.lerp(top, botRight, r / (rows - 1.0));
        }

        static double3 LerpColumn(double3 firstOfRow, double3 lastOfRow, int c, int columns)
        {
            if (c == 0)
                return firstOfRow;

            if (c == columns - 1)
                return lastOfRow;

            return math.lerp(firstOfRow, lastOfRow, c / (columns - 1.0));
        }
    }

    public static int[] GetSubdividedIndices(int rows)
    {
        var indices = new int[EarthGeneratorHelpers.RowsToIndicesCount(rows)];
        int indicesIt = 0;
        int verticesIt = 0;

        for (int r = 0; r < rows; r++)
        {
            int columns = r + 1;

            for (int c = 0; c < columns; c++)
            {
                // has vertices below
                if (r < rows - 1)
                {
                    indices[indicesIt++] = verticesIt;
                    indices[indicesIt++] = verticesIt + columns;
                    indices[indicesIt++] = verticesIt + columns + 1;

                    // has vertices to the right
                    if (c < columns - 1)
                    {
                        indices[indicesIt++] = verticesIt;
                        indices[indicesIt++] = verticesIt + columns + 1;
                        indices[indicesIt++] = verticesIt + 1;
                    }
                }

                verticesIt++;
            }
        }

        return indices;
    }

    public static List<int> GetVerticesIndexesWithoutEdges(int rows)
    {
        var verticesIndexes = new List<int>(EarthGeneratorHelpers.RowsToIndicesCount(rows));

        for (int r = 1; r < rows - 1; r++)
        {
            int columns = r + 1;

            for (int c = 1; c < columns - 1; c++)
            {
                verticesIndexes.Add(EarthGeneratorHelpers.RowAndColumnToVertexIndex(r, c));
            }
        }

        return verticesIndexes;
    }

    public static double3[] CalculateNormals(double3[] vertices, int[] indices)
    {
        var normals = new double3[vertices.Length];

        for (int i = 0; i < indices.Length; i += 3)
        {
            var vertexAB = vertices[indices[i + 1]] - vertices[indices[i]];
            var vertexAC = vertices[indices[i + 2]] - vertices[indices[i]];

            var cross = math.cross(vertexAB, vertexAC);

            normals[indices[i]] += cross;
            normals[indices[i + 1]] += cross;
            normals[indices[i + 2]] += cross;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = math.normalize(normals[i]);
        }

        return normals;
    }

    public static List<CornerGroup> FindOverlapsCorners(MeshData[] meshDatas, int rows)
    {
        var edgesAll = GetEdges(meshDatas, rows);
        var cornersGroups = new List<CornerGroup>();

        while (edgesAll.Count > 0)
        {
            edgesAll.PopOut(out var edge);
            FindCorner(edge, cornersGroups, rows);
        }

        return cornersGroups;
    }

    static void FindCorner(MeshDataEdge2 edge, List<CornerGroup> cornerGroups, int rows)
    {
        AddStart();
        AddEnd();

        // ---

        void AddStart()
        {
            var indiceLoc = edge.GetIndiceLoc(rows, false);

            for (int i = 0; i < cornerGroups.Count; i++)
            {
                if (EarthGeneratorHelpers.IsCloseEnough(edge.VerticeStart, cornerGroups[i].Vertex))
                {
                    cornerGroups[i].IndicesLocs.Add(indiceLoc);
                    return;
                }
            }

            cornerGroups.Add(new CornerGroup(edge.VerticeStart, indiceLoc));
        }

        void AddEnd()
        {
            var indiceLoc = edge.GetIndiceLoc(rows, true);

            for (int i = 0; i < cornerGroups.Count; i++)
            {
                if (EarthGeneratorHelpers.IsCloseEnough(edge.VerticeEnd, cornerGroups[i].Vertex))
                {
                    cornerGroups[i].IndicesLocs.Add(indiceLoc);
                    return;
                }
            }

            cornerGroups.Add(new CornerGroup(edge.VerticeEnd, indiceLoc));
        }
    }

    public static List<IndiceLocPair> FindOverlapsEdges(MeshData[] meshDatas, int rows)
    {
        var edgesAll = GetEdges(meshDatas, rows);
        var edgesPairs = new List<(MeshDataEdge2 EdgeOriginal, MeshDataEdge2 EdgePair, bool Flipped)>(edgesAll.Count / 2);

        while (edgesAll.Count > 0)
        {
            edgesAll.PopOut(out var edgeOriginal);
            FindPair(edgesAll, edgeOriginal, out var edgePair, out bool flipped);

            edgesPairs.Add((edgeOriginal, edgePair, flipped));
        }

        var edgeIndices = new MeshDataEdgeIndices(rows);
        var pairs = new List<IndiceLocPair>(edgesPairs.Count * (rows - 2));

        for (int i = 0; i < edgesPairs.Count; i++)
        {
            var (edgeOriginal, edgePair, flipped) = edgesPairs[i];

            var indicesOriginal = edgeIndices.GetIndices(edgeOriginal.EdgeType, false);
            var indicesPair = edgeIndices.GetIndices(edgePair.EdgeType, flipped);

            for (int j = 1; j < rows - 1; j++)
            {
                var indiceLocFirst = new IndiceLoc(edgeOriginal.MeshDataIndex, indicesOriginal[j]);
                var indiceLocSecond = new IndiceLoc(edgePair.MeshDataIndex, indicesPair[j]);

                pairs.Add(new IndiceLocPair(indiceLocFirst, indiceLocSecond));
            }
        }

        return pairs;
    }

    static List<MeshDataEdge2> GetEdges(MeshData[] meshDatas, int rows)
    {
        var list = new List<MeshDataEdge2>(meshDatas.Length * 3);

        for (int i = 0; i < meshDatas.Length; i++)
        {
            var vertices = meshDatas[i].Vertices;

            list.Add(new MeshDataEdge2(MeshDataEdgeType.Left, rows, vertices, i));
            list.Add(new MeshDataEdge2(MeshDataEdgeType.Right, rows, vertices, i));
            list.Add(new MeshDataEdge2(MeshDataEdgeType.Bot, rows, vertices, i));
        }

        return list;
    }

    static void FindPair(List<MeshDataEdge2> edgesList, MeshDataEdge2 edgeOriginal, out MeshDataEdge2 edgePair, out bool flipped)
    {
        for (int i = 0; i < edgesList.Count; i++)
        {
            var edge = edgesList[i];

            if (EarthGeneratorHelpers.IsCloseEnough(edgeOriginal.VerticeStart, edge.VerticeStart) && EarthGeneratorHelpers.IsCloseEnough(edgeOriginal.VerticeEnd, edge.VerticeEnd))
            {
                edgesList.RemoveAt(i);

                edgePair = edge;
                flipped = false;
                return;
            }

            if (EarthGeneratorHelpers.IsCloseEnough(edgeOriginal.VerticeStart, edge.VerticeEnd) && EarthGeneratorHelpers.IsCloseEnough(edgeOriginal.VerticeEnd, edge.VerticeStart))
            {
                edgesList.RemoveAt(i);

                edgePair = edge;
                flipped = true;
                return;
            }
        }

        throw new System.Exception("EarthGeneratorUtilities :: FindPair :: Cannot match edge!");
    }
}