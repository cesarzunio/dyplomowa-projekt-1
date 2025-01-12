using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using System;

[NoAlias]
public unsafe struct RawBag<T> : IRawSerializable<T>, IDisposable
    where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction, NoAlias]
    T* _data;

    int _count;
    int _capacity;
    public readonly Allocator Allocator;

    public readonly bool IsCreated => _data != null;
    public readonly int Count => _count;
    public readonly int Capacity => _capacity;

    public RawBag(Allocator allocator, int capacity = 8)
    {
        _data = null;
        _count = 0;
        _capacity = 0;
        Allocator = allocator;

        if (capacity > 0)
        {
            SetCapacity(capacity);
        }
    }

    public RawBag(RawSerializationData<T> serializationData)
    {
        _data = serializationData.Data;
        _count = serializationData.Length;
        _capacity = serializationData.Length;
        Allocator = serializationData.Allocator;
    }

    public static RawBag<T> Null() => new(Allocator.None, 0);

    public void Dispose()
    {
        if (!IsCreated)
            return;

        UnsafeUtility.Free(_data, Allocator);
        _data = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _count = 0;

    public readonly ref T this[int index] => ref _data[index];

    public readonly ref T this[uint index] => ref _data[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T* Ptr(int index) => _data + index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T* Ptr(uint index) => _data + index;

    public void Add(T value)
    {
        if (_count == _capacity)
            SetCapacity(_capacity * 2);

        _data[_count++] = value;
    }

    public void Insert(T value, int index)
    {
        if (_count == _capacity)
            SetCapacity(_capacity * 2);

        int elementsToMove = _count - index;

        if (elementsToMove > 0)
        {
            CesMemoryUtility.ShiftRightByOne(_data + index + 1, elementsToMove);
        }

        _data[index] = value;
        _count++;
    }

    public IndexChange RemoveAt(int index)
    {
        _count--;

        if (index == _count)
            return IndexChange.None;

        _data[index] = _data[_count];

        return new IndexChange(_count, index);
    }

    public void Reverse()
    {
        for (int i = 0, j = _count - 1; i < j; i++, j--)
        {
            (_data[j], _data[i]) = (_data[i], _data[j]);
        }
    }

    void SetCapacity(int capacity)
    {
        if (capacity <= _capacity)
            return;

        var data = CesMemoryUtility.Allocate<T>(capacity, Allocator);

        if (_capacity > 0)
        {
            CesMemoryUtility.CopyAndFree(_capacity, data, _data, Allocator);
        }

        _data = data;
        _capacity = capacity;
    }

    public readonly int GetSerializationLength() => _count;

    public readonly T* GetSerializedData() => _data;
}