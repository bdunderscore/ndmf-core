﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace nadena.dev.ndmf.ReactiveQuery.unity.editor
{
    using UnityObject = UnityEngine.Object;
    
    internal enum HierarchyEvent
    {
        /// <summary>
        /// Fired when an unspecified changed may have happened to this object.
        /// </summary>
        ObjectDirty,
        /// <summary>
        /// Fired when the parentage of this object has changed.
        /// </summary>
        PathChange,
        /// <summary>
        /// Fired when the set or order of components on this object may have changed
        /// </summary>
        SelfComponentsChanged,
        /// <summary>
        /// Fired when the set or order of components on this object or any children may have changed
        /// </summary>
        ChildComponentsChanged,
        /// <summary>
        /// Fired when an object is destroyed or otherwise we're not quite sure what's going on.
        /// </summary>
        ForceInvalidate,
    }
    
    internal interface IHierarchyListener
    {
        void NotifyPathChange(GameObject obj);
        void NotifyPotentialComponentSetChange(GameObject obj);
    }
    
    internal class ShadowHierarchy
    {
        internal SynchronizationContext _syncContext;
        internal Dictionary<int, ShadowGameObject> _gameObjects = new();
        internal Dictionary<int, ShadowObject> _otherObjects = new();
        
        int lastPruned = Int32.MinValue;
        
        internal IDisposable RegisterGameObjectListener(GameObject targetObject, ListenerSet<HierarchyEvent>.Invokee invokee,
            object target)
        {
            ShadowGameObject shadowObject = ActivateShadowObject(targetObject);

            return shadowObject._listeners.Register(invokee, target);
        }
        
        internal IDisposable RegisterObjectListener(UnityObject targetComponent, ListenerSet<HierarchyEvent>.Invokee invokee,
            object target)
        {
            if (!_otherObjects.TryGetValue(targetComponent.GetInstanceID(), out var shadowComponent))
            {
                shadowComponent = new ShadowObject(targetComponent);
                _otherObjects[targetComponent.GetInstanceID()] = shadowComponent;
            }

            return shadowComponent._listeners.Register(invokee, target);
        }

        /// <summary>
        /// Activates monitoring for all children of the specified GameObject. This is needed to ensure child component
        /// change notifications are propagated correctly.
        /// </summary>
        /// <param name="root"></param>
        internal void EnableComponentMonitoring(GameObject root)
        {
            var obj = ActivateShadowObject(root);

            EnableComponentMonitoring(obj);
        }

        private void EnableComponentMonitoring(ShadowGameObject obj)
        {
            if (obj.ComponentMonitoring) return;
            obj.ComponentMonitoring = true;

            foreach (Transform child in obj.GameObject.transform)
            {
                EnableComponentMonitoring(child.gameObject);
            }
        }

        internal void EnablePathMonitoring(GameObject root)
        {
            var obj = ActivateShadowObject(root);

            while (obj != null)
            {
                obj.PathMonitoring = true;
                obj = obj.Parent;
            }
        }
        
        private ShadowGameObject ActivateShadowObject(GameObject targetObject)
        {
            // An object is activated when it, or a parent, has a listener attached.
            // An object is deactivated ("inert") when we traverse it and find no listeners in any of its children.
            // Inert objects are skipped for path update notifications; however, we can't just delete them, because
            // we may need to know about them for future structure change notifications at their parents.
            int instanceId = targetObject.GetInstanceID();
            if (!_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                shadow = new ShadowGameObject(targetObject);
                _gameObjects[instanceId] = shadow;

                shadow.Scene = targetObject.scene;
                var parent = targetObject.transform.parent?.gameObject;
                if (parent == null)
                {
                    shadow.Parent = null;
                }
                else
                {
                    shadow.Parent = ActivateShadowObject(parent);
                }
            }

            return shadow;
        }

        /// <summary>
        /// Fires a notification that properties on a specific object (GameObject or otherwise) has changed.
        /// </summary>
        /// <param name="instanceId"></param>
        internal void FireObjectChangeNotification(int instanceId)
        {
            if (_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                shadow._listeners.Fire(HierarchyEvent.ObjectDirty);
            }
            
            var component = EditorUtility.InstanceIDToObject(instanceId) as Component;
            if (component != null)
            {
                // This event may have been a component reordering, so trigger a synthetic structure change event.
                // TODO: Cache component positions?
                var parentId = component.gameObject.GetInstanceID();
                FireStructureChangeEvent(parentId);
            }
            
            if (_otherObjects.TryGetValue(instanceId, out var shadowComponent))
            {
                shadowComponent._listeners.Fire(HierarchyEvent.ObjectDirty);
            }
        }

        /// <summary>
        /// Fires a notification that the specified GameObject has a new parent.
        /// </summary>
        /// <param name="instanceId"></param>
        internal void FireReparentNotification(int instanceId)
        {
            // Always activate on reparent. This is because we might be reparenting _into_ an active hierarchy.
            var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            ShadowGameObject shadow;
            if (obj != null)
            {
                shadow = ActivateShadowObject(obj);
            }
            else
            {
                if (_gameObjects.TryGetValue(instanceId, out var _))
                {
                    FireDestroyNotification(instanceId);
                }
                return;
            }
            
            FireParentComponentChangeNotifications(shadow.Parent);
            if (shadow.PathMonitoring) FirePathChangeNotifications(shadow);
            
            // Update parentage and refire

            var newParent = shadow.GameObject.transform.parent?.gameObject;
            if (newParent == null)
            {
                shadow.Parent = null;
            }
            else if (newParent != shadow.Parent?.GameObject)
            {
                shadow.Parent = ActivateShadowObject(newParent);
                FireParentComponentChangeNotifications(shadow.Parent);

                var ptr = shadow.Parent;
                while (ptr != null && !ptr.PathMonitoring)
                {
                    ptr.PathMonitoring = true;
                    ptr = ptr.Parent;
                }
            }
            
            // This needs to run even if the parent did not change, just in case we did a just-in-time creation of this
            // shadow object.
            if (shadow.Parent?.ComponentMonitoring == true) EnableComponentMonitoring(shadow);
        }

        private void FirePathChangeNotifications(ShadowGameObject shadow)
        {
            if (!shadow.PathMonitoring) return;
            shadow._listeners.Fire(HierarchyEvent.PathChange);
            foreach (var child in shadow.Children)
            {
                FirePathChangeNotifications(child);
            }
        }

        private void FireParentComponentChangeNotifications(ShadowGameObject obj)
        {
            while (obj != null)
            {
                obj._listeners.Fire(HierarchyEvent.ChildComponentsChanged);
                obj = obj.Parent;
            }
        }

        internal void FireDestroyNotification(int instanceId)
        {
            if (_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                FireParentComponentChangeNotifications(shadow.Parent);
                ForceInvalidateHierarchy(shadow);
            }
        }

        void ForceInvalidateHierarchy(ShadowGameObject obj)
        {
            obj._listeners.Fire(HierarchyEvent.ForceInvalidate);
            _gameObjects.Remove(obj.InstanceID);

            foreach (var child in obj.Children)
            {
                ForceInvalidateHierarchy(child);
            }
        }

        internal void FireReorderNotification(int parentInstanceId)
        {
            if (!_gameObjects.TryGetValue(parentInstanceId, out var shadow))
            {
                return;
            }

            FireParentComponentChangeNotifications(shadow);
        }

        internal void FireStructureChangeEvent(int instanceId)
        {
            if (!_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                return;
            }

            shadow._listeners.Fire(HierarchyEvent.SelfComponentsChanged);
            FireParentComponentChangeNotifications(shadow.Parent);
        }

        internal void InvalidateAll()
        {
            var oldDict = _gameObjects;
            _gameObjects = new Dictionary<int, ShadowGameObject>();

            foreach (var shadow in oldDict.Values)
            {
                shadow._listeners.Fire(HierarchyEvent.ForceInvalidate);
            }
            
            var oldComponents = _otherObjects;
            _otherObjects = new Dictionary<int, ShadowObject>();
            
            foreach (var shadow in oldComponents.Values)
            {
                shadow._listeners.Fire(HierarchyEvent.ForceInvalidate);
            }
        }

        /// <summary>
        /// Assume that everything has changed for the specified object and its children. Fire off all relevant
        /// notifications and rebuild state.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void InvalidateTree(int instanceId)
        {
            if (_gameObjects.TryGetValue(instanceId, out var shadow))
            {
                shadow._listeners.Fire(HierarchyEvent.ForceInvalidate);
                FireParentComponentChangeNotifications(shadow.Parent);
                _gameObjects.Remove(instanceId);

                var parentGameObject = shadow.Parent?.GameObject;

                if (parentGameObject != null)
                {
                    // Repair parent's child mappings
                    foreach (Transform child in parentGameObject.transform)
                    {
                        ActivateShadowObject(child.gameObject);
                    }
                }
                
                // Finally recreate the target object, just in case it took up some objects from somewhere else
                var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (gameObject != null)
                {
                    ActivateShadowObject(gameObject);
                }
            }
        }

        public void FireGameObjectCreate(int instanceId)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (obj == null) return;
            
            ActivateShadowObject(obj);
        }
    }

    internal class ShadowObject
    {
        internal int InstanceID { get; private set; }
        internal UnityObject Object { get; private set; }
        
        internal ListenerSet<HierarchyEvent> _listeners = new ListenerSet<HierarchyEvent>();
        
        internal ShadowObject(UnityObject component)
        {
            InstanceID = component.GetInstanceID();
            Object = component;
        }
    }
    
    /// <summary>
    /// Represents a single GameObject in a loaded scene. This shadow copy will be retained, once an interest is
    /// registered, until the GameObject is destroyed or scene unloaded.
    /// </summary>
    internal class ShadowGameObject
    {
        internal int InstanceID { get; private set; }
        internal GameObject GameObject { get; private set; }
        internal Scene Scene { get; set; }
        private readonly Dictionary<int, ShadowGameObject> _children = new Dictionary<int, ShadowGameObject>();
        
        public IEnumerable<ShadowGameObject> Children => _children.Values;

        
        private ShadowGameObject _parent;
        internal bool PathMonitoring { get; set; } = false;
        internal bool ComponentMonitoring { get; set; } = false;

        internal ShadowGameObject Parent
        {
            get => _parent;
            set
            {
                if (value == _parent) return;

                if (_parent != null)
                {
                    _parent._children.Remove(InstanceID);
                    // Fire off a property change notification for the parent itself
                    // TODO: tests
                    _parent._listeners.Fire(HierarchyEvent.ObjectDirty);
                }

                _parent = value;
                
                if (_parent != null)
                {
                    _parent._children[InstanceID] = this;
                    _parent._listeners.Fire(HierarchyEvent.ObjectDirty);
                }
            }
        }
        
        internal ListenerSet<HierarchyEvent> _listeners = new ListenerSet<HierarchyEvent>();
        
        internal ShadowGameObject(GameObject gameObject)
        {
            InstanceID = gameObject.GetInstanceID();
            GameObject = gameObject;
            Scene = gameObject.scene;
        }
    }
}