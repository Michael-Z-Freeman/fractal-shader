using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FractalShader
{
	/// <summary>
	/// UI component that manages a Boolean (On/Off) option.
	/// Expected to be linked to Toggle events (OnValueChanged) via the prefab.
	/// </summary>
	public class O_Bool : MonoBehaviour
	{
		[Header("Core References")]
		[Tooltip("The main App reference of the system.")]
		public App app;

		[Tooltip("The key name under which this Boolean is registered in the OptionsManager.")]
		public string optionName;

		[Header("UI References (Do not change)")]
		[Tooltip("Reference to the Toggle within the prefab.")]
		public Toggle toggle;

		[Tooltip("The GameObject to activate when the state is On (True).")]
		public GameObject on;

		[Tooltip("The GameObject to activate when the state is Off (False).")]
		public GameObject off;

		[Tooltip("Optional: The Text component displaying the name of this option. If left empty, it will be searched in the first child object.")]
		[SerializeField] private TextMeshProUGUI titleText;

		private bool isReady;

		private void Start()
		{
			// Safety check
			if (app == null || app.optionsManager == null)
			{
				Debug.LogError($"[O_Bool] Missing App or OptionsManager reference on '{gameObject.name}'!", this);
				return;
			}

			bool initialValue = (bool)app.optionsManager.GetOption(optionName);

			// Fallback: If title text is not assigned, try to find it in the first child
			if (titleText == null && transform.childCount > 0)
			{
				titleText = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
			}

			if (titleText != null)
			{
				titleText.text = gameObject.name;
			}

			// Set initial state (Using SetIsOnWithoutNotify prevents triggering the OnValueChanged event)
			if (toggle != null)
			{
				toggle.SetIsOnWithoutNotify(initialValue);
			}

			UpdateVisualStates(initialValue);

			isReady = true;
		}

		/// <summary>
		/// Using Update instead of OnGUI provides a massive performance boost.
		/// The UI is updated only when the value actually changes.
		/// </summary>
		private void Update()
		{
			if (!isReady || toggle == null) return;

			bool currentValue = (bool)app.optionsManager.GetOption(optionName);

			// If the system value differs from the UI Toggle value (e.g., changed by another script), sync the UI
			if (toggle.isOn != currentValue)
			{
				// CAUTION: Using "toggle.isOn = currentValue" would trigger OnValueChanged and cause an infinite loop.
				// SetIsOnWithoutNotify strictly updates the visual state without firing the event.
				toggle.SetIsOnWithoutNotify(currentValue);
				UpdateVisualStates(currentValue);
			}
		}

		/// <summary>
		/// Called via Unity Event when the Toggle value is changed by the user.
		/// </summary>
		public void OnValueChanged(bool val)
		{
			if (isReady)
			{
				app.optionsManager.SetOption(optionName, val);
				UpdateVisualStates(val);
			}
		}

		#region Helper Methods

		/// <summary>
		/// Updates the visual GameObjects for the On/Off states.
		/// </summary>
		private void UpdateVisualStates(bool state)
		{
			// Only call SetActive if the objects are assigned and their current state differs from the target state.
			// This prevents redundant SetActive calls every frame, protecting performance.
			if (on != null && on.activeSelf != state) on.SetActive(state);
			if (off != null && off.activeSelf != !state) off.SetActive(!state);
		}

		#endregion
	}
}