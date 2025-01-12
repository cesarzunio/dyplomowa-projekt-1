using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Burst.CompilerServices;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using System;

public static unsafe class TexUtilities
{
    public static int2 ClampPixelCoord(int2 pixelCoord, int2 textureSize)
    {
        if (pixelCoord.y < 0)
        {
            pixelCoord.x += textureSize.x / 2;
            pixelCoord.y = math.abs(pixelCoord.y);
        }
        else if (pixelCoord.y >= textureSize.y)
        {
            pixelCoord.x += textureSize.x / 2;
            pixelCoord.y = textureSize.y - 1 - (pixelCoord.y - textureSize.y);
        }

        pixelCoord.x = (pixelCoord.x + textureSize.x) % textureSize.x;

        return pixelCoord;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PixelCoordToFlat(int pixelCoordX, int pixelCoordY, int textureSizeX) => pixelCoordX + (pixelCoordY * textureSizeX);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PixelCoordToFlat(int2 pixelCoord, int textureSizeX) => PixelCoordToFlat(pixelCoord.x, pixelCoord.y, textureSizeX);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PixelCoordToFlatLong(long pixelCoordX, long pixelCoordY, long textureSizeX) => pixelCoordX + (pixelCoordY * textureSizeX);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PixelCoordToFlatLong(int2 pixelCoord, int textureSizeX) => PixelCoordToFlatLong(pixelCoord.x, pixelCoord.y, textureSizeX);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 FlatToPixelCoordInt2(int flat, int textureSizeX) => new(flat % textureSizeX, flat / textureSizeX);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FlatToPixelCoordInts(int flat, int textureSizeX, out int pixelCoordX, out int pixelCoordY)
    {
        pixelCoordX = flat % textureSizeX;
        pixelCoordY = flat / textureSizeX;
    }

    /// <summary>
    /// Up Down, Left Right
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetNeighbors4(int2 pixelCoord, int2 textureSize, int2* neighborsArray)
    {
        neighborsArray[0] = ClampPixelCoord(pixelCoord + new int2(0, 1), textureSize);
        neighborsArray[1] = ClampPixelCoord(pixelCoord + new int2(0, -1), textureSize);
        neighborsArray[2] = ClampPixelCoord(pixelCoord + new int2(-1, 0), textureSize);
        neighborsArray[3] = ClampPixelCoord(pixelCoord + new int2(1, 0), textureSize);
    }

    /// <summary>
    /// Left to Right, Down to Up
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetNeighbors8(int2 pixelCoord, int2 textureSize, int2* neighborsArray)
    {
        neighborsArray[0] = ClampPixelCoord(pixelCoord + new int2(-1, -1), textureSize);
        neighborsArray[1] = ClampPixelCoord(pixelCoord + new int2(0, -1), textureSize);
        neighborsArray[2] = ClampPixelCoord(pixelCoord + new int2(1, -1), textureSize);

        neighborsArray[3] = ClampPixelCoord(pixelCoord + new int2(-1, 0), textureSize);
        neighborsArray[4] = ClampPixelCoord(pixelCoord + new int2(1, 0), textureSize);

        neighborsArray[5] = ClampPixelCoord(pixelCoord + new int2(-1, 1), textureSize);
        neighborsArray[6] = ClampPixelCoord(pixelCoord + new int2(0, 1), textureSize);
        neighborsArray[7] = ClampPixelCoord(pixelCoord + new int2(1, 1), textureSize);
    }

    /// <summary>
    /// Left to Right, Down to Up
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetNeighbors8WithCenter(int2 pixelCoord, int2 textureSize, int2* neighborsArray)
    {
        neighborsArray[0] = ClampPixelCoord(pixelCoord + new int2(-1, -1), textureSize);
        neighborsArray[1] = ClampPixelCoord(pixelCoord + new int2(0, -1), textureSize);
        neighborsArray[2] = ClampPixelCoord(pixelCoord + new int2(1, -1), textureSize);

        neighborsArray[3] = ClampPixelCoord(pixelCoord + new int2(-1, 0), textureSize);
        neighborsArray[4] = pixelCoord;
        neighborsArray[5] = ClampPixelCoord(pixelCoord + new int2(1, 0), textureSize);

        neighborsArray[6] = ClampPixelCoord(pixelCoord + new int2(-1, 1), textureSize);
        neighborsArray[7] = ClampPixelCoord(pixelCoord + new int2(0, 1), textureSize);
        neighborsArray[8] = ClampPixelCoord(pixelCoord + new int2(1, 1), textureSize);
    }

    public static void GetNeighborsNxN(int2 pixelCoord, int2 textureSize, ref RawListStackalloc<int2> neighborsList, int n)
    {
        if (n % 2 == 0)
            throw new Exception($"TexUtilities :: GetNeighborsNxN :: n ({n}) must be odd!");

        neighborsList.Clear();

        for (int y = -n / 2; y <= n / 2; y++)
        {
            for (int x = -n / 2; x <= n / 2; x++)
            {
                if (y == 0 && x == 0)
                    continue;

                neighborsList.Add(ClampPixelCoord(pixelCoord + new int2(x, y), textureSize));
            }
        }
    }

    /// <summary>
    /// Up Down, Left Right
    /// </summary>
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public static void GetNeighbors4Edge(int2 pixelCoord, int2 textureSize, ref RawListStackalloc<int2> neighborsList)
    //{
    //    neighborsList.Clear();

    //    neighborsList.Add(ClampPixelCoord(pixelCoord + new int2(0, 1), textureSize));
    //    neighborsList.Add(ClampPixelCoord(pixelCoord + new int2(0, -1), textureSize));

    //    if (pixelCoord.y > 0 && pixelCoord.y < textureSize.y - 1)
    //    {
    //        neighborsList.Add(ClampPixelCoord(pixelCoord + new int2(-1, 0), textureSize));
    //        neighborsList.Add(ClampPixelCoord(pixelCoord + new int2(1, 0), textureSize));
    //    }
    //}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 FlipY(int2 pixelCoord, int textureSizeY) => new(pixelCoord.x, textureSizeY - pixelCoord.y - 1);
}

public readonly struct Neighbors4
{
    public readonly int2 Left;
    public readonly int2 Right;
    public readonly int2 Down;
    public readonly int2 Up;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Neighbors4(int2 pixelCoord, int2 textureSize)
    {
        Left = TexUtilities.ClampPixelCoord(pixelCoord + new int2(-1, 0), textureSize);
        Right = TexUtilities.ClampPixelCoord(pixelCoord + new int2(1, 0), textureSize);
        Down = TexUtilities.ClampPixelCoord(pixelCoord + new int2(0, -1), textureSize);
        Up = TexUtilities.ClampPixelCoord(pixelCoord + new int2(0, 1), textureSize);
    }
}