---
sidebar_position: 4
---

# 配置

虽然大多数设置可以通过 GUI 进行管理，但高级用户可以直接修改配置文件。

全局设置存储在 `config/settings.json` 中。

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "PackageCachePath": "D:\\WSL_Cache",
    "DefaultTerminalStartPath": "~",
    "DefaultDistro": "Ubuntu-24.04"
}
```

## 设置参考

| 键 (Key) | 描述 | 默认值 |
| :--- | :--- | :--- |
| `DefaultInstallPath` | 如果未在安装期间提供自定义路径，发行版将被安装到的根目录。 | `D:\WSL` |
| `PackageCachePath` | 存储下载的离线安装包 (`.appx`, `.appxbundle`) 的目录。 | `D:\WSL_Cache` |
| `DefaultTerminalStartPath` | 打开终端时的默认启动目录。使用 `~` 表示 Linux 主目录，或 `/mnt/c/` 表示 Windows C 盘。 | `~` |
| `DefaultDistro` | 用于“快速模式”安装的发行版标识符。 | `Ubuntu-24.04` |

## 发行版定义

可用发行版列表维护在 `config/distros.json` 中。此文件会自动更新，但也可以编辑以添加自定义源。
