using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FractalShader
{
	/// <summary>
	/// UI component that manages a single Float value option.
	/// Expected to be linked to InputField events via the prefab.
	/// </summary>
	public class O_Single : MonoBehaviour, IPointerClickHandler
	{
		[Header("Core References")]
		[Tooltip("The main App reference of the system.")]
		public App app;

		[Tooltip("The key name under which this float value is registered in the OptionsManager.")]
		public string optionName;

		[Header("Animation Settings")]
		public bool animate;
		public float animationSpeed;
		public float min;
		public float max;
		public bool oscillating;

		[Header("UI References (Do not change)")]
		[Tooltip("Reference to the Input Field within the prefab.")]
		public TMP_InputField inputField;

		[Tooltip("Optional: The Text component displaying the name of this option. If left empty, it will be searched in the first child object.")]
		[SerializeField] private TextMeshProUGUI titleText;

		// State variables
		private bool isTyping;
		private bool isReady;
		private bool isAnimating;

		private void Start()
		{
			if (app == null || app.optionsManager == null)
			{
				Debug.LogError($"[O_Single] Missing App or OptionsManager reference on '{gameObject.name}'!", this);
				return;
			}

			// Fetch initial value and update UI
			float initialValue = (float)app.optionsManager.GetOption(optionName);
			UpdateInputField(initialValue);

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
			if (inputField != null)
			{
				inputField.textComponent.alignment = TextAlignmentOptions.MidlineRight;
			}

			isReady = true;
		}

		/// <summary>
		/// Using Update instead of OnGUI significantly improves performance and reduces GC allocation.
		/// </summary>
		private void Update()
		{
			if (!isTyping && isReady && inputField != null)
			{
				float currentValue = (float)app.optionsManager.GetOption(optionName);
				UpdateInputField(currentValue);
			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (animate && eventData.button == PointerEventData.InputButton.Right)
			{
				if (isAnimating)
				{
					app.optionsManager.StopAnimation(optionName);
					if (inputField != null) inputField.interactable = true;
				}
				else
				{
					app.optionsManager.StartAnimation(optionName, animationSpeed, min, max, oscillating);
					if (inputField != null) inputField.interactable = false;
				}
				isAnimating = !isAnimating;
			}
		}

		/// <summary>
		/// Called via Unity Event when editing the InputField ends.
		/// </summary>
		public void OnEndEdit(string val)
		{
			if (isReady)
			{
				// Safe parsing: Prevents crashes if the user enters letters or leaves it blank.
				float safeValue = ParseSafe(val);
				app.optionsManager.SetOption(optionName, safeValue);
			}
		}

		/// <summary>
		/// Called via Unity Event when the InputField is selected.
		/// </summary>
		public void OnSelect()
		{
			isTyping = true;
		}

		/// <summary>
		/// Called via Unity Event when the InputField is deselected.
		/// </summary>
		public void OnDeselect()
		{
			isTyping = false;
		}

		#region Helper Methods

		/// <summary>
		/// Updates the UI only if the value has changed. Prevents unnecessary string allocations.
		/// </summary>
		private void UpdateInputField(float value)
		{
			string strValue = value.ToString();
			if (inputField.text != strValue)
			{
				inputField.text = strValue;
			}
		}

		/// <summary>
		/// Safely parses a string to a float. Returns 0 if parsing fails.
		/// </summary>
		private float ParseSafe(string input)
		{
			if (float.TryParse(input, out float result))
			{
				return result;
			}
			return 0f;
		}

		#endregion
	}
}