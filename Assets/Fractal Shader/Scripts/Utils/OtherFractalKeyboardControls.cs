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
        private GameObject frameRateCanvas;
        private Text frameRateLabel;
        private float smoothedFrameRate;

        // Smooth damping state for Mandelbox scale
        private float mandelboxScaleVelocity;
        private bool mandelboxAutoLoop;
        private float mandelboxLoopPhase;
        private const float MandelboxLoopDuration = 270f; // Ultra-slow full round-trip duration in seconds (4.5 minutes)

        // Relative traversal speeds: 0.5-1.0 and 1.5-5.0 are fast, while 1.0-1.5 is slow.
        private const float FastLoopSpeed = 3f;
        private const float SlowLoopSpeed = 1f / 3f;
        private const float LowRangeDuration = (1f - 0.5f) / FastLoopSpeed;
        private const float MiddleRangeDuration = (1.5f - 1f) / SlowLoopSpeed;
        private const float HighRangeDuration = (5f - 1.5f) / FastLoopSpeed;
        private const float OneWayLoopDuration = LowRangeDuration + MiddleRangeDuration + HighRangeDuration;

        // Microphone-reactive Mandelbox scale state.
        private const int MicrophoneSampleCount = 256;
        private const float MicrophoneCentreMin = 0.99f;
        private const float MicrophoneCentreMax = 1.20f;
        private const float MicrophonePreferredScale = 1.02f;
        private const float MicrophoneLowerRangeBias = 5.5f;
        private const float MicrophoneHighScaleThreshold = 1.05f;
        private const float MicrophoneHighExcursionChance = 0.0025f;
        private const float MicrophoneHighExcursionDuration = 0.35f;
        private const float MicrophoneNoiseFloor = 0.018f;
        private const float MicrophoneFullScaleLevel = 0.09f;
        private bool microphoneScaleMode;
        private AudioClip microphoneClip;
        private readonly float[] microphoneSamples = new float[MicrophoneSampleCount];
        private float nextMicrophoneNudgeTime;
        private float microphoneScaleTarget = 1f;
        private float microphoneScaleVelocity;
        private float microphoneHighExcursionUntil;

        private void Awake()
        {
            app = GetComponent<App>();
            HideLegacyOptions();
            CreateHelpOverlay();
            CreateFrameRateDisplay();
        }

        private void Update()
        {
            HideLegacyOptions();
            UpdateFrameRateDisplay();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.hKey.wasPressedThisFrame && helpOverlay != null)
            {
                bool showOverlay = !helpOverlay.activeSelf;
                helpOverlay.SetActive(showOverlay);
                if (frameRateCanvas != null)
                    frameRateCanvas.SetActive(showOverlay);
            }

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

        private void OnDestroy()
        {
            StopMicrophoneInput();
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

            // Toggle automated slow scale loop through entire range and back (L key)
            if (keyboard.lKey.wasPressedThisFrame)
            {
                mandelboxAutoLoop = !mandelboxAutoLoop;
                if (mandelboxAutoLoop)
                {
                    // Map the current scale into the descending half of the loop so it heads to 0.5 first.
                    float currentClamped = Mathf.Clamp(fractal.scale, 0.5f, 5.0f);
                    mandelboxLoopPhase = Mathf.PI * (1f - ScaleToLoopProgress(currentClamped));
                }
            }

            // Toggle microphone-reactive scale nudges (U key).
            if (keyboard.uKey.wasPressedThisFrame)
            {
                microphoneScaleMode = !microphoneScaleMode;
                if (microphoneScaleMode)
                {
                    mandelboxAutoLoop = false;
                    microphoneScaleTarget = Mathf.Clamp(fractal.scale, MicrophoneCentreMin, MicrophoneCentreMax);
                    microphoneScaleVelocity = 0f;
                    microphoneHighExcursionUntil = 0f;
                    nextMicrophoneNudgeTime = Time.unscaledTime;
                    StartMicrophoneInput();
                }
                else
                {
                    StopMicrophoneInput();
                }
            }

            // Use shared targetScale on ControlsHelper so keyboard and trackpad share the same state and damping
            if (helper != null)
            {
                if (helper.targetScale < 0f) helper.targetScale = fractal.scale;

                float dir = ArrowDirection(keyboard);
                if (!Mathf.Approximately(dir, 0f))
                {
                    // Manual control interrupts automated loop
                    mandelboxAutoLoop = false;

                    bool isFast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                    bool isSlow = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                                  keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed ||
                                  keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

                    // Calibrated gentle keyboard speed: 0.08x/sec baseline
                    float speed = isFast ? 0.35f : (isSlow ? 0.02f : 0.08f);
                    float step = 1.0f + dir * speed * Time.deltaTime;
                    helper.targetScale = Mathf.Clamp(helper.targetScale * step, 0.5f, 5f);
                }

                if (helper.trackpadScaleMode && helper.isDragging)
                {
                    // Trackpad drag interrupts automated loop
                    mandelboxAutoLoop = false;
                }

                if (microphoneScaleMode)
                {
                    UpdateMicrophoneScale(fractal, helper);
                }
                else if (mandelboxAutoLoop)
                {
                    // Advance smooth geometric ping-pong phase
                    mandelboxLoopPhase += (2f * Mathf.PI / MandelboxLoopDuration) * Time.deltaTime;
                    if (mandelboxLoopPhase >= 2f * Mathf.PI)
                        mandelboxLoopPhase -= 2f * Mathf.PI;

                    // The first half descends from 5.0 to 0.5; the second half climbs back.
                    float cycleProgress = mandelboxLoopPhase / (2f * Mathf.PI);
                    float scaleProgress = cycleProgress <= 0.5f
                        ? 1f - cycleProgress * 2f
                        : (cycleProgress - 0.5f) * 2f;
                    float autoScale = LoopProgressToScale(scaleProgress);

                    helper.targetScale = autoScale;
                    fractal.O_Scale = autoScale;
                }
                else if (!helper.trackpadScaleMode || !helper.isDragging)
                {
                    // Smooth damping handles manual keyboard interpolation
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

        private void StartMicrophoneInput()
        {
            if (Microphone.devices.Length == 0)
                return;

            Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (microphoneClip == null)
                microphoneClip = Microphone.Start(null, true, 1, 44100);
        }

        private void StopMicrophoneInput()
        {
            if (Microphone.IsRecording(null))
                Microphone.End(null);

            microphoneClip = null;
        }

        private void UpdateMicrophoneScale(Mandelbox fractal, ControlsHelper helper)
        {
            if (microphoneClip == null || !Microphone.IsRecording(null))
            {
                StartMicrophoneInput();
                return;
            }

            int microphonePosition = Microphone.GetPosition(null) - MicrophoneSampleCount;
            if (microphonePosition < 0)
                microphonePosition += microphoneClip.samples;
            microphoneClip.GetData(microphoneSamples, microphonePosition);

            float sumOfSquares = 0f;
            for (int i = 0; i < microphoneSamples.Length; i++)
                sumOfSquares += microphoneSamples[i] * microphoneSamples[i];
            float rmsLevel = Mathf.Sqrt(sumOfSquares / microphoneSamples.Length);
            // Ignore quiet room tone, then strongly expand the remaining microphone range.
            float volume = Mathf.InverseLerp(MicrophoneNoiseFloor, MicrophoneFullScaleLevel, rmsLevel);

            // Audio events add a changing push to the scale velocity. The target then glides continuously,
            // like a sequence of uneven manual trackpad drags rather than discrete scale jumps.
            if (Time.unscaledTime >= nextMicrophoneNudgeTime)
            {
                float impulse = Random.Range(-1f, 1f) * Mathf.Lerp(0.04f, 1.10f, volume);

                // The 1.05-1.20 range is a rare loud-audio flourish, rather than the usual destination.
                if (impulse > 0f && microphoneScaleTarget <= MicrophoneHighScaleThreshold &&
                    volume > 0.65f && Random.value < MicrophoneHighExcursionChance * volume)
                {
                    microphoneHighExcursionUntil = Time.unscaledTime + MicrophoneHighExcursionDuration;
                }

                float activeUpperLimit = Time.unscaledTime < microphoneHighExcursionUntil
                    ? MicrophoneCentreMax
                    : MicrophoneHighScaleThreshold;

                // Reduce upward pushes as the scale leaves the preferred lower range.
                if (impulse > 0f)
                {
                    float highScaleProgress = Mathf.InverseLerp(MicrophoneHighScaleThreshold, activeUpperLimit, microphoneScaleTarget);
                    impulse *= Mathf.Lerp(1f, 0.05f, highScaleProgress);
                }
                microphoneScaleVelocity = Mathf.Clamp(microphoneScaleVelocity + impulse, -1.35f, 1.35f);
                nextMicrophoneNudgeTime = Time.unscaledTime + Mathf.Lerp(0.18f, 0.025f, volume);
            }

            float dt = Time.unscaledDeltaTime;
            // Strongly favour the 0.99-1.05 region; high excursions return quickly.
            float highScaleReturnProgress = Mathf.InverseLerp(MicrophoneHighScaleThreshold, MicrophoneCentreMax, microphoneScaleTarget);
            float restoringForce = MicrophoneLowerRangeBias * Mathf.Lerp(1f, 10f, highScaleReturnProgress);
            microphoneScaleVelocity += (MicrophonePreferredScale - microphoneScaleTarget) * restoringForce * dt;
            microphoneScaleVelocity *= Mathf.Exp(-Mathf.Lerp(1.8f, 0.35f, volume) * dt);
            microphoneScaleTarget += microphoneScaleVelocity * dt;

            float allowedUpperLimit = Time.unscaledTime < microphoneHighExcursionUntil
                ? MicrophoneCentreMax
                : MicrophoneHighScaleThreshold;
            if (microphoneScaleTarget > allowedUpperLimit)
            {
                microphoneScaleTarget = allowedUpperLimit;
                microphoneScaleVelocity = Mathf.Min(0f, microphoneScaleVelocity * -0.4f);
            }

            // Reverse gently at either end instead of abruptly clamping a jump.
            if (microphoneScaleTarget < MicrophoneCentreMin || microphoneScaleTarget > MicrophoneCentreMax)
            {
                microphoneScaleTarget = Mathf.Clamp(microphoneScaleTarget, MicrophoneCentreMin, MicrophoneCentreMax);
                microphoneScaleVelocity *= -0.55f;
            }

            helper.targetScale = microphoneScaleTarget;
            fractal.O_Scale = Mathf.SmoothDamp(fractal.scale, microphoneScaleTarget, ref mandelboxScaleVelocity, 0.035f);
        }

        // Converts time through one upward leg of the loop (0.5 -> 5.0) into a scale value.
        private static float LoopProgressToScale(float progress)
        {
            float weightedTime = Mathf.Clamp01(progress) * OneWayLoopDuration;
            if (weightedTime < LowRangeDuration)
                return 0.5f + weightedTime * FastLoopSpeed;

            weightedTime -= LowRangeDuration;
            if (weightedTime < MiddleRangeDuration)
                return 1f + weightedTime * SlowLoopSpeed;

            weightedTime -= MiddleRangeDuration;
            return Mathf.Min(5f, 1.5f + weightedTime * FastLoopSpeed);
        }

        // Converts a scale value into time through one upward leg so enabling the loop has no jump.
        private static float ScaleToLoopProgress(float scale)
        {
            float weightedTime;
            if (scale < 1f)
                weightedTime = (scale - 0.5f) / FastLoopSpeed;
            else if (scale < 1.5f)
                weightedTime = LowRangeDuration + (scale - 1f) / SlowLoopSpeed;
            else
                weightedTime = LowRangeDuration + MiddleRangeDuration + (scale - 1.5f) / FastLoopSpeed;

            return Mathf.Clamp01(weightedTime / OneWayLoopDuration);
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
            panel.sizeDelta = new Vector2(450f, 360f);

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

        private void CreateFrameRateDisplay()
        {
            frameRateCanvas = new GameObject("Frame Rate Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = frameRateCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;

            CanvasScaler scaler = frameRateCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject labelObject = new GameObject("Frame Rate", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(frameRateCanvas.transform, false);
            RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
            labelTransform.anchorMin = new Vector2(1f, 1f);
            labelTransform.anchorMax = new Vector2(1f, 1f);
            labelTransform.pivot = new Vector2(1f, 1f);
            labelTransform.anchoredPosition = new Vector2(-32f, -28f);
            labelTransform.sizeDelta = new Vector2(180f, 36f);

            frameRateLabel = labelObject.GetComponent<Text>();
            frameRateLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            frameRateLabel.fontSize = 22;
            frameRateLabel.alignment = TextAnchor.UpperRight;
            frameRateLabel.color = new Color(0.7f, 1f, 0.85f, 0.9f);
            frameRateLabel.raycastTarget = false;
            frameRateLabel.text = "FPS --";
        }

        private void UpdateFrameRateDisplay()
        {
            if (frameRateLabel == null || Time.unscaledDeltaTime <= 0f)
                return;

            float instantaneousFrameRate = 1f / Time.unscaledDeltaTime;
            float smoothing = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            smoothedFrameRate = Mathf.Lerp(smoothedFrameRate, instantaneousFrameRate, smoothing);
            frameRateLabel.text = $"FPS {Mathf.RoundToInt(smoothedFrameRate)}";
        }

        private string HelpText()
        {
            string common = "\nP  Return to default rotation\nM  Return to main menu\nH  Hide / show this help";
            if (app is Mandelbox mandelbox)
            {
                bool isTrackpadScale = mandelbox.controlsHelper != null && mandelbox.controlsHelper.trackpadScaleMode;
                string modeStr = isTrackpadScale ? "<color=#00FF99>Scale Mode</color>" : "Orbit Mode";
                string loopStr = mandelboxAutoLoop ? "<color=#00FF99>Active</color>" : "Off";
                string microphoneStr = microphoneScaleMode ? "<color=#00FF99>Active</color>" : "Off";
                return $"<b>MANDELBOX CONTROLS</b>\n\n" +
                       $"Scale: <b>{mandelbox.scale:F2}</b>\n" +
                       $"L  Scale Loop: <b>{loopStr}</b>\n" +
                       $"U  Microphone Scale: <b>{microphoneStr}</b>\n" +
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
