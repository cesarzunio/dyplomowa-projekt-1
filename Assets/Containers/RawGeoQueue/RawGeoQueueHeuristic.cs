using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public unsafe struct RawGeoQueueHeuristic<H> where H : unmanaged, IHeuristicable
{
    [NativeDisableUnsafePtrRestriction]
    int2* _heap;

    UnsafeHashMap<int2, double> _costs;
    UnsafeHashMap<int2, int> _indexInHeap;

    int _count;
    int _capacity;
    H _heuristic;

    readonly Allocator _allocator;

    public readonly bool IsCreated => _heap != null;
    public readonly int Count => _count;

    public RawGeoQueueHeuristic(int capacity, int hashMapsCapacity, H heuristic, Allocator allocator)
    {
        _heap = null;
        _costs = new UnsafeHashMap<int2, double>(hashMapsCapacity, allocator);
        _indexInHeap = new UnsafeHashMap<int2, int>(hashMapsCapacity, allocator);

        _count = 0;
        _capacity = 0;
        _heuristic = heuristic;
        _allocator = allocator;

        SetCapacity(capacity);
    }

    public void Add(int2 item, double cost)
    {
        if (_count == _capacity)
            SetCapacity(_capacity * 2);

        _heap[_count++] = item;
        _costs[item] = cost;
        _indexInHeap[item] = _count - 1;

        HeapifyUp(_count - 1);
    }

    public void AddOrUpdate(int2 item, double cost)
    {
        if (_indexInHeap.TryGetValue(item, out int index))
        {
            _costs[item] = cost;
            HeapifyUp(index);
            return;
        }

        Add(item, cost);
    }

    public int2 Pop()
    {
        var root = _heap[0];

        _heap[0] = _heap[_count - 1];
        _indexInHeap[_heap[0]] = 0;

        _count--;
        _indexInHeap.Remove(root);

        HeapifyDown(0);

        return root;
    }

    public bool TryPop(out int2 item)
    {
        if (Count == 0)
        {
            item = default;
            return false;
        }

        item = Pop();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(H heuristic)
    {
        _count = 0;
        _heuristic = heuristic;
        _costs.Clear();
        _indexInHeap.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCost(int2 item, double cost) => _costs[item] = cost;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetCost(int2 item) => _costs[item];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCost(int2 item, out double cost) => _costs.TryGetValue(item, out cost);

    public void Dispose()
    {
        if (!IsCreated)
            return;

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
    bool IsLowerCost(int2 lhs, int2 rhs)
    {
        double lhsH = _heuristic.GetH(lhs);
        double rhsH = _heuristic.GetH(rhs);

        return _costs[lhs] + lhsH < _costs[rhs] + rhsH;
    }

    void SetCapacity(int capacity)
    {
        if (capacity <= _capacity)
            return;

        var heap = CesMemoryUtility.Allocate<int2>(capacity, _allocator);

        if (_capacity > 0)
        {
            CesMemoryUtility.CopyAndFree(_capacity, heap, _heap, _allocator);
        }

        _heap = heap;
        _capacity = capacity;
    }
}
