using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

namespace FractalShader
{
	/// <summary>
	/// Main application class for rendering 2D Mandelbrot and Julia fractals.
	/// Handles zooming, panning, and coordinate translation for the complex plane.
	/// </summary>
	public class Mandelbrot : App
	{
		// Singleton instance for global access
		public static Mandelbrot Instance { get; private set; }

		[Header("--- CAMERA AND VIEW AREA ---")]
		[Tooltip("Current magnification level.")]
		public float zoom = 1f;

		[Tooltip("The iterations limit for escape-time calculation.")]
		public int iterations = 100;

		[Tooltip("Toggle between Mandelbrot and Julia set rendering.")]
		public bool julia;

		[Tooltip("The constant 'C' used specifically for Julia set rendering.")]
		public Vector2 c;

		[Tooltip("The calculated viewport area in the complex plane (X, Y, Width, Height).")]
		public Vector4 area;

		[Header("--- VISUALS ---")]
		[Tooltip("Color used for points inside the set to prevent ghosting artifacts.")]
		public Color backgroundColor = Color.black;

		[Tooltip("Brightness multiplier for the escape-time gradient colors.")]
		[Range(0.1f, 5f)] public float brightness = 1.5f;

		private Vector2 center = new Vector2(-0.5f, 0f);
		private Vector3[] colorGradient;
		private TextMeshProUGUI coordinateText;
		public ComputeBuffer colorBuffer;

		/// <summary>
		/// Generates a random color vector for the procedural gradient.
		/// </summary>
		private static Vector4 GetRandomColor(float alpha)
		{
			return new Vector4(Random.Range(0, 200), Random.Range(0, 200), Random.Range(0, 200), alpha);
		}

		private void Start()
		{
			// Standard Singleton pattern setup with safety check
			if (Instance != null && Instance != this)
			{
				Destroy(this);
				return;
			}
			Instance = this;

			// Generate the procedural color gradient for rendering
			colorGradient = Gradients.GeneratePalette(new[] {
				new Vector4(255, 255, 255, 0f),
				GetRandomColor(0.5f),
				GetRandomColor(1f)
			}, 100);

			// Initialize GPU buffer for colors
			colorBuffer = new ComputeBuffer(colorGradient.Length, sizeof(float) * 3);
			colorBuffer.SetData(colorGradient);

			// Dynamically find the coordinate UI text if available
			if (canvas != null && canvas.transform.childCount > 3)
			{
				coordinateText = canvas.transform.GetChild(3).GetComponent<TextMeshProUGUI>();
			}
		}

		private void OnDisable()
		{
			// Release the compute buffer to prevent memory leaks
			if (colorBuffer != null)
			{
				colorBuffer.Release();
				colorBuffer = null;
			}
		}

		protected override void Render(RenderTexture targetTexture)
		{
			// The compute shader dispatch is handled via a dedicated RenderPass/Feature
			CalculateViewportArea();
		}

		/// <summary>
		/// Recalculates the complex plane boundaries based on zoom and screen aspect ratio.
		/// </summary>
		public void CalculateViewportArea()
		{
			float width = 4f / Mathf.Pow(1.1f, zoom);
			float height = 4f / Mathf.Pow(1.1f, zoom);
			float aspectRatio = (float)screenWidth / screenHeight;

			// Adjust width or height to maintain a consistent aspect ratio in the complex plane
			if (width / height < aspectRatio)
				width = height * aspectRatio;
			else if (width / height > aspectRatio)
				height = width / aspectRatio;

			float x = center.x - width / 2f;
			float y = -center.y + height / 2f;
			area = new Vector4(x, y, width, height);
		}

		/// <summary>
		/// Converts screen pixel coordinates to complex plane coordinates.
		/// </summary>
		private Vector2 ScreenToComplex(Vector2 screenPosition)
		{
			return new Vector2(
				screenPosition.x / (screenWidth / area.z) + area.x,
				screenPosition.y / (screenHeight / area.w) - area.y
			);
		}



		protected override void Update()
		{
			// Update FPS counter from base class
			base.Update();

			CalculateViewportArea();

			// Map mouse position to complex plane for UI feedback
			Vector2 mouseComplexPos = ScreenToComplex(Input.mousePosition);
			if (coordinateText != null)
			{
				coordinateText.text = $"C ( {mouseComplexPos.x:0.0000000} / {mouseComplexPos.y:0.0000000}i )";
			}

			// Handle Zooming
			if (Mathf.Abs(controlsHelper.zoomDelta) > 0)
			{
				zoom += controlsHelper.zoomDelta * RequestSmoothDeltaTime();
				if (zoom < 1) zoom = 1f;

				// Pivot zoom around the mouse position
				center = mouseComplexPos;
				CalculateViewportArea();

				Vector2 compensation = center - ScreenToComplex(Input.mousePosition);
				center += compensation;
			}

			// Handle Panning (Dragging)
			if (controlsHelper.isDragging)
			{
				center += ScreenToComplex(controlsHelper.dragStartPosition) - ScreenToComplex(Input.mousePosition);
				controlsHelper.dragStartPosition = Input.mousePosition;
			}

			// Handle Julia Set Switching
			if (controlsHelper.isRightClickActive)
			{
				controlsHelper.isRightClickActive = false;
				if (!EventSystem.current.IsPointerOverGameObject())
				{
					c = mouseComplexPos;
					julia = !julia;
				}
			}
		}

		#region UI Option Properties

		// Bridge properties for the OptionsManager and UI Controllers
		public float O_Zoom { get => zoom; set => zoom = value; }
		public float O_Iterations { get => iterations; set => iterations = Mathf.RoundToInt(value); }
		public bool O_Julia { get => julia; set => julia = value; }
		public Vector2 O_C { get => c; set => c = value; }

		#endregion
	}
}