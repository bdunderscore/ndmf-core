using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnitTests
{
    public class BasicQueryTest
    {
        [Test]
        public void TrivialQuery()
        {
            var q = new TestQuery<int>(_ => Task.FromResult(42));

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
            
            var q = new TestQuery<int>(_ => Task.FromResult(value));

            Assert.AreEqual(1, await q.GetValueAsync());
            value = 2;
            Assert.AreEqual(1, await q.GetValueAsync());
            q.Invalidate();
        }

        [Test]
        public async Task ChainedInvalidation()
        {
            int value = 1;
            
            var q1 = new TestQuery<int>(_ => Task.FromResult(value));
            var q2 = new TestQuery<int>(async ctx => await ctx.Observe(q1));
            
            Assert.AreEqual(1, await q2.GetValueAsync());

            value = 2;
            q1.Invalidate();
            
            Assert.AreEqual(2, await q2.GetValueAsync());
        }
        
        [Test]
        public void TaskDelay()
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            var q = new TestQuery<int>(_ => tcs.Task);
            var q2 = new TestQuery<int>(async ctx => await ctx.Observe(q));

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
            var q = new TestQuery<int>(_ => tcs.Task);

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

        [Test]
        public void ObserveMultipleQueries()
        {
            var q1 = new TestQuery<int>(_ => Task.FromResult(1));
            var q2 = new TestQuery<int>(_ => Task.FromResult(2));
            var q3 = new TestQuery<int>(async ctx => await ctx.Observe(q1) + await ctx.Observe(q2));
            
            Assert.AreEqual(3, q3.GetValueAsync().Result);
            
            q2.Value = _ => Task.FromResult(30);
            Assert.AreEqual(31, q3.GetValueAsync().Result);
            
            q1.Value = _ => Task.FromResult(10);
            Assert.AreEqual(40, q3.GetValueAsync().Result);
        }

        [Test]
        public void StopObserving()
        {
            var counter = 1;
            var q1 = new TestQuery<int>(_ => Task.FromResult(counter++));

            var shouldCheck = new TestQuery<bool>(_ => Task.FromResult(true));
            var q2 = new TestQuery<int>(async ctx =>
            {
                var check = await ctx.Observe(shouldCheck);
                if (check)
                {
                    return await ctx.Observe(q1);
                }
                else
                {
                    return 0;
                }
            });
            
            Assert.AreEqual(1, q2.GetValueAsync().Result);
            q1.Invalidate();
            Assert.AreEqual(2, q2.GetValueAsync().Result);
            shouldCheck.Value = _ => Task.FromResult(false);
            Assert.AreEqual(0, q2.GetValueAsync().Result);
            q1.Invalidate();
            Assert.AreEqual(0, q2.GetValueAsync().Result);
        }
    }
}