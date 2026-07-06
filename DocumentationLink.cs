#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Nunifuchisaka
{
  internal static class DocumentationLink
  {
    private const string Url = "https://github.com/Nunifuchisaka/UnityEditorExtension#readme";

    public static void Draw()
    {
      if (EditorGUILayout.LinkButton("ドキュメントを開く"))
      {
        Application.OpenURL(Url);
      }
    }
  }
}
#endif
