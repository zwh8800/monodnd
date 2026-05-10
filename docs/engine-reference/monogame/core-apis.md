# MonoGame 核心 2D API 参考

> **验证来源**: https://docs.monogame.net/api/
> **适用版本**: MonoGame 3.8.5+ / .NET 8 / C# 12
> **用途**: AI 代码生成的签名验证 — 不要猜测，使用此参考

---

## 目录

1. [Game 类 — 游戏入口](#game-类--游戏入口)
2. [游戏生命周期](#游戏生命周期)
3. [GraphicsDeviceManager — 图形配置](#graphicsdevicemanager--图形配置)
4. [SpriteBatch — 2D 渲染](#spritebatch--2d-渲染)
5. [Texture2D — 纹理](#texture2d--纹理)
6. [ContentManager — 资源加载](#contentmanager--资源加载)
7. [GameTime — 帧时间](#gametime--帧时间)
8. [Keyboard 输入](#keyboard-输入)
9. [Mouse 输入](#mouse-输入)
10. [GraphicsDevice — 图形设备](#graphicsdevice--图形设备)
11. [常用枚举和结构](#常用枚举和结构)

---

## Game 类 — 游戏入口

```csharp
namespace Microsoft.Xna.Framework;

public class Game : IDisposable
{
    // 构造函数
    public Game();

    // 属性
    public ContentManager Content { get; set; }
    public GraphicsDevice GraphicsDevice { get; }
    public GameWindow Window { get; }
    public bool IsActive { get; }
    public bool IsMouseVisible { get; set; }
    public TimeSpan TargetElapsedTime { get; set; }
    public bool IsFixedTimeStep { get; set; }      // 默认 true
    public bool IsRunningSlowly { get; }            // 丢帧检测

    // 可重写方法（生命周期回调）
    protected virtual void Initialize();
    protected virtual void LoadContent();
    protected virtual void UnloadContent();
    protected virtual void Update(GameTime gameTime);
    protected virtual void Draw(GameTime gameTime);
    protected virtual bool BeginDraw();             // Draw 前调用
    protected virtual void EndDraw();               // Draw 后调用
    protected virtual void BeginRun();              // Initialize 之后，第一次 Update 之前

    // 方法
    public void Exit();
    public void Run();                              // 启动游戏循环
    public void ResetElapsedTime();                 // 重置经过时间
    public void Tick();                             // 手动触发一次游戏循环迭代

    // Components
    public GameComponentCollection Components { get; } // 管理 GameComponent
}
```

### 典型用法

```csharp
public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // 初始化非图形资源
        // base.Initialize() 内部会调用 LoadContent()
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        // Content.Load<Texture2D>("assetName");
    }

    protected override void Update(GameTime gameTime)
    {
        // 游戏逻辑更新（60次/秒 固定步长）
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        // 渲染代码
        base.Draw(gameTime);
    }
}
```

> **项目注意**: `DndGame` 使用 `GameRoot : Game`（见 `src/DndGame/Core/GameRoot.cs`），不直接使用 `Game1`。

---

## 游戏生命周期

执行顺序（关键！）：

```
构造函数 Game1()
  → Initialize()        [查询服务，初始化非图形组件]
    → base.Initialize()  [内部调用 LoadContent()]
      → LoadContent()   [加载纹理、字体、音效等资源]
  → BeginRun()          [第一次 Update 前调用]
  → 游戏循环开始:
      → Update(gametime)  [固定步长 60 FPS，或可变速]
        → 输入处理
        → 游戏逻辑
        → 物理/碰撞
      → Draw(gametime)    [渲染帧]
        → BeginDraw()
        → GraphicsDevice.Clear()
        → SpriteBatch.Begin() ... End()
        → EndDraw()
  → UnloadContent()     [游戏退出时释放资源]
```

**固定步长 vs 可变步长**:

| 模式 | `IsFixedTimeStep` | 说明 |
|------|-------------------|------|
| 固定步长 | `true` (默认) | `Update` 按 `TargetElapsedTime`（默认 1/60 秒）调用 |
| 可变步长 | `false` | `Update` 在每次 `Draw` 后立即调用，需使用 `gameTime.ElapsedGameTime` |

**丢帧处理**: 当 `Update` 耗时超过 `TargetElapsedTime`，`IsRunningSlowly = true`，Game 会跳过 `Draw`，连续调用 `Update` 直到追上进度。

---

## GraphicsDeviceManager — 图形配置

```csharp
namespace Microsoft.Xna.Framework;

public class GraphicsDeviceManager : IGraphicsDeviceService, IDisposable
{
    // 构造函数
    public GraphicsDeviceManager(Game game);

    // 属性
    public GraphicsDevice GraphicsDevice { get; }
    public bool IsFullScreen { get; set; }
    public bool PreferMultiSampling { get; set; }
    public int PreferredBackBufferWidth { get; set; }   // 默认 800
    public int PreferredBackBufferHeight { get; set; }  // 默认 480
    public SurfaceFormat PreferredBackBufferFormat { get; set; }
    public DepthFormat PreferredDepthStencilFormat { get; set; }
    public bool SynchronizeWithVerticalRetrace { get; set; } // VSync，默认 true
    public bool HardwareModeSwitch { get; set; }             // true=模式切换, false=无边框全屏

    // 方法
    public void ApplyChanges();  // 应用属性修改
    public void ToggleFullScreen();

    // 静态属性
    public static readonly int DefaultPreparingDeviceSettings;
}
```

### 配置示例

```csharp
public Game1()
{
    _graphics = new GraphicsDeviceManager(this);

    // 分辨率和全屏
    _graphics.PreferredBackBufferWidth = 1280;
    _graphics.PreferredBackBufferHeight = 720;
    _graphics.IsFullScreen = false;

    // VSync
    _graphics.SynchronizeWithVerticalRetrace = true;

    // 应用设置
    _graphics.ApplyChanges();

    Content.RootDirectory = "Content";
    IsMouseVisible = true;
}
```

> **项目注意**: 项目使用 320×180 内部分辨率（pixel-art），通过 `PresentationParameters` 缩放。

---

## SpriteBatch — 2D 渲染

```csharp
namespace Microsoft.Xna.Framework.Graphics;

public class SpriteBatch : GraphicsResource, IDisposable
{
    // 构造函数
    public SpriteBatch(GraphicsDevice graphicsDevice);
    public SpriteBatch(GraphicsDevice graphicsDevice, int capacity);

    // 开始批处理
    public void Begin(
        SpriteSortMode sortMode = SpriteSortMode.Deferred,
        BlendState blendState = null,
        SamplerState samplerState = null,
        DepthStencilState depthStencilState = null,
        RasterizerState rasterizerState = null,
        Effect effect = null,
        Matrix? transformMatrix = null
    );

    // 绘制纹理 —— 位置版
    public void Draw(
        Texture2D texture,
        Vector2 position,
        Color color
    );
    public void Draw(
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color
    );
    public void Draw(
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        float layerDepth
    );
    public void Draw(
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth
    );

    // 绘制纹理 —— 矩形版
    public void Draw(
        Texture2D texture,
        Rectangle destinationRectangle,
        Color color
    );
    public void Draw(
        Texture2D texture,
        Rectangle destinationRectangle,
        Rectangle? sourceRectangle,
        Color color
    );
    public void Draw(
        Texture2D texture,
        Rectangle destinationRectangle,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        SpriteEffects effects,
        float layerDepth
    );

    // 绘制文本
    public void DrawString(
        SpriteFont spriteFont,
        string text,
        Vector2 position,
        Color color
    );
    public void DrawString(
        SpriteFont spriteFont,
        string text,
        Vector2 position,
        Color color,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        float layerDepth
    );
    public void DrawString(
        SpriteFont spriteFont,
        string text,
        Vector2 position,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth
    );
    // StringBuilder 重载同上（参数签名一致，仅 text 类型不同）
    public void DrawString(
        SpriteFont spriteFont,
        StringBuilder text,
        Vector2 position,
        Color color
    );
    public void DrawString(
        SpriteFont spriteFont,
        StringBuilder text,
        Vector2 position,
        Color color,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        float layerDepth
    );
    public void DrawString(
        SpriteFont spriteFont,
        StringBuilder text,
        Vector2 position,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth
    );

    // 结束批处理（提交所有绘制调用到 GPU）
    public void End();
}
```

### SpriteSortMode 枚举

| 值 | 说明 |
|------|------|
| `Deferred` (默认) | 不排序，按调用顺序提交（性能最优） |
| `Immediate` | 每调用一次 Draw 立即提交（不批处理，性能最差） |
| `Texture` | 按纹理排序，减少状态切换 |
| `BackToFront` | 按 layerDepth 从后到前 |
| `FrontToBack` | 按 layerDepth 从前到后 |

### 渲染模式

```csharp
// 标准模式（带矩阵变换）
_spriteBatch.Begin(
    SpriteSortMode.Deferred,
    BlendState.AlphaBlend,
    null, null, null, null,
    Matrix.CreateScale(4f)  // 像素艺术缩放
);

// 无变换模式（默认）
_spriteBatch.Begin();
_spriteBatch.Draw(texture, position, Color.White);
_spriteBatch.End();
```

> **注意**: `Begin()` 和 `End()` 必须成对出现。所有 `Draw` 调用必须在 `Begin`/`End` 之间。

---

## Texture2D — 纹理

```csharp
namespace Microsoft.Xna.Framework.Graphics;

public class Texture2D : Texture
{
    // 构造函数（创建空纹理）
    public Texture2D(GraphicsDevice graphicsDevice, int width, int height);
    public Texture2D(GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format);

    // 从流加载（支持 bmp, gif, jpg, png, tif — 不含 tga）
    public static Texture2D FromStream(GraphicsDevice graphicsDevice, Stream stream);
    public static Texture2D FromStream(GraphicsDevice graphicsDevice, Stream stream, Action<byte[]> colorProcessor);

    // 属性
    public int Width { get; }
    public int Height { get; }
    public Rectangle Bounds { get; }
    public SurfaceFormat Format { get; }

    // 方法
    public void SetData<T>(T[] data) where T : struct;
    public void SetData<T>(int level, Rectangle? rect, T[] data, int startIndex, int elementCount) where T : struct;
    public void GetData<T>(T[] data) where T : struct;
    public void SaveAsPng(Stream stream, int width, int height);
    public void SaveAsJpeg(Stream stream, int width, int height);
}
```

**内容管线加载**（推荐方式）：

```csharp
Texture2D texture = Content.Load<Texture2D>("Textures/character");
// 参数: 相对于 Content.RootDirectory 的路径，不包含扩展名
```

**运行时加载**（绕过内容管线）：

```csharp
using (FileStream stream = new FileStream("Content/character.png", FileMode.Open))
{
    Texture2D texture = Texture2D.FromStream(GraphicsDevice, stream);
}
```

---

## ContentManager — 资源加载

```csharp
namespace Microsoft.Xna.Framework.Content;

public class ContentManager : IDisposable
{
    // 构造函数
    public ContentManager(IServiceProvider serviceProvider);
    public ContentManager(IServiceProvider serviceProvider, string rootDirectory);

    // 属性
    public string RootDirectory { get; set; }
    public IServiceProvider ServiceProvider { get; }

    // 加载资源（核心方法）
    public virtual T Load<T>(string assetName);
    // assetName: 相对于 RootDirectory 的路径，不包含 .xnb 扩展名

    // 本地化加载（按当前 Culture 尝试加载）
    public T LoadLocalized<T>(string assetName);

    // 卸载所有资源
    public virtual void Unload();

    // 读取原始资源
    protected virtual T ReadAsset<T>(string assetName, Action<IDisposable> recordDisposableObject);
}
```

### 支持加载的类型

| 类型 | 说明 |
|------|------|
| `Texture2D` | 纹理/精灵图 |
| `SpriteFont` | 位图字体（.spritefont） |
| `SoundEffect` | 音效（.wav, .xnb） |
| `Song` | 音乐（.mp3, .ogg, .wma, .xnb） |
| `Effect` | 着色器（.fx 编译后） |

### 典型加载模式

```csharp
// 在 LoadContent() 中：
Texture2D heroTexture = Content.Load<Texture2D>("Characters/hero");
SpriteFont mainFont = Content.Load<SpriteFont>("Fonts/default");
SoundEffect clickSound = Content.Load<SoundEffect>("Sounds/click");
```

---

## GameTime — 帧时间

```csharp
namespace Microsoft.Xna.Framework;

public class GameTime
{
    // 构造函数
    public GameTime();
    public GameTime(TimeSpan totalGameTime, TimeSpan elapsedGameTime);
    public GameTime(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);

    // 属性
    public TimeSpan TotalGameTime { get; }      // 游戏启动以来的总时间
    public TimeSpan ElapsedGameTime { get; }    // 上一帧到当前帧的间隔（delta time）
    public bool IsRunningSlowly { get; }        // 是否因为性能不足在丢帧
}
```

### 使用示例

```csharp
protected override void Update(GameTime gameTime)
{
    float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
    // 使用 deltaTime 做帧率独立运动
    position += velocity * deltaTime;

    // 累计游戏时间
    float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
}

protected override void Draw(GameTime gameTime)
{
    // 通常 Draw 中不需要 deltaTime，
    // 但可用于插值渲染（预测位置）
    float interpolation = (float)gameTime.ElapsedGameTime.TotalSeconds;
}
```

> **固定步长模式**：`ElapsedGameTime` 总是等于 `TargetElapsedTime`（默认 1/60 秒），但如果丢帧则不会。

---

## Keyboard 输入

```csharp
namespace Microsoft.Xna.Framework.Input;

public static class Keyboard
{
    // 获取键盘状态
    public static KeyboardState GetState();
    public static KeyboardState GetState(PlayerIndex playerIndex);
}

public struct KeyboardState
{
    // 按键状态查询
    public bool IsKeyDown(Keys key);
    public bool IsKeyUp(Keys key);

    // 获取所有按下的键
    public Keys[] GetPressedKeys();

    // 属性
    public int CharCount { get; }
}

// 常用键枚举（部分）
public enum Keys
{
    A, B, C, ..., Z,
    D0, D1, ..., D9,
    F1, F2, ..., F12,
    Enter, Escape, Space, Tab, Back,
    Left, Right, Up, Down,
    LeftShift, RightShift,
    LeftControl, RightControl,
    LeftAlt, RightAlt,
    // ... 完整列表见官方文档
}
```

### 检测按下/释放（边缘触发）

```csharp
private KeyboardState _previousKeyboardState;

protected override void Update(GameTime gameTime)
{
    KeyboardState currentState = Keyboard.GetState();

    // 刚按下（边缘触发）
    if (currentState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space))
    {
        // 空格键刚被按下
    }

    // 按住不放
    if (currentState.IsKeyDown(Keys.Left))
    {
        // 左方向键正被按住
    }

    // 刚释放
    if (currentState.IsKeyUp(Keys.Space) && _previousKeyboardState.IsKeyDown(Keys.Space))
    {
        // 空格键刚被释放
    }

    _previousKeyboardState = currentState;
}
```

---

## Mouse 输入

```csharp
namespace Microsoft.Xna.Framework.Input;

public static class Mouse
{
    // 属性
    public static IntPtr WindowHandle { get; set; }

    // 获取状态
    public static MouseState GetState();
    public static MouseState GetState(GameWindow window);

    // 设置光标
    public static void SetCursor(MouseCursor cursor);

    // 设置鼠标位置（相对于窗口）
    public static void SetPosition(int x, int y);
}

public struct MouseState
{
    // 位置
    public int X { get; }                           // 鼠标 X（相对于窗口）
    public int Y { get; }                           // 鼠标 Y（相对于窗口）

    // 按钮状态（ButtonState.Pressed / Released）
    public ButtonState LeftButton { get; }
    public ButtonState MiddleButton { get; }
    public ButtonState RightButton { get; }
    public ButtonState XButton1 { get; }
    public ButtonState XButton2 { get; }

    // 滚轮
    public int ScrollWheelValue { get; }             // 累计滚动值（未重置）
    public int HorizontalScrollWheel { get; }
}

public enum ButtonState
{
    Released,
    Pressed
}
```

### 使用示例

```csharp
MouseState mouseState = Mouse.GetState();

// 检测点击
if (mouseState.LeftButton == ButtonState.Pressed) { }

// 获取位置
Vector2 mousePosition = new Vector2(mouseState.X, mouseState.Y);

// 检测滚动
int scrollDelta = mouseState.ScrollWheelValue - _previousScrollValue;
```

---

## GraphicsDevice — 图形设备

```csharp
namespace Microsoft.Xna.Framework.Graphics;

public class GraphicsDevice : IDisposable
{
    // 属性
    public Viewport Viewport { get; set; }
    public PresentationParameters PresentationParameters { get; }
    public Color BlendFactor { get; set; }
    public BlendState BlendState { get; set; }
    public SamplerState SamplerState { get; set; }
    public DepthStencilState DepthStencilState { get; set; }
    public RasterizerState RasterizerState { get; set; }
    public TextureCollection Textures { get; }
    public GraphicsAdapter Adapter { get; }
    public GraphicsProfile GraphicsProfile { get; }

    // 清屏
    public void Clear(Color color);
    public void Clear(ClearOptions options, Color color, float depth, int stencil);
    public void Clear(ClearOptions options, Vector4 color, float depth, int stencil);

    // 设置/获取渲染目标
    public void SetRenderTarget(RenderTarget2D renderTarget);
    public RenderTarget2D GetRenderTarget();

    // 重置（修改分辨率/全屏模式后调用）
    public void Reset(PresentationParameters presentationParameters);

    // 视口管理
    public Rectangle ScissorRectangle { get; set; }
}
```

> **注意**: `Game.GraphicsDevice` 是框架自动创建的，不要自己构造 `GraphicsDevice`。如果需要手动创建，使用 `new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, new PresentationParameters())`。

---

## 常用枚举和结构

### SpriteEffects（绘制翻转）

```csharp
[Flags]
public enum SpriteEffects
{
    None = 0,
    FlipHorizontally = 1,
    FlipVertically = 2
}
```

### BlendState（混合模式）

```csharp
public class BlendState : GraphicsResource
{
    public static readonly BlendState AlphaBlend;      // 透明度混合（默认）
    public static readonly BlendState Opaque;          // 不透明
    public static readonly BlendState Additive;        // 叠加混合
    public static readonly BlendState NonPremultiplied; // 非预乘 Alpha
}
```

### SamplerState（纹理采样）

```csharp
public class SamplerState : GraphicsResource
{
    public static readonly SamplerState LinearClamp;    // 线性过滤
    public static readonly SamplerState PointClamp;     // 点采样（像素艺术必备）
    public static readonly SamplerState LinearWrap;     // 线性 + 重复
    public static readonly SamplerState PointWrap;      // 点采样 + 重复

    public TextureAddressMode AddressU { get; set; }
    public TextureAddressMode AddressV { get; set; }
    public TextureFilter Filter { get; set; }
    public int MaxAnisotropy { get; set; }
    public int MaxMipLevel { get; set; }
    public float MipMapLevelOfDetailBias { get; set; }
}
```

### 颜色

```csharp
// Color 结构体包含预定义颜色常量
Color.White, Color.Black, Color.Transparent,
Color.CornflowerBlue, Color.Red, Color.Green, Color.Blue,
// 以及 FromNonPremultiplied 等方法
```

### 向量和矩形

```csharp
// 均在 Microsoft.Xna.Framework 命名空间
Vector2    // X, Y — 2D 向量/位置
Vector3    // X, Y, Z
Point      // X, Y — 整数坐标
Rectangle  // X, Y, Width, Height
Matrix     // 4×4 变换矩阵（含 Matrix.CreateScale, CreateTranslation, CreateRotationZ 等）
```

---

## 命名空间一览

```csharp
using Microsoft.Xna.Framework;             // Game, GameTime, Vector2, Color, Matrix, etc.
using Microsoft.Xna.Framework.Graphics;     // SpriteBatch, Texture2D, GraphicsDevice, etc.
using Microsoft.Xna.Framework.Content;      // ContentManager
using Microsoft.Xna.Framework.Input;        // Keyboard, Mouse, GamePad
using Microsoft.Xna.Framework.Audio;        // SoundEffect
using Microsoft.Xna.Framework.Media;        // Song, MediaPlayer
```

---

## 像素艺术项目关键设置

```csharp
// GraphicsDeviceManager 配置
_graphics.PreferredBackBufferWidth = 320;   // 内部渲染分辨率
_graphics.PreferredBackBufferHeight = 180;
_graphics.HardwareModeSwitch = false;       // 无边框窗口缩放

// SpriteBatch 缩放渲染
_spriteBatch.Begin(
    SpriteSortMode.Deferred,
    BlendState.AlphaBlend,
    SamplerState.PointClamp,                // 点采样 — 像素艺术必须
    null, null, null,
    Matrix.CreateScale(4f)                  // 4倍缩放 → 1280×720 显示
);
```
