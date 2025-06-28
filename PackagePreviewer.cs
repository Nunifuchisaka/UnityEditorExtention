using UnityEngine;
using UnityEditor;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Nunifuchisaka
{
  public class PackagePreviewer : EditorWindow
  {
    private class AssetInfo { public string Path { get; set; } public string Guid { get; set; } public string Status { get; set; } public string ExistingPath { get; set; } public Texture Icon { get; set; } }
    private List<AssetInfo> _assetInfos = new List<AssetInfo>();
    private Vector2 _scrollPosition;
    private string _selectedPackagePath;
    private bool _isParsing = false;

    [MenuItem("Tools/Nunifuchisaka/Import Package with Preview...")]
    private static void ShowWindowMenu()
    {
      string path = EditorUtility.OpenFilePanel("Select Unity Package", "", "unitypackage");
      if (string.IsNullOrEmpty(path)) { return; }
      PackagePreviewer window = GetWindow<PackagePreviewer>(true, "Package Previewer", true);
      window.minSize = new Vector2(500, 300);
      window.StartParsing(path);
    }

    private void StartParsing(string packagePath)
    {
      _selectedPackagePath = packagePath;
      _isParsing = true;
      _assetInfos.Clear();
      Repaint();
      EditorApplication.delayCall += () =>
      {
        try { ParsePackage(_selectedPackagePath); }
        catch (Exception e)
        {
          Debug.LogError($"[PackagePreviewer] Failed to parse package. Error: {e.ToString()}");
          EditorUtility.DisplayDialog("Error", "An unexpected error occurred. See the console for details.", "OK");
        }
        finally { _isParsing = false; Repaint(); }
      };
    }

    private void OnGUI()
    {
      EditorGUILayout.LabelField("Package:", EditorStyles.boldLabel);
      EditorGUILayout.SelectableLabel(_selectedPackagePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
      EditorGUILayout.Space();
      if (_isParsing) { EditorGUILayout.LabelField("Parsing package contents, please wait..."); return; }
      EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
      EditorGUILayout.LabelField("Package Contents", EditorStyles.boldLabel);
      GUILayout.FlexibleSpace();
      EditorGUILayout.LabelField("Status", GUILayout.Width(80));
      EditorGUILayout.EndHorizontal();
      _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, "box");
      if (_assetInfos.Count == 0) { EditorGUILayout.HelpBox("No assets found in the package.", MessageType.Info); }
      foreach (var info in _assetInfos)
      {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Box(info.Icon, GUIStyle.none, GUILayout.Width(20), GUILayout.Height(20));
        EditorGUILayout.LabelField(new GUIContent(info.Path));
        Color originalColor = GUI.color;
        GUI.color = info.Status == "Overwrite" ? Color.yellow : Color.green;
        EditorGUILayout.LabelField(info.Status, GUILayout.Width(80));
        GUI.color = originalColor;
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(info.ExistingPath))
        {
          EditorGUI.indentLevel++;
          EditorGUILayout.LabelField(new GUIContent($"└ Overwrites: {info.ExistingPath}", "This is the path of the existing asset in your project that will be overwritten."), EditorStyles.miniLabel);
          EditorGUI.indentLevel--;
        }
      }
      EditorGUILayout.EndScrollView();
      EditorGUILayout.Space();
      if (GUILayout.Button("Open Standard Import Dialog", GUILayout.Height(30)))
      {
        if (!string.IsNullOrEmpty(_selectedPackagePath)) { AssetDatabase.ImportPackage(_selectedPackagePath, true); this.Close(); }
      }
    }

    private void ParsePackage(string path)
    {
      _assetInfos.Clear();
      var tempAssetInfos = new Dictionary<string, AssetInfo>();

      using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
      using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
      using (MemoryStream memoryStream = new MemoryStream())
      {
        gzipStream.CopyTo(memoryStream);
        byte[] buffer = memoryStream.ToArray();
        int offset = 0;

        while (offset + 512 <= buffer.Length)
        {
          byte[] header = new byte[512];
          Array.Copy(buffer, offset, header, 0, 512);
          if (header.All(b => b == 0)) { break; }

          string name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
          string sizeStr = Encoding.UTF8.GetString(header, 124, 12).TrimEnd('\0').Trim();
          long size = 0;
          if (!string.IsNullOrEmpty(sizeStr))
          {
            try { size = Convert.ToInt64(sizeStr, 8); } catch { /* ignore parse error */ }
          }

          bool isTargetFile = false;
          if (!string.IsNullOrEmpty(name) && !name.Contains("PaxHeader") && !name.Contains("/._"))
          {
            string normalizedName = name.StartsWith("./") ? name.Substring(2) : name;
            string[] parts = normalizedName.Split('/');

            if (parts.Length == 2 && parts[1] == "pathname")
            {
              isTargetFile = true;
              string guid = parts[0];
              string assetPath = Encoding.UTF8.GetString(buffer, offset + 512, (int)size).Trim();
              string existingPath = AssetDatabase.GUIDToAssetPath(guid);

              var info = new AssetInfo
              {
                Guid = guid,
                Path = assetPath,
                Status = string.IsNullOrEmpty(existingPath) ? "New" : "Overwrite",
                ExistingPath = existingPath,
                Icon = AssetDatabase.GetCachedIcon(assetPath) ?? EditorGUIUtility.IconContent("DefaultAsset Icon").image
              };
              tempAssetInfos[guid] = info;
            }
          }

          offset += 512;
          offset += (int)size;
          if (offset % 512 != 0) { offset += 512 - (offset % 512); }
        }
      }

      _assetInfos.AddRange(tempAssetInfos.Values);
      _assetInfos.Sort((a, b) => a.Path.CompareTo(b.Path));
    }
  }
}