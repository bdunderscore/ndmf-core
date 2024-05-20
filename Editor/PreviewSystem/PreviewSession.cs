#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    public class PreviewSession : IDisposable
    {
        #region Static State

        /// <summary>
        /// The PreviewSession used for any cameras not overriden using `OverrideCamera`.
        /// </summary>
        public static PreviewSession Active { get; set; } = null;


        /// <summary>
        /// Applies this PreviewSession to the `target` camera.
        /// </summary>
        /// <param name="target"></param>
        public void OverrideCamera(Camera target)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes all camera overrides from the `target` camera.
        /// </summary>
        /// <param name="target"></param>
        public static void ClearCameraOverride(Camera target)
        {
            throw new NotImplementedException();
        }

        #endregion

        internal IEnumerable<(Renderer, Renderer)> GetReplacements()
        {
            return _observer._currentInstance?.Replacements ?? Enumerable.Empty<(Renderer, Renderer)>();
        }

        private readonly Sequencer _sequence = new Sequencer();

        internal class Registration
        {
            public IRenderFilter filter;
            public ReactiveValue<Renderer[]> targetRenderers;
        }

        internal class ResolvedRegistration
        {
            public readonly IRenderFilter filter;
            public readonly ImmutableHashSet<Renderer> targets;

            public ResolvedRegistration(IRenderFilter filter, ImmutableHashSet<Renderer> targets)
            {
                this.filter = filter;
                this.targets = targets;
            }
        }

        private ReactiveField<ImmutableDictionary<SequencePoint, Registration>> targets
            = new(ImmutableDictionary<SequencePoint, Registration>.Empty);

        private ReactiveValue<(ImmutableList<SequencePoint>, ImmutableHashSet<Renderer>)> _resolvedState;

        private TargetObserver _observer;
        private IDisposable _session;

        private ReactiveValue<ImmutableList<ResolvedRegistration>> _resolved;

        public PreviewSession()
        {
            _resolved = ReactiveValue<ImmutableList<ResolvedRegistration>>.Create("resolved sequence", async ctx =>
            {
                var targets = await ctx.Observe(this.targets.AsReactiveValue());
                var sequence = await ctx.Observe(_sequence.Sequence);

                // avoid NPEs due to timing issues by checking that targets contains the key here
                var registrations = sequence.Where(p => targets.ContainsKey(p)).Select(p => targets[p]);

                return (await Task.WhenAll(registrations.Select(async r =>
                {
                    var targets = await ctx.Observe(r.targetRenderers);
                    return new ResolvedRegistration(r.filter, targets.ToImmutableHashSet());
                }))).ToImmutableList();
            });

            _observer = new TargetObserver();
            _session = _resolved.Subscribe(_observer);
        }

        internal class TargetObserver : IObserver<ImmutableList<ResolvedRegistration>>
        {
            public PreviewSessionInstance _currentInstance;

            public void OnCompleted()
            {
                _currentInstance.Dispose();
                _currentInstance = null;
            }

            public void OnError(Exception error)
            {
                Debug.LogException(error);
            }

            public void OnNext(ImmutableList<ResolvedRegistration> registrations)
            {
                _currentInstance?.Dispose();
                _currentInstance = null;

                _currentInstance = new PreviewSessionInstance(registrations);
            }
        }

        private int lastUpdateFrame = 0;

        internal void OnUpdate(int updateFrameCount)
        {
            if (lastUpdateFrame != updateFrameCount)
            {
                lastUpdateFrame = updateFrameCount;
                _observer._currentInstance?.Update();
            }
        }

        /// <summary>
        /// Sets the order in which mesh mutations are executed. Any sequence points not listed in this sequence will
        /// be executed after these registered points, in `AddMutator` invocation order.
        /// </summary>
        /// <param name="sequencePoints"></param>
        public void SetSequence(IEnumerable<SequencePoint> sequencePoints)
        {
            
        }

        public IDisposable AddMutator(SequencePoint sequencePoint, IRenderFilter filter,
            ReactiveValue<Renderer[]> targetRenderers)
        {
            _sequence.AddPoint(sequencePoint);

            targets.Value = targets.Value.Add(sequencePoint, new Registration()
            {
                filter = filter,
                targetRenderers = targetRenderers,
            });

            return new RemovalDisposable(this, sequencePoint);
        }

        private class RemovalDisposable : IDisposable
        {
            private PreviewSession _session;
            private SequencePoint _point;
            private Registration _registration;

            public RemovalDisposable(PreviewSession session, SequencePoint point)
            {
                _session = session;
                _point = point;
                _registration = session.targets.Value[point];
            }

            public void Dispose()
            {
                _session.targets.Value = _session.targets.Value.Remove(_point);
            }
        }

        /// <summary>
        /// Returns a new PreviewSession which inherits all mutators from the parent session. Any mutators added to this
        /// new session run after the parent session's mutators.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
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