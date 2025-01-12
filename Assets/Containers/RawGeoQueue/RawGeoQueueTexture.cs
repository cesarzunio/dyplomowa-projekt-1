using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

[NoAlias]
public unsafe struct RawGeoQueueTexture
{
    [NativeDisableUnsafePtrRestriction, NoAlias]
    int2* _heap;

    [NativeDisableUnsafePtrRestriction, NoAlias]
    double* _costs;

    [NativeDisableUnsafePtrRestriction, NoAlias]
    int* _indexInHeap;

    int _count;
    int _capacity;
    readonly int2 _textureSize;
    readonly Allocator _allocator;

    public readonly bool IsCreated => _heap != null;
    public readonly int Count => _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RawGeoQueueTexture(int capacity, int2 textureSize, Allocator allocator)
    {
        _heap = null;
        _costs = null;
        _indexInHeap = null;

        _count = 0;
        _capacity = 0;
        _textureSize = textureSize;
        _allocator = allocator;

        if (capacity > 0)
        {
            int length = textureSize.x * textureSize.y;

            _costs = CesMemoryUtility.Allocate<double>(length, allocator);
            _indexInHeap = CesMemoryUtility.Allocate<int>(length, allocator);
            
            for (int i = 0; i < length; i++)
            {
                _indexInHeap[i] = -1;
            }

            SetCapacity(capacity);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(int2 item, double cost)
    {
        if (_count == _capacity)
            SetCapacity(_capacity * 2);

        int itemFlat = ItemFlat(item);

        _heap[_count++] = item;
        _costs[itemFlat] = cost;
        _indexInHeap[itemFlat] = _count - 1;

        HeapifyUp(_count - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddOrUpdate(int2 item, double cost)
    {
        int itemFlat = ItemFlat(item);
        int indexInHeap = _indexInHeap[itemFlat];

        if (indexInHeap != -1)
        {
            _costs[itemFlat] = cost;
            HeapifyUp(indexInHeap);
            return;
        }

        Add(item, cost);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int2 Pop()
    {
        var root = _heap[0];
        int rootFlat = ItemFlat(root);

        _heap[0] = _heap[_count - 1];
        _indexInHeap[ItemFlat(_heap[0])] = 0;

        _count--;
        _indexInHeap[rootFlat] = -1;

        HeapifyDown(0);

        return root;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out int2 item)
    {
        if (_count == 0)
        {
            item = default;
            return false;
        }

        item = Pop();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;

        int length = _textureSize.x * _textureSize.y;

        for (int i = 0; i < length; i++)
        {
            _indexInHeap[i] = -1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetCost(int2 item) => _costs[ItemFlat(item)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCost(int2 item, out double cost)
    {
        int itemFlat = ItemFlat(item);
        int indexInHeap = _indexInHeap[itemFlat];

        if (Hint.Unlikely(indexInHeap == -1))
        {
            cost = default;
            return false;
        }

        cost = _costs[itemFlat];
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        UnsafeUtility.Free(_heap, _allocator);
        UnsafeUtility.Free(_costs, _allocator);
        UnsafeUtility.Free(_indexInHeap, _allocator);

        _heap = null;
        _costs = null;
        _indexInHeap = null;
    }

    void SetCapacity(int capacity)
    {
        if (capacity <= _capacity)
            return;

        var heap = CesMemoryUtility.Allocate<int2>(capacity, _allocator);

        if (IsCreated)
        {
            CesMemoryUtility.CopyAndFree(_capacity, heap, _heap, _allocator);
        }

        _heap = heap;
        _capacity = capacity;
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
                Swap(ref index, parentIndex);
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
            int rightChildIndex = leftChildIndex + 1;
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

            Swap(ref index, smallest);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Swap(ref int index, int indexNew)
    {
        int itemFlat = ItemFlat(_heap[index]);
        int smallestItemFlat = ItemFlat(_heap[indexNew]);

        _indexInHeap[itemFlat] = indexNew;
        _indexInHeap[smallestItemFlat] = index;

        (_heap[index], _heap[indexNew]) = (_heap[indexNew], _heap[index]);
        index = indexNew;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IsLowerCost(int2 lhs, int2 rhs) => _costs[ItemFlat(lhs)] < _costs[ItemFlat(rhs)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly int ItemFlat(int2 item) => TexUtilities.PixelCoordToFlat(item, _textureSize.x);
}
