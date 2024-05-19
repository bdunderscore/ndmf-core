using System;
using UnityEngine;

namespace nadena.dev.ndmf.reactive_query.core.PreviewSystem
{
    public sealed class RenderFilter
    {
        private RenderFilter()
        {
        }
        
        public static RenderFilter At(SequencePoint point)
        {
            throw new NotImplementedException();
        }
        
        public RenderFilter OnInit(Func<Renderer, bool> action)
        {
            throw new NotImplementedException();
        }
        
        public RenderFilter OnFrame(Action<Renderer> action)
        {
            throw new NotImplementedException();
        }
    }
}