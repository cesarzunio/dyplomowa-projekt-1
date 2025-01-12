using Unity.Mathematics;

public readonly struct IndiceLocPair
{
    public readonly IndiceLoc First;
    public readonly IndiceLoc Second;

    public IndiceLocPair(IndiceLoc first, IndiceLoc second)
    {
        First = first;
        Second = second;
    }

    public double3 GetAverageVertex(MeshData[] meshDatas)
    {
        return (meshDatas.GetVertex(First) + meshDatas.GetVertex(Second)) / 2;
    }
}
