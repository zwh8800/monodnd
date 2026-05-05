# 酒馆与命运，游戏引擎选型对比分析

> 文档版本：v2.0
> 更新日期：2026-05-05
> 编写目的：为《酒馆与命运》项目确定最优技术路线
> 核心变化：v1.0 推荐 Godot 4.x + GDScript，v2.0 基于 AI-First 开发范式重新评估，推荐 MonoGame + C#

---

## 目录

1. [项目需求摘要](#1-项目需求摘要)
2. [引擎总览对比表](#2-引擎总览对比表)
3. [各引擎详细分析](#3-各引擎详细分析)
   - [3.1 Godot 4.x](#31-godot-4x)
   - [3.2 Unity 6](#32-unity-6)
   - [3.3 GameMaker](#33-gamemaker)
   - [3.4 RPG Maker](#34-rpg-maker)
   - [3.5 MonoGame](#35-monogame)
   - [3.6 LibGDX](#36-libgdx)
   - [3.7 Pygame](#37-pygame)
   - [3.8 Cocos2d-x](#38-cocos2d-x)
   - [3.9 其他引擎简要说明](#39-其他引擎简要说明)
4. [最终推荐](#4-最终推荐)
5. [推荐技术栈](#5-推荐技术栈)
6. [AI-First开发范式影响分析](#6-ai-first开发范式影响分析)
7. [风险与备选方案](#7-风险与备选方案)

---

## 1. 项目需求摘要

### 1.1 项目定位

《酒馆与命运》是一款以 DND 5e 规则为核心的 **回合制像素风 Roguelike RPG**。玩家经营酒馆、招募冒险者、探索随机生成的地城、与 NPC 互动，剧情走向受玩家选择影响。核心特色是通过大语言模型（LLM）驱动叙事，实现"每一场冒险都是独一无二的故事"。

### 1.2 核心功能需求

| 需求维度 | 具体描述 | 优先级 |
|---------|---------|--------|
| **2D 像素渲染** | FF6 风格，角色 16x24 像素，Tileset 32x32 像素，支持动画、视差滚动、光影效果 | P0 |
| **回合制战斗** | 完整实现 DND 5e 规则（动作、附赠动作、反应、优势/劣势、豁免、法术位等） | P0 |
| **程序化地图生成** | 地牢/野外地图的随机生成，基于种子可复现，支持手动编辑 | P1 |
| **LLM 集成** | 调用大语言模型 API（OpenAI / 本地模型），解析 JSON 响应，驱动 NPC 对话和叙事 | P1 |
| **跨平台** | 优先 PC（Windows / macOS / Linux），后续覆盖移动端（iOS / Android） | P1 |
| **中文支持** | 像素风格中文字体渲染，文本排版，输入法兼容 | P1 |
| **低配性能** | 集成显卡可流畅运行，内存占用控制在 512MB 以内 | P2 |
| **本地持久化** | SQLite 存档，支持读档、多存档位、云存档 | P2 |

### 1.3 团队约束

- 小团队（1-3 人），成员技能以 **C# / Lua / JavaScript / C++** 为主，**不熟悉 GDScript**
- 核心开发范式为 **AI-First（Vibe Coding）**，依赖 AI 代码生成，需要编译期错误检测能力
- 预算有限，优先免费/开源方案
- 开发周期目标：6 个月产出可玩原型，12 个月发布正式版
- 需要快速迭代验证核心玩法，但更关注"AI 生成代码的质量可验证性"而非"手写代码的效率"

### 1.4 技术约束

- LLM 调用需要 HTTP 请求能力与 JSON 解析（C# 中 HttpClient + System.Text.Json 是标准方案）
- SQLite 是存档和数据库的首选方案（Microsoft.Data.Sqlite 或 sqlite-net）
- 像素字体需要引擎支持自定义位图字体或动态字体（FontStashSharp / BMFont 方案）
- 程序化地图生成需要引擎提供足够的底层 Tilemap 控制
- **编译期类型安全是硬性需求**：AI 生成代码必须有编译器作为第一道防线
- **跨域混淆风险必须可控**：AI 不应将其他框架的 API 混入目标代码

---

## 2. 引擎总览对比表

| 维度 | Godot 4.x | Unity 6 | GameMaker | RPG Maker MZ | MonoGame | LibGDX | Pygame | Cocos2d-x |
|------|-----------|---------|-----------|-------------|----------|--------|--------|-----------|
| **授权许可** | MIT 免费 | 商业许可（收入门槛） | 订阅制（Creator $4.99/月） | 商业付费（$79.99） | MIT 免费 | Apache 2.0 | LGPL | MIT 免费 |
| **价格** | 完全免费 | 年收入<$200K 免费 | 低门槛月费 | 一次性购买 | 免费 | 免费 | 免费 | 免费 |
| **2D 原生支持** | 原生 2D 管线 | 3D 引擎，2D 为附加 | 原生 2D | 原生 2D | 需自建管线 | 需自建管线 | 基础绘制 | 原生 2D |
| **像素渲染** | 极好（Nearest 零配置） | 好（需手动配置） | 极好（零配置） | 好（内置风格） | 手动实现但控制力强 | 手动实现 | 基础支持 | 好 |
| **Tilemap 系统** | 内置 TileMapLayer，强 | Tilemap + 2D Extras | 内置 Tile 系统 | 内置地图编辑器 | 无内置（MonoGame.Extended 可加载 Tiled） | 无内置（支持 TMX 加载） | 无内置 | 内置 |
| **D&D 5e 实现** | 需自建，GDScript 灵活 | 有社区 D&D SDK | 有限制，复杂规则吃力 | 几乎不可能 | 需完全自建（C# 强类型优势） | 需完全自建 | 需完全自建 | 较困难 |
| **LLM 集成** | HTTPRequest + 原生 JSON | UnityWebRequest + JSON | 扩展有限，GML 受限 | 不支持 | HttpClient + System.Text.Json | OkHttp 可用 | requests 库 | 需 C++ HTTP 库 |
| **SQLite** | godot-sqlite 插件 | System.Data.SQLite | 需 GML 扩展 | 不支持 | Microsoft.Data.Sqlite / sqlite-net | SQLite JDBC | sqlite3 标准库 | 需 C++ 插件 |
| **中文像素字体** | DynamicFont + 插件 | TMP + BMFont | 支持 GMS Font | 需插件 | FontStashSharp + Noto Sans CJK | Freetype 集成 | pygame.font | BMFont 支持 |
| **PC 支持** | 原生导出 | 原生导出 | 原生导出 | 原生导出 | 原生导出（.NET 跨平台） | 需打包 | 需打包 | 原生导出 |
| **移动端支持** | 导出+插件 | 成熟稳定 | 导出 | 导出（需插件） | 需额外工作 | 弱（iOS 问题） | 差 | 强项 |
| **安装包大小** | ~30MB | ~150MB+（含 Mono） | ~20MB | ~50MB | ~5MB（自打包） | ~5MB（自打包） | ~10MB（自打包） | ~15MB |
| **低配性能** | 极好 | 一般（3D 管线开销） | 极好 | 好 | 极好（裸金属） | 极好（Java 略高） | 一般（Python 慢） | 好 |
| **学习曲线** | 低（GDScript） | 中-高（C# + 生态） | 低（GML 可视化） | 最低（事件表） | 高（需底层知识） | 中-高（Java） | 低（Python） | 中（C++/JS） |
| **社区活跃度** | 快速增长中 | 最大 | 中型 | 中型 | 中小型 | 中型 | 大型（Python） | 衰退中 |
| **中文社区** | 活跃（B站/QQ群） | 极活跃 | 较小 | 小 | 极小 | 极小 | 一般 | 小 |
| **小团队适合度** | 好（手写代码场景） | 好 | 好 | 特定场景 | 一般（但 AI 工作流下反转） | 一般 | 原型可用 | 一般 |
| **AI代码生成适配度** | 低（GDScript 动态类型） | 高（C# + 海量训练数据） | 低（GML 小众） | 无（事件系统） | **极高**（C# + 小 API 面 + monogame-mcp） | 中（Java 数据充足） | 高（Python 数据最多） | 低（C++/Lua 数据少） |
| **编译期类型安全** | 无（GDScript 动态类型） | 高（C# dotnet build） | 低（GML 弱类型） | 无（事件驱动） | **极高**（C# + .NET 完整类型系统） | 高（Java 编译器） | 无（Python 运行时） | 高（C++ 编译） |
| **跨域混淆风险** | 高（GDScript 与 Python 混淆） | 低（自包含生态） | 低（GML 独特语法） | 低（非代码） | **极低**（XNA API 稳定唯一） | 中（Java + Android 混淆） | 低（特征明确） | 中（C++ 框架混淆） |

---

## 3. 各引擎详细分析

### 3.1 Godot 4.x

#### 概述

Godot 4.x 是一个完全免费开源的游戏引擎，采用 MIT 许可证。它从底层设计就是原生 2D 引擎（坐标系统使用整数像素，无 3D 管线开销），这在 2D 像素项目中有天然优势。

#### 2D 像素渲染

Godot 的 2D 管线天生适合像素艺术。`TextureFilter = Nearest` 让像素风格不需要任何额外配置，16x24 的角色精灵和 32x32 的 Tileset 直接在编辑器中精确显示。TileMapLayer 节点（4.x 新增）相比 3.x 大幅改进，支持分层 Tilemap、自动瓦片（AutoTiling）、随机瓦片（RandomTiler）和场景瓦片（Scene Tiler），对于程序化地图生成非常友好。

#### 回合制战斗与 D&D 5e

GDScript 是 Python 风格的高级脚本语言，语法简洁。实现 D&D 5e 的战斗逻辑，动作经济系统、优势/劣势骰子、法术位管理、状态效果，都可以用清晰的数据结构表达。但问题在于：GDScript 的动态类型意味着所有类型错误都在运行时暴露，无法通过编译器前置发现。

#### 程序化地图生成

Godot 的 TileMapLayer 支持运行时修改瓦片数据。用 FastNoiseLite 噪点图生成地形，再根据高度图分配瓦片类型，整个过程在 `_ready()` 里几百行代码完成。

#### LLM 集成

内置 `HTTPRequest` 节点，配合 `JSON.parse()` 和 `JSON.stringify()`，调用 OpenAI API 或其他 LLM 端点非常直接。社区插件 GodoLM 进一步封装了整个流程。这是 Godot 在同级别引擎中最突出的优势之一。

#### 中文支持

Godot 4.x 支持动态字体（DynamicFont），可以加载中文字体文件。文本渲染支持自动换行、富文本标签（BBCode），中文排版基本没问题。

#### SQLite

`godot-sqlite` 插件封装了 SQLite API，支持同步和异步查询。插件维护活跃，与 Godot 4.x 兼容。

#### AI/Vibe Coding 适配度

**评分：低**。这是 Godot 在本项目中的致命短板。

1. **动态类型陷阱**：GDScript 是动态类型语言，AI 生成的代码即使有语法错误、类型错误、空引用错误，都无法在编译期发现。实际用户经验表明："用 AI 开发 Godot 就会写出一堆甚至语法报错的代码而没有前期检查出来"。
2. **`:=` 类型推断问题**：`instantiate()` 返回的是 `Variant` 而非具体类型，AI 无法正确推断返回值的类型，导致链式调用的代码频繁报错。
3. **GDScript 训练数据有限**：相比 Python、JavaScript、C#，AI 在 GDScript 上的训练数据量少得多，生成质量不稳定。

#### Godot C# 补充分析

Godot 也支持 C#（通过 .NET 6+ 绑定），但存在更严重的问题：

1. **跨域混淆风险极高**：AI 训练数据中 Unity C# 的占比远大于 Godot C#。AI 会不自觉地把 `MonoBehaviour`、`Start()`、`Update()`、`GameObject.Find()` 等 Unity API 混入 Godot C# 代码中。
2. **编译通过但逻辑错误**：这些混淆代码往往能通过编译（因为两者都是 C#），但在 Godot 中运行时表现为完全不同的行为。**编译通过的逻辑错误比语法错误更难发现**。
3. **godogen 项目实证**：github.com/htdt/godogen 是一个 GDScript 到 C# 的迁移工具。实证数据显示，迁移后构建脚本缩减 14%（因 C# 编译捕获了部分错误），但仍有大量跨域混淆问题需要在运行时才能发现。

#### 小团队适合度（AI 工作流下）

对于手写代码的小团队，Godot 依然是优秀选择。但对于 AI-First 开发范式，"编辑器友好"的优势被"AI 生成代码质量不可控"的劣势抵消。当大部分代码由 AI 生成时，编译期检查的重要性远超编辑器的拖拽体验。

#### 主要风险（本项目视角）

- GDScript 动态类型无法在编译期捕获 AI 生成代码的错误
- Godot C# 存在严重跨域混淆风险（AI 混入 Unity API）
- `:=` 类型推断使 AI 生成的 `instantiate()` 调用链不稳定
- 编辑器拖拽工作流与 AI 代码生成范式不匹配

> **结论：不推荐。** 尽管 Godot 在 2D 像素和 LLM 集成上有优势，但其语言层面的类型安全问题对 AI-First 开发范式是致命的。GDScript 动态类型和 Godot C# 的跨域混淆风险，使编译器无法发挥应有的防护作用。

---

### 3.2 Unity 6

#### 概述

Unity 是市场占有率最高的游戏引擎之一。Unity 6 在 2D 工作流上做了很多改进，但它本质上仍然是 3D 引擎。

#### 2D 像素渲染

Unity 的 2D 渲染通过 SpriteRenderer 和 Tilemap 系统实现。要达到 Godot 那样的像素精确度，需要手动设置 Filter Mode 为 Point（No Filter），关闭抗锯齿，调整 PPU（Pixels Per Unit）。这些配置不复杂，但说明 2D 在 Unity 中是"配置出来的"而不是"默认的"。

#### 回合制战斗与 D&D 5e

C# 是强类型语言，适合实现复杂规则系统。社区有现成的 D&D 5e SDK，包括骰子引擎、规则解析、法术和怪物数据库。如果利用这些资产，能大幅缩短开发周期。

#### LLM 集成

`UnityWebRequest` 配合 `Newtonsoft.Json` 或 `System.Text.Json` 可以调用任何 HTTP API。C# 的异步模型（async/await）比 GDScript 的信号机制更现代。

#### AI/Vibe Coding 适配度

**评分：高**。C# 是 AI 训练数据最丰富的语言之一，编译期检查完善。但 Unity 的 API 面过大（超过 700 个命名空间），AI 经常生成已废弃的 API 调用或不推荐的模式。不过相比 Godot，至少编译能捕获大部分问题。

#### 跨平台

Unity 的跨平台覆盖最广，但包体偏大（~150MB 起步），许可条款的不确定性仍然让独立开发者担忧。

#### 小团队适合度

资产商店是一把双刃剑：可以买到现成的系统，但也会让项目陷入"资产堆砌"的泥潭。编辑器功能臃肿，对低配开发机不友好。

#### 主要风险

- "3D 引擎强做 2D"有不可避免的额外开销
- 包体大（~150MB），性能开销高
- 许可条款变化历史让人不安
- API 面过大，AI 易生成废弃 API 调用
- 编辑器对老机器不友好

> **结论：备选但不推荐首选。** Unity 的 C# 生态对 AI 开发友好，但包体过大、引擎臃肿、许可不确定性是减分项。对于像素风 2D 项目，Unity 的大多数 3D 功能是冗余的。

---

### 3.3 GameMaker

#### 概述

GameMaker 是经典的 2D 游戏开发工具，以快速原型和像素艺术支持著称。

#### 2D 像素渲染

GameMaker 在像素艺术领域地位独特。它的默认渲染管线对像素风格零配置，不需要调任何参数，16x24 的精灵就是精确的 16x24。如果只看 2D 像素渲染这一个维度，GameMaker 是所有引擎中体验最好的。

#### 回合制战斗与 D&D 5e

这是 GameMaker 的短板。GML（GameMaker Language）最初被设计为一种简单的脚本语言，要实现 D&D 5e 那样复杂的状态机和规则系统，代码会变得相当混乱。缺乏强类型系统意味着大量运行时错误需要手动排查。

#### AI/Vibe Coding 适配度

**评分：低**。GML 是非常小众的语言，AI 训练数据极少。AI 生成的 GML 代码质量不可靠，且 GML 缺乏编译期类型检查，无法在前期捕获 AI 生成的错误。LLM 集成也需要依赖第三方 GML 扩展，生态不成熟。

#### 主要风险

- GML 技能无法迁移到其他引擎
- 复杂 RPG 系统的代码组织困难
- SQLite 和 LLM 集成依赖第三方扩展
- AI 训练数据极少，代码生成质量低

> **结论：不推荐。** GML 的类型系统和 AI 生态都不足以支撑 AI-First 开发。

---

### 3.4 RPG Maker

#### 概述

RPG Maker 是一个专注于 JRPG 制作的引擎系列，最新的 MZ 版本拥有最完善的功能集。

#### 2D 像素渲染

RPG Maker 默认使用 48x48 的网格系统（MZ 版本），这与我们的 32x32 Tileset 和 16x24 角色尺寸有冲突。虽然可以通过插件修改，但定制程度受限。

#### 回合制战斗与 D&D 5e

这是 RPG Maker 的最大局限。它的战斗系统是硬编码的 JRPG 回合制。D&D 5e 的动作经济、先攻排序、优势/劣势、借机攻击，这些概念几乎不可能用 RPG Maker 的事件系统实现。

#### AI/Vibe Coding 适配度

**评分：无**。RPG Maker 的核心工作流是事件系统和可视化编辑，不是代码驱动。AI 代码生成在这个平台上没有用武之地。LLM 集成完全不支持，程序化地图生成也没有 API。

> **结论：强烈不推荐。** 定制性极差，D&D 5e 规则几乎不可能实现，LLM 集成不成熟。

---

### 3.5 MonoGame

#### 概述

MonoGame 是微软 XNA 框架的开源继承者（MIT 许可证），提供跨平台的底层游戏开发框架。它不是"引擎"而是"框架"，没有编辑器，一切通过 C# 代码构建。这恰好与 AI-First 开发范式高度契合。

#### 2D 像素渲染

MonoGame 给你绝对的控制权。你可以精确控制每个像素的渲染方式，纹理过滤、SpriteBatch 的排序模式、渲染目标的管理都在你的手中。对于像素艺术，这意味着你可以做到理论上最精确的渲染。`SpriteBatch` 配合 `SamplerState.PointClamp` 实现像素风格的 Nearest 过滤，每一帧你可以决定画什么、画在哪、怎么画。

#### 回合制战斗与 D&D 5e

C# 是强类型语言，适合实现复杂规则。你可以设计干净的数据结构（`record`、`enum`、`interface`、泛型约束）来表达 D&D 5e 的整个规则系统。编译器会在你运行之前捕获类型错误、空引用、switch 遗漏分支等问题。这在 AI 生成代码的场景下是巨大的优势，AI 写出的 `DiceRoller` 类如果有类型错误，`dotnet build` 会直接报错。

#### 程序化地图生成

没有内置 Tilemap 系统，但 `MonoGame.Extended` 提供了 Tiled 地图文件（.tmx）的加载和渲染支持。对于程序化生成，你可以配合 GoRogue 的地图生成算法（BSP 房间、洞穴生成、迷宫生成）生成瓦片数据，然后通过自定义的 `TilemapRenderer` 渲染。

#### LLM 集成

`HttpClient` 和 `System.Text.Json` 是 .NET 标准库的一部分，不需要任何第三方依赖。C# 的 `async/await` 模型天然适合异步 API 调用。你可以构建一个干净的 `LLMGateway` 抽象层，与 Godot 方案中的架构设计思路完全一致，只是底层实现从 `HTTPRequest` 换成了 `HttpClient`。

#### SQLite

`Microsoft.Data.Sqlite` 是微软官方的 SQLite 提供程序，质量可靠。`sqlite-net` 是轻量级 ORM，PCL 兼容。作为标准的 .NET 项目，引入 NuGet 包没有任何障碍。

#### 中文支持

`FontStashSharp` 是 MonoGame 社区推荐的动态字体渲染库，支持从 .ttf 文件动态生成像素风格的位图字体。配合 `Noto Sans CJK`（谷歌的开源中日韩字体），可以很好地实现中文渲染。

#### AI/Vibe Coding 适配度

**评分：极高**。MonoGame 在所有被评估的引擎中，AI 代码生成适配度最高，理由如下：

1. **小 API 面**：MonoGame 的核心 API 非常精简（`GraphicsDevice`、`SpriteBatch`、`ContentManager`、`Game` 基类），AI 模型很容易学习和记忆。不像 Unity 有 700+ 命名空间，MonoGame 的 API 可以在几页文档中列完。
2. **XNA API 稳定性**：API 自 2006 年以来基本稳定，AI 训练数据中不存在"新旧 API 混用"问题。AI 不会像在 Unity 中那样生成已废弃的 `Application.LoadLevel()`。
3. **零跨域混淆**：XNA API 是独特且封闭的，没有 `MonoBehaviour`、没有 `Start()`/`Update()` 约定、没有 `GameObject`。AI 不可能把 Unity API 混入 MonoGame 代码，因为两者在命名空间和类名上没有任何交集。
4. **monogame-mcp**：社区开发的 MCP（Model Context Protocol）工具，让 AI 助手可以直接操作 MonoGame 项目，创建 Content.mgcb、添加 NuGet 包、生成 SpriteBatch 样板代码。这在其他引擎中没有等效工具。
5. **代码即场景**：没有可视化编辑器，所有游戏对象通过 C# 代码创建。AI 最擅长的事情就是生成代码，你不需要 AI 去操作一个 GUI 编辑器，只需要 AI 生成 C# 代码然后 `dotnet build`。

#### 跨平台

MonoGame 支持 Windows、macOS、Linux、Android、iOS、PS4、Xbox 等。平台覆盖与 Godot 一样广。作为 .NET 项目，天然支持跨平台编译。

#### 学习曲线

**高**。你需要理解游戏循环、图形管线、内容管线、跨平台编译等概念。但这对本团队来说不是主要障碍，团队熟悉 C#/C++/JS，有底层开发经验。更高的学习曲线换来的是更底层的控制力和更精准的 AI 代码生成。

#### 小团队适合度（AI 工作流下）

传统观念认为 MonoGame 不适合小团队，因为没有编辑器、需要自建工具链。但在 AI-First 开发范式下，这个判断需要重新审视：

- **传统观点**：没有编辑器 = 效率低
- **AI 视角**：没有编辑器 = 所有工作都是代码 = AI 可以参与所有工作
- **传统观点**：需要自建管线 = 工作量大
- **AI 视角**：AI 生成管线代码 + 编译器验证 = 不自建也能快速迭代

对于手写代码团队，MonoGame 确实比 Godot 慢。但对于 AI 辅助团队，MonoGame 的"全代码"特性使 AI 能覆盖 100% 的开发工作，而不仅仅局限于脚本层面。

#### 主要风险

- 没有编辑器，传统开发效率低
- 社区较小，中文资料极少
- 需要自建 UI 系统和场景管理（可通过 Nez、Myra 等库缓解）
- 调试和迭代速度依赖于 AI 工具的成熟度

---

### 3.6 LibGDX

#### 概述

LibGDX 是 Java 生态中最成熟的跨平台游戏开发框架。设计理念与 MonoGame 类似：提供底层抽象，不提供编辑器。

#### 2D 像素渲染

LibGDX 的 SpriteBatch 提供了高效的 2D 渲染，纹理过滤设置为 Nearest 即可实现像素风格。通过 `TiledMapRenderer` 直接加载 Tiled 编辑器制作的 .tmx 地图文件，这比 MonoGame 方便。

#### 回合制战斗与 D&D 5e

Java 的强类型和面向对象特性适合构建复杂规则系统。但与 MonoGame 一样，不提供游戏逻辑框架，需要自己搭建一切。

#### AI/Vibe Coding 适配度

**评分：中**。Java 是 AI 训练数据充足的编程语言，但 LibGDX 的游戏专用 API 在训练数据中的占比不大。AI 可能会将 Android SDK API 混入 LibGDX 代码（两者都是 Java，命名空间不同但容易混淆）。iOS 支持因 RoboVM 停维护而处于不确定状态。

#### 主要风险

- iOS 支持不完善（RoboVM 停维护）
- Java 与 Android SDK 的跨域混淆风险
- 中文社区极小

> **结论：备选但不推荐首选。** iOS 支持问题是硬伤，Java 的跨域混淆风险高于 MonoGame 的 C#。

---

### 3.7 Pygame

#### 概述

Pygame 是 Python 生态中最流行的游戏开发库，基于 SDL2。定位是"学习工具和快速原型"，而不是"专业游戏引擎"。

#### 2D 像素渲染

Pygame 提供基本的 blit 操作，可以精确控制像素。像素艺术在 Pygame 中实现没有技术障碍，但也缺乏任何高级特性，没有 Tilemap 系统、没有动画系统、没有场景管理。

#### 回合制战斗与 D&D 5e

Python 是表达力很强的语言，实现 D&D 5e 的规则引擎在代码层面非常愉快。但 Pygame 只管显示和输入，所有游戏逻辑（状态管理、事件队列、UI 导航）都要你自己实现。

#### AI/Vibe Coding 适配度

**评分：高**。Python 是 AI 训练数据最丰富的语言，没有之一。但 Pygame 的致命问题是：**不支持移动端**，违反项目的跨平台需求。另外 Python 没有编译步骤，AI 生成的代码错误只能在运行时暴露，这与项目的"编译期检测"核心需求冲突。

#### 主要风险

- **不支持移动端**，违反项目跨平台需求
- Python 性能瓶颈，复杂场景会卡顿
- 没有编译器捕获 AI 错误
- **推荐仅用于原型验证**，不适合作为正式产品的技术底座

> **结论：推荐用于原型验证，不推荐作为正式开发引擎。**

---

### 3.8 Cocos2d-x

#### 概述

Cocos2d-x 曾经是移动游戏开发的热门选择，国内 2D 手游时代大量产品基于此引擎。如今社区活跃度已经大幅下降。

#### 2D 像素渲染

Cocos2d-x 的 2D 渲染能力扎实，Sprite、TileMap、Animation 系统完整。像素艺术支持通过设置纹理过滤参数实现。

#### 回合制战斗与 D&D 5e

C++ 开发效率低（如果用原生 C++ API）。也支持 Lua 和 JS 绑定，但绑定层的维护在各个版本中不一致。

#### AI/Vibe Coding 适配度

**评分：低**。C++ 在 AI 训练数据中占比不低，但 Cocos2d-x 的特定 API 数据有限。AI 生成的 C++ 代码容易混入其他框架（Qt、SFML、SDL2）的风格。社区衰退意味着即使 AI 生成了不错的代码，你也不容易找到同类项目参考。

#### 主要风险

- 社区衰退，维护停滞
- 新平台兼容性不确定
- 中文社区也在萎缩

> **结论：不推荐。** 社区衰退是不可逆的趋势，新平台支持将成为持续痛点。

---

### 3.9 其他引擎简要说明

| 引擎 | 说明 |
|------|------|
| **Unreal Engine** | 3A 级引擎，2D 像素项目如同用牛刀杀鸡。蓝图系统虽然强大但不适合 2D 像素 + AI 代码生成。不考虑。 |
| **Ren'Py** | 视觉小说引擎，适合 AVG 类型。战斗系统和地图探索需要大量 hack。AI 适配度低。不考虑。 |
| **Solar2D** | 2D 引擎，Lua 脚本。社区小，中文资料少，LLM 集成不成熟。不考虑。 |
| **Defold** | 免费 2D 引擎，Lua 脚本。引擎设计偏手游广告游戏，复杂 RPG 支持有限。备选但不推荐。 |
| **Bevy** | Rust 游戏引擎，ECS 架构。很有潜力但生态不成熟（2026 年尚未到 1.0）。短期不适用。 |
| **FNA** | MonoGame 的"精确重实现"分支，更贴近 XNA 4.0 Refresh API。可作为 MonoGame 的替代，但生态略小。 |

---

## 4. 最终推荐

### 4.1 核心推荐：MonoGame + C#

综合所有维度评估，特别是在 **AI-First 开发范式** 的框架下重新审视各引擎，**MonoGame + C# 是《酒馆与命运》项目的最优选择**。

#### 为什么不是 Godot GDScript

GDScript 是 Godot 的原生脚本语言，语法简洁、上手快，但它有三个致命问题：

1. **动态类型，AI 代码错误无法编译期发现**：GDScript 没有编译步骤，所有类型错误、空引用、参数错误都在运行时暴露。当 AI 生成大量代码时，这意味着每次运行都可能遇到新的运行时错误，调试成本极高。用户的实际经验验证了这一点："用 AI 开发 Godot 就会写出一堆甚至语法报错的代码而没有前期检查出来"。
2. **`:=` 类型推断陷阱**：GDScript 的 `:=` 操作符进行类型推断，但 `instantiate()` 返回的是 `Variant` 而非具体类型。AI 生成的代码中，`var enemy := preload("res://enemy.tscn").instantiate()` 的 `enemy` 是 `Variant`，后续调用 `enemy.take_damage(10)` 在编译期完全无法验证。
3. **GDScript 训练数据稀缺**：相比 C#、Python、JavaScript，AI 在 GDScript 上的训练数据量差距在一到两个数量级。AI 生成的 GDScript 代码质量不稳定，而且很多"看起来对"的代码在运行时表现异常。

#### 为什么不是 Godot C#

Godot 支持 C#（通过 .NET 绑定），但带来了一个更隐蔽的问题：

1. **跨域混淆风险极高**：AI 训练数据中 Unity C# 的占比远大于 Godot C#。当 AI 被要求"用 C# 写 Godot 代码"时，它经常生成 `MonoBehaviour`、`Start()`、`Update()`、`GameObject.Find()` 等 Unity API 调用。这些代码在 Godot 中**编译能通过**（两者都是 C# 编译器），但运行时行为完全不同。**编译通过的逻辑错误比语法错误更危险**，你不会立刻发现问题，直到某个功能在游戏中表现异常。
2. **godogen 项目实证**：github.com/htdt/godogen 是 GDScript 到 C# 的迁移工具。数据显示，迁移后构建脚本缩减了 14%（编译器捕获了部分 GDScript 的运行时错误），但跨域混淆问题仍然存在。即使经过专门的迁移工具处理，AI 仍然会在 Godot C# 中混入 Unity 代码。
3. **生态分裂**：Godot 的 C# 绑定相比 GDScript 是二等公民，部分新特性（如某些编辑器插件）不支持 C#，文档以 GDScript 为主，社区资源也集中在 GDScript 上。

#### 为什么不是 Cocos Creator TypeScript

Cocos Creator 使用 TypeScript，看起来有类型系统，但问题在于：

1. **TypeScript 的类型检查是可选的**：`tsc --noEmit` 不是默认行为，很多 Cocos Creator 项目甚至不配置类型检查。核心诉求"编译期捕获 AI 错误"在这里不成立。
2. **社区正在衰退**：Cocos Creator 的主要市场在中国，但近年用户量持续下降。AI 训练数据中 Cocos Creator 的占比很小。
3. **Cocos Creator 的编辑器依赖度高**：场景、动画、UI 都依赖编辑器配置，AI 无法有效参与这些工作。

#### 为什么选择 MonoGame + C#

以下是 8 个核心理由，按重要性排列：

**1. 编译期类型安全（dotnet build 作为第一道防线）**

当 AI 生成 C# 代码后，运行 `dotnet build` 可以捕获以下错误：
- 语法错误（括号不匹配、缺少分号等）
- 类型错误（将 `string` 传给 `int` 参数）
- 空引用风险（Nullable Reference Types 警告）
- 未定义的方法或属性
- switch 表达式未穷尽所有分支
- 泛型约束违反

这意味着 AI 生成的代码有 **机械化的质量门禁**。比对 Godot GDScript（每次运行才能发现错误），MonoGame + C# 的工作流是：AI 生成代码 → `dotnet build` → 编译器指出错误 → AI 修复 → 循环直到构建通过。这大幅缩短了"AI 生成 → 验证 → 修复"的反馈周期。

**2. 零跨域混淆风险**

XNA API 自 2006 年以来保持了高度稳定，而且它是一个封闭、独特的 API 体系：
- 没有 `MonoBehaviour`（有 `Game` 基类）
- 没有 `Start()`/`Update()` 约定（有 `Initialize()`/`LoadContent()`/`Update()`/`Draw()` 重写）
- 没有 `GameObject`（有 `Entity` 组件，如果使用 Nez）
- 没有 `Transform`（有 `Transform2D`，语义不同）

AI 训练数据中不存在与 XNA API 混淆的对象。当 AI 被要求写 MonoGame 代码时，它只能生成 MonoGame 风格代码。这与 Godot C# 形成鲜明对比，Godot C# 的 API 风格与 Unity 太相似，导致 AI 持续混淆。

**3. C# 语言熟悉度**

团队成员熟悉 C# / Lua / JavaScript / C++。C# 是团队核心技能之一，不需要额外学习 GDScript 或 GML。C# 本身也是一流语言，泛型、LINQ、async/await、record、模式匹配，所有这些特性在构建复杂的 D&D 5e 规则系统时都非常有用。

**4. 成熟的 Roguelike 生态**

MonoGame 生态中有一套专门为 Roguelike 游戏设计的库：
- **GoRogue**：FOV（视野计算）、寻路（A*）、地图生成（BSP 房间、洞穴、迷宫）、SenseMap（感知地图）。这些是 Roguelike 游戏的核心基础设施。
- **RogueSharp**：备选的 Roguelike 库，提供 FOV 和寻路。
- 这些库在 Godot 生态中没有直接等效物（Godot 需要自己实现或移植）。

**5. monogame-mcp：AI 开发专用工具**

monogame-mcp（Model Context Protocol）是一个专门为 AI 助手设计的工具，让 AI 可以直接操作 MonoGame 项目：
- 创建和修改 `.csproj` 项目文件
- 管理 `Content.mgcb` 内容管线配置
- 添加和移除 NuGet 包
- 生成 SpriteBatch 渲染样板代码
- 配置跨平台编译参数

这是其他引擎没有的 AI 工具。它让 AI 不仅能"写代码"，还能"管理项目"。

**6. 商业验证**

MonoGame 有成功的商业案例。**8-Bit Adventures 2** 是一款使用 MonoGame 开发的商业像素风 RPG，在 Steam 上获得了好评。这证明 MonoGame 可以支撑完整的商业 RPG 项目。其他成功的 MonoGame 商业作品包括：
- **Streets of Rage 4**（部分使用）
- **Celeste**（最初原型使用 XNA/MonoGame）
- **Stardew Valley**（最初使用 XNA，后迁移）

**7. 代码即场景，完美匹配 AI 工作流**

MonoGame 没有可视化编辑器。所有游戏对象通过 C# 代码创建和组合：
- 场景通过 `Scene` 类创建
- 实体通过 `Entity` 类实例化（如果使用 Nez）
- 精灵通过 `SpriteBatch.Draw()` 渲染
- UI 通过代码布局

这意味着 AI 可以参与游戏开发的**所有环节**，不是因为 AI 擅长操作 GUI 编辑器（它不擅长），而是因为所有工作都是写代码（AI 擅长的事情）。

在 Godot 中，AI 只能生成 GDScript 脚本，但场景布局、节点连接、信号绑定、Tilemap 绘制都需要手动在编辑器中完成。在 MonoGame 中，所有这些都可以通过代码完成，AI 可以全流程参与。

**8. .NET 生态**

.NET 8+ 提供了现代化的开发体验：
- **NuGet 包管理**：超过 30 万个包
- **System.Text.Json**：高性能 JSON 解析与序列化
- **HttpClient**：完整的 HTTP 客户端
- **LINQ**：数据查询与变换
- **Microsoft.Data.Sqlite**：SQLite 提供程序
- **Nullalbe Reference Types**：编译期空引用检查
- **Source Generators**：编译时代码生成
- **AOT 编译**：.NET 8 支持 Native AOT，生成更小的原生二进制文件

这些在 Godot 中要么需要插件（godot-sqlite），要么需要自己封装（HTTPRequest 虽然内置但功能有限），要么不存在（LINQ、Source Generators）。

### 4.2 备选方案

| 场景 | 备选引擎 |
|------|---------|
| MonoGame 生态不够用，需要更成熟的引擎支持 | **Godot C#**，需接受跨域混淆风险，配合严格的代码审查流程 |
| 团队扩张到 5+ 人，需要标准化工作流 | **Unity 6**，需接受包体大、许可不确定性的代价 |
| 只需要验证核心玩法的可行性 | **Pygame**，快速原型，但不作为正式开发引擎 |
| 对编译期类型安全要求降低，更看重开发效率 | **Godot GDScript**，2D 像素体验最好，但 AI 代码质量不可控 |

### 4.3 不推荐方案

| 引擎 | 不推荐理由 |
|------|-----------|
| Godot GDScript | 动态类型 + AI 代码错误无法编译期发现，与 AI-First 范式冲突 |
| Godot C# | 跨域混淆风险极高（AI 混入 Unity API），编译通过但逻辑错误 |
| GameMaker | GML 语言天花板低，AI 训练数据极少 |
| RPG Maker | D&D 5e 规则几乎不可能实现，不支持代码驱动 |
| LibGDX | iOS 支持不完善，Java + Android 跨域混淆 |
| Pygame | 不支持移动端，性能有上限，无编译期检查 |
| Cocos2d-x | 社区衰退，维护停滞 |

---

## 5. 推荐技术栈

### 5.1 核心技术栈

| 层级 | 技术选型 | 说明 |
|------|---------|------|
| **游戏框架** | MonoGame 3.8.5+ | C# / .NET 8+，XNA 继承者，MIT 许可证 |
| **场景/实体** | Nez | Scene / Entity / Component 架构，内置 FSM / 行为树 / GOAP |
| **地图系统** | MonoGame.Extended 6.0 | Tiled / LDtk 地图加载与渲染，Input 管理，Camera 辅助 |
| **Roguelike 核心** | GoRogue | FOV 视野计算、A* 寻路、地图生成（BSP/洞穴/迷宫）、SenseMap |
| **UI 系统** | Myra | 像素风 UI，支持主题 / 布局 / 数据绑定 / 事件处理 |
| **数据持久化** | sqlite-net | 轻量 ORM，PCL 兼容，支持异步查询 |
| **LLM 集成** | HttpClient + System.Text.Json | 标准 HTTP + JSON，无第三方依赖 |
| **字体渲染** | FontStashSharp + Noto Sans CJK | 动态字体渲染，中文像素风支持 |
| **AI 开发工具** | monogame-mcp | MCP 协议，AI 助手直接操作 MonoGame 项目 |
| **内容管线** | MGCB (MonoGame Content Pipeline) | 纹理 / 音效 / 字体编译管理 |

### 5.2 工具链

| 用途 | 工具 | 说明 |
|------|------|------|
| **像素美术** | Aseprite | 精灵绘制、逐帧动画、像素字体生成 |
| **地图编辑** | Tiled | TMX 格式地图编辑，通过 MonoGame.Extended 加载 |
| **音效** | Bosca Ceoil / LMMS | 芯片音乐制作，像素风音效 |
| **版本控制** | Git + GitHub | .csproj 和 .cs 文件天然可 diff |
| **项目管理** | GitHub Projects / Notion | 任务跟踪、设计文档 |
| **LLM 模型** | 首选 OpenAI GPT-4o-mini（经济） | 备选通义千问 / DeepSeek，本地 Ollama + 小模型 |
| **IDE** | Visual Studio / Rider / VS Code | C# 开发环境，LSP 支持完善 |
| **构建工具** | dotnet CLI | `dotnet build` / `dotnet test` / `dotnet publish` |
| **AI 编码助手** | opencode + monogame-mcp | AI-First 开发工作流核心工具链 |

### 5.3 NuGet 包清单

| 包名 | 用途 | 必要性 |
|------|------|:------:|
| **MonoGame.Content.Builder.Task** | 内容管线 MSBuild 集成 | 必要 |
| **MonoGame.Extended** | Tiled 地图加载、Camera、Input、扩展 | 推荐 |
| **Nez** | ECS 架构、Scene 管理、FSM、行为树 | 推荐 |
| **GoRogue** | FOV、寻路、地图生成 | 推荐 |
| **Myra** | UI 系统 | 推荐 |
| **sqlite-net** | SQLite ORM | 推荐 |
| **Newtonsoft.Json** / **System.Text.Json** | JSON 序列化 | 必要 |
| **FontStashSharp.MonoGame** | 动态字体渲染 | 推荐 |
| **NLog** / **Serilog** | 结构化日志 | 可选 |
| **xunit** + **FluentAssertions** | 单元测试 | 必要 |

### 5.4 项目结构

```
dnd/
├── DndGame/                    # 主项目
│   ├── Content/                 # 原始资源
│   │   ├── textures/            # 纹理 (.png)
│   │   ├── fonts/               # 字体文件 (.ttf / .spritefont)
│   │   ├── audio/               # 音效 (.wav / .ogg)
│   │   ├── maps/                # Tiled 地图 (.tmx)
│   │   └── Content.mgcb         # MonoGame 内容管线配置
│   ├── Core/                    # 引擎核心
│   │   ├── GameRoot.cs          # Game 主类
│   │   ├── SceneManager.cs      # 场景切换
│   │   └── ServiceLocator.cs    # 服务注册
│   ├── Scenes/                  # 游戏场景
│   │   ├── MainMenuScene.cs
│   │   ├── TavernScene.cs
│   │   ├── AdventureScene.cs
│   │   └── CombatScene.cs
│   ├── Systems/                 # 游戏系统
│   │   ├── Combat/              # 战斗引擎
│   │   ├── Character/           # 角色系统
│   │   ├── Tavern/              # 酒馆系统
│   │   ├── Adventure/           # 冒险系统
│   │   ├── Settlement/          # 结算系统
│   │   └── WorldState/          # 世界状态
│   ├── Gateway/                 # LLM Gateway
│   │   ├── LLMGateway.cs        # 网关主类
│   │   ├── Agents/              # Agent 定义
│   │   ├── Validation/          # Schema 验证
│   │   ├── Cache/               # 语义缓存
│   │   └── Fallback/            # 离线降级
│   ├── Entities/                # 游戏实体
│   │   ├── Character.cs
│   │   ├── Enemy.cs
│   │   ├── Item.cs
│   │   └── NPC.cs
│   ├── UI/                      # UI 组件 (Myra)
│   │   ├── CombatUI.cs
│   │   ├── TavernUI.cs
│   │   ├── CharacterPanel.cs
│   │   └── DialogueUI.cs
│   └── Program.cs               # 入口
├── Data/                        # 数据文件
│   ├── schemas/                 # JSON Schema 定义
│   ├── templates/               # 离线模板
│   │   ├── adventures/
│   │   ├── narratives/
│   │   └── descriptions/
│   ├── config/                  # 配置表 (JSON)
│   │   ├── races.json
│   │   ├── classes.json
│   │   ├── spells.json
│   │   └── monsters.json
│   └── database/                # SQLite 运行时数据
├── DndGame.Tests/               # 测试项目
│   ├── Systems/
│   │   ├── CombatEngineTests.cs
│   │   ├── DiceRollerTests.cs
│   │   ├── CharacterSystemTests.cs
│   │   └── AdventureSystemTests.cs
│   └── Gateway/
│       ├── SchemaValidatorTests.cs
│       └── CacheManagerTests.cs
├── docs/                        # 设计文档
├── DndGame.sln                  # 解决方案文件
└── README.md
```

### 5.5 架构注意事项

- **数据驱动设计**：D&D 5e 的规则（法术、怪物、职业）尽量配置在 JSON 文件中，避免硬编码。这符合 AI-First 范式，AI 生成数据比 AI 生成代码更容易验证。
- **模块化架构**：战斗引擎、地图生成、对话系统之间通过接口（interface）解耦，方便单元测试。
- **LLM 抽象层**：在 LLM 调用和游戏逻辑之间加一层抽象，方便切换模型或降级到本地规则。
- **编译器即验证器**：利用 C# 的强类型特性，将游戏数据建模为 `record` 类型，让编译器验证数据结构的正确性。

---

## 6. AI-First 开发范式影响分析

### 6.1 背景：为什么需要重新评估引擎选型

传统的游戏引擎选型框架关注的是：渲染能力、编辑器体验、社区规模、学习成本、跨平台支持。这些维度在"开发者手写代码"的假设下成立。

但《酒馆与命运》项目采用 **AI-First（Vibe Coding）** 开发范式，大部分代码由 AI 生成，开发者负责：需求描述、代码审查、架构决策、AI 纠错。这改变了引擎选型的核心权重：

| 传统权重 | AI-First 权重 |
|---------|--------------|
| 编辑器功能 > 语言类型安全 | 编译期类型安全 > 编辑器功能 |
| 社区规模 > API 稳定性 | API 稳定性 + 小 API 面 > 社区规模 |
| 2D 渲染开箱即用 > 代码控制力 | AI 代码生成适配度 > 渲染开箱即用 |
| 可视化工作流 > 代码驱动 | 代码驱动 > 可视化工作流 |

### 6.2 拖拽式编辑器 vs 代码即场景

传统引擎（Godot、Unity、GameMaker）依赖可视化编辑器来创建场景、连接节点、配置属性。这对人类开发者是高效的，但对 AI 是低效的，AI 无法"拖拽"节点，无法"点击"按钮。

MonoGame 的"代码即场景"模式意味着：
- AI 可以通过生成 C# 代码来创建任何游戏对象
- AI 可以修改所有游戏参数（通过代码而非编辑器属性面板）
- AI 可以管理资源加载（通过 `ContentManager` 的代码调用）
- 开发者可以通过 `dotnet build` 验证 AI 生成代码的正确性

这不是 MonoGame 的缺点，而是它在 AI-First 范式下的核心竞争力。

### 6.3 编译器即第一道防线

在 AI-First 工作流中，编译器承担了最重要的质量门禁角色：

```
工作流对比：

Godot GDScript:
  AI 生成代码 → 运行游戏 → 运行时错误 → AI 修复 → 再次运行
  ↑ 反馈周期：30秒-2分钟（取决于游戏启动时间）
  ↑ 错误类型：运行时崩溃、空引用、类型不匹配

MonoGame C#:
  AI 生成代码 → dotnet build → 编译错误 → AI 修复 → 构建通过
  ↑ 反馈周期：2-5秒
  ↑ 错误类型：语法错误、类型错误、空引用警告、未定义引用
```

编译器的优势不只是反馈速度，还有错误的确定性。运行时错误可能不总是复现（取决于游戏状态），但编译错误是确定的，同样的代码永远产生同样的错误。

### 6.4 LSP 作为第二道防线

在开发者编写代码和审查 AI 代码时，LSP（Language Server Protocol）提供实时反馈：
- **类型提示**：悬停查看变量类型、方法签名
- **内联错误**：编写过程中即时标记语法和类型错误
- **自动补全**：提示可用的 API 调用
- **引用查找**：快速跳转到定义和引用

C# 的 LSP 实现（Roslyn）是业界最成熟的之一。VS Code + C# Dev Kit 或 Rider 都提供一流的 LSP 体验。这意味着在 AI 生成代码之前，开发者就能通过 LSP 预览代码质量。

### 6.5 单元测试作为第三道防线

即使编译器验证通过，AI 生成代码的逻辑仍可能出错。单元测试提供了第三层防护：
- `dotnet test` 运行所有测试
- 战斗引擎的骰子概率、伤害计算、规则逻辑都可以有对应的测试用例
- AI 修改代码后，运行测试确认核心逻辑未被破坏
- 测试本身也可以由 AI 生成（开发者定义行为契约，AI 生成测试实现）

在 MonoGame 中，由于游戏逻辑与渲染逻辑分离（Nez 的 ECS 架构天然支持），核心系统（战斗引擎、角色系统、冒险系统）可以脱离 MonoGame 上下文进行纯逻辑测试。

### 6.6 数据驱动设计

AI-First 开发范式的另一个重要原则是 **数据驱动设计**：

```
传统方式：
  硬编码法术数据 → AI 修改代码 → 风险：改错逻辑

数据驱动方式：
  JSON 配置法术数据 → AI 修改 JSON → 风险低：JSON Schema 验证
  → 游戏加载 JSON 实例化法术 → 编译器验证序列化类型
```

数据驱动设计在 MonoGame + C# 中的优势：
- **record 类型**：`public record Spell(string Id, string Name, int Level, SpellSchool School, DamageData Damage)`，编译器确保数据类型正确
- **System.Text.Json 反序列化**：JSON 直接映射为强类型对象，类型不匹配编译报错
- **JSON Schema 验证**：AI 生成的配置数据通过 Schema 验证，格式错误在加载时即被捕获
- **数据与逻辑分离**：AI 生成数据配置，开发者审查逻辑代码

### 6.7 渐进式类型安全

在 AI-First 范式中，类型安全是一个渐进的过程：

```
从松散到严格的类型安全链：

runtime (Python/GDScript)
  → compile-time types (C#/Java)
    → nullable reference types (C# 8+)
      → discriminated unions (C# pattern matching)
        → JSON Schema validation (data boundary)
          → property-based testing (fuzz testing)
```

MonoGame + C# 让项目可以占据链上的"编译期类型检查"到"JSON Schema 验证"区间，这是 AI 代码质量保证的最佳实践区间。再往上的 fuzz testing 和正式验证对游戏项目过于昂贵，再往下的纯运行时检查对 AI 生成代码不够安全。

### 6.8 对引擎选型的启示

基于 AI-First 范式的分析，引擎选型的核心结论是：

1. **语言能力 > 编辑器能力**：选择有强类型系统、编译期检查的语言，而非编辑器的拖拽便利性
2. **API 稳定性 > API 丰富度**：小且稳定的 API 面比大而全但混乱的 API 更适合 AI 生成
3. **代码驱动 > 可视化驱动**：所有游戏对象和数据通过代码管理，AI 才能覆盖全部开发环节
4. **测试可自动化 > 调试可交互**：编译 + 单元测试的自动化防线优于手动运行游戏的调试方式
5. **.NET 标准库 > 引擎内置工具**：HttpClient、System.Text.Json 等标准库比引擎的专有 API 更可靠

---

## 7. 风险与备选方案

### 7.1 MonoGame 风险对冲

| 风险 | 概率 | 影响 | 缓解措施 |
|:----:|:----:|:----:|----------|
| **无编辑器，开发效率低** | 高 | 高 | 采用 Nez ECS 架构减少样板代码；monogame-mcp 辅助 AI 开发；数据驱动设计减少手写代码量 |
| **社区较小，中文资料极少** | 高 | 中 | 主要依赖官方文档（英文）和 GitHub 样例；通过 AI 生成代码而非查找社区教程；关键问题在 GitHub Issues 中追踪 |
| **UI 系统需要自建** | 中 | 高 | 使用 Myra 作为 UI 框架，减少手写 UI 代码；Myra 支持主题系统，像素风 UI 开箱即用 |
| **移动端适配工作量大** | 中 | 中 | 开发早期建立跨平台测试流程；MonoGame 的 `ViewportAdapter` 处理分辨率适配；移动端触摸输入通过 MonoGame.Extended Input 处理 |
| **学习曲线高** | 中 | 低 | 团队有 C#/C++/JS 经验，底层开发不是障碍；AI 辅助降低初始学习成本 |
| **MonoGame 更新缓慢** | 低 | 中 | 锁定主版本号（3.8.5+），核心 API 自 XNA 以来基本稳定，版本变动影响小 |
| **GoRogue 或 Nez 停维护** | 低 | 高 | 核心系统（战斗引擎、角色系统）不依赖这些库；GoRogue 的主要功能（FOV、寻路）可实现性不高，备选方案多 |
| **monogame-mcp 不成熟** | 中 | 中 | monogame-mcp 是加分项不是必需品；AI 通过标准 C# 代码生成也能工作，只是效率稍低 |
| **中文像素字体渲染质量** | 低 | 低 | FontStashSharp + Noto Sans CJK 方案成熟；备选方案：BMFont 预渲染 + 自定义像素字体 |
| **.NET 发布包体大小** | 低 | 低 | .NET 8 支持 Native AOT 编译，最小发布包约 5-15MB（含 MonoGame 依赖），远小于 Unity |

### 7.2 备选方案

如果 MonoGame 在开发过程中暴露出严重问题，备选路线如下：

**Plan B：MonoGame → Godot C#**

迁移条件：MonoGame 生态不足以支撑项目需求（如 Myra 无法满足复杂 UI 需求）。
- 迁移成本：中高
- 数据层（JSON 配置、SQLite Schema）可以直接复用
- 游戏逻辑层（战斗引擎、角色系统）的 C# 代码需要适配 Godot 的节点系统
- 渲染层需要重写（SpriteBatch → Godot 节点）
- **必须配合严格的代码审查流程**，建立 AI 生成的 Godot C# 代码的 Unity API 过滤器
- 跨域混淆风险需要工具层面的检测（写一个 Unity API 黑名单检测脚本）

**Plan C：MonoGame → Unity 6**

迁移条件：团队扩张到 5 人以上，需要标准化工具链和更大的开发者市场。
- 迁移成本：高
- 数据层可以直接复用
- 游戏逻辑层：C# 代码主体可以复用，但需要适配 Unity 的 GameObject/Component 架构
- 渲染层：MonoGame 的 SpriteBatch 调用需要改为 Unity 的 SpriteRenderer
- 优势：Unity 的资产商店和社区生态可以加速后续开发
- 代价：包体增大、性能开销上升、许可条款风险

**Plan D：混合方案**

- **MonoGame 主力 + Pygame 原型验证**：在 Pygame 中快速验证核心机制（D&D 5e 规则引擎、LLM 对话流），验证通过后移植到 MonoGame
- 降低"走错方向"的风险
- Pygame 的 LLM 集成和 Python 快速迭代能力可以用来验证游戏设计假设

### 7.3 关键决策记录

| 决策 | 选择 | 理由 | 时间 |
|:----:|:----:|------|:----:|
| 游戏框架 | MonoGame 3.8.5+ | 编译期类型安全 + 零跨域混淆 + AI-First 适配 | v2.0 |
| 编程语言 | C# (.NET 8+) | 强类型 + 团队熟悉 + AI 数据丰富 | v2.0 |
| 场景/ECS | Nez | 成熟的 MonoGame ECS 框架，Active/Node/Component 架构 | v2.0 |
| Roguelike | GoRogue | FOV/寻路/地图生成一站式解决方案 | v2.0 |
| UI 框架 | Myra | 像素风友好，数据绑定支持 | v2.0 |
| LLM 集成 | HttpClient + System.Text.Json | 标准库，零第三方依赖 | v2.0 |

---

> **结论：MonoGame + C# 是《酒馆与命运》项目在 AI-First 开发范式下的最优技术底座。** 它的编译期类型安全提供了 AI 生成代码的质量门禁，零跨域混淆风险避免了 Godot C# 的 Unity API 污染，代码即场景的模式完美匹配 AI 工作流，成熟的 Roguelike 生态（GoRogue）和 .NET 标准库（HttpClient + System.Text.Json）为项目提供了坚实的基础。

> 主要代价是无编辑器带来的传统开发效率损失，但通过 AI 辅助（monogame-mcp）和 Nez/Myra/GoRogue 等成熟库，可以将这部分损失降到最低。如果 MonoGame 生态在后续开发中暴露出严重不足，备选路线为 Godot C#（配合 Unity API 过滤器）或 Unity 6。

---

> **文档版本**: v2.0
> **更新日期**: 2026-05-05
> **前置文档**: GDD-v1.md
> **关联文档**: 02-overall-architecture.md（需同步更新至 MonoGame 架构）
> **核心变化**: v1.0 推荐 Godot 4.x + GDScript → v2.0 基于 AI-First 开发范式重评估 → 推荐 MonoGame + C#
