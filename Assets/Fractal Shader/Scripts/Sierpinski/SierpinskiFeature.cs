using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
	/// <summary>
	/// URP Feature that executes the Sierpinski Compute Shader as a post-processing effect.
	/// </summary>
	public class SierpinskiFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public class Settings
		{
			public ComputeShader computeShader;
		}

		public Settings settings = new Settings();
		private SierpinskiPass renderPass;

		public override void Create()
		{
			renderPass = new SierpinskiPass(settings)
			{
				renderPassEvent = RenderPassEvent.AfterRenderingSkybox
			};
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (settings.computeShader == null || Sierpinski.Instance == null) return;
			if (renderingData.cameraData.cameraType != UnityEngine.CameraType.Game) return;

			renderer.EnqueuePass(renderPass);
		}

		class SierpinskiPass : ScriptableRenderPass
		{
			private Settings settings;
			private static readonly int TempSourceId = Shader.PropertyToID("_SierpinskiSource");
			private static readonly int TempTargetId = Shader.PropertyToID("_SierpinskiTarget");

			public SierpinskiPass(Settings settings) { this.settings = settings; }

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (Sierpinski.Instance == null) return;
				var fractal = Sierpinski.Instance;
				FractalRenderGraph.Record(renderGraph, frameData, "Sierpinski Compute Pass", settings.computeShader, (cmd, kernel, width, height) =>
				{
					cmd.SetComputeFloatParam(settings.computeShader, "_Division", fractal.divisionFactor);
					cmd.SetComputeFloatParam(settings.computeShader, "_Position", fractal.cutoutPosition);
					cmd.SetComputeFloatParam(settings.computeShader, "_HoleSize", fractal.holeSize);
					cmd.SetComputeIntParam(settings.computeShader, "_Iterations", fractal.iterations);
					cmd.SetComputeVectorParam(settings.computeShader, "_FractalColor", fractal.fractalColor);
					cmd.SetComputeIntParam(settings.computeShader, "_Width", width);
					cmd.SetComputeIntParam(settings.computeShader, "_Height", height);
				});
			}

			private void ExecuteLegacy(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				CommandBuffer cmd = CommandBufferPool.Get("SierpinskiFractalPass");
				var renderer = renderingData.cameraData.renderer;
				RenderTargetIdentifier source = BuiltinRenderTextureType.CameraTarget;
				RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

				// Prepare descriptors for Compute Shader compatibility
				desc.msaaSamples = 1;
				desc.depthBufferBits = 0;

				cmd.GetTemporaryRT(TempSourceId, desc);
				cmd.Blit(source, TempSourceId);

				desc.enableRandomWrite = true;
				cmd.GetTemporaryRT(TempTargetId, desc);

				ComputeShader cs = settings.computeShader;
				int kernel = cs.FindKernel("CSMain");
				var s = Sierpinski.Instance;

				// Dispatch Parameters to GPU
				cmd.SetComputeFloatParam(cs, "_Division", s.divisionFactor);
				cmd.SetComputeFloatParam(cs, "_Position", s.cutoutPosition);
				cmd.SetComputeFloatParam(cs, "_HoleSize", s.holeSize);
				cmd.SetComputeIntParam(cs, "_Iterations", s.iterations);
				cmd.SetComputeVectorParam(cs, "_FractalColor", s.fractalColor);
				cmd.SetComputeIntParam(cs, "_Width", desc.width);
				cmd.SetComputeIntParam(cs, "_Height", desc.height);

				cmd.SetComputeTextureParam(cs, kernel, "Source", TempSourceId);
				cmd.SetComputeTextureParam(cs, kernel, "Texture", TempTargetId);

				// Execution
				int groupsX = Mathf.CeilToInt(desc.width / 8f);
				int groupsY = Mathf.CeilToInt(desc.height / 8f);
				cmd.DispatchCompute(cs, kernel, groupsX, groupsY, 1);

				cmd.Blit(TempTargetId, source);

				// Cleanup
				cmd.ReleaseTemporaryRT(TempSourceId);
				cmd.ReleaseTemporaryRT(TempTargetId);
				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}
		}
	}
}
