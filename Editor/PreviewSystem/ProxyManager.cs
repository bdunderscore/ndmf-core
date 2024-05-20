#region

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// Tracks the proxy meshes created by the preview system.
    /// </summary>
    internal static class ProxyManager
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
            EditorApplication.update += () => { updateCount++; };
        }

        private static int updateCount = 0;
        private static List<(Renderer, bool)> _resetActions = new();

        private static void OnPostRender(Camera cam)
        {
            ResetStates();
        }

        private static void OnPreCull(Camera cam)
        {
            ResetStates();


            var sess = PreviewSession.Active;
            if (sess == null) return;

            sess.OnUpdate(updateCount);

            foreach (var (original, replacement) in sess.GetReplacements())
            {
                _resetActions.Add((original, false));
                _resetActions.Add((replacement, true));

                replacement.forceRenderingOff = false;
                original.forceRenderingOff = true;
            }
        }

        private static void ResetStates()
        {
            foreach (var (renderer, state) in _resetActions)
            {
                if (renderer != null) renderer.forceRenderingOff = state;
            }

            _resetActions.Clear();
        }
    }
}