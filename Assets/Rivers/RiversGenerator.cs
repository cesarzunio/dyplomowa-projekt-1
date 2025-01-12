using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed class RiversGenerator : MonoBehaviour
{
    const Allocator ALLOCATOR = Allocator.Persistent;

    [Header("Generator")]
    [SerializeField] Texture2D _riversBeforeTexture;
    [SerializeField] Texture2D _regionsTexture;

    [Header("Extend")]
    [SerializeField] Texture2D _riversAfterTexture;
    [SerializeField] Texture2D _waterHeightTexture;
    [SerializeField] Texture2D _distancesTexture;

    [Header("Save")]
    [SerializeField] SaveTextureToFile _saveMain;
    [SerializeField] SaveTextureToFile _saveExtend;

    public void Perform()
    {
        var textureSize = new int2(_riversBeforeTexture.width, _riversBeforeTexture.height);

        var riversMap = _riversBeforeTexture.GetRawTextureData<Color32>().ColorsToIntsCheckAlpha(ALLOCATOR);
        var regionsMap = _regionsTexture.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);
        var multipliers = RiverUtilities.GenerateNeighborMultipliers(regionsMap, textureSize, ALLOCATOR);
        var orders = new RawArray<int>(ALLOCATOR, 0, riversMap.Length);
        var closed = new RawArray<bool>(ALLOCATOR, false, riversMap.Length);
        var output = new RawArray<Color32>(ALLOCATOR, default, riversMap.Length);
        RiverUtilities.FindMouths(riversMap, textureSize, ALLOCATOR, out var mouthsPrimary, out var mouthsSecondary);

        var jobPrimary = new RiversGeneratorJobPrimary2
        {
            TextureSize = textureSize,
            RiverMap = riversMap,
            RegionMap = regionsMap,
            Multipliers = multipliers,
            MouthsPrimary = mouthsPrimary,
            Closed = closed,
            Orders = orders,
            Output = output,
        };

        jobPrimary.Schedule().Complete();
        //jobPrimary.Execute();

        var jobSecondary = new RiversGeneratorJobSecondary
        {
            TextureSize = textureSize,
            RiverMap = riversMap,
            RegionMap = regionsMap,
            Multipliers = multipliers,
            MouthsSecondary = mouthsSecondary,
            Orders = orders,
            Output = output,
        };

        jobSecondary.Schedule().Complete();
        //jobSecondary.Execute();

        this.SaveToTexture(output);

        riversMap.Dispose();
        regionsMap.Dispose();
        multipliers.Dispose();
        orders.Dispose();
        closed.Dispose();
        output.Dispose();
        mouthsPrimary.Dispose();
        mouthsSecondary.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetRiverParams(
        int2 previous, int2 current, RawArray<int> regionMap, int2 textureSize, int waterColor,
        out int previousFlat, out bool previousIsWater, out int currentFlat, out bool currentIsWater)
    {
        previousFlat = TexUtilities.PixelCoordToFlat(previous, textureSize.x);
        var previousColor = regionMap[previousFlat];
        previousIsWater = previousColor == waterColor;

        currentFlat = TexUtilities.PixelCoordToFlat(current, textureSize.x);
        var currentColor = regionMap[currentFlat];
        currentIsWater = currentColor == waterColor;
    }

    public void Extend()
    {
        var textureSize = new int2(_riversAfterTexture.width, _riversAfterTexture.height);

        var riversMap = _riversAfterTexture.GetRawTextureData<Color32>().ColorsToIntsCheckAlpha(ALLOCATOR);
        var regionsMap = _regionsTexture.GetRawTextureData<Color32>().ColorsToInts(ALLOCATOR);
        var heightMap = _waterHeightTexture.GetRawTextureData<byte>().ByteToFloat(ALLOCATOR);
        var distances = _distancesTexture.GetRawTextureData<byte>().ByteToFloat(ALLOCATOR);
        RiverUtilities.FindMouths(riversMap, textureSize, ALLOCATOR, out var mouthsPrimary, out var mouthsSecondary);

        var job = new RiversExtenderJob
        {
            TextureSize = textureSize,
            RiversMap = riversMap,
            RegionsMap = regionsMap,
            HeightMap = heightMap,
            Distances = distances,
            MouthsPrimary = mouthsPrimary,
            MouthsSecondary = mouthsSecondary
        };

        //job.Schedule().Complete();
        job.Execute();

        _saveExtend.Save(riversMap.IntsToColorsCheckAlpha());

        riversMap.Dispose();
        regionsMap.Dispose();
        mouthsPrimary.Dispose();
        mouthsSecondary.Dispose();
        heightMap.Dispose();
        distances.Dispose();
    }
}

[CustomEditor(typeof(RiversGenerator))]
public sealed class RiversManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Perform"))
        {
            var rm = (RiversGenerator)target;
            rm.Perform();
        }

        //if (GUILayout.Button("Extend"))
        //{
        //    var rm = (RiversGenerator)target;
        //    rm.Extend();
        //}
    }
}