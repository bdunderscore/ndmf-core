using System;
using UnityEngine;

namespace nadena.dev.ndmf.reactive_query.core.PreviewSystem
{
    public class PreviewSession : IDisposable
    {
        public PreviewSession Active { get; set; } = null;
        
        public void OverrideCamera(Camera target)
        {
            
        }

        public void ClearCameraOverride(Camera target)
        {
            
        }

        public void AddMutator(RenderFilter mutator)
        {
            
        }

        public void RemoveMutator(RenderFilter mutator)
        {
            
        }

        public PreviewSession Fork()
        {
            throw new NotImplementedException(); 
        }
        
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}