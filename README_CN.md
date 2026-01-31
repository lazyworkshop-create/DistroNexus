# DistroNexus (发行版枢纽)

**中文** | [English](README.md)

> **🎉 版本 2.0 发布！** - 使用 .NET 10 + WPF 完全重写，为 Windows 提供原生体验。

**DistroNexus** 是一个现代化的 Windows 应用程序，用于管理 Windows Subsystem for Linux (WSL) 发行版。采用 .NET 10 和 WPF 构建，提供原生、直观的界面用于下载、安装和管理 WSL 实例。

## 📘 官方文档

完整的文档、使用指南和发布日志请访问我们的官方网站：
👉 **[https://lazyworkshop-create.github.io/DistroNexus/zh-Hans/](https://lazyworkshop-create.github.io/DistroNexus/zh-Hans/)**

## ✨ 功能特性

### v2.0 亮点
*   **原生 Windows UI**：采用 Fluent Design System 的现代 WPF 界面
*   **深色模式支持**：根据系统偏好自动切换主题
*   **PowerShell 模块**：11 个 cmdlet 实现完整自动化能力
*   **包管理器**：浏览和下载 WSL 发行版目录
*   **一键操作**：单击启动、停止、移除实例
*   **进度跟踪**：下载和操作的实时进度显示

### 核心功能
*   **实例管理**：
    *   ✅ 启动/停止实例
    *   ✅ 移动到不同驱动器
    *   ✅ 重命名实例
    *   ✅ 移除实例
    *   ✅ 设置凭据
*   **自定义安装**：将 WSL 发行版安装到任意目录
*   **发行版目录**：浏览和下载精选的发行版
*   **设置管理**：全面的配置选项
*   **日志系统**：详细的日志便于故障排查

## 🚀 快速开始

### 需求
- Windows 10 版本 2004 或更新版本，或 Windows 11
- .NET 10 桌面运行时（安装程序已包含）
- WSL2 已启用（用于使用）

### 安装

#### 选项 1：安装程序（推荐）
1. 从 [Releases](https://github.com/lazyworkshop-create/DistroNexus/releases) 下载 `DistroNexus-2.0.0-Setup.exe`
2. 运行安装程序
3. 从开始菜单启动

#### 选项 2：便携版
1. 下载 `DistroNexus-v2.0.0-Release.zip`
2. 解压到任意文件夹
3. 运行 `DistroNexus.Desktop.exe`

#### 选项 3：自包含版（无需 .NET）
1. 下载 `DistroNexus-v2.0.0-Release-selfcontained.zip`
2. 解压到任意文件夹
3. 运行 `DistroNexus.Desktop.exe`

## 🛠️ PowerShell 模块

DistroNexus 2.0 包含用于自动化的 PowerShell 模块：

```powershell
# 导入模块
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"

# 列出所有实例
Get-WslInstance

# 安装自定义实例
Install-DistroNexusInstance -DistroName "MyUbuntu" -InstallPath "D:\WSL\MyUbuntu" -Username "admin"

# 启动实例
Start-WslInstance -DistroName "Ubuntu-22.04"
```

可用的 cmdlet：
- `Get-WslInstance` - 列出所有 WSL 实例
- `Start-WslInstance` - 启动实例
- `Stop-WslInstance` - 停止实例
- `Move-WslInstance` - 重定位实例
- `Rename-WslInstance` - 重命名实例
- `Remove-WslInstance` - 卸载实例
- `Install-DistroNexusInstance` - 自定义安装
- `Set-WslCredentials` - 更新凭据
- `Get-DistroNexusPackage` - 浏览发行版
- `Save-DistroNexusPackage` - 下载包
- `Update-DistroNexusCatalog` - 刷新目录

## ⚙️ 配置

设置存储在 `%APPDATA%\DistroNexus\settings.json`：

```json
{
    "DefaultInstallPath": "C:\\WSL",
    "DefaultWslVersion": 2,
    "DefaultUsername": "root",
    "CatalogUrl": "https://raw.githubusercontent.com/yourusername/DistroNexus/main/config/distros.json",
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
.\tools\build-installer.ps1 -Version 2.0.0

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
│       ├── Public/                       # 公开 cmdlet（11 个）
│       ├── Private/                      # 内部工具函数
│       ├── DistroNexus.psd1              # 模块清单
│       └── DistroNexus.psm1              # 模块脚本
├── config/
│   ├── distros.json                      # 发行版目录
│   └── settings.json                     # 默认设置
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
- 确保 Windows 10/11 已启用 WSL2

### PowerShell 模块相关问题
- 确保 PowerShell 7.0 或更新版本已安装
- 使用管理员权限运行 PowerShell
- 验证模块路径是否正确

## 📄 许可证

本项目采用 MIT 许可证。详见 [LICENSE](LICENSE) 文件。
