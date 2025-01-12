using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CesCollectionsUtility
{
    public static void DisposeDepth1<T>(this RawArray<T> rawArray) where T : unmanaged, IDisposable
    {
        for (int i = 0; i < rawArray.Length; i++)
        {
            rawArray[i].Dispose();
        }

        rawArray.Dispose();
    }

    public static void DisposeDepth1<T>(this RawBag<T> rawBag) where T : unmanaged, IDisposable
    {
        for (int i = 0; i < rawBag.Count; i++)
        {
            rawBag[i].Dispose();
        }

        rawBag.Dispose();
    }
}
