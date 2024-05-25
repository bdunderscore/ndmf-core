﻿#region

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal class ProxyObjectController : IDisposable
    {
        private readonly Renderer _originalRenderer;
        private Renderer _replacementRenderer;

        internal ProxyPipeline Pipeline { get; set; }
        internal Renderer Renderer => _replacementRenderer;

        public ProxyObjectController(Renderer originalRenderer)
        {
            _originalRenderer = originalRenderer;

            CreateReplacementObject();
        }

        private void UpdateRenderer()
        {
            MeshState state = Pipeline?.GetState(_originalRenderer);
            SkinnedMeshRenderer smr = null;

            if (_originalRenderer is SkinnedMeshRenderer smr_)
            {
                smr = smr_;

                var replacementSMR = (SkinnedMeshRenderer)_replacementRenderer;
                replacementSMR.sharedMesh = state?.Mesh ?? smr_.sharedMesh;
            }
            else
            {
                var filter = _replacementRenderer.GetComponent<MeshFilter>();
                filter.sharedMesh = state?.Mesh ?? _originalRenderer.GetComponent<MeshFilter>().sharedMesh;
            }

            _replacementRenderer.sharedMaterials = state?.Materials?.ToArray() ?? _originalRenderer.sharedMaterials;

            var target = _replacementRenderer;
            var original = _originalRenderer;

            if (target.gameObject.scene != original.gameObject.scene &&
                original.gameObject.scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(target.gameObject, original.gameObject.scene);
            }

            target.transform.position = original.transform.position;
            target.transform.rotation = original.transform.rotation;

            target.localBounds = original.localBounds;
            if (target is SkinnedMeshRenderer targetSMR && original is SkinnedMeshRenderer originalSMR)
            {
                targetSMR.rootBone = originalSMR.rootBone;
                targetSMR.quality = originalSMR.quality;

                if (targetSMR.sharedMesh != null)
                {
                    var blendShapeCount = targetSMR.sharedMesh.blendShapeCount;
                    for (var i = 0; i < blendShapeCount; i++)
                    {
                        targetSMR.SetBlendShapeWeight(i, originalSMR.GetBlendShapeWeight(i));
                    }
                }
            }

            target.shadowCastingMode = original.shadowCastingMode;
            target.receiveShadows = original.receiveShadows;
            target.lightProbeUsage = original.lightProbeUsage;
            target.reflectionProbeUsage = original.reflectionProbeUsage;
            target.probeAnchor = original.probeAnchor;
            target.motionVectorGenerationMode = original.motionVectorGenerationMode;
            target.allowOcclusionWhenDynamic = original.allowOcclusionWhenDynamic;

            Pipeline?.RunOnFrame(_originalRenderer, _replacementRenderer);
        }

        private bool CreateReplacementObject()
        {
            var replacementGameObject = new GameObject("Proxy renderer for " + _originalRenderer.gameObject.name);
            replacementGameObject.hideFlags = HideFlags.DontSave;

#if MODULAR_AVATAR_DEBUG_HIDDEN
            replacementGameObject.hideFlags = HideFlags.DontSave;
#endif

            replacementGameObject.AddComponent<SelfDestructComponent>().KeepAlive = this;

            if (_originalRenderer is SkinnedMeshRenderer smr)
            {
                _replacementRenderer = replacementGameObject.AddComponent<SkinnedMeshRenderer>();
            }
            else if (_originalRenderer is MeshRenderer mr)
            {
                _replacementRenderer = replacementGameObject.AddComponent<MeshRenderer>();
                replacementGameObject.AddComponent<MeshFilter>();
            }
            else
            {
                Debug.Log("Unsupported renderer type: " + _replacementRenderer.GetType());
                Object.DestroyImmediate(replacementGameObject);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>(original, replacement renderer)</returns>
        public (Renderer, Renderer) OnPreCull()
        {
            UpdateRenderer();

            return (_originalRenderer, _replacementRenderer);
        }

        public void Dispose()
        {
            if (_replacementRenderer != null)
            {
                Object.DestroyImmediate(_replacementRenderer.gameObject);
                _replacementRenderer = null;
            }
        }
    }
}