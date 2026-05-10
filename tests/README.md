# 测试基础设施

**引擎**: MonoGame 3.8.5+ / .NET 8 / C# 12
**测试框架**: xUnit + FluentAssertions
**CI**: `.github/workflows/tests.yml`
**配置日期**: 2026-05-10

## 目录布局

```
tests/
  DndGame.Tests/           # xUnit 测试项目
    Unit/                   # 纯逻辑单元测试（公式、状态机、数据校验）
      Combat/               # 战斗系统测试
      Character/            # 角色系统测试
      Gateway/              # LLM 网关测试
    Integration/            # 跨系统集成测试
  smoke/                    # 冒烟测试关键路径清单
  evidence/                 # 截图日志与手动测试签核记录
```

## 运行测试

```bash
dotnet test                                    # 全部测试
dotnet test --filter "FullyQualifiedName~CombatFSMTests"  # 特定测试
dotnet test --verbosity normal                 # 详细输出
```

## 测试命名

- **文件**: `[System]Tests.cs`（如 `CombatFSMTests.cs`）
- **方法**: `MethodName_Scenario_ExpectedResult`（AAA 模式）
- **示例**: `RollD20_StandardRoll_ReturnsValueBetween1And20`

## Story Type → 测试证据

| Story Type | 必需证据 | 位置 |
|---|---|---|
| Logic | 自动化单元测试 — 必须通过 | `tests/DndGame.Tests/Unit/[system]/` |
| Integration | 集成测试或 playtest 文档 | `tests/DndGame.Tests/Integration/[system]/` |
| Visual/Feel | 截图 + lead sign-off | `tests/evidence/` |
| UI | 手动走查或交互测试 | `tests/evidence/` |
| Config/Data | 冒烟检查通过 | `tests/smoke/critical-paths.md` |

## CI

每次 push 到 `main` 和 pull request 都会自动运行测试。
失败的测试套件阻止合并。

## 测试规则

- 纯逻辑测试不得依赖 MonoGame 的 `GraphicsDevice`
- 使用具名常量，不在测试断言中硬编码魔数
- 不得破坏已有测试
