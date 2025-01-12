using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static class RawArrayUtilities
{
    public static RawArray<T> ToRawArray<T>(this T[] array, Allocator allocator) where T : unmanaged
    {
        var rawArray = new RawArray<T>(allocator, array.Length);

        for (int i = 0; i < array.Length; i++)
        {
            rawArray[i] = array[i];
        }

        return rawArray;
    }

    public static T[] ToArray<T>(this RawArray<T> rawArray) where T : unmanaged
    {
        var array = new T[rawArray.Length];

        for (int i = 0; i < rawArray.Length; i++)
        {
            array[i] = rawArray[i];
        }

        return array;
    }

    public static T[] ToArray<T>(this RawBag<T> rawBag) where T : unmanaged
    {
        var array = new T[rawBag.Count];

        for (int i = 0; i < array.Length; i++)
        {
            array[i] = rawBag[i];
        }

        return array;
    }

    public static RawArray<int> ColorsToInts(this NativeArray<Color32> nativeArray, Allocator allocator)
    {
        var rawArray = new RawArray<int>(allocator, nativeArray.Length);

        for (int i = 0; i < nativeArray.Length; i++)
        {
            rawArray[i] = nativeArray[i].ToIndex();
        }

        return rawArray;
    }

    public static RawArray<int> ColorsToIntsCheckAlpha(this NativeArray<Color32> nativeArray, Allocator allocator)
    {
        var rawArray = new RawArray<int>(allocator, nativeArray.Length);

        for (int i = 0; i < nativeArray.Length; i++)
        {
            rawArray[i] = nativeArray[i].a == 0 ? -1 : nativeArray[i].ToIndex();
        }

        return rawArray;
    }

    public static Color32[] IntsToColors(this RawArray<int> rawArray)
    {
        var array = new Color32[rawArray.Length];

        for (int i = 0; i < rawArray.Length; i++)
        {
            array[i] = rawArray[i].ToColor32();
        }

        return array;
    }

    public static Color32[] IntsToColorsCheckAlpha(this RawArray<int> rawArray)
    {
        var array = new Color32[rawArray.Length];

        for (int i = 0; i < rawArray.Length; i++)
        {
            array[i] = rawArray[i] == -1 ? new Color32(0, 0, 0, 0) : rawArray[i].ToColor32();
        }

        return array;
    }

    public static RawArray<float> ByteToFloat(this NativeArray<byte> nativeArray, Allocator allocator)
    {
        var rawArray = new RawArray<float>(allocator, nativeArray.Length);

        for (int i = 0; i < nativeArray.Length; i++)
        {
            rawArray[i] = nativeArray[i] / 255f;
        }

        return rawArray;
    }

    public static int[] ToIntsArray(this RawArray<int2> rawBag, int textureSizeX)
    {
        var ints = new int[rawBag.Length];

        for (int i = 0; i < ints.Length; i++)
        {
            ints[i] = TexUtilities.PixelCoordToFlat(rawBag[i], textureSizeX);
        }

        return ints;
    }

    public static int[] ToIntsArray(this RawBag<int2> rawBag, int textureSizeX)
    {
        var ints = new int[rawBag.Count];

        for (int i = 0; i < ints.Length; i++)
        {
            ints[i] = TexUtilities.PixelCoordToFlat(rawBag[i], textureSizeX);
        }

        return ints;
    }
}
