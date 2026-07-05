# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 概要

VRChatアバター用のUnityエディタ拡張集。VCCプロジェクト（Avatar3）の `Assets/Nunifuchisaka/Editor/` 配下にあり、このディレクトリ単体がgitリポジトリになっている。ビルド・テストコマンドはない — Unityエディタが自動でコンパイルし、エラーはUnityのConsoleに出る。ツール一覧とユーザー向けの使い方は `README.md` を参照。

## アーキテクチャ

- 各 `.cs` ファイルは独立したツール。基本パターン：`namespace Nunifuchisaka` 内の `EditorWindow` 派生クラスで、`[MenuItem("Tools/Nunifuchisaka/〇〇...", false, priority)]` でメニュー登録する。ファイル全体を `#if UNITY_EDITOR` で囲む。
- 各ツールはUI（`OnGUI`）とロジック（`public static` な `Execute` / `ExecuteCopy` メソッド）を分離している。これは `AvatarCopier` が各ツールのロジックを直接呼び出すため。UIを持たないロジックだけのクラスは `〇〇Logic`（例：`PrefabInstantiatorLogic`）。
- `AvatarCopier` がオーケストレーター。「Copy All」で以下を順に実行する：
  `PrefabInstantiatorLogic.DuplicatePrefabs` → `TransformCopier` → `MaterialCopier` → `BlendShapeCopier` → `SyncActiveState` → `ComponentCopier`
- コピー元とコピー先のオブジェクト対応付けは、全ツール共通で**同名の子オブジェクト**（相対パス）で行う。同じ階層に同名の兄弟がいると正しく対応しない。
- VRChat SDK / Modular Avatar / AAO / FloorAdjuster への参照はアセンブリ参照を持たず、型名・名前空間の文字列マッチで判定する。判定ロジックは `ComponentFilter` に一元化されており、新しい判定もここに追加する。これらのパッケージに直接依存するコードを書かないこと。
- `ComponentCopier` は3パス構成：①階層の複製 → ②コンポーネントのコピー（`GetSortPriority` で PhysBoneCollider → PhysBone の順序を保証。同型複数は出現順のn番目同士で対応付け）→ ③リフレクションでコピー元階層内への参照をコピー先の対応オブジェクトへリマップ。
  - 「All Components」モード（`doCopyAll`）では `ComponentFilter` の判定をバイパスしてすべてコピーする。ただし Transform / Renderer / MeshFilter は `IsAlwaysExcluded` で常に除外（Transform・Material・BlendShapeは専用ツールの担当のため）。
  - Pass 3のリマップはC#フィールドのリフレクションで行うため、有効なのはマネージドコンポーネント（VRC / MA などのMonoBehaviour）のみ。ネイティブ実装の標準コンポーネント（例：`Joint.connectedBody`）のシリアライズフィールドはリフレクションから見えず、リマップされない。
- シーンを変更する操作は必ずUndo対応する：`Undo.SetCurrentGroupName` + `Undo.GetCurrentGroup` で開始し、`Undo.RegisterCreatedObjectUndo` / `Undo.AddComponent` / `Undo.RecordObject` を使い、最後に `Undo.CollapseUndoOperations` でまとめる。リフレクションでフィールドを書き換えた後は `PrefabUtility.RecordPrefabInstancePropertyModifications` + `EditorUtility.SetDirty` で永続化する。

## テスト

- 自動テスト基盤はない。動作テストは、このディレクトリ直下の `Test.unity` シーンで必ず実行する（作業中のシーンを汚さないため）。ユーザーにUnityエディタで `Test.unity` を開いてもらってから実行を依頼する。
- テスト方法：一時的なエディタスクリプト（`TempSelfTest.cs` など）を作成してメニューから実行してもらい、結果を `[SelfTest] PASS/FAIL` 形式で `Debug.Log` に出す。結果は `C:\Users\nunif\AppData\Local\Unity\Editor\Editor.log` から読み取れる（過去セッションのログも残っているのでファイル末尾の実行分を見る）。検証が終わったら一時スクリプトは削除する。
- VRChat SDK等の型はアセンブリ参照せず、テスト内でもリフレクション（型名検索）で取得する。
- コンパイル検証だけならUnityを開かずに可能。Unityバージョンは `ProjectSettings/ProjectVersion.txt` を参照。パスにスペースを含むためレスポンスファイル方式を使う（bash）：

  ```bash
  cd "/c/Users/nunif/Dropbox/3DModel/VCC_Projects/Avatar3/Assets/Nunifuchisaka/Editor"
  U="/c/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Data"
  UW="C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Data"
  RSP="$TMP/check.rsp"
  { echo "-target:library"; echo "-define:UNITY_EDITOR"; echo "-nowarn:0169,0649"
    echo "-out:$TMP/check.dll"
    for f in "$U/Managed/UnityEngine/"*.dll; do echo "-reference:\"$UW/Managed/UnityEngine/$(basename "$f")\""; done
    echo "-reference:\"$UW/NetStandard/ref/2.1.0/netstandard.dll\""
    for f in *.cs; do echo "\"$f\""; done; } > "$RSP"
  "$U/NetCoreRuntime/dotnet.exe" "$U/DotNetSdkRoslyn/csc.dll" -nologo "@$RSP" && echo COMPILE_OK
  ```

## 規約

- インデントはスペース2つ。
- UIラベル・ログ・コメントは日本語と英語が混在（既存ファイルに合わせる）。ログは `[クラス名]` プレフィックス付きで `Debug.Log` / `LogWarning` / `LogError` を使う。
- `.meta` ファイルは `.gitignore` に含まれ、すべてgit管理外。新規ファイル作成時はUnityが `.meta` を生成する（手動で作らない）。`.cs` をリネームする際はGUID維持のため `.meta` も同名にリネームする。
- ツールの機能を追加・変更したら `README.md` のツール一覧・使い方も更新する。
