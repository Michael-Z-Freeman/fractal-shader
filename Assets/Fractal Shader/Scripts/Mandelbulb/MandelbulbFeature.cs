#pragma warning disable CS0618 // Suppresses obsolete warnings for RenderTargetHandle usage

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	/// <summary>
	/// A Universal Render Pipeline (URP) Feature that injects the Mandelbulb Raymarching Compute Shader into the camera rendering loop.
	/// </summary>
	public class MandelbulbFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings
		{
			[Tooltip("The main Compute Shader to be executed during this render pass.")]
			public ComputeShader computeShader;
		}

		public Settings settings = new Settings();
		private MandelbulbPass renderPass;

		/// <summary>
		/// Initializes the render pass and defines when it should execute in the URP pipeline.
		/// </summary>
		public override void Create()
		{
			renderPass = new MandelbulbPass(settings)
			{
				// Executing after the Skybox ensures the fractal is drawn over the background environment
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

			// Explicitly use UnityEngine.CameraType to avoid namespace collisions with custom enums
			if (renderingData.cameraData.cameraType != UnityEngine.CameraType.Game) return;

			renderer.EnqueuePass(renderPass);
		}

		/// <summary>
		/// The actual Render Pass that executes the Compute Shader via Command Buffers.
		/// </summary>
		class MandelbulbPass : ScriptableRenderPass
		{
			private Settings settings;

			// --- PERFORMANCE OPTIMIZATION: CACHING SHADER IDs ---
			// Caching IDs once is significantly faster than passing strings every frame.
			private static readonly int TempSourceId = Shader.PropertyToID("_TempSource");
			private static readonly int TempTargetId = Shader.PropertyToID("_TempTarget");
			private static readonly int CamToWorldId = Shader.PropertyToID("CamToWorld");
			private static readonly int CamInverseProjId = Shader.PropertyToID("CamInverseProjection");

			private static readonly int PowerId = Shader.PropertyToID("Power");
			private static readonly int IterationsId = Shader.PropertyToID("Iterations");
			private static readonly int JuliaId = Shader.PropertyToID("Julia");
			private static readonly int CId = Shader.PropertyToID("C");
			private static readonly int MixId = Shader.PropertyToID("Mix");
			private static readonly int AltId = Shader.PropertyToID("Alt");

			private static readonly int FractalColorId = Shader.PropertyToID("FractalColor");
			private static readonly int BackgroundColorId = Shader.PropertyToID("BackgroundColor");
			private static readonly int EmissionStrengthId = Shader.PropertyToID("EmissionStrength");
			private static readonly int FogDensityId = Shader.PropertyToID("FogDensity");
			private static readonly int MaxDistanceId = Shader.PropertyToID("MaxDistance");

			private static readonly int WidthId = Shader.PropertyToID("_Width");
			private static readonly int HeightId = Shader.PropertyToID("_Height");
			private static readonly int SourceTextureId = Shader.PropertyToID("Source");
			private static readonly int TargetTextureId = Shader.PropertyToID("Texture");

			public MandelbulbPass(Settings settings)
			{
				this.settings = settings;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (Mandelbulb.Instance == null) return;
				var fractal = Mandelbulb.Instance;
				Camera activeCam = fractal.cam;
				if (activeCam == null) return;
				FractalRenderGraph.Record(renderGraph, frameData, "Mandelbulb Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeMatrixParam(settings.computeShader, CamToWorldId, activeCam.cameraToWorldMatrix);
					cmd.SetComputeMatrixParam(settings.computeShader, CamInverseProjId, activeCam.projectionMatrix.inverse);
					cmd.SetComputeFloatParam(settings.computeShader, PowerId, fractal.power);
					cmd.SetComputeIntParam(settings.computeShader, IterationsId, fractal.iterations);
					cmd.SetComputeFloatParam(settings.computeShader, JuliaId, fractal.julia);
					cmd.SetComputeVectorParam(settings.computeShader, CId, fractal.c);
					cmd.SetComputeFloatParam(settings.computeShader, MixId, fractal.mix);
					cmd.SetComputeIntParam(settings.computeShader, AltId, fractal.alt ? 1 : 0);
					cmd.SetComputeVectorParam(settings.computeShader, FractalColorId, fractal.fractalColor);
					cmd.SetComputeVectorParam(settings.computeShader, BackgroundColorId, fractal.backgroundColor);
					cmd.SetComputeFloatParam(settings.computeShader, EmissionStrengthId, fractal.emissionStrength);
					cmd.SetComputeFloatParam(settings.computeShader, FogDensityId, fractal.fogDensity);
					cmd.SetComputeFloatParam(settings.computeShader, MaxDistanceId, fractal.maxDistance);
					cmd.SetComputeIntParam(settings.computeShader, WidthId, width);
					cmd.SetComputeIntParam(settings.computeShader, HeightId, height);
				});
			}

			private void ExecuteLegacy(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				// Safety check to ensure the Mandelbulb manager is active
				if (Mandelbulb.Instance == null) return;

				CommandBuffer cmd = CommandBufferPool.Get("MandelbulbComputePass");

				// Get the current camera's color target and descriptor
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

				// Retrieve the custom camera for proper perspective
				Camera activeCam = Mandelbulb.Instance.cam;
				if (activeCam == null) activeCam = renderingData.cameraData.camera;

				// Set Camera Matrices
				cmd.SetComputeMatrixParam(cs, CamToWorldId, activeCam.cameraToWorldMatrix);
				cmd.SetComputeMatrixParam(cs, CamInverseProjId, activeCam.projectionMatrix.inverse);

				// --- MANDELBULB MATHEMATICAL VARIABLES ---
				cmd.SetComputeFloatParam(cs, PowerId, Mandelbulb.Instance.power);
				cmd.SetComputeIntParam(cs, IterationsId, Mandelbulb.Instance.iterations);
				cmd.SetComputeFloatParam(cs, JuliaId, Mandelbulb.Instance.julia);
				cmd.SetComputeVectorParam(cs, CId, Mandelbulb.Instance.c);
				cmd.SetComputeFloatParam(cs, MixId, Mandelbulb.Instance.mix);
				cmd.SetComputeIntParam(cs, AltId, Mandelbulb.Instance.alt ? 1 : 0);

				// --- COLOR AND LIGHTING VARIABLES ---
				cmd.SetComputeVectorParam(cs, FractalColorId, Mandelbulb.Instance.fractalColor);
				cmd.SetComputeVectorParam(cs, BackgroundColorId, Mandelbulb.Instance.backgroundColor);
				cmd.SetComputeFloatParam(cs, EmissionStrengthId, Mandelbulb.Instance.emissionStrength);
				cmd.SetComputeFloatParam(cs, FogDensityId, Mandelbulb.Instance.fogDensity);
				cmd.SetComputeFloatParam(cs, MaxDistanceId, Mandelbulb.Instance.maxDistance);

				// GPU Freeze Prevention (Resolution bounds)
				cmd.SetComputeIntParam(cs, WidthId, desc.width);
				cmd.SetComputeIntParam(cs, HeightId, desc.height);

				// Bind the Textures
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
