using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DndGame.Core;

/// <summary>
/// 游戏根类 —— 管理场景、全局系统和主游戏循环。
/// 直接继承 MonoGame 的 Game 类，采用基于场景的架构。
/// </summary>
public class GameRoot : Game
{
    /// <summary>
    /// 游戏全局单例实例引用。
    /// </summary>
    private static GameRoot? _instance;

    /// <summary>
    /// 图形设备管理器，控制窗口大小、垂直同步等图形设置。
    /// </summary>
    private readonly GraphicsDeviceManager _graphics;

    /// <summary>
    /// 全局精灵批处理对象，供所有场景共享渲染。
    /// </summary>
    private SpriteBatch? _spriteBatch;

    /// <summary>
    /// 当前激活的场景，每帧更新和绘制由此场景驱动。
    /// </summary>
    private Scene? _currentScene;

    /// <summary>
    /// 待切换的下一个场景，在下一帧更新循环开始时生效。
    /// </summary>
    private Scene? _nextScene;

    /// <summary>
    /// 全局单例访问器。如果游戏尚未初始化则抛出异常。
    /// </summary>
    public static GameRoot Instance => _instance ?? throw new InvalidOperationException("GameRoot 尚未初始化");

    /// <summary>
    /// 全局精灵批处理对象，所有场景共享此实例进行渲染。
    /// </summary>
    public SpriteBatch SpriteBatch => _spriteBatch ?? throw new InvalidOperationException("SpriteBatch 尚未初始化");

    /// <summary>
    /// 当前激活的场景实例。
    /// </summary>
    public Scene? CurrentScene => _currentScene;

    /// <summary>
    /// 设计分辨率宽度（像素）。
    /// </summary>
    public const int DESIGN_WIDTH = 1280;

    /// <summary>
    /// 设计分辨率高度（像素）。
    /// </summary>
    public const int DESIGN_HEIGHT = 720;

    /// <summary>
    /// 构造函数。初始化图形设备管理器和游戏窗口设置。
    /// 将自身实例赋值给静态单例引用。
    /// </summary>
    public GameRoot()
    {
        // 将当前实例注册为全局单例
        _instance = this;

        // 初始化图形设备管理器，设置窗口分辨率和垂直同步
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = DESIGN_WIDTH,
            PreferredBackBufferHeight = DESIGN_HEIGHT,
            SynchronizeWithVerticalRetrace = true
        };

        // 设置内容管线的根目录
        Content.RootDirectory = "Content";
        // 显示鼠标光标
        IsMouseVisible = true;
        // 设置游戏窗口标题
        Window.Title = "酒馆与命运";
    }

    /// <summary>
    /// 切换到新场景，支持可选的过渡效果。
    /// 实际切换发生在下一帧 Update 的开始时，以确保当前帧正常结束。
    /// </summary>
    /// <param name="nextScene">要切换到的目标场景实例。</param>
    public void StartSceneTransition(Scene nextScene)
    {
        // 缓存目标场景，实际切换延迟到下一帧 Update 开始时执行
        _nextScene = nextScene;
    }

    /// <summary>
    /// 初始化游戏系统。在构造函数之后、加载内容之前调用。
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// 加载游戏内容（纹理、字体、音频等资源）。
    /// 在此处创建全局共享的 SpriteBatch 实例。
    /// </summary>
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        base.LoadContent();
    }

    /// <summary>
    /// 每帧更新游戏逻辑。处理场景切换和当前场景的帧更新。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态快照。</param>
    protected override void Update(GameTime gameTime)
    {
        // 处理待执行的场景切换：结束旧场景 → 设置新场景 → 初始化 → 开始
        if (_nextScene != null)
        {
            // 结束当前场景，清理其资源
            _currentScene?.End();
            // 切换场景引用
            _currentScene = _nextScene;
            // 清除待切换标记
            _nextScene = null;
            // 初始化新场景
            _currentScene.Initialize();
            // 通知新场景开始运行
            _currentScene.Begin();
        }

        // 更新当前激活的场景
        _currentScene?.Update(gameTime);
        base.Update(gameTime);
    }

    /// <summary>
    /// 每帧渲染画面。清空屏幕后绘制当前场景。
    /// </summary>
    /// <param name="gameTime">当前帧的时间状态快照。</param>
    protected override void Draw(GameTime gameTime)
    {
        // 清空屏幕为黑色背景
        GraphicsDevice.Clear(Color.Black);
        // 绘制当前场景内容
        _currentScene?.Draw(gameTime);
        base.Draw(gameTime);
    }

    /// <summary>
    /// 释放托管和非托管资源。确保场景已结束、精灵批处理已释放。
    /// </summary>
    /// <param name="disposing">是否正在释放托管资源。</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 结束当前场景，清理实体和组件
            _currentScene?.End();
            // 释放全局精灵批处理对象
            _spriteBatch?.Dispose();
        }
        base.Dispose(disposing);
    }
}
