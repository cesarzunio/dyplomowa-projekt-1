using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed class CheckIfNotTransparent : MonoBehaviour
{
    [SerializeField] Texture2D _texture;

    public void Check()
    {
        var textureSize = new int2(_texture.width, _texture.height);
        var pixels = _texture.GetRawTextureData<Color32>();

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0)
            {
                var pixelCoord = TexUtilities.FlatToPixelCoordInt2(i, textureSize.x);
                var flipped = TexUtilities.FlipY(pixelCoord, textureSize.y);

                Debug.Log($"{flipped} is transparent!");
            }
        }
    }
}

[CustomEditor(typeof(CheckIfNotTransparent))]
public sealed class CheckIfNotTransparentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Check"))
        {
            var cint = (CheckIfNotTransparent)target;
            cint.Check();
        }
    }
}
