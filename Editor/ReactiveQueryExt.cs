#region

using System;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    public static class ReactiveQueryExt
    {
        /// <summary>
        /// Monitors a given Unity object for changes, and recomputes when changes are detected.
        ///
        /// This will recompute when properties of the object change, when the object is destroyed, or (in the case of
        /// a GameObject), when the children of the GameObject changed.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Observe<T>(this ComputeContext ctx, T obj) where T : Object
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            ObjectWatcher.Instance.MonitorObjectProps(out var dispose, obj, _ => invalidate(), onInvalidate);
            onInvalidate.ContinueWith(_ => dispose.Dispose());
            
            return obj;
        }
        
        public static C GetComponent<C>(this ComputeContext ctx, GameObject obj) where C : Component
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponent(out var dispose, obj, _ => invalidate(), onInvalidate,
                () => obj != null ? obj.GetComponent<C>() : null);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        /* TODO - need to monitor for component holder
        public static C GetComponent<C>(this ComputeContext ctx, Component obj) where C : Component
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var objHolder = obj.gameObject;
            var c1 = ObjectWatcher.Instance.MonitorObjectProps()
            
            var c = ObjectWatcher.Instance.MonitorGetComponent(out var dispose, obj.gameObject, _ => invalidate(), onInvalidate, () => obj.GetComponent<C>());
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
            
        }
        */
        
        public static Component GetComponent(this ComputeContext ctx, GameObject obj, Type type)
        {
            if (obj == null) return null;
            
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponent(out var dispose, obj, _ => invalidate(), onInvalidate,
                () => obj != null ? obj.GetComponent(type) : (Component)null);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static C[] GetComponents<C>(this ComputeContext ctx, GameObject obj) where C : Component
        {
            if (obj == null) return Array.Empty<C>();
            
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate,
                () => obj != null ? obj.GetComponents<C>() : Array.Empty<C>(), false);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static Component[] GetComponents(this ComputeContext ctx, GameObject obj, Type type)
        {
            if (obj == null) return Array.Empty<Component>();
            
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate,
                () => obj != null ? obj.GetComponents(type) : Array.Empty<Component>(), false);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static C[] GetComponentsInChildren<C>(this ComputeContext ctx, GameObject obj, bool includeInactive) where C : Component
        {
            if (obj == null) return Array.Empty<C>();
            
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate,
                () => { return obj != null ? obj.GetComponentsInChildren<C>(includeInactive) : Array.Empty<C>(); },
                true);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static Component[] GetComponentsInChildren(this ComputeContext ctx, GameObject obj, Type type, bool includeInactive)
        {
            if (obj == null) return Array.Empty<Component>();
            
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;

            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate,
                () => obj != null ? obj.GetComponentsInChildren(type, includeInactive) : Array.Empty<Component>(),
                true);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
    }
}