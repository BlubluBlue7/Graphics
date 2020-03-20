﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Create a new type of Settings Asset.
class PerformanceTestsSettings : ScriptableObject
{
    public const string k_PerformanceTestsPath = "Assets/PerformanceTestsSettings.asset";

    [SerializeField]
    public TestSceneAsset                   testDescriptionAsset = null;

    [SerializeField]
    public EditorShaderStaticAnalysisAsset  staticAnalysisAsset = null;

    internal static PerformanceTestsSettings GetOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<PerformanceTestsSettings>(k_PerformanceTestsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PerformanceTestsSettings>();
            AssetDatabase.CreateAsset(settings, k_PerformanceTestsPath);
            AssetDatabase.SaveAssets();
        }
        return settings;
    }

    internal static SerializedObject GetSerializedSettings()
    {
        return new SerializedObject(GetOrCreateSettings());
    }
}

// Register a SettingsProvider using IMGUI for the drawing framework:
static class PerformanceTestsIMGUIRegister
{
    [SettingsProvider]
    public static SettingsProvider CreatePerformanceTestsProvider()
    {
        // First parameter is the path in the Settings window.
        // Second parameter is the scope of this setting: it only appears in the Project Settings window.
        var provider = new SettingsProvider("Project/Performance Testing", SettingsScope.Project)
        {
            // By default the last token of the path is used as display name if no label is provided.
            label = "Settings",
            // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
            guiHandler = (searchContext) =>
            {
                var settings = PerformanceTestsSettings.GetSerializedSettings();
                EditorGUILayout.PropertyField(settings.FindProperty("testDescriptionAsset"), new GUIContent("testDescriptionAsset"));
                EditorGUILayout.PropertyField(settings.FindProperty("staticAnalysisAsset"), new GUIContent("staticAnalysisAsset"));
            },

            // Populate the search keywords to enable smart search filtering and label highlighting:
            keywords = new HashSet<string>(new[] { "Performance" })
        };

        return provider;
    }
}

// Create PerformanceTestsProvider by deriving from SettingsProvider:
class PerformanceTestsProvider : SettingsProvider
{
    private SerializedObject m_CustomSettings;

    class Styles
    {
        public static GUIContent number = new GUIContent("My Number");
        public static GUIContent someString = new GUIContent("Some string");
    }

    const string k_PerformanceTestsPath = "ProjectSettings/PerformanceTestsSettings.asset";
    public PerformanceTestsProvider(string path, SettingsScope scope = SettingsScope.User)
        : base(path, scope) {}

    public static bool IsSettingsAvailable()
    {
        return File.Exists(k_PerformanceTestsPath);
    }

    public override void OnActivate(string searchContext, VisualElement rootElement)
    {
        // This function is called when the user clicks on the MyCustom element in the Settings window.
        m_CustomSettings = PerformanceTestsSettings.GetSerializedSettings();
    }

    public override void OnGUI(string searchContext)
    {
        // Use IMGUI to display UI:
        // EditorGUILayout.PropertyField(m_CustomSettings.FindProperty("m_Number"), Styles.number);
        // EditorGUILayout.PropertyField(m_CustomSettings.FindProperty("m_SomeString"), Styles.someString);
        Debug.Log("UI ?");
    }

    // Register the SettingsProvider
    [SettingsProvider]
    public static SettingsProvider CreatePerformanceTestsProvider()
    {
        if (IsSettingsAvailable())
        {
            var provider = new PerformanceTestsProvider("Project/PerformanceTestsProvider", SettingsScope.Project);

            // Automatically extract all keywords from the Styles.
            provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
            return provider;
        }

        // Settings Asset doesn't exist yet; no need to display anything in the Settings window.
        return null;
    }
}
