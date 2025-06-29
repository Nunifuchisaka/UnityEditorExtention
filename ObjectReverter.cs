#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Nunifuchisaka
{
  public static class ObjectReverter
  {
    public static void RevertAll(GameObject targetObject)
    {
      if (targetObject == null)
      {
        Debug.LogWarning("[Reverter] 対象のGameObjectがnullです。");
        return;
      }

      GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(targetObject);

      if (instanceRoot == null)
      {
        Debug.LogWarning($"[Reverter] '{targetObject.name}' はPrefabインスタンスの一部ではないため、Revertできません。");
        return;
      }

      EditorApplication.delayCall += () =>
      {
        if (instanceRoot != null)
        {
          PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
          Debug.Log($"[Reverter] Complete.");
        }
      };
    }
  }
}
#endif