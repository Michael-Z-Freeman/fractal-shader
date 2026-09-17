using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FractalShader
{
	/// <summary>
	/// UI component that manages Vector3 options.
	/// Expected to be linked to InputField events (OnSelect, OnDeselect, OnEndEdit) via the prefab.
	/// </summary>
	public class O_Vec3 : MonoBehaviour, IPointerClickHandler
	{
		[Header("Core References")]
		[Tooltip("The main App reference of the system.")]
		public App app;

		[Tooltip("The key name under which this Vector3 is registered in the OptionsManager.")]
		public string optionName;

		[Header("Animation Settings")]
		public bool animate;
		public Vector3 animationSpeed;
		public Vector3 min;
		public Vector3 max;
		public bool oscillating;

		[Header("UI References (Do not change)")]
		[Tooltip("References to the Input Fields within the prefab.")]
		public TMP_InputField inputFieldA;
		public TMP_InputField inputFieldB;
		public TMP_InputField inputFieldC;

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
				Debug.LogError($"[O_Vec3] Missing App or OptionsManager reference on '{gameObject.name}'!", this);
				return;
			}

			// Fetch the initial value and update the UI
			Vector3 value = (Vector3)app.optionsManager.GetOption(optionName);
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
			inputFieldA.textComponent.alignment = TextAlignmentOptions.MidlineRight;
			inputFieldB.textComponent.alignment = TextAlignmentOptions.MidlineRight;
			inputFieldC.textComponent.alignment = TextAlignmentOptions.MidlineRight;

			isReady = true;
		}

		/// <summary>
		/// Using Update instead of OnGUI provides a massive performance boost and prevents heavy Garbage Collection.
		/// The UI is updated only when the user is not typing and the system is ready.
		/// </summary>
		private void Update()
		{
			if (!isTyping && isReady)
			{
				Vector3 value = (Vector3)app.optionsManager.GetOption(optionName);
				UpdateInputFields(value);
			}
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
		/// Called via Unity Event when editing the InputFields ends.
		/// </summary>
		public void OnEndEdit()
		{
			if (!isReady) return;

			// Safe parsing: Prevents the script (and the game) from crashing if the user enters letters or leaves it blank.
			float x = ParseSafe(inputFieldA.text);
			float y = ParseSafe(inputFieldB.text);
			float z = ParseSafe(inputFieldC.text);

			app.optionsManager.SetOption(optionName, new Vector3(x, y, z));
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
		private void UpdateInputFields(Vector3 value)
		{
			// Checking if the value has changed before assigning prevents redundant UI rendering and Garbage Collection.
			if (inputFieldA.text != value.x.ToString()) inputFieldA.text = value.x.ToString();
			if (inputFieldB.text != value.y.ToString()) inputFieldB.text = value.y.ToString();
			if (inputFieldC.text != value.z.ToString()) inputFieldC.text = value.z.ToString();
		}

		private void SetInputFieldsInteractable(bool state)
		{
			inputFieldA.interactable = state;
			inputFieldB.interactable = state;
			inputFieldC.interactable = state;
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