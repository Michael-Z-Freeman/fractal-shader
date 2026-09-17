using UnityEngine;
using UnityEngine.EventSystems;

namespace FractalShader
{
	/// <summary>
	/// Handles continuous scrolling logic when a UI button is held down.
	/// Implements IPointer interfaces to detect click-and-hold states.
	/// </summary>
	public class ScrollButtons : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
	{
		[Header("References")]
		[Tooltip("The Scroll manager script that controls the ScrollRect.")]
		public Scroll scrollManager;

		[Header("Settings")]
		[Tooltip("If true, this button scrolls up. If false, it scrolls down.")]
		public bool isUpButton;

		private bool isPressed;

		private void Update()
		{
			// If the user is holding the button down, trigger the scroll logic every frame
			if (isPressed && scrollManager != null)
			{
				scrollManager.ButtonScroll(isUpButton);
			}
		}

		/// <summary>
		/// Detects when the user starts clicking/touching the button.
		/// </summary>
		public void OnPointerDown(PointerEventData eventData)
		{
			isPressed = true;
		}

		/// <summary>
		/// Detects when the user releases the button or moves the pointer away.
		/// </summary>
		public void OnPointerUp(PointerEventData eventData)
		{
			isPressed = false;
		}

		private void OnDisable()
		{
			// Safety: Ensure the button doesn't get "stuck" in a pressed state if the UI is hidden
			isPressed = false;
		}
	}
}