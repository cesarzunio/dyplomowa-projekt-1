/// <summary>
/// Temporary struct that holds edge indices
/// </summary>
public readonly struct MeshDataEdgeIndices
{
    public readonly int[] Left;
    public readonly int[] LeftFlipped;

    public readonly int[] Right;
    public readonly int[] RightFlipped;

    public readonly int[] Bot;
    public readonly int[] BotFlipped;

    public MeshDataEdgeIndices(int rows)
    {
        CreateIndices(rows, MeshDataEdgeType.Left, out Left, out LeftFlipped);
        CreateIndices(rows, MeshDataEdgeType.Right, out Right, out RightFlipped);
        CreateIndices(rows, MeshDataEdgeType.Bot, out Bot, out BotFlipped);
    }

    public readonly int[] GetIndices(MeshDataEdgeType edgeType, bool flipped) => edgeType switch
    {
        MeshDataEdgeType.Left => flipped ? LeftFlipped : Left,
        MeshDataEdgeType.Right => flipped ? RightFlipped : Right,
        MeshDataEdgeType.Bot => flipped ? BotFlipped : Bot,

        _ => throw new System.Exception("MeshDataEdgeIndices :: GetIndices :: Cannot match EdgeType: " + edgeType)
    };

    static void CreateIndices(int rows, MeshDataEdgeType edgeType, out int[] indices, out int[] indicesFlipped)
    {
        (int indiceStart, int rowInc, int flatInc) = edgeType switch
        {
            MeshDataEdgeType.Left => (0, 1, 1),
            MeshDataEdgeType.Right => (0, 1, 2),
            MeshDataEdgeType.Bot => (EarthGeneratorHelpers.MeshDataBotLeftIndice(rows), 0, 1),

            _ => throw new System.Exception("MeshDataEdgeIndices :: CreateIndices :: Cannot match EdgeType: " + edgeType)
        };

        indices = new int[rows];
        indicesFlipped = new int[rows];

        int indice = indiceStart;

        for (int r = 0; r < rows; r++)
        {
            indicesFlipped[rows - r - 1] = indices[r] = indice;
            indice += (rowInc * r) + flatInc;
        }
    }
}
