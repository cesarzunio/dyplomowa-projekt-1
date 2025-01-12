using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public interface IHeuristicable
{
    public double GetH(int2 item);
}
