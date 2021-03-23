using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering
{
    /// <summary>
    /// An Asset which holds a set of settings to use with a <see cref="Probe Reference Volume"/>.
    /// </summary>
    public sealed class ProbeReferenceVolumeProfile : ScriptableObject
    {
        internal static readonly int k_MaxSubdivision = 7;

        /// <summary>
        /// The default dimensions for APV's index data structure.
        /// </summary>
        public static Vector3Int s_DefaultIndexDimensions = new Vector3Int(1024, 64, 1024);

        /// <summary>
        /// The size of a Cell.
        /// </summary>
        [Min(1)]
        public int cellSize = 64;
        /// <summary>
        /// The size of a Brick.
        /// </summary>
        [Min(0.01f)]
        public float minDistanceBetweenProbes = 1.0f;
        /// <summary>
        /// The normal bias to apply during shading.
        /// </summary>
        [Range(0.0f, 1.0f), Delayed]
        public float normalBias = 0.2f;

        public int maxSubdivision => Mathf.CeilToInt(Mathf.Log((float)cellSize / brickSize, 3));
        public float brickSize => Mathf.Max(0.01f, minDistanceBetweenProbes * 3.0f);

        /// <summary>
        /// Determines if the Probe Reference Volume Profile is equivalent to another one.
        /// </summary>
        /// <param name ="otherProfile">The profile to compare with.</param>
        /// <returns>Whether the Probe Reference Volume Profile is equivalent to another one.</returns>
        public bool IsEquivalent(ProbeReferenceVolumeProfile otherProfile)
        {
            return minDistanceBetweenProbes == otherProfile.minDistanceBetweenProbes &&
                cellSize == otherProfile.cellSize &&
                maxSubdivision == otherProfile.maxSubdivision &&
                normalBias == otherProfile.normalBias;
        }
    }

#if UNITY_EDITOR
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ProbeReferenceVolumeProfile))]
    internal class ProbeReferenceVolumeProfileEditor : Editor
    {
        private SerializedProperty m_CellSize;
        private SerializedProperty m_MinDistanceBetweenProbes;
        private SerializedProperty m_NormalBias;
        private SerializedProperty m_IndexDimensions;
        ProbeReferenceVolumeProfile profile => target as ProbeReferenceVolumeProfile;

        sealed class Styles
        {
            // TODO: Better tooltip are needed here.
            public readonly GUIContent cellSizeStyle = new GUIContent("Cell Size", "Determine the size of the cells.");
            public readonly GUIContent minDistanceBetweenProbes = new GUIContent("Min Distance Between Probes", "The minimal distance between two probes in meters.");
            public readonly GUIContent normalBias = new GUIContent("Normal Bias", "The normal bias used when sampling the volume. It can reduce leaking.");
            public readonly GUIContent indexDimensions = new GUIContent("Index Dimensions", "The dimensions of the index buffer.");
        }

        static Styles s_Styles = new Styles();

        private void OnEnable()
        {
            m_CellSize = serializedObject.FindProperty(nameof(ProbeReferenceVolumeProfile.cellSize));
            m_MinDistanceBetweenProbes = serializedObject.FindProperty(nameof(ProbeReferenceVolumeProfile.minDistanceBetweenProbes));
            m_NormalBias = serializedObject.FindProperty(nameof(ProbeReferenceVolumeProfile.normalBias));
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_CellSize, s_Styles.cellSizeStyle);
            EditorGUILayout.PropertyField(m_MinDistanceBetweenProbes, s_Styles.minDistanceBetweenProbes);
            EditorGUILayout.HelpBox("Maximum subvision of the volume: " + profile.maxSubdivision, MessageType.Info);
            EditorGUILayout.PropertyField(m_NormalBias, s_Styles.normalBias);

            ProbeReferenceVolume.instance.normalBiasFromProfile = m_NormalBias.floatValue;

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                float minDistanceBetweenProbes = ((float)profile.cellSize / Mathf.Pow(3, ProbeReferenceVolumeProfile.k_MaxSubdivision)) / 3.0f;
                if (profile.minDistanceBetweenProbes < minDistanceBetweenProbes)
                    profile.minDistanceBetweenProbes = minDistanceBetweenProbes;
            }
        }
    }
#endif
}
