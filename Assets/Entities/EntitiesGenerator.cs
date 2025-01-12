using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class EntitiesGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] Texture2D _entitiesTexture;
    [SerializeField] string _savePathFields;

    [Header("Output")]
    [SerializeField] string _savePathEntities;

    public void Generate()
    {
        var textureSize = new int2(_entitiesTexture.width, _entitiesTexture.height);
        var entitiesMap = _entitiesTexture.GetRawTextureData<Color32>().ColorsToIntsCheckAlpha(ALLOCATOR);
        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);

        EntitiesGeneratorUtility.SaveEntities(_savePathEntities, entitiesMap, textureSize, fields);
    }
}

[CustomEditor(typeof(EntitiesGenerator))]
public sealed class EntitiesGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate"))
        {
            var eg = (EntitiesGenerator)target;
            eg.Generate();
        }
    }
}
