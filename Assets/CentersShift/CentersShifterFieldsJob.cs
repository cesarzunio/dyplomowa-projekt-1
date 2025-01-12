using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public unsafe struct CentersShifterFieldsJob : IJob
{
    const int NEIGHBORS = 4;

    public int2 TextureSize;
    public RawGeoQueueTexture Queue;
    public RawArray<int> CoordToField;
    public RawArray<bool> Closed;
    public RawArray<int> CentersMap;
    public RawArray<int> RegionsMap;
    public RawArray<bool> RiversMap;
    public RawArray<int> FieldsMap;

    [BurstCompile]
    public void Execute()
    {
        Queue.Clear();

        AddStartings(ref Queue, CoordToField, CentersMap, RiversMap, TextureSize);

        var neighbors = stackalloc int2[NEIGHBORS];

        while (Queue.TryPop(out var current))
        {
            int currentFlat = TexUtilities.PixelCoordToFlat(current, TextureSize.x);
            var currentField = CoordToField[currentFlat];
            var currentRegion = RegionsMap[currentFlat];

            Closed[currentFlat] = true;
            FieldsMap[currentFlat] = currentField;

            bool currentIsRiver = RiversMap[currentFlat];

            var currentPlaneUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(current, TextureSize);
            var currentUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(currentPlaneUv);

            double currentCost = Queue.GetCost(current);

            TexUtilities.GetNeighbors4(current, TextureSize, neighbors);

            for (int i = 0; i < NEIGHBORS; i++)
            {
                var neighbor = neighbors[i];
                var neighborFlat = TexUtilities.PixelCoordToFlat(neighbor, TextureSize.x);

                if (Hint.Unlikely(Closed[neighborFlat]))
                    continue;

                var neighborRegion = RegionsMap[neighborFlat];
                bool neighborIsRiver = RiversMap[neighborFlat];

                if (Hint.Unlikely(currentRegion != neighborRegion))
                    continue;

                if (Hint.Unlikely(currentIsRiver && !neighborIsRiver))
                    continue;

                var neighborPlaneUv = GeoUtilitiesDouble.PixelCoordToPlaneUv(neighbor, TextureSize);
                var neighborUnitSphere = GeoUtilitiesDouble.PlaneUvToUnitSphere(neighborPlaneUv);

                double distance = GeoUtilitiesDouble.Distance(currentUnitSphere, neighborUnitSphere);
                double costNew = currentCost + distance;

                if (!Queue.TryGetCost(neighbor, out double cost) || costNew < cost)
                {
                    Queue.AddOrUpdate(neighbor, costNew);
                    CoordToField[neighborFlat] = currentField;
                }
            }
        }
    }

    static void AddStartings(ref RawGeoQueueTexture queue, RawArray<int> coordToField, RawArray<int> centersMap, RawArray<bool> riversMap, int2 textureSize)
    {
        for (int i = 0; i < centersMap.Length; i++)
        {
            if (Hint.Likely(centersMap[i] == -1 || riversMap[i]))
                continue;

            var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, textureSize.x);

            queue.Add(pixelCoord, 0.0);
            coordToField[i] = centersMap[i];
        }
    }
}
