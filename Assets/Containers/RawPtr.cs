using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public unsafe struct RawPtr<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction]
    T* _ptr;

    readonly Allocator _allocator;

    public readonly bool IsCreated => _ptr != null;

    public RawPtr(Allocator allocator)
    {
        _ptr = CesMemoryUtility.Allocate<T>(1, allocator);
        _allocator = allocator;
    }

    public RawPtr(Allocator allocator, T value) : this(allocator)
    {
        *_ptr = value;
    }

    public void Dispose()
    {
        if (!IsCreated)
            throw new System.Exception("RawPtr :: Dispose :: Is already disposed!");

        UnsafeUtility.Free(_ptr, _allocator);
        _ptr = null;
    }

    public T Value => *_ptr;

    public ref T Ref => ref (*_ptr);

    public readonly T* Ptr => _ptr;
}
