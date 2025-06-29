#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

namespace Nunifuchisaka
{
  public static class MaterialCopier
  {
    public static void ExecuteCopy(GameObject source, GameObject dest)
    {
      if (source == null || dest == null)
      {
        Debug.LogError("[MaterialCopier] コピー元とコピー先の両方のオブジェクトを指定してください。");
        return;
      }

      Undo.SetCurrentGroupName("Copy Materials");
      int group = Undo.GetCurrentGroup();

      try
      {
        CopyMaterialsRecursively(source.transform, dest.transform);
        Debug.Log("[MaterialCopier] Complete.");
      }
      catch (Exception e)
      {
        Debug.LogError($"[MaterialCopier] エラーが発生しました: {e.Message}\n{e.StackTrace}");
      }
      finally
      {
        Undo.CollapseUndoOperations(group);
      }
    }

    private static void CopyMaterialsRecursively(Transform source, Transform dest)
    {
      var sourceRenderer = source.GetComponent<Renderer>();
      var destRenderer = dest.GetComponent<Renderer>();

      // コピー元とコピー先の両方にRendererが存在する場合のみ、マテリアルをコピー
      if (sourceRenderer != null && destRenderer != null)
      {
        Undo.RecordObject(destRenderer, "Copy Materials");
        destRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        Debug.Log($"[MaterialCopier] '{dest.name}' のマテリアルをコピーしました");
      }

      foreach (Transform sourceChild in source)
      {
        Transform destChild = dest.Find(sourceChild.name);
        if (destChild != null)
        {
          CopyMaterialsRecursively(sourceChild, destChild);
        }
      }
    }
  }
}
#endif