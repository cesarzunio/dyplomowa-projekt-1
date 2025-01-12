using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor.UIElements;

public static unsafe class BinarySaveUtility
{
    public static void WriteValue<T>(this FileStream fileStream, T value) where T : unmanaged
    {
        int sizeOfT = UnsafeUtility.SizeOf<T>();
        var array = stackalloc byte[sizeOfT];

        *(T*)array = value;

        fileStream.Write(new ReadOnlySpan<byte>(array, sizeOfT));
    }

    public static void WriteArraySimple<T>(FileStream fileStream, T* array, int length) where T : unmanaged
    {
        if (length <= 0)
            return;

        int sizeOfT = UnsafeUtility.SizeOf<T>();
        int sizeT = GetSafeSizeT(sizeOfT, length);

        var span = new ReadOnlySpan<byte>(array, sizeT);
        fileStream.Write(span);
    }

    public static void WriteRawContainerSimple<T, U>(FileStream fileStream, T rawContainer)
        where T : unmanaged, IRawSerializable<U>
        where U : unmanaged
    {
        int length = rawContainer.GetSerializationLength();
        var data = rawContainer.GetSerializedData();

        fileStream.WriteValue(length);

        WriteArraySimple(fileStream, data, length);
    }

    public static void WriteRawContainerSimple<T, U>(string path, T rawContainer)
        where T : unmanaged, IRawSerializable<U>
        where U : unmanaged
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        int length = rawContainer.GetSerializationLength();
        var data = rawContainer.GetSerializedData();

        fileStream.WriteValue(length);

        WriteArraySimple(fileStream, data, length);
    }

    public static T ReadValue<T>(FileStream fileStream) where T : unmanaged
    {
        int sizeOfT = UnsafeUtility.SizeOf<T>();
        var array = stackalloc byte[sizeOfT];

        var span = new Span<byte>(array, sizeOfT);
        int bytesRead = fileStream.Read(span);

        if (bytesRead != sizeOfT)
        {
            throw new Exception("SaveUtility :: ReadValue :: Wrong number of bytes read!");
        }

        return *(T*)array;
    }

    public static void ReadArraySimple<T>(FileStream fileStream, int length, T* array) where T : unmanaged
    {
        if (length <= 0)
            return;

        int sizeOfT = UnsafeUtility.SizeOf<T>();
        int sizeT = GetSafeSizeT(sizeOfT, length);

        var span = new Span<byte>(array, sizeT);
        int bytesRead = fileStream.Read(span);

        if (bytesRead != sizeT)
            throw new Exception("SaveUtility :: ReadArraySimple :: Wrong number of bytes read!");
    }

    public static T* ReadArraySimple<T>(FileStream fileStream, int length, Allocator allocator) where T : unmanaged
    {
        if (length <= 0)
            return null;

        int sizeOfT = UnsafeUtility.SizeOf<T>();
        int sizeT = GetSafeSizeT(sizeOfT, length);

        var array = CesMemoryUtility.Allocate<T>(length, allocator);

        var span = new Span<byte>(array, sizeT);
        int bytesRead = fileStream.Read(span);

        if (bytesRead != sizeT)
            throw new Exception($"SaveUtility :: ReadArraySimple :: Wrong number of bytes read! {bytesRead}, but should be {sizeT}");

        return array;
    }

    public static T* ReadArraySimple<T>(string path, int length, Allocator allocator) where T : unmanaged
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        return ReadArraySimple<T>(fileStream, length, allocator);
    }

    public static T* ReadArraySimpleInParts<T>(FileStream fileStream, long length, int partLength, Allocator allocator) where T : unmanaged
    {
        if (length <= 0L)
            return null;

        var array = CesMemoryUtility.Allocate<T>(length, allocator);
        long lengthCurrent = 0L;

        while (lengthCurrent < length)
        {
            int lengthToRead = (int)math.min(length - lengthCurrent, (long)partLength);

            ReadArraySimple(fileStream, lengthToRead, array + lengthCurrent);
            lengthCurrent += lengthToRead;
        }

        return array;
    }

    public static T* ReadArraySimpleInParts<T>(string path, long length, int partLength, Allocator allocator) where T : unmanaged
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

        return ReadArraySimpleInParts<T>(fileStream, length, partLength, allocator);
    }

    public static RawArray<T> ReadRawArray<T>(FileStream fileStream, BinaryReader binaryReader, Allocator allocator) where T : unmanaged
    {
        var serializationData = GetRawSerializationData<T>(fileStream, binaryReader, allocator);

        return new RawArray<T>(serializationData);
    }

    public static RawArray<T> ReadRawArray<T>(string path, Allocator allocator) where T : unmanaged
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        return ReadRawArray<T>(fileStream, binaryReader, allocator);
    }

    public static RawBag<T> ReadRawBag<T>(FileStream fileStream, BinaryReader binaryReader, Allocator allocator) where T : unmanaged
    {
        var serializationData = GetRawSerializationData<T>(fileStream, binaryReader, allocator);

        return new RawBag<T>(serializationData);
    }

    public static RawBag<T> ReadRawBag<T>(string path, Allocator allocator) where T : unmanaged
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        return ReadRawBag<T>(fileStream, binaryReader, allocator);
    }

    static RawSerializationData<T> GetRawSerializationData<T>(FileStream fileStream, BinaryReader binaryReader, Allocator allocator) where T : unmanaged
    {
        int length = binaryReader.ReadInt32();
        var array = ReadArraySimple<T>(fileStream, length, allocator);

        return new RawSerializationData<T>(array, length, allocator);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetSafeSizeT(int sizeOfT, long length)
    {
        if (sizeOfT <= 0)
            throw new Exception($"BinarySaveUtility :: GetSafeSizeT :: SizeOfT cannot be {sizeOfT}");

        if (length <= 0)
            throw new Exception($"BinarySaveUtility :: GetSafeSizeT :: Length cannot be {length}");

        long sizeT = sizeOfT * length;

        if (sizeT > int.MaxValue)
            throw new Exception("BinarySaveUtility :: GetSafeSizeT :: Bytes exceeds int.MaxValue!");

        return (int)sizeT;
    }
}