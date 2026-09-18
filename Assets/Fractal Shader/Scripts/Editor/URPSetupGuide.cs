using UnityEngine;
using UnityEditor;

namespace FractalShader
{
    public class URPSetupGuide : EditorWindow
    {
        // The guide remains available manually, but must not interrupt scene opening.
        [MenuItem("Tools/Fractal Framework/URP Setup Guide")]
        public static void ShowWindow()
        {
            // Creates and shows the window, focusing it if it's already open
            GetWindow<URPSetupGuide>("URP Setup Guide").Focus();
        }

        private void OnGUI()
        {
            GUILayout.Label("Fractal Framework - URP Compatibility Guide", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("The fractal renderer features in this project are configured for Unity's Render Graph path. " +
                "This guide no longer requires Compatibility Mode.", MessageType.Info);

            EditorGUILayout.Space();

            // Step-by-step instructions
            GUILayout.Label("Current setup:", EditorStyles.boldLabel);
            GUILayout.Label("The active renderer data already contains the fractal renderer features. Open the Graphics settings only if you need to inspect the project-wide URP configuration.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();

            // Button to quickly open settings
            if (GUILayout.Button("Open Graphics Settings"))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("The fractal demo scenes use the shared PC renderer data and its configured renderer features.", MessageType.Info);
        }
    }
}
