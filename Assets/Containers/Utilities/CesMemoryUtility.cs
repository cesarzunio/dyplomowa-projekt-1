using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public static unsafe class CesMemoryUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* Allocate<T>(long length, Allocator allocator, bool clearMemory = false) where T : unmanaged
    {
        long sizeT = UnsafeUtility.SizeOf<T>() * length;
        int alignOfT = UnsafeUtility.AlignOf<T>();

        var ptr = (T*)UnsafeUtility.Malloc(sizeT, alignOfT, allocator);

        if (clearMemory)
        {
            UnsafeUtility.MemClear(ptr, sizeT);
        }

        return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T** AllocatePtrs<T>(long length, Allocator allocator) where T : unmanaged
    {
        long sizePtr = UnsafeUtility.SizeOf<IntPtr>() * length;
        int alignOfPtr = UnsafeUtility.AlignOf<IntPtr>();

        return (T**)UnsafeUtility.Malloc(sizePtr, alignOfPtr, allocator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy<T>(long length, T* destination, T* source) where T : unmanaged
    {
        long sizeT = UnsafeUtility.SizeOf<T>() * length;

        UnsafeUtility.MemCpy(destination, source, sizeT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyAndFree<T>(long length, T* destination, T* source, Allocator allocator) where T : unmanaged
    {
        Copy(length, destination, source);

        UnsafeUtility.Free(source, allocator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ShiftLeftByOne<T>(T* destination, long elementsToMove) where T : unmanaged
    {
        long sizeT = UnsafeUtility.SizeOf<T>() * elementsToMove;

        UnsafeUtility.MemMove(destination, destination + 1, sizeT);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ShiftRightByOne<T>(T* destination, long elementsToMove) where T : unmanaged
    {
        long sizeT = UnsafeUtility.SizeOf<T>() * elementsToMove;

        UnsafeUtility.MemMove(destination, destination - 1, sizeT);
    }
}
