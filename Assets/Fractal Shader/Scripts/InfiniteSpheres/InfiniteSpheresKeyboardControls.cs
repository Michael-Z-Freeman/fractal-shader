using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FractalShader
{
    /// <summary>Keyboard-only controls and help overlay for the Infinite Spheres demo scene.</summary>
    [RequireComponent(typeof(InfiniteSpheres))]
    public sealed class InfiniteSpheresKeyboardControls : MonoBehaviour
    {
        [SerializeField] private GameObject helpOverlay;
        [SerializeField, Min(0.01f)] private float radiusChangePerSecond = 0.75f;
        [SerializeField] private float minimumRadius = 0.05f;
        [SerializeField] private float maximumRadius = 3f;

        private InfiniteSpheres fractal;

        private void Awake()
        {
            fractal = GetComponent<InfiniteSpheres>();
            HideLegacyOptions();
            CreateHelpOverlayIfNeeded();
        }

        private void Update()
        {
            // The imported demo can re-enable its legacy TMP options during startup.
            // Keep it disabled so this scene remains independent of TMP Essentials.
            HideLegacyOptions();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.hKey.wasPressedThisFrame && helpOverlay != null)
                helpOverlay.SetActive(!helpOverlay.activeSelf);

            if (keyboard.mKey.wasPressedThisFrame)
            {
                SceneManager.LoadScene("FractalMenu");
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
                fractal.O_Repeat = !fractal.useRepetition;

            if (keyboard.iKey.wasPressedThisFrame)
                fractal.O_Invert = !fractal.invertGeometry;

            float direction = (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                              (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            if (!Mathf.Approximately(direction, 0f))
                fractal.O_Radius = Mathf.Clamp(fractal.radius + direction * radiusChangePerSecond * Time.deltaTime, minimumRadius, maximumRadius);
        }

        private void HideLegacyOptions()
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (candidate.name == "Options" && candidate.GetComponentInParent<Canvas>() != null)
                {
                    candidate.gameObject.SetActive(false);
                    return;
                }
            }
        }

        private void CreateHelpOverlayIfNeeded()
        {
            if (helpOverlay != null)
                return;

            GameObject canvasObject = new GameObject("Infinite Spheres Keyboard Help Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            helpOverlay = new GameObject("Keyboard Help", typeof(RectTransform), typeof(Image));
            helpOverlay.transform.SetParent(canvas.transform, false);
            RectTransform panel = helpOverlay.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(260f, -120f);
            panel.sizeDelta = new Vector2(405f, 300f);

            Image panelImage = helpOverlay.GetComponent<Image>();
            panelImage.color = new Color(0.02f, 0.03f, 0.06f, 0.82f);
            panelImage.raycastTarget = false;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            labelObject.transform.SetParent(helpOverlay.transform, false);
            RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = new Vector2(18f, 14f);
            labelTransform.offsetMax = new Vector2(-18f, -14f);

            UnityEngine.UI.Text label = labelObject.GetComponent<UnityEngine.UI.Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "<b>INFINITE SPHERES CONTROLS</b>\n\nW A S D  Move\nMouse  Look\nShift  Fly faster\nQ / E / Space  Down / Up\nLeft / Right Arrow  Radius\nR  Toggle repetition\nI  Toggle inverted geometry\nM  Return to main menu\nEsc  Release mouse\nH  Hide / show this help";
        }
    }
}
