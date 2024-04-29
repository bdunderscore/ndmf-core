using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace nadena.dev.ndmf.ReactiveQuery.unity.editor
{
    internal sealed class ThrottledSynchronizationContext : SynchronizationContext
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            ReactiveQueryScheduler.SynchronizationContextOverride.Value
                = new ThrottledSynchronizationContext(SynchronizationContext.Current);
        }
        
        private readonly object _lock = new object();
        private readonly SynchronizationContext _parent;
        private Queue<PendingWork> _pendingWork = new Queue<PendingWork>();
        private int _owningThreadId;
        
        // locked:
        private List<PendingWork> _remoteWork = new List<PendingWork>();
        private bool _isQueued = false;
        
        private bool IsRunning { get; set; } = false;

        private bool IsQueued
        {
            get => _isQueued;
            set
            {
                if (value == _isQueued)
                {
                    return;
                }
                
                _isQueued = value;
                if (_isQueued)
                {
                    _parent.Post(RunWithTimeLimit, this);
                }
            }
        }

        public ThrottledSynchronizationContext(SynchronizationContext parent)
        {
            _parent = parent;
            parent.Send(InitThreadId, this);
        }

        private static void InitThreadId(object state)
        {
            var self = (ThrottledSynchronizationContext) state;
            self._owningThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private static void RunWithTimeLimit(object state)
        {
            var self = (ThrottledSynchronizationContext) state;

            lock (self._lock)
            {
                self.IsQueued = false;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            self.RunUntil(() => sw.ElapsedMilliseconds >= 100);
        }

        public void RunUntil(Func<bool> terminationCondition)
        {
            if (_owningThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException("Can only be called from the owning thread");
            }
            
            lock (_lock)
            {
                IsRunning = true;
                _remoteWork.ForEach(_pendingWork.Enqueue);
                _remoteWork.Clear();
            }

            using (TaskThrottle.WithThrottleCondition(terminationCondition))
            {
                int n = 0;
                do
                {
                    _pendingWork.Dequeue().Run();
                    n++;
                } while (_pendingWork.Count > 0 && !terminationCondition());
            }

            lock (_lock)
            {
                IsRunning = false;
                if (_pendingWork.Count > 0)
                {
                    IsQueued = true;
                }
            }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (_owningThreadId == Thread.CurrentThread.ManagedThreadId && IsRunning)
            {
                _pendingWork.Enqueue(new PendingWork(d, state, null));
            }
            else
            {
                lock (_lock)
                {
                    _remoteWork.Add(new PendingWork(d, state, null));
                    IsQueued = true;
                }
            }
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (_owningThreadId == Thread.CurrentThread.ManagedThreadId && IsRunning)
            {
                d(state);
            }
            else
            {
                var waitHandle = new ManualResetEvent(false);
                lock (_lock)
                {
                    _remoteWork.Add(new PendingWork(d, state, waitHandle));
                    IsQueued = true;
                }

                waitHandle.WaitOne();
            }
        }

        private class PendingWork
        {
            public SendOrPostCallback Callback;
            public object State;
            public ManualResetEvent WaitHandle;

            public PendingWork(SendOrPostCallback callback, object state, ManualResetEvent waitHandle)
            {
                Callback = callback;
                State = state;
                WaitHandle = waitHandle;
            }
            
            public void Run()
            {
                try
                {
                    Callback(State);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    WaitHandle?.Set();
                }
            }
        }
    }
}