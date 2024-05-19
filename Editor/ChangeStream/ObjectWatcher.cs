using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.ReactiveQuery.unity.editor
{
    using UnityObject = UnityEngine.Object;
    /// <summary>
    /// ObjectWatcher provides a high level API for monitoring for various changes to assets and scene objects in the
    /// editor.
    /// </summary>
    internal sealed class ObjectWatcher
    {
        // Supported watch categories:
        // - Single-object watch: Monitor asset, component properties, etc
        //   -> simple mapping
        // - Parent watch: Monitor whether the parent of an object changes
        //   -> record parent path
        // - Component search: Monitor the set of components matching a type filter under a given object
        //   -> 
        
        // Event types:
        //   - ChangeScene: Fires everything
        //   - CreateGameObjectHierarchy: Check parents, possibly fire component search notifications
        //     -> May result in creation of new components under existing nodes
        //   - ChangeGameObjectStructureHierarchy: Check old and new parents, possibly fire component search notifications
        //     -> May result in creation of new components under existing nodes, or reparenting of components
        //   - ChangeGameObjectStructure: Check parents, possibly fire component search notifications
        //     -> Creates/deletes components
        //   - ChangeGameObjectOrComponentProperties:
        //     -> If component, fire single notification. If GameObject, this might be a component reordering, so fire
        //        the component search notifications as needed
        //   - CreateAssetObject: Ignored
        //   - DestroyAssetObject: Fire single object notifications
        //   - ChangeAssetObjectProperties: Fire single object notifications
        //   - UpdatePrefabInstances: Treated as ChangeGameObjectStructureHierarchy
        //   - ChangeChildrenOrder: Fire component search notifications
        
        // High level structure:
        //   We maintain a "shadow hierarchy" of GameObjects with their last known parent/child relationships.
        //   Since OCES doesn't give us the prior state, we need this to determine which parent objects need to be
        //   notified when objects move. Each shadow GameObject also tracks the last known set of components on the object.
        //
        //   Listeners come in two flavors: object listeners (asset/component watches as well as parent watches), and
        //   component search listeners, which can be local or recursive.
        
        public static ObjectWatcher Instance { get; private set; } = new ObjectWatcher();
        internal ShadowHierarchy Hierarchy = new ShadowHierarchy();
        private readonly SynchronizationContext _syncContext = SynchronizationContext.Current;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        
        internal ObjectWatcher()
        {
            
        }

        public void MonitorObjectProps<T>(out IDisposable cancel, UnityObject obj, Action<T> callback, T target) where T : class
        {
            cancel = default;
      
            if (obj is GameObject go)
            {
                cancel = Hierarchy.RegisterGameObjectListener(go, (t, e) =>
                {
                    switch (e)
                    {
                        case HierarchyEvent.ObjectDirty:
                        case HierarchyEvent.ForceInvalidate:
                            InvokeCallback(callback, t);
                            return true;
                        default:
                            return false;
                    }
                }, target);
            } else
            {
                cancel = Hierarchy.RegisterObjectListener(obj, (t, e) =>
                {
                    switch (e)
                    {
                        case HierarchyEvent.ObjectDirty:
                        case HierarchyEvent.ForceInvalidate:
                            InvokeCallback(callback, t);
                            return true;
                        default:
                            return false;
                    }
                }, target);
            }

            cancel = CancelWrapper(cancel);
        }

        private static void InvokeCallback<T>(Action<T> callback, object t) where T : class
        {
            try
            {
                callback((T)t);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public C[] MonitorGetComponents<T, C>(out IDisposable cancel, GameObject obj, Action<T> callback, T target, Func<C[]> get, bool includeChildren) where T : class
        {
            cancel = default;

            C[] components = get();
            
            Hierarchy.RegisterGameObjectListener(obj, (t, e) =>
            {
                if (e == HierarchyEvent.ChildComponentsChanged && !includeChildren) return false;
                
                switch (e)
                {
                    case HierarchyEvent.ChildComponentsChanged:
                    case HierarchyEvent.SelfComponentsChanged:
                    case HierarchyEvent.ForceInvalidate:
                        var newComponents = get();
                        if (components.SequenceEqual(newComponents))
                        {
                            return false;
                        }
                        else
                        {
                            InvokeCallback(callback, t);
                            return true;
                        }
                    default:
                        return false;
                }
            }, target);

            if (includeChildren) Hierarchy.EnableComponentMonitoring(obj);
            
            cancel = CancelWrapper(cancel);

            return components;
        }

        public C MonitorGetComponent<T, C>(out IDisposable cancel, GameObject obj, Action<T> callback, T target, Func<C> get) where T : class
        {
            cancel = default;

            C component = get();
            
            Hierarchy.RegisterGameObjectListener(obj, (t, e) =>
            {
                switch (e)
                {
                    case HierarchyEvent.SelfComponentsChanged:
                    case HierarchyEvent.ChildComponentsChanged:
                    case HierarchyEvent.ForceInvalidate:
                        var newComponent = get();
                        if (ReferenceEquals(component, newComponent))
                        {
                            return false;
                        }
                        else
                        {
                            InvokeCallback(callback, t);
                            return true;
                        }
                    default:
                        return false;
                }
            }, target);

            cancel = CancelWrapper(cancel);

            return component;
        }
        
        class WrappedDisposable : IDisposable
        {
            private readonly int _targetThread;
            private readonly SynchronizationContext _syncContext;
            private IDisposable _orig;
            
            public WrappedDisposable(IDisposable orig, SynchronizationContext syncContext)
            {
                this._orig = orig;
                _targetThread = Thread.CurrentThread.ManagedThreadId;
                this._syncContext = syncContext;
            }
            
            public void Dispose()
            {
                lock (this)
                {
                    if (_orig == null) return;
                    
                    if (Thread.CurrentThread.ManagedThreadId == _targetThread)
                    {
                        _orig.Dispose();
                    }
                    else
                    {
                        var orig = _orig;
                        _syncContext.Post(_ => orig.Dispose(), null);
                    }

                    _orig = null;
                }
            }
        }
        private IDisposable CancelWrapper(IDisposable orig)
        {
            return new WrappedDisposable(orig, _syncContext);
        }
    }
}