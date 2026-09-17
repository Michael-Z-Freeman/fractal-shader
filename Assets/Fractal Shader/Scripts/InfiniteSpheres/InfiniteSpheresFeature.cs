using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	public class InfiniteSpheresFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings { public ComputeShader computeShader; }

		public Settings settings = new Settings();
		private InfiniteSpheresPass renderPass;

		public override void Create()
		{
			renderPass = new InfiniteSpheresPass(settings) { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (settings.computeShader == null || renderingData.cameraData.cameraType != UnityEngine.CameraType.Game) return;
			renderer.EnqueuePass(renderPass);
		}

		class InfiniteSpheresPass : ScriptableRenderPass
		{
			private Settings settings;
			private static readonly int TempSourceId = Shader.PropertyToID("_TempSource");
			private static readonly int TempTargetId = Shader.PropertyToID("_TempTarget");

			// Shader Uniform IDs
			private static readonly int CamToWorldId = Shader.PropertyToID("CamToWorld");
			private static readonly int CamInverseProjId = Shader.PropertyToID("CamInverseProjection");
			private static readonly int RadiusId = Shader.PropertyToID("Radius");
			private static readonly int RepeatId = Shader.PropertyToID("Repeat");
			private static readonly int InvertId = Shader.PropertyToID("Invert");
			private static readonly int SphereColorId = Shader.PropertyToID("SphereColor");
			private static readonly int BackgroundColorId = Shader.PropertyToID("BackgroundColor");
			private static readonly int EmissionStrengthId = Shader.PropertyToID("EmissionStrength");
			private static readonly int FogDensityId = Shader.PropertyToID("FogDensity");
			private static readonly int MaxDistanceId = Shader.PropertyToID("MaxDistance");
			private static readonly int WidthId = Shader.PropertyToID("_Width");
			private static readonly int HeightId = Shader.PropertyToID("_Height");

			public InfiniteSpheresPass(Settings settings) { this.settings = settings; }

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (InfiniteSpheres.Instance == null) return;
				var fractal = InfiniteSpheres.Instance;
				Camera activeCam = fractal.cam;
				if (activeCam == null) return;
				FractalRenderGraph.Record(renderGraph, frameData, "Infinite Spheres Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeMatrixParam(settings.computeShader, CamToWorldId, activeCam.cameraToWorldMatrix);
					cmd.SetComputeMatrixParam(settings.computeShader, CamInverseProjId, activeCam.projectionMatrix.inverse);
					cmd.SetComputeFloatParam(settings.computeShader, RadiusId, fractal.radius);
					cmd.SetComputeIntParam(settings.computeShader, RepeatId, fractal.useRepetition ? 1 : 0);
					cmd.SetComputeIntParam(settings.computeShader, InvertId, fractal.invertGeometry ? 1 : 0);
					cmd.SetComputeVectorParam(settings.computeShader, SphereColorId, fractal.sphereColor);
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
				if (InfiniteSpheres.Instance == null) return;

				CommandBuffer cmd = CommandBufferPool.Get("InfiniteSpheresPass");
				var renderer = renderingData.cameraData.renderer;
				RenderTargetIdentifier source = BuiltinRenderTextureType.CameraTarget;
				RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

				desc.msaaSamples = 1;
				desc.depthBufferBits = 0;

				cmd.GetTemporaryRT(TempSourceId, desc);
				cmd.Blit(source, TempSourceId);

				desc.enableRandomWrite = true;
				cmd.GetTemporaryRT(TempTargetId, desc);

				ComputeShader cs = settings.computeShader;
				int kernel = cs.FindKernel("CSMain");

				Camera activeCam = InfiniteSpheres.Instance.cam ?? renderingData.cameraData.camera;
				cmd.SetComputeMatrixParam(cs, CamToWorldId, activeCam.cameraToWorldMatrix);
				cmd.SetComputeMatrixParam(cs, CamInverseProjId, activeCam.projectionMatrix.inverse);

				// --- DÜZELTİLEN KISIM: RepeatId artık useRepetition kullanıyor ---
				cmd.SetComputeFloatParam(cs, RadiusId, InfiniteSpheres.Instance.radius);
				cmd.SetComputeIntParam(cs, RepeatId, InfiniteSpheres.Instance.useRepetition ? 1 : 0);
				cmd.SetComputeIntParam(cs, InvertId, InfiniteSpheres.Instance.invertGeometry ? 1 : 0);

				cmd.SetComputeVectorParam(cs, SphereColorId, InfiniteSpheres.Instance.sphereColor);
				cmd.SetComputeVectorParam(cs, BackgroundColorId, InfiniteSpheres.Instance.backgroundColor);
				cmd.SetComputeFloatParam(cs, EmissionStrengthId, InfiniteSpheres.Instance.emissionStrength);
				cmd.SetComputeFloatParam(cs, FogDensityId, InfiniteSpheres.Instance.fogDensity);
				cmd.SetComputeFloatParam(cs, MaxDistanceId, InfiniteSpheres.Instance.maxDistance);
				cmd.SetComputeIntParam(cs, WidthId, desc.width);
				cmd.SetComputeIntParam(cs, HeightId, desc.height);

				cmd.SetComputeTextureParam(cs, kernel, "Source", TempSourceId);
				cmd.SetComputeTextureParam(cs, kernel, "Texture", TempTargetId);

				cmd.DispatchCompute(cs, kernel, Mathf.CeilToInt(desc.width / 8f), Mathf.CeilToInt(desc.height / 8f), 1);
				cmd.Blit(TempTargetId, source);

				cmd.ReleaseTemporaryRT(TempSourceId);
				cmd.ReleaseTemporaryRT(TempTargetId);

				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}
		}
	}
}
