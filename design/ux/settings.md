# UX Spec: 设置界面 (Settings)

> **Status**: Complete
> **Author**: wastecat + ux-designer
> **Last Updated**: 2026-05-12
> **Journey Phase(s)**: 全局可用（标题屏幕 + 酒馆 + 暂停菜单）
> **Template**: UX Spec
> **Accessibility Tier**: Standard（# Basic → Standard 升级，2026-05-12）

---

## Purpose & Player Need

**设置界面是玩家对《酒馆与命运》的"控制室"**——在这里，游戏从"设计师预设的体验"变成"玩家自己调节的体验"。它不是功能清单，而是玩家与游戏之间的信任桥梁：当画面太暗、动画太快、按键不顺手时，设置界面是玩家**唯一不需要退出游戏就能解决问题的地方**。

### 四个标签页，四种玩家需求

| 标签页 | 服务的 Player Need | 设计暗示 |
|--------|-------------------|---------|
| **画面** | 🛋️ **舒适度** — "让我看得舒服，不刺眼、不模糊、分得清敌我" | 首次访问时亮度/缩放是最高频操作；色盲模式是"配置一次就忘"的安全网 |
| **声音** | 🛋️ **舒适度** — "让我听得舒服，不被重复音效烦扰，但关键时刻不遗漏" | 三条独立音量滑块（音乐/音效/环境音）是核心；听觉警告开关是战术辅助 |
| **游戏** | 🎮 **掌控感** — "让我控制节奏——快一点刷战斗、慢一点读故事" | 动画速度和文字速度是"偶尔回来微调"的开关；战斗节奏影响策略深度 vs 时间效率 |
| **控制** | 🔧 **个性化** — "让按键顺应我的肌肉记忆，而非我去迁就游戏" | 键位重绑是"配置一次就忘"的核心功能；键盘和手柄各自独立映射 |

### 使用模式

- **画面/控制**：一次性配置（首次启动或换设备时），之后基本不动
- **声音/游戏**：偶尔微调（根据冒险节奏、心情、或设备环境切换）
- **不需要方案系统**：四标签页的扁平结构已足够——玩家不需要"保存多个配置方案"

### 如果设置界面不存在或难用，会发生什么？

- 色盲玩家无法区分敌友 → **放弃游戏**
- 动画太慢的玩家在战斗中失去耐心 → **跳过战斗动画 → 失去战术沉浸**
- 按键不顺手但无法改的玩家 → **操作失误 → 永久死亡 → 挫败退坑**
- 字体太小看不清装备属性的玩家 → **策略选择盲目 → 冒险失败 → 归咎游戏不公平**

### 一句话总结

> "玩家来到设置界面，想要的是一个**让游戏适应自己，而非自己适应游戏**的地方。"

---

## Player Context on Arrival

玩家从三个入口抵达设置界面，每次都带着不同的情绪和需求。

### 三条到达路径

| 到达路径 | 玩家刚才在做什么 | 情绪状态 | 典型意图 | 设计响应 |
|----------|-----------------|---------|---------|---------|
| **标题 → 设置** | 刚启动游戏，浏览标题菜单 | 🧘 平静、探索心态 | "第一次玩，先把画面和声音调舒服" | 首次访问时锚定到"画面"标签页 + 引导文字；后续访问记住上次标签页位置 |
| **酒馆 → 设置** | 冒险结算后休整，或管理队伍 | 😌 放松、从容 | "动画太快了调慢点" / "BGM 循环烦了关小声" | 默认锚定到"游戏"或"声音"标签页（根据上次访问） |
| **暂停 → 设置** | 战斗或探索中紧急暂停 | 😰 可能焦虑——"按键为什么不对！" | "我要把确认键改掉" / "太暗了看不见" | 打开时顶部显示"游戏已暂停"轻提示（2 秒自动消失）；**不锁定任何标签页** |

### 首次启动引导

- **触发条件**：玩家在标题屏幕首次进入设置界面
- **行为**：自动锚定到 **"画面"** 标签页，标签栏上方显示一行引导文字
- **引导文字**：`"调整画面与声音以获得最佳体验"`（14px, `SecondaryText` 色, 居中）
- **关闭方式**：按 `Esc` 或切换标签页时引导文字自动消失；玩家操作任意设置项后也消失
- **后续访问**：不再显示引导，记住玩家上次关闭设置界面时的标签页位置

### 暂停入口的特殊处理

- 标题栏/标签栏上方短暂显示 **"游戏已暂停"** 提示（14px, 金色 `#FFD700`, 淡入 100ms → 停留 2s → 淡出 200ms）
- 提示文字不阻挡任何交互——玩家可以立即操作设置项
- 不锁定任何标签页：战斗中需要改键位时正是"控制"标签页最有用的时候

---

## Navigation Position

设置界面是**全局可及的独立覆盖层**——不隶属于任何特定父屏幕，而是从三个不同上下文进入后，**始终返回来处**。

```
标题屏幕 ──→ 设置 ──→ 标题屏幕
酒馆     ──→ 设置 ──→ 酒馆
暂停菜单 ──→ 设置 ──→ 暂停菜单（→ 继续游戏）
```

它不是酒馆的"子菜单"，也不是标题的"分支"——它是一个**上下文感知的通用工具层**：记住从哪来，就回哪去。设置界面内部不提供"返回标题"按钮——退出冒险等导航决策由暂停菜单承担，保持设置界面的单一职责。

---

## Entry & Exit Points

### 入口

| 入口来源 | 触发方式 | 玩家携带的上下文 |
|----------|---------|-----------------|
| 标题屏幕 | 选择"设置"菜单项 | 无存档上下文；若首次访问则触发引导模式（锚定到"画面"标签页） |
| 酒馆主屏幕 | 点击顶部 HUD `[设置]` 按钮 | 当前酒馆状态（等级/声望/金币信息用于后续返回定位，不影响设置功能） |
| 暂停菜单（战斗/探索中） | 按 `Esc` / `Start` → 选择"设置" | 游戏已暂停状态；打开时显示"游戏已暂停"轻提示（2s 自动消失） |

### 出口

| 出口目标 | 触发方式 | 备注 |
|----------|---------|------|
| **返回来处**（标题 / 酒馆 / 暂停菜单） | 点击 `[关闭 X]`、按 `Esc`、点击遮罩区域、手柄 `B` | 遵循覆盖层模式（交互模式 §9）：三重关闭机制。若存在未保存更改 → 弹出确认对话框（模式 1） |
| （无"返回标题"入口） | — | 设置界面保持单一职责——退出冒险等导航决策由暂停菜单提供 |

---

## Layout Specification

### Information Hierarchy

设置界面承载四个标签页、共 26 个设置项。信息层级按"热路径优先"原则组织——玩家最常调整的项放在最显眼位置。

#### 画面 (7 项)

| 优先级 | 设置项 | 理由 |
|:------:|--------|------|
| 1 | 亮度/伽马 | 房间光线变化是最频繁的调整触发——白天/夜晚/开灯/关灯 |
| 2 | 文字缩放（含字体大小下限） | 核心无障碍需求；Standard 等级的支柱功能 |
| 3 | UI 缩放 | 一次性配置，但影响所有菜单的可用性 |
| 4 | 全屏模式 | 首次配置后可遗忘 |
| 5 | 色盲模式 | 一次性安全网；受众特定但影响深远 |
| 6 | 屏幕震动 | 被动调整——"受不了才来关" |
| 7 | 闪烁效果 | 同屏幕震动 |

#### 声音 (4 项)

| 优先级 | 设置项 | 理由 |
|:------:|--------|------|
| 1 | 主音量 | 全局控制——"太吵了一键关小" |
| 2 | 音效音量 | 战斗音效重复是最大痛点；独立于音乐调整 |
| 3 | 音乐音量 | BGM 氛围调节 |
| 4 | 环境音音量 | 几乎不碰——酒馆壁炉噼啪/冒险风声 |

#### 游戏 (6 项)

| 优先级 | 设置项 | 理由 |
|:------:|--------|------|
| 1 | 动画速度 | 核心设计需求——直接控制全局 UI 动画节奏（面板滑入/按钮反馈/骰子动画） |
| 2 | 战斗速度 | 直接影响策略深度——快速刷低级怪 vs 慢慢品味 Boss 战 |
| 3 | 文字速度 | 叙事节奏偏好——速读者 vs 沉浸读者 |
| 4 | 自动战斗 | 降负开关——老玩家刷低级遭遇时的选项 |
| 5 | 字幕 | 默认开启的辅助功能，很少需要改动 |
| 6 | 按键提示 | 同字幕 |

#### 控制 (3 项 + 2 恢复按钮)

| 优先级 | 设置项 | 理由 |
|:------:|--------|------|
| 1 | 键盘键位重绑 | PC 主力用户的核心需求——Standard 等级支柱功能 |
| 2 | 恢复键盘默认 | 安全网——"改乱了可以回去" |
| 3 | 手柄键位重绑 | 次要输入设备——有手柄的玩家才会用 |
| — | 恢复手柄默认 | 安全网（手柄侧） |

---

### Layout Zones

采用 **方案 A：单列列表式** —— 三个简单标签页（画面/声音/游戏）使用垂直单列布局，每行一个设置项。控制标签页因键位映射表需容纳约 16 行，切换为双列键位表布局。

```
┌──────────────────────────────────────┐
│  [X]  标题栏 (40px)                  │
├────┬────┬────┬────┬─────────────────┤
│ T1 │ T2 │ T3 │ T4 │   标签栏 (36px)  │
├────┴────┴────┴────┴─────────────────┤
│                                      │
│  设置项 1       [控件]              │ ← 内容行 (48px/行)
│  设置项 2       [控件]              │
│  ...                                │
│                                      │
├──────────────────────────────────────┤
│   [恢复默认]            [关闭]       │ ← 底部栏 (40px)
└──────────────────────────────────────┘
```

| 区域 | 尺寸 | 说明 |
|------|:---:|------|
| 窗口总尺寸 | ~560×640 px | 居中于 1280×720，遵循 FF6 暗色窗 Chrome |
| 标题栏 | 全宽 × 40px | "设置" 标题 16px + 右上角 [X] 16×16 |
| 标签栏 | 全宽 × 36px | 4 标签页，使用模式 4（标签页/分区导航） |
| 内容区 | 全宽 − 16px 内边距 | 每行 48px：12px 标签 + 24px 控件 + 12px 间距 |
| 底部栏 | 全宽 × 40px | "恢复默认"（左）+ "关闭"（右）|

---

### Component Inventory

所有选项型设置项统一使用**下拉选择器**（`当前值 + ▼` → 点击展开列表 → ↑↓ 选择 → Enter 确认），不再区分 3–4 选项用 ← →、5+ 选项用下拉——一致性优先。

#### 标签页 1: 画面

| # | 设置项 | 组件 | 内容 | 模式 |
|---|--------|------|------|------|
| 1 | 亮度 | 连续滑块 | 轨道 + 手柄 + 右侧数值 `80%`。填充色 `#44FF44` | 新模式: 连续滑块 |
| 2 | 文字缩放 | 下拉选择器 | 4 档: `小` `▶标准` `大` `特大` | 新模式: 下拉选择器 |
| 3 | UI 缩放 | 下拉选择器 | 4 档: `紧凑` `▶标准` `宽松` `极宽` | 下拉选择器 |
| 4 | 色盲模式 | 下拉选择器 | 5 档: `▶无` `红色盲` `绿色盲` `蓝色盲` `高对比度` | 下拉选择器 |
| 5 | 全屏模式 | 开关 | `[■ 开]` / `[□ 关]`，32×16px 矩形 | 新模式: 开关 |
| 6 | 屏幕震动 | 开关 | `[■ 开]`（默认） | 开关 |
| 7 | 闪烁效果 | 开关 | `[■ 开]`（默认） | 开关 |

#### 标签页 2: 声音

| # | 设置项 | 组件 | 内容 | 模式 |
|---|--------|------|------|------|
| 1 | 主音量 | 连续滑块 | 滑块 + 数值 `80%`。轨道蓝色 `#4488FF`。滑块下方 10px 提示文字"全局缩放因子" | 连续滑块 |
| 2 | 音乐音量 | 连续滑块 | 滑块 + 数值 `80%`，蓝色轨道 | 连续滑块 |
| 3 | 音效音量 | 连续滑块 | 滑块 + 数值 `80%`，蓝色轨道 | 连续滑块 |
| 4 | 环境音音量 | 连续滑块 | 滑块 + 数值 `80%`，蓝色轨道 | 连续滑块 |

> **主音量语义**：主音量是全局缩放因子（0–100%），乘以各子滑块的百分比得到实际输出音量——而非子滑块的"总和"。各子滑块始终展示其独立设置值。

#### 标签页 3: 游戏

| # | 设置项 | 组件 | 内容 | 模式 |
|---|--------|------|------|------|
| 1 | 动画速度 | 连续滑块（带刻度） | 滑块 + 数值 `1.0×`。轨道金色 `#FFD700`。0.5× / 1.0× / 1.5× / 2.0× 位置有像素刻度线（┆），拖拽时吸附到刻度 | 连续滑块 + 刻度吸附 |
| 2 | 战斗速度 | 下拉选择器 | 3 档: `▶1×` `2×` `4×` | 下拉选择器 |
| 3 | 文字速度 | 下拉选择器 | 4 档: `慢` `▶正常` `快` `瞬间` | 下拉选择器 |
| 4 | 自动战斗 | 开关 | `[□ 关]`（默认） | 开关 |
| 5 | 字幕 | 开关 | `[■ 开]`（默认） | 开关 |
| 6 | 按键提示 | 开关 | `[■ 开]`（默认） | 开关 |

#### 标签页 4: 控制

| # | 设置项 | 组件 | 内容 | 模式 |
|---|--------|------|------|------|
| 1 | 键盘映射表 | 键位重绑行 × 12 | 左列 8 行（确认/取消/攻击/法术/物品/技能/待机/结束回合）+ 右列 4 行（地图/背包/角色/日志）。每行: 动作名（120px）+ `[按键]` 按钮（80×24px） | 新模式: 键位重绑行 |
| 2 | 手柄映射表 | 键位重绑行 × 8 | 右列 8 行（确认/取消/攻击/法术/物品/技能/暂停/日志） | 键位重绑行 |
| 3 | 恢复键盘默认 | 危险按钮 | 点击 → 模式 1 确认对话框（"确认恢复键盘默认设置？此操作不可撤销。"） | 模式 1（确认/取消对话框） |
| 4 | 恢复手柄默认 | 危险按钮 | 同键盘 | 模式 1 |

#### 底部操作栏（所有标签页共享）

| 组件 | 内容 | 可见性 |
|------|------|:---:|
| 恢复默认 | "恢复默认" — 重置当前标签页所有设置项为默认值。点击 → 模式 1 确认对话框。 | 仅画面/声音/游戏标签页 |
| 关闭 | "关闭" — 保存所有更改并返回进入设置前的屏幕。也响应 `Esc` / 遮罩点击 / 手柄 `B`。 | 始终 |

---

### ASCII Wireframes

#### 标签页 1: 画面

```
┌──────────────────────────────────────────────────────────┐
│  [X]  设  置                                            │  标题栏 40px
├──────┬──────┬──────┬──────┬─────────────────────────────┤
│ ▶画面│ 声音 │ 游戏 │ 控制 │                              │  标签栏 36px
├──────┴──────┴──────┴──────┴─────────────────────────────┤
│                                                          │
│  亮度         [████████████████░░░░░░░░]  80%           │  滑块行 48px
│                                                          │
│  文字缩放      ▶ 标准                          ▼        │  下拉行 48px
│                                                          │
│  UI 缩放      ▶ 标准                          ▼        │
│                                                          │
│  色盲模式      ▶ 无                            ▼        │
│                                                          │
│  全屏模式      [■ 开]                                   │  开关行 48px
│                                                          │
│  屏幕震动      [■ 开]                                   │
│                                                          │
│  闪烁效果      [■ 开]                                   │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                      [恢复默认]            [关闭]        │  底部栏 40px
└──────────────────────────────────────────────────────────┘

  滑块轨道: ████ = 已填充段 (绿色 #44FF44)  ░░░░ = 空段 (深灰)
  开关:     [■ 开] = 填充矩形  [□ 关] = 空心矩形
  下拉:     ▶ 标准 = 当前值 + ▼ = 展开箭头
```

#### 标签页 2: 声音

```
┌──────────────────────────────────────────────────────────┐
│  [X]  设  置                                            │
├──────┬──────┬──────┬──────┬─────────────────────────────┤
│ 画面 │▶声音│ 游戏 │ 控制 │                              │
├──────┴──────┴──────┴──────┴─────────────────────────────┤
│                                                          │
│  主音量       [████████████████████░░░░]  80%           │  蓝色轨道
│               全局缩放因子——各子滑块在此之上生效          │  提示文字 10px
│                                                          │
│  音乐         [████████████████████░░░░]  80%           │
│                                                          │
│  音效         [████████████████████░░░░]  80%           │
│                                                          │
│  环境音       [████████████████████░░░░]  80%           │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                      [恢复默认]            [关闭]        │
└──────────────────────────────────────────────────────────┘

  所有滑块轨道颜色: 蓝色 #4488FF
  主音量下方提示: "全局缩放因子" → 10px 小字, SecondaryText 色
```

#### 标签页 3: 游戏

```
┌──────────────────────────────────────────────────────────┐
│  [X]  设  置                                            │
├──────┬──────┬──────┬──────┬─────────────────────────────┤
│ 画面 │ 声音 │▶游戏│ 控制 │                              │
├──────┴──────┴──────┴──────┴─────────────────────────────┤
│                                                          │
│  动画速度     [████████████████░░░░]  1.0×             │  金色轨道+刻度
│               0.5×    1.0×    1.5×    2.0×             │  刻度标记 10px
│               ┆        ┆        ┆        ┆              │
│                                                          │
│  战斗速度      ▶ 1×                            ▼        │
│                                                          │
│  文字速度      ▶ 正常                          ▼        │
│                                                          │
│  自动战斗      [□ 关]                                   │
│                                                          │
│  字幕          [■ 开]                                   │
│                                                          │
│  按键提示      [■ 开]                                   │
│                                                          │
├──────────────────────────────────────────────────────────┤
│                      [恢复默认]            [关闭]        │
└──────────────────────────────────────────────────────────┘

  动画速度滑块: 轨道金色 #FFD700, 吸附到刻度 (0.5×/1.0×/1.5×/2.0×)
  刻度 ┆ = 2px 像素竖线
```

#### 标签页 4: 控制

```
┌──────────────────────────────────────────────────────────┐
│  [X]  设  置                                            │
├──────┬──────┬──────┬──────┬─────────────────────────────┤
│ 画面 │ 声音 │ 游戏 │▶控制│                              │
├──────┴──────┴──────┴──────┴─────────────────────────────┤
│                                                          │
│  ── 键盘映射 ────────────  ── 手柄映射 ──────────────  │  分区标题 14px
│                                                          │
│  确认          [ Enter     ]   确认          [ A       ] │  键位行 36px
│  取消          [ Escape    ]   取消          [ B       ] │
│  攻击          [ A         ]   攻击          [ X       ] │
│  法术          [ S         ]   法术          [ Y       ] │
│  物品          [ D         ]   物品          [ LT      ] │
│  技能          [ F         ]   技能          [ RT      ] │
│  待机          [ W         ]   暂停          [ Start   ] │
│  结束回合      [ Space     ]   日志          [ Select  ] │
│  地图          [ M         ]                             │
│  背包          [ I         ]                             │
│  角色          [ C         ]                             │
│  日志          [ J         ]                             │
│                                                          │
├──────────────────────────────────────────────────────────┤
│          [恢复键盘默认]    [恢复手柄默认]    [关闭]      │
└──────────────────────────────────────────────────────────┘

  键位按钮: 80×24px, 深灰背景 + 白色文字 + 1px 浅边框
  聚焦态: 2px 金色边框 + 底部 14px 提示 "按任意键重新绑定…按 Esc 取消"
  冲突警告: 新键已被占用 → 该行闪烁红色 200ms × 3 + 底部显示 "与 [动作名] 冲突，覆盖？"
```

---

## States & Variants

| 状态/变体 | 触发条件 | 变化内容 |
|-----------|---------|---------|
| **默认态** | 正常打开设置界面 | 所有设置项显示当前值；标签页锚定到上次关闭时的位置（首次访问则为"画面"） |
| **首次访问引导** | 标题屏幕首次进入设置 | 顶部显示引导文字 "调整画面与声音以获得最佳体验"（14px, `SecondaryText` 色, 居中于标签栏上方）。切换标签页或操作任意设置项后消失 |
| **暂停态** | 从暂停菜单进入 | 标签栏上方显示 "游戏已暂停"（14px, 金色 `#FFD700`, 淡入 100ms → 停留 2s → 淡出 200ms） |
| **键位重绑等待** | 点击一个键位按钮 | 该按钮文字变为 "…"（闪烁 500ms 周期），底部提示 "按任意键重新绑定…按 Esc 取消"。其他键位按钮变灰不可交互 |
| **键位冲突** | 重绑时新键已被占用 | 冲突行闪烁红色 200ms × 3；底部提示 "与 [动作名] 冲突——按 Enter 覆盖，按 Esc 取消" |
| **未保存更改** | 修改设置后未点"关闭"就按 Esc / 点击遮罩 | 弹出模式 1 确认对话框："保存更改？" → `[保存]` `[放弃]` `[取消]` |
| **恢复默认确认** | 点击"恢复默认"按钮 | 弹出模式 1 确认对话框："确认恢复 [标签页名] 默认设置？此操作不可撤销。" → `[确认]` `[取消]` |
| **保存失败** | 磁盘写入异常 | Toast 通知（模式 5）："⚠ 设置保存失败，请重试"（红色边框, 5s 显示）。当前修改保留在内存中 |

---

## Interaction Map

本界面使用 **Keyboard/Mouse (Primary) + Gamepad (Partial)** 输入方案。交互模式遵循已有的模式 12（高亮/选中态）三层体系。

### 全局导航

| 操作 | 键盘/鼠标 | 手柄 | 反馈 | 结果 |
|------|----------|------|------|------|
| 切换标签页 | `← →` 或点击标签 | `LB` / `RB` | 选中标签高亮（金色下划线 2px）+ 内容区交叉淡入淡出 150ms（受 `anim_speed` 影响） | 切换标签页内容；焦点移至内容区第一项 |
| 关闭设置 | `Esc` / 点击 `[X]` / 点击遮罩 | `B` | 若未保存 → 弹出保存确认对话框。否则覆盖层 fade out 120ms，返回上一屏幕 | 保存（如有更改）并返回 |
| 关闭（无更改） | 同上（无修改时） | 同上 | 直接关闭，无对话框 | 返回上一屏幕 |

### 滑块交互（亮度、音量、动画速度）

| 操作 | 键盘/鼠标 | 手柄 | 反馈 | 结果 |
|------|----------|------|------|------|
| 聚焦滑块 | `Tab` / `↑↓` 导航到滑块行 | `D-Pad ↑↓` | 2px 金色聚焦边框（模式 12 L2） | — |
| 增大 | `→` / 点击轨道右侧 | `D-Pad →` / `R-Stick →` | 轨道即时填充；数值实时更新 | 值 +5%（亮度/音量）或 +0.05×（动画速度） |
| 减小 | `←` / 点击轨道左侧 | `D-Pad ←` / `R-Stick ←` | 同上 | 值 −5% 或 −0.05× |
| 拖拽 | 鼠标按住滑块手柄拖动 | 不可用 | 手柄实时跟随 D-Pad / R-Stick | — |
| 跳转 | 点击轨道任意位置 | 不可用 | 手柄即时跳转到点击位置对应值 | — |
| 动画速度吸附 | 拖拽释放时（仅动画速度滑块） | `← →` 释放时 | 手柄吸附到最近的刻度（0.5× 步长）+ 轻微咔嗒音效 | 值 → 0.5/1.0/1.5/2.0 |

### 下拉选择器交互（文字缩放、UI 缩放、色盲模式、战斗速度、文字速度）

| 操作 | 键盘/鼠标 | 手柄 | 反馈 | 结果 |
|------|----------|------|------|------|
| 聚焦 | `Tab` / `↑↓` 导航 | `D-Pad ↑↓` | 2px 金色聚焦边框 | — |
| 展开列表 | `Enter` / `Space` / 鼠标点击 | `A` | 列表从当前行下方展开（overlay_pop 100ms）；选中项 ▶ 高亮；不显示遮罩（轻量展开） | 显示选项列表 |
| 选择 | `↑↓` + `Enter` / 鼠标点击 | `D-Pad ↑↓` + `A` | 选中项 ▶ 移动；确认后列表缩回（80ms）+ 当前值更新 | 值变更 |
| 取消选择 | `Esc`（列表展开时） | `B` | 列表缩回，保持原值 | 无变更 |

### 开关交互（全屏模式、屏幕震动、闪烁效果、自动战斗、字幕、按键提示）

| 操作 | 键盘/鼠标 | 手柄 | 反馈 | 结果 |
|------|----------|------|------|------|
| 切换 | `Enter` / `Space` / 鼠标点击 | `A` | 开关矩形即时切换（`[■]` ↔ `[□]`）50ms；若为全屏模式 → 额外执行全屏切换 | 值即时变更 |
| 聚焦 | `Tab` / `↑↓` | `D-Pad ↑↓` | 2px 金色聚焦边框 | — |

### 键位重绑行交互（控制标签页）

| 操作 | 键盘/鼠标 | 手柄 | 反馈 | 结果 |
|------|----------|------|------|------|
| 开始重绑 | `Enter` / 鼠标点击 | `A` | 按钮文字 → `…`（闪烁 500ms）；底部提示 "按任意键重新绑定…按 Esc 取消"；其他键位行变灰不可聚焦 | 进入等待输入状态 |
| 绑定新键 | 按下目标按键 | 按下目标手柄键 | 按钮显示新键名；底部提示消失；所有键位行恢复可交互 | 更新映射；写入配置 |
| 取消重绑 | `Esc`（等待输入时） | `B` | 按钮恢复原键名；提示消失 | 无变更 |
| 冲突处理 | 新键已被其他动作占用 | 同键盘 | 冲突行闪烁红色 200ms × 3；底部提示 "与 [动作名] 冲突——按 Enter 覆盖，按 Esc 取消" | — |
| 覆盖冲突 | `Enter`（冲突提示时） | `A` | 冲突行键位 → `—`（清空）；当前行显示新键 | 两行映射均更新 |
| 取消覆盖 | `Esc`（冲突提示时） | `B` | 恢复原键名，冲突行不变 | 无变更 |

### 按钮交互（恢复默认、关闭）

| 操作 | 键盘/鼠标 | 手柄 | 反馈 | 结果 |
|------|----------|------|------|------|
| 恢复默认 | `Enter` / 鼠标点击 | `A` | 弹出模式 1 确认对话框（"确认恢复 [标签页名] 默认设置？此操作不可撤销。"） | 确认后重置当前标签页所有值 |
| 关闭 | `Enter` / 鼠标点击 / `Esc` | `A` / `B` | 若有未保存更改 → 弹出保存确认对话框（"保存更改？" → `[保存]` `[放弃]` `[取消]`） | 保存并返回 |

---

## Events Fired

| 玩家动作 | 事件名 | 载荷/数据 | 备注 |
|----------|--------|----------|------|
| 打开设置界面 | `SettingsOpened` | `{source: "title" \| "tavern" \| "pause"}` | 分析入口分布 |
| 切换标签页 | `SettingsTabChanged` | `{from: string, to: string}` | 了解哪些标签页被访问 |
| 调整滑块 | `SettingsValueChanged` | `{key: string, old_value: number, new_value: number}` | 亮度/音量/动画速度。节流：拖拽结束时发送一次 |
| 切换下拉选项 | `SettingsValueChanged` | `{key: string, old_value: string, new_value: string}` | 文字缩放/UI 缩放/色盲/战斗速度/文字速度 |
| 切换开关 | `SettingsValueChanged` | `{key: string, old_value: bool, new_value: bool}` | 全屏/震动/闪烁/自动战斗/字幕/按键提示 |
| 开始键位重绑 | `KeyRebindStarted` | `{action: string, device: "keyboard" \| "gamepad"}` | — |
| 完成键位重绑 | `KeyRebindCompleted` | `{action: string, old_key: string, new_key: string, had_conflict: bool}` | ⚠️ 修改持久化配置——需同步到 `ISettingsProvider` |
| 恢复默认 | `SettingsResetToDefault` | `{tab: string, count_reset: number}` | ⚠️ 批量修改持久化配置 |
| 关闭设置（有更改） | `SettingsSaved` | `{changed_keys: string[], total_changes: number}` | ⚠️ 触发持久化写入 |
| 关闭设置（无更改） | 无事件 | — | 避免噪音事件 |
| 保存失败 | `SettingsSaveFailed` | `{error: string}` | 错误追踪 |

> ⚠️ 标记表示该动作修改持久化游戏状态——需要架构团队确认 `ISettingsProvider` 的线程安全写入策略。

---

## Transitions & Animations

### Screen Enter/Exit

| Animation | Duration | Easing | Description |
|-----------|:---:|--------|-------------|
| Settings enter | 150ms | `ease_out_back` | `overlay_pop` — scale from center. Mask fades in `rgba(0,0,0,0→0.5)` synchronously |
| Settings exit | 120ms | `ease_in` | Reverse scale-in + fade out |
| Tab switch | 150ms | `ease_out` | Content area crossfade (affected by `anim_speed`: 300ms at 0.5x, 75ms at 2.0x) |

### Component Animations

| Animation | Duration | Description |
|-----------|:---:|------|
| Dropdown expand | 100ms | `overlay_pop` micro — expands downward from row position |
| Dropdown collapse | 80ms | Reverse scale-in |
| Toggle switch | 50ms | Rectangle fill/unfill instant |
| Rebind wait blink | 500ms cycle | "..." text alternates white / `SecondaryText` |
| Key conflict blink | 200ms × 3 | Background red `#FF4444` blinks 3 times |
| Bottom hint appear/disappear | fade in 100ms / fade out 200ms | Rebind and conflict hints |

### Reduced Motion Mode

When `reducedMotion=true` (or `anim_speed` ≤ 0.1x):

| Animation | Replacement |
|-----------|------------|
| Settings enter/exit | Instant (0ms), no scale animation |
| Tab switch | Instant, no crossfade |
| Dropdown expand/collapse | Instant show/hide |
| Rebind blink | Disabled — "..." stays static white |
| Conflict blink | Disabled — conflict row stays static red until player confirms or cancels |

---

## Data Requirements

The data source for the Settings screen is `ISettingsProvider` (GDD §1C dependency table). All values are read from and written to the Provider.

| Data Item | Source System | R/W | Type | Default | Notes |
|-----------|--------------|:---:|------|---------|-------|
| Brightness | `ISettingsProvider` | R/W | number (0-100) | 80 | — |
| Text Scale Factor | `ISettingsProvider` | R/W | number (0.8/1.0/1.3/1.6) | 1.0 | Maps to GDD §14.2 four tiers |
| UI Scale Factor | `ISettingsProvider` | R/W | number (0.8/1.0/1.2/1.5) | 1.0 | Maps to GDD §14.3 four tiers |
| Colorblind Mode | `ISettingsProvider` | R/W | enum | None | 5 modes (GDD §14.1) |
| Fullscreen | `ISettingsProvider` | R/W | bool | false | GDD §11.1 (F11) |
| Screen Shake | `ISettingsProvider` | R/W | bool | true | GDD §14.4 |
| Flash Effects | `ISettingsProvider` | R/W | bool | true | GDD §14.4 |
| Master Volume | `ISettingsProvider` | R/W | number (0-100) | 80 | Global scaling factor |
| Music Volume | `ISettingsProvider` | R/W | number (0-100) | 80 | — |
| SFX Volume | `ISettingsProvider` | R/W | number (0-100) | 80 | — |
| Ambient Volume | `ISettingsProvider` | R/W | number (0-100) | 80 | — |
| Animation Speed | `ISettingsProvider` | R/W | number (0.5-2.0) | 1.0 | Snaps to 0.5x step (GDD §1D.4) |
| Combat Speed | `ISettingsProvider` | R/W | number (1/2/4) | 1 | GDD §11.6 |
| Text Speed | `ISettingsProvider` | R/W | enum | Normal | GDD §14.4 |
| Auto Battle | `ISettingsProvider` | R/W | bool | false | GDD §14.4 |
| Subtitles | `ISettingsProvider` | R/W | bool | true | GDD §14.4 |
| Key Hints | `ISettingsProvider` | R/W | bool | true | GDD §14.4 |
| Keyboard Bindings | `ISettingsProvider` | R/W | `Record<string, Keys>` | See GDD §11.1 | JSON persisted |
| Gamepad Bindings | `ISettingsProvider` | R/W | `Record<string, Buttons>` | See GDD §11.3 | JSON persisted |
| Last Tab Position | `ISettingsProvider` | R/W | string | "display" | Saved on close |
| Is First Visit | `ISettingsProvider` | R | bool | true | First-launch guide trigger |

> **Architecture Concern**: `ISettingsProvider` thread-safe write strategy needs architecture team confirmation. Writes are low-frequency but key rebinds can happen mid-combat pause. Recommend synchronous writes (flush to disk) with memory-cached reads.

---

## Accessibility

Based on the upgraded **Standard** accessibility tier (`design/accessibility-requirements.md`), the Settings screen is responsible for the following:

### Keyboard Navigation

- [x] All interactive components reachable via `Tab` / arrow keys
- [x] Tabs via `← →` / `LB` `RB`
- [x] Sliders via `← →` (±5% or ±0.05x), supports hold-to-repeat
- [x] Dropdowns via `Enter` expand, `↑↓` select, `Enter` confirm
- [x] Toggles via `Enter` / `Space`
- [x] Key rebind fully keyboard-accessible — including conflict override flow
- [x] `Esc` always valid: cancel current action → return to tab → close settings (LIFO layers)

### Gamepad Navigation

- [x] All interactive components reachable via `D-Pad` + `A`/`B`
- [x] Key rebind supports gamepad button input
- [x] Sliders via `D-Pad ← →` or `R-Stick`
- [x] Close via `B` = `Esc` (Pattern 7 convention)

### Visual Accessibility

| Requirement | Implementation |
|-------------|---------------|
| Text contrast ≥ 4.5:1 | Warm white `#F0E6D2` vs dark background `rgba(10,10,30,0.85)` ≈ 12:1 ✅ |
| Focus border contrast ≥ 3:1 | Gold `#FFD700` vs dark background ≈ 8:1 ✅ |
| No color-only info | All labels are text; toggles use `[■]`/`[□]` shape; dropdowns use `▶` marker |
| Color reliance audit | Slider fill colors (green/blue/gold) are decorative — values always shown as numbers. Colorblind modes auto-map per GDD §14.1 |
| Font size minimum | Via "Text Scale" setting — "Extra Large" tier delivers 19.2px at 1280x720 (≥ WCAG 2.1 AA 18px) |
| UI scaling | "Extra Wide" tier delivers larger buttons/spacing at 1280x720 — improved touch accessibility |

### Motion & Cognitive Accessibility

| Requirement | Implementation |
|-------------|---------------|
| Photosensitive safety | Key conflict blink rate = 200ms × 3 = ~5Hz (within safe threshold). No high-frequency flashing |
| Reduced motion | `reducedMotion=true` replaces all animations with instant transitions (see Transitions section) |
| Adjustable animation speed | Animation speed slider (0.5x-2.0x) allows players to slow all UI animations |
| Text size minimum | Text Scale ≥ 1.3x delivers 15.6px body text at 1280x720; ≥ 1.6x delivers 19.2px |

---

## Localization Considerations

Current game supports Simplified Chinese only, but design leaves room for future multilingual support.

### Text Length Constraints

| Element | CN Length | Design Width | 40% Expansion | Risk |
|---------|:---:|:---:|:---:|:---:|
| Tab: "画面" | 2 chars | 80px | "Display"=7 chars | 🟡 Medium — tab bar needs 120px/tab |
| Tab: "控制" | 2 chars | 80px | "Controls"=8 chars | 🟡 Same |
| Label: "文字缩放" | 4 chars | 120px | "Text Scale"=10 chars | 🟢 OK |
| Label: "色盲模式" | 4 chars | 120px | "Colorblind Mode"=16 chars | 🟢 OK |
| Dropdown value: "特大" | 2 chars | 60px | "Extra Large"=11 chars | 🟢 OK |
| Keybind action: "结束回合" | 4 chars | 80px | "End Turn"=8 chars | 🟢 OK |
| Button: "恢复默认" | 4 chars | 96px | "Restore Defaults"=17 chars | 🔴 HIGH — needs auto-width |
| Button: "关闭" | 2 chars | 64px | "Close"=5 chars | 🟢 OK |

### Layout-Critical Elements

| Element | Risk | Mitigation |
|---------|:---:|-----------|
| Tab buttons | 🟡 | Tab width = `max(80px, content_width + 16px)` — auto-fit |
| "Restore Defaults" button | 🔴 | 96px min-width insufficient for ~140px needed. Use `max(96px, content_width + 24px)` or abbreviate to "Restore" |
| Keybind buttons | 🟡 | `[ Escape ]` vs `[ Space ]` width diff ~30%. Unified 80px button width (Latin fonts are narrower, fits) |
| Conflict hint text | 🔴 | English ~50 chars. Ensure bottom hint row fits single line (~520px full width is sufficient) |
| Dropdown values | 🟢 | All tier labels are short, no layout pressure |

> **HIGH PRIORITY for localization engineer**: The "Restore Defaults" button (width constraint) and the conflict hint text (line-length constraint) are the two elements to test first during localization.

---

## Acceptance Criteria

```
Performance
- [ ] AC-SET-01: Settings screen opens within 200ms from trigger and completes enter animation
  WHEN clicking the [Settings] button in the top HUD of Tavern
  THEN mask + content window visible within 200ms and overlay_pop 150ms animation complete

Navigation
- [ ] AC-SET-02: All three entry points arrive and return correctly
  GIVEN game at Title Screen, Tavern Main, and Pause Menu respectively
  WHEN entering Settings then clicking Close / pressing Esc
  THEN return to Title Screen, Tavern Main, and Pause Menu respectively (no wrong destination)

- [ ] AC-SET-03: Tab switching via arrow keys updates content correctly
  GIVEN Settings open at Display tab
  WHEN pressing → 3 times
  THEN display Audio, Gameplay, Controls tab content in sequence; first item in each tab receives focus

Core Function
- [ ] AC-SET-04: Animation speed slider snaps to ticks and takes immediate effect
  GIVEN Gameplay tab, default animation speed 1.0x
  WHEN dragging slider near 1.5x tick and releasing / pressing → to 1.5x
  THEN slider snaps to 1.5x; value displays "1.5x"; after closing Settings, UI panel animations are visibly faster

- [ ] AC-SET-05: Full key rebind flow including conflict override
  GIVEN Controls tab, default keyboard bindings (Confirm=Enter, Cancel=Escape, Attack=A)
  WHEN (1) clicking "Attack" keybind button → (2) pressing Enter → (3) seeing conflict hint → (4) pressing Enter to override
  THEN (1) button shows "..." blinking → (2) hint "Conflict with Confirm — Enter=override, Esc=cancel" → (3) conflict row blinks red → (4) Attack=Enter, Confirm=— (cleared). New bindings take effect after closing Settings

Error State
- [ ] AC-SET-06: Unsaved changes trigger save confirmation on Esc
  GIVEN Settings open, brightness changed from 80% to 60%
  WHEN pressing Esc (without clicking Close first)
  THEN confirmation dialog "Save changes?" → [Save] [Discard] [Cancel]; choosing Save keeps brightness at 60%; choosing Discard restores 80%

Accessibility
- [ ] AC-SET-07: All interactive elements reachable via Tab traversal within each tab
  GIVEN Settings open at Display tab
  WHEN repeatedly pressing Tab
  THEN focus cycles through: Brightness slider → Text Scale dropdown → UI Scale dropdown → Colorblind dropdown → Fullscreen toggle → Screen Shake toggle → Flash Effects toggle → Restore Defaults button → Close button → back to tab bar. No dead ends, no skipped elements

- [ ] AC-SET-08: Esc cancels key rebind wait state
  GIVEN Controls tab, currently in Attack rebind wait state (button shows "...")
  WHEN pressing Esc
  THEN rebind cancelled; Attack key restored to A; bottom hint disappears; all keybind rows become interactable again
```

---

## Open Questions

| # | Question | Status |
|---|----------|:---:|
| Q1 | `ISettingsProvider` thread-safe write strategy — synchronous flush or async queue? | → `lead-programmer` |
| Q2 | Myra framework support for dynamic font size switching at runtime? | → `ui-programmer` |
| Q3 | Should a "Reset All Settings" (cross-tab) button exist in addition to per-tab restore? | → `game-designer` — deferred: per-tab restore is sufficient for MVP |
| Q4 | How to handle key rebind for "Confirm" when it is the key used to start a rebind? | → `ui-programmer` — constraint: Confirm action mapped key cannot enter rebind via Enter; must use mouse click to start |
| Q5 | Reduced-motion: is there a global `reducedMotion` flag, or is it inferred from `anim_speed ≤ 0.1x`? | → `game-designer` + `lead-programmer` — recommend explicit flag for clarity |
