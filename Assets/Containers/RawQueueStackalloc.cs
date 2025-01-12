using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

public unsafe struct RawQueueStackalloc<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction]
    readonly T* _buffer;

    int _head;
    int _tail;
    int _count;
    readonly int _capacity;

    public readonly int Count => _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RawQueueStackalloc(T* buffer, int capacity)
    {
#if CES_COLLECTIONS_CHECK
        if (capacity < 1)
            throw new Exception("RawQueueStackalloc :: Capacity must be higher than 0!");
#endif

        _buffer = buffer;
        _head = 0;
        _tail = 0;
        _count = 0;
        _capacity = capacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(T item)
    {
#if CES_COLLECTIONS_CHECK
        if (_count == _capacity)
            throw new Exception("RawQueueStackalloc :: Enqueue :: Queue is full!");
#endif

        _buffer[_tail] = item;
        _tail = (_tail + 1) % _capacity;
        _count++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Dequeue()
    {
#if CES_COLLECTIONS_CHECK
        if (_count == 0)
            throw new Exception("RawQueueStackalloc :: Dequeue :: Queue is empty!");
#endif

        var item = _buffer[_head];
        _head = (_head + 1) % _capacity;
        _count--;

        return item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T Peek()
    {
#if CES_COLLECTIONS_CHECK
        if (_count == 0)
            throw new Exception("RawQueueStackalloc :: Peek :: Queue is empty!");
#endif

        return _buffer[_head];
    }
}
