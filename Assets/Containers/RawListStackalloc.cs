using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public unsafe struct RawListStackalloc<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction]
    readonly T* _array;

    int _count;
    readonly int _capacity;

    public readonly int Count => _count;
    public readonly int Capacity => _capacity;

    public RawListStackalloc(T* array, int capacity)
    {
        _array = array;
        _count = 0;
        _capacity = capacity;
    }

    public readonly ref T this[int index] => ref _array[index];

    public readonly ref T this[uint index] => ref _array[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T value)
    {
        if (_count == _capacity)
            throw new System.Exception("RawListStackalloc :: List is full!");

        _array[_count++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _count = 0;
}
