using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using FontStashSharp;

namespace DndGame.UI;

/// <summary>
/// Myra UI 管理器，负责初始化 Myra 框架、管理 Desktop 和字体加载。
/// </summary>
public class UIManager
{
    private Desktop? _desktop;
    private FontSystem? _fontSystem;

    /// <summary>
    /// Myra Desktop 实例，所有 UI 组件的根容器。
    /// </summary>
    public Desktop Desktop => _desktop ?? throw new InvalidOperationException("UIManager 尚未初始化");

    /// <summary>
    /// 初始化 Myra 框架并创建 Desktop。
    /// </summary>
    public void Initialize(Game game)
    {
        MyraEnvironment.Game = game;
        _desktop = new Desktop();
    }

    /// <summary>
    /// 加载 CJK 字体文件并返回 FontSystem 实例。
    /// </summary>
    public FontSystem LoadFont(string fontPath)
    {
        _fontSystem = new FontSystem();
        _fontSystem.AddFont(File.ReadAllBytes(fontPath));
        return _fontSystem;
    }

    /// <summary>
    /// 获取指定大小的字体。需先调用 LoadFont。
    /// </summary>
    public DynamicSpriteFont GetFont(int size)
    {
        if (_fontSystem == null)
            throw new InvalidOperationException("字体尚未加载，请先调用 LoadFont。");
        return _fontSystem.GetFont(size);
    }

    /// <summary>
    /// 渲染 UI 层。在 GameRoot.Draw() 中场景绘制之后调用。
    /// </summary>
    public void Render()
    {
        _desktop?.Render();
    }
}
