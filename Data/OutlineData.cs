using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Outline.Data
{
    public class OutlineData : ContextItem
    {
        public TextureHandle Silhouette;
        
        public override void Reset()
        {
            Silhouette = TextureHandle.nullHandle;
        }
    }
}
