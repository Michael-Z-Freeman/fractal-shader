using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Base abstract class for Compute Shader based rendering operations.
	/// Manages the rendering pipeline, textures, and coordinates all framework modules.
	/// </summary>
	[RequireComponent(typeof(Camera))]
	public abstract class App : MonoBehaviour
	{
		public enum CameraType { None, Orbit, Free }

		[Header("Core Engine References")]
		[Tooltip("The main Compute Shader used to calculate the fractal geometry.")]
		public ComputeShader shader;

		[Tooltip("The main UI Canvas for this fractal application.")]
		public GameObject canvas;

		[Header("Camera Configuration")]
		[Tooltip("Determines how the user can interact with the scene.")]
		public CameraType cameraType = CameraType.None;

		// Public module references (Internal Handshake)
		[HideInInspector] public Camera cam;
		[HideInInspector] public AnimationController animationController;
		[HideInInspector] public OfflineRenderer offlineRenderer;
		[HideInInspector] public OptionsManager optionsManager;
		[HideInInspector] public ControlsHelper controlsHelper;

		// Internal rendering variables
		protected RenderTexture primaryTex;
		protected RenderTexture secondaryTex;
		protected int screenWidth;
		protected int screenHeight;
		private bool requiresUpdate = true;

		protected virtual void Awake()
		{
			// 1. Cache the main camera component
			cam = GetComponent<Camera>();

			// 2. Automatically attach or find necessary framework modules
			animationController = GetComponent<AnimationController>() ?? gameObject.AddComponent<AnimationController>();
			offlineRenderer = GetComponent<OfflineRenderer>() ?? gameObject.AddComponent<OfflineRenderer>();

			// Critical Setup: Initialize OptionsManager and link this App as the target
			optionsManager = GetComponent<OptionsManager>() ?? gameObject.AddComponent<OptionsManager>();
			optionsManager.target = this;

			controlsHelper = GetComponent<ControlsHelper>() ?? gameObject.AddComponent<ControlsHelper>();

			// 3. Initialize the input mapping system
			if (controlsHelper != null)
			{
				controlsHelper.InitializeInputs();
			}
		}

		protected virtual void OnEnable()
		{
			InitializeRenderTextures();
		}

		protected virtual void Update() { }
		#region Rendering Pipeline

		/// <summary>
		/// Flag the system to execute a new render pass during the next frame.
		/// </summary>
		public void ReRender() => requiresUpdate = true;

		/// <summary>
		/// To be implemented by child classes (e.g., Sierpinski) to dispatch specific kernels.
		/// </summary>
		protected abstract void Render(RenderTexture targetTexture);

		[ImageEffectOpaque]
		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			InitializeRenderTextures();

			if (requiresUpdate)
			{
				// Execute the fractal logic onto our primary buffer
				Render(primaryTex);
				requiresUpdate = false;
			}

			// Output the final generated texture to the display
			Graphics.Blit(primaryTex, destination);
		}

		/// <summary>
		/// Manages RenderTexture allocation and handles screen resolution changes.
		/// </summary>
		private void InitializeRenderTextures()
		{
			screenWidth = cam.pixelWidth;
			screenHeight = cam.pixelHeight;

			if (primaryTex == null || primaryTex.width != screenWidth || primaryTex.height != screenHeight)
			{
				if (primaryTex != null)
				{
					primaryTex.Release();
					secondaryTex.Release();
				}

				// Standard Color Buffer
				primaryTex = new RenderTexture(screenWidth, screenHeight, 0, RenderTextureFormat.ARGB32);
				primaryTex.enableRandomWrite = true;
				primaryTex.Create();

				// High-Precision Depth/Data Buffer
				secondaryTex = new RenderTexture(screenWidth, screenHeight, 0, RenderTextureFormat.ARGBFloat);
				secondaryTex.enableRandomWrite = true;
				secondaryTex.Create();

				ReRender();
			}
		}

		#endregion

		#region Coordinate Geometry (Fixed Method Names)

		/// <summary>
		/// Maps horizontal (Azimuth) and vertical (Elevation) angles to a normalized direction vector.
		/// </summary>
		public Vector3 SphericalCoordsToCartesianCoords(float azimuth, float elevation)
		{
			float radAzimuth = azimuth * Mathf.Deg2Rad;
			float radElevation = elevation * Mathf.Deg2Rad;

			float x = Mathf.Cos(radAzimuth) * Mathf.Cos(radElevation);
			float y = Mathf.Sin(radElevation);
			float z = Mathf.Sin(radAzimuth) * Mathf.Cos(radElevation);

			return new Vector3(x, y, z).normalized;
		}

		/// <summary>
		/// Maps a Cartesian vector back to Spherical angles for camera orientation logic.
		/// </summary>
		public Vector2 CartesianCoordsToSphericalCoords(Vector3 cartesianPoint)
		{
			cartesianPoint.Normalize();
			float elevation = Mathf.Asin(cartesianPoint.y) * Mathf.Rad2Deg;
			float azimuth = Mathf.Atan2(cartesianPoint.z, cartesianPoint.x) * Mathf.Rad2Deg;

			return new Vector2(azimuth, elevation);
		}

		#endregion

		#region Framework Utilities

		/// <summary>
		/// Returns DeltaTime synchronized with the OfflineRenderer state.
		/// </summary>
		public float RequestDeltaTime() => offlineRenderer != null ? offlineRenderer.RequestDeltaTime(false) : Time.deltaTime;
		public float RequestSmoothDeltaTime() => offlineRenderer != null ? offlineRenderer.RequestDeltaTime(true) : Time.smoothDeltaTime;
 
		#endregion
	}
}