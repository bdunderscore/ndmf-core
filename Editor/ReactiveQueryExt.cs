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
            
            var c = ObjectWatcher.Instance.MonitorGetComponent(out var dispose, obj, _ => invalidate(), onInvalidate, () => obj.GetComponent<C>());
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
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            var c = ObjectWatcher.Instance.MonitorGetComponent(out var dispose, obj, _ => invalidate(), onInvalidate, () => obj.GetComponent(type));
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static C[] GetComponents<C>(this ComputeContext ctx, GameObject obj) where C : Component
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate, () => obj.GetComponents<C>(), false);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static Component[] GetComponents(this ComputeContext ctx, GameObject obj, Type type)
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate, () => obj.GetComponents(type), false);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static C[] GetComponentsInChildren<C>(this ComputeContext ctx, GameObject obj, bool includeInactive) where C : Component
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate, () => obj.GetComponentsInChildren<C>(includeInactive), true);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
        
        public static Component[] GetComponentsInChildren(this ComputeContext ctx, GameObject obj, Type type, bool includeInactive)
        {
            var invalidate = ctx.Invalidate;
            var onInvalidate = ctx.OnInvalidate;
            
            var c = ObjectWatcher.Instance.MonitorGetComponents(out var dispose, obj, _ => invalidate(), onInvalidate, () => obj.GetComponentsInChildren(type, includeInactive), true);
            onInvalidate.ContinueWith(_ => dispose.Dispose());

            return c;
        }
    }
}