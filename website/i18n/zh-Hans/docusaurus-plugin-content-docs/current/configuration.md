---
sidebar_position: 4
---

# 配置

大多数设置可在桌面界面中管理，高级用户也可以直接编辑设置文件。

全局设置存储在 `%APPDATA%\\DistroNexus\\settings.json`。

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "PackageCachePath": "D:\\WSL\\packages",
    "TerminalStartPath": "~",
    "DefaultWslVersion": 2,
    "DefaultUsername": "root",
    "DefaultDistributionId": "Ubuntu-24.04",
    "EnableLogging": true,
    "CatalogUrl": "https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/main/config/catalog.json",
    "Theme": "Auto"
}
```

## 设置参考

| 键 (Key) | 描述 | 默认值 |
| :--- | :--- | :--- |
| `DefaultInstallPath` | 如果未在安装期间提供自定义路径，发行版将被安装到的根目录。 | `D:\WSL` |
| `PackageCachePath` | 存储下载离线安装包 (`.appx`, `.appxbundle`) 的目录。 | `D:\WSL\packages` |
| `TerminalStartPath` | 打开终端时的默认启动目录。可使用 `~` 或绝对路径。 | `~` |
| `DefaultWslVersion` | 新安装实例默认使用的 WSL 版本。 | `2` |
| `DefaultUsername` | 新实例初始化时使用的默认用户名。 | `root` |
| `DefaultDistributionId` | 快速安装默认使用的发行版标识符。 | `Ubuntu-24.04` |
| `EnableLogging` | 是否启用日志与诊断输出。 | `true` |
| `CatalogUrl` | 发行版目录源地址。 | `https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/main/config/catalog.json` |
| `Theme` | 主题偏好（`Light`、`Dark`、`Auto`）。 | `Auto` |

## 说明

目录与模板元数据由 DistroNexus 运行时流程维护。用户侧建议优先通过 `settings.json` 进行配置。
