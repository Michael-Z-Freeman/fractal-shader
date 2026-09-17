using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	/// <summary>
	/// A Universal Render Pipeline (URP) Feature that injects the FRAX Raymarching Compute Shader into the camera rendering loop.
	/// </summary>
	public class RaymarchFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings
		{
			[Tooltip("The main Compute Shader to be executed during this render pass.")]
			public ComputeShader computeShader;
		}

		public Settings settings = new Settings();
		private RaymarchPass renderPass;

		/// <summary>
		/// Initializes the render pass and defines when it should execute in the URP pipeline.
		/// </summary>
		public override void Create()
		{
			renderPass = new RaymarchPass(settings)
			{
				// Executing after the Skybox ensures the fractal is drawn over the background
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

			// Explicitly use UnityEngine.CameraType to avoid namespace collisions with your custom CameraType enum
			if (renderingData.cameraData.cameraType != UnityEngine.CameraType.Game) return;

			renderer.EnqueuePass(renderPass);
		}

		/// <summary>
		/// The actual Render Pass that executes the Compute Shader via Command Buffers.
		/// </summary>
		class RaymarchPass : ScriptableRenderPass
		{
			private Settings settings;

			// --- PERFORMANCE OPTIMIZATION: CACHING SHADER IDs ---
			// Calling Shader.PropertyToID once is much faster and prevents garbage generation every frame.
			private static readonly int TempSourceId = Shader.PropertyToID("_TempSource");
			private static readonly int TempTargetId = Shader.PropertyToID("_TempTarget");
			private static readonly int CamToWorldId = Shader.PropertyToID("CamToWorld");
			private static readonly int CamInverseProjId = Shader.PropertyToID("CamInverseProjection");

			private static readonly int EdgeId = Shader.PropertyToID("Edge");
			private static readonly int CurveSpaceId = Shader.PropertyToID("CurveSpace");

			private static readonly int SizeId = Shader.PropertyToID("Size");
			private static readonly int IterationsId = Shader.PropertyToID("Iterations");
			private static readonly int AccentColorId = Shader.PropertyToID("AccentColor");
			private static readonly int BackgroundColorId = Shader.PropertyToID("BackgroundColor");
			private static readonly int ColorIntensityId = Shader.PropertyToID("ColorIntensity");

			private static readonly int WidthId = Shader.PropertyToID("_Width");
			private static readonly int HeightId = Shader.PropertyToID("_Height");
			private static readonly int SourceTextureId = Shader.PropertyToID("Source");
			private static readonly int TargetTextureId = Shader.PropertyToID("Texture");

			public RaymarchPass(Settings settings)
			{
				this.settings = settings;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (FRAX.Instance == null) return;
				var fractal = FRAX.Instance;
				Camera activeCam = fractal.cam;
				if (activeCam == null) return;
				FractalRenderGraph.Record(renderGraph, frameData, "FRAX Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeMatrixParam(settings.computeShader, CamToWorldId, activeCam.cameraToWorldMatrix);
					cmd.SetComputeMatrixParam(settings.computeShader, CamInverseProjId, activeCam.projectionMatrix.inverse);
					cmd.SetComputeFloatParam(settings.computeShader, EdgeId, fractal.currentEdge);
					cmd.SetComputeVectorParam(settings.computeShader, CurveSpaceId, fractal.currentCurveSpace * fractal.curve);
					cmd.SetComputeFloatParam(settings.computeShader, SizeId, fractal.size);
					cmd.SetComputeIntParam(settings.computeShader, IterationsId, fractal.iterations);
					cmd.SetComputeVectorParam(settings.computeShader, AccentColorId, fractal.accentColor);
					cmd.SetComputeVectorParam(settings.computeShader, BackgroundColorId, fractal.backgroundColor);
					cmd.SetComputeFloatParam(settings.computeShader, ColorIntensityId, fractal.colorIntensity);
					cmd.SetComputeIntParam(settings.computeShader, WidthId, width);
					cmd.SetComputeIntParam(settings.computeShader, HeightId, height);
				});
			}

			private void ExecuteLegacy(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				// Safety check to ensure the FRAX manager is active
				if (FRAX.Instance == null) return;

				CommandBuffer cmd = CommandBufferPool.Get("RaymarchComputePass");

				// Get the current camera's color target
				RenderTargetIdentifier source = BuiltinRenderTextureType.CameraTarget;
				RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

				// Preventing MSAA and Depth buffer Random Write crashes by disabling them for the temporary compute textures
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

				// Retrieve the custom camera to allow proper raymarching perspective and movement
				Camera activeCam = FRAX.Instance.cam;
				if (activeCam == null) activeCam = renderingData.cameraData.camera;

				// Set Camera Matrices
				cmd.SetComputeMatrixParam(cs, CamToWorldId, activeCam.cameraToWorldMatrix);
				cmd.SetComputeMatrixParam(cs, CamInverseProjId, activeCam.projectionMatrix.inverse);

				// --- CORE FRAX VARIABLES ---
				cmd.SetComputeFloatParam(cs, EdgeId, FRAX.Instance.currentEdge);
				cmd.SetComputeVectorParam(cs, CurveSpaceId, FRAX.Instance.currentCurveSpace * FRAX.Instance.curve);

				// --- CUSTOM INSPECTOR SETTINGS ---
				cmd.SetComputeFloatParam(cs, SizeId, FRAX.Instance.size);
				cmd.SetComputeIntParam(cs, IterationsId, FRAX.Instance.iterations);
				cmd.SetComputeVectorParam(cs, AccentColorId, FRAX.Instance.accentColor);
				cmd.SetComputeVectorParam(cs, BackgroundColorId, FRAX.Instance.backgroundColor);
				cmd.SetComputeFloatParam(cs, ColorIntensityId, FRAX.Instance.colorIntensity);

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
