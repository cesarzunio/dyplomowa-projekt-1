using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public readonly struct IndexChange
{
    public readonly int From;
    public readonly int To;

    public static readonly IndexChange None = new(-1, -1);

    public IndexChange(int from, int to)
    {
        From = from;
        To = to;
    }
}