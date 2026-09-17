using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FractalShader
{
    // [InitializeOnLoad] ensures the static constructor runs when the Unity Editor loads or recompiles
    [InitializeOnLoad]
    public class URPSetupGuide : EditorWindow
    {
        // List of scenes that should trigger the popup window
        private static readonly string[] targetScenes = {
            "FRAX",
            "InfiniteSpheres",
            "Mandelbox",
            "Mandelbrot",
            "Mandelbulb",
            "MengerSponge",
            "OctahedronFlake",
            "Sierpinski"
        };

        // Static constructor subscribes to the scene opened event
        static URPSetupGuide()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        // Called automatically every time a new scene is opened in the Editor
        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            foreach (string targetScene in targetScenes)
            {
                if (scene.name == targetScene)
                {
                    ShowWindow();
                    break; // Stop checking once we find a match
                }
            }
        }

        // Adds a button to the top menu (can still be opened manually)
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

            // Warning Box for Solution 1
            EditorGUILayout.HelpBox("Render Graph Warning Solution\n\n" +
                "The custom Renderer Features in this package (Infinite Spheres, Mandelbrot, Raymarch, etc.) " +
                "rely on Unity's standard rendering system. To ensure they work correctly with the new 'Render Graph' system, " +
                "you must enable Compatibility Mode.", MessageType.Warning);

            EditorGUILayout.Space();

            // Step-by-step instructions
            GUILayout.Label("Step-by-Step Solution:", EditorStyles.boldLabel);
            GUILayout.Label("1. Open Edit > Project Settings > Graphics from the top menu.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("2. Navigate to the URP settings.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("3. Check the 'Compatibility Mode (RenderGraph disabled)' option.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();

            // Button to quickly open settings
            if (GUILayout.Button("Open Graphics Settings"))
            {
                SettingsService.OpenProjectSettings("Project/Graphics");
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // Reminder for the Renderer Data setup
            EditorGUILayout.HelpBox("IMPORTANT REMINDER:\n\n" +
                "For the visual effects to render in your scene, you must select your active 'Universal Renderer Data' " +
                "asset in the Inspector. Then, scroll down to the 'Renderer Features' list and add the " +
                "features you want to use (e.g., Infinite Spheres Feature).", MessageType.Info);
        }
    }
}