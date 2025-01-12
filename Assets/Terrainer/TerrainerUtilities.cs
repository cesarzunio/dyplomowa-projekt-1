//using Newtonsoft.Json;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Runtime.CompilerServices;
//using System.Threading.Tasks;
//using Unity.IO.LowLevel.Unsafe;
//using Unity.Mathematics;
//using Unity.VisualScripting;
//using UnityEngine;
//using UnityEngine.Networking;

//public static class TerrainerUtilities
//{
//    const string NATURAL = "natural";

//    static TerrainEnum TagToEnum(string tag) => tag switch
//    {
//        "wood" => TerrainEnum.Wood,
//        "scrub" => TerrainEnum.Scrub,
//        "wetland" => TerrainEnum.Wetland,
//        "grassland" => TerrainEnum.Grassland,
//        "sand" => TerrainEnum.Sand,
//        "heath" => TerrainEnum.Heath,
//        "scree" => TerrainEnum.Scree,
//        "beach" => TerrainEnum.Beach,
//        "shrub" => TerrainEnum.Shrub,
//        "bare_rock" => TerrainEnum.BareRock,
//        "hill" => TerrainEnum.Hill,
//        "ridge" => TerrainEnum.Ridge,
//        "glacier" => TerrainEnum.Glacier,

//        _ => throw new Exception($"TerrainerUtilities :: TagToEnum :: Cannot match string: {tag}")
//    };

//    static int TagToPriority(string tag) => tag switch
//    {
//        "wood" => 0,
//        "scrub" => 0,
//        "wetland" => 0,
//        "grassland" => 0,
//        //"tree_row" => 0,
//        "sand" => 0,
//        "heath" => 0,
//        "scree" => 0,
//        "beach" => 0,
//        "shrub" => 0,

//        "bare_rock" => 1, // kamienie
//        //"peak" => 1,
//        //"cliff" => 1,
//        //"rock" => 1,

//        "hill" => 1,
//        //"valley" => 1,

//        "ridge" => 2, // szczyty górskie
//        "glacier" => 2,

//        _ => -1,
//    };

//    static string TagRemap(string tag) => tag switch
//    {
//        "tree_row" => "wood",

//        "cliff" => "bare_rock",
//        "rock" => "bare_rock",

//        "peak" => "ridge",

//        _ => tag,
//    };

//    //static readonly Dictionary<string, int> Priorities = new()
//    //{
//    //    { "wood", 0 },
//    //    { "scrub", 5 },
//    //    { "wetland", 0 },
//    //    { "grassland", 0 },
//    //    { "tree_row", 0 },
//    //    { "sand", 0 },
//    //    { "heath", 0 },
//    //    { "scree", 0 },
//    //    { "beach", 0 },
//    //    { "shrub", 0 },

//    //    { "bare_rock", 1 },
//    //    { "peak", 1 },
//    //    { "cliff", 1 },
//    //    { "rock", 1 },
//    //    { "ridge", 1 },

//    //    { "hill", 1 }, // ??
//    //    { "valley", 1 }, // ??

//    //    { "glacier", 2 },
//    //};

//    public static TaskAwaiter GetAwaiter(this AsyncOperation asyncOp)
//    {
//        var tcs = new TaskCompletionSource<AsyncOperation>();

//        asyncOp.completed += operation => { tcs.SetResult(operation); };

//        return ((Task)tcs.Task).GetAwaiter();
//    }

//    public static async Task<string> GetNaturalFeatures(double lat, double lon, double offset)
//    {
//        string latMin = (lat - offset).ToString(CultureInfo.InvariantCulture);
//        string lonMin = (lon - offset).ToString(CultureInfo.InvariantCulture);
//        string latMax = (lat + offset).ToString(CultureInfo.InvariantCulture);
//        string lonMax = (lon + offset).ToString(CultureInfo.InvariantCulture);

//        string baseUrl = "http://overpass-api.de/api/interpreter";
//        string query = $"[out:json];(way[natural]({latMin},{lonMin},{latMax},{lonMax});relation[natural]({latMin},{lonMin},{latMax},{lonMax}););out body;";
//        string encodedQuery = UnityWebRequest.EscapeURL(query);
//        string finalUrl = $"{baseUrl}?data={encodedQuery}";

//        var request = UnityWebRequest.Get(finalUrl);
//        request.downloadHandler = new DownloadHandlerBuffer();

//        await request.SendWebRequest();

//        if (request.result == UnityWebRequest.Result.Success)
//        {
//            var deserialized = JsonConvert.DeserializeObject<TerrainRoot>(request.downloadHandler.text);

//            Debug.Log("---------------");

//            foreach (var el in deserialized.Elements)
//            {
//                foreach (var kvp in el.Tags)
//                {
//                    if (kvp.Key != NATURAL || TagToPriority(TagRemap(kvp.Value)) == -1)
//                        continue;

//                    Debug.Log("It: " + TagRemap(kvp.Value));
//                }
//            }

//            var tagsData = new Dictionary<string, TerrainCountAndPriority>();
//            var priorityTags = new List<string>();

//            if (TryGetBestMatch(deserialized, tagsData, priorityTags, out string tagBest))
//            {
//                Debug.Log("Best: " + tagBest);
//                return tagBest;
//            }
//            else
//            {
//                Debug.Log("No best?");
//                return "No best";
//            }
//        }
//        else
//        {
//            Debug.LogError($"Error: {request.error}");
//            Debug.LogError(request.downloadHandler.text);
//            return "Error";
//        }
//    }

//    public static bool TryGetBestMatch(TerrainRoot terrainRoot, Dictionary<string, TerrainCountAndPriority> tagsData, List<string> priorityTags, out string tagBest)
//    {
//        GetTagsData(terrainRoot, tagsData);

//        if (tagsData.Count == 0)
//        {
//            tagBest = string.Empty;
//            return false;
//        }

//        TryGetPriorityTags(tagsData, priorityTags);

//        if (priorityTags.Count == 1)
//        {
//            tagBest = priorityTags[0];
//            return true;
//        }

//        tagBest = GetHighestCountTag(tagsData, priorityTags);
//        return true;
//    }

//    static void GetTagsData(TerrainRoot terrainRoot, Dictionary<string, TerrainCountAndPriority> tagsData)
//    {
//        tagsData.Clear();

//        for (int i = 0; i < terrainRoot.Elements.Count; i++)
//        {
//            if (!terrainRoot.Elements[i].Tags.TryGetValue(NATURAL, out string tag))
//                continue;

//            tag = TagRemap(tag);

//            int priority = TagToPriority(tag);

//            if (priority == -1)
//                continue;

//            if (tagsData.ContainsKey(tag))
//            {
//                tagsData[tag].IncrementCount();
//            }
//            else
//            {
//                tagsData[tag] = new TerrainCountAndPriority
//                {
//                    Count = 1,
//                    Priority = priority
//                };
//            }
//        }
//    }

//    static void TryGetPriorityTags(Dictionary<string, TerrainCountAndPriority> tagsData, List<string> priorityTags)
//    {
//        priorityTags.Clear();

//        int priorityHighest = int.MinValue;

//        foreach (var kvp in tagsData)
//        {
//            if (priorityHighest < kvp.Value.Priority)
//            {
//                priorityHighest = kvp.Value.Priority;

//                priorityTags.Clear();
//                priorityTags.Add(kvp.Key);
//            }
//            else if (priorityHighest == kvp.Value.Priority)
//            {
//                priorityTags.Add(kvp.Key);
//            }
//        }
//    }

//    static string GetHighestCountTag(Dictionary<string, TerrainCountAndPriority> tagsData, List<string> priorityTags)
//    {
//        int highestCount = int.MinValue;
//        int highestCountIndex = -1;

//        for (int i = 0; i < priorityTags.Count; i++)
//        {
//            int count = tagsData[priorityTags[i]].Count;

//            if (highestCount < count)
//            {
//                highestCount = count;
//                highestCountIndex = i;
//            }
//        }

//        return priorityTags[highestCountIndex];
//    }
//}

//public struct TerrainRoot
//{
//    public List<TerrainElement> Elements;
//}

//public struct TerrainElement
//{
//    public Dictionary<string, string> Tags;
//}

//public struct TerrainCountAndPriority
//{
//    public int Count;
//    public int Priority;

//    public void IncrementCount() => Count++;
//}

//public enum TerrainEnum : byte
//{
//    Wood = 0,
//    Scrub = 1,
//    Wetland = 2,
//    Grassland = 3,
//    Sand = 4,
//    Heath = 5,
//    Scree = 6,
//    Beach = 7,
//    Shrub = 8,
//    BareRock = 9,
//    Hill = 10,
//    Ridge = 11,
//    Glacier = 12,
//}