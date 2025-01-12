using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using System;

[NoAlias]
public unsafe struct RawArray<T> : IRawSerializable<T>, IDisposable
    where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction, NoAlias]
    T* _data;

    public readonly int Length;
    public readonly Allocator Allocator;

    public readonly bool IsCreated => _data != null;
    public readonly T* Data => _data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RawArray(Allocator allocator, int length)
    {
        if (length < 1)
        {
            _data = null;
            Length = 0;
            Allocator = allocator;
            return;
        }

        _data = CesMemoryUtility.Allocate<T>(length, allocator);
        Length = length;
        Allocator = allocator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RawArray(Allocator allocator, T valueDefault, int length) : this(allocator, length)
    {
        for (int i = 0; i < length; i++)
        {
            _data[i] = valueDefault;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RawArray(RawSerializationData<T> serializationData)
    {
        _data = serializationData.Data;
        Length = serializationData.Length;
        Allocator = serializationData.Allocator;
    }

    public static RawArray<T> Null() => new(Allocator.None, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Set(T value)
    {
        for (int i = 0; i < Length; i++)
        {
            _data[i] = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!IsCreated)
            return;

        UnsafeUtility.Free(_data, Allocator);
        _data = null;
    }

    public readonly ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (index < 0 || index >= Length)
                throw new Exception($"RawArray :: this[int] :: Index ({index}) out of range ({Length})!");

            return ref _data[index];
        }
    }

    public readonly ref T this[uint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (index >= (uint)Length)
                throw new Exception($"RawArray :: this[uint] :: Index ({index}) out of range ({Length})!");

            return ref _data[index];
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T* Ptr(int index) => _data + index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T* Ptr(uint index) => _data + index;

    public readonly int GetSerializationLength() => Length;

    public readonly T* GetSerializedData() => _data;
}