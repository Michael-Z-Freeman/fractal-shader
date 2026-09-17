using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FractalShader
{
    /// <summary>Schedules the package's screen-space compute effects using the URP Render Graph API.</summary>
    internal static class FractalRenderGraph
    {
        private sealed class PassData
        {
            internal ComputeShader shader;
            internal int kernel;
            internal TextureHandle source;
            internal TextureHandle target;
            internal int width;
            internal int height;
            internal Action<ComputeCommandBuffer, int, int, int> configure;
        }

        internal static void Record(
            RenderGraph renderGraph,
            ContextContainer frameData,
            string passName,
            ComputeShader shader,
            Action<ComputeCommandBuffer, int, int, int> configure)
        {
            if (shader == null)
                return;

            var resources = frameData.Get<UniversalResourceData>();
            var source = resources.activeColorTexture;
            var descriptor = source.GetDescriptor(renderGraph);
            descriptor.name = passName + " Target";
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.enableRandomWrite = true;
            var target = renderGraph.CreateTexture(descriptor);

            using (var builder = renderGraph.AddComputePass<PassData>(passName, out var passData))
            {
                passData.shader = shader;
                passData.kernel = shader.FindKernel("CSMain");
                passData.source = source;
                passData.target = target;
                passData.width = descriptor.width;
                passData.height = descriptor.height;
                passData.configure = configure;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(target, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, ComputeGraphContext context) =>
                {
                    context.cmd.SetComputeTextureParam(data.shader, data.kernel, "Source", data.source);
                    context.cmd.SetComputeTextureParam(data.shader, data.kernel, "Texture", data.target);
                    data.configure?.Invoke(context.cmd, data.kernel, data.width, data.height);
                    context.cmd.DispatchCompute(
                        data.shader,
                        data.kernel,
                        Mathf.CeilToInt(data.width / 8f),
                        Mathf.CeilToInt(data.height / 8f),
                        1);
                });
            }

            resources.cameraColor = target;
        }
    }
}
