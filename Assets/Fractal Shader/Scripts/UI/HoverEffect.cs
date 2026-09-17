using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FractalShader
{
	/// <summary>
	/// Smoothly transitions a UI Graphic's color when the mouse enters or exits its area.
	/// Works with Buttons, Images, and Text components.
	/// </summary>
	public class HoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		[Header("Target")]
		[Tooltip("The UI component (Image, Text, etc.) to apply the color effect to.")]
		public Graphic targetGraphic;

		[Header("Colors")]
		[Tooltip("The default color of the element.")]
		public Color normalColor = Color.white;

		[Tooltip("The color when the mouse is hovering over the element.")]
		public Color hoverColor = Color.gray;

		[Header("Settings")]
		[Tooltip("How long the transition should take in seconds.")]
		public float fadeDuration = 0.15f;

		private Coroutine fadeRoutine;

		private void Start()
		{
			// Fallback: If no target is assigned, try to find one on this object
			if (targetGraphic == null)
			{
				targetGraphic = GetComponent<Graphic>();
			}

			// Ensure we start with the normal color
			if (targetGraphic != null)
			{
				targetGraphic.color = normalColor;
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			StartFade(targetGraphic.color, hoverColor);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			StartFade(targetGraphic.color, normalColor);
		}

		private void StartFade(Color from, Color to)
		{
			// Stop existing fade to prevent color flickering or logic conflicts
			if (fadeRoutine != null)
			{
				StopCoroutine(fadeRoutine);
			}

			fadeRoutine = StartCoroutine(FadeRoutine(from, to));
		}

		private IEnumerator FadeRoutine(Color startColor, Color endColor)
		{
			float elapsed = 0f;

			while (elapsed < fadeDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(elapsed / fadeDuration);

				if (targetGraphic != null)
				{
					targetGraphic.color = Color.Lerp(startColor, endColor, t);
				}

				yield return null; // Wait for next frame
			}

			// Finalize color at the end
			if (targetGraphic != null)
			{
				targetGraphic.color = endColor;
			}

			fadeRoutine = null;
		}
	}
}