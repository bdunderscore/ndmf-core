using System;
using System.Threading.Tasks;

namespace nadena.dev.ndmf.ReactiveQuery.StandaloneTests
{
    public static class TestHelpers
    {
        public static async Task<T> Timeout<T>(this Task<T> task, int timeout = 1000)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                return task.Result; 
            }
            else
            {
                throw new TimeoutException("Task did not complete in time");
            }
        } 
    }
}