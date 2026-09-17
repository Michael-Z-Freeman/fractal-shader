#pragma warning disable CS0618 // Suppresses obsolete warnings for RenderTargetHandle usage

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	/// <summary>
	/// A Universal Render Pipeline (URP) Feature that injects the Octahedron Flake Compute Shader into the camera rendering loop.
	/// </summary>
	public class OctahedronFlakeFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings
		{
			[Tooltip("The main Compute Shader to be executed during this render pass.")]
			public ComputeShader computeShader;
		}

		public Settings settings = new Settings();
		private OctahedronFlakePass renderPass;

		/// <summary>
		/// Initializes the render pass and defines when it should execute in the URP pipeline.
		/// </summary>
		public override void Create()
		{
			renderPass = new OctahedronFlakePass(settings)
			{
				// Rendering after the skybox ensures the fractal sits correctly in the scene environment
				renderPassEvent = RenderPassEvent.AfterRenderingSkybox
			};
		}

		/// <summary>
		/// Injects the render pass into the active renderer.
		/// </summary>
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			// Safety checks: Abort if settings are missing or if the instance isn't active
			if (settings.computeShader == null || OctahedronFlake.Instance == null) return;

			// Only render for the Game camera (skips scene view/previews if desired)
			if (renderingData.cameraData.cameraType != UnityEngine.CameraType.Game) return;

			renderer.EnqueuePass(renderPass);
		}

		/// <summary>
		/// The actual Render Pass that executes the Compute Shader via Command Buffers.
		/// </summary>
		class OctahedronFlakePass : ScriptableRenderPass
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
			private static readonly int SizeDecId = Shader.PropertyToID("SizeDec");

			private static readonly int FractalColorId = Shader.PropertyToID("FractalColor");
			private static readonly int BackgroundColorId = Shader.PropertyToID("BackgroundColor");
			private static readonly int EmissionStrengthId = Shader.PropertyToID("EmissionStrength");
			private static readonly int FogDensityId = Shader.PropertyToID("FogDensity");
			private static readonly int MaxDistanceId = Shader.PropertyToID("MaxDistance");
			private static readonly int WidthId = Shader.PropertyToID("_Width");
			private static readonly int HeightId = Shader.PropertyToID("_Height");

			private static readonly int SourceTextureId = Shader.PropertyToID("Source");
			private static readonly int TargetTextureId = Shader.PropertyToID("Texture");

			public OctahedronFlakePass(Settings settings)
			{
				this.settings = settings;
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (OctahedronFlake.Instance == null) return;
				var fractal = OctahedronFlake.Instance;
				Camera activeCam = fractal.cam;
				if (activeCam == null) return;
				FractalRenderGraph.Record(renderGraph, frameData, "Octahedron Flake Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeMatrixParam(settings.computeShader, CamToWorldId, activeCam.cameraToWorldMatrix);
					cmd.SetComputeMatrixParam(settings.computeShader, CamInverseProjId, activeCam.projectionMatrix.inverse);
					cmd.SetComputeIntParam(settings.computeShader, IterationsId, Mathf.RoundToInt(fractal.O_Iterations));
					cmd.SetComputeFloatParam(settings.computeShader, SizeId, fractal.O_Size);
					cmd.SetComputeFloatParam(settings.computeShader, SizeDecId, fractal.O_SizeDec);
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
				// Final safety check for the singleton instance
				if (OctahedronFlake.Instance == null) return;

				CommandBuffer cmd = CommandBufferPool.Get("OctahedronFlakeComputePass");

				// Retrieve the current camera color target and descriptor
				RenderTargetIdentifier source = BuiltinRenderTextureType.CameraTarget;
				RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

				// Disable MSAA and Depth buffer to prevent Random Write crashes in the Compute Shader
				desc.msaaSamples = 1;
				desc.depthBufferBits = 0;

				// 1. Create a temporary source texture to prevent feedback loops/flickering
				cmd.GetTemporaryRT(TempSourceId, desc);
				cmd.Blit(source, TempSourceId);

				// 2. Prepare the target texture for Compute Shader random write access
				int tempTargetId = TempTargetId;
				desc.enableRandomWrite = true;
				cmd.GetTemporaryRT(tempTargetId, desc);

				ComputeShader cs = settings.computeShader;
				int kernel = cs.FindKernel("CSMain");

				// Use the custom framework camera or fallback to the current URP camera
				Camera activeCam = OctahedronFlake.Instance.cam;
				if (activeCam == null) activeCam = renderingData.cameraData.camera;

				// Set View Matrices
				cmd.SetComputeMatrixParam(cs, CamToWorldId, activeCam.cameraToWorldMatrix);
				cmd.SetComputeMatrixParam(cs, CamInverseProjId, activeCam.projectionMatrix.inverse);

				// --- FRACTAL MATHEMATICAL DATA ---
				cmd.SetComputeIntParam(cs, IterationsId, Mathf.RoundToInt(OctahedronFlake.Instance.O_Iterations));
				cmd.SetComputeFloatParam(cs, SizeId, OctahedronFlake.Instance.O_Size);
				cmd.SetComputeFloatParam(cs, SizeDecId, OctahedronFlake.Instance.O_SizeDec);

				// --- VISUAL RENDERING DATA ---
				cmd.SetComputeVectorParam(cs, FractalColorId, OctahedronFlake.Instance.fractalColor);
				cmd.SetComputeVectorParam(cs, BackgroundColorId, OctahedronFlake.Instance.backgroundColor);
				cmd.SetComputeFloatParam(cs, EmissionStrengthId, OctahedronFlake.Instance.emissionStrength);
				cmd.SetComputeFloatParam(cs, FogDensityId, OctahedronFlake.Instance.fogDensity);
				cmd.SetComputeFloatParam(cs, MaxDistanceId, OctahedronFlake.Instance.maxDistance);
				cmd.SetComputeIntParam(cs, WidthId, desc.width);
				cmd.SetComputeIntParam(cs, HeightId, desc.height);

				// Bind Textures
				cmd.SetComputeTextureParam(cs, kernel, SourceTextureId, TempSourceId);
				cmd.SetComputeTextureParam(cs, kernel, TargetTextureId, tempTargetId);

				// Calculate thread groups (8x8 threads per group)
				int threadGroupsX = Mathf.CeilToInt(desc.width / 8f);
				int threadGroupsY = Mathf.CeilToInt(desc.height / 8f);

				// Dispatch Compute
				cmd.DispatchCompute(cs, kernel, threadGroupsX, threadGroupsY, 1);

				// Output result to screen and clean up
				cmd.Blit(tempTargetId, source);
				cmd.ReleaseTemporaryRT(TempSourceId);
				cmd.ReleaseTemporaryRT(tempTargetId);

				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}
		}
	}
}
