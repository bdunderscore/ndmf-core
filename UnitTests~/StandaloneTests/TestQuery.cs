using System;
using System.Threading.Tasks;
using nadena.dev.ndmf.ReactiveQuery;

namespace nadena.dev.ndmf.ReactiveQuery.StandaloneTests
{
    internal class TestQuery<T> : ReactiveQuery<T>
    {
        private Func<ComputeContext, Task<T>> _value;

        public Func<ComputeContext, Task<T>> Value
        {
            get => _value;
            set
            {
                _value = value;
                Invalidate();
            }
        }
        
        public TestQuery(Func<ComputeContext, Task<T>> func)
        {
            _value = func;
        }
        
        protected override async Task<T> Compute(ComputeContext context)
        {
            return await _value(context);
        }

        public override string ToString()
        {
            return "TestQuery";
        }
    }
}