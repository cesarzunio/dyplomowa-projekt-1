using System;
using System.IO;
using Unity.Mathematics;
using UnityEngine;

public sealed class SaveTextureToFile : MonoBehaviour
{
    [SerializeField] Texture2D _tex;
    [SerializeField] string _filePath;

    public int2 TextureSize => new int2(_tex.width, _tex.height);

    public void Save(Color32[] pixels)
    {
        var nativeArray = _tex.GetRawTextureData<Color32>();

        if (pixels.Length != nativeArray.Length)
            throw new Exception("SaveTextureToFile :: Save :: Textures sizes don't match!");

        for (int i = 0; i < pixels.Length; i++)
        {
            nativeArray[i] = pixels[i];
        }

        _tex.Apply();

        SaveToFile();
    }

    public void Save(RawArray<Color32> pixels)
    {
        var nativeArray = _tex.GetRawTextureData<Color32>();

        if (pixels.Length != nativeArray.Length)
            throw new Exception("SaveTextureToFile :: Save :: Textures sizes don't match!");

        for (int i = 0; i < pixels.Length; i++)
        {
            nativeArray[i] = pixels[i];
        }

        _tex.Apply();

        SaveToFile();
    }

    public void Save<T>(RawArray<T> pixels, Func<T, Color32> conversion) where T : unmanaged
    {
        var nativeArray = _tex.GetRawTextureData<Color32>();

        if (pixels.Length != nativeArray.Length)
            throw new Exception("SaveTextureToFile :: Save :: Textures sizes don't match!");

        for (int i = 0; i < pixels.Length; i++)
        {
            nativeArray[i] = conversion(pixels[i]);
        }

        _tex.Apply();

        SaveToFile();
    }

    public void Save(Func<int, Color32> conversion)
    {
        var nativeArray = _tex.GetRawTextureData<Color32>();

        for (int i = 0; i < nativeArray.Length; i++)
        {
            nativeArray[i] = conversion(i);
        }

        _tex.Apply();

        SaveToFile();
    }

    void SaveToFile()
    {
        var bytes = _tex.EncodeToPNG();
        File.WriteAllBytes(_filePath, bytes);

        Debug.Log("SaveTextureToFile :: Save :: Saved to " + _filePath);
    }
}

public static class SaveTextureToFileUtilities
{
    public static void SaveToTexture(this MonoBehaviour m, Color32[] pixels)
    {
        if (!m.TryGetComponent<SaveTextureToFile>(out var sttf))
            throw new Exception("SaveTextureToFileUtilities :: SaveToTexture :: No component on this GameObject!");

        sttf.Save(pixels);
    }

    public static void SaveToTexture(this MonoBehaviour m, RawArray<Color32> pixels)
    {
        if (!m.TryGetComponent<SaveTextureToFile>(out var sttf))
            throw new Exception("SaveTextureToFileUtilities :: SaveToTexture :: No component on this GameObject!");

        sttf.Save(pixels);
    }

    public static void SaveToTexture<T>(this MonoBehaviour m, RawArray<T> pixels, Func<T, Color32> conversion) where T : unmanaged
    {
        if (!m.TryGetComponent<SaveTextureToFile>(out var sttf))
            throw new Exception("SaveTextureToFileUtilities :: SaveToTexture :: No component on this GameObject!");

        sttf.Save(pixels, conversion);
    }

    public static void SaveToTexture(this MonoBehaviour m, Func<int, Color32> conversion)
    {
        if (!m.TryGetComponent<SaveTextureToFile>(out var sttf))
            throw new Exception("SaveTextureToFileUtilities :: SaveToTexture :: No component on this GameObject!");

        sttf.Save(conversion);
    }
}