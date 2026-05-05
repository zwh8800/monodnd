# 酒馆与命运 — MonoGame 开发环境搭建指南

> **文档版本**: v1.0
> **更新日期**: 2026-05-05
> **适用范围**: 《酒馆与命运》项目全平台开发环境搭建
> **前置文档**: 01-engine-selection.md, 02-overall-architecture.md, 03-vibe-coding-conventions.md
> **目标读者**: 新加入项目的开发者，或需要在不同机器上搭建开发环境的团队成员

---

## 目录

1. [环境概述](#1-环境概述)
2. [必备软件安装](#2-必备软件安装)
3. [项目创建与初始化](#3-项目创建与初始化)
4. [内容管线配置 MGCB](#4-内容管线配置-mgcb)
5. [Nez 框架集成](#5-nez-框架集成)
6. [GoRogue 集成](#6-gorogue-集成)
7. [验证环境搭建成功](#7-验证环境搭建成功)
8. [常见问题与排错](#8-常见问题与排错)

---

## 1. 环境概述

### 1.1 技术栈版本矩阵

以下版本必须与 02-overall-architecture.md 完全一致。

| 组件 | 版本 | 说明 |
|------|:----:|------|
| .NET SDK | 8.0+ | 目标框架 net8.0 |
| MonoGame | 3.8.5+ | DesktopGL，OpenGL 后端 |
| C# | 12 | record 类型、模式匹配 |
| Nez | 2.x | Scene/Entity/Component ECS |
| MonoGame.Extended | 6.0+ | Tiled 地图加载、Camera |
| GoRogue | 3.x | FOV 视野、A* 寻路、地图生成 |
| Myra | 1.x | 像素风 UI 框架 |
| sqlite-net-pcl | 1.9+ | 轻量 ORM |
| JsonSchema.Net | 7+ | JSON Schema Draft 7+ 验证 |
| FontStashSharp.MonoGame | 1.x | 动态字体渲染，支持中文 |
| xUnit | 2.x | 单元测试框架 |
| FluentAssertions | 7.x | 自然语言风格测试断言 |
| MonoGame.Content.Builder.Task | 3.8.5+ | MSBuild 内容管线构建 |

### 1.2 开发环境最低要求

| 项目 | macOS | Windows | Linux |
|------|-------|---------|-------|
| 操作系统 | macOS 13 Ventura+ | Windows 10 2004+ | Ubuntu 22.04+ / Fedora 38+ |
| CPU | Intel Core i5 / Apple M1+ | Intel Core i5 / AMD Ryzen 3+ | Intel Core i3+ |
| 内存 | 8 GB（推荐 16 GB） | 同左 | 同左 |
| GPU | OpenGL 4.1+ | OpenGL 4.1+ / DirectX 11+ | OpenGL 4.1+ |

### 1.3 目标平台支持

| 平台 | 后端 | 架构 | 支持等级 |
|------|------|:----:|:--------:|
| Windows | DesktopGL | x64 | 主力 |
| macOS (Apple Silicon) | DesktopGL | arm64 | 主力（当前开发机） |
| macOS (Intel) | DesktopGL | x64 | 支持 |
| Linux | DesktopGL | x64 | 支持 |
| Android / iOS | Android / iOS | arm64 | Phase 2+ |

---

## 2. 必备软件安装

### 2.1 .NET 8 SDK

**官方下载**: https://dotnet.microsoft.com/download/dotnet/8.0

```bash
# macOS (brew)
brew install dotnet-sdk

# Windows (winget)
winget install Microsoft.DotNet.SDK.8

# Ubuntu/Debian
sudo apt update && sudo apt install -y dotnet-sdk-8.0

# Fedora
sudo dnf install dotnet-sdk-8.0
```

**验证**:

```bash
dotnet --version
# 期望: 8.0.4xx（如 8.0.405）
```

**常见问题**: macOS 上如果 `dotnet` 找不到，检查 `export PATH=$PATH:/usr/local/share/dotnet` 是否在 ~/.zshrc 中。

### 2.2 IDE

**选项 A: VS Code + C# Dev Kit**

```bash
# macOS
brew install --cask visual-studio-code

# Windows / Linux
# 从 https://code.visualstudio.com/ 下载安装
```

VS Code 扩展（必装）：`ms-dotnettools.csdevkit`（C# Dev Kit）。

**选项 B: JetBrains Rider**

```bash
# macOS
brew install --cask rider

# Windows
winget install JetBrains.Rider

# Linux: 从 https://www.jetbrains.com/rider/download/ 下载
```

### 2.3 Git

```bash
# macOS (一般自带，如需更新)
brew install git

# Windows
winget install Git.Git

# Ubuntu/Debian
sudo apt install git
```

**全局配置**:

```bash
git config --global user.name "Your Name"
git config --global user.email "your@email.com"
git config --global init.defaultBranch main
```

### 2.4 MonoGame 项目模板

```bash
dotnet new install MonoGame.Templates.CSharp

# 验证
dotnet new list | grep -i monogame
# 期望: mgdesktopgl, mgwindowsdx 等模板
```

如需更新或重装：

```bash
dotnet new uninstall MonoGame.Templates.CSharp
dotnet new install MonoGame.Templates.CSharp
```

### 2.5 MGCB 工具（内容管线核心）

```bash
# 全局安装 MGCB 命令行工具
dotnet tool install -g dotnet-mgcb

# MGCB Editor（可视化编辑器）
dotnet tool install -g dotnet-mgcb-editor

# 验证
dotnet mgcb --version
```

macOS 上如果 MGCB Editor 无法启动，装平台指定版本：

```bash
dotnet tool install -g dotnet-mgcb-editor-mac
dotnet mgcb-editor-mac ./Content/Content.mgcb
```

### 2.6 Aseprite（像素美术）

官方下载: https://aseprite.org/

```bash
# macOS
brew install --cask aseprit

# Windows
winget install Aseprite.Aseprite
```

### 2.7 Tiled（地图编辑器）

官方下载: https://www.mapeditor.org/

```bash
# macOS
brew install --cask tiled

# Windows
winget install Tiled.Tiled

# Ubuntu/Debian
sudo apt install tiled
```

### 2.8 安装速查表

| 工具 | macOS | Windows | Linux |
|------|-------|---------|-------|
| .NET 8 SDK | `brew install dotnet-sdk` | `winget install Microsoft.DotNet.SDK.8` | `sudo apt install dotnet-sdk-8.0` |
| VS Code | `brew install --cask visual-studio-code` | winget / 官网下载 | 官网 .deb/.rpm |
| Git | `brew install git` | `winget install Git.Git` | `sudo apt install git` |
| MonoGame 模板 | `dotnet new install MonoGame.Templates.CSharp` | 同左 | 同左 |
| MGCB | `dotnet tool install -g dotnet-mgcb` | 同左 | 同左 |
| Aseprite | `brew install --cask aseprit` | `winget install Aseprite.Aseprite` | 官网 AppImage |
| Tiled | `brew install --cask tiled` | `winget install Tiled.Tiled` | `sudo apt install tiled` |

---

## 3. 项目创建与初始化

### 3.1 创建解决方案

```bash
cd /Users/wastecat/code/game/dnd

# 创建解决方案
dotnet new sln -n DndGame

# 创建项目目录
mkdir -p src/DndGame
mkdir -p tests/DndGame.Tests
```

### 3.2 创建 MonoGame 主项目

```bash
cd src/DndGame
dotnet new mgdesktopgl -n DndGame
cd ../../
```

### 3.3 创建测试项目

```bash
cd tests/DndGame.Tests
dotnet new xunit -n DndGame.Tests
cd ../../
```

### 3.4 添加到解决方案

```bash
dotnet sln DndGame.sln add src/DndGame/DndGame.csproj
dotnet sln DndGame.sln add tests/DndGame.Tests/DndGame.Tests.csproj
```

### 3.5 安装 NuGet 包（核心步骤）

**主项目**:

```bash
cd src/DndGame

# 游戏框架（模板已自动添加，按需更新版本）
dotnet add package MonoGame.Framework.DesktopGL --version 3.8.5.*
dotnet add package MonoGame.Content.Builder.Task --version 3.8.5.*

# 场景管理/ECS
dotnet add package Nez --version 2.*

# 地图/Tiled
dotnet add package MonoGame.Extended --version 6.0.*
dotnet add package MonoGame.Extended.Content.Pipeline --version 6.0.*
dotnet add package MonoGame.Extended.Tiled --version 6.0.*

# Roguelike 工具
dotnet add package GoRogue --version 3.*

# UI 框架
dotnet add package Myra --version 1.*

# 数据持久化
dotnet add package sqlite-net-pcl --version 1.9.*

# JSON Schema 验证
dotnet add package JsonSchema.Net --version 7.*

# 字体渲染
dotnet add package FontStashSharp.MonoGame --version 1.*

cd ../../
```

**测试项目**:

```bash
cd tests/DndGame.Tests
dotnet add package FluentAssertions --version 7.*
dotnet add package Microsoft.NET.Test.Sdk --version 17.*
cd ../../
```

### 3.6 完整的 .csproj

安装完所有包后，`src/DndGame/DndGame.csproj` 的内容应为：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>12</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.5.*" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.5.*" />
    <PackageReference Include="Nez" Version="2.*" />
    <PackageReference Include="MonoGame.Extended" Version="6.0.*" />
    <PackageReference Include="MonoGame.Extended.Content.Pipeline" Version="6.0.*" />
    <PackageReference Include="MonoGame.Extended.Tiled" Version="6.0.*" />
    <PackageReference Include="GoRogue" Version="3.*" />
    <PackageReference Include="Myra" Version="1.*" />
    <PackageReference Include="sqlite-net-pcl" Version="1.9.*" />
    <PackageReference Include="JsonSchema.Net" Version="7.*" />
    <PackageReference Include="FontStashSharp.MonoGame" Version="1.*" />
  </ItemGroup>
</Project>
```

测试项目 `tests/DndGame.Tests/DndGame.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>12</LangVersion>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="7.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DndGame\DndGame.csproj" />
  </ItemGroup>
</Project>
```

### 3.7 .gitignore

与 03-vibe-coding-conventions.md §6.4 一致：

```
# MonoGame / 内容管线
bin/
obj/
Content/*.xnb
*.mgcontent
Content/obj/

# IDE
.vs/
.vscode/settings.json
.idea/
*.user
*.suo

# OS
.DS_Store
Thumbs.db
*.swp

# 构建输出
[Dd]ebug/
[Rr]elease/
packages/
```

### 3.8 创建项目目录结构

与 02-overall-architecture.md §4.4 一致：

```bash
cd src/DndGame
mkdir -p Core Systems/Combat Systems/Character Systems/Tavern
mkdir -p Systems/Adventure Systems/WorldState Systems/Items
mkdir -p Gateway/Agents Gateway/Validation Gateway/Cache
mkdir -p Gateway/Fallback/Templates
mkdir -p Scenes Entities
mkdir -p UI/Layouts UI/Widgets
mkdir -p Data/Database/Migrations Data/Config Data/Schemas
mkdir -p Data/Templates/adventures Data/Templates/narratives Data/Templates/descriptions
mkdir -p Content/Sprites/Characters Content/Sprites/Enemies
mkdir -p Content/Sprites/Tilesets Content/Sprites/UI
mkdir -p Content/Tilesets Content/Maps Content/Fonts
mkdir -p Content/Audio/BGM Content/Audio/SFX
cd ../../

mkdir -p tests/DndGame.Tests/Unit/Combat
mkdir -p tests/DndGame.Tests/Unit/Character
mkdir -p tests/DndGame.Tests/Unit/Adventure
mkdir -p tests/DndGame.Tests/Unit/Gateway
mkdir -p tests/DndGame.Tests/Integration
```

### 3.9 首次构建

```bash
dotnet build
# 期望: Build succeeded. 0 Warning(s) 0 Error(s)
```

---

## 4. 内容管线配置 MGCB

### 4.1 Content.mgcb 文件结构

`Content.mgcb` 位于 `src/DndGame/Content/`，由项目模板自动生成。管理所有原始资源（纹理、字体、音效）的编译规则。结构如下：

```ini
#------------------ 全局设置 ------------------
/outputDir:bin/$(Platform)
/intermediateDir:obj/$(Platform)
/platform:DesktopGL
/config:
/profile:Reach

#------------------ 纹理 ------------------
#begin textures/player.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyColor=255,0,255,255
/processorParam:ColorKeyEnabled=True
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/processorParam:GenerateMipmaps=False
/build:textures/player.png

#------------------ 字体 (SpriteFont) ------------------
#begin fonts/MenuFont.spritefont
/importer:FontDescriptionImporter
/processor:FontDescriptionProcessor
/build:fonts/MenuFont.spritefont

#------------------ 音效 ------------------
#begin audio/sfx/sword_swing.wav
/importer:WavImporter
/processor:SoundEffectProcessor
/build:audio/sfx/sword_swing.wav

#------------------ 背景音乐 ------------------
#begin audio/bgm/tavern_theme.ogg
/importer:OggImporter
/processor:SongProcessor
/build:audio/bgm/tavern_theme.ogg

#------------------ Tiled地图 ------------------
#begin maps/dungeon_01.tmx
/importer:TiledMapImporter
/processor:TiledMapProcessor
/build:maps/dungeon_01.tmx
```

### 4.2 使用 MGCB Editor

```bash
# 在项目目录中打开
cd src/DndGame
dotnet mgcb-editor ./Content/Content.mgcb
```

MGCB Editor 提供可视化界面：
- 左侧项目树：显示所有已注册的资源
- 右侧属性面板：设置导入器/处理器参数
- 工具栏：添加文件、构建、清理

### 4.3 纹理配置要点

| 参数 | 像素风推荐值 | 说明 |
|------|:-----------:|------|
| ColorKeyColor | 255,0,255,255 | 品红色透明色 |
| ColorKeyEnabled | True | 启用透明色 |
| PremultiplyAlpha | True | 预乘Alpha |
| GenerateMipmaps | False | 像素风格不需要 mipmap |
| TextureFormat | Color | 不压缩，保留像素细节 |

### 4.4 字体方案

**方案 A: SpriteFont**（适合英文 UI）

创建 `.spritefont` 文件：
```xml
<XnaContent>
  <Asset Type="Graphics:FontDescription">
    <FontName>Verdana</FontName>
    <Size>16</Size>
    <Spacing>1</Spacing>
    <Style>Regular</Style>
    <CharacterRegions>
      <CharacterRegion>
        <Start>&#32;</Start>
        <End>&#126;</End>
      </CharacterRegion>
    </CharacterRegions>
  </Asset>
</XnaContent>
```

**方案 B: FontStashSharp 动态字体**（本项目主力方案，支持中文）

FontStashSharp 直接加载 `.ttf` 文件，不经过 MGCB 编译。将 `NotoSansCJKsc-Regular.ttf` 放在 `Content/Fonts/` 目录中，在 .csproj 中添加：

```xml
<ItemGroup>
  <None Update="Content\Fonts\NotoSansCJKsc-Regular.ttf">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

或者在 Content.mgcb 中用 CopyProcessor：
```ini
#begin fonts/NotoSansCJKsc-Regular.ttf
/importer:TrueTypeImporter
/processor:CopyProcessor
/build:fonts/NotoSansCJKsc-Regular.ttf
```

### 4.5 命令行构建

```bash
# 通过 dotnet build 自动编译（推荐，已集成 MonoGame.Content.Builder.Task）
dotnet build

# 手动调用 MGCB
dotnet mgcb ./Content/Content.mgcb

# 清理编译缓存
dotnet mgcb /clean ./Content/Content.mgcb
```

编译后的 `.xnb` 文件输出到 `Content/bin/DesktopGL/`。

### 4.6 在代码中加载资源

```csharp
// 纹理
Texture2D player = Content.Load<Texture2D>("textures/player");

// 字体（SpriteFont）
SpriteFont font = Content.Load<SpriteFont>("fonts/MenuFont");

// 音效
SoundEffect sfx = Content.Load<SoundEffect>("audio/sfx/sword_swing");

// 背景音乐
Song bgm = Content.Load<Song>("audio/bgm/tavern_theme");

// Tiled 地图
TiledMap map = Content.Load<TiledMap>("maps/dungeon_01");
```

---

## 5. Nez 框架集成

### 5.1 Game1.cs 改造

将模板生成的 `Game1.cs` 改为继承 Nez 的 `Core` 类：

```csharp
using Nez;

namespace DndGame.Core;

public class Game1 : Core
{
    public Game1() : base(
        width: 1280,
        height: 720,
        isFullScreen: false,
        windowTitle: "酒馆与命运")
    {
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Graphics.PreferredBackBufferWidth = 1280;
        Graphics.PreferredBackBufferHeight = 720;
        Graphics.SynchronizeWithVerticalRetrace = true;
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        Scene = new MainMenuScene();
    }
}
```

**要点**:
- 继承 `Nez.Core` 而非 `Microsoft.Xna.Framework.Game`。
- `Core` 封装了 `GraphicsDeviceManager`、`SpriteBatch`。
- `Scene` 属性由 Nez 管理，直接赋值切换场景。

### 5.2 Scene/Entity/Component 示例

```csharp
// 场景
public class MainMenuScene : Scene
{
    public override void OnStart()
    {
        AddSceneComponent(new DefaultRenderer());

        var title = CreateEntity("title");
        title.AddComponent(new SpriteRenderer(
            Entity.Scene.Content.Load<Texture2D>("textures/title_logo")));
    }
}

// 组件
public class TitleComponent : Component
{
    private SpriteRenderer _renderer;

    public override void OnAddedToEntity()
    {
        // 构造函数不做繁重操作，在 OnAddedToEntity 中初始化
        var tex = Entity.Scene.Content.Load<Texture2D>("textures/title_logo");
        _renderer = new SpriteRenderer(tex);
        Entity.AddComponent(_renderer);
    }
}
```

### 5.3 场景切换

```csharp
// 带过渡动画的场景切换
Core.StartSceneTransition(new FadeTransition(() => new CombatScene()));

// 带上下文的场景切换
Core.StartSceneTransition(new FadeTransition(() => new AdventureScene(data)));
```

### 5.4 常见集成问题

| 问题 | 原因 | 解法 |
|------|------|------|
| `Core` 找不到 | 缺 `using Nez;` | 添加 using 指令 |
| 场景切换后黑屏 | 未添加 Renderer | 在 `OnStart` 中加 `AddSceneComponent(new DefaultRenderer())` |
| Sprite 模糊 | 纹理过滤模式 Linear | 用 `SamplerState.PointClamp` |
| Nez 版本兼容 | Nez 2.x 与 MonoGame 3.8.5+ | 锁定 Nez 最新稳定版 |

---

## 6. GoRogue 集成

### 6.1 ArrayMap 基本用法

```csharp
using GoRogue;
using GoRogue.MapViews;

public enum TileType { Floor = 0, Wall = 1, Water = 2, Door = 3 }

public class GameMap
{
    public ArrayMap<TileType> Tiles { get; private set; }

    public GameMap(int width, int height)
    {
        Tiles = new ArrayMap<TileType>(width, height);
    }

    // 生成通行性映射（false = 不可通行）
    public ArrayMap<bool> GetWalkabilityMap()
    {
        var walkable = new ArrayMap<bool>(Tiles.Width, Tiles.Height);
        for (int x = 0; x < Tiles.Width; x++)
        for (int y = 0; y < Tiles.Height; y++)
            walkable[x, y] = Tiles[x, y] == TileType.Floor;
        return walkable;
    }
}
```

### 6.2 FOV 视野计算

```csharp
using GoRogue;
using GoRogue.FOV;

public class FOVSystem
{
    private FOV _fov;
    private ArrayMap<bool> _walkabilityMap;

    public FOVSystem(ArrayMap<bool> walkabilityMap)
    {
        _walkabilityMap = walkabilityMap;
        _fov = new FOV(walkabilityMap);
    }

    public HashSet<Coord> CalculateFOV(Coord origin, int radius)
    {
        _fov.Calculate(origin.X, origin.Y, radius, Radius.CIRCLE);
        return _fov.CurrentFOV.ToHashSet();
    }

    public bool IsVisible(Coord pos) => _fov.BooleanFOV[pos.X, pos.Y];
}
```

### 6.3 A* 寻路

```csharp
using GoRogue;
using GoRogue.Pathing;

public class PathfindingSystem
{
    private AStar _aStar;

    public PathfindingSystem(ArrayMap<bool> walkabilityMap)
    {
        _aStar = new AStar(walkabilityMap, Distance.MANHATTAN);
    }

    public List<Coord> FindPath(Coord start, Coord end)
    {
        var path = _aStar.ShortestPath(start, end);
        return path?.ToList() ?? new List<Coord>();
    }

    public bool IsReachable(Coord start, Coord end)
        => _aStar.ShortestPath(start, end) != null;
}
```

### 6.4 地图生成（CaveGenerator）

```csharp
using GoRogue.MapGeneration.Generators;

public class MapGenerator
{
    public ArrayMap<TileType> GenerateCaveMap(int width, int height)
    {
        var generator = new CaveGenerator(width, height);
        var generated = generator.Generate();

        var result = new ArrayMap<TileType>(width, height);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            result[x, y] = generated[x, y] ? TileType.Floor : TileType.Wall;

        return result;
    }

    public ArrayMap<TileType> GenerateBSPMap(int width, int height)
    {
        var generator = new RoomGenerator(width, height);
        var generated = generator.Generate();

        var result = new ArrayMap<TileType>(width, height);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            result[x, y] = generated[x, y] ? TileType.Floor : TileType.Wall;

        return result;
    }
}
```

### 6.5 与 Nez Scene 集成

```csharp
public class AdventureScene : Scene
{
    private FOVSystem _fovSystem;
    private PathfindingSystem _pathfinding;

    public override void OnStart()
    {
        AddSceneComponent(new DefaultRenderer());

        var map = new GameMap(40, 30);
        // ... 初始化地图数据 ...

        var walkability = map.GetWalkabilityMap();
        _fovSystem = new FOVSystem(walkability);
        _pathfinding = new PathfindingSystem(walkability);

        var player = CreateEntity("player");
        player.AddComponent(new PlayerComponent(_fovSystem, _pathfinding));
    }
}
```

**注意**: GoRogue 的 `Coord` 与 Nez 的 `Point` 可互相转换：`new Point(coord.X, coord.Y)`。

---

## 7. 验证环境搭建成功

### 7.1 验证清单

| # | 验证项 | 命令/方法 | 期望结果 | 状态 |
|:-:|--------|-----------|----------|:----:|
| 1 | dotnet build | `dotnet build` | 0 error, 0 warning | [ ] |
| 2 | dotnet test | `dotnet test` | All tests passed | [ ] |
| 3 | 窗口可打开 | `dotnet run` | 1280x720 窗口 | [ ] |
| 4 | Nez 初始化 | 代码注入（见下文） | 控制台输出成功信息 | [ ] |
| 5 | GoRogue ArrayMap | 代码注入 | 创建 10x10 成功 | [ ] |
| 6 | Myra 可用 | `dotnet list package \| grep Myra` | Myra 1.x 已安装 | [ ] |
| 7 | FontStashSharp 中文 | 代码注入 | "你好世界"渲染成功 | [ ] |
| 8 | sqlite-net 数据库 | 代码注入 | 创建/插入/查询成功 | [ ] |
| 9 | Content.mgcb | `ls Content/bin/DesktopGL/` | .xnb 文件存在 | [ ] |
| 10 | Tiled 地图 | `tiled --version` | Tiled 已安装 | [ ] |

### 7.2 核心验证代码

在 `Program.cs` 的 `Main` 方法中依次执行以下验证段（可在完成全部验证后移除）：

**Nez 验证**:

```csharp
// Nez Core 初始化后在 Game1.LoadContent 中输出
// 在 Game1.cs 的 LoadContent 中
Console.WriteLine($"Nez Core 初始化成功，版本: {Nez.Core.Version}");
```

**GoRogue 验证**:

```csharp
using GoRogue;
using GoRogue.MapViews;

var testMap = new ArrayMap<bool>(10, 10);
for (int x = 0; x < 10; x++)
for (int y = 0; y < 10; y++)
    testMap[x, y] = (x + y) % 2 == 0;
Console.WriteLine($"GoRogue ArrayMap 创建成功: {testMap.Width}x{testMap.Height}");
```

**FontStashSharp 验证**:

```csharp
using FontStashSharp;

var fontSystem = new FontSystem();
fontSystem.AddFont(File.ReadAllBytes("Content/Fonts/NotoSansCJKsc-Regular.ttf"));
var font = fontSystem.GetFont(16);
var size = font.MeasureString("你好世界");
Console.WriteLine($"FontStashSharp 中文渲染成功，尺寸: {size.X}x{size.Y}");
```

**sqlite-net 验证**:

```csharp
using SQLite;

var db = new SQLiteConnection(":memory:");
db.CreateTable<TestEntity>();
db.Insert(new TestEntity { Name = "验证", Value = 42 });
var result = db.Table<TestEntity>().First();
Console.WriteLine($"sqlite-net 数据库成功: Id={result.Id}, Name={result.Name}");

public class TestEntity
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Value { get; set; }
}
```

### 7.3 完整验证命令序列

```bash
# 1. 检查 SDK
dotnet --version                    # >= 8.0

# 2. 检查模板
dotnet new list | grep mgdesktopgl

# 3. 构建
dotnet build                        # 0 errors, 0 warnings

# 4. 测试
dotnet test                         # All passed

# 5. 内容管线编译
dotnet mgcb src/DndGame/Content/Content.mgcb

# 6. 发布验证
dotnet publish -c Release src/DndGame/DndGame.csproj
```

---

## 8. 常见问题与排错

### 8.1 macOS 问题

**OpenGL 错误**:

```
The type initializer for 'OpenTK.Toolkit' threw an exception.
```

确保 macOS 版本 >= 10.15（Apple Silicon 通过 Rosetta 2 兼容 OpenGL）。

**dotnet 找不到**:

```bash
export PATH=$PATH:/usr/local/share/dotnet
echo 'export PATH=$PATH:/usr/local/share/dotnet' >> ~/.zshrc
```

**MGCB Editor 无响应**:

```bash
dotnet tool install -g dotnet-mgcb-editor-mac
dotnet mgcb-editor-mac ./Content/Content.mgcb
```

### 8.2 Windows 问题

**MGCB Editor 无法打开**:

```bash
dotnet tool install -g dotnet-mgcb-editor-windows
dotnet mgcb-editor-windows ./Content/Content.mgcb
```

**音频编译失败**:

```
No suitable audio conversion tool found.
```

安装 ffmpeg：`winget install ffmpeg`。

### 8.3 Linux 问题

**字体依赖**:

```bash
sudo apt install libfreetype6 libfontconfig1 fonts-noto-cjk
```

**OpenGL 驱动**:

```bash
sudo apt install mesa-utils
glxinfo | grep "OpenGL version"  # 需要 4.1+
```

### 8.4 NuGet 版本冲突

```bash
# 查看所有传递依赖
dotnet list package --include-transitive

# 找到冲突来源后，在 .csproj 中显式锁定版本
```

常见冲突：Nez 依赖旧版 MonoGame 核心包。解决：

```xml
<!-- 统一版本声明 -->
<PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.5.*" />
```

### 8.5 .NET SDK 版本不匹配

```
NETSDK1045: The current .NET SDK does not support targeting .NET 8.0.
```

用 `global.json` 锁定 SDK 版本：

```json
{ "sdk": { "version": "8.0.400", "rollForward": "latestFeature" } }
```

### 8.6 Nez 与 MonoGame 兼容性

| Nez 版本 | MonoGame 版本 | 状态 |
|:--------:|:-------------:|:----:|
| 2.0.x | 3.8.1.x | 旧版 |
| 2.1.x | 3.8.1.x - 3.8.5.x | 推荐 |
| 2.2.x | 3.8.5+ | 最新 |

```bash
# 更新 Nez
dotnet add package Nez --version 2.*
```

### 8.7 GoRogue 与 MonoGame.Extended 共存

两者无直接冲突。注意 `Coord`(GoRogue) 与 `Point2`(MonoGame.Extended) 是不同的类型。

### 8.8 内容管线排错速查

| 错误 | 原因 | 解法 |
|------|------|------|
| `No suitable processor found` | 缺 Content Pipeline NuGet | 装 `MonoGame.Extended.Content.Pipeline` |
| `File not found: xxx.png` | 路径不对 | 路径相对于 Content 目录 |
| `Importer '...' not found` | MGCB 版本不匹配 | 重装 `dotnet-mgcb` |
| `Audio conversion failed` | 缺 ffmpeg | 安装 ffmpeg |
| `.xnb not found at runtime` | 未复制到输出目录 | 装 `MonoGame.Content.Builder.Task` |

### 8.9 重建环境

```bash
dotnet clean
rm -rf src/DndGame/Content/bin src/DndGame/Content/obj
dotnet restore
dotnet mgcb --rebuild src/DndGame/Content/Content.mgcb
dotnet build
dotnet test
```

---

> **文档维护**: 当 MonoGame 或任一 NuGet 包版本更新时，需要同步更新 §1.1 版本矩阵和 §3.5 安装命令。版本变更必须与 02-overall-architecture.md 保持一致。
>
> **环境搭建完成确认**: 请完成 §7.1 验证清单的全部 10 项检查，所有项通过后再开始开发。
