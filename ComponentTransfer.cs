using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Nunifuchisaka
{
  public class ComponentCopier : EditorWindow
  {
    private GameObject sourceObject;
    private GameObject destinationObject;

    private bool copyVrcComponents = true;
    private bool copyMaComponents = true;
    private bool copyAaoComponents = true;
    
    private bool copyTransform = false;

    [MenuItem("Tools/Nunifuchisaka/ComponentCopier")]
    public static void ShowWindow()
    {
      GetWindow<ComponentCopier>("ComponentCopier");
    }

    private void OnGUI()
    {
      GUILayout.Label("Select Target GameObject", EditorStyles.boldLabel);

      sourceObject = (GameObject)EditorGUILayout.ObjectField("From", sourceObject, typeof(GameObject), true);
      destinationObject = (GameObject)EditorGUILayout.ObjectField("To", destinationObject, typeof(GameObject), true);

      GUILayout.Space(10);
      
      EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
      copyTransform = EditorGUILayout.Toggle(new GUIContent("Transform", "有効にすると、既存オブジェクトのTransform情報（位置、回転、スケール）を上書きコピーします。"), copyTransform);
      copyVrcComponents = EditorGUILayout.Toggle(new GUIContent("VRC", "VRChat SDK関連のコンポーネントをコピーします。"), copyVrcComponents);
      copyMaComponents = EditorGUILayout.Toggle(new GUIContent("ModularAvatar", "Modular Avatar関連のコンポーネントをコピーします。"), copyMaComponents);
      copyAaoComponents = EditorGUILayout.Toggle(new GUIContent("AAO TraceAndOptimize", "TraceAndOptimizeコンポーネントをコピーします。"), copyAaoComponents);

      GUILayout.Space(20);

      if (GUILayout.Button("Transfer"))
      {
        CopyComponents();
      }
    }

    private void CopyComponents()
    {
      if (sourceObject == null || destinationObject == null)
      {
        Debug.LogError("[ComponentCopier] From と To の両方を設定してください。");
        return;
      }

      if (!copyVrcComponents && !copyMaComponents && !copyAaoComponents && !copyTransform)
      {
        Debug.LogWarning("[ComponentCopier] コピーする条件を少なくとも1つチェックしてください。");
        return;
      }

      Undo.SetCurrentGroupName("Copy Components Recursively");
      int group = Undo.GetCurrentGroup();

      try
      {
        Debug.Log($"[ComponentCopier] ----- Start copying from '{sourceObject.name}' to '{destinationObject.name}' -----");

        Debug.Log("[ComponentCopier] Pass 1: Replicating hierarchy...");
        ReplicateHierarchyRecursively(sourceObject.transform, destinationObject.transform);

        Debug.Log("[ComponentCopier] Pass 2: Copying components...");
        CopyComponentsRecursively(sourceObject.transform, destinationObject.transform);

        Debug.Log("[ComponentCopier] Pass 3: Remapping references...");
        RemapAllReferencesRecursively(destinationObject.transform);

        Debug.Log("[ComponentCopier] ----- All passes completed successfully. -----");
      }
      catch (System.Exception e)
      {
        Debug.LogError($"[ComponentCopier] An error occurred: {e.Message}\n{e.StackTrace}");
      }
      finally
      {
        Undo.CollapseUndoOperations(group);
      }
    }

    private void ReplicateHierarchyRecursively(Transform source, Transform parentInDest)
    {
      foreach (Transform sourceChild in source)
      {
        Transform destChild = parentInDest.Find(sourceChild.name);
        if (destChild == null)
        {
          GameObject newDestGo = new GameObject(sourceChild.name);
          Undo.RegisterCreatedObjectUndo(newDestGo, "Replicate Hierarchy");
          destChild = newDestGo.transform;
          destChild.SetParent(parentInDest);
          destChild.localPosition = sourceChild.localPosition;
          destChild.localRotation = sourceChild.localRotation;
          destChild.localScale = sourceChild.localScale;
        }
        ReplicateHierarchyRecursively(sourceChild, destChild);
      }
    }

    private void CopyComponentsRecursively(Transform source, Transform destination)
    {
      Component[] sourceComponents = source.GetComponents<Component>().OrderBy(c => GetSortPriority(c)).ToArray();

      foreach (var sourceComponent in sourceComponents)
      {
        // 【修正】copyTransformフラグがfalseの場合のみ、Transformをスキップする
        if (sourceComponent is Transform && !copyTransform) continue;

        System.Type componentType = sourceComponent.GetType();
        string componentName = componentType.Name;
        
        // Transformをコピーする場合、shouldCopyをtrueにする
        bool shouldCopy = (sourceComponent is Transform && copyTransform);

        if (!shouldCopy)
        {
            string componentNamespace = componentType.Namespace ?? "";
            shouldCopy = (copyVrcComponents && (componentName.StartsWith("VRC") || componentNamespace.StartsWith("VRC"))) ||
                         (copyMaComponents && (componentName.StartsWith("MA") || componentName.StartsWith("ModularAvatar"))) ||
                         (copyAaoComponents && componentName.StartsWith("TraceAndOptimize"));
        }

        if (shouldCopy)
        {
          Component destComponent = destination.GetComponent(componentType);
          if (destComponent == null)
          {
            destComponent = Undo.AddComponent(destination.gameObject, componentType);
          }
          else
          {
            Undo.RecordObject(destComponent, "Paste Component Values");
          }

          if (UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent))
          {
            UnityEditorInternal.ComponentUtility.PasteComponentValues(destComponent);
          }
        }
      }

      foreach (Transform sourceChild in source)
      {
        Transform destinationChild = destination.Find(sourceChild.name);
        if (destinationChild != null)
        {
          CopyComponentsRecursively(sourceChild, destinationChild);
        }
      }
    }
    
    private void RemapAllReferencesRecursively(Transform dest)
    {
      foreach (Component component in dest.GetComponents<Component>())
      {
        RemapReferences(component);
      }

      foreach (Transform child in dest)
      {
        RemapAllReferencesRecursively(child);
      }
    }

    private int GetSortPriority(Component component)
    {
      string componentName = component.GetType().Name;
      if (componentName == "VRCPhysBoneCollider") return 0;
      if (componentName == "VRCPhysBone") return 1;
      if (componentName.StartsWith("MA") || componentName.StartsWith("ModularAvatar")) return 3;
      if (componentName == "TraceAndOptimize") return 4;
      return 2;
    }

    private void RemapReferences(object targetObject)
    {
      ProcessFieldsRecursively(targetObject, targetObject.GetType());
    }
    
    private void ProcessFieldsRecursively(object targetObject, System.Type targetType)
    {
      FieldInfo[] fields = targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (FieldInfo field in fields)
      {
        object fieldValue = field.GetValue(targetObject);
        if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
        {
          if (fieldValue is UnityEngine.Object sourceReference && sourceReference != null)
          {
            var newReference = RemapObject(sourceReference);
            if (newReference != null) field.SetValue(targetObject, newReference);
          }
        }
        else if (field.FieldType.IsArray && fieldValue is System.Array array)
        {
          System.Type elementType = field.FieldType.GetElementType();
          if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
          {
            for (int i = 0; i < array.Length; i++)
            {
              if (array.GetValue(i) is UnityEngine.Object sourceReference && sourceReference != null)
              {
                var newReference = RemapObject(sourceReference);
                if (newReference != null) array.SetValue(newReference, i);
              }
            }
          }
        }
        else if (typeof(IList).IsAssignableFrom(field.FieldType) && field.FieldType.IsGenericType && fieldValue is IList list)
        {
          System.Type genericType = field.FieldType.GetGenericArguments()[0];
          if (typeof(UnityEngine.Object).IsAssignableFrom(genericType))
          {
            for (int i = 0; i < list.Count; i++)
            {
              if (list[i] is UnityEngine.Object sourceReference && sourceReference != null)
              {
                var newReference = RemapObject(sourceReference);
                if (newReference != null) list[i] = newReference;
              }
            }
          }
        }
        else if (field.FieldType.IsValueType && !field.FieldType.IsPrimitive && field.FieldType.Namespace != null && !field.FieldType.Namespace.StartsWith("System"))
        {
          ProcessFieldsRecursively(fieldValue, field.FieldType);
          field.SetValue(targetObject, fieldValue);
        }
      }
    }
    private UnityEngine.Object RemapObject(UnityEngine.Object sourceRefObject)
    {
      GameObject sourceGo = null;
      if (sourceRefObject is GameObject) sourceGo = (GameObject)sourceRefObject;
      if (sourceRefObject is Component) sourceGo = ((Component)sourceRefObject).gameObject;

      if (sourceGo == null || !sourceGo.transform.IsChildOf(sourceObject.transform)) return null;
      
      string path = GetRelativePath(sourceObject.transform, sourceGo.transform);
      Transform destinationTransform = (path == null) ? destinationObject.transform : destinationObject.transform.Find(path);

      if (destinationTransform == null) return null;

      if (sourceRefObject is GameObject) return destinationTransform.gameObject;
      if (sourceRefObject is Component) return destinationTransform.GetComponent(sourceRefObject.GetType());
      
      return null;
    }
    private string GetRelativePath(Transform root, Transform child)
    {
      if (child == root) return null;
      string path = child.name;
      Transform parent = child.parent;
      while (parent != null)
      {
        if (parent == root) return path;
        path = parent.name + "/" + path;
        parent = parent.parent;
      }
      return null;
    }
  }
}