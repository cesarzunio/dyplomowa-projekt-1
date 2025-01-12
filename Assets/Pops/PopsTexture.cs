using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public readonly unsafe struct PopsTexture
{
    public readonly int2 TextureSize;

    [NativeDisableUnsafePtrRestriction]
    public readonly float* Pops;

    readonly Allocator _allocator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PopsTexture(int2 textureSize, float* pops, Allocator allocator)
    {
        TextureSize = textureSize;
        Pops = pops;
        _allocator = allocator;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        UnsafeUtility.Free(Pops, _allocator);
    }
}
