﻿#region

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal static class HandleUtilityPatches
    {
        internal static void Patch_FilterInstanceIDs(Harmony h)
        {
            var t_HandleUtility = AccessTools.TypeByName("UnityEditor.HandleUtility");
            var m_orig = AccessTools.Method(t_HandleUtility, "FilterInstanceIDs");

            var m_prefix = AccessTools.Method(typeof(HandleUtilityPatches), "Prefix_FilterInstanceIDs");
            var m_postfix = AccessTools.Method(typeof(HandleUtilityPatches), "Postfix_FilterInstanceIDs");

            h.Patch(original: m_orig, prefix: new HarmonyMethod(m_prefix), postfix: new HarmonyMethod(m_postfix));
        }

        [UsedImplicitly]
        private static bool Prefix_FilterInstanceIDs(
            ref IEnumerable<GameObject> gameObjects,
            out int[] parentInstanceIDs,
            out int[] childInstanceIDs
        )
        {
            gameObjects = RemapObjects(gameObjects);
            parentInstanceIDs = childInstanceIDs = null;
            return true;
        }

        [UsedImplicitly]
        private static void Postfix_FilterInstanceIDs(
            ref IEnumerable<GameObject> gameObjects,
            ref int[] parentInstanceIDs,
            ref int[] childInstanceIDs
        )
        {
            var sess = PreviewSession.Current;
            if (sess == null) return;

            HashSet<int> newChildInstanceIDs = null;

            foreach (var parent in gameObjects)
            {
                foreach (var renderer in parent.GetComponentsInChildren<Renderer>())
                {
                    if (sess.OriginalToProxyRenderer.TryGetValue(renderer, out var proxy) && proxy != null)
                    {
                        if (newChildInstanceIDs == null) newChildInstanceIDs = new HashSet<int>(childInstanceIDs);
                        newChildInstanceIDs.Add(proxy.GetInstanceID());
                    }
                }
            }

            if (newChildInstanceIDs != null)
            {
                childInstanceIDs = newChildInstanceIDs.ToArray();
            }
        }

        private static IEnumerable<GameObject> RemapObjects(IEnumerable<GameObject> objs)
        {
            var sess = PreviewSession.Current;
            if (sess == null) return objs;

            return objs.Select(
                obj =>
                {
                    if (obj == null) return obj;
                    if (sess.OriginalToProxyObject.TryGetValue(obj, out var proxy) && proxy != null)
                    {
                        return proxy.gameObject;
                    }
                    else
                    {
                        return obj;
                    }
                }
            ).ToArray();
        }
    }
}