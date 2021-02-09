using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CustomEditor(typeof(PostProcessingRendererFeature))]
public class PostProcessingRendererFeatureEditor : Editor
{
    private int selectedVolume;
    private string[] volumeComponentOptions;
    private PostProcessingRendererFeature _rendererFeature;
    
    private void OnEnable()
    {
        volumeComponentOptions = getVolumeComponentOptions();
        _rendererFeature = (PostProcessingRendererFeature) target;
    }

    public override void OnInspectorGUI()
    {
        selectedVolume = _rendererFeature.settings.volumeComponentIndex;

        EditorGUI.BeginDisabledGroup(volumeComponentOptions.Length == 0);
        selectedVolume = EditorGUILayout.Popup("Volume Component" ,selectedVolume, volumeComponentOptions);
        EditorGUI.EndDisabledGroup();
        if (volumeComponentOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("There are no VolumeComponent classes with the ForPostProcessing attribute", MessageType.Warning);
            _rendererFeature.settings.volumeComponentIndex = 0;
        }
        
        else
        {
            _rendererFeature.settings.volumeComponentName = volumeComponentOptions[_rendererFeature.settings.volumeComponentIndex];
            _rendererFeature.settings.volumeComponentIndex = selectedVolume;
        }

        _rendererFeature.settings.effectMaterial = (Material) EditorGUILayout.ObjectField("Material",_rendererFeature.settings.effectMaterial, typeof(Material), false);
    }

    private string[] getVolumeComponentOptions()
    {
        return AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
            .Where(x => typeof(VolumeComponent).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract && x.GetCustomAttribute<ForCustomPostProcessingAttribute>() != null)
            .Select(x => x.Name).ToArray();
    }
}