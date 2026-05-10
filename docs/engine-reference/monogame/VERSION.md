# MonoGame 引擎版本参考

> **最后更新**: 2026-05-10
> **适用项目**: 酒馆与命运 / Tavern & Destiny (Roguelike DND 5e Pixel-Art RPG)

---

## 引擎版本

| 项目 | 版本号 |
|------|--------|
| **当前稳定版** | 3.8.4.1 |
| **最新预览版** | 3.8.5-preview.1 (2025-12 发布) |
| **项目锁定** | 3.8.5+ (NuGet 通配符 `3.8.*`) |
| **框架** | .NET 8+ |
| **C# 版本** | 12 |
| **NuGet 包** | `MonoGame.Framework.DesktopGL` |

> **注意**: 项目 `.csproj` 中版本号为 `3.8.*`，即自动使用最新的公开稳定版（不含预览版）。

---

## 知识截止点

本参考文档基于以下来源编写（截至 2026-05-10）：

| 来源 | URL |
|------|-----|
| 官方 API 文档 | https://docs.monogame.net/api/ |
| 3.8.x 迁移指南 | https://docs.monogame.net/articles/migration/migrate_38.html |
| 3.7→3.8 迁移指南 | https://docs.monogame.net/articles/migration/migrate_37.html |
| GitHub Release 3.8.5-preview.1 | https://github.com/MonoGame/MonoGame/releases/tag/v3.8.5-preview.1 |
| 官方博客 3.8.5 公告 | https://monogame.net/blog/2025-12-19-385-preview/ |

**AI 生成代码注意事项**：
- MonoGame 3.8 API 超出大部分 LLM 训练数据的截止日期
- 所有 API 签名**必须**通过官方文档验证，不得依赖模型记忆
- 本目录的文件可作为已验证的参考

---

## 3.7 → 3.8 重大变更

| 变更 | 旧版本 (3.7) | 新版本 (3.8+) |
|------|-------------|--------------|
| **项目格式** | 非 SDK 风格 `.csproj` | SDK 风格 `.csproj` |
| **安装方式** | MSI 安装器 | NuGet 包 + VS 扩展 |
| **MGCB 编辑器** | 全局安装的 Pipeline Tool | 本地 `.config/dotnet-tools.json` 管理 |
| **工具分发** | 系统级安装 | .NET Tools (`dotnet mgcb`) |
| **引用方式** | 程序集引用 + 提示路径 | `<PackageReference Include="MonoGame.Framework.DesktopGL">` |

## 3.8.x → 3.8.4.1+ 重要变更

| 变更 | 说明 |
|------|------|
| **MGCB 编辑器不再全局安装** | 改为项目本地 `.config/dotnet-tools.json` 管理 |
| **推荐 .NET 9** | 从 3.8.4 起推荐 .NET 9，但不是强制 |
| **`RestoreDotNetTools` 不再需要** | 从 3.8.4.1 起，`.csproj` 中的 `RestoreDotNetTools` 段可以安全删除 |
| **csproj 简化** | 从 3.8.4.1 起配置更简洁 |
| **StbSharp 图片加载** | `Texture2D.FromStream()` 使用 StbSharp，不再支持 TIF/DDS（DX 平台） |

## 3.8.5-preview.1 新增功能（尚未正式发布）

> 项目使用 `3.8.*` 通配符，但预览版不会自动使用。以下功能仅供参考。

- **DirectX 12 原生支持**（预览）
- **Vulkan 原生后端支持**（预览）
- **NetStandard 2.1 原生支持**
- **新的 Content Builder 解决方案**（替代 MGCB Editor）
- **新的 StartKit 模板**
- **新的 Random 实现**
- **扩展 GamePad 支持**（最多 8 个控制器）
- **HSL/HSV 颜色转换**
- **超过 100 项更新和修复**

### 已知预览版问题

- Content Pipeline 中自定义 Importer/Processor 的处理方式有破坏性变更
- 新 Pipeline 可能产生冗余的 `Content/` 子文件夹
- 扩展库（如 MonoGame.Extended）可能需要匹配预览版本

---

## 目标平台

| 平台 | 说明 |
|------|------|
| **DesktopGL** | 跨平台桌面（Windows/macOS/Linux），项目当前使用 |
| WindowsDX | 仅 Windows，使用 DirectX |
| Android/iOS | 移动端支持 |

---

## 关键 NuGet 包

```xml
<!-- 核心框架 -->
<PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.*" />

<!-- 内容构建工具 -->
<PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.*" />

<!-- dotnet 工具配置 (.config/dotnet-tools.json) -->
<!-- dotnet-mgcb, dotnet-mgcb-editor 版本必须与 MonoGame 版本匹配 -->
```

---

## 与项目其他依赖的兼容性

| 依赖 | 兼容版本 | 备注 |
|------|---------|------|
| MonoGame.Extended | 待确认 | 纹理图集、Tiled 地图等扩展功能 |
| FontStashSharp | MonoGame 3.8+ | 字体渲染 |
| GoRogue | 2.6.4 | 独立，不依赖 MonoGame |

> **重要**: 项目使用**自定义 ECS**（Scene/Entity/Component），而非 Nez。不要引用 Nez 的 MonoGame 包装 API。
