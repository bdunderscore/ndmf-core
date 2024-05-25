#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    public sealed class MeshState
    {
        public Renderer Original { get; }
        public Mesh Mesh { get; set; }
        public ImmutableList<Material> Materials { get; set; }
        public event Action OnDispose;

        private bool _disposed = false;

        internal MeshState(Renderer renderer)
        {
            Original = renderer;

            if (renderer is SkinnedMeshRenderer smr)
            {
                Mesh = Object.Instantiate(smr.sharedMesh);
            }
            else if (renderer is MeshRenderer mr)
            {
                Mesh = Object.Instantiate(mr.GetComponent<MeshFilter>().sharedMesh);
            }

            Materials = renderer.sharedMaterials.Select(m => new Material(m)).ToImmutableList();
        }

        // Not IDisposable as we don't want to expose that as a public API
        internal void Dispose()
        {
            if (_disposed) return;

            Object.DestroyImmediate(Mesh);
            foreach (var material in Materials)
            {
                Object.DestroyImmediate(material);
            }

            OnDispose?.Invoke();
        }
    }

    public interface IRenderFilter
    {
        public ReactiveValue<IImmutableList<IImmutableList<Renderer>>> TargetGroups { get; }

        public Task MutateMeshData(IList<MeshState> state, ComputeContext context)
        {
            return Task.CompletedTask;
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            
        }
    }
}