using Outline.Constants;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Outline.Passes
{
    public class OutlineStencilPass : ScriptableRenderPass
    {
        private class PassData
        {
            public RendererListHandle RendererListHandle;
        }
        
        private const string PassName = "Outline Stencil Pass";
        
        private static readonly ShaderTagId[] ShaderTagIds =
        {
            new("SRPDefaultUnlit"),
            new("UniversalForward"),
            new("UniversalForwardOnly"),
        };

        private readonly OutlineSettings _settings;
        private readonly Material _material;

        public OutlineStencilPass(OutlineSettings settings, Material material)
        {
            _settings = settings;
            _material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            var cullingResults = renderingData.cullResults;
            var camera = cameraData.camera;

            var rendererListDesc = new RendererListDesc(ShaderTagIds, cullingResults, camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque,
                overrideMaterial = _material,
                overrideMaterialPassIndex = ShaderPass.InteriorStencil,
                layerMask = _settings.LayerMask,
                renderingLayerMask = _settings.RenderingLayerMask,
            };
            var rendererListHandle = renderGraph.CreateRendererList(rendererListDesc);
            
            using var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData);
            
            passData.RendererListHandle = rendererListHandle;
            
            builder.UseRendererList(in rendererListHandle);
            builder.SetRenderAttachment(resourceData.cameraColor, 0);
            builder.SetRenderAttachmentDepth(resourceData.cameraDepth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            context.cmd.BeginSample(PassName);
            context.cmd.ClearRenderTarget(RTClearFlags.Stencil, Color.clear, 1f, 0);
            context.cmd.DrawRendererList(data.RendererListHandle);
            context.cmd.EndSample(PassName);
        }
    }
}