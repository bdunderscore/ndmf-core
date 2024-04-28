using System.Threading.Tasks;

namespace nadena.dev.ndmf.ReactiveQuery
{
    public static class ReactiveQueryScheduler
    {
        public static TaskFactory DefaultTaskFactory { get; set; } = new TaskFactory(TaskScheduler.Default);
    }
}