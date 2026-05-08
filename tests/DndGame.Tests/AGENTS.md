# 测试知识库

xUnit + FluentAssertions 测试套件。无模拟框架——使用手写测试替身。

## 结构

```
tests/DndGame.Tests/
├── DndGame.Tests.csproj       # .NET 8，xUnit 2.*, FluentAssertions 7.*
└── Unit/
    ├── EventBusTests.cs       # 核心 EventBus
    ├── ServiceLocatorTests.cs # 核心 ServiceLocator
    ├── Combat/               # 战斗系统测试（7 个文件）
    │   ├── DiceRollerTests.cs
    │   ├── CombatFSMTests.cs
    │   ├── ActionResolverTests.cs
    │   ├── ConditionSystemTests.cs
    │   ├── AISystemTests.cs
    │   ├── GoRogueMapManagerTests.cs
    │   └── DungeonGeneratorTests.cs
    ├── Character/             # 角色系统测试（2 个文件）
    │   ├── CharacterGeneratorTests.cs
    │   └── CharacterDataTests.cs
    ├── Gateway/               # LLM 网关测试（1 个文件，多个类）
    │   └── GatewayTests.cs
    └── Integration/           # (空——已规划）
```

## 何处查找

| 需求 | 位置 | 备注 |
|------|------|------|
| 测试命名约定 | 任意测试文件 | `MethodName_Scenario_ExpectedResult` |
| 测试替身示例 | `Combat/ActionResolverTests.cs` | `private class TestCombatant : ICombatant` |
| 流畅断言模式 | 任意测试文件 | `result.Should().Be()`、`action.Should().Throw<T>()` |
| AAA 模式示例 | `ServiceLocatorTests.cs` | 标准 // Arrange / // Act / // Assert |
| 全局状态重置 | `ServiceLocatorTests.cs` | 构造函数中的 `ServiceLocator.Reset()` |

## 约定

- **命名**：`MethodName_Scenario_ExpectedResult`（PascalCase，xUnit `[Fact]`）
- **AAA 模式**：始终使用 `// Arrange` / `// Act` / `// Assert` 区域标记
- **断言**：FluentAssertions（`Should().Be()`、`Should().Throw<T>()`、`Should().HaveCount()`）
- **测试替身**：手写 `private class` 实现接口（无 Moq/NSubstitute）
- **无模拟框架**——依赖项使用手写替身。接受这种权衡以保持简单。
- **概率测试**：多次循环（100–10,000 次迭代），失败时调用 `Assert.Fail()`
- **XML 文档注释**：测试类和方法使用简体中文的 `/// <summary>`
- **命名空间**：`DndGame.Tests.Unit.{模块}` 对应源命名空间 `DndGame.{模块}`
- **无共享夹具**——每项测试自包含。无 `IClassFixture<T>` 或 `ICollectionFixture<T>`
- **切勿**调用 `ServiceLocator.Reset()` 除非你的测试污染了全局状态

## 反模式

- ❌ 测试依赖 MonoGame 的 `GraphicsDevice` —— 纯逻辑测试不得触碰图形
- ❌ 测试名称中的魔法数字——使用命名常量
- ❌ 通过删除失败测试来"通过"——修复代码，而非测试
- ❌ 在测试代码中注释掉断言——删除或修复
- ❌ 测试间共享可变状态（除非在构造函数中显式重置）

## 运行命令

```bash
# 全部测试
dotnet test

# 筛选（按方法名）
dotnet test --filter "FullyQualifiedName~CombatEngineTests"

# 筛选（按文件）
dotnet test --filter "ClassName=DiceRollerTests"
```

## 注释

- **已存在 13 个测试文件，115 个以上测试方法，100% 通过率**。
- **TestCombatant 在 ActionResolverTests.cs 和 AISystemTests.cs 中重复**——存在提取到共享 `TestHelpers/` 目录的机会。
- **`.claude/rules/test-standards.md` 引用的是 GDScript snake_case 约定**——请忽略。实际 C# 代码使用 PascalCase `MethodName_Scenario_ExpectedResult`。
- **测试项目缺少 `TreatWarningsAsErrors`**（主项目已设置）——测试中的警告不会导致构建失败。
- **无模拟库**——随着复杂性增长，考虑添加 Moq 或 NSubstitute。
