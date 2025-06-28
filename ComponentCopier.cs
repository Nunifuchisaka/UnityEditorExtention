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
    private bool copyBlendShapes = true;
    private bool copyMaterials = true;

    [MenuItem("Tools/Nunifuchisaka/ComponentCopier...")]
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
      copyTransform = EditorGUILayout.Toggle(new GUIContent("Transform", "既存オブジェクトのTransform情報（位置、回転、スケール）を上書きコピーします。"), copyTransform);
      copyMaterials = EditorGUILayout.Toggle(new GUIContent("Materials", "Rendererのマテリアルをコピーします。"), copyMaterials);
      copyBlendShapes = EditorGUILayout.Toggle(new GUIContent("BlendShapes", "SkinnedMeshRendererのBlendShape（シェイプキー）のウェイト値をコピーします。"), copyBlendShapes);
      copyVrcComponents = EditorGUILayout.Toggle(new GUIContent("VRC", "VRChat SDK関連のコンポーネントをコピーします。"), copyVrcComponents);
      copyMaComponents = EditorGUILayout.Toggle(new GUIContent("ModularAvatar", "Modular Avatar関連のコンポーネントをコピーします。"), copyMaComponents);
      copyAaoComponents = EditorGUILayout.Toggle(new GUIContent("AAO TraceAndOptimize", "TraceAndOptimizeコンポーネントをコピーします。"), copyAaoComponents);
      GUILayout.Space(20);
      if (GUILayout.Button("Copier"))
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
      if (!copyVrcComponents && !copyMaComponents && !copyAaoComponents && !copyTransform && !copyBlendShapes && !copyMaterials)
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
        if (sourceComponent is Transform && (!copyTransform || source == sourceObject.transform)) continue;
        System.Type componentType = sourceComponent.GetType();
        string componentName = componentType.Name;
        bool shouldCopy = (sourceComponent is Transform && copyTransform);
        if (!shouldCopy)
        {
            string componentNamespace = componentType.Namespace ?? "";
            shouldCopy = (copyMaterials && sourceComponent is Renderer) ||
                         (copyBlendShapes && sourceComponent is SkinnedMeshRenderer) ||
                         (copyVrcComponents && (componentName.StartsWith("VRC") || componentNamespace.StartsWith("VRC"))) ||
                         (copyMaComponents && (componentName.StartsWith("MA") || componentName.StartsWith("ModularAvatar"))) ||
                         (copyAaoComponents && componentName.StartsWith("TraceAndOptimize"));
        }
        if (shouldCopy)
        {
          Component destComponent = destination.GetComponent(componentType);
          if (destComponent == null) destComponent = Undo.AddComponent(destination.gameObject, componentType);
          else Undo.RecordObject(destComponent, "Paste Component Values");

          Transform originalRootBone = null;
          Transform originalProbeAnchor = null;

          // ペースト前に、コピー先の現在の値を保存
          if (destComponent is SkinnedMeshRenderer smr) originalRootBone = smr.rootBone;
          if (destComponent is Renderer rend) originalProbeAnchor = rend.probeAnchor;

          if (UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent))
          {
            if (UnityEditorInternal.ComponentUtility.PasteComponentValues(destComponent))
            {
              // ペースト後、保存しておいた元の値を書き戻す
              if (destComponent is SkinnedMeshRenderer smr) smr.rootBone = originalRootBone;
              if (destComponent is Renderer rend) rend.probeAnchor = originalProbeAnchor;

              if (copyBlendShapes && destComponent is SkinnedMeshRenderer destSMR && sourceComponent is SkinnedMeshRenderer sourceSMR) CopierBlendShapes(sourceSMR, destSMR);
              if (copyMaterials && destComponent is Renderer destRenderer && sourceComponent is Renderer sourceRenderer) CopierMaterials(sourceRenderer, destRenderer);
            }
          }
        }
      }
      foreach (Transform sourceChild in source)
      {
        Transform destinationChild = destination.Find(sourceChild.name);
        if (destinationChild != null) CopyComponentsRecursively(sourceChild, destinationChild);
      }
    }

    private void CopierBlendShapes(SkinnedMeshRenderer source, SkinnedMeshRenderer dest)
    {
      if (source.sharedMesh == null || dest.sharedMesh == null) return;
      Undo.RecordObject(dest, "Copier BlendShapes");
      for (int i = 0; i < source.sharedMesh.blendShapeCount; i++)
      {
        string blendShapeName = source.sharedMesh.GetBlendShapeName(i);
        int destIndex = dest.sharedMesh.GetBlendShapeIndex(blendShapeName);
        if (destIndex != -1) dest.SetBlendShapeWeight(destIndex, source.GetBlendShapeWeight(i));
      }
      Debug.Log($"[ComponentCopier] Copied BlendShape values for '{dest.name}'.");
    }

    private void CopierMaterials(Renderer source, Renderer dest)
    {
      Undo.RecordObject(dest, "Copier Materials");
      dest.sharedMaterials = source.sharedMaterials;
      Debug.Log($"[ComponentCopier] Copied materials for '{dest.name}'.");
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
      if (component is SkinnedMeshRenderer) return -1;
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
        ProcessValue(fieldValue, field.FieldType, (newValue) => field.SetValue(targetObject, newValue));
      }
    }

    private void ProcessValue(object value, System.Type valueType, System.Action<object> setter)
    {
      if (value == null) return;
      if (typeof(UnityEngine.Object).IsAssignableFrom(valueType))
      {
        var newReference = RemapObject((UnityEngine.Object)value);
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
              var newReference = RemapObject(elem);
              if (newReference != null) array.SetValue(newReference, i);
            }
      }
      else if (typeof(IList).IsAssignableFrom(valueType) && valueType.IsGenericType)
      {
        var list = value as IList;
        System.Type genericType = valueType.GetGenericArguments()[0];
        if (typeof(UnityEngine.Object).IsAssignableFrom(genericType))
          for (int i = 0; i < list.Count; i++)
            if(list[i] is UnityEngine.Object elem)
            {
              var newReference = RemapObject(elem);
              if (newReference != null) list[i] = newReference;
            }
      }
      else if (valueType.IsValueType && !valueType.IsPrimitive && valueType.Namespace != null && !valueType.Namespace.StartsWith("System"))
      {
        ProcessFieldsRecursively(value, valueType);
        setter(value);
      }
    }

    private UnityEngine.Object RemapObject(UnityEngine.Object sourceRefObject)
    {
      if (sourceRefObject == null) return null;
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