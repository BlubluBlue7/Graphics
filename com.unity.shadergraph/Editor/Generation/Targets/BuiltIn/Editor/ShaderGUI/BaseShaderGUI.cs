using System;
using UnityEngine;
using UnityEngine.Rendering;
using RenderQueue = UnityEngine.Rendering.RenderQueue;

namespace UnityEditor.Rendering.BuiltIn.ShaderGraph
{
    public class BuiltInBaseShaderGUI : ShaderGUI
    {
        public enum SurfaceType
        {
            Opaque,
            Transparent
        }

        public enum BlendMode
        {
            Alpha,   // Old school alpha-blending mode, fresnel does not affect amount of transparency
            Premultiply, // Physically plausible transparency mode, implemented as alpha pre-multiply
            Additive,
            Multiply
        }

        public enum RenderFace
        {
            Front = 2,
            Back = 1,
            Both = 0
        }

        protected class Styles
        {
            public static readonly string[] surfaceTypeNames = Enum.GetNames(typeof(SurfaceType));
            public static readonly string[] blendModeNames = Enum.GetNames(typeof(BlendMode));
            public static readonly string[] renderFaceNames = Enum.GetNames(typeof(RenderFace));

            public static readonly GUIContent surfaceType = EditorGUIUtility.TrTextContent("Surface Type",
                "Select a surface type for your texture. Choose between Opaque or Transparent.");
            public static readonly GUIContent blendingMode = EditorGUIUtility.TrTextContent("Blending Mode",
                "Controls how the color of the Transparent surface blends with the Material color in the background.");
            public static readonly GUIContent cullingText = EditorGUIUtility.TrTextContent("Render Face",
                "Specifies which faces to cull from your geometry. Front culls front faces. Back culls backfaces. None means that both sides are rendered.");
            public static readonly GUIContent alphaClipText = EditorGUIUtility.TrTextContent("Alpha Clipping",
                "Makes your Material act like a Cutout shader. Use this to create a transparent effect with hard edges between opaque and transparent areas.");
        }

        bool m_FirstTimeApply = true;

        override public void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            Material targetMat = materialEditor.target as Material;
            // Make sure that needed setup (ie keywords/renderqueue) are set up if we're switching some existing
            // material to a universal shader.
            if (m_FirstTimeApply)
            {
                DrawGui(materialEditor, targetMat, properties);
                UpdateMaterials(materialEditor);

                m_FirstTimeApply = false;
            }

            ShaderPropertiesGUI(materialEditor, targetMat, properties);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            // Clear all keywords for fresh start
            // Note: this will nuke user-selected custom keywords when they change shaders
            material.shaderKeywords = null;

            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            // Setup keywords based on the new shader
            Unity.Rendering.BuiltIn.ShaderUtils.ResetMaterialKeywords(material);
        }

        static void ShaderPropertiesGUI(MaterialEditor materialEditor, Material material, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            DrawGui(materialEditor, material, properties);
            if (EditorGUI.EndChangeCheck())
                UpdateMaterials(materialEditor);
        }

        static void DrawGui(MaterialEditor materialEditor, Material material, MaterialProperty[] properties)
        {
            var surfaceTypeProp = FindProperty(Property.Surface(), properties, false);
            if (surfaceTypeProp != null)
            {
                DoPopup(Styles.surfaceType, materialEditor, surfaceTypeProp, Styles.surfaceTypeNames);
                var surfaceType = (SurfaceType)surfaceTypeProp.floatValue;
                if (surfaceType == SurfaceType.Transparent)
                {
                    var blendModeProp = FindProperty(Property.Blend(), properties, false);
                    DoPopup(Styles.blendingMode, materialEditor, blendModeProp, Styles.blendModeNames);
                }
            }
            var cullingProp = FindProperty(Property.Cull(), properties, false);
            DoPopup(Styles.cullingText, materialEditor, cullingProp, Enum.GetNames(typeof(RenderFace)));

            var alphaClipProp = FindProperty(Property.AlphaClip(), properties, false);
            DrawFloatToggleProperty(Styles.alphaClipText, alphaClipProp);

            DrawShaderGraphProperties(materialEditor, material, properties);
        }

        static void DrawShaderGraphProperties(MaterialEditor materialEditor, Material material, MaterialProperty[] properties)
        {
            if (properties == null)
                return;

            for (var i = 0; i < properties.Length; i++)
            {
                if ((properties[i].flags & (MaterialProperty.PropFlags.HideInInspector | MaterialProperty.PropFlags.PerRendererData)) != 0)
                    continue;

                float h = materialEditor.GetPropertyHeight(properties[i], properties[i].displayName);
                Rect r = EditorGUILayout.GetControlRect(true, h, EditorStyles.layerMaskField);

                materialEditor.ShaderProperty(r, properties[i], properties[i].displayName);
            }
        }

        static void UpdateMaterials(MaterialEditor materialEditor)
        {
            foreach (var obj in materialEditor.targets)
                SetupSurface((Material)obj);
        }

        public static void SetupSurface(Material material)
        {
            bool alphaClipping = false;
            var alphaClipProp = Property.AlphaClip();
            if (material.HasProperty(alphaClipProp))
                alphaClipping = material.GetFloat(alphaClipProp) >= 0.5;

            CoreUtils.SetKeyword(material, "_ALPHATEST_ON", alphaClipping);
            CoreUtils.SetKeyword(material, "_AlphaClip", alphaClipping);

            var surfaceTypeProp = Property.Surface();
            if(material.HasProperty(surfaceTypeProp))
            {
                var surfaceType = (SurfaceType)material.GetFloat(surfaceTypeProp);
                if (surfaceType == SurfaceType.Opaque)
                {
                    string renderType;
                    RenderQueue renderQueue;
                    if (alphaClipping)
                    {
                        renderQueue = RenderQueue.AlphaTest;
                        renderType = "TransparentCutout";
                    }
                    else
                    {
                        renderQueue = RenderQueue.Geometry;
                        renderType = "Opaque";
                    }

                    CoreUtils.SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", surfaceType == SurfaceType.Transparent);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.SetOverrideTag("RenderType", renderType);
                    material.renderQueue = (int)renderQueue;
                    SetBlendMode(material, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.Zero);
                    SetMaterialZWriteProperty(material, true);
                }
                else
                {
                    var blendProp = Property.Blend();
                    if(material.HasProperty(blendProp))
                    {
                        var blendMode = (BlendMode)material.GetFloat(blendProp);
                        if (blendMode == BlendMode.Alpha)
                            SetBlendMode(material, UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        else if (blendMode == BlendMode.Premultiply)
                            SetBlendMode(material, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        else if (blendMode == BlendMode.Additive)
                            SetBlendMode(material, UnityEngine.Rendering.BlendMode.One, UnityEngine.Rendering.BlendMode.One);
                        else if (blendMode == BlendMode.Multiply)
                            SetBlendMode(material, UnityEngine.Rendering.BlendMode.DstColor, UnityEngine.Rendering.BlendMode.Zero);
                    }
                    
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetOverrideTag("RenderType", "Transparent");
                    SetMaterialZWriteProperty(material, false);
                }
            }
        }

        static void SetMaterialZWriteProperty(Material material, bool state)
        {
            var zWriteProp = Property.ZWrite();
            if(material.HasProperty(zWriteProp))
            {
                material.SetFloat(zWriteProp, state == true ? 1.0f : 0.0f);
            }
        }

        static void SetBlendMode(Material material, UnityEngine.Rendering.BlendMode srcBlendMode, UnityEngine.Rendering.BlendMode dstBlendMode)
        {
            var srcBlendProp = Property.SrcBlend();
            if (material.HasProperty(srcBlendProp))
                material.SetFloat(srcBlendProp, (int)srcBlendMode);
            var dstBlendProp = Property.DstBlend();
            if (material.HasProperty(dstBlendProp))
                material.SetFloat(dstBlendProp, (int)dstBlendMode);
        }

        public static void DoPopup(GUIContent label, MaterialEditor materialEditor, MaterialProperty property, string[] options)
        {
            if (property == null)
                return;

            materialEditor.PopupShaderProperty(property, label, options);
        }
        public static void DrawFloatToggleProperty(GUIContent styles, MaterialProperty prop)
        {
            if (prop == null)
                return;

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;
            bool newValue = EditorGUILayout.Toggle(styles, prop.floatValue == 1);
            if (EditorGUI.EndChangeCheck())
                prop.floatValue = newValue ? 1.0f : 0.0f;
            EditorGUI.showMixedValue = false;
        }
    }

    // Currently the shader graph project doesn't have a reference to the necessary assembly to access this (RenderPipelines.Core.Editor)
    public static partial class MaterialEditorExtension
    {
        public static int PopupShaderProperty(this MaterialEditor editor, MaterialProperty prop, GUIContent label, string[] displayedOptions)
        {
            int val = (int)prop.floatValue;

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = prop.hasMixedValue;
            int newValue = EditorGUILayout.Popup(label, val, displayedOptions);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck() && (newValue != val || prop.hasMixedValue))
            {
                editor.RegisterPropertyChangeUndo(label.text);
                prop.floatValue = val = newValue;
            }

            return val;
        }
    }
}
