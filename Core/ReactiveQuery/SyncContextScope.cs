using System;
using System.Threading;
using System.Threading.Tasks;

namespace nadena.dev.ndmf.ReactiveQuery
{
    public sealed class SyncContextScope : IDisposable
    {
        SynchronizationContext _old = SynchronizationContext.Current;
        
        public SyncContextScope(SynchronizationContext context)
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
        
        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_old);
        }
    }
}