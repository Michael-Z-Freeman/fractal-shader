using FractalShader;
using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Manages the parameters for a 2D generalized Sierpinski Carpet.
	/// Bridges the UI inputs to the Compute Shader via the URP Render Feature.
	/// </summary>
	public class Sierpinski : App
	{
		public static Sierpinski Instance;

		[Header("Fractal Configuration")]
		[Tooltip("The grid division factor. A value of 3.0 creates a standard carpet.")]
		public float divisionFactor = 3.0f;

		[Tooltip("The relative center position of the cutout within each grid cell.")]
		public float cutoutPosition = 1.5f;

		[Tooltip("The relative size of the hole. Values near 0.5 are standard.")]
		public float holeSize = 0.5f;

		[Tooltip("The recursion depth. Higher values increase detail but cost performance.")]
		[Range(1, 12)] public int iterations = 5;

		[Header("Visual Settings")]
		public Color fractalColor = new Color(0.8f, 0.2f, 0.2f, 1.0f);

		protected override void Awake()
		{
			base.Awake();
			Instance = this;

			// 2D fractals usually don't require camera movement
			cameraType = CameraType.None;
		}

		// Rendering is handled by the URP Feature, so we leave this empty
		protected override void Render(RenderTexture target) { }

		#region Options Manager Bridge (O_ Prefix)

		public float O_Division { get => divisionFactor; set { divisionFactor = value; ReRender(); } }
		public float O_Position { get => cutoutPosition; set { cutoutPosition = value; ReRender(); } }
		public float O_Size { get => holeSize; set { holeSize = value; ReRender(); } }
		public float O_Iterations { get => iterations; set { iterations = Mathf.RoundToInt(value); ReRender(); } }

		#endregion
	}
}