using System;
using System.IO;
using Unity.Mathematics;
using UnityEngine;

public static class TextureSaver
{
    public static void Save(Color[] colors, int2 textureSize, string savePath)
    {
        var tex = new Texture2D(textureSize.x, textureSize.y, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };

        tex.SetPixels(colors);
        tex.Apply();

        SaveAndDispose(tex, savePath);
    }

    public static void Save(int2 textureSize, string savePath, Func<int, Color32> indexToColor)
    {
        var tex = new Texture2D(textureSize.x, textureSize.y, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };

        var rawTextureData = tex.GetRawTextureData<Color32>();

        for (int i = 0; i < rawTextureData.Length; i++)
        {
            rawTextureData[i] = indexToColor(i);
        }

        tex.Apply();

        SaveAndDispose(tex, savePath);
    }

    static void SaveAndDispose(Texture2D tex, string savePath)
    {
        var bytes = tex.EncodeToPNG();
        File.WriteAllBytes(savePath, bytes);

        GameObject.DestroyImmediate(tex);

        Debug.Log($"Saved texture to: {savePath}");
    }
}
