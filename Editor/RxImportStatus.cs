using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;

public static class RxImportStatus
{
    private static string importStatusPath => Path.Combine("ProjectSettings", "RxAssetImportStatus.json");

    [Serializable]
    public class ImportEntry
    {
        public string version;
        public string importedAt;
    }

    [Serializable]
    private class ImportStatusData
    {
        public List<string> keys = new();
        public List<ImportEntry> values = new();

        public Dictionary<string, ImportEntry> ToDictionary()
        {
            var dict = new Dictionary<string, ImportEntry>();
            for (int i = 0; i < keys.Count; i++)
            {
                dict[keys[i]] = values[i];
            }
            return dict;
        }

        public static ImportStatusData FromDictionary(Dictionary<string, ImportEntry> dict)
        {
            var data = new ImportStatusData();
            foreach (var kvp in dict)
            {
                data.keys.Add(kvp.Key);
                data.values.Add(kvp.Value);
            }
            return data;
        }
    }

    private static Dictionary<string, ImportEntry> cache;

    private static void Load()
    {
        if (cache != null) return;

        if (File.Exists(importStatusPath))
        {
            string json = File.ReadAllText(importStatusPath);
            var data = JsonUtility.FromJson<ImportStatusData>(json);
            cache = data?.ToDictionary() ?? new Dictionary<string, ImportEntry>();
        }
        else
        {
            cache = new Dictionary<string, ImportEntry>();
        }
    }

    public static void SetImported(string assetName, string version)
    {
        Load();
        cache[assetName] = new ImportEntry
        {
            version = version,
            importedAt = DateTime.UtcNow.ToString("s")
        };
        Save();
    }

    public static string GetImportedVersion(string assetName)
    {
        Load();
        return cache.TryGetValue(assetName, out var entry) ? entry.version : null;
    }

    public static ImportEntry GetEntry(string assetName)
    {
        Load();
        return cache.TryGetValue(assetName, out var entry) ? entry : null;
    }

    private static void Save()
    {
        var data = ImportStatusData.FromDictionary(cache);
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(importStatusPath, json);
        AssetDatabase.Refresh();
    }
}
