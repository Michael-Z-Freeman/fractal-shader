using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	/// <summary>
	/// A Universal Render Pipeline (URP) Feature that injects the Mandelbrot/Julia Compute Shader into the camera rendering loop.
	/// </summary>
	public class MandelbrotFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings
		{
			[Tooltip("The main Compute Shader to be executed during this render pass.")]
			public ComputeShader computeShader;
		}

		public Settings settings = new Settings();
		private MandelbrotPass renderPass;

		/// <summary>
		/// Initializes the render pass and defines when it should execute in the URP pipeline.
		/// </summary>
		public override void Create()
		{
			renderPass = new MandelbrotPass(settings)
			{
				// Rendering after the skybox ensures the 2D fractal fills the background correctly
				renderPassEvent = RenderPassEvent.AfterRenderingSkybox
			};
		}

		/// <summary>
		/// Injects the render pass into the active renderer.
		/// </summary>
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			// Abort if the shader is missing
			if (settings.computeShader == null) return;

			// Explicitly use UnityEngine.CameraType to avoid namespace collisions with the custom CameraType enum
			if (renderingData.cameraData.cameraType != UnityEngine.CameraType.Game) return;

			renderer.EnqueuePass(renderPass);
		}

		/// <summary>
		/// The actual Render Pass that executes the Compute Shader via Command Buffers.
		/// </summary>
		class MandelbrotPass : ScriptableRenderPass
		{
			private Settings settings;

			// --- PERFORMANCE OPTIMIZATION: CACHING SHADER IDs ---
			// Caching IDs once is significantly faster than passing strings every frame.
			private static readonly int TempSourceId = Shader.PropertyToID("_TempSource");
			private static readonly int TempTargetId = Shader.PropertyToID("_TempTarget");

			private static readonly int JuliaId = Shader.PropertyToID("Julia");
			private static readonly int AreaId = Shader.PropertyToID("Area");
			private static readonly int CId = Shader.PropertyToID("C");
			private static readonly int IterationsId = Shader.PropertyToID("Iterations");

			private static readonly int BackgroundColorId = Shader.PropertyToID("BackgroundColor");
			private static readonly int BrightnessId = Shader.PropertyToID("Brightness");

			private static readonly int WidthId = Shader.PropertyToID("_Width");
			private static readonly int HeightId = Shader.PropertyToID("_Height");
			private static readonly int ColorsBufferId = Shader.PropertyToID("Colors");
			private static readonly int ColorCountId = Shader.PropertyToID("ColorCount");
			private static readonly int SourceTextureId = Shader.PropertyToID("Source");
			private static readonly int TargetTextureId = Shader.PropertyToID("Texture");

			public MandelbrotPass(Settings settings)
			{
				this.settings = settings;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (Mandelbrot.Instance == null || Mandelbrot.Instance.colorBuffer == null) return;
				var fractal = Mandelbrot.Instance;
				FractalRenderGraph.Record(renderGraph, frameData, "Mandelbrot Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeIntParam(settings.computeShader, JuliaId, fractal.julia ? 1 : 0);
					cmd.SetComputeVectorParam(settings.computeShader, AreaId, fractal.area);
					cmd.SetComputeVectorParam(settings.computeShader, CId, fractal.c);
					cmd.SetComputeIntParam(settings.computeShader, IterationsId, fractal.iterations);
					cmd.SetComputeVectorParam(settings.computeShader, BackgroundColorId, fractal.backgroundColor);
					cmd.SetComputeFloatParam(settings.computeShader, BrightnessId, fractal.brightness);
					cmd.SetComputeIntParam(settings.computeShader, WidthId, width);
					cmd.SetComputeIntParam(settings.computeShader, HeightId, height);
					cmd.SetComputeIntParam(settings.computeShader, ColorCountId, fractal.colorBuffer.count);
					cmd.SetComputeBufferParam(settings.computeShader, kernel, ColorsBufferId, fractal.colorBuffer);
				});
			}

			private void ExecuteLegacy(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				// Safety check to ensure the Mandelbrot manager and its color buffer are active
				if (Mandelbrot.Instance == null || Mandelbrot.Instance.colorBuffer == null) return;

				CommandBuffer cmd = CommandBufferPool.Get("MandelbrotComputePass");

				// Get the current camera's color target
				RenderTargetIdentifier source = BuiltinRenderTextureType.CameraTarget;
				RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

				// Disable MSAA and depth buffer to prevent Random Write crashes with Compute Shaders
				desc.msaaSamples = 1;
				desc.depthBufferBits = 0;

				// Create a temporary texture to hold the current screen image
				cmd.GetTemporaryRT(TempSourceId, desc);
				cmd.Blit(source, TempSourceId);

				// Create a temporary texture for the Compute Shader to write into
				desc.enableRandomWrite = true;
				cmd.GetTemporaryRT(TempTargetId, desc);

				ComputeShader cs = settings.computeShader;
				int kernel = cs.FindKernel("CSMain");

				// --- FRACTAL CORE PARAMETERS ---
				cmd.SetComputeIntParam(cs, JuliaId, Mandelbrot.Instance.julia ? 1 : 0);
				cmd.SetComputeVectorParam(cs, AreaId, Mandelbrot.Instance.area);
				cmd.SetComputeVectorParam(cs, CId, Mandelbrot.Instance.c);
				cmd.SetComputeIntParam(cs, IterationsId, Mandelbrot.Instance.iterations);

				// --- CUSTOM VISUAL SETTINGS ---
				cmd.SetComputeVectorParam(cs, BackgroundColorId, Mandelbrot.Instance.backgroundColor);
				cmd.SetComputeFloatParam(cs, BrightnessId, Mandelbrot.Instance.brightness);

				// GPU Bounds Protection
				cmd.SetComputeIntParam(cs, WidthId, desc.width);
				cmd.SetComputeIntParam(cs, HeightId, desc.height);
				cmd.SetComputeIntParam(cs, ColorCountId, Mandelbrot.Instance.colorBuffer.count);

				// Bind the Color Palette Buffer and Textures
				cmd.SetComputeBufferParam(cs, kernel, ColorsBufferId, Mandelbrot.Instance.colorBuffer);
				cmd.SetComputeTextureParam(cs, kernel, SourceTextureId, TempSourceId);
				cmd.SetComputeTextureParam(cs, kernel, TargetTextureId, TempTargetId);

				// Calculate Thread Groups based on screen resolution (8x8 threads per group)
				int threadGroupsX = Mathf.CeilToInt(desc.width / 8f);
				int threadGroupsY = Mathf.CeilToInt(desc.height / 8f);

				// Dispatch the Compute Shader
				cmd.DispatchCompute(cs, kernel, threadGroupsX, threadGroupsY, 1);

				// Blit the result back to the main camera target
				cmd.Blit(TempTargetId, source);

				// Clean up temporary textures
				cmd.ReleaseTemporaryRT(TempSourceId);
				cmd.ReleaseTemporaryRT(TempTargetId);

				// Execute and release the Command Buffer
				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}
		}
	}
}
