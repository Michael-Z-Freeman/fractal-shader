using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	/// <summary>
	/// A Universal Render Pipeline (URP) Feature that injects the Mandelbox Compute Shader into the camera rendering loop.
	/// </summary>
	public class MandelboxFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings
		{
			[Tooltip("The main Compute Shader to be executed during this render pass.")]
			public ComputeShader computeShader;
		}

		public Settings settings = new Settings();
		private MandelboxPass renderPass;

		/// <summary>
		/// Initializes the render pass and defines when it should execute in the URP pipeline.
		/// </summary>
		public override void Create()
		{
			renderPass = new MandelboxPass(settings)
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

			// Explicitly use UnityEngine.CameraType to avoid namespace collisions with the custom CameraType enum
			if (renderingData.cameraData.cameraType != UnityEngine.CameraType.Game) return;

			renderer.EnqueuePass(renderPass);
		}

		/// <summary>
		/// The actual Render Pass that executes the Compute Shader via Command Buffers.
		/// </summary>
		class MandelboxPass : ScriptableRenderPass
		{
			private Settings settings;

			// --- PERFORMANCE OPTIMIZATION: CACHING SHADER IDs ---
			// Caching IDs once is significantly faster than passing strings every frame.
			private static readonly int TempSourceId = Shader.PropertyToID("_TempSource");
			private static readonly int TempTargetId = Shader.PropertyToID("_TempTarget");
			private static readonly int CamToWorldId = Shader.PropertyToID("CamToWorld");
			private static readonly int CamInverseProjId = Shader.PropertyToID("CamInverseProjection");

			private static readonly int ScaleId = Shader.PropertyToID("Scale");
			private static readonly int IterationsId = Shader.PropertyToID("Iterations");
			private static readonly int JuliaId = Shader.PropertyToID("Julia");
			private static readonly int CId = Shader.PropertyToID("C");
			private static readonly int MixId = Shader.PropertyToID("Mix");
			private static readonly int ColorId = Shader.PropertyToID("Color");

			private static readonly int WidthId = Shader.PropertyToID("_Width");
			private static readonly int HeightId = Shader.PropertyToID("_Height");
			private static readonly int SourceTextureId = Shader.PropertyToID("Source");
			private static readonly int TargetTextureId = Shader.PropertyToID("Texture");

			public MandelboxPass(Settings settings)
			{
				this.settings = settings;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (Mandelbox.Instance == null) return;
				var fractal = Mandelbox.Instance;
				Camera activeCam = fractal.cam;
				if (activeCam == null) return;
				FractalRenderGraph.Record(renderGraph, frameData, "Mandelbox Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeMatrixParam(settings.computeShader, CamToWorldId, activeCam.cameraToWorldMatrix);
					cmd.SetComputeMatrixParam(settings.computeShader, CamInverseProjId, activeCam.projectionMatrix.inverse);
					cmd.SetComputeFloatParam(settings.computeShader, ScaleId, fractal.scale);
					cmd.SetComputeIntParam(settings.computeShader, IterationsId, fractal.iterations);
					cmd.SetComputeIntParam(settings.computeShader, JuliaId, fractal.julia ? 1 : 0);
					cmd.SetComputeVectorParam(settings.computeShader, CId, fractal.c);
					cmd.SetComputeFloatParam(settings.computeShader, MixId, fractal.mix);
					cmd.SetComputeVectorParam(settings.computeShader, ColorId, fractal.boxColor);
					cmd.SetComputeIntParam(settings.computeShader, WidthId, width);
					cmd.SetComputeIntParam(settings.computeShader, HeightId, height);
				});
			}

			private void ExecuteLegacy(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				// Safety check to ensure the Mandelbox manager is active
				if (Mandelbox.Instance == null) return;

				CommandBuffer cmd = CommandBufferPool.Get("MandelboxComputePass");

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

				// Retrieve the custom camera for proper fractal perspective
				Camera activeCam = Mandelbox.Instance.cam;
				if (activeCam == null) activeCam = renderingData.cameraData.camera;

				// Set Camera Matrices (Verified working method from Infinite Spheres)
				cmd.SetComputeMatrixParam(cs, CamToWorldId, activeCam.cameraToWorldMatrix);
				cmd.SetComputeMatrixParam(cs, CamInverseProjId, activeCam.projectionMatrix.inverse);

				// --- FRACTAL PARAMETERS ---
				cmd.SetComputeFloatParam(cs, ScaleId, Mandelbox.Instance.scale);
				cmd.SetComputeIntParam(cs, IterationsId, Mandelbox.Instance.iterations);
				cmd.SetComputeIntParam(cs, JuliaId, Mandelbox.Instance.julia ? 1 : 0);
				cmd.SetComputeVectorParam(cs, CId, Mandelbox.Instance.c);
				cmd.SetComputeFloatParam(cs, MixId, Mandelbox.Instance.mix);
				cmd.SetComputeVectorParam(cs, ColorId, Mandelbox.Instance.boxColor);

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
