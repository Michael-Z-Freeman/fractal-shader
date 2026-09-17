using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	/// <summary>
	/// A Universal Render Pipeline (URP) Feature that injects the Menger Sponge Compute Shader into the camera rendering loop.
	/// </summary>
	public class MengerSpongeFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings
		{
			[Tooltip("The main Compute Shader to be executed during this render pass.")]
			public ComputeShader computeShader;
		}

		public Settings settings = new Settings();
		private MengerSpongePass renderPass;

		/// <summary>
		/// Initializes the render pass and defines when it should execute in the URP pipeline.
		/// </summary>
		public override void Create()
		{
			renderPass = new MengerSpongePass(settings)
			{
				// Rendering after the skybox ensures the fractal is drawn over the environment
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
		class MengerSpongePass : ScriptableRenderPass
		{
			private Settings settings;

			// --- PERFORMANCE OPTIMIZATION: CACHING SHADER IDs ---
			// Caching IDs once is significantly faster than passing strings every frame.
			private static readonly int TempSourceId = Shader.PropertyToID("_TempSource");
			private static readonly int TempTargetId = Shader.PropertyToID("_TempTarget");
			private static readonly int CamToWorldId = Shader.PropertyToID("CamToWorld");
			private static readonly int CamInverseProjId = Shader.PropertyToID("CamInverseProjection");

			private static readonly int IterationsId = Shader.PropertyToID("Iterations");
			private static readonly int SizeId = Shader.PropertyToID("Size");
			private static readonly int EdgeId = Shader.PropertyToID("Edge");
			private static readonly int CutId = Shader.PropertyToID("Cut");
			private static readonly int CantorId = Shader.PropertyToID("Cantor");

			private static readonly int WidthId = Shader.PropertyToID("_Width");
			private static readonly int HeightId = Shader.PropertyToID("_Height");
			private static readonly int SourceTextureId = Shader.PropertyToID("Source");
			private static readonly int TargetTextureId = Shader.PropertyToID("Texture");

			public MengerSpongePass(Settings settings)
			{
				this.settings = settings;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (MengerSponge.Instance == null) return;
				var fractal = MengerSponge.Instance;
				Camera activeCam = fractal.cam;
				if (activeCam == null) return;
				FractalRenderGraph.Record(renderGraph, frameData, "Menger Sponge Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeMatrixParam(settings.computeShader, CamToWorldId, activeCam.cameraToWorldMatrix);
					cmd.SetComputeMatrixParam(settings.computeShader, CamInverseProjId, activeCam.projectionMatrix.inverse);
					cmd.SetComputeIntParam(settings.computeShader, IterationsId, Mathf.RoundToInt(fractal.O_Iterations));
					cmd.SetComputeFloatParam(settings.computeShader, SizeId, fractal.O_Size);
					cmd.SetComputeFloatParam(settings.computeShader, EdgeId, fractal.O_Edge);
					cmd.SetComputeFloatParam(settings.computeShader, CutId, fractal.O_Cut);
					cmd.SetComputeIntParam(settings.computeShader, CantorId, fractal.O_Cantor ? 1 : 0);
					cmd.SetComputeIntParam(settings.computeShader, WidthId, width);
					cmd.SetComputeIntParam(settings.computeShader, HeightId, height);
				});
			}

			private void ExecuteLegacy(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				// Safety check to ensure the MengerSponge manager is active
				if (MengerSponge.Instance == null) return;

				CommandBuffer cmd = CommandBufferPool.Get("MengerSpongeComputePass");

				// Get the current camera's color target and descriptor
				// Note: cameraColorTargetHandle is used to support recent URP updates
				RenderTargetIdentifier source = BuiltinRenderTextureType.CameraTarget;
				RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

				// Disable MSAA and depth buffer for the compute shader temporary textures to avoid Random Write errors
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

				// Retrieve the custom camera for proper fractal perspective
				Camera activeCam = MengerSponge.Instance.cam;
				if (activeCam == null) activeCam = renderingData.cameraData.camera;

				// Set Camera Matrices
				cmd.SetComputeMatrixParam(cs, CamToWorldId, activeCam.cameraToWorldMatrix);
				cmd.SetComputeMatrixParam(cs, CamInverseProjId, activeCam.projectionMatrix.inverse);

				// --- FRACTAL PARAMETERS ---
				cmd.SetComputeIntParam(cs, IterationsId, Mathf.RoundToInt(MengerSponge.Instance.O_Iterations));
				cmd.SetComputeFloatParam(cs, SizeId, MengerSponge.Instance.O_Size);
				cmd.SetComputeFloatParam(cs, EdgeId, MengerSponge.Instance.O_Edge);
				cmd.SetComputeFloatParam(cs, CutId, MengerSponge.Instance.O_Cut);
				cmd.SetComputeIntParam(cs, CantorId, MengerSponge.Instance.O_Cantor ? 1 : 0);

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

				// Blit the final result back to the camera target
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
