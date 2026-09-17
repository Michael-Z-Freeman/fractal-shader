using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FractalShader
{
	/// <summary>
	/// Manages user input and camera movement logic.
	/// Supports Free Look (FPS), Orbit (Spherical), and Flight control schemes.
	/// </summary>
	public class ControlsHelper : MonoBehaviour
	{
		private App app;
		private Controls inputActions;

		[Header("Sensitivity & Limits")]
		public float sensitivity = 10f;
		public float minRadius = 1f;
		public float maxRadius = 5f;

		// --- Standard Input States ---
		[HideInInspector] public Vector2 moveInput, mouseDelta, dragStartPosition;
		[HideInInspector] public float tiltInput, zoomDelta;
		[HideInInspector] public bool isDragging, isRightClickActive;
		private bool primaryClickStarted;
		private bool primaryClickReleased;

		// --- Flight Control States ---
		[HideInInspector] public float throttle, roll, yaw, pitch;

		[Header("Camera Animation")]
		[Tooltip("Target framerate for smooth camera transitions.")]
		[Range(10, 60)] public int targetFPS = 30;

		public bool animateElevation;
		public bool animateAzimuth;
		public bool animateRadius;

		private int animElevationID, animAzimuthID, animRadiusID;

		private void OnEnable()
		{
			app = GetComponent<App>();
		}

		private void Start()
		{
			if (inputActions != null) inputActions.Enable();
		}

		private void OnDisable()
		{
			if (inputActions != null) inputActions.Disable();
		}

		/// <summary>
		/// Maps Input System events to internal variables and initializes UI state.
		/// </summary>
		public void InitializeInputs()
		{
			GameObject optionsMenu = app.canvas.transform.GetChild(0).gameObject;
			inputActions = new Controls();

			// --- Default Action Mapping ---
			inputActions.Default.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
			inputActions.Default.Move.canceled += ctx => moveInput = Vector2.zero;

			inputActions.Default.Look.performed += ctx => mouseDelta = ctx.ReadValue<Vector2>();
			inputActions.Default.Look.canceled += ctx => mouseDelta = Vector2.zero;

			inputActions.Default.Tilt.performed += ctx => tiltInput = ctx.ReadValue<float>();
			inputActions.Default.Tilt.canceled += ctx => tiltInput = 0;

			// Note: Using "LetfClick" to match your specific Input Action naming
			// UI pointer state is updated after Input Action callbacks, so click work is deferred to Update.
			inputActions.Default.LetfClick.performed += ctx => primaryClickStarted = true;
			inputActions.Default.LetfClick.canceled += ctx => primaryClickReleased = true;

			inputActions.Default.RightClick.performed += ctx => isRightClickActive = true;
			inputActions.Default.RightClick.canceled += ctx => isRightClickActive = false;

			// Toggle logic for the Options Menu (R key or Gamepad Start)
			inputActions.Default.ToggleOptions.canceled += ctx => optionsMenu.SetActive(!optionsMenu.activeSelf);

			inputActions.Default.Zoom.performed += ctx => zoomDelta = ctx.ReadValue<float>();
			inputActions.Default.Zoom.canceled += ctx => zoomDelta = 0f;


			inputActions.Default.Record.canceled += ctx =>
				app.offlineRenderer.ToggleRendering(25, Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);

			// --- Flight Action Mapping ---
			inputActions.Flight.Throttle.performed += ctx => throttle = ctx.ReadValue<float>();
			inputActions.Flight.Throttle.canceled += ctx => throttle = 0;

			inputActions.Flight.Roll.performed += ctx => roll = ctx.ReadValue<float>();
			inputActions.Flight.Roll.canceled += ctx => roll = 0;

			inputActions.Flight.Yaw.performed += ctx => yaw = ctx.ReadValue<float>();
			inputActions.Flight.Yaw.canceled += ctx => yaw = 0;

			inputActions.Flight.Pitch.performed += ctx => pitch = ctx.ReadValue<float>();
			inputActions.Flight.Pitch.canceled += ctx => pitch = 0;

			optionsMenu.SetActive(true);
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;

			inputActions.Enable();
		}

		private void Update()
		{
			ProcessPointerInput();
			Transform camTransform = app.transform;
			bool isLocked = Cursor.lockState == CursorLockMode.Locked;

			switch (app.cameraType)
			{
				case App.CameraType.Free:
					if (isLocked && !app.offlineRenderer.isRendering)
						UpdateFreeLookCamera(camTransform);
					break;
				case App.CameraType.Orbit:
					UpdateOrbitCamera(camTransform, isLocked);
					break;
			}
		}

		private void ProcessPointerInput()
		{
			if (primaryClickStarted)
			{
				primaryClickStarted = false;
				if (!IsPointerOverUi())
				{
					HandleDragStart();
					ToggleCursorLock();
				}
			}

			if (primaryClickReleased)
			{
				primaryClickReleased = false;
				HandleDragEnd();
			}
		}

		private void UpdateFreeLookCamera(Transform t)
		{
			float dt = app.RequestSmoothDeltaTime();

			// Apply rotation (Euler)
			t.Rotate(new Vector3(
				-mouseDelta.y * dt,
				mouseDelta.x * dt,
				-tiltInput * dt * 10f) * 5f);

			// Apply translation (WASD)
			t.Translate(new Vector3(
				moveInput.x * dt * sensitivity,
				0,
				moveInput.y * dt * sensitivity));

			app.ReRender();
		}

		private void UpdateOrbitCamera(Transform t, bool isLocked)
		{
			Vector3 pos = t.localPosition;
			float radius = pos.magnitude;
			Vector2 sphericalAngles = app.CartesianCoordsToSphericalCoords(pos.normalized);

			HandleAnimationSync(ref sphericalAngles, ref radius);

			float dt = app.RequestSmoothDeltaTime();

			// Horizontal Rotation (Azimuth)
			if (animateAzimuth) sphericalAngles.x = app.animationController.Get(animAzimuthID);
			else if (!app.offlineRenderer.isRendering && isLocked)
				sphericalAngles.x -= mouseDelta.x * dt * sensitivity;

			// Vertical Rotation (Elevation)
			if (animateElevation) sphericalAngles.y = app.animationController.Get(animElevationID);
			else if (!app.offlineRenderer.isRendering && isLocked)
				sphericalAngles.y -= mouseDelta.y * dt * sensitivity;

			// Distance (Radius)
			if (animateRadius) radius = app.animationController.Get(animRadiusID);
			else if (!app.offlineRenderer.isRendering && isLocked)
				radius -= zoomDelta * sensitivity / 3000f;

			if (isLocked || animateAzimuth || animateElevation || animateRadius)
			{
				ClampOrbitBoundaries(ref radius, ref sphericalAngles);
				t.localPosition = app.SphericalCoordsToCartesianCoords(sphericalAngles.x, sphericalAngles.y) * radius;

				Transform parent = t.parent;
				t.LookAt(parent ? parent.position : Vector3.zero);
				app.ReRender();
			}
		}

		private void HandleAnimationSync(ref Vector2 angles, ref float radius)
		{
			if (!animateAzimuth) animAzimuthID = 0;
			else if (animAzimuthID == 0) animAzimuthID = app.animationController.Register(angles.x, 15f, 0, 360, false);

			if (!animateElevation) animElevationID = 0;
			else if (animElevationID == 0) animElevationID = app.animationController.Register(angles.y, 0.05f, -80, 80, true);

			if (!animateRadius) animRadiusID = 0;
			else if (animRadiusID == 0) animRadiusID = app.animationController.Register(radius, 0.06f, minRadius, maxRadius, true);
		}

		private void ClampOrbitBoundaries(ref float r, ref Vector2 angles)
		{
			r = Mathf.Clamp(r, minRadius, maxRadius);
			if (angles.x < 0) angles.x += 360f;
			if (angles.x >= 360) angles.x -= 360f;
			angles.y = Mathf.Clamp(angles.y, -80f, 80f);
		}

		private bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

		private void ToggleCursorLock()
		{
			if (app.cameraType != App.CameraType.None)
			{
				if (Cursor.lockState == CursorLockMode.Locked)
				{
					Cursor.lockState = CursorLockMode.None;
					Cursor.visible = true;
				}
				else
				{
					Cursor.lockState = CursorLockMode.Locked;
					Cursor.visible = false;
				}
			}
		}

		private void HandleDragStart()
		{
			isDragging = true;
			dragStartPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
		}

		private void HandleDragEnd() => isDragging = false;
	}
}
