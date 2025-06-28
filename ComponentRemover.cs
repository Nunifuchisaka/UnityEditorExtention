using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Nunifuchisaka
{
  public class ComponentRemover : EditorWindow
  {
    private GameObject rootObject;

    private bool removeVrcComponents = true;
    private bool removeMaComponents = true;
    private bool removeAaoComponents = true;

    [MenuItem("Tools/Nunifuchisaka/Component Remover...")]
    public static void ShowWindow()
    {
      GetWindow<ComponentRemover>("Component Remover");
    }

    private void OnGUI()
    {
      GUILayout.Label("Select Target GameObject", EditorStyles.boldLabel);
      
      rootObject = (GameObject)EditorGUILayout.ObjectField("Root Object", rootObject, typeof(GameObject), true);
      
      GUILayout.Space(10);
      
      EditorGUILayout.LabelField("Remove Conditions", EditorStyles.boldLabel);
      removeVrcComponents = EditorGUILayout.Toggle(new GUIContent("VRC", "VRChat SDK関連のコンポーネント（VRC...で始まるもの）を削除します。"), removeVrcComponents);
      removeMaComponents = EditorGUILayout.Toggle(new GUIContent("ModularAvatar", "Modular Avatar関連のコンポーネント（ModularAvatar...で始まるもの）を削除します。"), removeMaComponents);
      removeAaoComponents = EditorGUILayout.Toggle(new GUIContent("AAO TraceAndOptimize", "Avatar OptimizerのTraceAndOptimizeを削除します。"), removeAaoComponents);
      
      GUILayout.Space(20);
      
      if (GUILayout.Button("Remove"))
      {
        RemoveSelectedComponents();
      }
    }

    private void RemoveSelectedComponents()
    {
      if (rootObject == null)
      {
        Debug.LogError("[ComponentRemover] Root Object を設定してください。");
        return;
      }

      if (!removeVrcComponents && !removeMaComponents && !removeAaoComponents)
      {
        Debug.LogWarning("[ComponentRemover] 削除する条件を少なくとも1つチェックしてください。");
        return;
      }

      Undo.SetCurrentGroupName("Remove Components from " + rootObject.name + " Hierarchy");
      int undoGroup = Undo.GetCurrentGroup();

      try
      {
        List<Component> componentsToRemove = new List<Component>();
        Component[] allComponents = rootObject.GetComponentsInChildren<Component>(true);

        foreach (var component in allComponents)
        {
          if (component == null || component is Transform)
          {
            continue;
          }

          string componentName = component.GetType().Name;

          bool shouldRemove =
              (removeVrcComponents && componentName.StartsWith("VRC")) ||
              (removeMaComponents && componentName.StartsWith("ModularAvatar")) ||
              (removeAaoComponents && componentName.StartsWith("TraceAndOptimize"));

          if (shouldRemove)
          {
            componentsToRemove.Add(component);
          }
        }

        if (componentsToRemove.Count > 0)
        {
          foreach(var component in componentsToRemove)
          {
            if(component != null) 
            {
              string componentNameForLog = component.GetType().Name;
              string gameObjectNameForLog = component.gameObject.name;

              Undo.DestroyObjectImmediate(component);
              
              Debug.Log($"[ComponentRemover] Removed component '{componentNameForLog}' from '{gameObjectNameForLog}'.");
            }
          }
          Debug.Log($"[ComponentRemover] ----- Finished: Removed {componentsToRemove.Count} component(s) successfully from the hierarchy. -----");
        }
        else
        {
          Debug.LogWarning("[ComponentRemover] No matching components found to remove in the hierarchy.");
        }
      }
      catch (System.Exception e)
      {
        Debug.LogError($"[ComponentRemover] An error occurred: {e.Message}\n{e.StackTrace}");
      }
      finally
      {
        Undo.CollapseUndoOperations(undoGroup);
      }
    }
  }
}