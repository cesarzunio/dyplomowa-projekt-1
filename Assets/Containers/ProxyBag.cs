using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using System;

public unsafe struct ProxyBag<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction]
    RawBag<T>* _rawBag;

    public readonly bool IsCreated => _rawBag != null;
    public readonly int Count => _rawBag->Count;
    public readonly int Capacity => _rawBag->Capacity;

    public ProxyBag(Allocator allocator, int capacity = 8)
    {
        _rawBag = null;

        if (capacity > 0)
        {
            _rawBag = (RawBag<T>*)CesMemoryUtility.AllocatePtrs<RawBag<T>>(1, allocator);
            *_rawBag = new RawBag<T>(allocator, capacity);
        }
    }

    public void Dispose()
    {
        if (!IsCreated)
            return;

        var allocator = _rawBag->Allocator;

        _rawBag->Dispose();

        UnsafeUtility.Free(_rawBag, allocator);
        _rawBag = null;
    }

    public ref T this[int index] => ref (*_rawBag)[index];

    public ref T this[uint index] => ref (*_rawBag)[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T* Ptr(int index) => _rawBag->Ptr(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T* Ptr(uint index) => _rawBag->Ptr(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T value) => _rawBag->Add(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IndexChange RemoveAt(int index) => _rawBag->RemoveAt(index);
}