using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Application class for rendering an Octahedron Flake (Sierpinski Octahedron) fractal.
	/// Inherits from the base App class to communicate with the Compute Shader via URP.
	/// </summary>
	public class OctahedronFlake : App
	{
		// Singleton instance for global access
		public static OctahedronFlake Instance { get; private set; }

		[Header("Fractal Math")]
		[Tooltip("The recursion depth of the fractal. High values increase detail but lower performance.")]
		[Range(0, 15)] public int iterations = 5;

		[Tooltip("The base size of the octahedron structure.")]
		[Range(0.1f, 5f)] public float size = 1f;

		[Tooltip("The scaling factor applied to the fractal in each iteration.")]
		[Range(1f, 3f)] public float sizeDec = 2f;

		[Header("Visual Settings")]
		[Tooltip("The primary color of the fractal surface with HDR support.")]
		[ColorUsage(true, true)] public Color fractalColor = new Color(0.4f, 0.5f, 0.9f);

		[Tooltip("Background color used to clear artifacts and represent the void.")]
		public Color backgroundColor = Color.black;

		[Tooltip("Intensity multiplier for the fractal's glow/emission.")]
		[Range(0, 10)] public float emissionStrength = 1.5f;

		[Tooltip("Density of the exponential depth-based fog.")]
		[Range(0, 0.1f)] public float fogDensity = 0.02f;

		[Tooltip("Maximum raymarching distance before stopping calculations.")]
		public float maxDistance = 100f;

		protected override void Awake()
		{
			// Initializes core components like AnimationController and OptionsManager in App.cs
			base.Awake();

			// Standard Singleton pattern setup
			if (Instance != null && Instance != this)
			{
				Destroy(this);
				return;
			}
			Instance = this;
		}

		protected override void OnEnable()
		{
			// Initializes RenderTextures and UI references from App.cs
			base.OnEnable();

			// Set the camera to Orbit mode to allow 360-degree inspection of the flake
			cameraType = CameraType.Orbit;
		}

		/// <summary>
		/// The main drawing function. 
		/// Left empty because the rendering logic is handled by the OctahedronFlakeFeature (URP).
		/// </summary>
		protected override void Render(RenderTexture targetTexture) { }

		#region UI Bridge Properties

		/// <summary>
		/// Properties used to bridge the internal fractal state with the custom UI system.
		/// Prefix 'O_' is maintained for compatibility with existing Slider/Toggle controllers.
		/// </summary>

		public float O_Iterations
		{
			get => iterations;
			set => iterations = Mathf.RoundToInt(value);
		}

		public float O_Size
		{
			get => size;
			set => size = value;
		}

		public float O_SizeDec
		{
			get => sizeDec;
			set => sizeDec = value;
		}

		#endregion
	}
}