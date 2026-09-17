using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FractalShader
{
	/// <summary>
	/// Manages the visibility and interactivity of scroll indicator buttons (Up/Down).
	/// Automatically disables buttons and fades their color when the scroll limit is reached.
	/// </summary>
	public class Scroll : MonoBehaviour
	{
		[Header("Controls")]
		public GameObject upButton;
		public GameObject downButton;
		public ScrollRect scrollRect;

		[Header("Visuals")]
		[Tooltip("The color used when a button is at the scroll limit and disabled.")]
		public Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

		[Tooltip("Duration of the color transition.")]
		public float fadeDuration = 0.2f;

		private Graphic upGraphic, downGraphic;
		private HoverEffect upHover, downHover;
		private Color upNormalColor, downNormalColor;

		private void Start()
		{
			// Initialize components
			upGraphic = upButton.GetComponent<Graphic>();
			downGraphic = downButton.GetComponent<Graphic>();

			upHover = upButton.GetComponent<HoverEffect>();
			downHover = downButton.GetComponent<HoverEffect>();

			upNormalColor = upGraphic.color;
			downNormalColor = downGraphic.color;

			// PERFORMANCE OPTIMIZATION: Use events instead of Update()
			scrollRect.onValueChanged.AddListener(OnScrollValueChanged);

			// Initial check
			UpdateVisuals(scrollRect.verticalNormalizedPosition);
		}

		private void OnScrollValueChanged(Vector2 value)
		{
			UpdateVisuals(value.y);
		}

		private void UpdateVisuals(float verticalPos)
		{
			// Check if we are at the top (Value ~ 1.0)
			bool isAtTop = Math.Abs(verticalPos - 1f) < 0.05f;
			// Check if we are at the bottom (Value ~ 0.0)
			bool isAtBottom = Math.Abs(verticalPos) < 0.05f;

			// Handle Up Button
			if (isAtTop)
			{
				SetButtonState(upHover, upGraphic, upNormalColor, disabledColor, false);
			}
			else
			{
				SetButtonState(upHover, upGraphic, disabledColor, upNormalColor, true);
			}

			// Handle Down Button
			if (isAtBottom)
			{
				SetButtonState(downHover, downGraphic, downNormalColor, disabledColor, false);
			}
			else
			{
				SetButtonState(downHover, downGraphic, disabledColor, downNormalColor, true);
			}
		}

		private void SetButtonState(HoverEffect hover, Graphic graphic, Color from, Color to, bool enabled)
		{
			if (hover.enabled == enabled) return; // Prevent redundant calls

			hover.enabled = enabled;
			StopCoroutine("FadeRoutine"); // Stop previous transitions on this specific graphic
			StartCoroutine(FadeRoutine(graphic, from, to));
		}

		/// <summary>
		/// Logic for UI buttons to scroll the list programmatically.
		/// </summary>
		/// <param name="isUp">True to scroll up, False to scroll down.</param>
		public void ButtonScroll(bool isUp)
		{
			float speed = 5f * Time.deltaTime;
			scrollRect.verticalNormalizedPosition += isUp ? speed : -speed;
		}

		private IEnumerator FadeRoutine(Graphic g, Color start, Color end)
		{
			float elapsed = 0f;
			while (elapsed < fadeDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				g.color = Color.Lerp(start, end, elapsed / fadeDuration);
				yield return null;
			}
			g.color = end;
		}

		private void OnDestroy()
		{
			// Clean up listener to prevent memory leaks
			if (scrollRect != null)
				scrollRect.onValueChanged.RemoveListener(OnScrollValueChanged);
		}
	}
}