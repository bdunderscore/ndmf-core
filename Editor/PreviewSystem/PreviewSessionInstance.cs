#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal sealed class PreviewSessionInstance : IDisposable
    {
        public IEnumerable<(Renderer, Renderer)> Replacements =>
            _rendererStates.Values.Select(s => (s.original, s.target));

        private class RendererState
        {
            public Renderer original, target;
            public List<IRenderFilterSession> boundSessions;
        }

        private class InitSession : IRenderFilterSession
        {
            public void SetupRenderer(Renderer original, Renderer target)
            {
                if (original is SkinnedMeshRenderer originalSMR && target is SkinnedMeshRenderer targetSMR)
                {
                    targetSMR.sharedMesh = originalSMR.sharedMesh;
                    targetSMR.bones = originalSMR.bones;
                }
                else if (original is MeshRenderer originalMR && target is MeshRenderer targetMR)
                {
                    var originalFilter = originalMR.GetComponent<MeshFilter>();
                    var targetFilter = targetMR.GetComponent<MeshFilter>();

                    if (originalFilter != null && targetFilter != null)
                    {
                        targetFilter.sharedMesh = originalFilter.sharedMesh;
                    }
                }
            }

            public void OnFrame(Renderer original, Renderer target, ref bool rebuild)
            {
                if (target.gameObject.scene != original.gameObject.scene &&
                    original.gameObject.scene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(target.gameObject, original.gameObject.scene);
                }

                target.transform.position = original.transform.position;
                target.transform.rotation = original.transform.rotation;

                target.sharedMaterials = original.sharedMaterials;
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
            }

            public void OnFrameOnce()
            {
            }

            public void Dispose()
            {
            }
        }

        private IRenderFilterSession _initSession = new InitSession();
        private List<IRenderFilterSession> _allSessions = new();
        private Dictionary<Renderer, RendererState> _rendererStates = new();
        private List<GameObject> _gameObjects = new();

        public PreviewSessionInstance(
            ImmutableList<PreviewSession.ResolvedRegistration> registrations
        )
        {
            foreach (var registration in registrations)
            {
                if (registration.targets.IsEmpty) continue;

                IRenderFilterSession session;
                try
                {
                    session = registration.filter.CreateSession();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    continue;
                }

                _allSessions.Add(session);

                foreach (var renderer in registration.targets)
                {
                    if (!_rendererStates.TryGetValue(renderer, out var state))
                    {
                        state = InitRenderer(renderer);
                    }

                    if (state == null)
                    {
                        // unsupported component
                        return;
                    }

                    try
                    {
                        session.SetupRenderer(state.original, state.target);
                        state.boundSessions.Add(session);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        private RendererState InitRenderer(Renderer renderer)
        {
            var targetObject = new GameObject("Proxy for " + renderer.gameObject.name);

            Renderer target;

            if (renderer is SkinnedMeshRenderer smr)
            {
                target = targetObject.AddComponent<SkinnedMeshRenderer>();
            }
            else if (renderer is MeshRenderer mr)
            {
                target = targetObject.AddComponent<MeshRenderer>();
                targetObject.AddComponent<MeshFilter>();
            }
            else
            {
                Object.DestroyImmediate(targetObject);
                return null;
            }

            target.forceRenderingOff = true;

            targetObject.hideFlags = HideFlags.DontSave;
#if MODULAR_AVATAR_DEBUG_HIDDEN
            targetObject.hideFlags = HideFlags.HideAndDontSave;
#endif

            _gameObjects.Add(targetObject);

            var state = new RendererState
            {
                original = renderer,
                target = target,
                boundSessions = new List<IRenderFilterSession>()
            };
            _rendererStates[renderer] = state;

            _initSession.SetupRenderer(state.original, state.target);
            state.boundSessions.Add(_initSession);

            return state;
        }

        public void Dispose()
        {
            foreach (var session in _allSessions)
            {
                try
                {
                    session.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            foreach (var obj in _gameObjects)
            {
                Object.DestroyImmediate(obj);
            }
        }

        public void Update()
        {
            bool needRebuild = false;

            foreach (var sess in _allSessions)
            {
                sess.OnFrameOnce();
            }

            foreach (var state in _rendererStates.Values)
            {
                if (state.original == null) continue;

                foreach (var sess in state.boundSessions)
                {
                    sess.OnFrame(state.original, state.target, ref needRebuild);
                }
            }

            // TODO request rebuild handling
        }
    }
}