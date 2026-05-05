using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DndGame.Core;

namespace DndGame.Scenes;

/// <summary>
/// 主菜单场景。
/// 游戏启动时显示的首个场景，负责渲染游戏标题和背景。
/// 使用 FontStashSharp 渲染中文文本，并带有基础的场景生命周期管理。
/// </summary>
public class MainMenuScene : Scene
{
    /// <summary>
    /// 背景纹理。用于填充整个窗口的背景色块。
    /// </summary>
    private Texture2D? _background;

    /// <summary>
    /// 字体系统实例。管理从 TTF 字体文件加载的字形数据。
    /// </summary>
    private FontSystem? _fontSystem;

    /// <summary>
    /// 动态精灵字体。从 FontSystem 中获取指定大小的字体对象，用于文本绘制。
    /// </summary>
    private DynamicSpriteFont? _font;

    /// <summary>
    /// 背景颜色。深蓝灰色调，营造酒馆夜晚的沉浸氛围。
    /// </summary>
    private readonly Color _backgroundColor = new(20, 30, 50);

    /// <summary>
    /// 初始化场景。在场景被设置为活动状态后调用一次。
    /// 负责创建背景纹理、加载中文字体文件并初始化字体系统。
    /// </summary>
    public override void Initialize()
    {
        // 创建一个 1x1 的纯白纹理作为背景画布，后续通过 Draw 方法填充指定颜色
        _background = new Texture2D(Game.GraphicsDevice, 1, 1);
        _background.SetData(new[] { Color.White });

        // 初始化 FontStashSharp 字体系统
        _fontSystem = new FontSystem();

        // 构造 NotoSansCJKsc 字体文件的完整路径
        // 该字体支持简体中文（SC = Simplified Chinese），是项目的中文显示字体
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "NotoSansCJKsc-Regular.ttf");

        // 检查字体文件是否存在：存在则加载，不存在则跳过
        // 这是防御性编程——字体文件缺失时游戏仍可运行（虽然文字无法渲染）
        if (File.Exists(fontPath))
        {
            // 以字节形式读取整个 TTF 文件并注册到字体系统中
            _fontSystem.AddFont(File.ReadAllBytes(fontPath));
        }

        // 从字体系统中获取字号 32 的动态字体对象
        // 若之前未加载任何字体文件，此调用不会抛出异常但后续 DrawText 无效果
        _font = _fontSystem.GetFont(32);
    }

    /// <summary>
    /// 每帧绘制场景。在 Update 之后由 GameRoot 调用。
    /// 负责绘制背景色块和居中的游戏标题文本。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态快照，包含经过时间和帧间隔。</param>
    public override void Draw(GameTime gameTime)
    {
        var sb = Game.SpriteBatch;

        // 开始精灵批处理，使用 PointClamp 采样器以保持像素风格的清晰边缘
        sb.Begin(samplerState: SamplerState.PointClamp);

        // 绘制全屏背景色块
        // 使用之前创建的 1x1 纹理，拉伸填充至设计分辨率（1280×720）
        sb.Draw(_background,
            new Rectangle(0, 0, GameRoot.DESIGN_WIDTH, GameRoot.DESIGN_HEIGHT),
            _backgroundColor);

        // 检查字体对象是否可用，避免在字体加载失败时发生空引用异常
        if (_font != null)
        {
            // 游戏标题文本（简体中文），显示在主菜单正中央
            var text = "你好酒馆";

            // 使用 MeasureString 测量文本渲染后的精确像素尺寸
            // 这是文本居中的关键步骤——必须先知道文本有多宽多高
            var size = _font.MeasureString(text);

            // 计算文本居中位置：(容器尺寸 - 文本尺寸) / 2
            // 分别在 X 轴和 Y 轴上居中，使文本位于屏幕正中央
            var position = new Vector2(
                (GameRoot.DESIGN_WIDTH - size.X) / 2,
                (GameRoot.DESIGN_HEIGHT - size.Y) / 2);

            // 在计算出的居中位置绘制白色标题文本
            _font.DrawText(sb, text, position, Color.White);
        }

        // 结束精灵批处理，提交所有绘制命令
        sb.End();

        // 调用基类的 Draw 方法，确保场景组件的正常渲染
        base.Draw(gameTime);
    }

    /// <summary>
    /// 结束场景。在场景被替换时调用，负责释放本场景分配的所有资源。
    /// </summary>
    public override void End()
    {
        // 释放背景纹理（IDisposable）
        _background?.Dispose();

        // 释放字体系统资源（IDisposable），包括所有加载的字形缓存
        _fontSystem?.Dispose();

        // 调用基类的 End 方法，清理实体和场景组件列表
        base.End();
    }
}
