using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.ReactiveQuery;
using nadena.dev.ndmf.ReactiveQuery.Core;
using nadena.dev.ndmf.ReactiveQuery.unity.editor;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.ReactiveQuery.Samples
{
    public class ObjectCounterWindow : EditorWindow, IObserver<int>, IInvalidationObserver
    {
        [MenuItem("bd_/ReactiveQuery/Samples/ObjectCounter")]
        public static void ShowWindow()
        {
            GetWindow<ObjectCounterWindow>();
        }
        
        private class TargetHolder : ScriptableObject
        {
            public GameObject target;
        }

        private TargetHolder _holder;
        private SerializedObject _holderSO;
        private SerializedProperty _targetProp;
        
        private static QueryCache<TargetHolder, GameObject> _holderToObj = new(
            async (ctx, holder) => ctx.Observe(holder).target, "Holder to Object");
        private static QueryCache<GameObject, int> _objToCount = new(
            async (ctx, obj) =>
            {
                if (obj == null) return 0;
                
                var transform = ctx.Observe(obj).transform;

                int children = transform.childCount;
                List<Task<int>> pendingTasks = new List<Task<int>>(children);
                foreach (Transform child in transform)
                {
                    pendingTasks.Add(ctx.Observe(_objToCount.Get(child.gameObject)));
                }

                await Task.WhenAll(pendingTasks);
                
                return children + pendingTasks.ConvertAll(t => t.Result).Sum();
            }, "Object to Count");
        private static QueryCache<TargetHolder, int> _holderToCount = new(
            async (ctx, holder) => await ctx.Observe(_objToCount.Get(
                await ctx.Observe(_holderToObj.Get(holder))
            )), "Holder to Count");

        private IDisposable _registration;
        
        private void OnEnable()
        {
            if (_holder == null)
            {
                _holder = CreateInstance<TargetHolder>();
            }
            
            if (_holderSO == null) {
                _holderSO = new SerializedObject(_holder);
                _targetProp = _holderSO.FindProperty(nameof(TargetHolder.target));
            }

            _registration = _holderToCount.Get(_holder).Subscribe(this);
        }

        private void OnDisable()
        {
            _registration.Dispose();
        }

        private void OnGUI()
        {
            _holderSO.Update();
            EditorGUILayout.PropertyField(_targetProp);
            _holderSO.ApplyModifiedProperties();
            
            EditorGUILayout.LabelField("Count", _label);
            
            if (_recomputing)
            {
                EditorGUILayout.LabelField("(Update in progress...)");
            }
        }

        private string _label = "<never computed>";
        private bool _recomputing = false;

        public void OnCompleted()
        {
            _label = "<not subscribed>";
            _recomputing = false;
            
            Repaint();
        }

        public void OnError(Exception error)
        {
            _label = "<error>";
            Debug.LogException(error);
            _recomputing = false;
            Repaint();
        }

        public void OnNext(int value)
        {
            _label = "" + value;
            _recomputing = false;
            Repaint();
        }

        public void OnInvalidate()
        {
            _recomputing = true;
            Repaint();
        }
    }
}