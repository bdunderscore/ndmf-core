using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace nadena.dev.ndmf.ReactiveQuery
{
    public abstract class ReactiveQuery<T> : IObservable<T>
    {
        protected TaskFactory Scheduler { get; } = Task.Factory;
        
        #region State
        
        private object _lock = new object();
        
        // Locked by _lock
        private long _invalidationCount = 0;
        
        [PublicAPI]

        private CancellationToken _cancellationToken = CancellationToken.None;
        private Task _cancelledTask = null;
        private TaskCompletionSource<object> _invalidated = null;

        private bool _currentValueIsValid = false;
        private Task<T> _valueTask = null;
        // Used to drive DestroyObsoleteValue
        private T _currentValue = default;
        private Exception _currentValueException = null;
        
        #endregion
        
        #region Public API

        public bool TryGetValue(out T value)
        {
            lock (_lock)
            {
                value = default;
                if (!_currentValueIsValid) return false;
                
                value = _currentValue;
                if (_currentValueException != null)
                {
                    throw _currentValueException;
                }

                return true;
            }
        }

        public async Task<T> GetValueAsync()
        {
            while (true)
            {
                try
                {
                    return await RequestCompute();
                }
                catch (TaskCanceledException e)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Returns a task which will complete the next time this task is invalidated. 
        /// </summary>
        public Task Invalidated
        {
            get
            {
                lock (_lock)
                {
                    if (_invalidated == null)
                    {
                        _invalidated = new TaskCompletionSource<object>();
                    }

                    return _invalidated.Task;
                }
            }
        }
        
        #endregion
        
        #region IObservable<T> API
        
        private HashSet<IObserver<T>> _observers = new(new ObjectIdentityComparer<IObserver<T>>());
        
        public IDisposable Subscribe(IObserver<T> observer)
        {
            lock (_lock)
            {
                _observers.Add(observer);

                if (_currentValueIsValid)
                {
                    if (_currentValueException != null)
                    {
                        observer.OnError(_currentValueException);
                    }
                    else
                    {
                        observer.OnNext(_currentValue);
                    }
                }
                else
                {
                    RequestCompute();
                }
            }

            return new RemoveObserver(this, observer);
        }
        
        private class RemoveObserver : IDisposable
        {
            private readonly ReactiveQuery<T> _parent;
            private readonly IObserver<T> _observer;

            public RemoveObserver(ReactiveQuery<T> parent, IObserver<T> observer)
            {
                _parent = parent;
                _observer = observer;
            }

            public void Dispose()
            {
                lock (_parent._lock)
                {
                    _parent._observers.Remove(_observer);
                    _observer.OnCompleted();
                }
            }
        }

        public void Invalidate()
        {
            Invalidate(-1);
        }
        
        #endregion
        
        #region Subclass API
        
        protected abstract Task<T> Compute(ComputeContext context);

        protected virtual void DestroyObsoleteValue(T value)
        {
            // no-op
        }

        // Implementing ToString is mandatory for all subclasses
        public abstract override string ToString();
        
        #endregion
        
        #region Internal API

        internal void Invalidate(long expectedSeq)
        {
            TaskCompletionSource<object> invalidationToken = null;
            
            lock (_lock)
            {
                if (expectedSeq == _invalidationCount || expectedSeq == -1)
                {
                    if (_valueTask != null && !_valueTask.IsCompleted)
                    {
                        _cancelledTask = _valueTask;
                    }

                    invalidationToken = _invalidated;
                    _invalidated = null;
                    _invalidationCount++;
                    _valueTask = null;

                    _currentValueIsValid = false;
                }
            }
            
            // This triggers invalidation of downstream queries (as well as potentially other user code), so drop the
            // lock before invoking it...
            invalidationToken?.SetResult(null);
        }

        internal async Task<T> ComputeInternal(ComputeContext context)
        {
            long seq = _invalidationCount;

            Task cancelledTask;
            lock (_lock)
            {
                cancelledTask = _cancelledTask;
                _cancelledTask = null;
            }

            // Ensure we don't ever have multiple instances of the same RQ computation running in parallel
            if (cancelledTask != null)
            {
                await cancelledTask.ContinueWith(_ => { }); // swallow exceptions
            }

            T result;
            ExceptionDispatchInfo e;
            try
            {
                result = await Compute(context);
                e = null;
            }
            catch (Exception ex)
            {
                result = default;
                e = ExceptionDispatchInfo.Capture(ex);
            }

            lock (_lock)
            {
                if (_invalidationCount == seq)
                {
                    if (e == null && !ReferenceEquals(result, _currentValue))
                    {
                        DestroyObsoleteValue(_currentValue);
                    }
                    
                    _currentValue = result;
                    _currentValueException = e?.SourceException;
                    _currentValueIsValid = true;
                    
                    foreach (var observer in _observers)
                    {
                        if (e != null)
                        {
                            observer.OnError(e.SourceException);
                        }
                        else
                        {
                            observer.OnNext(result);
                        }
                    }
                }
            }

            e?.Throw();
            return result;
        }

        internal Task<T> RequestCompute()
        {
            lock (_lock)
            {
                if (_valueTask == null)
                {
                    var context = new ComputeContext(() => ToString());

                    var invalidateSeq = _invalidationCount;
                    context.Invalidate = () => Invalidate(invalidateSeq);
                    // TODO: arrange for cancellation when we invalidate the task
                    context.CancellationToken = new CancellationToken();

                    // _context.Activate();
                    _valueTask = Scheduler.StartNew(() => ComputeInternal(context), context.CancellationToken).Unwrap();
                }

                return _valueTask;
            }
        }

        #endregion
    }
}