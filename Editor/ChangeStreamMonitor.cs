using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace nadena.dev.ndmf.ReactiveQuery.unity.editor
{
    internal class ChangeStreamMonitor
    {
        public static ChangeStreamMonitor Instance { get; private set; }
        
        private int mainThreadId = Thread.CurrentThread.ManagedThreadId;
        // instanceId => actions
        private readonly Dictionary<int, HashSet<Action>> _monitorCallbacks
            = new Dictionary<int, HashSet<Action>>();

        /// <summary>
        /// This reversible index contains a mapping from child instanceId to parent instanceId. This is used to
        /// reconstruct which objects we need to notify when heirarchy change notifications are issued, after the
        /// objects have actually changed parentage.
        /// </summary>
        private ReversibleIndex<int, int> _childToParent = new ReversibleIndex<int, int>();
        
        /// <summary>
        /// Deregistrations coming from other threads are deferred until the next time the main thread touches this
        /// CSM.
        /// </summary>
        private ConcurrentQueue<Action> _pendingDeregister = new ConcurrentQueue<Action>();

        [InitializeOnLoadMethod]
        static void Init()
        {
            Instance = new ChangeStreamMonitor();

            ObjectChangeEvents.changesPublished += Instance.OnChange;
        }

        private void CheckThread()
        {
            if (mainThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException("ChangeStreamMonitor must be accessed from the main thread.");
            }
        }
        
        public void Monitor(UnityEngine.Object obj, Action callback, Task deregistration)
        {
            var instanceId = obj.GetInstanceID();
            CheckThread();
            
            ProcessPendingDeregistrations();
            
            if (!_monitorCallbacks.TryGetValue(instanceId, out var callbacks))
            {
                callbacks = new HashSet<Action>(new ObjectIdentityComparer<Action>());
                _monitorCallbacks[instanceId] = callbacks;
            }

            deregistration.ContinueWith(_ => Deregister(callback, callbacks, instanceId),
                TaskContinuationOptions.ExecuteSynchronously);

            callbacks.Add(callback);
        }

        private void ProcessPendingDeregistrations()
        {
            while (_pendingDeregister.TryDequeue(out var action)) action();
        }

        private void Deregister(Action callback, HashSet<Action> callbacks, int instanceId)
        {
            if (mainThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                _pendingDeregister.Enqueue(() => Deregister(callback, callbacks, instanceId));
                return;
            }
            
            callbacks.Remove(callback);
            if (callbacks.Count == 0 && _monitorCallbacks.TryGetValue(instanceId, out var curVal) && curVal.Count == 0)
            {
                _monitorCallbacks.Remove(instanceId);
                MaybePrune(instanceId);
            }
        }

        private void MaybePrune(int instanceId)
        {
            while (!_childToParent.GetKeys(instanceId).Any() && !_monitorCallbacks.ContainsKey(instanceId))
            {
                var hasParent = _childToParent.TryGet(instanceId, out var parentId);
                _childToParent.Remove(instanceId);
                
                // Continue up the tree...
                if (!hasParent) break;
                instanceId = parentId;
            }
        }

        private void OnChange(ref ObjectChangeEventStream stream)
        {
            ProcessPendingDeregistrations();

            Stopwatch sw = new Stopwatch();
            sw.Start();
            int length = stream.length;
            for (int i = 0; i < length; i++)
            {
                try
                {
                    HandleEvent(stream, i);
                } catch (Exception e)
                {
                    Debug.LogError($"Error handling event {i}: {e}");
                }
            }
            
            sw.Stop();
            Debug.Log($"Handled {length} events in {sw.ElapsedMilliseconds}ms");
        }

        private void RecordObjectParentage(GameObject obj)
        {
            while (obj != null)
            {
                var parent = obj.transform.parent;
                if (parent == null)
                {
                    _childToParent.Remove(obj.GetInstanceID());
                }
                else
                {
                    var objInstance = obj.GetInstanceID();
                    var parentInstance = parent.gameObject.GetInstanceID();
                    if (_childToParent.TryGet(objInstance, out var oldParent))
                    {
                        if (oldParent == parentInstance) break;
                        
                        // Something has gotten out of sync, wake up everything.
                        FireAllNotifications();
                        return;
                    }
                    else
                    {
                        _childToParent.Set(obj.GetInstanceID(), parent.gameObject.GetInstanceID());
                    }
                }
                
                obj = parent.gameObject;
            }
        }

        private void FireAllNotifications()
        {
            var toInvoke = _monitorCallbacks.Values.ToList().SelectMany(set => set);
            _monitorCallbacks.Clear();
            _childToParent.Clear();
                    
            foreach (var callback in toInvoke)
            {
                callback();
            }
        }

        private void FireNotificationsDeeply(int instanceId)
        {
            // Start from the leaves...
            foreach (var child in _childToParent.GetKeys(instanceId))
            {
                FireNotifications(child);
            }
            
            FireNotifications(instanceId);
        }

        private void HandleEvent(ObjectChangeEventStream stream, int i)
        {
            switch (stream.GetEventType(i))
            {
                case ObjectChangeKind.None: break;
                
                case ObjectChangeKind.ChangeScene:
                {
                    OnChangeScene();

                    break;
                }

                case ObjectChangeKind.CreateGameObjectHierarchy:
                {
                    stream.GetCreateGameObjectHierarchyEvent(i, out var data);
                    OnCreateGameObjectHierarchy(data);
                    break;
                }

                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                {
                    stream.GetChangeGameObjectStructureHierarchyEvent(i, out var data);
                    
                    OnChangeGameObjectStructureHierarchy(data);

                    break;
                }
                
                case ObjectChangeKind.ChangeGameObjectStructure: // add/remove components
                {
                    stream.GetChangeGameObjectStructureEvent(i, out var data);
                    OnChangeGameObjectStructure(data);
                    break;
                }
                
                case ObjectChangeKind.ChangeGameObjectParent:
                {
                    stream.GetChangeGameObjectParentEvent(i, out var data);
                    OnChangeGameObjectParent(data);

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                {
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var data);
                    OnChangeGameObjectOrComponentProperties(data);
                    break;
                }

                case ObjectChangeKind.DestroyGameObjectHierarchy:
                {
                    stream.GetDestroyGameObjectHierarchyEvent(i, out var data);
                    
                    OnDestroyGameObjectHierarchy(data);
                    break;
                }
                
                case ObjectChangeKind.CreateAssetObject: break;
                case ObjectChangeKind.DestroyAssetObject:
                {
                    stream.GetDestroyAssetObjectEvent(i, out var data);
                    OnDestroyAssetObject(data);

                    break;
                }
                
                case ObjectChangeKind.ChangeAssetObjectProperties:
                {
                    stream.GetChangeAssetObjectPropertiesEvent(i, out var data);
                    OnChangeAssetObjectProperties(data);

                    break;
                }
                
                case ObjectChangeKind.UpdatePrefabInstances:
                {
                    stream.GetUpdatePrefabInstancesEvent(i, out var data);
                    OnUpdatePrefabInstances(data);

                    break;
                }

                case ObjectChangeKind.ChangeChildrenOrder:
                {
                    stream.GetChangeChildrenOrderEvent(i, out var data);
                    OnChangeChildrenOrder(data);

                    break;
                }
            }
        }

        private void OnChangeChildrenOrder(ChangeChildrenOrderEventArgs data)
        {
            var instanceId = data.instanceId;
            var scene = data.scene;
            var obj = EditorUtility.InstanceIDToObject(instanceId);

            if (obj != null && obj is GameObject go)
            {
                // Dirty the object and all children
                FireNotifications(obj);

                foreach (Transform child in go.transform)
                {
                    FireNotifications(child.gameObject);
                }
            }
        }

        private void OnUpdatePrefabInstances(UpdatePrefabInstancesEventArgs data)
        {
            var scene = data.scene;
            foreach (var iid in data.instanceIds)
            {
                FireNotificationsDeeply(iid);
            }
        }

        private void OnChangeAssetObjectProperties(ChangeAssetObjectPropertiesEventArgs data)
        {
            var instanceId = data.instanceId;
            FireNotifications(instanceId);
        }

        private void OnDestroyAssetObject(DestroyAssetObjectEventArgs data)
        {
            var instanceId = data.instanceId;
            var obj = EditorUtility.InstanceIDToObject(instanceId);

            if (obj != null)
            {
                FireNotifications(obj);
            }
        }

        private void OnDestroyGameObjectHierarchy(DestroyGameObjectHierarchyEventArgs data)
        {
            // TODO: We need to record the original heirarchy here as well
            var instanceId = data.instanceId;

            FireNotificationsDeeply(instanceId);
        }

        private void OnChangeGameObjectOrComponentProperties(ChangeGameObjectOrComponentPropertiesEventArgs data)
        {
            var instanceId = data.instanceId;
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            if (obj is Component c) obj = c.gameObject; 
                    
            if (obj != null) FireNotifications(obj.GetInstanceID());
        }

        private void OnChangeGameObjectParent(ChangeGameObjectParentEventArgs data)
        {
            var instanceId = data.instanceId;
            var priorParentId = data.previousParentInstanceId;
            var newParentId = data.newParentInstanceId;
                    
            FireNotifications(instanceId);
            FireNotifications(priorParentId);
            FireNotifications(newParentId);
            _childToParent.Set(instanceId, newParentId);
            MaybePrune(priorParentId);
        }

        private void OnChangeGameObjectStructure(ChangeGameObjectStructureEventArgs data)
        {
            var instanceId = data.instanceId;
            var scene = data.scene;
            var obj = EditorUtility.InstanceIDToObject(instanceId);
                    
            FireNotificationsDeeply(instanceId);
        }

        private void OnChangeGameObjectStructureHierarchy(ChangeGameObjectStructureHierarchyEventArgs data)
        {
            // TODO - we need to record original parent/child relationships so we can fire off notifications
            // for everything that was previously present.

            var instanceId = data.instanceId;

            FireNotificationsDeeply(instanceId);
        }

        private void OnCreateGameObjectHierarchy(CreateGameObjectHierarchyEventArgs data)
        {
            var instanceId = data.instanceId;
            var scene = data.scene;
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            var parentObj = (obj as GameObject)?.transform.parent?.gameObject;
            
            if (parentObj != null) FireNotifications(parentObj.GetInstanceID());
        }

        private void OnChangeScene()
        {
            // Invoke everything...
            FireAllNotifications();
        }

        private void FireNotifications(UnityEngine.Object obj)
        {
            FireNotifications(obj.GetInstanceID());
        }

        private void FireNotifications(int instanceId)
        {
            if (_monitorCallbacks.TryGetValue(instanceId, out var callbacks))
            {
                _monitorCallbacks.Remove(instanceId);
                
                foreach (var callback in callbacks.ToList())
                {
                    callback();
                }
            }
            
            MaybePrune(instanceId);
        }
    }
}