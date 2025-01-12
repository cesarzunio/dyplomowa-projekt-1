using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class RandomColorsGenerator : MonoBehaviour
{
    [SerializeField] SaveTextureToFile _save;

    public void Generate()
    {
        var textureSize = _save.TextureSize;
        var randomColors = CesColorUtilities.CreateRandomColors(textureSize.x * textureSize.y);

        _save.Save(randomColors);
    }
}

[CustomEditor(typeof(RandomColorsGenerator))]
public sealed class RandomColorsGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var rcg = (RandomColorsGenerator)target;
            rcg.Generate();
        }
    }
}