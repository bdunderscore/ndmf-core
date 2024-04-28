using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace nadena.dev.ndmf.ReactiveQuery
{
    internal sealed class SynchronousTaskScheduler : TaskScheduler
    {
        internal static SynchronousTaskScheduler Instance { get; } = new SynchronousTaskScheduler();
        
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Array.Empty<Task>();
        }

        protected override void QueueTask(Task task)
        {
            base.TryExecuteTask(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            base.TryExecuteTask(task);
            return true;
        }
    }
}