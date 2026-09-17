using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FractalShader
{
	public class O_Slider : MonoBehaviour, IPointerClickHandler
	{
		[Header("Core References")]
		public App app;
		public string optionName;

		[Header("Slider Settings")]
		public float min;
		public float max;
		public bool wholeNumbers;

		[Header("Animation Settings")]
		public bool animate;
		public float animationSpeed;
		public bool oscillating;

		[Header("UI References")]
		public Slider slider;
		public TMP_InputField inputField;
		[SerializeField] private TextMeshProUGUI titleText;

		private bool isTyping;
		private bool isReady;
		private bool isAnimating;

		// Start metodunu IEnumerator yaparak 0.2 saniye gecikme ekledik
		private IEnumerator Start()
		{
			// 0.2 saniye bekle: Diğer scriptlerin (App, Sierpinski vb.) Awake işlemlerini bitirmesi için
			yield return new WaitForSeconds(0.2f);

			// 1. Güvenlik Kontrolü: App referansı var mı?
			if (app == null) app = Object.FindFirstObjectByType<App>();

			if (app == null || app.optionsManager == null)
			{
				Debug.LogError($"[O_Slider] '{gameObject.name}' üzerinde App veya OptionsManager referansı eksik!", this);
				yield break;
			}

			// 2. Güvenlik Kontrolü: OptionsManager bir hedefi (target) dinliyor mu?
			// Hatanın asıl kaynağı burasıydı.
			if (app.optionsManager.target == null)
			{
				Debug.LogWarning($"[O_Slider] '{gameObject.name}' için hedef henüz atanmamış. Bekleniyor...");
				yield return new WaitForSeconds(0.1f);
			}

			// Slider ayarlarını yap
			if (slider != null)
			{
				slider.maxValue = max;
				slider.minValue = min;
				slider.wholeNumbers = wholeNumbers;
			}

			// İlk değeri alırken hata almamak için kontrol ekliyoruz
			try
			{
				float initialValue = (float)app.optionsManager.GetOption(optionName);
				UpdateVisuals(initialValue);
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[O_Slider] '{optionName}' değeri '{app.optionsManager.target.name}' scriptinde bulunamadı! Hata: {e.Message}");
			}

			// Başlık yazısını ayarla
			if (titleText == null && transform.childCount > 0)
				titleText = transform.GetChild(0).GetComponent<TextMeshProUGUI>();

			if (titleText != null) titleText.text = gameObject.name;

			if (inputField != null)
				inputField.textComponent.alignment = TextAlignmentOptions.MidlineRight;

			isReady = true;
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (animate && eventData.button == PointerEventData.InputButton.Right)
			{
				if (isAnimating)
				{
					app.optionsManager.StopAnimation(optionName);
					SetInteractableState(true);
				}
				else
				{
					app.optionsManager.StartAnimation(optionName, animationSpeed, min, max, oscillating);
					SetInteractableState(false);
				}
				isAnimating = !isAnimating;
			}
		}

		private void Update()
		{
			if (!isReady || isTyping) return;

			// Animasyon sırasında slider'ın güncellenmesi için
			float currentValue = (float)app.optionsManager.GetOption(optionName);
			if (slider != null && !Mathf.Approximately(slider.value, currentValue))
			{
				UpdateVisuals(currentValue);
			}
		}

		public void OnValueChanged(float val)
		{
			if (isReady)
			{
				app.optionsManager.SetOption(optionName, val);
				if (!isTyping && inputField != null) UpdateInputFieldText(val);
			}
		}

		public void OnEndEdit(string val)
		{
			if (isReady && slider != null)
			{
				float safeValue = ParseSafe(val);
				safeValue = Mathf.Clamp(safeValue, slider.minValue, slider.maxValue);
				app.optionsManager.SetOption(optionName, safeValue);
				UpdateVisuals(safeValue);
			}
		}

		public void OnSelect() => isTyping = true;
		public void OnDeselect() => isTyping = false;

		#region Helpers
		private void UpdateVisuals(float value)
		{
			if (slider != null) slider.SetValueWithoutNotify(value);
			if (!isTyping) UpdateInputFieldText(value);
		}

		private void UpdateInputFieldText(float value)
		{
			if (inputField != null)
			{
				string strValue = value.ToString("0.###");
				if (inputField.text != strValue) inputField.text = strValue;
			}
		}

		private void SetInteractableState(bool state)
		{
			if (slider != null) slider.interactable = state;
			if (inputField != null) inputField.interactable = state;
		}

		private float ParseSafe(string input)
		{
			return float.TryParse(input, out float result) ? result : (slider != null ? slider.minValue : 0f);
		}
		#endregion
	}
}