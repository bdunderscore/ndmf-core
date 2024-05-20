#region

using System;
using nadena.dev.ndmf.rq.unity.editor;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.rq.Samples
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

        private static ReactiveQuery<TargetHolder, GameObject> _holderToObj = new("Holder to Object",
            async (ctx, holder) => ctx.Observe(holder).target);

        private static ReactiveQuery<GameObject, int> _objToCount = new("Object to Count", async (ctx, obj) =>
        {
            if (obj == null) return 0;

            var transform = ctx.Observe(obj).transform;

            var children = ctx.GetComponentsInChildren<Transform>(transform.gameObject, true);

            return children.Length;
        });

        private static ReactiveQuery<TargetHolder, int> _holderToCount = new("Holder to Count", async (ctx, holder) =>
            await ctx.Observe(_objToCount.Get(
                await ctx.Observe(_holderToObj.Get(holder))
            )));

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