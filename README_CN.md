# DistroNexus (发行版枢纽)

**中文** | [English](README.md)

> **🎉 版本 2.0 发布！** - 使用 .NET 10 + WPF 完全重写，为 Windows 提供原生体验。

**DistroNexus** 是一个现代化的 Windows 应用程序，用于管理 Windows Subsystem for Linux (WSL) 发行版。采用 .NET 10 和 WPF 构建，提供原生、直观的界面用于下载、安装和管理 WSL 实例。

## 📘 官方文档

完整的文档、使用指南和发布日志请访问我们的官方网站：
👉 **[https://lazyworkshop-create.github.io/DistroNexus/zh-Hans/](https://lazyworkshop-create.github.io/DistroNexus/zh-Hans/)**

## ✨ 功能特性

### 1.0 基础能力
*   **实例管理**：
    *   ✅ 启动/停止实例
    *   ✅ 移动到不同驱动器
    *   ✅ 重命名实例
    *   ✅ 移除实例
    *   ✅ 设置凭据
*   **自定义安装**：将 WSL 发行版安装到任意目录
*   **发行版目录**：从精选源浏览并下载发行版

### 2.0 新增能力
*   **原生 Windows UI**：采用 Fluent Design System 的现代 WPF 界面
*   **深色模式支持**：根据系统偏好自动切换主题
*   **PowerShell 模块**：15 个 cmdlet，支持自动化与脚本化工作流
    *   ✅ 可在应用内调用，也可在独立 PowerShell 会话中使用
    *   ✅ 支持 CI、环境初始化与批量管理的可重复操作
*   **模板系统**：内置模板可快速完成环境引导
    *   ✅ 覆盖常见语言运行时、容器与本地开发场景
    *   ✅ 支持参数化模板执行，满足不同环境定制需求
*   **包管理体验**：更完善的浏览与下载流程
*   **进度与日志**：实时操作进度与详细诊断信息

## 🚀 快速开始

### 需求
- Windows 10 版本 2004 或更新版本，或 Windows 11
- .NET 10 桌面运行时（安装程序已包含）
- WSL2 已启用（用于使用）

### 安装

#### 选项 1：安装程序（推荐）
1. 从 [Releases](https://github.com/lazyworkshop-create/DistroNexus/releases) 下载 `DistroNexus-2.0.1-Setup.exe`
2. 运行安装程序
3. 从开始菜单启动

#### 选项 2：便携版
1. 下载 `DistroNexus-v2.0.1-Release.zip`
2. 解压到任意文件夹
3. 运行 `DistroNexus.Desktop.exe`

#### 选项 3：自包含版（无需 .NET）
1. 下载 `DistroNexus-v2.0.1-Release-selfcontained.zip`
2. 解压到任意文件夹
3. 运行 `DistroNexus.Desktop.exe`

## 🛠️ PowerShell 模块

DistroNexus 2.0 包含用于自动化的 PowerShell 模块：

```powershell
# 导入模块
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"

# 列出所有实例
Get-DistroNexusInstance

# 安装自定义实例
Install-DistroNexusInstance -DistroName "MyUbuntu" -InstallPath "D:\WSL\MyUbuntu" -Username "admin"

# 启动实例
Start-DistroNexusInstance -DistroName "Ubuntu-22.04"
```

可用的 cmdlet：
- `Get-DistroNexusInstance` - 列出所有 WSL 实例
- `Start-DistroNexusInstance` - 启动实例
- `Stop-DistroNexusInstance` - 停止实例
- `Move-DistroNexusInstance` - 重定位实例
- `Rename-DistroNexusInstance` - 重命名实例
- `Remove-DistroNexusInstance` - 卸载实例
- `Install-DistroNexusInstance` - 自定义安装
- `Set-DistroNexusCredential` - 更新凭据
- `Get-DistroNexusPackage` - 浏览发行版
- `Save-DistroNexusPackage` - 下载包
- `Remove-DistroNexusPackage` - 删除缓存包
- `Update-DistroNexusCatalog` - 刷新目录
- `Get-DistroNexusTemplate` - 列出内置模板
- `Apply-DistroNexusTemplate` - 应用模板到实例
- `Invoke-DistroNexusTemplateAutomation` - 运行模板自动化流程

## 🧩 模板系统

DistroNexus 内置了模板系统，可将 WSL 实例快速配置为可直接使用的开发环境。

- 模板目录文件：`config/templates.json`
- 模板脚本资源：`config/templates/*`
- 主要命令：`Get-DistroNexusTemplate`、`Apply-DistroNexusTemplate`、`Invoke-DistroNexusTemplateAutomation`

### 快速使用

```powershell
# 列出所有模板
Get-DistroNexusTemplate

# 按分类筛选
Get-DistroNexusTemplate -Category "Development"

# 将模板应用到现有 WSL 实例
Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -TemplateId "python-dev" -Verbose

# 应用模板并传入运行时变量
Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -TemplateId "nodejs-dev" -Variables @{ NodeVersion = "20" }
```

### 模板自动化验证

```powershell
# 对指定模板执行 Dry Run
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds "python-dev","nodejs-dev" -Distro "Ubuntu-22.04" -DryRun

# 执行全部模板自动化（建议在受控测试环境中使用）
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro "Ubuntu-22.04"
```

### 安全提示

- 应用自定义模板时，可能会在目标发行版内执行 Shell 脚本。
- 执行前请先审查模板脚本，尤其是第三方/自定义模板。
- 在可用场景下优先使用 `-WhatIf` / `-Confirm` 进行安全预演。

## ⚙️ 配置

设置存储在 `%APPDATA%\DistroNexus\settings.json`：

```json
{
    "DefaultInstallPath": "C:\\WSL",
    "DefaultWslVersion": 2,
    "DefaultUsername": "root",
    "CatalogUrl": "https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/main/config/catalog.json",
    "Theme": "Auto",
    "EnableLogging": true
}
```

通过应用程序的设置页面配置，或直接编辑 JSON。

## 🏗️ 从源码构建

### 需求
- .NET 10 SDK
- PowerShell 7.0 或更新版本
- Windows 10/11

### 构建步骤

```powershell
# 克隆仓库
git clone https://github.com/lazyworkshop-create/DistroNexus.git
cd DistroNexus

# 使用提供的脚本构建
.\tools\build.ps1 -Configuration Release

# 或直接使用 dotnet CLI
dotnet build src/Client/DistroNexus.slnx -c Release
```

### 发布分发

```powershell
# 创建便携 ZIP 包（框架依赖）
.\tools\build.ps1 -Publish -CreateZip -Configuration Release

# 创建自包含包（无需 .NET 运行时）
.\tools\build.ps1 -Publish -SelfContained -CreateZip -Configuration Release

# 构建 Windows 安装程序（需要 Inno Setup）
.\tools\build-installer.ps1 -Version 2.0.1

# 输出将在 release/ 目录中
```

## 📁 项目结构

```
DistroNexus/
├── src/
│   ├── Client/
│   │   ├── DistroNexus.Desktop/          # WPF 应用程序
│   │   │   ├── Views/                    # XAML 视图
│   │   │   ├── ViewModels/               # ViewModels (MVVM)
│   │   │   ├── Converters/               # 值转换器
│   │   │   ├── Resources/                # 图像、图标
│   │   │   └── App.xaml                  # 应用入口
│   │   ├── DistroNexus.Core/             # 核心库
│   │   │   ├── Services/                 # 服务实现
│   │   │   ├── Models/                   # 数据模型
│   │   │   └── Interfaces/               # 服务接口
│   │   └── DistroNexus.Tests/            # 单元测试
│   └── PowerShell/
│       ├── Public/                       # 公开 cmdlet（15 个）
│       ├── Private/                      # 内部工具函数
│       ├── DistroNexus.psd1              # 模块清单
│       └── DistroNexus.psm1              # 模块脚本
├── config/
│   ├── catalog.json                      # 发行版目录
│   ├── templates.json                    # 模板元数据
│   └── templates/                        # 模板脚本资源
├── docs/                                 # 文档
│   ├── release_notes/                    # 版本发布说明
│   └── archive/                          # 历史文档和 v1 比对
├── tools/
│   ├── build_v2.ps1                      # 构建自动化
│   ├── build-installer.ps1               # 安装程序构建器
│   ├── package-portable.ps1              # 便携包创建器
│   └── packaging/                        # 安装程序资源
├── tests/                                # 测试套件
│   ├── PowerShell/                       # Pester 测试
│   ├── CSharp/                           # xUnit 测试
│   └── TestUtilities/                    # 共享测试工具
├── website/                              # Docusaurus 文档网站
├── README.md                             # 英文文档
└── README_CN.md                          # 中文文档
```

## 🔍 故障排查

### 应用程序无法启动
- 确保安装了 .NET 10 桌面运行时
- 检查 `%APPDATA%\DistroNexus\logs\` 中的错误信息
- 尝试以管理员身份运行

### PowerShell 模块相关问题
```powershell
# 验证模块路径
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1" -Verbose

# 检查模块状态
Get-Module DistroNexus
```

### WSL 实例相关问题
- 验证 WSL2 状态：`wsl --status`
- 检查 WSL 版本：`wsl --list --verbose`
- 更新 WSL：`wsl --update`

## 🤝 贡献

欢迎贡献！请直接在 GitHub 提交 Issue 或 Pull Request。

1. Fork 仓库
2. 创建功能分支（`git checkout -b feature/amazing-feature`）
3. 提交改动（`git commit -m 'feat: add amazing feature'`）
4. 推送分支（`git push origin feature/amazing-feature`）
5. 创建 Pull Request

## 📄 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。

## 🙏 致谢

- [WPF-UI](https://github.com/lepoco/wpfui) - 现代 Fluent Design 控件
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM 基础设施
- Microsoft WSL Team - 推动 Linux on Windows 生态发展

## 📞 支持

- 📖 [文档](https://lazyworkshop-create.github.io/DistroNexus/)
- 🐛 [问题反馈](https://github.com/lazyworkshop-create/DistroNexus/issues)
- 💬 [讨论区](https://github.com/lazyworkshop-create/DistroNexus/discussions)

---

**DistroNexus v2.0** - 以原生 .NET 性能与体验，在 Windows 上打造你的 Linux 开发环境。
