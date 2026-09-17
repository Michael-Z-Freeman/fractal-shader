using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FractalShader
{
	/// <summary>
	/// Manages the main menu, application listing, and scene transitions.
	/// Loads fractal metadata from JSON and generates the UI list dynamically.
	/// </summary>
	public class AppManager : MonoBehaviour
	{
		[Header("UI References")]
		public GameObject contentPanel;
		public GameObject listPanel;

		[Header("Internal UI Elements")]
		private GameObject infoSection;
		private GameObject loadButton;
		private TextMeshProUGUI titleText;
		private TextMeshProUGUI descriptionText;
		private TextMeshProUGUI typeText;
		private TextMeshProUGUI sizeText;
		private TextMeshProUGUI dateText;

		private List<AppData> apps = new List<AppData>();
		private int selectedIndex;

		/// <summary>
		/// Property to handle selection changes and trigger UI updates.
		/// </summary>
		public int SelectedIndex
		{
			get => selectedIndex;
			set
			{
				selectedIndex = value;
				UpdateUIContent();
			}
		}
 

		public void Start()
		{
			// --- Component Initialization ---
			// Using hierarchical indexing to find the UI elements based on your project structure
			infoSection = contentPanel.transform.GetChild(3).gameObject;
			loadButton = contentPanel.transform.GetChild(4).gameObject;

			Transform textContainer = contentPanel.transform.GetChild(2);
			titleText = textContainer.GetChild(0).GetComponent<TextMeshProUGUI>();
			descriptionText = textContainer.GetChild(1).GetComponent<TextMeshProUGUI>();

			Transform infoGrid = infoSection.transform.GetChild(1);
			typeText = infoGrid.GetChild(0).GetComponent<TextMeshProUGUI>();
			sizeText = infoGrid.GetChild(1).GetComponent<TextMeshProUGUI>();
			dateText = infoGrid.GetChild(2).GetComponent<TextMeshProUGUI>();

			// --- Data Loading ---
			// Loads "Apps.json" from the Resources folder
			TextAsset appsJson = Resources.Load<TextAsset>("Apps");
			if (appsJson != null)
			{
				apps = JsonConvert.DeserializeObject<List<AppData>>(appsJson.text);
			}

			// Populate the scrollable list (skipping index 0 if it's reserved for the Home/Intro screen)
			for (int i = 1; i < apps.Count; i++)
			{
				CreateMenuElement(i);
			}

			// --- Layout Calculation ---
			// Dynamically scales the list container based on the number of items
			int itemHeight = 24;
			int spacing = 50;
			int totalHeight = (apps.Count * itemHeight) + ((apps.Count - 1) * spacing);

			RectTransform elementContainer = listPanel.transform.GetChild(1).GetChild(0).GetComponent<RectTransform>();
			elementContainer.sizeDelta = new Vector2(0, totalHeight);
			elementContainer.anchoredPosition = new Vector2(0, totalHeight / -2.0f);

			// Set initial state
			UpdateUIContent();
		}

		/// <summary>
		/// Instantiates a clickable link in the side menu for a specific app.
		/// </summary>
		private void CreateMenuElement(int index)
		{
			GameObject linkPrefab = Resources.Load("Link") as GameObject;
			Transform container = listPanel.transform.GetChild(1).GetChild(0);

			GameObject element = Instantiate(linkPrefab, container);
			element.name = apps[index].Name;

			element.GetComponent<TextMeshProUGUI>().text = "// " + apps[index].Name;

			Button btn = element.GetComponent<Button>();
			btn.transition = Selectable.Transition.None;
			btn.onClick.AddListener(() => { SelectedIndex = index; });
		}

		/// <summary>
		/// Updates the main display area with the details of the selected fractal.
		/// </summary>
		private void UpdateUIContent()
		{
			if (apps.Count == 0) return;

			AppData app = apps[SelectedIndex];
			titleText.text = app.Name;
			descriptionText.text = app.Description;

			// Handle Home/Index 0 special case (usually hiding metadata and load buttons)
			if (SelectedIndex == 0)
			{
				infoSection.SetActive(false);
				loadButton.SetActive(false);
				return;
			}

			infoSection.SetActive(true);
			loadButton.SetActive(true);

			// Format technical details
			typeText.text = app.Is3D ? "3D" : "2D";
			sizeText.text = (Math.Round(app.FileSize / 10.0) / 100.0).ToString("0.00") + " KB";
			dateText.text = app.ReleaseDate;
		}

 
		/// <summary>
		/// Data structure for Fractal metadata. 
		/// Matches the format of the JSON file in the Resources folder.
		/// </summary>
		[Serializable]
		private class AppData
		{
			public string Name { get; set; }
			public string Description { get; set; }
			public bool Is3D { get; set; }
			public short FileSize { get; set; }
			public string ReleaseDate { get; set; }

			public AppData(string name, bool is3d, short fileSize, string releaseDate, string description)
			{
				Name = name;
				Description = description;
				Is3D = is3d;
				FileSize = fileSize;
				ReleaseDate = releaseDate;
			}
		}

		/// <summary>
		/// Closes the application.
		/// </summary>
		public void Quit()
		{
			Application.Quit();
		}
	}
}