using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class FieldsToIndexes : MonoBehaviour
{
    [SerializeField] Texture2D _fieldsTex;
    [SerializeField] int _max;

    public void Convert()
    {
        //var sw = Stopwatch.StartNew();

        //var textureSize = new int2(_fieldsTex.width, _fieldsTex.height);
        //var fieldsMap = _fieldsTex.GetRawTextureData<Color32>();
        //int fieldsCount = 0;
        //var fieldToIndex = new Dictionary<int, int>(2048 * 1024);
        //var indexesMap = new int[fieldsMap.Length];
        //var indexesMapColors = new Color32[fieldsMap.Length];

        //Debug.Log("Prepare: " + sw.Elapsed.TotalSeconds);
        //sw.Restart();

        //for (int i = 0; i < fieldsMap.Length; i++)
        //{
        //    int fieldIndexA = CesColorUtilities.Color32ToIndex(fieldsMap[i]);

        //    if (fieldToIndex.TryGetValue(fieldIndexA, out int fieldIndexB))
        //    {
        //        indexesMap[i] = fieldIndexB;
        //        continue;
        //    }

        //    fieldToIndex[fieldIndexA] = fieldsCount;
        //    indexesMap[i] = fieldsCount;
        //    fieldsCount++;
        //}

        //for (int i = 0; i < indexesMap.Length; i++)
        //{
        //    indexesMapColors[i] = CesColorUtilities.IndexToColor32(indexesMap[i]);
        //}

        //Debug.Log("Main: " + sw.Elapsed.TotalMilliseconds);
        //sw.Restart();

        //SaveTextureToFile.SaveFrom(this.gameObject, indexesMapColors);

        //Debug.Log("Final: " + sw.Elapsed.TotalMilliseconds);
    }
}

[CustomEditor(typeof(FieldsToIndexes))]
public sealed class FieldsToIndexesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Convert"))
        {
            var fti = (FieldsToIndexes)target;
            fti.Convert();
        }
    }
}
