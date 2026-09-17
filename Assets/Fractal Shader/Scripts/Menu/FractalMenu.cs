using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FractalShader
{
    /// <summary>A self-contained launcher for the imported fractal demo scenes.</summary>
    public sealed class FractalMenu : MonoBehaviour
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Fractal Shader/Scenes/FRAX.unity",
            "Assets/Fractal Shader/Scenes/InfiniteSpheres.unity",
            "Assets/Fractal Shader/Scenes/Mandelbox.unity",
            "Assets/Fractal Shader/Scenes/Mandelbrot.unity",
            "Assets/Fractal Shader/Scenes/Mandelbulb.unity",
            "Assets/Fractal Shader/Scenes/MengerSponge.unity",
            "Assets/Fractal Shader/Scenes/OctahedronFlake.unity",
            "Assets/Fractal Shader/Scenes/Sierpinski.unity"
        };

        private static readonly string[] DisplayNames =
        {
            "FRAX", "Infinite Spheres", "Mandelbox", "Mandelbrot",
            "Mandelbulb", "Menger Sponge", "Octahedron Flake", "Sierpinski"
        };

        private int selectedIndex;

        private void Awake()
        {
            BuildInterface();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame) LoadFractal(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) LoadFractal(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) LoadFractal(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) LoadFractal(3);
            else if (keyboard.digit5Key.wasPressedThisFrame) LoadFractal(4);
            else if (keyboard.digit6Key.wasPressedThisFrame) LoadFractal(5);
            else if (keyboard.digit7Key.wasPressedThisFrame) LoadFractal(6);
            else if (keyboard.digit8Key.wasPressedThisFrame) LoadFractal(7);

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                selectedIndex = (selectedIndex + ScenePaths.Length - 1) % ScenePaths.Length;
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                selectedIndex = (selectedIndex + 1) % ScenePaths.Length;
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                LoadFractal(selectedIndex);
        }

        private void BuildInterface()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasObject = new GameObject("Fractal Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
            }

            Image background = CreateImage(canvasObject.transform, "Background", new Color(0.015f, 0.02f, 0.05f, 1f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Text heading = CreateText(canvasObject.transform, "Heading", font, 52, TextAnchor.MiddleCenter, "FRACTAL SHADER", Color.white);
            SetRect(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(900f, 80f));

            Text instructions = CreateText(canvasObject.transform, "Instructions", font, 24, TextAnchor.MiddleCenter, "Choose a demo  •  Press 1–8, use arrow keys + Enter, or click a tile", new Color(0.7f, 0.8f, 1f, 1f));
            SetRect(instructions.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(1100f, 48f));

            for (int index = 0; index < ScenePaths.Length; index++)
            {
                int capturedIndex = index;
                int column = index % 2;
                int row = index / 2;
                Button button = CreateButton(canvasObject.transform, font, $"{index + 1}.  {DisplayNames[index]}", capturedIndex);
                SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(column == 0 ? -310f : 310f, 170f - row * 125f), new Vector2(560f, 96f));
                button.onClick.AddListener(() => LoadFractal(capturedIndex));
            }

            Text footer = CreateText(canvasObject.transform, "Footer", font, 18, TextAnchor.MiddleCenter, "Each choice loads its original demo scene. No fractal assets have been modified.", new Color(0.55f, 0.65f, 0.85f, 1f));
            SetRect(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(1200f, 36f));
        }

        private static Button CreateButton(Transform parent, Font font, string label, int index)
        {
            GameObject buttonObject = new GameObject($"{index + 1} - {label}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.19f, 0.37f, 0.95f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.6f, 0.8f, 1f, 1f);
            colors.pressedColor = new Color(0.35f, 0.55f, 0.9f, 1f);
            button.colors = colors;

            Text text = CreateText(buttonObject.transform, "Label", font, 28, TextAnchor.MiddleLeft, label, Color.white);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-30f, 0f));
            text.raycastTarget = false;
            return button;
        }

        private void LoadFractal(int index)
        {
            if (index >= 0 && index < ScenePaths.Length)
                SceneManager.LoadScene(ScenePaths[index]);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string name, Font font, int fontSize, TextAnchor alignment, string value, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            return text;
        }

        private static void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
        }
    }
}
