#if UNITY_EDITOR
using UnityEngine;

namespace Nunifuchisaka
{
  /// <summary>
  /// VRChat SDK / Modular Avatar / AAO / FloorAdjuster のコンポーネント判定を一元化する。
  /// これらのパッケージへのアセンブリ参照を持たないため、型名・名前空間の文字列マッチで判定する。
  /// </summary>
  public static class ComponentFilter
  {
    public static bool IsVrc(Component component)
    {
      var type = component.GetType();
      return type.Name.StartsWith("VRC") || (type.Namespace ?? "").StartsWith("VRC");
    }

    public static bool IsModularAvatar(Component component)
    {
      return component.GetType().Name.StartsWith("ModularAvatar");
    }

    public static bool IsAao(Component component)
    {
      return component.GetType().Name == "TraceAndOptimize";
    }

    public static bool IsFloorAdjuster(Component component)
    {
      return component.GetType().Name == "FloorAdjuster";
    }

    /// <summary>
    /// 有効化された条件のいずれかにコンポーネントが該当するかを返す。
    /// </summary>
    public static bool Matches(Component component, bool vrc, bool modularAvatar, bool aao, bool floorAdjuster)
    {
      return (vrc && IsVrc(component)) ||
             (modularAvatar && IsModularAvatar(component)) ||
             (aao && IsAao(component)) ||
             (floorAdjuster && IsFloorAdjuster(component));
    }
  }
}
#endif
