﻿using System.Drawing;
using System.Numerics;
using XREngine.Components;
using XREngine.Components.Lights;
using XREngine.Data;
using XREngine.Data.Geometry;
using XREngine.Data.Rendering;
using XREngine.Data.Trees;
using XREngine.Rendering;
using XREngine.Rendering.Commands;
using XREngine.Rendering.Info;

namespace XREngine.Scene
{
    /// <summary>
    /// Represents a scene with special optimizations for rendering in 3D.
    /// </summary>
    public class VisualScene3D : VisualScene
    {
        public Octree<RenderInfo3D> RenderTree { get; } = new Octree<RenderInfo3D>(new AABB());

        public void SetBounds(AABB bounds)
        {
            RenderTree.Remake(bounds);
            //Lights.LightProbeTree.Remake(bounds);
        }

        public override IRenderTree GenericRenderTree => RenderTree;

        public override void DebugRender(XRCamera? camera, bool onlyContainingItems = false)
            => RenderTree.DebugRender(camera?.WorldFrustum(), onlyContainingItems, RenderAABB);

        private void RenderAABB(Vector3 extents, Vector3 center, Color color)
            => Engine.Rendering.Debug.RenderAABB(extents, center, false, color);

        public void RaycastAsync(
            Segment worldSegment,
            SortedDictionary<float, List<(RenderInfo3D item, object? data)>> items,
            Func<RenderInfo3D, Segment, (float? distance, object? data)> directTest,
            Action<SortedDictionary<float, List<(RenderInfo3D item, object? data)>>> finishedCallback)
            => RenderTree.RaycastAsync(worldSegment, items, directTest, finishedCallback);

        public override void CollectRenderedItems(
            RenderCommandCollection meshRenderCommands,
            XRCamera? camera,
            bool cullWithFrustum,
            Func<XRCamera>? cullingCameraOverride,
            IVolume? collectionVolumeOverride,
            bool collectMirrors)
        {
            XRCamera? cullingCamera = cullingCameraOverride?.Invoke() ?? camera;
            IVolume? collectionVolume = collectionVolumeOverride ?? (cullWithFrustum ? cullingCamera?.WorldFrustum() : null);
            CollectRenderedItems(meshRenderCommands, collectionVolume, camera, collectMirrors);
        }
        public void CollectRenderedItems(RenderCommandCollection commands, IVolume? collectionVolume, XRCamera? camera, bool collectMirrors)
        {
            bool IntersectionTest(RenderInfo3D item, IVolume? cullingVolume, bool containsOnly)
                => item.AllowRender(cullingVolume, commands, camera, containsOnly, collectMirrors);

            void AddRenderCommands(ITreeItem item)
            {
                if (item is RenderInfo renderable)
                    renderable.CollectCommands(commands, camera);
            }

            RenderTree.CollectVisible(collectionVolume, false, AddRenderCommands, IntersectionTest);
        }

        public IReadOnlyList<RenderInfo3D> Renderables => _renderables;
        private readonly List<RenderInfo3D> _renderables = [];
        public void AddRenderable(RenderInfo3D renderable)
        {
            _renderables.Add(renderable);
            RenderTree.Add(renderable);
        }
        public void RemoveRenderable(RenderInfo3D renderable)
        {
            _renderables.Remove(renderable);
            RenderTree.Remove(renderable);
        }

        public override IEnumerator<RenderInfo> GetEnumerator()
        {
            foreach (var renderable in _renderables)
                yield return renderable;
        }
    }
}