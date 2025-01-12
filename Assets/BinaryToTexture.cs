using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public sealed unsafe class BinaryToTexture : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [Header("Input")]
    [SerializeField] string _pathInput;
    [SerializeField] int2 _textureSize;
    [SerializeField] int2 _textureSizeOutput;
    [SerializeField] bool _flipY;

    [Header("Output")]
    [SerializeField] string _pathOutput;

    public void ConvertByte()
    {
        long length = (long)_textureSize.x * (long)_textureSize.y;
        int partLength = (int)(length / 10);

        var textureOriginal = BinarySaveUtility.ReadArraySimpleInParts<byte>(_pathInput, length, partLength, ALLOCATOR);

        TextureSaver.Save(_textureSizeOutput, _pathOutput, (i) =>
        {
            var pixelCoordOutput = TexUtilities.FlatToPixelCoordInt2(i, _textureSizeOutput.x);
            var uv = GeoUtilitiesDouble.PixelCoordToPlaneUv(pixelCoordOutput, _textureSizeOutput);
            var pixelCoord = GeoUtilitiesDouble.PlaneUvToPixelCoord(uv, _textureSize);
            long flat = TexUtilities.PixelCoordToFlatLong(pixelCoord, _textureSize.x);
            byte b = (byte)(textureOriginal[flat] * 1);
            return new Color32(b, b, b, 255);
        });

        UnsafeUtility.Free(textureOriginal, ALLOCATOR);
    }

    public void ConvertFloat()
    {
        int length = _textureSize.x * _textureSize.y / 2;
        var array = BinarySaveUtility.ReadArraySimple<float>(_pathInput, length, ALLOCATOR);
        var colors = new Color[length];

        float water = float.MinValue;

        for (int i = 0; i < length; i++)
        {
            water = math.max(water, array[i]);
        }

        Debug.Log((water == float.MaxValue));

        var minMax = new float2(float.MaxValue, float.MinValue);

        for (int i = 0; i < length; i++)
        {
            if (array[i] == water)
                continue;

            minMax.x = math.min(minMax.x, array[i]);
            minMax.y = math.max(minMax.y, array[i]);
        }

        Debug.Log(minMax);

        for (int y = 0; y < _textureSize.y / 2; y++)
        {
            for (int x = 0; x < _textureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                int flat = TexUtilities.PixelCoordToFlat(pixelCoord, _textureSize.x);

                //if (_flipY)
                //{
                //    pixelCoord = TexUtilities.FlipY(pixelCoord, _textureSize.y);
                //}

                int flat2 = TexUtilities.PixelCoordToFlat(pixelCoord, _textureSize.x);
                float value = math.saturate(math.unlerp(minMax.x, minMax.y, array[flat2]));

                colors[flat] = array[flat2] == water ? default : new Color(value, value, value, 1f);
            }
        }

        TextureSaver.Save(colors, new int2(_textureSize.x, _textureSize.y / 2), _pathOutput);

        UnsafeUtility.Free(array, ALLOCATOR);
    }

    public void TestPops()
    {
        var popsTextureOriginal = PopsUtility.LoadPopsTextureOriginal(_pathInput, _textureSize, ALLOCATOR);
        var minMax = new float2(float.MaxValue, float.MinValue);
        var colors = new Color[_textureSize.x * _textureSize.y];

        for (int y = 0; y < _textureSize.y; y++)
        {
            for (int x = 0; x < _textureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                float value = popsTextureOriginal[pixelCoord];

                if (value == float.MaxValue)
                    continue;

                minMax.x = math.min(minMax.x, value);
                minMax.y = math.max(minMax.y, value);
            }
        }

        Debug.Log(minMax);

        for (int y = 0; y < _textureSize.y; y++)
        {
            for (int x = 0; x < _textureSize.x; x++)
            {
                var pixelCoord = new int2(x, y);
                int flat = TexUtilities.PixelCoordToFlat(pixelCoord, _textureSize.x);

                float value = math.saturate(math.unlerp(minMax.x, minMax.y, popsTextureOriginal[pixelCoord]));

                colors[flat] = popsTextureOriginal[pixelCoord] == float.MaxValue ? default : new Color(value, value, value, 1f);
            }
        }

        TextureSaver.Save(colors, _textureSize, _pathOutput);

        popsTextureOriginal.Dispose();
    }
}

[CustomEditor(typeof(BinaryToTexture))]
public sealed class BinaryToTextureEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Convert byte"))
        {
            var btt = (BinaryToTexture)target;
            btt.ConvertByte();
        }

        if (GUILayout.Button("Convert Float"))
        {
            var btt = (BinaryToTexture)target;
            btt.ConvertFloat();
        }

        if (GUILayout.Button("Test pops"))
        {
            var btt = (BinaryToTexture)target;
            btt.TestPops();
        }
    }
}
