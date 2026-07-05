# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 概要

VRChatアバター用のUnityエディタ拡張集。VCCプロジェクト（Avatar3）の `Assets/Nunifuchisaka/Editor/` 配下にあり、このディレクトリ単体がgitリポジトリになっている。ビルド・テストコマンドはない — Unityエディタが自動でコンパイルし、エラーはUnityのConsoleに出る。

## アーキテクチャ

- 各 `.cs` ファイルは独立したツール。基本パターン：`namespace Nunifuchisaka` 内の `EditorWindow` 派生クラスで、`[MenuItem("Tools/Nunifuchisaka/〇〇...", false, priority)]` でメニュー登録する。ファイル全体を `#if UNITY_EDITOR` で囲む。
- 各ツールはUI（`OnGUI`）とロジック（`public static` な `Execute` / `ExecuteCopy` メソッド）を分離している。これは `AvatarCopier` が各ツールのロジックを直接呼び出すため。UIを持たないロジックだけのクラスは `〇〇Logic`（例：`PrefabInstantiatorLogic`）。
- `AvatarCopier` がオーケストレーター。「Copy All」で以下を順に実行する：
  `PrefabInstantiatorLogic.DuplicatePrefabs` → `TransformCopier` → `MaterialCopier` → `BlendShapeCopier` → `SyncActiveState` → `ComponentCopier`
- VRChat SDK / Modular Avatar / AAO / FloorAdjuster への参照はアセンブリ参照を持たず、型名・名前空間の文字列マッチで判定する。判定ロジックは `ComponentFilter` に一元化されており、新しい判定もここに追加する。これらのパッケージに直接依存するコードを書かないこと。
- `ComponentCopier` は3パス構成：①階層の複製 → ②コンポーネントのコピー（`GetSortPriority` で PhysBoneCollider → PhysBone の順序を保証）→ ③リフレクションでコピー元階層内への参照をコピー先の対応オブジェクトへリマップ。
- シーンを変更する操作は必ずUndo対応する：`Undo.SetCurrentGroupName` + `Undo.GetCurrentGroup` で開始し、`Undo.RegisterCreatedObjectUndo` / `Undo.AddComponent` / `Undo.RecordObject` を使い、最後に `Undo.CollapseUndoOperations` でまとめる。

## テスト

- 自動テスト基盤はない。動作テストは、このディレクトリ直下の `Test.unity` シーンで必ず実行する（作業中のシーンを汚さないため）。ユーザーにUnityエディタで `Test.unity` を開いてもらってから実行を依頼する。
- テスト方法：一時的なエディタスクリプト（`TempSelfTest.cs` など）を作成してメニューから実行してもらい、結果を `[SelfTest] PASS/FAIL` 形式で `Debug.Log` に出す。結果は `C:\Users\nunif\AppData\Local\Unity\Editor\Editor.log` から読み取れる。検証が終わったら一時スクリプトは削除する。
- VRChat SDK等の型はアセンブリ参照せず、テスト内でもリフレクション（型名検索）で取得する。
- コンパイル検証だけならUnityを開かずに可能：Unity同梱のRoslyn（`Editor/Data/DotNetSdkRoslyn/csc.dll` を `NetCoreRuntime/dotnet.exe` で実行）に `Managed/UnityEngine/*.dll` と `NetStandard/ref/2.1.0/netstandard.dll` を参照させ、`-define:UNITY_EDITOR` を付けて全 `.cs` をコンパイルする。Unityバージョンは `ProjectSettings/ProjectVersion.txt` を参照。

## 規約

- インデントはスペース2つ。
- UIラベル・ログ・コメントは日本語と英語が混在（既存ファイルに合わせる）。ログは `[クラス名]` プレフィックス付きで `Debug.Log` / `LogWarning` / `LogError` を使う。
- `.meta` ファイルは `.gitignore` に含まれ、すべてgit管理外。新規ファイル作成時はUnityが `.meta` を生成する（手動で作らない）。`.cs` をリネームする際はGUID維持のため `.meta` も同名にリネームする。
