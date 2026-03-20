using System;
using Outline.Passes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Outline
{
    [Serializable]
    public class OutlineSettings
    {
        public LayerMask LayerMask = ~0;
        public RenderingLayerMask RenderingLayerMask = 1 << 7;
        [Space(10)]
        [ColorUsage(true, true)] 
        public Color OutlineColor = Color.white;
        [Range(0.0f, 128.0f)] 
        public float OutlinePixelWidth = 4f;
    }

    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/Outline";
        
        [SerializeField]
        private RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        [SerializeField] 
        private OutlineSettings _settings;
        
        private OutlineStencilPass _outlineStencilPass;
        private OutlineSilhouettePass _outlineSilhouettePass;
        private OutlineJumpFloodPass _outlineJumpFloodPass;
        private Shader _outlineShader;
        private Material _outlineMaterial;

        public override void Create()
        {
#if UNITY_EDITOR
            _outlineShader = Shader.Find(ShaderName);
#endif
            
            CoreUtils.Destroy(_outlineMaterial);
            _outlineMaterial = CoreUtils.CreateEngineMaterial(_outlineShader);
            
            if (_outlineMaterial == null)
            {
                Debug.LogError("LMAO пососи!");
                return;
            }
            
            _settings ??= new OutlineSettings();

            _outlineStencilPass = new OutlineStencilPass(_settings, _outlineMaterial);
            _outlineStencilPass.renderPassEvent = _renderPassEvent;
            _outlineSilhouettePass = new OutlineSilhouettePass(_settings, _outlineMaterial);
            _outlineSilhouettePass.renderPassEvent = _renderPassEvent;
            _outlineJumpFloodPass = new OutlineJumpFloodPass(_settings, _outlineMaterial);
            _outlineJumpFloodPass.renderPassEvent = _renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_settings.OutlineColor.a <= 0.0f || _settings.OutlinePixelWidth <= 0.0f)
            {
                return;
            }

            if (renderingData.cameraData.camera.cameraType is not (CameraType.Game or CameraType.SceneView))
            {
                return;
            }
            
            renderer.EnqueuePass(_outlineStencilPass);
            renderer.EnqueuePass(_outlineSilhouettePass);
            renderer.EnqueuePass(_outlineJumpFloodPass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            
            CoreUtils.Destroy(_outlineMaterial);
        }
    }
}