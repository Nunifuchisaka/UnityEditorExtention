using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class NunifuchisakaRemoveComponent : EditorWindow
{
  private GameObject targetObject;

  private bool removeVrcComponents = true;
  private bool removeMaComponents = true;
  private bool removeAaoComponents = true;

  [MenuItem("Tools/Nunifuchisaka/RemoveComponent")]
  public static void ShowWindow()
  {
    GetWindow<NunifuchisakaRemoveComponent>("Remove Component");
  }

  private void OnGUI()
  {
    GUILayout.Label("Remove Components from Target and Children", EditorStyles.boldLabel);
    EditorGUILayout.HelpBox("指定したオブジェクトとその全階層の子オブジェクトから、条件に一致するコンポーネントを削除します。この操作はUndo可能です。", MessageType.Info);
    
    targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
    
    GUILayout.Space(10);
    
    EditorGUILayout.LabelField("Remove Conditions", EditorStyles.boldLabel);
    removeVrcComponents = EditorGUILayout.Toggle(new GUIContent("Remove 'VRC' components", "VRChat SDK関連のコンポーネント（VRC...で始まるもの）を削除します。"), removeVrcComponents);
    removeMaComponents = EditorGUILayout.Toggle(new GUIContent("Remove 'MA' components", "Modular Avatar関連のコンポーネント（ModularAvatar...で始まるもの）を削除します。"), removeMaComponents);
    removeAaoComponents = EditorGUILayout.Toggle(new GUIContent("Remove 'AAO' components", "Avatar Assembler(旧名AAO)関連のコンポーネント（TraceAndOptimize...で始まるもの）を削除します。"), removeAaoComponents);
    
    GUILayout.Space(20);
    
    if (GUILayout.Button("Remove Components from Hierarchy"))
    {
      RemoveSelectedComponents();
    }
  }

  private void RemoveSelectedComponents()
  {
    if (targetObject == null)
    {
      Debug.LogError("[RemoveComponent] Target Object を設定してください。");
      return;
    }

    if (!removeVrcComponents && !removeMaComponents && !removeAaoComponents)
    {
      Debug.LogWarning("[RemoveComponent] 削除する条件を少なくとも1つチェックしてください。");
      return;
    }

    Undo.SetCurrentGroupName("Remove Components from " + targetObject.name + " Hierarchy");
    int undoGroup = Undo.GetCurrentGroup();

    try
    {
      List<Component> componentsToRemove = new List<Component>();
      Component[] allComponents = targetObject.GetComponentsInChildren<Component>(true);

      foreach (var component in allComponents)
      {
        if (component == null || component is Transform)
        {
          continue;
        }

        string componentName = component.GetType().Name;

        bool shouldRemove =
            (removeVrcComponents && componentName.StartsWith("VRC")) ||
            (removeVrcComponents && componentName.StartsWith("Pipeline Manager")) ||
            (removeMaComponents && componentName.StartsWith("ModularAvatar")) ||
            (removeAaoComponents && componentName.StartsWith("TraceAndOptimize"));

        if (shouldRemove)
        {
          componentsToRemove.Add(component);
          Debug.Log($"[RemoveComponent] '{componentName}'.");
        }
      }

      if (componentsToRemove.Count > 0)
      {
        foreach(var component in componentsToRemove)
        {
          if(component != null) 
          {
            // 1. 破壊する前に、ログ用の情報を変数に保存する
            string componentNameForLog = component.GetType().Name;
            string gameObjectNameForLog = component.gameObject.name;

            // 2. コンポーネントを破壊する
            Undo.DestroyObjectImmediate(component);
            
            // 3. 保存しておいた変数を使い、安全にログを出力する
            Debug.Log($"[RemoveComponent] Removed component '{componentNameForLog}' from '{gameObjectNameForLog}'.");
          }
        }
        Debug.Log($"[RemoveComponent] ----- Finished: Removed {componentsToRemove.Count} component(s) successfully from the hierarchy. -----");
      }
      else
      {
        Debug.LogWarning("[RemoveComponent] No matching components found to remove in the hierarchy.");
      }
    }
    catch (System.Exception e)
    {
      Debug.LogError($"[RemoveComponent] An error occurred: {e.Message}\n{e.StackTrace}");
    }
    finally
    {
      Undo.CollapseUndoOperations(undoGroup);
    }
  }
}