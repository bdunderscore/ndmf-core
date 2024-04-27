using System;
using System.Threading;
using System.Threading.Tasks;
using nadena.dev.ndmf.ReactiveQuery;
using NUnit.Framework;

namespace UnitTests
{
    class TestQuery : ReactiveQuery<int>
    {
        private Func<ComputeContext, Task<int>> _value;

        public Func<ComputeContext, Task<int>> Value
        {
            get => _value;
            set
            {
                _value = value;
                Invalidate();
            }
        }
        
        public TestQuery(Func<ComputeContext, Task<int>> func)
        {
            _value = func;
        }
        
        protected override async Task<int> Compute(ComputeContext context)
        {
            return await _value(context);
        }

        public override string ToString()
        {
            return "TestQuery";
        }
    }
    
    public class BasicQueryTest
    {
        [Test]
        public void TrivialQuery()
        {
            var q = new TestQuery(_ => Task.FromResult(42));

            Assert.IsFalse(q.TryGetValue(out var _));
            var task = q.GetValueAsync();

            Task.WaitAll(new Task[] {task}, 1000);
            
            Assert.AreEqual(42, task.Result);
            Assert.IsTrue(q.TryGetValue(out var result));
            Assert.AreEqual(42, result);
        }

        [Test]
        public async Task CacheAndInvalidate()
        {
            int value = 1;
            
            var q = new TestQuery(_ => Task.FromResult(value));

            Assert.AreEqual(1, await q.GetValueAsync());
            value = 2;
            Assert.AreEqual(1, await q.GetValueAsync());
            q.Invalidate();
        }

        [Test]
        public async Task ChainedInvalidation()
        {
            int value = 1;
            
            var q1 = new TestQuery(_ => Task.FromResult(value));
            var q2 = new TestQuery(async ctx => await ctx.Observe(q1));
            
            Assert.AreEqual(1, await q2.GetValueAsync());

            value = 2;
            q1.Invalidate();
            
            Assert.AreEqual(2, await q2.GetValueAsync());
        }
        
        [Test]
        public void TaskDelay()
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            var q = new TestQuery(_ => tcs.Task);
            var q2 = new TestQuery(async ctx => await ctx.Observe(q));

            var t2 = q2.GetValueAsync();
            Task.WaitAll(new Task[] {t2}, 100);
            Assert.IsFalse(t2.IsCompleted);
            
            tcs.SetResult(42);
            Task.WaitAll(new Task[] {t2}, 100);
            Assert.IsTrue(t2.IsCompleted);
            Assert.AreEqual(42, t2.Result);
        }

        [Test]
        public void CancellationAwaitsPreviousExecution()
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            var q = new TestQuery(_ => tcs.Task);

            var task = q.GetValueAsync();
            Task.WaitAll(new Task[] {task}, 100);
            
            q.Value = _ => Task.FromResult(42);

            var task2 = q.GetValueAsync();
            Task.WaitAll(new Task[] {task2}, 100);
            Assert.IsFalse(task2.IsCompleted);
            
            tcs.SetResult(123);
            Task.WaitAll(new Task[] {task2}, 100);
            Assert.IsTrue(task2.IsCompleted);
            Assert.AreEqual(42, task2.Result);
        }
    }
}