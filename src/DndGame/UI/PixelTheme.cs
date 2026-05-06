using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace DndGame.UI;

/// <summary>
/// FF6 风格像素主题配置。
/// 修改全局 Stylesheet.Current 实现复古 RPG 视觉效果。
/// 所有颜色常量为 public static readonly，供其他系统引用（如战斗伤害/治疗颜色）。
/// </summary>
public static class PixelTheme
{
    // ── 窗口颜色 ──
    /// <summary>窗口背景：深蓝半透明</summary>
    public static readonly Color WindowBg = new(10, 10, 30, 217);

    /// <summary>窗口外边框</summary>
    public static readonly Color WindowBorderOuter = new(26, 26, 46);

    /// <summary>窗口内边框</summary>
    public static readonly Color WindowBorderInner = new(74, 74, 106);

    // ── 按钮颜色 ──
    /// <summary>按钮默认背景</summary>
    public static readonly Color ButtonBg = new(42, 42, 78);

    /// <summary>按钮默认边框</summary>
    public static readonly Color ButtonBorder = new(74, 74, 106);

    /// <summary>按钮默认文字</summary>
    public static readonly Color ButtonText = new(240, 230, 210);

    /// <summary>按钮悬停背景</summary>
    public static readonly Color ButtonHoverBg = new(58, 58, 94);

    /// <summary>按钮悬停边框</summary>
    public static readonly Color ButtonHoverBorder = new(106, 106, 138);

    /// <summary>按钮按下背景</summary>
    public static readonly Color ButtonPressedBg = new(26, 26, 46);

    /// <summary>按钮按下边框</summary>
    public static readonly Color ButtonPressedBorder = new(42, 42, 78);

    /// <summary>按钮按下文字</summary>
    public static readonly Color ButtonPressedText = new(208, 208, 224);

    // ── 文字颜色 ──
    /// <summary>主文本：暖白色</summary>
    public static readonly Color PrimaryText = new(240, 230, 210);

    // ── 功能颜色 ──
    /// <summary>金币/贵重物品色</summary>
    public static readonly Color Gold = new(255, 215, 0);

    /// <summary>伤害数值色</summary>
    public static readonly Color DamageRed = new(255, 68, 68);

    /// <summary>治疗数值色</summary>
    public static readonly Color HealingGreen = new(68, 255, 68);

    /// <summary>
    /// 将 FF6 风格像素主题应用到全局 Stylesheet 和指定 Desktop。
    /// 必须在创建任何 UI 控件之前调用。
    /// </summary>
    /// <param name="desktop">Myra Desktop 根容器，用于设置桌面背景画刷。</param>
    public static void Apply(Desktop desktop)
    {
        var stylesheet = Stylesheet.Current;

        ConfigureDesktop(stylesheet, desktop);
        ConfigureWindowStyle(stylesheet);
        ConfigureButtonStyle(stylesheet);
        ConfigureLabelStyle(stylesheet);
    }

    private static void ConfigureDesktop(Stylesheet stylesheet, Desktop desktop)
    {
        if (stylesheet.DesktopStyle != null)
        {
            stylesheet.DesktopStyle.Background = new SolidBrush(WindowBg);
        }
        desktop.Background = new SolidBrush(WindowBg);
    }

    private static void ConfigureWindowStyle(Stylesheet stylesheet)
    {
        var windowStyle = stylesheet.WindowStyle;

        windowStyle.Background = new SolidBrush(WindowBg);
        windowStyle.Border = new SolidBrush(WindowBorderOuter);
        windowStyle.BorderThickness = new Thickness(2);

        if (windowStyle.TitleStyle != null)
        {
            windowStyle.TitleStyle.TextColor = PrimaryText;
        }
    }

    private static void ConfigureButtonStyle(Stylesheet stylesheet)
    {
        var buttonStyle = stylesheet.ButtonStyle;

        buttonStyle.Background = new SolidBrush(ButtonBg);
        buttonStyle.OverBackground = new SolidBrush(ButtonHoverBg);
        buttonStyle.PressedBackground = new SolidBrush(ButtonPressedBg);

        buttonStyle.Border = new SolidBrush(ButtonBorder);
        buttonStyle.OverBorder = new SolidBrush(ButtonHoverBorder);
        buttonStyle.FocusedBorder = new SolidBrush(ButtonPressedBorder);

        if (buttonStyle.LabelStyle != null)
        {
            buttonStyle.LabelStyle.TextColor = ButtonText;
            buttonStyle.LabelStyle.OverTextColor = Color.White;
            buttonStyle.LabelStyle.PressedTextColor = ButtonPressedText;
        }
    }

    private static void ConfigureLabelStyle(Stylesheet stylesheet)
    {
        stylesheet.LabelStyle.TextColor = PrimaryText;
    }
}
