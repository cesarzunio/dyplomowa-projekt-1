using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.IO.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;

public static class TerrainerUtilities2
{
    const string NATURAL = "natural";

    static string TagRemap(string tag) => tag switch
    {
        "tree_row" => "wood",

        "cliff" => "bare_rock",
        "rock" => "bare_rock",

        "peak" => "ridge",

        _ => tag,
    };

    static TerrainEnum TagToEnum(string tag) => tag switch
    {
        "wood" => TerrainEnum.Wood,
        "scrub" => TerrainEnum.Scrub,
        "wetland" => TerrainEnum.Wetland,
        "grassland" => TerrainEnum.Grassland,
        "sand" => TerrainEnum.Sand,
        "heath" => TerrainEnum.Heath,
        "scree" => TerrainEnum.Scree,
        "beach" => TerrainEnum.Beach,
        "shrub" => TerrainEnum.Shrub,
        "bare_rock" => TerrainEnum.BareRock,
        "hill" => TerrainEnum.Hill,
        "ridge" => TerrainEnum.Ridge,
        "glacier" => TerrainEnum.Glacier,

        _ => TerrainEnum.None,
    };

    static int TagToPriority(TerrainEnum tag) => tag switch
    {
        TerrainEnum.Wood => 0,
        TerrainEnum.Scrub => 0,
        TerrainEnum.Wetland => 0,
        TerrainEnum.Grassland => 0,
        TerrainEnum.Sand => 0,
        TerrainEnum.Heath => 0,
        TerrainEnum.Scree => 0,
        TerrainEnum.Beach => 0,
        TerrainEnum.Shrub => 0,
        TerrainEnum.BareRock => 1,
        TerrainEnum.Hill => 1,
        TerrainEnum.Ridge => 2,
        TerrainEnum.Glacier => 2,

        _ => throw new Exception($"Cannot match tag: {tag}")
    };

    public static async Task<TerrainEnum> GetNaturalFeatures(double lat, double lon, double offset, Dictionary<TerrainEnum, TerrainCountAndPriority> tagsData, List<TerrainEnum> priorityTags)
    {
        string latMin = (lat - offset).ToString(CultureInfo.InvariantCulture);
        string lonMin = (lon - offset).ToString(CultureInfo.InvariantCulture);
        string latMax = (lat + offset).ToString(CultureInfo.InvariantCulture);
        string lonMax = (lon + offset).ToString(CultureInfo.InvariantCulture);

        string baseUrl = "http://overpass-api.de/api/interpreter";
        string query = $"[out:json];(way[natural]({latMin},{lonMin},{latMax},{lonMax});relation[natural]({latMin},{lonMin},{latMax},{lonMax}););out body;";
        string encodedQuery = UnityWebRequest.EscapeURL(query);
        string finalUrl = $"{baseUrl}?data={encodedQuery}";

        var request = UnityWebRequest.Get(finalUrl);
        request.downloadHandler = new DownloadHandlerBuffer();

        await request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Error: {request.error}");
            return TerrainEnum.Error;
        }

        var deserialized = JsonConvert.DeserializeObject<TerrainRoot>(request.downloadHandler.text);

        if (TryGetBestMatch(deserialized, tagsData, priorityTags, out var tagBest))
            return tagBest;

        Debug.LogError("Not found");
        return TerrainEnum.None;
    }

    static TaskAwaiter GetAwaiter(this AsyncOperation asyncOp)
    {
        var tcs = new TaskCompletionSource<AsyncOperation>();

        asyncOp.completed += operation => { tcs.SetResult(operation); };

        return ((Task)tcs.Task).GetAwaiter();
    }

    public static bool TryGetBestMatch(TerrainRoot terrainRoot, Dictionary<TerrainEnum, TerrainCountAndPriority> tagsData, List<TerrainEnum> priorityTags, out TerrainEnum tagBest)
    {
        tagsData.Clear();
        priorityTags.Clear();

        GetTagsData(terrainRoot, tagsData);

        if (tagsData.Count == 0)
        {
            tagBest = TerrainEnum.None;
            return false;
        }

        TryGetPriorityTags(tagsData, priorityTags);

        if (priorityTags.Count == 1)
        {
            tagBest = priorityTags[0];
            return true;
        }

        tagBest = GetHighestCountTag(tagsData, priorityTags);
        return true;
    }

    static void GetTagsData(TerrainRoot terrainRoot, Dictionary<TerrainEnum, TerrainCountAndPriority> tagsData)
    {
        tagsData.Clear();

        for (int i = 0; i < terrainRoot.Elements.Count; i++)
        {
            if (!terrainRoot.Elements[i].Tags.TryGetValue(NATURAL, out string tagStr))
                continue;

            tagStr = TagRemap(tagStr);
            var tag = TagToEnum(tagStr);

            if (tag == TerrainEnum.None)
                continue;

            int priority = TagToPriority(tag);

            if (tagsData.ContainsKey(tag))
            {
                tagsData[tag].IncrementCount();
            }
            else
            {
                tagsData[tag] = new TerrainCountAndPriority
                {
                    Count = 1,
                    Priority = priority
                };
            }
        }
    }

    static void TryGetPriorityTags(Dictionary<TerrainEnum, TerrainCountAndPriority> tagsData, List<TerrainEnum> priorityTags)
    {
        priorityTags.Clear();

        int priorityHighest = int.MinValue;

        foreach (var kvp in tagsData)
        {
            if (priorityHighest < kvp.Value.Priority)
            {
                priorityHighest = kvp.Value.Priority;

                priorityTags.Clear();
                priorityTags.Add(kvp.Key);
            }
            else if (priorityHighest == kvp.Value.Priority)
            {
                priorityTags.Add(kvp.Key);
            }
        }
    }

    static TerrainEnum GetHighestCountTag(Dictionary<TerrainEnum, TerrainCountAndPriority> tagsData, List<TerrainEnum> priorityTags)
    {
        int highestCount = int.MinValue;
        int highestCountIndex = -1;

        for (int i = 0; i < priorityTags.Count; i++)
        {
            int count = tagsData[priorityTags[i]].Count;

            if (highestCount < count)
            {
                highestCount = count;
                highestCountIndex = i;
            }
        }

        return priorityTags[highestCountIndex];
    }
}

public struct TerrainRoot
{
    public List<TerrainElement> Elements;
}

public struct TerrainElement
{
    public Dictionary<string, string> Tags;
}

public struct TerrainCountAndPriority
{
    public int Count;
    public int Priority;

    public void IncrementCount() => Count++;
}

public enum TerrainEnum : byte
{
    Wood = 0,
    Scrub = 1,
    Wetland = 2,
    Grassland = 3,
    Sand = 4,
    Heath = 5,
    Scree = 6,
    Beach = 7,
    Shrub = 8,
    BareRock = 9,
    Hill = 10,
    Ridge = 11,
    Glacier = 12,

    Error = 200,
    None = 201,
}