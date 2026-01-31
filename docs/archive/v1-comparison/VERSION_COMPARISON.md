# DistroNexus v1.0 (Go) vs v2.0 (WPF) 功能差异对比

> 文档生成日期: 2025-01-29
> 对比版本: v1.0 (Golang + Fyne) vs v2.0 (.NET 10 + WPF)

## 目录

1. [总览](#总览)
2. [主界面 - 顶部导航栏差异](#主界面---顶部导航栏差异)
3. [主界面 - 实例列表差异](#主界面---实例列表差异)
4. [实例操作按钮差异](#实例操作按钮差异)
5. [包管理器差异](#包管理器差异)
6. [设置界面差异](#设置界面差异)
7. [安装向导差异](#安装向导差异)
8. [高级功能差异](#高级功能差异)
9. [总结与建议](#总结与建议)

---

## 总览

### v1.0 (Golang + Fyne) 概述
- **技术栈**: Golang 1.22 + Fyne 2.7.2
- **架构模式**: 直接调用 PowerShell 脚本
- **UI 特点**: 跨平台 GUI 框架，界面较简单

### v2.0 (WPF + WPF-UI) 概述
- **技术栈**: .NET 10 + WPF-UI 4.2.0
- **架构模式**: MVVM + 依赖注入
- **UI 特点**: 原生 Windows 体验，Fluent Design 设计

---

## 主界面 - 顶部导航栏差异

### v1.0 (Golang) - 顶部工具栏按钮

| 按钮位置 | 按钮名称 | 图标 | 功能 |
|---------|---------|------|------|
| 左侧 | 首页 | HomeIcon | 切换到首页标签页 |
| 左侧 | 包管理 | StorageIcon | 切换到包管理标签页 |
| 中间分隔 | (分隔) | - | Spacer 用于布局 |
| 右侧 | 安装 | ContentAddIcon | 打开安装对话框（高亮显示） |
| 右侧 | 设置 | SettingsIcon | 打开设置对话框 |

**代码位置**: `src/internal/ui/mainwindow.go:94-119`

### v2.0 (WPF) - 顶部工具栏按钮

| 按钮位置 | 按钮名称 | 图标 | 功能 |
|---------|---------|------|------|
| 左侧 (仪表盘可见时) | Refresh | ArrowSync20 | 刷新实例列表 |
| 左侧 (仪表盘可见时) | Install New | Add20 | 打开安装向导（主按钮样式） |
| 左侧 (仪表盘可见时) | Package Manager | Apps20 | 导航到包管理页面 |
| 左侧 (仪表盘可见时) | Settings | Settings20 | 导航到设置页面 |
| 右侧 (始终可见) | 切换主题 | WeatherMoon20 | 在深色/浅色主题间切换 |
| 右侧 (始终可见) | 切换语言 | LocalLanguage20 | 在英文/中文语言间切换 |

**代码位置**: `src/Client/DistroNexus.Desktop/MainWindow.xaml:46-76`

### 差异总结

| 功能 | v1.0 | v2.0 | 差异说明 |
|------|------|------|---------|
| 首页按钮 | ✅ | ❌ | v2.0 使用 Dashboard 概念，无需独立首页按钮 |
| 包管理按钮 | ✅ | ✅ | 两者都有，v2.0 设计更现代 |
| 安装按钮 | ✅ | ✅ | 两者都有，v2.0 使用 "Install New" 更清晰 |
| 设置按钮 | ✅ | ✅ | 两者都有 |
| 刷新按钮 | ❌ | ✅ | v1.0 在首页标签页内，v2.0 在顶部工具栏 |
| **主题切换** | ❌ | ✅ | **v2.0 新增** |
| **语言切换** | ❌ | ✅ | **v2.0 新增** |

---

## 主界面 - 实例列表差异

### v1.0 (Golang) - 首页标签页

| UI 元素 | 位置 | 功能 |
|---------|------|------|
| 标题 "Installed Distributions" | 顶部 | 显示已安装发行版标题 |
| 刷新按钮 | 顶部右侧 | 强制刷新实例列表并更新目录 |
| 实例卡片列表 | 主内容区 | 显示每个 WSL 实例的详细信息 |
| 进度提示 | 刷新时显示 | 显示刷新进度 |

**代码位置**: `src/internal/ui/home_tab.go:62-120`

### v2.0 (WPF) - 仪表盘

| UI 元素 | 位置 | 功能 |
|---------|------|------|
| 实例卡片列表 | 主内容区 | 显示每个 WSL 实例的详细信息 |
| 空状态提示 | 无实例时显示 | 显示 "No WSL instances found" 提示 |
| 加载动画 | 后台刷新时显示 | ProgressRing + "Loading..." 文字 |
| 状态栏 | 底部 | 显示当前操作状态信息 |

**代码位置**: `src/Client/DistroNexus.Desktop/MainWindow.xaml:80-169`

### 差异总结

| 功能 | v1.0 | v2.0 | 差异说明 |
|------|------|------|---------|
| 实例列表展示 | ✅ | ✅ | 两者都有 |
| 刷新功能 | ✅ | ✅ | v1.0 在标签页内，v2.0 在工具栏 |
| 空状态提示 | ❌ | ✅ | **v2.0 新增**：无实例时显示友好提示 |
| 加载动画 | ✅ | ✅ | v1.0 使用 ProgressBarInfinite，v2.0 使用 ProgressRing |
| 状态栏 | ❌ | ✅ | **v2.0 新增**：底部状态栏显示操作信息 |
| 自动刷新 | ❌ | ✅ | **v2.0 新增**：每 10 秒自动刷新实例状态 |

---

## 实例操作按钮差异

### v1.0 (Golang) - 实例卡片按钮

每个实例卡片包含以下按钮（根据实例状态动态显示）:

| 按钮名称 | 图标 | 显示条件 | 功能 |
|---------|------|---------|------|
| Start | MediaPlayIcon | 实例未运行时 | 后台启动实例 |
| Terminal | ComputerIcon | 实例运行中 | 打开终端连接实例 |
| Stop | MediaStopIcon | 实例运行中 | 停止实例 |
| Move | StorageIcon | 实例未运行时 | 移动实例到新位置 |
| Rename | DocumentCreateIcon | 实例未运行时 | 重命名实例 |
| Set Credentials | AccountIcon | 实例未运行时 | 设置用户名和密码 |
| Delete | DeleteIcon | 实例未运行时 | 卸载/删除实例 |

**代码位置**: `src/internal/ui/home_tab.go:146-289`

### v2.0 (WPF) - 实例卡片按钮

每个实例卡片包含以下按钮（根据实例状态动态显示）:

| 按钮名称 | 图标 | 显示条件 | 功能 |
|---------|------|---------|------|
| Start | Play20 | 实例未运行时 | 启动实例 |
| Stop | Stop20 | 实例运行中 | 停止实例 |
| Terminal | WindowConsole20 | 始终可见 | 打开 Windows Terminal 连接实例 |
| More (下拉菜单) | MoreHorizontal20 | 始终可见 | 打开更多操作菜单，包含：
  - Move (ArrowMove20) | 菜单项 | 移动实例
  - Rename (Rename20) | 菜单项 | 重命名实例
  - Set Credentials (Key20) | 菜单项 | 设置凭据
  - Remove (Delete20) | 菜单项 | 删除实例 |

**代码位置**: `src/Client/DistroNexus.Desktop/MainWindow.xaml:123-148`

### 差异总结

| 功能 | v1.0 | v2.0 | 差异说明 |
|------|------|------|---------|
| Start 按钮 | ✅ | ✅ | 两者都有 |
| Stop 按钮 | ✅ | ✅ | 两者都有 |
| Terminal 按钮 | ✅ | ✅ | 两者都有 |
| Move 按钮 | ✅ | ✅ | v1.0 在卡片上，v2.0 在下拉菜单中 |
| Rename 按钮 | ✅ | ✅ | v1.0 在卡片上，v2.0 在下拉菜单中 |
| Set Credentials | ✅ | ✅ | v1.0 在卡片上，v2.0 在下拉菜单中 |
| Delete/Remove | ✅ | ✅ | 两者都有 |
| **更多操作菜单** | ❌ | ✅ | **v2.0 新增**：使用下拉菜单组织次要操作 |
| 操作可见性逻辑 | ✅ | ✅ | 两者都根据状态动态显示/隐藏按钮 |

---

## 包管理器差异

### v1.0 (Golang) - 包管理标签页

| 功能按钮 | 位置 | 功能 |
|---------|------|------|
| Update Sources | 顶部工具栏 | 从远程源更新发行包目录 |
| Download All | 顶部工具栏 | 下载所有未缓存的发行包 |
| Add Custom | 顶部工具栏 | 添加自定义包源 |
| Refresh List | 顶部工具栏右侧 | 刷新包列表显示 |
| Download | 包卡片（未缓存） | 下载单个发行包到缓存 |
| Install | 包卡片（已缓存） | 打开安装对话框预填充该包 |
| Redownload | 包卡片（已缓存） | 重新下载包（替换现有缓存） |
| Delete Cache | 包卡片（已缓存） | 删除缓存的包文件 |

**代码位置**: `src/internal/ui/package_manager_tab.go:19-277`

### v2.0 (WPF) - 包管理页面

| 功能按钮 | 位置 | 功能 |
|---------|------|------|
| Refresh | 顶部工具栏 | 刷新发行包目录 |
| Search Box | 顶部工具栏 | 搜索发行包（实时过滤） |
| Add Source | 顶部工具栏 | 打开自定义源添加面板 |
| Back | 顶部工具栏右侧 | 返回仪表盘 |
| Download | 包卡片（未缓存） | 下载包到缓存（Primary 样式） |
| Actions (下拉) | 包卡片（已缓存） | 包含：
  - Redownload (ArrowSync20) | 菜单项 | 重新下载包
  - Delete Cache (Delete20) | 菜单项 | 删除缓存 |
| Offline Mode Indicator | 顶部 | 显示离线模式状态徽章 |
| Add Source Panel | 底部可切换面板 | 输入自定义源 URL |

**代码位置**: `src/Client/DistroNexus.Desktop/Views/PackageManagerPage.xaml:1-225`

### 差异总结

| 功能 | v1.0 | v2.0 | 差异说明 |
|------|------|------|---------|
| 刷新目录 | ✅ | ✅ | 两者都有 |
| 搜索功能 | ❌ | ✅ | **v2.0 新增**：实时搜索过滤 |
| Download All | ✅ | ❌ | **v1.0 独有**：批量下载所有包 |
| 自定义源 | ✅ | ✅ | 两者都支持 |
| 单包下载 | ✅ | ✅ | 两者都有 |
| 安装按钮（已缓存） | ✅ | ❌ | **v1.0 独有**：包管理页直接安装，v2.0 需在仪表盘操作 |
| 重新下载 | ✅ | ✅ | 两者都有 |
| 删除缓存 | ✅ | ✅ | 两者都有 |
| **离线模式** | ❌ | ✅ | **v2.0 新增**：网络失败时自动切换离线模式 |
| **包分组** | ❌ | ✅ | **v2.0 新增**：按类别分组显示包 |
| **搜索框** | ❌ | ✅ | **v2.0 新增**：实时搜索过滤 |
| **徽章标识** | 部分有 | ✅ | v2.0 更完善：Cached/Online/Custom 徽章 |

---

## 设置界面差异

### v1.0 (Golang) - 设置对话框

| 设置项 | 说明 |
|--------|------|
| Default Install Path | 默认安装路径（带文件夹选择按钮） |
| Distro Cache Path | 发行包缓存路径（带文件夹选择按钮） |
| Default Quick Distro | 默认快速发行版 |
| Update Source URL | 更新源 URL |
| Default Terminal Path | 默认终端起始路径（带文件夹选择按钮） |
| **Reset to Defaults** | 重置为默认值按钮 |

**代码位置**: `src/internal/ui/settings.go:12-105`

### v2.0 (WPF) - 设置页面

设置页面分为多个卡片分组：

#### 1. General Settings (通用设置)
| 设置项 | 说明 |
|--------|------|
| Default Installation Path | 默认安装路径 |
| Package Cache Path | 包缓存路径 |
| Default WSL Version | WSL 版本选择（WSL 1 / WSL 2） |
| Default Username | 默认用户名 |
| Default Terminal Start Path | 默认终端起始路径 |
| Default Distribution | 默认发行版（下拉选择） |

#### 2. Appearance (外观设置)
| 设置项 | 说明 |
|--------|------|
| Theme | 主题选择（Light / Dark / Auto） |
| Language | 语言选择（en-US / zh-CN） |

#### 3. Download Settings (下载设置)
| 设置项 | 说明 |
|--------|------|
| Online Catalog Source URL | 在线目录源 URL |
| Max Concurrent Downloads | 最大并发下载数（1-10） |
| Max Retry Attempts | 最大重试次数（0-10） |
| Auto Retry Failed Downloads | 自动重试失败下载（开关） |

#### 4. Cache Management (缓存管理)
| 设置项 | 说明 |
|--------|------|
| Package Cache Location | 缓存位置显示 |
| Open Folder | 打开缓存文件夹 |
| Cached Packages | 已缓存包数量 |
| Total Size | 总缓存大小 |
| Refresh | 刷新缓存信息 |
| **Clear All Cache** | 清除所有缓存（Danger 样式） |
| Cached Files List | 已缓存文件列表（可逐个删除） |

#### 5. Behavior (行为设置)
| 设置项 | 说明 |
|--------|------|
| Enable Logging | 启用日志（开关） |
| Check for Updates on Startup | 启动时检查更新（开关） |
| Show Confirmation Dialogs | 显示确认对话框（开关） |
| Log File Path | 日志文件路径（只读） |

**代码位置**: `src/Client/DistroNexus.Desktop/Views/SettingsPage.xaml:1-321`

### 差异总结

| 功能 | v1.0 | v2.0 | 差异说明 |
|------|------|------|---------|
| 默认安装路径 | ✅ | ✅ | 两者都有 |
| 缓存路径 | ✅ | ✅ | 两者都有 |
| 更新源 URL | ✅ | ✅ | 两者都有 |
| 终端路径 | ✅ | ✅ | 两者都有 |
| 重置按钮 | ✅ | ✅ | 两者都有 |
| **主题设置** | ❌ | ✅ | **v2.0 新增**：Light/Dark/Auto |
| **语言设置** | ❌ | ✅ | **v2.0 新增**：en-US/zh-CN |
| **WSL 版本选择** | ❌ | ✅ | **v2.0 新增**：默认 WSL 1/2 |
| **默认用户名** | ❌ | ✅ | **v2.0 新增** |
| **并发下载** | ❌ | ✅ | **v2.0 新增**：可配置并发数 |
| **重试次数** | ❌ | ✅ | **v2.0 新增**：可配置重试次数 |
| **自动重试** | ❌ | ✅ | **v2.0 新增**：失败自动重试 |
| **缓存管理** | ❌ | ✅ | **v2.0 新增**：详细缓存统计和管理 |
| **清除缓存** | ❌ | ✅ | **v2.0 新增**：清除所有或单个缓存 |
| **日志设置** | ❌ | ✅ | **v2.0 新增**：启用/禁用日志 |
| **启动检查更新** | ❌ | ✅ | **v2.0 新增** |
| **确认对话框** | ❌ | ✅ | **v2.0 新增**：控制确认提示 |
| **设置分组** | ❌ | ✅ | **v2.0 新增**：分类卡片式设置界面 |
| **保存提示** | ❌ | ✅ | **v2.0 新增**：未保存更改提示 "*" |
| **设置持久化** | ✅ | ✅ | 两者都支持 |

---

## 安装向导差异

### v1.0 (Golang) - 安装对话框

| 步骤/字段 | 说明 |
|-----------|------|
| Distribution Family | 发行版家族下拉选择 |
| Version | 版本下拉选择 |
| Instance Name | 实例名称输入框 |
| Quick Mode Check | "Quick Mode (Root User, Default Path)" 复选框 |
| Quick Mode 时 | 隐藏详细字段，使用默认值 |
| Standard Mode 时显示：
  - Install Location | 安装路径（带文件夹选择按钮）
  - Username | 用户名输入框
  - Password | 密码输入框 |
| Install / Cancel 按钮 | 安装或取消 |

**特点**:
- 单页对话框
- Quick Mode 可快速跳过详细配置
- 标准模式需要填写所有字段
- 选择版本时自动填充默认实例名

**代码位置**: `src/internal/ui/install_dialog.go:17-222`

### v2.0 (WPF) - 安装向导

v2.0 使用 6 步向导框架（Workflow-based Wizard）:

| 步骤 | 名称 | 功能 |
|------|------|------|
| Step 1 | Select Distribution | 选择发行版和版本 |
| Step 2 | Install Path | 选择安装路径 |
| Step 3 | User Configuration | 配置用户名和密码 |
| Step 4 | Review | 审查安装选项 |
| Step 5 | Progress | 显示安装进度 |
| Step 6 | Result | 安装结果 |

**各步骤详细功能**:

#### Step 1: Select Distribution
- 发行版下拉选择
- 版本下拉选择
- 实例名称输入框（自动填充）
- Next / Cancel 按钮

#### Step 2: Install Path
- 安装路径输入框
- 浏览文件夹按钮
- 显示可用空间信息
- Previous / Next / Cancel 按钮

#### Step 3: User Configuration
- 用户名输入框
- 密码输入框
- 确认密码输入框
- 创建根用户选项
- Previous / Next / Cancel 按钮

#### Step 4: Review
- 显示所有安装选项摘要
- 发行版和版本
- 安装路径
- 用户配置
- Previous / Install / Cancel 按钮

#### Step 5: Progress
- 进度环（ProgressRing）
- 当前步骤文本
- 详细日志文本框
- 取消按钮（可取消安装）

#### Step 6: Result
- 成功/失败图标
- 结果消息
- 详细错误信息（如果失败）
- Finish 按钮

**特点**:
- 6 步流程化向导
- 每步可独立验证
- 支持取消和返回
- 详细进度显示
- 结果页总结

**代码位置**: `src/Client/DistroNexus.Desktop/Wizard/Steps/*.cs`

### 差异总结

| 功能 | v1.0 | v2.0 | 差异说明 |
|------|------|------|---------|
| 发行版选择 | ✅ | ✅ | 两者都有 |
| 版本选择 | ✅ | ✅ | 两者都有 |
| 实例名称 | ✅ | ✅ | 两者都有 |
| 安装路径 | ✅ | ✅ | 两者都有 |
| 用户名/密码 | ✅ | ✅ | 两者都有 |
| Quick Mode | ✅ | ❌ | **v1.0 独有**：快速模式 |
| **多步向导** | ❌ | ✅ | **v2.0 新增**：6 步流程 |
| **步骤导航** | ❌ | ✅ | **v2.0 新增**：Previous/Next |
| **审查步骤** | ❌ | ✅ | **v2.0 新增**：Review 显示摘要 |
| **进度步骤** | ✅ | ✅ | v1.0 使用阻塞对话框，v2.0 使用独立步骤 |
| **结果页** | ✅ | ✅ | 两者都有 |
| **取消安装** | ✅ | ✅ | 两者都支持 |
| **空间显示** | ❌ | ✅ | **v2.0 新增**：显示可用空间 |
| **确认密码** | ❌ | ✅ | **v2.0 新增**：双重密码验证 |
| **创建根用户** | ❌ | ✅ | **v2.0 新增**：选项控制 |

---

## 高级功能差异

### v1.0 (Golang) - 独有功能

| 功能 | 说明 |
|------|------|
| **Quick Mode** | 安装时快速模式，使用 root 用户和默认路径 |
| **Download All** | 一键下载所有未缓存的发行包 |
| **包管理页直接安装** | 已缓存包可直接从包管理页启动安装 |
| **自定义本地包** | 支持添加本地文件路径作为包源 |
| **扫描本地发行版** | ScanDistros 功能扫描本地 WSL 实例 |

### v2.0 (WPF) - 独有功能

| 功能 | 说明 |
|------|------|
| **主题切换** | Light/Dark/Auto 三种主题 |
| **多语言支持** | 英文/中文语言切换 |
| **离线模式** | 网络失败时自动切换离线模式 |
| **实时搜索** | 包管理页实时搜索过滤 |
| **包分组显示** | 按类别分组显示发行包 |
| **缓存管理** | 详细的缓存统计和管理界面 |
| **自动刷新** | 每 10 秒自动刷新实例状态 |
| **状态栏** | 底部状态栏显示操作信息 |
| **空状态提示** | 无实例时显示友好提示 |
| **徽章系统** | Cached/Online/Custom 状态徽章 |
| **进度环** | 现代化的加载动画 |
| **更多操作菜单** | 下拉菜单组织次要操作 |
| **详细日志** | Microsoft.Extensions.Logging 日志系统 |
| **设置分组** | 分类卡片式设置界面 |
| **保存提示** | 未保存更改提示 |
| **并发下载配置** | 可配置并发下载数 |
| **重试配置** | 可配置重试次数和自动重试 |
| **WSL 版本选择** | 默认 WSL 1/2 配置 |
| **日志控制** | 启用/禁用日志功能 |
| **启动检查更新** | 启动时自动检查更新 |
| **确认对话框控制** | 控制确认提示显示 |
| **MVVM 架构** | 现代化的 MVVM + DI 架构 |
| **单元测试** | xUnit 单元测试覆盖 |

---

## 总结与建议

### 功能对比矩阵

| 功能模块 | v1.0 功能数 | v2.0 功能数 | v1.0 独有 | v2.0 独有 |
|---------|------------|------------|----------|----------|
| 主界面导航 | 5 | 6 | 1 | 2 |
| 实例列表 | 3 | 5 | 0 | 2 |
| 实例操作 | 7 | 7 | 0 | 1 |
| 包管理器 | 8 | 10 | 2 | 4 |
| 设置界面 | 6 | 17 | 0 | 11 |
| 安装向导 | 6 | 12 | 1 | 6 |
| 高级功能 | 5 | 18 | 5 | 13 |
| **总计** | **40** | **75** | **9** | **39** |

### 关键发现

#### v1.0 独有的 9 项功能:
1. Quick Mode (快速安装模式)
2. Download All (批量下载)
3. 包管理页直接安装
4. 自定义本地包（路径）
5. 扫描本地发行版功能
6. 首页按钮
7. 单页安装对话框
8. 安装时默认名自动填充
9. 简单的进度对话框

#### v2.0 新增的 39 项功能:
1. 主题切换（3 种）
2. 多语言支持（2 种）
3. 离线模式
4. 实时搜索
5. 包分组显示
6. 缓存管理（7 项子功能）
7. 自动刷新
8. 状态栏
9. 空状态提示
10. 徽章系统
11. 更多操作菜单
12. 6 步向导框架
13. 步骤导航
14. 审查步骤
15. 空间显示
16. 确认密码
17. 创建根用户选项
18. 详细日志系统
19. 设置分组（5 个卡片）
20. 保存提示
21. 并发下载配置
22. 重试配置
23. WSL 版本选择
24. 日志控制
25. 启动检查更新
26. 确认对话框控制
27. MVVM 架构
28. 依赖注入
29. 单元测试
30. 现代化 UI 设计
31. Fluent Design
32. 进度环动画
33. 加载覆盖层
34. 错误处理
35. 全局异常捕获
36. 状态同步
37. 实例状态更新
38. 导航服务
39. 服务层抽象

### 建议补充到 v2.0 的 v1.0 功能

#### 高优先级:
1. **Quick Mode (快速安装模式)**
   - 原因: 简化快速安装流程，提高用户体验
   - 实现建议: 在安装向导 Step 1 添加 "Quick Mode" 复选框，跳过步骤 2-3

2. **包管理页直接安装**
   - 原因: 提升包管理页的功能完整性
   - 实现建议: 在已缓存包卡片添加 "Install" 按钮，预填充打开安装向导

#### 中优先级:
3. **Download All (批量下载)**
   - 原因: 方便一次性下载所有发行包用于离线环境
   - 实现建议: 在包管理页添加 "Download All" 按钮，确认后批量下载

4. **自定义本地包**
   - 原因: 支持导入本地已下载的发行包文件
   - 实现建议: 在包管理页添加 "Import Local Package" 功能

#### 低优先级:
5. **扫描本地发行版**
   - 原因: v2.0 已有自动刷新功能，手动扫描需求较低
   - 实现建议: 如果有特殊需求可添加强制重新扫描功能

### v2.0 相比 v1.0 的优势

1. **用户体验**: 现代化的 Fluent Design 界面，主题切换，多语言支持
2. **架构质量**: MVVM + DI，可测试，可维护，可扩展
3. **功能完整性**: 更详细的设置选项，缓存管理，离线模式
4. **错误处理**: 完善的日志系统，全局异常处理
5. **代码质量**: 单元测试，服务层抽象，类型安全

### 结论

v2.0 版本在功能和用户体验上显著优于 v1.0，新增了 39 项功能，而 v1.0 仅有的 9 项独特功能中，大部分可以通过合理的设计改进融入 v2.0。

**建议优先实现 Quick Mode 和包管理页直接安装**，这两个功能对用户体验提升最大，其他功能可以根据实际需求逐步补充。

---

## 附录

### 文件索引

#### v1.0 (Golang) 相关文件:
- `src/internal/ui/mainwindow.go` - 主窗口和工具栏
- `src/internal/ui/home_tab.go` - 首页实例列表
- `src/internal/ui/install_dialog.go` - 安装对话框
- `src/internal/ui/package_manager_tab.go` - 包管理器
- `src/internal/ui/settings.go` - 设置界面
- `src/internal/logic/wsl_manager.go` - WSL 管理逻辑
- `src/internal/logic/installer.go` - 安装逻辑
- `src/internal/config/loader.go` - 配置加载

#### v2.0 (WPF) 相关文件:
- `src/Client/DistroNexus.Desktop/MainWindow.xaml` - 主窗口
- `src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs` - 主视图模型
- `src/Client/DistroNexus.Desktop/Views/PackageManagerPage.xaml` - 包管理页面
- `src/Client/DistroNexus.Desktop/Views/SettingsPage.xaml` - 设置页面
- `src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs` - 包管理视图模型
- `src/Client/DistroNexus.Desktop/Wizard/Steps/*.cs` - 安装向导步骤
- `src/Client/DistroNexus.Core/Services/WslManagerService.cs` - WSL 管理服务
- `src/Client/DistroNexus.Core/Services/CatalogService.cs` - 目录服务

---

*本文档由 DistroNexus 项目自动生成*
*最后更新: 2025-01-29*
