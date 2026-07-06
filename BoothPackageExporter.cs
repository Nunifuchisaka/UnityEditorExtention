#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nunifuchisaka
{
  /// <summary>
  /// パッケージ資産を.unitypackage化し、README・LICENSE・利用規約とともにBOOTH配布用zipにまとめる（開発者専用ツール）。
  /// </summary>
  public class BoothPackageExporter : EditorWindow
  {
    private static readonly string[] ExcludedNames = { "Website", "source.json", "Test.unity", "CLAUDE.md" };
    private static readonly string[] BundledDocs = { "README.md", "LICENSE", "Terms.txt" };

    private string _repoRootAssetPath;
    private string _packageName;
    private string _packageVersion;
    private string _lastResultMessage = "";

    [MenuItem("Tools/Nunifuchisaka/Booth Package Exporter...", false, 200)]
    private static void ShowWindow()
    {
      BoothPackageExporter window = GetWindow<BoothPackageExporter>(true, "Booth Package Exporter", true);
      window.minSize = new Vector2(480, 220);
    }

    private void OnEnable()
    {
      try
      {
        string repoRootAbsolutePath = GetRepoRootAbsolutePath();
        _repoRootAssetPath = ToAssetPath(repoRootAbsolutePath);
        PackageJson packageJson = ReadPackageJson(repoRootAbsolutePath);
        _packageName = packageJson.name;
        _packageVersion = packageJson.version;
      }
      catch (Exception e)
      {
        Debug.LogError($"[BoothPackageExporter] Failed to read package info. Error: {e}");
      }
    }

    private void OnGUI()
    {
      DocumentationLink.Draw();
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Package Root:", _repoRootAssetPath ?? "-");
      EditorGUILayout.LabelField("Name:", _packageName ?? "-");
      EditorGUILayout.LabelField("Version:", _packageVersion ?? "-");
      EditorGUILayout.HelpBox("package.json / README.md / LICENSE / Terms.txt を .unitypackage 化し、BOOTH配布用のzip（dist~/ 配下）を作成します。", MessageType.Info);
      EditorGUILayout.Space();

      if (GUILayout.Button("Export BOOTH Package", GUILayout.Height(30)))
      {
        try
        {
          string zipPath = Export();
          _lastResultMessage = $"Exported: {zipPath}";
          Debug.Log($"[BoothPackageExporter] {_lastResultMessage}");
          EditorUtility.DisplayDialog("Booth Package Exporter", _lastResultMessage, "OK");
        }
        catch (Exception e)
        {
          Debug.LogError($"[BoothPackageExporter] Export failed. Error: {e}");
          EditorUtility.DisplayDialog("Error", $"エクスポートに失敗しました。詳細はConsoleを確認してください。\n\n{e.Message}", "OK");
        }
      }

      if (!string.IsNullOrEmpty(_lastResultMessage))
      {
        EditorGUILayout.Space();
        EditorGUILayout.SelectableLabel(_lastResultMessage, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
      }
    }

    public static string Export()
    {
      string repoRootAbsolutePath = GetRepoRootAbsolutePath();
      string repoRootAssetPath = ToAssetPath(repoRootAbsolutePath);
      PackageJson packageJson = ReadPackageJson(repoRootAbsolutePath);
      string thisFileName = Path.GetFileName(GetThisFilePath());

      List<string> assetPaths = new List<string>();
      foreach (string entry in Directory.GetFileSystemEntries(repoRootAbsolutePath))
      {
        string name = Path.GetFileName(entry);
        if (name.StartsWith(".")) { continue; }
        if (name.EndsWith("~")) { continue; }
        if (name.EndsWith(".meta")) { continue; }
        if (name == thisFileName) { continue; }
        if (Array.IndexOf(ExcludedNames, name) >= 0) { continue; }
        assetPaths.Add($"{repoRootAssetPath}/{name}");
      }

      string distDirAbsolutePath = Path.Combine(repoRootAbsolutePath, "dist~");
      Directory.CreateDirectory(distDirAbsolutePath);

      string fileBaseName = packageJson.name.StartsWith("com.") ? packageJson.name.Substring("com.".Length) : packageJson.name;
      string unityPackagePath = Path.Combine(distDirAbsolutePath, $"{fileBaseName}-{packageJson.version}.unitypackage");
      AssetDatabase.ExportPackage(assetPaths.ToArray(), unityPackagePath, ExportPackageOptions.Recurse);

      foreach (string docName in BundledDocs)
      {
        if (!File.Exists(Path.Combine(repoRootAbsolutePath, docName)))
        {
          throw new FileNotFoundException($"BOOTH配布に必要なファイルが見つかりません: {docName}");
        }
      }

      string zipPath = Path.Combine(distDirAbsolutePath, $"{fileBaseName}-{packageJson.version}-booth.zip");
      if (File.Exists(zipPath)) { File.Delete(zipPath); }

      using (FileStream zipStream = new FileStream(zipPath, FileMode.Create))
      using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
      {
        AddFileToZip(archive, unityPackagePath, Path.GetFileName(unityPackagePath));
        foreach (string docName in BundledDocs)
        {
          AddFileToZip(archive, Path.Combine(repoRootAbsolutePath, docName), docName);
        }
      }

      return zipPath;
    }

    private static void AddFileToZip(ZipArchive archive, string sourcePath, string entryName)
    {
      ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
      using (Stream entryStream = entry.Open())
      using (FileStream fileStream = File.OpenRead(sourcePath))
      {
        fileStream.CopyTo(entryStream);
      }
    }

    private static string GetRepoRootAbsolutePath()
    {
      return Path.GetDirectoryName(GetThisFilePath()).Replace('\\', '/');
    }

    private static string ToAssetPath(string absolutePath)
    {
      string dataPath = Application.dataPath;
      if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
      {
        throw new InvalidOperationException($"リポジトリが Assets フォルダ外にあります: {absolutePath}");
      }
      string relative = absolutePath.Substring(dataPath.Length).TrimStart('/');
      return string.IsNullOrEmpty(relative) ? "Assets" : $"Assets/{relative}";
    }

    private static string GetThisFilePath([CallerFilePath] string path = null)
    {
      return path.Replace('\\', '/');
    }

    [Serializable]
    private class PackageJson
    {
      public string name;
      public string version;
    }

    private static PackageJson ReadPackageJson(string repoRootAbsolutePath)
    {
      string json = File.ReadAllText(Path.Combine(repoRootAbsolutePath, "package.json"));
      return JsonUtility.FromJson<PackageJson>(json);
    }
  }
}
#endif
