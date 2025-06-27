using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Nunifuchisaka
{
  public class TransformReverter : EditorWindow
  {
    // ウィンドウに表示するための変数
    private GameObject targetObject;
    private bool revertPosition = true;
    private bool revertRotation = true;
    private bool revertScale = true;

    [MenuItem("Tools/Nunifuchisaka/Transform Reverter")]
    public static void ShowWindow()
    {
      GetWindow<TransformReverter>("Transform Reverter");
    }

    private void OnGUI()
    {
      GUIStyle boldLabel = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };

      // 1. オブジェクト選択フィールド
      GUILayout.Label("Select Target GameObject", boldLabel);
      targetObject = (GameObject)EditorGUILayout.ObjectField("Root Object", targetObject, typeof(GameObject), true);

      EditorGUILayout.Space(10);

      // 2. Revert対象プロパティ選択のチェックボックス
      GUILayout.Label("Choose Properties to Revert", boldLabel);
      revertPosition = EditorGUILayout.Toggle("Revert Position", revertPosition);
      revertRotation = EditorGUILayout.Toggle("Revert Rotation", revertRotation);
      revertScale = EditorGUILayout.Toggle("Revert Scale", revertScale);
      
      EditorGUILayout.Space(20);

      // 3. 実行ボタン
      GUI.enabled = targetObject != null;
      if (GUILayout.Button("Revert Selected Properties", GUILayout.Height(30)))
      {
        RevertTransforms();
      }
      GUI.enabled = true;
    }

    private void RevertTransforms()
    {
      if (targetObject == null)
      {
        Debug.LogError("[TransformReverter] Target object is not selected.");
        return;
      }

      Undo.RegisterCompleteObjectUndo(targetObject, "Revert Transform Properties");

      ProcessGameObjectRecursively(targetObject);

      Debug.Log($"[TransformReverter] Process completed for '{targetObject.name}'.");
    }

    private void ProcessGameObjectRecursively(GameObject go)
    {
      if (!PrefabUtility.IsPartOfPrefabInstance(go))
      {
        foreach (Transform child in go.transform)
        {
          ProcessGameObjectRecursively(child.gameObject);
        }
        return;
      }

      var newModifications = new List<PropertyModification>();
      
      PropertyModification[] currentModifications = PrefabUtility.GetPropertyModifications(go);
      if (currentModifications != null)
      {
        foreach (var mod in currentModifications)
        {
          if (!(mod.target is Transform))
          {
            newModifications.Add(mod);
            continue;
          }

          bool keepModification = 
            (!revertPosition && mod.propertyPath.StartsWith("m_LocalPosition")) ||
            (!revertRotation && mod.propertyPath.StartsWith("m_LocalRotation")) ||
            (!revertScale   && mod.propertyPath.StartsWith("m_LocalScale"));

          if (keepModification)
          {
            newModifications.Add(mod);
          }
        }
      }

      PrefabUtility.SetPropertyModifications(go, newModifications.ToArray());

      foreach (Transform child in go.transform)
      {
        ProcessGameObjectRecursively(child.gameObject);
      }
    }
  }
}