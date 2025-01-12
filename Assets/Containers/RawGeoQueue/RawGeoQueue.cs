using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public unsafe struct RawGeoQueue<T> where T : unmanaged, IEquatable<T>
{
    [NativeDisableUnsafePtrRestriction]
    T* _heap;

    UnsafeHashMap<T, double> _costs;
    UnsafeHashMap<T, int> _indexInHeap;
    int _count;
    int _capacity;

    readonly Allocator _allocator;

    public readonly int Count => _count;

    public RawGeoQueue(int capacity, int hashMapsCapacity, Allocator allocator)
    {
        _heap = null;
        _costs = new UnsafeHashMap<T, double>(hashMapsCapacity, allocator);
        _indexInHeap = new UnsafeHashMap<T, int>(hashMapsCapacity, allocator);

        _count = 0;
        _capacity = 0;
        _allocator = allocator;

        SetCapacity(capacity);
    }

    public void Add(T item, double cost)
    {
        if (_count == _capacity)
            SetCapacity(_capacity * 2);

        _heap[_count++] = item;
        _costs[item] = cost;
        _indexInHeap[item] = _count - 1;

        HeapifyUp(_count - 1);
    }

    public void AddOrUpdate(T item, double cost)
    {
        if (_indexInHeap.TryGetValue(item, out int index))
        {
            _costs[item] = cost;
            HeapifyUp(index);
            return;
        }

        Add(item, cost);
    }

    public T Pop()
    {
        var root = _heap[0];

        _heap[0] = _heap[_count - 1];
        _indexInHeap[_heap[0]] = 0;

        _count--;
        _indexInHeap.Remove(root);

        HeapifyDown(0);

        return root;
    }

    public bool TryPop(out T item)
    {
        if (Count == 0)
        {
            item = default;
            return false;
        }

        item = Pop();
        return true;
    }

    public void Clear()
    {
        _count = 0;
        _costs.Clear();
        _indexInHeap.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetCost(T item) => _costs[item];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCost(T item, out double cost) => _costs.TryGetValue(item, out cost);

    public void Dispose()
    {
        UnsafeUtility.Free(_heap, _allocator);
        _heap = null;

        _costs.Dispose();
        _indexInHeap.Dispose();
    }

    void HeapifyUp(int index)
    {
        if (index < 0 || index > _count - 1)
            return;

        var item = _heap[index];

        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;
            var parentItem = _heap[parentIndex];

            if (IsLowerCost(item, parentItem))
            {
                _indexInHeap[item] = parentIndex;
                _indexInHeap[parentItem] = index;

                (_heap[index], _heap[parentIndex]) = (_heap[parentIndex], _heap[index]);

                index = parentIndex;
            }
            else
            {
                break;
            }
        }
    }

    void HeapifyDown(int index)
    {
        int lastIndex = _count - 1;

        while (true)
        {
            int leftChildIndex = 2 * index + 1;
            int rightChildIndex = 2 * index + 2;
            int smallest = index;

            if (leftChildIndex <= lastIndex && IsLowerCost(_heap[leftChildIndex], _heap[smallest]))
            {
                smallest = leftChildIndex;
            }

            if (rightChildIndex <= lastIndex && IsLowerCost(_heap[rightChildIndex], _heap[smallest]))
            {
                smallest = rightChildIndex;
            }

            if (smallest == index)
                break;

            _indexInHeap[_heap[index]] = smallest;
            _indexInHeap[_heap[smallest]] = index;

            (_heap[index], _heap[smallest]) = (_heap[smallest], _heap[index]);
            index = smallest;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IsLowerCost(T lhs, T rhs) => _costs[lhs] < _costs[rhs];

    void SetCapacity(int capacity)
    {
        if (capacity <= _capacity)
            return;

        var heap = CesMemoryUtility.Allocate<T>(capacity, _allocator);

        if (_capacity > 0)
        {
            CesMemoryUtility.CopyAndFree(_capacity, heap, _heap, _allocator);
        }

        _heap = heap;
        _capacity = capacity;
    }
}
