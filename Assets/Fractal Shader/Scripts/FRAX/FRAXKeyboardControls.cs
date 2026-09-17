using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FractalShader
{
    /// <summary>Keyboard-only controls and help overlay for the FRAX demo scene.</summary>
    [RequireComponent(typeof(FRAX))]
    public sealed class FRAXKeyboardControls : MonoBehaviour
    {
        [SerializeField] private GameObject helpOverlay;
        [SerializeField, Min(0.01f)] private float curveChangePerSecond = 0.5f;

        private FRAX fractal;

        private void Awake()
        {
            fractal = GetComponent<FRAX>();
            HideLegacyOptions();
            CreateHelpOverlayIfNeeded();
        }

        private void HideLegacyOptions()
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform candidate in transforms)
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

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("FRAX keyboard help could not find a Canvas.", this);
                return;
            }

            helpOverlay = new GameObject("Keyboard Help", typeof(RectTransform), typeof(Image));
            helpOverlay.transform.SetParent(canvas.transform, false);

            RectTransform panel = helpOverlay.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(260f, -120f);
            panel.sizeDelta = new Vector2(370f, 245f);

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
            // Use Unity's built-in font so the help does not depend on this package's
            // incomplete TextMeshPro project settings.
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "<b>FRAX CONTROLS</b>\n\nW A S D  Move\nMouse  Look\nShift  Fly faster\nQ / E / Space  Down / Up\nLeft / Right Arrow  Curve Space\nEsc  Release mouse\nH  Hide / show this help";
        }

        private void Update()
        {
            // The imported demo controller can re-enable this legacy slider UI at runtime.
            // Keep the keyboard-only presentation free of the old controls.
            HideLegacyOptions();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.hKey.wasPressedThisFrame && helpOverlay != null)
                helpOverlay.SetActive(!helpOverlay.activeSelf);

            float direction = (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                              (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            if (Mathf.Approximately(direction, 0f))
                return;

            fractal.curve = Mathf.Clamp01(fractal.curve + direction * curveChangePerSecond * Time.deltaTime);
            fractal.ReRender();
        }
    }
}
