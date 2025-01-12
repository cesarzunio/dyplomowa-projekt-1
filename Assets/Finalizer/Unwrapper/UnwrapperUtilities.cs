using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class UnwrapperUtilities
{
    const Allocator ALLOCATOR = Allocator.Persistent;
    const int RIVER_COORDS = 2048;

    public static void SaveRiversDataFinal(string path, RawArray<RiverData> riversData)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        fileStream.WriteValue(riversData.Length);

        for (int i = 0; i < riversData.Length; i++)
        {
            BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, riversData[i].StartsFrom);
            BinarySaveUtility.WriteRawContainerSimple<RawArray<int2>, int2>(fileStream, riversData[i].PixelCoords);
            BinarySaveUtility.WriteRawContainerSimple<RawBag<uint>, uint>(fileStream, riversData[i].EndsInto);
        }
    }

    public static RawArray<RiverData> LoadRiversData(string path, Allocator allocator)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        int length = binaryReader.ReadInt32();
        var riversData = new RawArray<RiverData>(allocator, length);

        for (int i = 0; i < length; i++)
        {
            riversData[i] = new RiverData
            {
                StartsFrom = BinarySaveUtility.ReadRawBag<uint>(fileStream, binaryReader, allocator),
                PixelCoords = BinarySaveUtility.ReadRawArray<int2>(fileStream, binaryReader, allocator),
                EndsInto = BinarySaveUtility.ReadRawBag<uint>(fileStream, binaryReader, allocator),
            };
        }

        return riversData;
    }

    public static RawArray<RiverData> CreateRiversDataFinal(RawArray<int> riversIndexesMap, RawBag<RawBag<int2>> riversCoords, int2 textureSize, Allocator allocator)
    {
        var riversData = new RawArray<RiverData>(allocator, riversCoords.Count);
        var endsIntoTemp = new RawArray<RawBag<uint>>(allocator, riversData.Length);
        var startsFromTemp = new RawArray<RawBag<uint>>(allocator, riversData.Length);

        for (int i = 0; i < riversData.Length; i++)
        {
            riversData[i] = new RiverData
            {
                StartsFrom = new RawBag<uint>(allocator),
                PixelCoords = ToRawArray(riversCoords[i], allocator),
                EndsInto = new RawBag<uint>(allocator),
            };

            endsIntoTemp[i] = new RawBag<uint>(allocator);
            startsFromTemp[i] = new RawBag<uint>(allocator);
        }

        for (int i = 0; i < riversCoords.Count; i++)
        {
            var startPixelCoord = riversCoords[i][0];
            var endPixelCoord = riversCoords[i][^1];

            int startFlat = TexUtilities.PixelCoordToFlat(startPixelCoord, textureSize.x);
            int endFlat = TexUtilities.PixelCoordToFlat(endPixelCoord, textureSize.x);

            int startRiverIndex = riversIndexesMap[startFlat];
            int endRiverIndex = riversIndexesMap[endFlat];

            if (startRiverIndex != i)
            {
                endsIntoTemp[startRiverIndex].Add((uint)i);
            }

            if (endRiverIndex != i)
            {
                startsFromTemp[endRiverIndex].Add((uint)i);
            }
        }

        for (int i = 0; i < riversData.Length; i++)
        {
            for (int j = 0; j < endsIntoTemp[i].Count; j++)
            {
                riversData[i].EndsInto.Add(endsIntoTemp[i][j]);
                riversData[endsIntoTemp[i][j]].StartsFrom.Add((uint)i);
            }

            for (int j = 0; j < startsFromTemp[i].Count; j++)
            {
                riversData[i].StartsFrom.Add(startsFromTemp[i][j]);
                riversData[startsFromTemp[i][j]].EndsInto.Add((uint)i);
            }
        }

        for (int i = 0; i < riversData.Length; i++)
        {
            endsIntoTemp[i].Dispose();
            startsFromTemp[i].Dispose();
        }

        endsIntoTemp.Dispose();
        startsFromTemp.Dispose();

        return riversData;
    }

    static RawArray<int2> ToRawArray(RawBag<int2> rawBag, Allocator allocator)
    {
        var array = new RawArray<int2>(allocator, rawBag.Count);

        for (int i = 0; i < array.Length; i++)
        {
            array[i] = rawBag[i];
        }

        return array;
    }

    public static void CreateRiversData(
        RawArray<int> regionsMap, RawArray<int> riversMap, int2 textureSize, Allocator allocator,
        out RawArray<RiverPointType> riversPointTypes, out RawArray<int> riversIndexesMap, out RawBag<RawBag<int2>> riversCoords)
    {
        RiverUtilities.FindMouths(riversMap, textureSize, ALLOCATOR, out var mouthsPrimary, out var mouthsSecondary);
        var multipliers = RiverUtilities.GenerateNeighborMultipliers(regionsMap, textureSize, ALLOCATOR);

        riversPointTypes = new RawArray<RiverPointType>(ALLOCATOR, RiverPointType.None, riversMap.Length);
        riversIndexesMap = new RawArray<int>(ALLOCATOR, -1, riversMap.Length);
        riversCoords = new RawBag<RawBag<int2>>(allocator, RIVER_COORDS);
        var riversCoordsPtr = new RawPtr<RawBag<RawBag<int2>>>(ALLOCATOR, riversCoords);

        var job = new UnwrapperJob3
        {
            TextureSize = textureSize,
            MouthsPrimary = mouthsPrimary,
            MouthsSecondary = mouthsSecondary,
            RiversMap = riversMap,
            Multipliers = multipliers,
            RiversPointTypes = riversPointTypes,
            RiversIndexesMap = riversIndexesMap,
            RiversCoords = riversCoordsPtr,
            Allocator = allocator
        };

        job.Schedule().Complete();

        riversCoords = riversCoordsPtr.Value;

        mouthsPrimary.Dispose();
        mouthsSecondary.Dispose();
        multipliers.Dispose();
        riversCoordsPtr.Dispose();
    }

    public enum RiverPointType : int
    {
        None = -1,

        River = 0,
        Source = 1,

        MouthPrimary = 2,
        MouthSecondary = 3,

        ConnectionIn = 4,
        ConnectionInPoint = 5,

        ConnectionOut = 6,
        ConnectionOutPoint = 7,
    }

    public struct RiverData : IDisposable
    {
        public RawBag<uint> StartsFrom;
        public RawArray<int2> PixelCoords;
        public RawBag<uint> EndsInto;

        public void Dispose()
        {
            StartsFrom.Dispose();
            PixelCoords.Dispose();
            EndsInto.Dispose();
        }
    }
}
