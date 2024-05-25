#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
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
            private ReactiveField<IImmutableList<IImmutableList<Renderer>>> _targetRenderer;

            public Filter(Renderer target)
            {
                _targetRenderer = new(
                    ImmutableList<IImmutableList<Renderer>>.Empty.Add(
                        ImmutableList<Renderer>.Empty.Add(target)
                    )
                );
            }

            public ReactiveValue<IImmutableList<IImmutableList<Renderer>>> TargetGroups =>
                _targetRenderer.AsReactiveValue();

            public Task MutateMeshData(IList<MeshState> state, ComputeContext context)
            {
                foreach (var mesh in state)
                {
                    var mat = new Material(mesh.Materials[0]);
                    mat.color = mat.color * new Color(1, 0.2f, 0.2f, 1f);
                    mesh.Materials = mesh.Materials.SetItem(0, mat);
                }

                return Task.CompletedTask;
            }
        }
    }
}