using Outline.Constants;
using Outline.Data;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Outline.Passes
{
    public class OutlineJumpFloodPass : ScriptableRenderPass
    {
        private class PassData
        {
            public TextureHandle Color;
            public TextureHandle Stencil;
            public TextureHandle Silhouette;
            public TextureHandle Nearest;
            public TextureHandle NearestPingPong;
            public Material Material;
            public int Iterations;
            public Color OutlineColor;
            public float OutlinePixelWidth;
        }

        private const string PassName = "Outline Jump Flood Pass";
        private const string NearestTextureName = "_NearestPoint";
        private const string NearestPingPongName = "_NearestPointPingPong";

        private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");
        private static readonly int AxisWidthID = Shader.PropertyToID("_AxisWidth");
        private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");

        private readonly OutlineSettings _settings;
        private readonly Material _material;

        public OutlineJumpFloodPass(OutlineSettings settings, Material material)
        {
            _settings = settings;
            _material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var outlineData = frameData.GetOrCreate<OutlineData>();

            var desc = renderGraph.GetTextureDesc(resourceData.cameraColor);
            desc.msaaSamples = MSAASamples.None;
            desc.colorFormat = GraphicsFormat.R16G16_SNorm;
            desc.filterMode = FilterMode.Point;

            using var builder = renderGraph.AddUnsafePass<PassData>(PassName, out var passData);
            
            passData.Color = resourceData.cameraColor;
            passData.Stencil = resourceData.cameraDepth;
            passData.Silhouette = outlineData.Silhouette;
            
            desc.name = NearestTextureName;
            passData.Nearest = renderGraph.CreateTexture(desc);
            
            desc.name = NearestPingPongName;
            passData.NearestPingPong = renderGraph.CreateTexture(desc);
            
            passData.Material = _material;
            
            var numMips = Mathf.CeilToInt(Mathf.Log(_settings.OutlinePixelWidth + 1.0f, 2f));
            passData.Iterations = numMips - 1;
            
            passData.OutlineColor = _settings.OutlineColor;
            passData.OutlinePixelWidth = _settings.OutlinePixelWidth;
            
            builder.UseTexture(passData.Stencil);
            builder.UseTexture(passData.Silhouette);
            builder.UseTexture(passData.Color, AccessFlags.Write);
            builder.UseTexture(passData.Nearest, AccessFlags.ReadWrite);
            builder.UseTexture(passData.NearestPingPong, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
        }

        private static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            
            cmd.BeginSample(PassName);
            
            var adjustedOutlineColor = data.OutlineColor;
            adjustedOutlineColor.a *= Mathf.Clamp01(data.OutlinePixelWidth);
                
            cmd.SetGlobalColor(OutlineColorID, adjustedOutlineColor.linear);
            cmd.SetGlobalFloat(OutlineWidthID, Mathf.Max(1f, data.OutlinePixelWidth));

            Blitter.BlitCameraTexture(cmd, data.Silhouette, data.Nearest, data.Material, ShaderPass.JfaInit);
                    
            for (var i = data.Iterations; i >= 0; i--)
            {
                var stepWidth = Mathf.Pow(2, i) + 0.5f;

                cmd.SetGlobalVector(AxisWidthID, new Vector2(stepWidth, 0f));
                Blitter.BlitCameraTexture(cmd, data.Nearest, data.NearestPingPong, data.Material, ShaderPass.JfaFloodSingleAxis);
                cmd.SetGlobalVector(AxisWidthID, new Vector2(0f, stepWidth));
                Blitter.BlitCameraTexture(cmd, data.NearestPingPong, data.Nearest, data.Material, ShaderPass.JfaFloodSingleAxis);
            }
                
            cmd.SetRenderTarget(data.Color, data.Stencil);
            cmd.SetGlobalTexture(BlitTexture, data.Nearest);
            cmd.DrawProcedural(Matrix4x4.identity, data.Material, ShaderPass.JfaOutline, MeshTopology.Triangles, 3, 1);
            
            cmd.EndSample(PassName);
        }
    }
}