﻿using XREngine.Rendering;
using XREngine.Scene.Transforms;

namespace XREngine.Components.Capture.Lights.Types
{
    /// <summary>
    /// Base class to handle shadow mapping for a light that only has one view.
    /// </summary>
    public abstract class OneViewLightComponent : LightComponent
    {
        private const uint DefaultResolution = 4096u;

        protected readonly XRViewport _viewport = new(null, DefaultResolution, DefaultResolution)
        {
            RenderPipeline = new ShadowRenderPipeline(),
            SetRenderPipelineFromCamera = false,
            AutomaticallyCollectVisible = false,
            AutomaticallySwapBuffers = false,
            AllowUIRender = false,
            CullWithFrustum = true,
        };

        public override void SetShadowMapResolution(uint width, uint height)
        {
            base.SetShadowMapResolution(width, height);
            _viewport.Resize(width, height);
        }

        protected abstract XRCameraParameters GetCameraParameters();

        public XRCamera? ShadowCamera => _viewport.Camera;

        protected internal override void OnComponentActivated()
        {
            base.OnComponentActivated();

            _viewport.WorldInstanceOverride = World;
            XRCamera cam = new(GetShadowCameraParentTransform(), GetCameraParameters());
            cam.PostProcessing!.ColorGrading.AutoExposure = false;
            cam.PostProcessing.ColorGrading.Exposure = 1.0f;
            _viewport.Camera = cam;

            if (Type == ELightType.Dynamic && CastsShadows && ShadowMap is null)
                SetShadowMapResolution(DefaultResolution, DefaultResolution);
        }

        protected virtual TransformBase GetShadowCameraParentTransform()
            => Transform;

        protected internal override void OnComponentDeactivated()
        {
            _viewport.WorldInstanceOverride = null;
            _viewport.Camera = null;

            base.OnComponentDeactivated();
        }

        public override void SwapBuffers()
        {
            if (!CastsShadows || ShadowMap is null)
                return;

            _viewport.SwapBuffers();
        }
        public override void CollectVisibleItems()
        {
            if (!CastsShadows || ShadowMap is null)
                return;

            _viewport.CollectVisible(false);
        }
        public override void RenderShadowMap(bool collectVisibleNow = false)
        {
            if (!CastsShadows || ShadowMap is null)
                return;

            if (collectVisibleNow)
            {
                CollectVisibleItems();
                SwapBuffers();
            }

            _viewport.Render(ShadowMap, null, null, true, ShadowMap.Material);
        }
    }
}
