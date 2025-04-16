using UnityEngine;
using UnityEditor;
using System;

public class RxAssetLibraryUploadPopup : EditorWindow
{
    private UnityEngine.Object newVersionAsset;
    private string assetName;
    private string category;
    private string tags;
    private string version;

    private static RxAssetLibraryTabs.RxAssetMetadata previousMetadata;

    public static void Open(RxAssetLibraryTabs.RxAssetMetadata metadata)
    {
        previousMetadata = metadata;
        RxAssetLibraryUploadPopup window = CreateInstance<RxAssetLibraryUploadPopup>();
        window.titleContent = new GUIContent("Upload New Version");
        window.minSize = new Vector2(400, 220);
        window.ShowUtility();
    }

    private void OnEnable()
    {
        if (previousMetadata != null)
        {
            assetName = previousMetadata.name;
            category = previousMetadata.category;
            tags = string.Join(", ", previousMetadata.tags);
            version = SuggestNextVersion(previousMetadata.version);
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Upload New Version of " + assetName, EditorStyles.boldLabel);
        newVersionAsset = EditorGUILayout.ObjectField("New Asset", newVersionAsset, typeof(UnityEngine.Object), false);

        EditorGUILayout.Space();
        category = EditorGUILayout.TextField("Category", category);
        tags = EditorGUILayout.TextField("Tags", tags);
        version = EditorGUILayout.TextField("Version", version);

        GUI.enabled = newVersionAsset != null;
        if (GUILayout.Button("Export Version " + version))
        {
            RxAssetLibraryExportHelper.ExportAsset(newVersionAsset, assetName, category, tags, version);
            Close();
        }
        GUI.enabled = true;
    }

    private string SuggestNextVersion(string currentVersion)
    {
        if (Version.TryParse(currentVersion, out Version parsed))
        {
            return $"{parsed.Major}.{parsed.Minor}.{parsed.Build + 1}";
        }
        return "1.0.0";
    }
}