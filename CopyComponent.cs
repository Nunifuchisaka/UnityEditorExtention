using UnityEngine;
using UnityEditor;

/// <summary>
/// 指定した条件にマッチするコンポーネントを、あるオブジェクトから別のオブジェクトへコピーするエディタ拡張
/// VRChatアバターのセットアップコピーなどを想定
/// </summary>
public class NunifuchisakaCopyComponent : EditorWindow
{
  private GameObject sourceObject;
  private GameObject destinationObject;

  // コピー条件のフラグ
  private bool copyTransform = true; // Transformの値をコピーするか
  private bool copyVrcComponents = true;
  private bool copyMaComponents = true;
  private bool copyAaoComponents = true;

  /// <summary>
  /// メニューからウィンドウを表示
  /// </summary>
  [MenuItem("Tools/Nunifuchisaka/CopyComponent")]
  public static void ShowWindow()
  {
    // ウィンドウを取得して表示
    GetWindow<NunifuchisakaCopyComponent>("Copy Component");
  }

  /// <summary>
  /// ウィンドウのGUIを描画
  /// </summary>
  private void OnGUI()
  {
    GUILayout.Label("Component Copier", EditorStyles.boldLabel);
    // EditorGUILayout.HelpBox("メモ", MessageType.Info);

    // オブジェクト指定フィールド
    sourceObject = (GameObject)EditorGUILayout.ObjectField("Source Object", sourceObject, typeof(GameObject), true);
    destinationObject = (GameObject)EditorGUILayout.ObjectField("Destination Object", destinationObject, typeof(GameObject), true);

    GUILayout.Space(10);

    // コピー条件のチェックボックス
    EditorGUILayout.LabelField("Copy Conditions", EditorStyles.boldLabel);
    copyTransform = EditorGUILayout.Toggle(new GUIContent("Copy Transform Values", "位置、回転、スケールをコピーします。"), copyTransform);
    copyVrcComponents = EditorGUILayout.Toggle(new GUIContent("Copy 'VRC' components", "VRChat SDK関連のコンポーネントをコピーします。"), copyVrcComponents);
    copyMaComponents = EditorGUILayout.Toggle(new GUIContent("Copy 'MA' components", "Modular Avatar関連のコンポーネントをコピーします。"), copyMaComponents);
    copyAaoComponents = EditorGUILayout.Toggle(new GUIContent("Copy 'AAO' components", "Avatar Assemblerなど、'AAO'で始まるコンポーネントをコピーします。"), copyAaoComponents);

    GUILayout.Space(20);

    // 実行ボタン
    if (GUILayout.Button("Copy Components"))
    {
      CopyComponents();
    }
  }

  /// <summary>
  /// コンポーネントのコピー処理を実行
  /// </summary>
  private void CopyComponents()
  {
    // オブジェクトが設定されているか確認
    if (sourceObject == null || destinationObject == null)
    {
      Debug.LogError("[CopyComponent] Source Object と Destination Object の両方を設定してください。");
      return;
    }

    // コピー条件が1つもチェックされていない場合は警告
    if (!copyTransform && !copyVrcComponents && !copyMaComponents && !copyAaoComponents)
    {
      Debug.LogWarning("[CopyComponent] コピーする条件を少なくとも1つチェックしてください。");
      return;
    }

    try
    {
      int copiedCount = 0;
      // Undo処理のために、コピー先のオブジェクトと既存コンポーネントを記録
      Undo.RecordObject(destinationObject, "Copy Components");
      foreach(var component in destinationObject.GetComponents<Component>())
      {
        Undo.RecordObject(component, "Copy Components");
      }

      Debug.Log($"[CopyComponent] ----- Start copying from '{sourceObject.name}' to '{destinationObject.name}' -----");

      // 1. Transformコンポーネントの値コピー
      if (copyTransform)
      {
        if (UnityEditorInternal.ComponentUtility.CopyComponent(sourceObject.transform))
        {
          if (UnityEditorInternal.ComponentUtility.PasteComponentValues(destinationObject.transform))
          {
            Debug.Log($"[CopyComponent] Copied values of 'Transform'.");
            copiedCount++;
          }
        }
      }

      // 2. 他のコンポーネントのコピー
      Component[] sourceComponents = sourceObject.GetComponents<Component>();

      foreach (var sourceComponent in sourceComponents)
      {
        // Transformは既に処理済みなのでスキップ
        if (sourceComponent is Transform)
        {
          continue;
        }

        System.Type componentType = sourceComponent.GetType();
        string componentName = componentType.Name;

        // コピー対象か判定
        bool shouldCopy =
            (copyVrcComponents && componentName.StartsWith("VRC")) ||
            (copyMaComponents && componentName.StartsWith("ModularAvatar")) ||
            (copyAaoComponents && componentName.StartsWith("TraceAndOptimize"));

        Debug.Log($"[CopyComponent] '{componentName}'.");

        if (shouldCopy)
        {
          // コピー先のオブジェクトに同じコンポーネントがあるか探す
          Component destComponent = destinationObject.GetComponent(componentType);

          // なければ新しく追加する (Undo対応)
          if (destComponent == null)
          {
            destComponent = Undo.AddComponent(destinationObject, componentType);
            Debug.Log($"[CopyComponent] Added new component '{componentName}' to '{destinationObject.name}'.");
          }

          // コンポーネントの値をコピー＆ペースト
          if (UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent))
          {
            if (UnityEditorInternal.ComponentUtility.PasteComponentValues(destComponent))
            {
              Debug.Log($"[CopyComponent] Pasted values for component '{componentName}'.");
              copiedCount++;
            }
            else
            {
              Debug.LogWarning($"[CopyComponent] Failed to paste values for '{componentName}'.");
            }
          }
          else
          {
            Debug.LogWarning($"[CopyComponent] Failed to copy component '{componentName}'.");
          }
        }
      }

      if (copiedCount > 0)
      {
        Debug.Log($"[CopyComponent] ----- Finished: Processed {copiedCount} component(s) successfully. -----");
      }
      else
      {
        Debug.LogWarning("[CopyComponent] No matching components found to copy.");
      }
    }
    catch (System.Exception e)
    {
      Debug.LogError($"[CopyComponent] An error occurred: {e.Message}");
    }
  }
}