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
            PreviewSession.Current = _session;

            if (_point == null)
            {
                _point = new SequencePoint();
            }

            _remove = _session.AddMutator(_point, new Filter(GetComponent<Renderer>()));
        }

        private void OnDisable()
        {
            PreviewSession.Current = null;

            _remove?.Dispose();
            _remove = null;
        }

        private class Filter : IRenderFilter
        {
            private ReactiveField<Renderer[]> _targetRenderer;

            public Filter(Renderer target)
            {
                _targetRenderer = new(new[] { target });
            }
            
            public IRenderFilterSession CreateSession()
            {
                Debug.Log("=== session init ===");

                return new FilterSession();
            }

            public ReactiveValue<Renderer[]> Targets => _targetRenderer.AsReactiveValue();
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