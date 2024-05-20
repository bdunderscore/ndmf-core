#region

using System;
using nadena.dev.ndmf.rq;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// Describes a transformation to be performed on a preview mesh.
    /// </summary>
    public interface IRenderFilter
    {
        public IRenderFilterSession CreateSession();
        public ReactiveValue<Renderer[]> Targets { get; }
    }

    public interface IRenderFilterSession : IDisposable
    {
        /// <summary>
        /// Invoked when the preview mesh is created. Typically, this is the phase to make changes to things like bones,
        /// bounds, etc.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="target"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void SetupRenderer(Renderer original, Renderer target)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Invoked once each editor frame. This is where you can do things like copy over blendshapes, etc.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="target"></param>
        /// <param name="rebuild">Set to true if you request that the mesh be recreated</param>
        public void OnFrame(Renderer original, Renderer target, ref bool rebuild)
        {
        }

        /// <summary>
        /// Invoked each editor frame, once, before the per-renderer OnFrame calls.
        /// </summary>
        public void OnFrameOnce()
        {
        }
    }
}