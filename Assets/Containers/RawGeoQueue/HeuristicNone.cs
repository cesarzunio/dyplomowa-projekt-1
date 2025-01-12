using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

public struct HeuristicNone : IHeuristicable
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double GetH(int2 item) => 0.0;
}
