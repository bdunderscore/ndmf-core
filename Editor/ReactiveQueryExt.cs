namespace nadena.dev.ndmf.ReactiveQuery.unity.editor
{
    public static class ReactiveQueryExt
    {
        public static T Observe<T>(this ComputeContext ctx, T obj) where T : UnityEngine.Object
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            ObjectWatcher.Instance.MonitorObjectProps(out var dispose, obj, _ => invalidate(), onInvalidate);
            onInvalidate.ContinueWith(_ => dispose.Dispose());
            
            return obj;
        }
    }
}