using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FractalShader
{
	/// <summary>
	/// UI component that manages Vector2 options.
	/// Expected to be linked to InputField events (OnSelect, OnDeselect, OnEndEdit) via the prefab.
	/// </summary>
	public class O_Vec2 : MonoBehaviour, IPointerClickHandler
	{
		[Header("Core References")]
		[Tooltip("The main App reference of the system.")]
		public App app;

		[Tooltip("The key name under which this Vector2 is registered in the OptionsManager.")]
		public string optionName;

		[Header("Animation Settings")]
		public bool animate;
		public Vector2 animationSpeed;
		public Vector2 min;
		public Vector2 max;
		public bool oscillating;

		[Header("UI References (Do not change)")]
		[Tooltip("References to the Input Fields within the prefab.")]
		public TMP_InputField inputFieldA;
		public TMP_InputField inputFieldB;

		[Tooltip("Optional: The Text component displaying the name of this option. If left empty, it will be searched in the first child object.")]
		[SerializeField] private TextMeshProUGUI titleText;

		// State variables (Keep private, no need for external access)
		private bool isTyping;
		private bool isReady;
		private bool isAnimating;

		private void Start()
		{
			if (app == null || app.optionsManager == null)
			{
				Debug.LogError($"[O_Vec2] Missing App or OptionsManager reference on '{gameObject.name}'!", this);
				return;
			}

			// Fetch the initial value and update the UI
			Vector2 value = (Vector2)app.optionsManager.GetOption(optionName);
			UpdateInputFields(value);

			// Fallback: If title text is not assigned, try to find it in the first child
			if (titleText == null && transform.childCount > 0)
			{
				titleText = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
			}

			if (titleText != null)
			{
				titleText.text = gameObject.name;
			}

			// Design settings
			if (inputFieldA != null) inputFieldA.textComponent.alignment = TextAlignmentOptions.MidlineRight;
			if (inputFieldB != null) inputFieldB.textComponent.alignment = TextAlignmentOptions.MidlineRight;

			isReady = true;
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (animate && eventData.button == PointerEventData.InputButton.Right)
			{
				if (isAnimating)
				{
					app.optionsManager.StopAnimation(optionName);
					SetInputFieldsInteractable(true);
				}
				else
				{
					app.optionsManager.StartAnimation(optionName, animationSpeed, min, max, oscillating);
					SetInputFieldsInteractable(false);
				}
				isAnimating = !isAnimating;
			}
		}

		/// <summary>
		/// Using Update instead of OnGUI provides a massive performance boost and prevents heavy Garbage Collection.
		/// The UI is updated only when the user is not typing and the system is ready.
		/// </summary>
		private void Update()
		{
			if (!isTyping && isReady)
			{
				Vector2 value = (Vector2)app.optionsManager.GetOption(optionName);
				UpdateInputFields(value);
			}
		}

		/// <summary>
		/// Called via Unity Event when editing the InputFields ends.
		/// </summary>
		public void OnEndEdit()
		{
			if (!isReady) return;

			// Safe parsing: Prevents the script (and the game) from crashing if the user enters letters or leaves it blank.
			float x = ParseSafe(inputFieldA != null ? inputFieldA.text : "0");
			float y = ParseSafe(inputFieldB != null ? inputFieldB.text : "0");

			app.optionsManager.SetOption(optionName, new Vector2(x, y));
		}

		/// <summary>
		/// Called via Unity Event when an InputField is selected.
		/// </summary>
		public void OnSelect()
		{
			isTyping = true;
		}

		/// <summary>
		/// Called via Unity Event when an InputField is deselected.
		/// </summary>
		public void OnDeselect()
		{
			isTyping = false;
		}

		#region Helper Methods

		// Helper functions to prevent code duplication
		private void UpdateInputFields(Vector2 value)
		{
			// Checking if the value has changed before assigning prevents redundant UI rendering and Garbage Collection.
			if (inputFieldA != null && inputFieldA.text != value.x.ToString()) inputFieldA.text = value.x.ToString();
			if (inputFieldB != null && inputFieldB.text != value.y.ToString()) inputFieldB.text = value.y.ToString();
		}

		private void SetInputFieldsInteractable(bool state)
		{
			if (inputFieldA != null) inputFieldA.interactable = state;
			if (inputFieldB != null) inputFieldB.interactable = state;
		}

		private float ParseSafe(string input)
		{
			// TryParse returns 0 and prevents exceptions if the text cannot be converted to a number.
			if (float.TryParse(input, out float result))
			{
				return result;
			}
			return 0f;
		}

		#endregion
	}
}