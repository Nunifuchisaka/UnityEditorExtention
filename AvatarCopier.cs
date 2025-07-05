#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Nunifuchisaka
{
  public class AvatarCopier : EditorWindow
  {
    private GameObject sourceObject;
    private GameObject destinationObject;

    private bool copyVrcComponents = true;
    private bool copyMaComponents = true;
    private bool copyAaoComponents = true;

    [MenuItem("Tools/Nunifuchisaka/Avatar Copier...", false, 1)]
    public static void ShowWindow()
    {
      GetWindow<AvatarCopier>("Avatar Copier");
    }

    private void OnGUI()
    {
      GUILayout.Label("アバター総合コピー", EditorStyles.boldLabel);
      EditorGUILayout.Space(10);

      EditorGUILayout.BeginVertical(GUI.skin.box);
      GUILayout.Label("対象オブジェクトの指定", EditorStyles.boldLabel);
      EditorGUILayout.Space(5);

      sourceObject = (GameObject)EditorGUILayout.ObjectField("From", sourceObject, typeof(GameObject), true);
      EditorGUILayout.Space(5);
      destinationObject = (GameObject)EditorGUILayout.ObjectField("To", destinationObject, typeof(GameObject), true);

      EditorGUI.EndDisabledGroup();
      EditorGUILayout.EndVertical();

      EditorGUILayout.Space(10);

      bool canProcess = sourceObject != null && destinationObject != null;
      EditorGUI.BeginDisabledGroup(!canProcess);
      {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("すべての機能を順番に実行", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        if (GUILayout.Button("Copy All"))
        {
          PrefabInstantiatorLogic.DuplicatePrefabs(sourceObject, destinationObject);
          TransformCopier.ExecuteCopy(sourceObject, destinationObject);
          MaterialCopier.ExecuteCopy(sourceObject, destinationObject);
          BlendShapeCopier.ExecuteCopy(sourceObject, destinationObject);
          SyncActiveState.Execute(sourceObject, destinationObject);
          ComponentCopier.ExecuteCopy(sourceObject, destinationObject, copyVrcComponents, copyMaComponents, copyAaoComponents);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("機能ごとに個別で実行", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Prefabインスタンスをコピー
        if (GUILayout.Button("Copy Prefab Instance"))
        {
          PrefabInstantiatorLogic.DuplicatePrefabs(sourceObject, destinationObject);
        }

        EditorGUILayout.Space(10);

        // Transformをコピー
        if (GUILayout.Button("Copy Transform"))
        {
          TransformCopier.ExecuteCopy(sourceObject, destinationObject);
        }

        EditorGUILayout.Space(10);

        // Materialをコピー
        if (GUILayout.Button("Copy Material"))
        {
          MaterialCopier.ExecuteCopy(sourceObject, destinationObject);
        }

        EditorGUILayout.Space(10);

        // BlendShapeをコピー
        if (GUILayout.Button("Copy BlendShape"))
        {
          BlendShapeCopier.ExecuteCopy(sourceObject, destinationObject);
        }

        EditorGUILayout.Space(10);

        // Active状態を同期
        if (GUILayout.Button("Sync ActiveState"))
        {
          SyncActiveState.Execute(sourceObject, destinationObject);
        }

        EditorGUILayout.Space(10);

        // Componentをコピー
        EditorGUILayout.LabelField("Copy Component", EditorStyles.boldLabel);
        copyVrcComponents = EditorGUILayout.Toggle(new GUIContent("VRC", "VRChat SDK関連のコンポーネントをコピーします。"), copyVrcComponents);
        copyMaComponents = EditorGUILayout.Toggle(new GUIContent("ModularAvatar", "Modular Avatar関連のコンポーネントをコピーします。"), copyMaComponents);
        copyAaoComponents = EditorGUILayout.Toggle(new GUIContent("AAO TraceAndOptimize", "TraceAndOptimizeコンポーネントをコピーします。"), copyAaoComponents);
        if (GUILayout.Button("Copy Component"))
        {
          ComponentCopier.ExecuteCopy(sourceObject, destinationObject, copyVrcComponents, copyMaComponents, copyAaoComponents);
        }
        EditorGUILayout.EndVertical();
      }
      EditorGUI.EndDisabledGroup();

      EditorGUILayout.Space(10);
      // GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(20));

      // Only destinationObject
      EditorGUILayout.BeginVertical(GUI.skin.box);
      GUILayout.Label("ユーティリティ操作", EditorStyles.boldLabel);
      EditorGUILayout.Space(5);

      EditorGUI.BeginDisabledGroup(!(destinationObject != null));
      {
        if (GUILayout.Button("Revert All 'To Object'"))
        {
          ObjectReverter.RevertAll(destinationObject);
        }
      }
      EditorGUI.EndDisabledGroup();
      EditorGUILayout.EndVertical();
    }
  }
}
#endif