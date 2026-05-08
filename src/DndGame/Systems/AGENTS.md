# Systems 模块

游戏玩法系统。所有业务逻辑位于此处。通过 IEventBus 通信——绝不直接引用其他系统。

## 结构

```
Systems/
├── Combat/                  # 战斗引擎：FSM、骰子、行动解析器、AI、地图
├── Character/               # 角色生成、数据记录、属性值
├── Adventure/               # 地下城探索：GoRogue 地图、FOV
├── Items/                   # (空——已规划）
├── Tavern/                  # (空——已规划）
└── WorldState/              # (空——已规划）
```

## 何处查找

| 需求 | 位置 | 备注 |
|------|------|------|
| 战斗状态机 | `Combat/CombatFSM.cs` | 回合制流程：Initiative → PlayerTurn → EnemyTurn → Resolution |
| 骰子系统 | `Combat/DiceRoller.cs` | d20 规则、优势/劣势、暴击 |
| 行动解析 | `Combat/ActionResolver.cs` | 命中计算、伤害、豁免、状态效果 |
| AI 目标选择 | `Combat/AISystem.cs` | 敌人回合决策 |
| 地图/FOV | `Combat/GoRogueMapManager.cs` | GoRogue 2.6.4 集成 |
| 状态效果 | `Combat/ConditionSystem.cs` | 中毒、震慑等 |
| 角色创建 | `Character/CharacterGenerator.cs` | 随机或模板角色生成 |
| 角色数据 | `Character/CharacterData.cs` | record 类型、属性值 |
| 地下城生成 | `Combat/DungeonGenerator.cs` | 程序化地图生成 |

## 约定

- **IEventBus 解耦** — 系统发布/订阅事件。绝不持有对其他系统具体类的引用。
- **数据驱动** — 数值来自 JSON/SQLite 配置，绝不硬编码。参见 `Data/Config/`（classes.json、races.json）。
- **LLM = 皮肤层** — 战斗/冒险系统中的数值结果由程序计算。LLM 仅生成叙述性风味文本。
- **命名空间**：`DndGame.Systems.{模块}`（例如 `DndGame.Systems.Combat`）。

## 反模式

- ❌ 业务逻辑位于 UI（Myra）——UI 仅收集输入和显示输出
- ❌ 直接引用 `new DiceRoller()` —— 使用接口和 EventBus
- ❌ 硬编码战斗公式——放入 JSON 或命名常量
- ❌ 在 System 类中混合关注点——每个系统一个职责（SRP）

## 注释

- **战斗是最成熟的系统** — ~9 个文件，~7 个测试文件。FSM 已完整实现。
- **DND 5e 偏差** — 先攻每轮重投、暴击最大化伤害骰、死亡豁免 3 轮无治疗即死、力竭 3 级、负重基于槽位、行动同时宣言后按先攻结算。
- **GoRogue 版本**：csproj 中为 2.6.4（文档引用 3.* —— 已锁定 2.6.4）。
- **Items/Tavern/WorldState** 是仅含目录结构的空桩——尚未实现代码。
