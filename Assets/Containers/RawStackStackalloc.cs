using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public unsafe struct RawStackStackalloc<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction]
    readonly T* _stack;

    int _count;
    readonly int _capacity;

    public readonly int Count => _count;
    public readonly int Capacity => _capacity;

    public RawStackStackalloc(T* stackPtr, int capacity)
    {
        _stack = stackPtr;
        _count = 0;
        _capacity = capacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T value) => _stack[_count++] = value;

    public bool TryPop(out T value)
    {
        if (_count == 0)
        {
            value = default;
            return false;
        }

        value = Pop();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop() => _stack[--_count];
}
