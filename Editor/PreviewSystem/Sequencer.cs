#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal sealed class Sequencer
    {
        private readonly HashSet<SequencePoint> _discovered = new();
        private readonly List<SequencePoint> _discoveryOrder = new();
        private readonly Dictionary<SequencePoint, int> _resolvedOrder = new();

        private ReactiveValue<ImmutableList<SequencePoint>> _finalResolved;

        public ReactiveValue<ImmutableList<SequencePoint>> Sequence => _finalResolved;

        public Sequencer()
        {
            _finalResolved = ReactiveValue<ImmutableList<SequencePoint>>.Create("Sequencer.Sequence", _ =>
            {
                return Task.FromResult(
                    _resolvedOrder
                        .OrderBy(kvp => kvp.Value)
                        .Select(kvp => kvp.Key)
                        .ToImmutableList());
            });
        }

        public void SetSequence(IEnumerable<SequencePoint> points)
        {
            _resolvedOrder.Clear();

            foreach (var point in points)
            {
                if (_discovered.Add(point))
                {
                    _discoveryOrder.Add(point);
                }

                _resolvedOrder[point] = _resolvedOrder.Count;
            }

            foreach (var point in _discoveryOrder)
            {
                if (!_resolvedOrder.ContainsKey(point))
                {
                    _resolvedOrder[point] = _resolvedOrder.Count;
                }
            }

            _finalResolved.Invalidate();
        }

        public void AddPoint(SequencePoint point)
        {
            if (_discovered.Add(point))
            {
                _discoveryOrder.Add(point);
                _resolvedOrder[point] = _resolvedOrder.Count;
            }

            _finalResolved.Invalidate();
        }
    }
}