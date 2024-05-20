#region

using UnityEditor;
using UnityEngine;

#endregion

internal class SelfDestructComponent : MonoBehaviour
{
    internal object KeepAlive; // don't destroy when non-null (non-serialized field)

    void OnValidate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (this != null && KeepAlive == null)
            {
                DestroyImmediate(gameObject);
            }
        };
#endif
    }
}