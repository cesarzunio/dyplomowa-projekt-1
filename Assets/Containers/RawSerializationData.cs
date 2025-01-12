using System.Runtime.CompilerServices;
using Unity.Collections;

public readonly unsafe struct RawSerializationData<T> where T : unmanaged
{
    public readonly T* Data;
    public readonly int Length;
    public readonly Allocator Allocator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RawSerializationData(T* array, int length, Allocator allocator)
    {
        Data = array;
        Length = length;
        Allocator = allocator;
    }

    public readonly bool IsInvalid => Data == null || Length <= 0;
}
