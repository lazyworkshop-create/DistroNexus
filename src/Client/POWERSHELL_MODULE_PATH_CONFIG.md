# PowerShell 模块路径配置功能实现总结

## 概述

实现了一个新功能，允许用户在 WPF 客户端中配置自定义 PowerShell 模块路径。此功能使用户能够在非标准位置使用 PowerShell 模块，同时保留自动检测功能作为后备选项。

## 实现的功能

### 1. 核心功能
- **自定义模块路径配置**: 用户可以通过设置页面指定 PowerShell 模块的位置
- **验证功能**: 在设置自定义路径时验证是否存在 `DistroNexus.psd1` 清单文件
- **自动检测后备**: 如果未设置自定义路径或路径无效，自动使用原有的检测逻辑
- **持久化存储**: 配置保存在 `GlobalSettings.json` 中，应用启动时加载

### 2. 用户界面
- 在设置页面添加了"Advanced Settings"部分
- 提供文件夹浏览对话框选择模块路径
- 显示清晰的说明文档，解释模块路径检测逻辑
- "Clear"按钮可清除自定义路径，恢复自动检测

## 修改的文件

### 1. `DistroNexus.Core/Models/GlobalSettings.cs`
```csharp
/// <summary>
/// Gets or sets the custom path to the PowerShell module.
/// If not set, the service will auto-detect the module path.
/// </summary>
public string? PowerShellModulePath { get; set; }
```

**改动说明**: 在全局设置模型中添加了 `PowerShellModulePath` 属性，用于存储用户配置的模块路径。

### 2. `DistroNexus.Core/Services/PowerShellService.cs`
**构造函数签名变更**:
```csharp
public PowerShellService(ILogger<PowerShellService> logger, string? customModulePath = null)
```

**功能增强**:
- 接受可选的 `customModulePath` 参数
- 如果提供了自定义路径，验证其有效性（检查 `DistroNexus.psd1` 是否存在）
- 自定义路径无效时，自动降级到原有的自动检测逻辑
- 记录详细的日志信息，方便调试

### 3. `DistroNexus.Desktop/App.xaml.cs`
**依赖注入配置变更**:
```csharp
// Register SettingsService first as it's needed for PowerShellService
services.AddSingleton<ISettingsService, SettingsService>();

// Register PowerShellService with factory to inject custom module path from settings
services.AddSingleton<IPowerShellService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PowerShellService>>();
    var settingsService = sp.GetRequiredService<ISettingsService>();
    
    // Try to load settings synchronously to get PowerShell module path
    string? customModulePath = null;
    try
    {
        var settings = settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        customModulePath = settings.PowerShellModulePath;
        
        if (!string.IsNullOrWhiteSpace(customModulePath))
        {
            logger.LogInformation("Loaded custom PowerShell module path from settings: {Path}", customModulePath);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to load settings during PowerShellService initialization. Using auto-detection.");
    }
    
    return new PowerShellService(logger, customModulePath);
});
```

**改动说明**: 
- 调整服务注册顺序，确保 `SettingsService` 在 `PowerShellService` 之前注册
- 使用工厂模式创建 `PowerShellService`，在实例化时从设置中读取模块路径
- 增加错误处理，确保即使读取设置失败也能正常初始化

### 4. `DistroNexus.Desktop/ViewModels/SettingsViewModel.cs`
**新增属性**:
```csharp
[ObservableProperty]
private string? _powerShellModulePath;
```

**新增命令**:
```csharp
[RelayCommand]
private void BrowsePowerShellModulePath()
{
    // 打开文件夹选择对话框
    // 验证所选目录包含 DistroNexus.psd1
    // 如果有效则设置 PowerShellModulePath
}

[RelayCommand]
private void ClearPowerShellModulePath()
{
    // 清除自定义路径，恢复自动检测
}
```

**修改方法**:
- `LoadSettingsAsync()`: 加载 `PowerShellModulePath`
- `SaveSettingsAsync()`: 保存 `PowerShellModulePath`

### 5. `DistroNexus.Desktop/Views/SettingsPage.xaml`
**新增 UI 部分**:
```xaml
<!-- Advanced Settings -->
<ui:Card Margin="0,0,0,15">
    <StackPanel>
        <TextBlock Text="Advanced Settings" FontSize="16" FontWeight="SemiBold"/>
        
        <TextBlock Text="PowerShell Module Path (Optional)"/>
        <TextBlock Text="Leave empty to use automatic detection..."/>
        
        <Grid>
            <ui:TextBox Text="{Binding PowerShellModulePath}"
                       PlaceholderText="Auto-detect module path"/>
            <ui:Button Content="Browse..." Command="{Binding BrowsePowerShellModulePathCommand}"/>
            <ui:Button Content="Clear" Command="{Binding ClearPowerShellModulePathCommand}"/>
        </Grid>
        
        <ui:InfoBar Title="Module Path Configuration">
            <!-- 显示模块路径检测逻辑的说明 -->
        </ui:InfoBar>
    </StackPanel>
</ui:Card>
```

## 工作流程

### 应用启动流程
1. **App.xaml.cs OnStartup**:
   - 配置依赖注入容器
   - 注册 `SettingsService`
   - 使用工厂方法创建 `PowerShellService`:
     - 从 `SettingsService` 读取 `PowerShellModulePath`
     - 将自定义路径传递给 `PowerShellService` 构造函数

2. **PowerShellService 初始化**:
   - 如果提供了 `customModulePath`:
     - 验证路径中是否存在 `DistroNexus.psd1`
     - 有效: 使用自定义路径
     - 无效: 记录警告，降级到自动检测
   - 如果未提供 `customModulePath`:
     - 使用原有的自动检测逻辑（检查环境变量、开发路径、安装路径等）

### 用户配置流程
1. 用户打开"Settings"页面
2. 滚动到"Advanced Settings"部分
3. 点击"Browse..."按钮
4. 选择包含 `DistroNexus.psd1` 的目录
5. 如果目录有效，路径显示在文本框中
6. 点击"Save Settings"保存配置
7. 应用需要重启以使新配置生效

### 清除自定义路径
1. 用户在"Advanced Settings"中点击"Clear"按钮
2. `PowerShellModulePath` 设置为 `null`
3. 点击"Save Settings"保存
4. 重启应用后，将使用自动检测逻辑

## 自动检测逻辑（未设置自定义路径时）

PowerShellService 按以下顺序搜索模块：

1. **环境变量**: `DISTRONEXUS_MODULE_PATH`
2. **开发路径**:
   - `{AppDirectory}\..\..\..\..\..\src\PowerShell\DistroNexus.psd1`
   - `{AppDirectory}\..\..\..\..\src\PowerShell\DistroNexus.psd1`
   - `{AppDirectory}\..\..\..\..\..\PowerShell\DistroNexus.psd1` (legacy)
   - `{AppDirectory}\..\..\..\..\PowerShell\DistroNexus.psd1` (legacy)
3. **安装路径**:
   - `%ProgramFiles%\DistroNexus\PowerShell\DistroNexus.psd1`
   - `%LOCALAPPDATA%\DistroNexus\PowerShell\DistroNexus.psd1`
   - `%USERPROFILE%\Documents\PowerShell\Modules\DistroNexus\DistroNexus.psd1`
4. **PowerShell Gallery**:
   - `%USERPROFILE%\Documents\PowerShell\Modules\DistroNexus\`

## 日志输出示例

### 使用自定义路径（有效）
```
[Info] PowerShell service initialized using: C:\Program Files\PowerShell\7\pwsh.exe
[Info] Using custom PowerShell module path from configuration: D:\Custom\Location\PowerShell
[Info] Custom module path validated successfully
```

### 使用自定义路径（无效，降级）
```
[Info] PowerShell service initialized using: C:\Program Files\PowerShell\7\pwsh.exe
[Warning] Custom module path is invalid (manifest not found): D:\Invalid\Path\DistroNexus.psd1. Falling back to auto-detection.
[Info] DistroNexus module detected at: D:\wsl\DistroNexus\src\PowerShell
```

### 使用自动检测
```
[Info] PowerShell service initialized using: C:\Program Files\PowerShell\7\pwsh.exe
[Info] DistroNexus module detected at: D:\wsl\DistroNexus\src\PowerShell
```

## 使用场景

### 场景 1: 开发环境
开发人员可能在多个项目分支工作，可以为每个分支配置不同的模块路径，无需修改环境变量。

### 场景 2: 企业部署
IT 管理员可以将模块部署到网络共享位置，然后配置所有客户端使用该路径。

### 场景 3: 便携版本
创建便携式安装包时，可以预配置相对于应用程序的模块路径。

### 场景 4: 测试不同版本
QA 团队可以轻松切换不同版本的 PowerShell 模块进行测试。

## 向后兼容性

- 现有用户升级后，`PowerShellModulePath` 默认为 `null`
- 应用将继续使用自动检测逻辑，行为与之前版本完全相同
- 不会影响现有的配置或工作流程

## 配置文件格式

`%LOCALAPPDATA%\DistroNexus\settings.json`:
```json
{
  "DefaultInstallPath": "C:\\WSL",
  "Theme": "Dark",
  "Language": "en-US",
  "PowerShellModulePath": "D:\\CustomLocation\\PowerShell",
  ...
}
```

如果 `PowerShellModulePath` 为 `null` 或不存在，将使用自动检测。

## 测试建议

### 单元测试
1. PowerShellService 使用有效自定义路径初始化
2. PowerShellService 使用无效自定义路径，降级到自动检测
3. SettingsViewModel 验证模块路径功能
4. BrowsePowerShellModulePath 命令拒绝无效路径

### 集成测试
1. 配置自定义路径，重启应用，验证模块加载
2. 清除自定义路径，重启应用，验证自动检测
3. 设置无效路径，验证降级行为

### 手动测试清单
- [ ] 打开设置页面，查看 Advanced Settings 部分
- [ ] 点击 Browse 按钮，选择有效的模块目录
- [ ] 验证路径在文本框中显示
- [ ] 保存设置，检查 settings.json 文件
- [ ] 重启应用，检查日志确认使用了自定义路径
- [ ] 清除路径，保存，重启应用
- [ ] 验证应用恢复到自动检测
- [ ] 尝试设置无效路径，确认显示错误提示

## 未来增强

1. **实时生效**: 提供"Apply"按钮，无需重启即可切换模块路径
2. **路径历史**: 记录最近使用的模块路径，便于快速切换
3. **模块版本检测**: 显示当前加载的模块版本号
4. **健康检查**: 添加"Test Module"按钮验证模块是否可用
5. **导入/导出**: 支持导出/导入完整配置，包括模块路径

## 相关文档

- [DIAGNOSTIC_GUIDE.md](./DIAGNOSTIC_GUIDE.md) - PowerShell 模块诊断指南
- [FIX_SUMMARY.md](./FIX_SUMMARY.md) - 模块路径检测修复总结

## 贡献者指南

修改模块路径相关代码时，请确保：

1. 保持向后兼容性（自定义路径为 null 时使用自动检测）
2. 记录详细的日志信息
3. 验证用户输入（检查清单文件是否存在）
4. 更新相关文档
5. 添加适当的错误处理
