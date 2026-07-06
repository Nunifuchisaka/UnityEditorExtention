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

    private bool copyAllComponents = true;
    private bool copyVrcComponents = true;
    private bool copyMaComponents = true;
    private bool copyAaoComponents = true;
    private bool copyFloorAdjuster = true;

    [MenuItem("Tools/Nunifuchisaka/Component Copier...", false, 100)]
    public static void ShowWindow()
    {
      GetWindow<ComponentCopier>("Component Copier");
    }

    private void OnGUI()
    {
      GUILayout.Label("Copy Specific Components", EditorStyles.boldLabel);
      DocumentationLink.Draw();
      sourceObject = (GameObject)EditorGUILayout.ObjectField("From", sourceObject, typeof(GameObject), true);
      destinationObject = (GameObject)EditorGUILayout.ObjectField("To", destinationObject, typeof(GameObject), true);
      GUILayout.Space(10);
      EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
      copyAllComponents = EditorGUILayout.Toggle(new GUIContent("All Components", "Transform・Renderer・MeshFilterを除くすべてのコンポーネントをコピーします。"), copyAllComponents);
      EditorGUI.BeginDisabledGroup(copyAllComponents);
      copyVrcComponents = EditorGUILayout.Toggle(new GUIContent("VRC", "VRChat SDK関連のコンポーネントをコピーします。"), copyVrcComponents);
      copyMaComponents = EditorGUILayout.Toggle(new GUIContent("ModularAvatar", "Modular Avatar関連のコンポーネントをコピーします。"), copyMaComponents);
      copyAaoComponents = EditorGUILayout.Toggle(new GUIContent("AAO TraceAndOptimize", "TraceAndOptimizeコンポーネントをコピーします。"), copyAaoComponents);
      copyFloorAdjuster = EditorGUILayout.Toggle(new GUIContent("FloorAdjuster", "FloorAdjusterコンポーネントをコピーします。"), copyFloorAdjuster);
      EditorGUI.EndDisabledGroup();

      GUILayout.Space(20);
      if (GUILayout.Button("Copy Components"))
      {
        ExecuteCopy(sourceObject, destinationObject, copyVrcComponents, copyMaComponents, copyAaoComponents, copyFloorAdjuster, copyAllComponents);
      }
    }

    public static void ExecuteCopy(GameObject source, GameObject dest, bool doCopyVrc, bool doCopyMa, bool doCopyAao, bool doCopyFloorAdjuster, bool doCopyAll = false)
    {
      if (source == null || dest == null)
      {
        Debug.LogError("[ComponentCopier] From と To の両方を設定してください。");
        return;
      }
      if (!doCopyAll && !doCopyVrc && !doCopyMa && !doCopyAao && !doCopyFloorAdjuster)
      {
        Debug.LogWarning("[ComponentCopier] コピーする条件を少なくとも1つチェックしてください。");
        return;
      }

      Undo.SetCurrentGroupName("Copy Components Recursively");
      int group = Undo.GetCurrentGroup();
      try
      {
        Debug.Log($"[ComponentCopier] ----- Start copying from '{source.name}' to '{dest.name}' -----" + (doCopyAll ? " (All Components)" : ""));
        Debug.Log("[ComponentCopier] Pass 1: Replicating hierarchy...");
        ReplicateHierarchyRecursively(source.transform, dest.transform, doCopyVrc, doCopyMa, doCopyAao, doCopyFloorAdjuster, doCopyAll);
        Debug.Log("[ComponentCopier] Pass 2: Copying components...");
        CopyComponentsRecursively(source.transform, dest.transform, doCopyVrc, doCopyMa, doCopyAao, doCopyFloorAdjuster, doCopyAll);
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

    private static void ReplicateHierarchyRecursively(Transform source, Transform parentInDest, bool copyVrc, bool copyMa, bool copyAao, bool copyFloorAdjuster, bool copyAll)
    {
      foreach (Transform sourceChild in source)
      {
        if (HierarchyHasCopyableComponents(sourceChild, copyVrc, copyMa, copyAao, copyFloorAdjuster, copyAll))
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
            // アクティブ状態もコピーする（SyncActiveStateはComponentCopierより先に実行されるため、ここで作られたオブジェクトは対象外）
            newDestGo.SetActive(sourceChild.gameObject.activeSelf);
          }
          ReplicateHierarchyRecursively(sourceChild, destChild, copyVrc, copyMa, copyAao, copyFloorAdjuster, copyAll);
        }
      }
    }

    private static bool HierarchyHasCopyableComponents(Transform target, bool copyVrc, bool copyMa, bool copyAao, bool copyFloorAdjuster, bool copyAll)
    {
        foreach(var component in target.GetComponents<Component>())
        {
            if (component == null) continue;
            if (IsAlwaysExcluded(component)) continue;

            if (copyAll || ComponentFilter.Matches(component, copyVrc, copyMa, copyAao, copyFloorAdjuster)) return true;
        }

        foreach(Transform child in target)
        {
            if (HierarchyHasCopyableComponents(child, copyVrc, copyMa, copyAao, copyFloorAdjuster, copyAll))
            {
                return true;
            }
        }
        return false;
    }

    private static void CopyComponentsRecursively(Transform source, Transform destination, bool copyVrc, bool copyMa, bool copyAao, bool copyFloorAdjuster, bool copyAll)
    {
      Component[] sourceComponents = source.GetComponents<Component>().OrderBy(c => GetSortPriority(c)).ToArray();
      // 同じ型のコンポーネントが同一GameObject上に複数ある場合、型ごとの出現順（n番目同士）で対応付ける
      var typeOccurrence = new Dictionary<System.Type, int>();
      foreach (var sourceComponent in sourceComponents)
      {
        if (sourceComponent == null) continue;
        if (IsAlwaysExcluded(sourceComponent))
        {
            continue;
        }

        System.Type componentType = sourceComponent.GetType();

        if (copyAll || ComponentFilter.Matches(sourceComponent, copyVrc, copyMa, copyAao, copyFloorAdjuster))
        {
          int occurrenceIndex;
          typeOccurrence.TryGetValue(componentType, out occurrenceIndex);
          typeOccurrence[componentType] = occurrenceIndex + 1;

          Component[] destComponents = destination.GetComponents(componentType);
          Component destComponent;
          if (occurrenceIndex < destComponents.Length)
          {
            destComponent = destComponents[occurrenceIndex];
            Undo.RecordObject(destComponent, "Modify Component");
          }
          else
          {
            destComponent = Undo.AddComponent(destination.gameObject, componentType);
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
        if (destinationChild != null) CopyComponentsRecursively(sourceChild, destinationChild, copyVrc, copyMa, copyAao, copyFloorAdjuster, copyAll);
      }
    }

    // どのモードでもコピー対象にしないコンポーネント。
    // Transformは TransformCopier、Renderer（Material・BlendShape）は MaterialCopier / BlendShapeCopier が担当する。
    // MeshFilterはRendererとペアで意味を持つため、Rendererと合わせて除外する。
    private static bool IsAlwaysExcluded(Component component)
    {
      return component is Transform || component is Renderer || component is MeshFilter;
    }

    private static void RemapAllReferencesRecursively(GameObject sourceRoot, GameObject destRoot)
    {
      RemapAllReferencesRecursivelyInternal(sourceRoot, destRoot, destRoot.transform);
    }

    private static void RemapAllReferencesRecursivelyInternal(GameObject sourceRoot, GameObject destRoot, Transform currentDest)
    {
      foreach (Component component in currentDest.GetComponents<Component>())
      {
        if (component == null) continue;
        if (component is Transform) continue;

        Undo.RecordObject(component, "Remap References");
        ProcessFieldsRecursively(component, component.GetType(), sourceRoot, destRoot, MaxRemapDepth);
        // リフレクションでのフィールド書き込みはUnityに変更が通知されないため、明示的に永続化する
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(component);
      }
      foreach (Transform child in currentDest)
      {
        RemapAllReferencesRecursivelyInternal(sourceRoot, destRoot, child);
      }
    }

    private static int GetSortPriority(Component component)
    {
      if (component == null) return 99;

      string componentName = component.GetType().Name;
      if (componentName == "VRCPhysBoneCollider") return 0;
      if (componentName == "VRCPhysBone") return 1;
      if (componentName.StartsWith("ModularAvatar")) return 3;
      if (componentName == "TraceAndOptimize") return 4;
      if (componentName == "FloorAdjuster") return 5;
      return 2;
    }
    
    // ネストしたSerializableクラス・構造体を再帰する深さの上限（自己参照による無限再帰を防ぐ）
    private const int MaxRemapDepth = 6;

    private static void ProcessFieldsRecursively(object targetObject, System.Type targetType, GameObject sourceRoot, GameObject destRoot, int depth)
    {
      if (targetObject == null || depth <= 0) return;
      FieldInfo[] fields = targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (FieldInfo field in fields)
      {
        object fieldValue = field.GetValue(targetObject);
        ProcessValue(fieldValue, field.FieldType, (newValue) => field.SetValue(targetObject, newValue), sourceRoot, destRoot, depth);
      }
    }

    private static void ProcessValue(object value, System.Type valueType, System.Action<object> setter, GameObject sourceRoot, GameObject destRoot, int depth)
    {
      if (value == null || depth <= 0) return;
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
        {
          for (int i = 0; i < array.Length; i++)
            if (array.GetValue(i) is UnityEngine.Object elem)
            {
              var newReference = RemapObject(elem, sourceRoot, destRoot);
              if (newReference != null) array.SetValue(newReference, i);
            }
        }
        else if (IsRecursableType(elementType))
        {
          // 構造体・Serializableクラス要素の中の参照もリマップする（例：VRCHeadChop.targetBones[n].transform）
          for (int i = 0; i < array.Length; i++)
          {
            object element = array.GetValue(i);
            if (element == null) continue;
            ProcessFieldsRecursively(element, element.GetType(), sourceRoot, destRoot, depth - 1);
            if (elementType.IsValueType) array.SetValue(element, i);
          }
        }
      }
      else if (typeof(IList).IsAssignableFrom(valueType) && valueType.IsGenericType)
      {
        var list = value as IList;
        System.Type genericType = valueType.GetGenericArguments()[0];
        if (typeof(UnityEngine.Object).IsAssignableFrom(genericType))
        {
          for (int i = 0; i < list.Count; i++)
            if(list[i] is UnityEngine.Object elem)
            {
              var newReference = RemapObject(elem, sourceRoot, destRoot);
              if (newReference != null) list[i] = newReference;
            }
        }
        else if (IsRecursableType(genericType))
        {
          for (int i = 0; i < list.Count; i++)
          {
            object element = list[i];
            if (element == null) continue;
            ProcessFieldsRecursively(element, element.GetType(), sourceRoot, destRoot, depth - 1);
            if (genericType.IsValueType) list[i] = element;
          }
        }
      }
      else if (IsRecursableType(valueType))
      {
        // 構造体に加え、Serializableクラスの中も再帰する（例：ModularAvatarのAvatarObjectReference.targetObject）
        ProcessFieldsRecursively(value, value.GetType(), sourceRoot, destRoot, depth - 1);
        if (valueType.IsValueType) setter(value);
      }
    }

    private static bool IsRecursableType(System.Type type)
    {
      if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return false;
      if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
      string ns = type.Namespace ?? "";
      if (ns.StartsWith("System")) return false;
      if (type.IsClass && ns.StartsWith("UnityEngine")) return false;
      return true;
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