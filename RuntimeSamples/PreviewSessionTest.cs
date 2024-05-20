#region

using System;
using System.Collections.Generic;
using nadena.dev.ndmf.preview;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.rq.Samples
{
    [ExecuteInEditMode]
    public class PreviewSessionTest : MonoBehaviour
    {
        private PreviewSession _session = new PreviewSession();
        private SequencePoint _point;
        private IDisposable _remove;


        private void OnEnable()
        {
            PreviewSession.Active = _session;

            if (_point == null)
            {
                _point = new SequencePoint();
            }

            _remove = _session.AddMutator(_point, new Filter(),
                new ReactiveField<Renderer[]>(new[] { GetComponent<Renderer>() }).AsReactiveValue());
        }

        private void OnDisable()
        {
            PreviewSession.Active = null;

            _remove?.Dispose();
            _remove = null;
        }

        private class Filter : IRenderFilter
        {
            public IRenderFilterSession CreateSession()
            {
                Debug.Log("=== session init ===");

                return new FilterSession();
            }
        }

        private class FilterSession : IRenderFilterSession
        {
            private Dictionary<Material, Material> _mats = new Dictionary<Material, Material>();

            public void SetupRenderer(Renderer original, Renderer target)
            {
            }

            public void OnFrame(Renderer original, Renderer target, ref bool rebuild)
            {
                var mat = original.sharedMaterial;
                if (!_mats.TryGetValue(mat, out var replacement))
                {
                    replacement = new Material(mat);
                    _mats[mat] = replacement;
                }

                target.sharedMaterial = replacement;

                replacement.color = mat.color * new Color(1, 0.2f, 0.2f, 1f);
            }

            public void Dispose()
            {
                foreach (var mat in _mats.Values)
                {
                    DestroyImmediate(mat);
                }
            }
        }
    }
}