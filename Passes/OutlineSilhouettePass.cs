using Outline.Constants;
using Outline.Data;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Outline.Passes
{
    public class OutlineSilhouettePass : ScriptableRenderPass
    {
        private class PassData
        {
            public RendererListHandle RendererListHandle;
            public TextureHandle Silhouette;
        }
        
        private const string PassName = "Outline Silhouette Pass";
        private const string SilhouetteTextureName = "_OutlineSilhouetteTexture";
        
        private static readonly ShaderTagId[] ShaderTagIds =
        {
            new("SRPDefaultUnlit"),
            new("UniversalForward"),
            new("UniversalForwardOnly"),
        };

        private readonly OutlineSettings _settings;
        private readonly Material _material;

        public OutlineSilhouettePass(OutlineSettings settings, Material material)
        {
            _settings = settings;
            _material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var outlineData = frameData.GetOrCreate<OutlineData>();

            var cullingResults = renderingData.cullResults;
            var camera = cameraData.camera;
            
            var rendererListDesc = new RendererListDesc(ShaderTagIds, cullingResults, camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque,
                overrideMaterial = _material,
                overrideMaterialPassIndex = ShaderPass.SilhouetteBufferFill,
                layerMask = _settings.LayerMask,
                renderingLayerMask = _settings.RenderingLayerMask,
            };
            var rendererListHandle = renderGraph.CreateRendererList(rendererListDesc);
            var desc = renderGraph.GetTextureDesc(resourceData.cameraColor);
            
            desc.colorFormat = GraphicsFormat.R8_UNorm;
            desc.filterMode = FilterMode.Point;
            desc.depthBufferBits = 0;
            desc.name = SilhouetteTextureName;

            using var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData);
            
            passData.RendererListHandle = rendererListHandle;
            passData.Silhouette = renderGraph.CreateTexture(desc);
            
            builder.UseRendererList(passData.RendererListHandle);
            builder.SetRenderAttachment(passData.Silhouette, 0);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));

            outlineData.Silhouette = passData.Silhouette;
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            context.cmd.BeginSample(PassName);
            context.cmd.ClearRenderTarget(false, true, Color.clear);
            context.cmd.DrawRendererList(data.RendererListHandle);
            context.cmd.EndSample(PassName);
        }
    }
}