# DistroNexus v2.0.1 正式发布：从“能用”到“高效可复用”的 WSL 工作台

如果你长期在 Windows 上使用 WSL，你应该很熟悉这些问题：
- 环境搭建重复、耗时且容易漂移；
- 多项目并行时，实例管理不够直观；
- 团队协作时，很难把“我的本地环境”稳定复制给别人。

近期，我们正式发布 **DistroNexus v2.0.1**。这是一次从底层到体验的完整升级，不只是"加了几个功能"，而是把 WSL 管理能力升级为一套现代化、可自动化的工程化工作台。

---

## 这次升级，核心变化是什么？

### 1) 架构级重写：.NET 10 + WPF
v2.0.1 从 v1.x（最新 v1.0.2）演进为全新的 .NET 10 + WPF 桌面架构：
- UI 从 Go/Fyne 迁移到原生 WPF，采用 Fluent Design 风格；
- 支持深色模式，操作过程提供更清晰的进度与状态反馈；
- 采用 MVVM（CommunityToolkit.Mvvm）+ 依赖注入；
- 长耗时操作走异步模型，交互体验更流畅。

这意味着：可维护性、可扩展性、Windows 端一致性都上了一个台阶。

### 2) PowerShell 模块平台：15 个 Cmdlet
v2.0.1 将核心能力沉淀为模块化命令平台（15 个 Cmdlet），覆盖：
- 实例生命周期（安装/启动/停止/迁移/重命名/移除）；
- 包与目录管理（下载缓存、更新目录元数据）；
- 模板查询与应用；
- 模板自动化验证工作流。

你可以在 GUI 里完成常规操作，也可以在脚本/CI 中复用同一套能力。

### 3) 内置模板系统：环境初始化从“手工”走向“标准化”
内置模板系统现已成为一等能力，当前提供 15 套模板，覆盖：
- Development 开发环境（.NET / Node.js / Python / Docker / Fullstack / Rust / Go 等）
- Platform 平台开发（Java/JVM）
- Cloud Native 云原生（容器运行时 / 本地 Kubernetes）
- Database 数据库（本地数据库栈）
- Data & AI 数据与 AI（AI/ML GPU 加速开发）

对个人开发者，它能减少重复配置；
对团队协作，它能有效消除"在我机器上能跑"的环境偏差问题。

### 4) 诊断与文档体系同步升级
- 日志能力在桌面端与 PowerShell 工作流间统一，日志路径：`%APPDATA%\DistroNexus\logs`；
- 产品与文档均提供英文 + 简体中文；
- 关键安装、配置、模板文档已经完成同步更新。

---

## 文档与生态同步就绪

v2.0.1 不只是"发了版本"，配套文档和工具链也已同步到位：
- 官网与中英文文档已全面更新至 v2 基线，开箱即可查阅；
- 模板系统文档按需求、架构、使用、开发、测试多层级完善，适配不同角色的阅读需求；
- Installer / Portable / Self-contained 三种安装方式均已验证可用。

**你拿到手的不仅是一个可运行的工具，也是一套完整的文档与参考体系。**

---

## 适合哪些人使用？

- 希望把 WSL 实例管理可视化、规范化的个人开发者；
- 需要快速初始化多语言开发环境的团队；
- 希望把本地环境管理纳入自动化流程的工程团队；
- 需要在 Windows 上长期维护多个隔离 Linux 工作区的用户。

---

## 如何开始

你可以按自己的使用习惯选择安装方式：
- **Installer（安装版）**：`DistroNexus-2.0.1-Setup.exe` — 标准 Windows 安装向导，自动处理依赖。
- **Portable（便携版）**：`DistroNexus-v2.0.1-Release.zip` — 解压即用，需已安装 .NET 10 Desktop Runtime。
- **Self-contained（独立版）**：`DistroNexus-v2.0.1-Release-selfcontained.zip` — 内置运行时，适合未安装 .NET Runtime 的机器，解压即用。

**下载地址**：<https://github.com/lazyworkshop-create/DistroNexus/releases/tag/v2.0.1>

系统要求：Windows 10 2004+ 或 Windows 11，启用 WSL2。

---

## 相关链接

- GitHub：<https://github.com/lazyworkshop-create/DistroNexus>
- 文档站：<https://lazyworkshop-create.github.io/DistroNexus/>
- 发布日志（中文）：[v2.0.1.zh-CN.md](https://github.com/lazyworkshop-create/DistroNexus/blob/main/docs/release_notes/v2.0.1.zh-CN.md)
- 发布日志（英文）：[v2.0.1.md](https://github.com/lazyworkshop-create/DistroNexus/blob/main/docs/release_notes/v2.0.1.md)

---

## 关于 AI 在这个项目中的角色

值得一提的是：**DistroNexus v2.0.1 几乎全部由 AI 完成端到端的执行工作**——从需求调研、架构设计、编码开发、测试验证到发布交付，包括文档体系和这篇发布日志本身。

这不是"用 AI 辅助写了几段代码"，而是 AI 承担了绝大部分工程执行，让我得以把精力投入到产品方向和最终产出上——这恰好是我一直想做但此前没有时间做的事。

如果你也对 AI 驱动的工程实践感兴趣，欢迎交流。

---

## 仓库概览

| 指标 | 数据 |
|---|---|
| 总提交数 | 205 |
| 源码行数 | ~31,000 行（C# / PowerShell / XAML / Shell） |
| 语言构成 | C# 66.6%，PowerShell 29.8%，Shell 2%，其他 1.6% |
| 源码文件 | C# 119 个，PowerShell 49 个，XAML 19 个 |
| 文档文件 | Markdown 135 篇 |
| 已发布版本 | 3 个（v1.0.1 → v1.0.2 → v2.0.1） |
| 开源协议 | MIT |
| 首次提交 | 2026-01-22 |
| v2.0.1 发布 | 2026-01-31 |

---

## 最后

v2.0.1 是 DistroNexus 的一个新起点。
如果你正好在 Windows + WSL 上做开发，欢迎下载体验，也欢迎通过 Issue 告诉我们你最想要的下一步能力。

如果这篇文章对你有帮助，欢迎转发给同样在折腾本地开发环境的朋友。🙏

标签：WSL ｜ Windows 开发 ｜ 开发效率 ｜ 开源工具 ｜ AI 驱动开发 ｜ DistroNexus
