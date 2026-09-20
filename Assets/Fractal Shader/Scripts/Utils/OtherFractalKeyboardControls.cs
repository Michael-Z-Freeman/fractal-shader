using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FractalShader
{
    /// <summary>
    /// Keyboard controls and a dependency-free help overlay for the legacy fractal demo scenes.
    /// It changes only each demo's existing public option properties; rendering remains in its URP feature.
    /// </summary>
    [RequireComponent(typeof(App))]
    public sealed class OtherFractalKeyboardControls : MonoBehaviour
    {
        [SerializeField] private GameObject helpOverlay;
        private App app;
        private Text helpLabel;

        // Smooth damping state for Mandelbox scale
        private float mandelboxScaleVelocity;

        private void Awake()
        {
            app = GetComponent<App>();
            HideLegacyOptions();
            CreateHelpOverlay();
        }

        private void Update()
        {
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

            if (app is Mandelbox mandelbox) UpdateMandelbox(keyboard, mandelbox);
            else if (app is Mandelbrot mandelbrot) UpdateMandelbrot(keyboard, mandelbrot);
            else if (app is Mandelbulb mandelbulb) UpdateMandelbulb(keyboard, mandelbulb);
            else if (app is MengerSponge menger) UpdateMengerSponge(keyboard, menger);
            else if (app is OctahedronFlake octahedron) UpdateOctahedronFlake(keyboard, octahedron);
            else if (app is Sierpinski sierpinski) UpdateSierpinski(keyboard, sierpinski);

            if (helpOverlay != null && helpOverlay.activeSelf && helpLabel != null)
                helpLabel.text = HelpText();
        }

        private static float ArrowDirection(Keyboard keyboard) =>
            (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);

        private static float BracketDirection(Keyboard keyboard) =>
            // Support both the original [ / ] pair and the more natural / key the
            // launcher instructions previously implied. On compact Mac layouts the
            // bracket keys are not always convenient, so comma/period are aliases.
            ((keyboard.rightBracketKey.wasPressedThisFrame || keyboard.slashKey.wasPressedThisFrame || keyboard.periodKey.wasPressedThisFrame) ? 1f : 0f) -
            ((keyboard.leftBracketKey.wasPressedThisFrame || keyboard.commaKey.wasPressedThisFrame) ? 1f : 0f);

        private void UpdateMandelbox(Keyboard keyboard, Mandelbox fractal)
        {
            var helper = fractal.controlsHelper;

            // Toggle trackpad drag mode between Orbit and Scaling (T key)
            if (keyboard.tKey.wasPressedThisFrame && helper != null)
            {
                helper.trackpadScaleMode = !helper.trackpadScaleMode;
            }

            // Use shared targetScale on ControlsHelper so keyboard and trackpad share the same state and damping
            if (helper != null)
            {
                if (helper.targetScale < 0f) helper.targetScale = fractal.scale;

                float dir = ArrowDirection(keyboard);
                if (!Mathf.Approximately(dir, 0f))
                {
                    bool isFast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                    bool isSlow = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                                  keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed ||
                                  keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

                    // Calibrated gentle keyboard speed: 0.08x/sec baseline
                    float speed = isFast ? 0.35f : (isSlow ? 0.02f : 0.08f);
                    float step = 1.0f + dir * speed * Time.deltaTime;
                    helper.targetScale = Mathf.Clamp(helper.targetScale * step, 0.5f, 5f);
                }

                // If not currently dragging trackpad, keyboard damping handles the interpolation
                if (!helper.trackpadScaleMode || !helper.isDragging)
                {
                    if (!Mathf.Approximately(fractal.scale, helper.targetScale))
                    {
                        fractal.O_Scale = Mathf.SmoothDamp(fractal.scale, helper.targetScale, ref mandelboxScaleVelocity, 0.09f);
                    }
                }
            }

            float iterations = BracketDirection(keyboard);
            if (!Mathf.Approximately(iterations, 0f)) fractal.O_Iterations = Mathf.Clamp(fractal.iterations + iterations, 1, 50);
            if (keyboard.jKey.wasPressedThisFrame) fractal.O_Julia = !fractal.julia;
            if (keyboard.kKey.wasPressedThisFrame) fractal.O_Mix = fractal.mix < 0.5f ? 1f : 0f;
        }

        private static void UpdateMandelbrot(Keyboard keyboard, Mandelbrot fractal)
        {
            float direction = ArrowDirection(keyboard);
            if (!Mathf.Approximately(direction, 0f)) fractal.O_Iterations = Mathf.Clamp(fractal.iterations + direction * 5f * Time.deltaTime, 10, 1000);
            if (keyboard.jKey.wasPressedThisFrame) fractal.O_Julia = !fractal.julia;
        }

        private static void UpdateMandelbulb(Keyboard keyboard, Mandelbulb fractal)
        {
            float direction = ArrowDirection(keyboard);
            if (!Mathf.Approximately(direction, 0f)) fractal.O_Power = Mathf.Clamp(fractal.power + direction * 2f * Time.deltaTime, 1f, 20f);
            float iterations = BracketDirection(keyboard);
            if (!Mathf.Approximately(iterations, 0f)) fractal.O_Iterations = Mathf.Clamp(fractal.iterations + iterations, 1, 100);
            if (keyboard.jKey.wasPressedThisFrame) fractal.O_Julia = fractal.julia < 0.5f ? 1f : 0f;
            if (keyboard.aKey.wasPressedThisFrame) fractal.O_Alt = !fractal.alt;
            if (keyboard.kKey.wasPressedThisFrame) fractal.O_Mix = fractal.mix < 0.5f ? 1f : 0f;
        }

        private static void UpdateMengerSponge(Keyboard keyboard, MengerSponge fractal)
        {
            float direction = ArrowDirection(keyboard);
            if (!Mathf.Approximately(direction, 0f)) fractal.O_Size = Mathf.Clamp(fractal.O_Size + direction * Time.deltaTime, 0.1f, 10f);
            float iterations = BracketDirection(keyboard);
            if (!Mathf.Approximately(iterations, 0f)) fractal.O_Iterations = Mathf.Clamp(fractal.O_Iterations + iterations, 1, 10);
            if (keyboard.xKey.wasPressedThisFrame) fractal.O_Cut = fractal.O_Cut < 0.5f ? 1f : 0f;
            if (keyboard.gKey.wasPressedThisFrame) fractal.O_Edge = fractal.O_Edge < 1.5f ? 2f : 1f;
            if (keyboard.cKey.wasPressedThisFrame) fractal.O_Cantor = !fractal.O_Cantor;
        }

        private static void UpdateOctahedronFlake(Keyboard keyboard, OctahedronFlake fractal)
        {
            float direction = ArrowDirection(keyboard);
            if (!Mathf.Approximately(direction, 0f)) fractal.O_Size = Mathf.Clamp(fractal.size + direction * Time.deltaTime, 0.1f, 5f);
            float iterations = BracketDirection(keyboard);
            if (!Mathf.Approximately(iterations, 0f)) fractal.O_Iterations = Mathf.Clamp(fractal.iterations + iterations, 0, 15);
            if (keyboard.zKey.wasPressedThisFrame) fractal.O_SizeDec = Mathf.Clamp(fractal.sizeDec - 0.1f, 1f, 3f);
            if (keyboard.xKey.wasPressedThisFrame) fractal.O_SizeDec = Mathf.Clamp(fractal.sizeDec + 0.1f, 1f, 3f);
        }

        private static void UpdateSierpinski(Keyboard keyboard, Sierpinski fractal)
        {
            float direction = ArrowDirection(keyboard);
            if (!Mathf.Approximately(direction, 0f)) fractal.O_Division = Mathf.Clamp(fractal.divisionFactor + direction * Time.deltaTime, 2f, 4f);
            float iterations = BracketDirection(keyboard);
            if (!Mathf.Approximately(iterations, 0f)) fractal.O_Iterations = Mathf.Clamp(fractal.iterations + iterations, 1, 12);
            if (keyboard.zKey.wasPressedThisFrame) fractal.O_Position = Mathf.Clamp(fractal.cutoutPosition - 0.05f, 0f, 4f);
            if (keyboard.xKey.wasPressedThisFrame) fractal.O_Position = Mathf.Clamp(fractal.cutoutPosition + 0.05f, 0f, 4f);
            if (keyboard.cKey.wasPressedThisFrame) fractal.O_Size = Mathf.Clamp(fractal.holeSize - 0.05f, 0f, 1f);
            if (keyboard.vKey.wasPressedThisFrame) fractal.O_Size = Mathf.Clamp(fractal.holeSize + 0.05f, 0f, 1f);
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

        private void CreateHelpOverlay()
        {
            if (helpOverlay != null)
                return;

            GameObject canvasObject = new GameObject("Keyboard Help Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
            panel.sizeDelta = new Vector2(445f, 310f);

            Image panelImage = helpOverlay.GetComponent<Image>();
            panelImage.color = new Color(0.02f, 0.03f, 0.06f, 0.82f);
            panelImage.raycastTarget = false;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(helpOverlay.transform, false);
            RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = new Vector2(18f, 14f);
            labelTransform.offsetMax = new Vector2(-18f, -14f);

            helpLabel = labelObject.GetComponent<Text>();
            helpLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            helpLabel.fontSize = 18;
            helpLabel.alignment = TextAnchor.UpperLeft;
            helpLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            helpLabel.verticalOverflow = VerticalWrapMode.Overflow;
            helpLabel.color = Color.white;
            helpLabel.raycastTarget = false;
            helpLabel.text = HelpText();
        }

        private string HelpText()
        {
            string common = "\n\nM  Return to main menu\nH  Hide / show this help";
            if (app is Mandelbox mandelbox)
            {
                bool isTrackpadScale = mandelbox.controlsHelper != null && mandelbox.controlsHelper.trackpadScaleMode;
                string modeStr = isTrackpadScale ? "<color=#00FF99>Scale Mode</color>" : "Orbit Mode";
                return $"<b>MANDELBOX CONTROLS</b>\n\n" +
                       $"Scale: <b>{mandelbox.scale:F2}</b>\n" +
                       $"T  Trackpad Drag: <b>{modeStr}</b>\n" +
                       $"Left / Right Arrow  Smooth Scale\n" +
                       $"  + Shift (Turbo) / Cmd (Fine)\n" +
                       $"Mouse / Trackpad  Orbit / Zoom\n" +
                       $"[ / ] or , / .  Iterations ({mandelbox.iterations})\n" +
                       $"J  Toggle Julia mode\n" +
                       $"K  Toggle colour mix" + common;
            }
            if (app is Mandelbrot) return "<b>MANDELBROT CONTROLS</b>\n\nMouse wheel  Zoom\nMouse drag  Pan\nRight click  Julia at cursor\nLeft / Right Arrow  Iterations\nJ  Toggle Julia mode" + common;
            if (app is Mandelbulb) return "<b>MANDELBULB CONTROLS</b>\n\nMouse  Orbit / zoom\nLeft / Right Arrow  Power\n[ / ] or , / .  Iterations\nJ  Toggle Julia mode\nA  Toggle alternate formula\nK  Toggle colour mix" + common;
            if (app is MengerSponge) return "<b>MENGER SPONGE CONTROLS</b>\n\nMouse  Orbit / zoom\nLeft / Right Arrow  Size\n[ / ] or , / .  Iterations\nX  Toggle cut\nG  Toggle edge size\nC  Toggle Cantor mode" + common;
            if (app is OctahedronFlake) return "<b>OCTAHEDRON FLAKE CONTROLS</b>\n\nMouse  Orbit / zoom\nLeft / Right Arrow  Size\n[ / ] or , / .  Iterations\nZ / X  Scale per iteration" + common;
            return "<b>SIERPINSKI CONTROLS</b>\n\nLeft / Right Arrow  Division factor\n[ / ] or , / .  Iterations\nZ / X  Cutout position\nC / V  Hole size" + common;
        }
    }
}
