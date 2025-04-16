using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public static class RxAssetLibraryTabs
{
    private static string searchQuery = "";
    private static Vector2 scrollPos;
    private static Dictionary<string, List<RxAssetMetadata>> groupedAssets = new Dictionary<string, List<RxAssetMetadata>>();
    private static HashSet<string> brokenAssetFolders = new HashSet<string>();

    private static string libraryListKey = "RxAssetLibraryEntries";
    private static string activeLibraryIndexKey = "RxAssetLibraryActiveIndex";
    private static List<RxLibraryEntry> _libraries;
    private static List<RxLibraryEntry> libraries => _libraries ??= LoadLibraries();

    public static string libraryPath => GetActiveLibrary()?.path;

    private static UnityEngine.Object newAsset;
    private static string newCategory = "";
    private static string newTags = "";
    private static bool useCustomCategory = false;
    private static string customCategory = "";

    private enum Tab { Browse, NewAsset, Settings }
    private static Tab currentTab = Tab.Browse;

    [MenuItem("Tools/RealX/Package Library")]
    public static void ShowWindow()
    {
        var window = EditorWindow.GetWindow<AssetLibraryInternalWindow>("Package Library");
        window.minSize = new Vector2(600, 400);
        RefreshAssetList();
    }

    private class AssetLibraryInternalWindow : EditorWindow
    {
        private void OnGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(currentTab == Tab.Browse, "Browse", EditorStyles.toolbarButton)) currentTab = Tab.Browse;
            if (GUILayout.Toggle(currentTab == Tab.NewAsset, "New Asset", EditorStyles.toolbarButton)) currentTab = Tab.NewAsset;
            if (GUILayout.Toggle(currentTab == Tab.Settings, "Settings", EditorStyles.toolbarButton)) currentTab = Tab.Settings;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            switch (currentTab)
            {
                case Tab.Browse:
                    DrawLibraryTab();
                    break;
                case Tab.NewAsset:
                    DrawNewAssetTab();
                    break;
                case Tab.Settings:
                    DrawSettingsTab();
                    break;
            }
        }
    }

    public static RxLibraryEntry GetActiveLibrary()
    {
        int index = EditorPrefs.GetInt(activeLibraryIndexKey, 0);
        if (index >= 0 && index < libraries.Count)
            return libraries[index];
        return libraries.FirstOrDefault();
    }

    public static void DrawLibraryTab()
    {
        GUILayout.Label("Browse Asset Library", EditorStyles.boldLabel);

        int activeIndex = EditorPrefs.GetInt(activeLibraryIndexKey, 0);
        var names = libraries.Select(l => l.name).ToArray();
        int newIndex = EditorGUILayout.Popup("Library", activeIndex, names);
        if (newIndex != activeIndex)
        {
            EditorPrefs.SetInt(activeLibraryIndexKey, newIndex);
            RefreshAssetList();
        }

        searchQuery = EditorGUILayout.TextField("Search", searchQuery);

        if (GUILayout.Button("Refresh", GUILayout.Width(100)))
        {
            RefreshAssetList();
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        var filteredAssets = groupedAssets
            .SelectMany(g => g.Value)
            .Where(m => string.IsNullOrEmpty(searchQuery) ||
                        m.name.ToLower().Contains(searchQuery.ToLower()) ||
                        m.tags.Any(t => t.ToLower().Contains(searchQuery.ToLower())))
            .ToList();

        var assetsByCategory = filteredAssets
            .GroupBy(m => m.category)
            .OrderBy(g => g.Key);

        foreach (var categoryGroup in assetsByCategory)
        {
            EditorGUILayout.LabelField($"Category: {categoryGroup.Key}", EditorStyles.boldLabel);

            foreach (var group in categoryGroup.GroupBy(m => m.name))
            {
                var versions = group.OrderByDescending(v => v.version).ToList();
                var latest = versions.First();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                Texture2D preview = null;

                if (!string.IsNullOrEmpty(latest.originalAsset))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(latest.originalAsset);
                    if (obj != null)
                    {
                        preview = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
                        if (AssetPreview.IsLoadingAssetPreview(obj.GetInstanceID()))
                        {
                            EditorApplication.delayCall += () => EditorWindow.GetWindow<AssetLibraryInternalWindow>()?.Repaint();
                        }
                    }
                }

                if (preview != null)
                {
                    GUILayout.Label(preview, GUILayout.Width(64), GUILayout.Height(64));
                }
                else
                {
                    GUILayout.Label("No Preview", GUILayout.Width(64), GUILayout.Height(64));
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Tags: " + string.Join(", ", latest.tags), EditorStyles.miniLabel);

                foreach (var version in versions)
                {
                    if (string.IsNullOrEmpty(version.version)) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("v" + version.version, GUILayout.Width(80));

                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Import").image, "Import Package"), GUILayout.Width(24), GUILayout.Height(24)))
                    {
                        string basePath = Path.Combine(libraryPath, version.name);
                        string fileBase = $"{version.name}_v{version.version}";
                        string packagePath = Path.Combine(basePath, fileBase + ".unitypackage");
                        AssetDatabase.ImportPackage(packagePath, true);
                    }

                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash").image, "Delete Version"), GUILayout.Width(24), GUILayout.Height(24)))
                    {
                        string folder = Path.Combine(libraryPath, version.name);
                        string baseFile = $"{version.name}_v{version.version}";
                        string packagePath = Path.Combine(folder, baseFile + ".unitypackage");
                        string jsonPath = Path.Combine(folder, baseFile + ".json");
                        string thumbnailPath = Path.Combine(folder, baseFile + "_preview.png");

                        if (File.Exists(packagePath)) File.Delete(packagePath);
                        if (File.Exists(jsonPath)) File.Delete(jsonPath);
                        if (File.Exists(thumbnailPath)) File.Delete(thumbnailPath);

                        AssetDatabase.Refresh();
                        EditorApplication.delayCall += RefreshAssetList;
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical(GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Toolbar Plus").image, "Upload New Version"), GUILayout.Width(24), GUILayout.Height(24)))
                {
                    RxAssetLibraryExportHelper.ShowUploadWindowWithDefaults(latest, libraryPath);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    public static void DrawNewAssetTab()
    {
        GUILayout.Label("Create New Asset", EditorStyles.boldLabel);

        int activeIndex = EditorPrefs.GetInt(activeLibraryIndexKey, 0);
        var names = libraries.Select(l => l.name).ToArray();
        int newIndex = EditorGUILayout.Popup("Library", activeIndex, names);
        if (newIndex != activeIndex)
        {
            EditorPrefs.SetInt(activeLibraryIndexKey, newIndex);
            RefreshAssetList();
        }

        newAsset = EditorGUILayout.ObjectField("Select Asset", newAsset, typeof(UnityEngine.Object), false);
        if (newAsset == null) return;

        string defaultName = newAsset.name;
        string assetName = EditorGUILayout.TextField("Asset Name", defaultName);

        var existingCategories = groupedAssets.Select(g => g.Value.First().category).Distinct().OrderBy(c => c).ToList();

        useCustomCategory = EditorGUILayout.Toggle("Custom Category", useCustomCategory);

        if (useCustomCategory)
        {
            customCategory = EditorGUILayout.TextField("Category", customCategory);
        }
        else
        {
            int selectedIndex = Mathf.Max(0, existingCategories.IndexOf(newCategory));
            selectedIndex = EditorGUILayout.Popup("Category", selectedIndex, existingCategories.ToArray());
            newCategory = existingCategories.Count > 0 ? existingCategories[selectedIndex] : "Uncategorized";
        }

        newTags = EditorGUILayout.TextField("Tags", newTags);

        if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_CloudConnect").image, "Export as v1.0.0"), GUILayout.Width(160)))
        {
            string categoryToUse = useCustomCategory ? customCategory : newCategory;
            RxAssetLibraryExportHelper.ExportAsset(newAsset, assetName, categoryToUse, newTags, "1.0.0", false, libraryPath);
            EditorApplication.delayCall += RefreshAssetList;
        }
    }

    public static void DrawSettingsTab()
    {
        GUILayout.Label("Library Settings", EditorStyles.boldLabel);

        GUILayout.Space(10);
        GUILayout.Label("Manage Libraries", EditorStyles.boldLabel);

        for (int i = 0; i < libraries.Count; i++)
        {
            var entry = libraries[i];
            EditorGUILayout.BeginHorizontal();
            entry.name = EditorGUILayout.TextField(entry.name, GUILayout.Width(150));
            EditorGUILayout.SelectableLabel(entry.path, EditorStyles.textField, GUILayout.Height(18));

            if (GUILayout.Button("🗑", GUILayout.Width(25)))
            {
                libraries.RemoveAt(i);
                SaveLibraries(libraries);
                if (EditorPrefs.GetInt(activeLibraryIndexKey, 0) >= libraries.Count)
                    EditorPrefs.SetInt(activeLibraryIndexKey, Mathf.Max(0, libraries.Count - 1));
                RefreshAssetList();
                return;
            }

            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("➕ Add New Library"))
        {
            string newPath = EditorUtility.OpenFolderPanel("Select Library Folder", "", "");
            if (!string.IsNullOrEmpty(newPath))
            {
                string name = Path.GetFileName(newPath);
                libraries.Add(new RxLibraryEntry { name = name, path = newPath });
                SaveLibraries(libraries);
            }
        }
    }

    public static void RefreshAssetList()
    {
        groupedAssets = LoadGroupedMetadata();
        brokenAssetFolders = DetectBrokenAssetFolders();
    }

    private static Dictionary<string, List<RxAssetMetadata>> LoadGroupedMetadata()
    {
        var result = new Dictionary<string, List<RxAssetMetadata>>();
        if (!Directory.Exists(libraryPath)) return result;

        var jsonFiles = Directory.GetFiles(libraryPath, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            string json = File.ReadAllText(file);
            var metadata = JsonUtility.FromJson<RxAssetMetadata>(json);
            if (metadata == null || string.IsNullOrEmpty(metadata.name)) continue;

            if (!result.ContainsKey(metadata.name))
                result[metadata.name] = new List<RxAssetMetadata>();

            result[metadata.name].Add(metadata);
        }

        return result;
    }

    private static HashSet<string> DetectBrokenAssetFolders()
    {
        var broken = new HashSet<string>();
        if (!Directory.Exists(libraryPath)) return broken;

        var folders = Directory.GetDirectories(libraryPath);
        foreach (var folder in folders)
        {
            var jsons = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            if (jsons.Length == 0)
            {
                string name = Path.GetFileName(folder);
                broken.Add(name);
            }
        }
        return broken;
    }

    [Serializable]
    public class RxAssetMetadata
    {
        public string name;
        public string category;
        public string version;
        public List<string> tags;
        public string originalAsset;
        public string exportedAt;
        public string thumbnailFile;
    }

    [Serializable]
    public class RxLibraryEntry
    {
        public string name;
        public string path;
    }

    private class Wrapper
    {
        public List<RxLibraryEntry> entries;
    }

    private static List<RxLibraryEntry> LoadLibraries()
    {
        if (!EditorPrefs.HasKey(libraryListKey))
        {
            var defaultLib = new RxLibraryEntry
            {
                name = "Default",
                path = "/Users/fabianfreund/Library/CloudStorage/OneDrive-RealX/02 - Documents - Development/01 - Projects/barXR/PackageLibrary 2"
            };
            var list = new List<RxLibraryEntry> { defaultLib };
            SaveLibraries(list);
            return list;
        }

        string json = EditorPrefs.GetString(libraryListKey);
        return JsonUtility.FromJson<Wrapper>(json)?.entries ?? new List<RxLibraryEntry>();
    }

    private static void SaveLibraries(List<RxLibraryEntry> entries)
    {
        var wrapper = new Wrapper { entries = entries };
        string json = JsonUtility.ToJson(wrapper);
        EditorPrefs.SetString(libraryListKey, json);
        _libraries = entries;
    }
}