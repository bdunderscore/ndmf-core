namespace nadena.dev.ndmf.ReactiveQuery.unity.editor
{
    public static class ReactiveQueryExt
    {
        public static T Observe<T>(this ComputeContext ctx, T obj) where T : UnityEngine.Object
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            ChangeStreamMonitor.Instance.Monitor(obj, invalidate, onInvalidate);

            return obj;;
        }
    }
}