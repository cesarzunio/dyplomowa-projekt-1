using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public unsafe interface IRawSerializable<T> where T : unmanaged
{
    int GetSerializationLength();
    T* GetSerializedData();
}