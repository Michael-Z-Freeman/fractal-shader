using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FractalShader
{
	/// <summary>
	/// Handles high-quality frame-by-frame rendering to disk.
	/// Allows creating perfectly smooth videos even if the real-time framerate is low.
	/// </summary>
	public class OfflineRenderer : MonoBehaviour
	{
		private App app;
		private string sessionTimestamp;
		private int targetFPS;
		private int frameCount;

		/// <summary>
		/// Returns true if a recording sequence is currently active.
		/// </summary>
		public bool isRendering { get; private set; }

		private void OnEnable()
		{
			app = GetComponent<App>();
		}

		private void Update()
		{
			if (isRendering)
			{
				// Trigger a re-render of the fractal for this specific frame
				app.ReRender();
				StartCoroutine(CaptureFrameRoutine());
			}
		}

		/// <summary>
		/// Captures the current frame at the end of the rendering pipeline.
		/// </summary>
		private IEnumerator CaptureFrameRoutine()
		{
			yield return new WaitForEndOfFrame();

			// Construct the directory path using Path.Combine for cross-platform compatibility
			string folderName = $"{sessionTimestamp} ({targetFPS} FPS)";
			string shaderDir = app.shader != null ? app.shader.name : "Universal";
			string path = Path.Combine(Application.persistentDataPath, "Recordings", shaderDir, folderName);

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			string fileName = $"frame_{frameCount:D5}.png"; // Padded format: frame_00001.png
			string fullPath = Path.Combine(path, fileName);

			ScreenCapture.CaptureScreenshot(fullPath);
			frameCount++;

			yield return null;
		}

		/// <summary>
		/// Returns the simulated delta time during recording or actual delta time during real-time play.
		/// This ensures math-heavy animations stay perfectly synced in video exports.
		/// </summary>
		public float RequestDeltaTime(bool useSmoothDelta)
		{
			if (isRendering)
				return 1f / targetFPS;

			return useSmoothDelta ? Time.smoothDeltaTime : Time.deltaTime;
		}

		/// <summary>
		/// Starts the sequence recording.
		/// </summary>
		/// <param name="fps">Target frames per second for the final video.</param>
		/// <param name="showUI">Whether the UI should be visible in the recording.</param>
		public void StartRendering(int fps, bool showUI)
		{
			if (isRendering) return;

			targetFPS = fps;
			sessionTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
			frameCount = 0;

			if (app.canvas != null)
				app.canvas.SetActive(showUI);

			isRendering = true;
			Debug.Log($"[OfflineRenderer] Recording started at {fps} FPS.");
		}

		/// <summary>
		/// Stops the current recording sequence.
		/// </summary>
		public void StopRendering()
		{
			if (!isRendering) return;

			isRendering = false;

			if (app.canvas != null)
				app.canvas.SetActive(true);

			Debug.Log("[OfflineRenderer] Recording sequence finalized.");
		}

		/// <summary>
		/// Toggles the recording state.
		/// </summary>
		public void ToggleRendering(int fps, bool showUI)
		{
			if (isRendering) StopRendering();
			else StartRendering(fps, showUI);
		}
	}
}