#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

namespace Nunifuchisaka
{
  public class ComponentCopier : EditorWindow
  {
    private GameObject sourceObject;
    private GameObject destinationObject;

    private bool copyVrcComponents = true;
    private bool copyMaComponents = true;
    private bool copyAaoComponents = true;

    [MenuItem("Tools/Nunifuchisaka/Component Copier...")]
    public static void ShowWindow()
    {
      GetWindow<ComponentCopier>("Component Copier");
    }

    private void OnGUI()
    {
      GUILayout.Label("Copy Specific Components", EditorStyles.boldLabel);
      sourceObject = (GameObject)EditorGUILayout.ObjectField("From", sourceObject, typeof(GameObject), true);
      destinationObject = (GameObject)EditorGUILayout.ObjectField("To", destinationObject, typeof(GameObject), true);
      GUILayout.Space(10);
      EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
      copyVrcComponents = EditorGUILayout.Toggle(new GUIContent("VRC", "VRChat SDK関連のコンポーネントをコピーします。"), copyVrcComponents);
      copyMaComponents = EditorGUILayout.Toggle(new GUIContent("ModularAvatar", "Modular Avatar関連のコンポーネントをコピーします。"), copyMaComponents);
      copyAaoComponents = EditorGUILayout.Toggle(new GUIContent("AAO TraceAndOptimize", "TraceAndOptimizeコンポーネントをコピーします。"), copyAaoComponents);
      GUILayout.Space(20);
      if (GUILayout.Button("Copy Components"))
      {
        ExecuteCopy(sourceObject, destinationObject, copyVrcComponents, copyMaComponents, copyAaoComponents);
      }
    }

    public static void ExecuteCopy(GameObject source, GameObject dest, bool doCopyVrc, bool doCopyMa, bool doCopyAao)
    {
      if (source == null || dest == null)
      {
        Debug.LogError("[ComponentCopier] From と To の両方を設定してください。");
        return;
      }
      if (!doCopyVrc && !doCopyMa && !doCopyAao)
      {
        Debug.LogWarning("[ComponentCopier] コピーする条件を少なくとも1つチェックしてください。");
        return;
      }

      Undo.SetCurrentGroupName("Copy Components Recursively");
      int group = Undo.GetCurrentGroup();
      try
      {
        Debug.Log($"[ComponentCopier] ----- Start copying from '{source.name}' to '{dest.name}' -----");
        Debug.Log("[ComponentCopier] Pass 1: Replicating hierarchy...");
        ReplicateHierarchyRecursively(source.transform, dest.transform, doCopyVrc, doCopyMa, doCopyAao);
        Debug.Log("[ComponentCopier] Pass 2: Copying components...");
        CopyComponentsRecursively(source.transform, dest.transform, doCopyVrc, doCopyMa, doCopyAao);
        Debug.Log("[ComponentCopier] Pass 3: Remapping references...");
        RemapAllReferencesRecursively(source, dest);
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

    // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
    // 【最終修正】インテリジェントな階層複製ロジック
    // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
    private static void ReplicateHierarchyRecursively(Transform source, Transform parentInDest, bool copyVrc, bool copyMa, bool copyAao)
    {
      foreach (Transform sourceChild in source)
      {
        // この子オブジェクト、またはその子孫にコピー対象のコンポーネントがあるか事前にチェック
        if (HierarchyHasCopyableComponents(sourceChild, copyVrc, copyMa, copyAao))
        {
          Transform destChild = parentInDest.Find(sourceChild.name);
          if (destChild == null)
          {
            GameObject newDestGo = new GameObject(sourceChild.name);
            Undo.RegisterCreatedObjectUndo(newDestGo, "Create GameObject");
            destChild = newDestGo.transform;
            destChild.SetParent(parentInDest);
            destChild.localPosition = sourceChild.localPosition;
            destChild.localRotation = sourceChild.localRotation;
            destChild.localScale = sourceChild.localScale;
          }
          // コピー対象がある階層のみ、再帰処理を続行
          ReplicateHierarchyRecursively(sourceChild, destChild, copyVrc, copyMa, copyAao);
        }
      }
    }

    // 【新規追加】指定されたTransformまたはその子孫にコピー対象コンポーネントが存在するかを判定する
    private static bool HierarchyHasCopyableComponents(Transform target, bool copyVrc, bool copyMa, bool copyAao)
    {
      foreach (var component in target.GetComponents<Component>())
      {
        if (component is Transform || component is Renderer) continue;

        string componentName = component.GetType().Name;
        string componentNamespace = component.GetType().Namespace ?? "";

        bool isCopyTarget = (copyVrc && (componentName.StartsWith("VRC") || componentNamespace.StartsWith("VRC"))) ||
                            (copyMa && (componentName.StartsWith("MA") || componentName.StartsWith("ModularAvatar"))) ||
                            (copyAao && componentName.StartsWith("TraceAndOptimize"));

        if (isCopyTarget) return true; // 対象を発見
      }

      // 自分になければ、子を再帰的に探索
      foreach (Transform child in target)
      {
        if (HierarchyHasCopyableComponents(child, copyVrc, copyMa, copyAao))
        {
          return true; // 子孫から対象を発見
        }
      }

      return false; // この階層以下には何もなかった
    }

    private static void CopyComponentsRecursively(Transform source, Transform destination, bool copyVrc, bool copyMa, bool copyAao)
    {
      Component[] sourceComponents = source.GetComponents<Component>().OrderBy(c => GetSortPriority(c)).ToArray();
      foreach (var sourceComponent in sourceComponents)
      {
        if (sourceComponent is Transform || sourceComponent is Renderer)
        {
          continue;
        }

        System.Type componentType = sourceComponent.GetType();
        string componentName = componentType.Name;
        string componentNamespace = componentType.Namespace ?? "";

        bool shouldCopy = (copyVrc && (componentName.StartsWith("VRC") || componentNamespace.StartsWith("VRC"))) ||
                         (copyMa && (componentName.StartsWith("MA") || componentName.StartsWith("ModularAvatar"))) ||
                         (copyAao && componentName.StartsWith("TraceAndOptimize"));

        if (shouldCopy)
        {
          Component destComponent = destination.GetComponent(componentType);
          if (destComponent == null) destComponent = Undo.AddComponent(destination.gameObject, componentType);
          else Undo.RecordObject(destComponent, "Modify Component");

          if (UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent))
          {
            UnityEditorInternal.ComponentUtility.PasteComponentValues(destComponent);
          }
        }
      }
      foreach (Transform sourceChild in source)
      {
        Transform destinationChild = destination.Find(sourceChild.name);
        if (destinationChild != null) CopyComponentsRecursively(sourceChild, destinationChild, copyVrc, copyMa, copyAao);
      }
    }

    private static void RemapAllReferencesRecursively(GameObject sourceRoot, GameObject destRoot)
    {
      RemapAllReferencesRecursivelyInternal(sourceRoot, destRoot, destRoot.transform);
    }

    private static void RemapAllReferencesRecursivelyInternal(GameObject sourceRoot, GameObject destRoot, Transform currentDest)
    {
      foreach (Component component in currentDest.GetComponents<Component>())
      {
        ProcessFieldsRecursively(component, component.GetType(), sourceRoot, destRoot);
      }
      foreach (Transform child in currentDest)
      {
        RemapAllReferencesRecursivelyInternal(sourceRoot, destRoot, child);
      }
    }

    private static int GetSortPriority(Component component)
    {
      string componentName = component.GetType().Name;
      if (componentName == "VRCPhysBoneCollider") return 0;
      if (componentName == "VRCPhysBone") return 1;
      if (componentName.StartsWith("MA") || componentName.StartsWith("ModularAvatar")) return 3;
      if (componentName == "TraceAndOptimize") return 4;
      return 2;
    }

    private static void ProcessFieldsRecursively(object targetObject, System.Type targetType, GameObject sourceRoot, GameObject destRoot)
    {
      FieldInfo[] fields = targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (FieldInfo field in fields)
      {
        object fieldValue = field.GetValue(targetObject);
        ProcessValue(fieldValue, field.FieldType, (newValue) => field.SetValue(targetObject, newValue), sourceRoot, destRoot);
      }
    }

    private static void ProcessValue(object value, System.Type valueType, System.Action<object> setter, GameObject sourceRoot, GameObject destRoot)
    {
      if (value == null) return;
      if (typeof(UnityEngine.Object).IsAssignableFrom(valueType))
      {
        var newReference = RemapObject((UnityEngine.Object)value, sourceRoot, destRoot);
        if (newReference != null) setter(newReference);
      }
      else if (valueType.IsArray)
      {
        var array = value as System.Array;
        System.Type elementType = valueType.GetElementType();
        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
          for (int i = 0; i < array.Length; i++)
            if (array.GetValue(i) is UnityEngine.Object elem)
            {
              var newReference = RemapObject(elem, sourceRoot, destRoot);
              if (newReference != null) array.SetValue(newReference, i);
            }
      }
      else if (typeof(IList).IsAssignableFrom(valueType) && valueType.IsGenericType)
      {
        var list = value as IList;
        System.Type genericType = valueType.GetGenericArguments()[0];
        if (typeof(UnityEngine.Object).IsAssignableFrom(genericType))
          for (int i = 0; i < list.Count; i++)
            if (list[i] is UnityEngine.Object elem)
            {
              var newReference = RemapObject(elem, sourceRoot, destRoot);
              if (newReference != null) list[i] = newReference;
            }
      }
      else if (valueType.IsValueType && !valueType.IsPrimitive && valueType.Namespace != null && !valueType.Namespace.StartsWith("System"))
      {
        ProcessFieldsRecursively(value, valueType, sourceRoot, destRoot);
        setter(value);
      }
    }

    private static UnityEngine.Object RemapObject(UnityEngine.Object sourceRefObject, GameObject sourceRoot, GameObject destRoot)
    {
      if (sourceRefObject == null) return null;
      GameObject sourceGo = null;
      if (sourceRefObject is GameObject) sourceGo = (GameObject)sourceRefObject;
      if (sourceRefObject is Component) sourceGo = ((Component)sourceRefObject).gameObject;
      if (sourceGo == null || !sourceGo.transform.IsChildOf(sourceRoot.transform)) return null;
      string path = GetRelativePath(sourceRoot.transform, sourceGo.transform);
      Transform destinationTransform = (path == null) ? destRoot.transform : destRoot.transform.Find(path);
      if (destinationTransform == null) return null;
      if (sourceRefObject is GameObject) return destinationTransform.gameObject;
      if (sourceRefObject is Component) return destinationTransform.GetComponent(sourceRefObject.GetType());
      return null;
    }
    private static string GetRelativePath(Transform root, Transform child)
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
#endif