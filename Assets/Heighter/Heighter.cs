using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public sealed unsafe class Heighter : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [SerializeField] Texture2D _landTexture;
    [SerializeField] Texture2D _heightTextureLand;
    [SerializeField] Texture2D _heightTextureWater;
    [SerializeField] double _scale;
    [SerializeField] string _savePathFields;
    [SerializeField] string _savePathFieldsMap;

    [Header("Output normals")]
    [SerializeField] string _savePathNormals;

    [Header("Output elevations")]
    [SerializeField] bool _createTexture;
    [SerializeField] string _savePathElevations;
    [SerializeField] string _savePathElevationsMap;

    public void GenerateNormalMap()
    {
        var landTexture = _landTexture.GetRawTextureData<byte>();
        var heightTextureLand = _heightTextureLand.GetRawTextureData<byte>();
        var heightTextureWater = _heightTextureWater.GetRawTextureData<byte>();
        var textureSize = new int2(_landTexture.width, _landTexture.height);

        var normals = HeighterNormalsUtility.CreateNormals(landTexture, heightTextureLand, heightTextureWater, textureSize, _scale);
        
        TextureSaver.Save(normals, textureSize, _savePathNormals);
    }

    public void GenerateFieldsElevations()
    {
        var landTexture = _landTexture.GetRawTextureData<byte>();
        var heightTextureLand = _heightTextureLand.GetRawTextureData<byte>();
        var heightTextureWater = _heightTextureWater.GetRawTextureData<byte>();

        var fields = FinalizerSaves.LoadFields(_savePathFields, ALLOCATOR);
        var fieldsMap = FinalizerSaves.LoadFieldsMap(_savePathFieldsMap, ALLOCATOR);

        var fieldToLatitude = HeighterHeightUtility.CreateFieldsLatitudes(landTexture, heightTextureLand, heightTextureWater, fields, fieldsMap);
        HeighterHeightUtility.SaveFieldToLatitude(_savePathElevations, fieldToLatitude);

        if (_createTexture)
        {
            TextureSaver.Save(fieldsMap.TextureSize, _savePathElevationsMap, (i) =>
            {
                uint field = fieldsMap.Fields[i];
                double latitude = fieldToLatitude[field].Height;
                byte b = (byte)math.min(255, 255 * math.unlerp(ConstData.EARTH_LOWEST, ConstData.EARTH_HIGHEST, latitude));
                return new Color32(b, b, b, 255);
            });
        }

        fields.Dispose();
        fieldsMap.Dispose();
    }
}

[CustomEditor(typeof(Heighter))]
public sealed class HeighterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate normal map"))
        {
            var heighter = (Heighter)target;
            heighter.GenerateNormalMap();
        }

        if (GUILayout.Button("Generate elevations"))
        {
            var heighter = (Heighter)target;
            heighter.GenerateFieldsElevations();
        }
    }
}
