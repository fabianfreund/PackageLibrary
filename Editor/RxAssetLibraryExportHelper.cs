using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class RxAssetLibraryExportHelper : EditorWindow
{
    private UnityEngine.Object assetToExport;
    private string assetName = "";
    private string category = "";
    private string tags = "";
    private string version = "1.0.0";
    private bool includePackages = false;
    private string customExportPath = "";

    public static void ShowUploadWindowWithDefaults(RxAssetLibraryTabs.RxAssetMetadata existing, string targetLibraryPath)
    {
        var window = CreateInstance<RxAssetLibraryExportHelper>();
        window.assetName = existing.name;
        window.category = existing.category;
        window.tags = string.Join(", ", existing.tags);
        window.version = NextVersion(existing.version);
        window.includePackages = false;
        window.customExportPath = targetLibraryPath;

        if (!string.IsNullOrEmpty(existing.originalAsset))
        {
            window.assetToExport = AssetDatabase.LoadMainAssetAtPath(existing.originalAsset);
        }

        window.titleContent = new GUIContent("Update Asset Version");
        window.ShowUtility();
    }

    private void OnGUI()
    {
        GUILayout.Label("Export Asset", EditorStyles.boldLabel);

        assetToExport = EditorGUILayout.ObjectField("Asset", assetToExport, typeof(UnityEngine.Object), false);
        assetName = EditorGUILayout.TextField("Name", assetName);
        category = EditorGUILayout.TextField("Category", category);
        tags = EditorGUILayout.TextField("Tags (comma-separated)", tags);
        version = EditorGUILayout.TextField("Version", version);
        includePackages = EditorGUILayout.Toggle("Include Package Dependencies", includePackages);

        EditorGUILayout.LabelField("Export Path:", customExportPath, EditorStyles.miniLabel);

        if (GUILayout.Button("Export"))
        {
            ExportAsset(assetToExport, assetName, category, tags, version, includePackages, customExportPath);
            Close();
        }
    }

    public static void ExportAsset(UnityEngine.Object asset, string name, string category, string tagsCsv, string version, bool includePackages = false, string exportRootPath = null)
    {
        if (asset == null || string.IsNullOrEmpty(name)) return;

        string[] mainDeps = AssetDatabase.GetDependencies(new[] { AssetDatabase.GetAssetPath(asset) }, true);
        if (!includePackages)
            mainDeps = mainDeps.Where(path => !path.StartsWith("Packages/")).ToArray();

        string targetPath = exportRootPath ?? RxAssetLibraryTabs.libraryPath;
        string assetDir = Path.Combine(targetPath, name);
        if (!Directory.Exists(assetDir)) Directory.CreateDirectory(assetDir);

        string fileBase = $"{name}_v{version}";
        string packagePath = Path.Combine(assetDir, fileBase + ".unitypackage");
        string jsonPath = Path.Combine(assetDir, fileBase + ".json");
        string thumbnailPath = Path.Combine(assetDir, fileBase + "_thumbnail.png");

        AssetDatabase.ExportPackage(mainDeps, packagePath, ExportPackageOptions.Interactive);

        string assetPath = AssetDatabase.GetAssetPath(asset);
        string thumbnailFile = Path.GetFileName(thumbnailPath);

        var preview = AssetPreview.GetAssetPreview(asset);
        if (preview != null)
        {
            byte[] png = preview.EncodeToPNG();
            File.WriteAllBytes(thumbnailPath, png);
        }

        var metadata = new RxAssetLibraryTabs.RxAssetMetadata
        {
            name = name,
            category = category,
            version = version,
            tags = tagsCsv.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList(),
            originalAsset = assetPath,
            exportedAt = DateTime.Now.ToString("s"),
            thumbnailFile = File.Exists(thumbnailPath) ? thumbnailFile : null
        };

        File.WriteAllText(jsonPath, JsonUtility.ToJson(metadata, true));
        AssetDatabase.Refresh();
    }

    private static string NextVersion(string current)
    {
        var parts = current.Split('.');
        if (parts.Length != 3) return current;

        if (int.TryParse(parts[2], out int patch))
        {
            return $"{parts[0]}.{parts[1]}.{patch + 1}";
        }

        return current;
    }
}
