# DistroNexus 2.0 缺失功能开发计划

> **版本**: 1.0 | **日期**: 2026-01-29  
> **目标**: 将 1.0 (Golang) 已实现但 2.0 (WPF) 缺失的核心功能补充完整

---

## 📋 执行摘要

### 优先级与工作量

| 功能 | 优先级 | 复杂度 | 开发 | 测试 | 总计 |
|------|--------|--------|------|------|------|
| **0. 现有代码架构修复** | 🟡 **建议** | 低 | 0.5-1天 | 0.5天 | **1-2天** |
| 1. 后台下载任务管理系统 | 🔴 P0 | 中 | 3-4天 | 1-2天 | 4-6天 |
| 2. 从缓存快速安装 | 🔴 P0 | 中 | 2-3天 | 1天 | 3-4天 |
| 3. 安装向导快速模式 | 🔴 P0 | 中 | 3-4天 | 1-2天 | 4-6天 |
| 4. 安装进度实时日志 | 🟡 P1 | 中-高 | 4-5天 | 2天 | 6-7天 |
| 5. 独立的更新源功能 | 🟡 P1 | 低 | 1-2天 | 0.5天 | 1.5-2.5天 |
| 6. 设置自动保存 | 🟢 P2 | 低 | 1天 | 0.5天 | 1.5天 |
| **总计（含 Phase 0）** | - | - | **15-20天** | **6.5-8.5天** | **21-29天** |
| **总计（不含 Phase 0）** | - | - | **14-19天** | **6-8天** | **20-27天** |

### 开发阶段建议

**推荐路径**：
```
Phase 0（可选）→ 功能 1-3（P0 核心功能）→ 功能 4-5（P1 体验增强）→ 功能 6（P2 便利功能）
```

**快速路径**（跳过 Phase 0）：
```
直接实施功能 1-6，在每个 PR 中逐步修复 Phase 0 的问题
```

---

## 🏗️ 架构实现规则

### PowerShell 模块调用规范

**核心原则**：WPF 客户端仅作为用户界面层，所有与操作系统及 WSL2 的交互**必须**通过 PowerShell 模块进行。

#### 1. 禁止的实现方式 ❌

```csharp
// ❌ 禁止：在 ViewModel 中直接调用系统 API
public class SomeViewModel
{
    void DoSomething()
    {
        // 禁止直接使用 Process.Start
        Process.Start("wsl.exe", "--list");
        
        // 禁止直接访问注册表
        Registry.GetValue(...);
        
        // 禁止直接操作文件系统（除非是 UI 验证逻辑）
        File.Delete(somePath);
        Directory.CreateDirectory(somePath);
        
        // 禁止使用 P/Invoke
        [DllImport("kernel32.dll")]
        extern static ...;
    }
}
```

#### 2. 正确的实现方式 ✅

```csharp
// ✅ 正确：所有系统交互都通过服务层
public class SomeViewModel
{
    private readonly IWslManagerService _wslManager;
    private readonly IPowerShellService _powerShell;
    private readonly IFileSystemService _fileSystem; // 如需要
    
    public SomeViewModel(
        IWslManagerService wslManager,
        IPowerShellService powerShell,
        IFileSystemService fileSystem)
    {
        _wslManager = wslManager;
        _powerShell = powerShell;
        _fileSystem = fileSystem;
    }
    
    async Task DoSomethingAsync()
    {
        // 通过服务层调用
        var instances = await _wslManager.GetInstancesAsync();
        
        // 或者直接执行 PowerShell 脚本
        var script = @"
            # 所有 wsl.exe 调用都在 PowerShell 脚本中
            wsl --list --verbose
        ";
        var result = await _powerShell.ExecuteScriptAsync(script);
    }
}
```

#### 3. 服务层实现规范

```csharp
// DistroNexus.Core/Services/SomeService.cs
public class SomeService : ISomeService
{
    private readonly IPowerShellService _powerShell;
    private readonly ILogger<SomeService> _logger;
    
    public SomeService(IPowerShellService powerShell, ILogger<SomeService> logger)
    {
        _powerShell = powerShell;
        _logger = logger;
    }
    
    public async Task<Result> DoOperationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 将系统操作封装为 PowerShell 脚本
            var script = @"
                # 在 PowerShell 中执行所有系统调用
                $wslInfo = wsl --list --verbose
                
                # 读取注册表
                $regPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss'
                $lxssData = Get-ItemProperty -Path $regPath
                
                # 文件操作
                if (Test-Path $somePath) {
                    Remove-Item -Path $somePath -Force
                }
                
                # 返回 JSON 格式结果便于解析
                @{
                    Success = $true
                    Data = $wslInfo
                } | ConvertTo-Json
            ";
            
            var result = await _powerShell.ExecuteScriptAsync(script, cancellationToken);
            return ParseResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
            throw;
        }
    }
}
```

#### 4. 允许的例外情况

**以下场景可以在 ViewModel 中直接使用 .NET API**：

| 场景 | 示例 | 理由 |
|-----|------|------|
| **UI 输入验证** | `Path.IsPathRooted()`, `Directory.Exists()` | 即时反馈，不涉及业务逻辑 |
| **本地配置读写** | `File.ReadAllText(configPath)` | 通过 `ISettingsService` 封装 |
| **临时文件操作** | `Path.GetTempFileName()` | 纯本地 UI 需求 |
| **环境变量读取** | `Environment.GetFolderPath()` | 跨平台标准 API |

**但强烈建议**：即使是上述场景，也应优先考虑封装为服务方法，以提高可测试性。

#### 5. 架构分层图

```
┌──────────────────────────────────────────────────────┐
│              WPF UI Layer (Desktop)                  │
│  ┌────────────────────────────────────────────┐     │
│  │         ViewModels (MVVM)                  │     │
│  │  - 只包含 UI 逻辑和数据绑定                │     │
│  │  - 禁止直接系统调用                        │     │
│  │  - 通过依赖注入获取服务                     │     │
│  └─────────────────┬──────────────────────────┘     │
└────────────────────┼────────────────────────────────┘
                     │ DI (接口调用)
┌────────────────────▼────────────────────────────────┐
│            Service Layer (Core)                     │
│  ┌─────────────────────────────────────────────┐   │
│  │  Business Services                          │   │
│  │  - WslManagerService                        │   │
│  │  - DownloadService                          │   │
│  │  - CatalogService                           │   │
│  │  - SettingsService                          │   │
│  └──────────────────┬──────────────────────────┘   │
│                     │ 依赖                          │
│  ┌──────────────────▼──────────────────────────┐   │
│  │  PowerShellService (核心抽象层)           │   │
│  │  - ExecuteScriptAsync()                     │   │
│  │  - ExecuteAsync<T>()                        │   │
│  └──────────────────┬──────────────────────────┘   │
└─────────────────────┼─────────────────────────────┘
                      │ Process.Start
┌─────────────────────▼─────────────────────────────┐
│         PowerShell Engine (pwsh.exe)              │
│  ┌──────────────────────────────────────────┐    │
│  │  PowerShell Scripts                      │    │
│  │  - wsl.exe 调用                          │    │
│  │  - 注册表操作 (Get-ItemProperty)         │    │
│  │  - 文件系统操作 (Test-Path, Remove-Item) │    │
│  │  - 其他系统调用                           │    │
│  └──────────────────────────────────────────┘    │
└──────────────────────────────────────────────────┘
```

#### 6. 现有代码合规性评估

根据代码检查，当前架构**整体合规**（4.6/5），但存在以下需要改进的地方：

| 违规位置 | 违规代码 | 改进方案 | 优先级 |
|---------|---------|---------|--------|
| `MainViewModel` | `Process.Start("wt.exe")` | 封装为 `ITerminalService.OpenTerminalAsync()` | 🟡 P1 |
| `SettingsViewModel` | `File.Delete(cachePath)` | 使用现有 `ICatalogService.DeleteCachedPackageAsync()` | 🟡 P1 |
| `PackageManagerViewModel` | `Directory.CreateDirectory()` | 移除（`DownloadService` 已处理） | 🟡 P1 |
| `InstallWizardViewModel` | `Directory.Exists()` 验证 | 可选：封装为 `IFileSystemService.ValidatePathAsync()` | 🟢 P2 |

---

## 🎯 功能 1: 后台下载任务管理系统

### 需求说明
实现全局后台下载任务管理系统，支持单个/批量下载、任务队列管理、进度监控，不阻塞 UI 交互。

**核心特性**:
- ✅ 下载任务在后台运行，不阻塞界面交互
- ✅ 全局下载任务清单，显示所有正在/已完成的下载
- ✅ 单个下载时自动创建下载任务
- ✅ 批量下载时自动将未下载包添加到任务队列
- ✅ 下载完成后自动更新包管理器界面的包状态

### 架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                      MainWindow                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  工具栏: [Refresh] [Install] [Settings]              │   │
│  │         [下载任务(3)] [主题] [语言]  ←─ 新增        │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────┬──────────────────────────────┐   │
│  │                      │  下载任务侧边栏 (可展开)     │   │
│  │   主内容区           │  ┌────────────────────────┐  │   │
│  │   (PackageManager    │  │ Ubuntu-22.04           │  │   │
│  │    Dashboard, etc)   │  │ [████████░░] 80%       │  │   │
│  │                      │  │ [取消]                 │  │   │
│  │                      │  ├────────────────────────┤  │   │
│  │                      │  │ Debian-12              │  │   │
│  │                      │  │ [████░░░░░░] 40%       │  │   │
│  │                      │  │ [取消]                 │  │   │
│  │                      │  ├────────────────────────┤  │   │
│  │                      │  │ Kali-2024 ✓ 完成       │  │   │
│  │                      │  └────────────────────────┘  │   │
│  │                      │  [Clear Completed]          │   │
│  └──────────────────────┴──────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘

数据流:
1. 用户点击下载 → PackageManagerViewModel
2. 创建任务 → DownloadTaskManager.AddTask()
3. 后台执行 → Task.Run(() => ProcessTaskAsync())
4. 进度更新 → DownloadTask.Progress (ObservableObject)
5. UI 自动刷新 → MainWindow 侧边栏 + PackageManagerPage
6. 完成通知 → 更新 Catalog → 刷新包状态
```

### 核心实现

#### 0. DistroPackage 模型扩展
```csharp
// DistroNexus.Core/Models/DistroPackage.cs
public partial class DistroPackage : ObservableObject
{
    // ... 现有属性 ...
    
    /// <summary>
    /// 是否正在下载中
    /// </summary>
    [ObservableProperty]
    private bool _isDownloading;
    
    /// <summary>
    /// 本地缓存路径
    /// </summary>
    [ObservableProperty]
    private string? _localPath;
    
    /// <summary>
    /// 是否已缓存
    /// </summary>
    public bool IsCached => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath);
}
```

#### 1. 新增数据模型
```csharp
// DistroNexus.Core/Models/DownloadTask.cs

/// <summary>
/// 单个下载任务
/// </summary>
public class DownloadTask : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    
    private DownloadStatus _status = DownloadStatus.Pending;
    public DownloadStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
    
    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }
    
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }
    
    private long _downloadedBytes;
    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set => SetProperty(ref _downloadedBytes, value);
    }
    
    private long _totalBytes;
    public long TotalBytes
    {
        get => _totalBytes;
        set => SetProperty(ref _totalBytes, value);
    }
    
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime? StartTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    
    public int RetryCount { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }
    
    // UI 绑定属性
    public string ProgressText => Status switch
    {
        DownloadStatus.Downloading => $"{Progress:F1}% ({FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)})",
        DownloadStatus.Completed => $"Completed ({FormatBytes(TotalBytes)})",
        DownloadStatus.Failed => $"Failed: {ErrorMessage}",
        DownloadStatus.Pending => "Waiting...",
        DownloadStatus.Cancelled => "Cancelled",
        _ => string.Empty
    };
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:F2} {sizes[order]}";
    }
}

public enum DownloadStatus
{
    Pending,      // 等待中
    Downloading,  // 下载中
    Completed,    // 已完成
    Failed,       // 失败
    Cancelled     // 已取消
}
```

#### 2. 下载任务管理服务
```csharp
// DistroNexus.Core/Interfaces/IDownloadTaskManager.cs

/// <summary>
/// 全局下载任务管理器
/// </summary>
public interface IDownloadTaskManager
{
    /// <summary>
    /// 所有下载任务的集合（支持数据绑定）
    /// </summary>
    ObservableCollection<DownloadTask> Tasks { get; }
    
    /// <summary>
    /// 添加单个下载任务
    /// </summary>
    DownloadTask AddTask(DistroPackage package, string destinationPath);
    
    /// <summary>
    /// 批量添加下载任务
    /// </summary>
    List<DownloadTask> AddTasks(IEnumerable<DistroPackage> packages);
    
    /// <summary>
    /// 取消下载任务
    /// </summary>
    Task CancelTaskAsync(Guid taskId);
    
    /// <summary>
    /// 重试失败的任务
    /// </summary>
    Task RetryTaskAsync(Guid taskId);
    
    /// <summary>
    /// 清除已完成/失败的任务
    /// </summary>
    void ClearCompletedTasks();
    
    /// <summary>
    /// 获取活动任务数量
    /// </summary>
    int GetActiveTasksCount();
    
    /// <summary>
    /// 任务状态变化事件
    /// </summary>
    event EventHandler<DownloadTask>? TaskStatusChanged;
}

// DistroNexus.Core/Services/DownloadTaskManager.cs

public class DownloadTaskManager : IDownloadTaskManager
{
    private readonly IDownloadService _downloadService;
    private readonly ICatalogService _catalogService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<DownloadTaskManager> _logger;
    private readonly SemaphoreSlim _semaphore;
    
    public ObservableCollection<DownloadTask> Tasks { get; } = new();
    
    public event EventHandler<DownloadTask>? TaskStatusChanged;
    
    public DownloadTaskManager(
        IDownloadService downloadService,
        ICatalogService catalogService,
        ISettingsService settingsService,
        ILogger<DownloadTaskManager> logger)
    {
        _downloadService = downloadService;
        _catalogService = catalogService;
        _settingsService = settingsService;
        _logger = logger;
        
        // 从设置获取最大并发数
        var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        var maxConcurrent = settings.MaxConcurrentDownloads > 0 ? settings.MaxConcurrentDownloads : 3;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }
    
    public DownloadTask AddTask(DistroPackage package, string destinationPath)
    {
        var task = new DownloadTask
        {
            PackageId = package.Id,
            PackageName = package.Name,
            DownloadUrl = package.DownloadUrl,
            DestinationPath = destinationPath,
            TotalBytes = package.FileSize,
            Status = DownloadStatus.Pending,
            CancellationTokenSource = new CancellationTokenSource()
        };
        
        Application.Current.Dispatcher.Invoke(() => Tasks.Add(task));
        
        // 在后台启动下载
        _ = Task.Run(() => ProcessTaskAsync(task));
        
        _logger.LogInformation("Added download task: {PackageName}", package.Name);
        return task;
    }
    
    public List<DownloadTask> AddTasks(IEnumerable<DistroPackage> packages)
    {
        var tasks = new List<DownloadTask>();
        var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        
        foreach (var package in packages)
        {
            if (package.IsCached) continue; // 跳过已缓存
            
            var fileName = Path.GetFileName(new Uri(package.DownloadUrl).LocalPath);
            var destination = Path.Combine(settings.PackageCachePath, fileName);
            
            var task = AddTask(package, destination);
            tasks.Add(task);
        }
        
        _logger.LogInformation("Added {Count} download tasks", tasks.Count);
        return tasks;
    }
    
    private async Task ProcessTaskAsync(DownloadTask task)
    {
        await _semaphore.WaitAsync(task.CancellationTokenSource!.Token);
        
        try
        {
            task.Status = DownloadStatus.Downloading;
            task.StartTime = DateTime.Now;
            OnTaskStatusChanged(task);
            
            var progress = new Progress<double>(percent =>
            {
                task.Progress = percent;
                task.DownloadedBytes = (long)(task.TotalBytes * percent / 100);
            });
            
            var settings = await _settingsService.LoadSettingsAsync();
            var maxRetries = settings.MaxRetryAttempts;
            bool success = false;
            Exception? lastException = null;
            
            // 重试逻辑
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                if (task.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    task.Status = DownloadStatus.Cancelled;
                    OnTaskStatusChanged(task);
                    return;
                }
                
                try
                {
                    success = await _downloadService.DownloadFileAsync(
                        task.DownloadUrl,
                        task.DestinationPath,
                        progress,
                        task.CancellationTokenSource.Token);
                    
                    if (success) break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    task.RetryCount = attempt + 1;
                    
                    if (attempt < maxRetries && settings.AutoRetryFailedDownloads)
                    {
                        _logger.LogWarning(ex, "Download attempt {Attempt}/{Max} failed for {Package}",
                            attempt + 1, maxRetries + 1, task.PackageName);
                        await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)));
                    }
                }
            }
            
            if (success)
            {
                task.Status = DownloadStatus.Completed;
                task.Progress = 100;
                task.CompletedTime = DateTime.Now;
                
                // 更新 Catalog 中的缓存状态
                await UpdatePackageCacheStatus(task.PackageId, task.DestinationPath);
            }
            else
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = lastException?.Message ?? "Download failed";
            }
            
            OnTaskStatusChanged(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error processing download task: {PackageName}", task.PackageName);
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = ex.Message;
            OnTaskStatusChanged(task);
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    private async Task UpdatePackageCacheStatus(string packageId, string localPath)
    {
        try
        {
            // ✅ 合规：通过 CatalogService 更新状态
            // CatalogService 内部如需校验文件（如计算哈希），应通过 PowerShell 执行
            var package = await _catalogService.GetDistributionByIdAsync(packageId);
            if (package != null)
            {
                package.IsCached = true;
                package.LocalPath = localPath;
                // CatalogService 应该有方法来保存更新
                // 例如：await _catalogService.UpdatePackageStatusAsync(package);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update package cache status");
        }
    }
    
    public async Task CancelTaskAsync(Guid taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null && task.Status == DownloadStatus.Downloading)
        {
            task.CancellationTokenSource?.Cancel();
            _logger.LogInformation("Cancelled download task: {PackageName}", task.PackageName);
        }
    }
    
    public async Task RetryTaskAsync(Guid taskId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null && task.Status == DownloadStatus.Failed)
        {
            task.Status = DownloadStatus.Pending;
            task.ErrorMessage = null;
            task.RetryCount = 0;
            task.Progress = 0;
            task.CancellationTokenSource = new CancellationTokenSource();
            
            _ = Task.Run(() => ProcessTaskAsync(task));
            _logger.LogInformation("Retrying download task: {PackageName}", task.PackageName);
        }
    }
    
    public void ClearCompletedTasks()
    {
        var completed = Tasks.Where(t => 
            t.Status == DownloadStatus.Completed || 
            t.Status == DownloadStatus.Failed || 
            t.Status == DownloadStatus.Cancelled).ToList();
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var task in completed)
            {
                Tasks.Remove(task);
            }
        });
        
        _logger.LogInformation("Cleared {Count} completed tasks", completed.Count);
    }
    
    public int GetActiveTasksCount()
    {
        return Tasks.Count(t => t.Status == DownloadStatus.Downloading || t.Status == DownloadStatus.Pending);
    }
    
    private void OnTaskStatusChanged(DownloadTask task)
    {
        TaskStatusChanged?.Invoke(this, task);
    }
}
```

#### 3. MainViewModel 集成（全局任务管理）
```csharp
// DistroNexus.Desktop/ViewModels/MainViewModel.cs

public partial class MainViewModel : ObservableObject
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    
    [ObservableProperty]
    private bool _isDownloadPanelVisible;
    
    [ObservableProperty]
    private int _activeDownloadsCount;
    
    public ObservableCollection<DownloadTask> DownloadTasks => _downloadTaskManager.Tasks;
    
    public MainViewModel(
        IWslManagerService wslManager,
        IDownloadTaskManager downloadTaskManager, // 新增
        ILogger<MainViewModel> logger)
    {
        _downloadTaskManager = downloadTaskManager;
        
        // 订阅任务状态变化
        _downloadTaskManager.TaskStatusChanged += OnDownloadTaskStatusChanged;
        
        // 定时更新活动任务数
        UpdateActiveDownloadsCount();
    }
    
    private void OnDownloadTaskStatusChanged(object? sender, DownloadTask task)
    {
        UpdateActiveDownloadsCount();
        
        // 任务完成时，通知包管理器刷新
        if (task.Status == DownloadStatus.Completed)
        {
            _logger.LogInformation("Download completed: {PackageName}", task.PackageName);
            // 可以触发包管理器刷新事件
        }
    }
    
    private void UpdateActiveDownloadsCount()
    {
        ActiveDownloadsCount = _downloadTaskManager.GetActiveTasksCount();
    }
    
    /// <summary>
    /// 切换下载任务面板显示
    /// </summary>
    [RelayCommand]
    private void ToggleDownloadPanel()
    {
        IsDownloadPanelVisible = !IsDownloadPanelVisible;
    }
    
    /// <summary>
    /// 清除已完成的下载任务
    /// </summary>
    [RelayCommand]
    private void ClearCompletedDownloads()
    {
        _downloadTaskManager.ClearCompletedTasks();
    }
}
```

#### 4. PackageManagerViewModel 集成
```csharp
// DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs

public partial class PackageManagerViewModel : ObservableObject
{
    private readonly IDownloadTaskManager _downloadTaskManager;
    
    public PackageManagerViewModel(
        ICatalogService catalogService,
        IDownloadService downloadService,
        IDownloadTaskManager downloadTaskManager, // 新增
        ILogger<PackageManagerViewModel> logger)
    {
        _downloadTaskManager = downloadTaskManager;
        
        // 订阅任务完成事件以刷新界面
        _downloadTaskManager.TaskStatusChanged += OnDownloadTaskStatusChanged;
    }
    
    private void OnDownloadTaskStatusChanged(object? sender, DownloadTask task)
    {
        if (task.Status == DownloadStatus.Completed)
        {
            // 找到对应的包并更新状态
            var package = Packages.FirstOrDefault(p => p.Id == task.PackageId);
            if (package != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    package.IsCached = true;
                    package.LocalPath = task.DestinationPath;
                    UpdateGroupedPackages(); // 刷新分组显示
                });
            }
        }
    }
    
    /// <summary>
    /// 下载单个包（自动创建后台任务）
    /// </summary>
    [RelayCommand]
    private void DownloadPackage(DistroPackage package)
    {
        if (package == null || package.IsCached) return;
        
        _logger.LogInformation("Starting download: {PackageName}", package.Name);
        
        var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        var fileName = Path.GetFileName(new Uri(package.DownloadUrl).LocalPath);
        var destination = Path.Combine(settings.PackageCachePath, fileName);
        
        // 标记包为下载中状态
        package.IsDownloading = true; // 需要在 DistroPackage 模型中添加此属性
        
        // 创建后台下载任务
        _downloadTaskManager.AddTask(package, destination);
        
        // 显示提示
        StatusMessage = $"Added '{package.Name}' to download queue";
    }
    
    /// <summary>
    /// 批量下载所有未缓存的包
    /// </summary>
    [RelayCommand]
    private void DownloadAllPackages()
    {
        var uncached = Packages.Where(p => !p.IsCached && !p.IsDownloading).ToList();
        
        if (!uncached.Any())
        {
            MessageBox.Show("All packages are already cached or downloading.", 
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // 计算总大小
        long totalSize = uncached.Sum(p => p.FileSize);
        var sizeGB = totalSize / (1024.0 * 1024.0 * 1024.0);
        
        var result = MessageBox.Show(
            $"Add {uncached.Count} package(s) to download queue?\n\n" +
            $"Total size: {sizeGB:F2} GB\n" +
            $"Downloads will run in the background.",
            "Confirm Batch Download",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result != MessageBoxResult.Yes) return;
        
        _logger.LogInformation("Starting batch download of {Count} packages", uncached.Count);
        
        // 标记所有包为下载中
        foreach (var pkg in uncached)
        {
            pkg.IsDownloading = true;
        }
        
        // 批量添加到下载队列
        var tasks = _downloadTaskManager.AddTasks(uncached);
        
        StatusMessage = $"Added {tasks.Count} packages to download queue";
        
        // 显示下载任务面板
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow?.DataContext is MainViewModel mainVm)
        {
            mainVm.ToggleDownloadPanelCommand.Execute(null);
        }
    }
}
```

#### 5. UI 组件

**主窗口添加下载任务按钮和面板**
```xaml
<!-- MainWindow.xaml -->
<ui:FluentWindow>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <!-- 标题栏 -->
            <RowDefinition Height="*"/>    <!-- 主内容 -->
        </Grid.RowDefinitions>
        
        <!-- 标题栏 -->
        <Grid Grid.Row="0" Background="{DynamicResource ApplicationBackgroundBrush}">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,20,10">
                <!-- 下载任务按钮 -->
                <ui:Button Icon="{ui:SymbolIcon ArrowDownload24}"
                          Command="{Binding ToggleDownloadPanelCommand}"
                          ToolTip="Download Tasks"
                          Margin="0,0,10,0">
                    <!-- 活动下载数徽章 -->
                    <ui:Button.Badge>
                        <ui:Badge Appearance="Primary" 
                                 Content="{Binding ActiveDownloadsCount}"
                                 Visibility="{Binding ActiveDownloadsCount, Converter={StaticResource CountToVisibilityConverter}}"/>
                    </ui:Button.Badge>
                </ui:Button>
                
                <!-- 主题切换 -->
                <ui:Button Icon="{ui:SymbolIcon WeatherMoon24}"
                          Command="{Binding ToggleThemeCommand}"
                          ToolTip="Toggle Theme"
                          Margin="0,0,10,0"/>
                
                <!-- 语言切换 -->
                <ui:Button Icon="{ui:SymbolIcon LocalLanguage24}"
                          Command="{Binding ToggleLanguageCommand}"
                          ToolTip="Switch Language"/>
            </StackPanel>
        </Grid>
        
        <!-- 主内容区 -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            
            <!-- 主内容 -->
            <ContentControl Grid.Column="0" Content="{Binding CurrentView}"/>
            
            <!-- 下载任务侧边栏 -->
            <Border Grid.Column="1"
                   Width="400"
                   BorderBrush="{DynamicResource ControlStrokeColorDefaultBrush}"
                   BorderThickness="1,0,0,0"
                   Background="{DynamicResource LayerFillColorDefaultBrush}"
                   Visibility="{Binding IsDownloadPanelVisible, Converter={StaticResource BoolToVisibilityConverter}}">
                
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    
                    <!-- 标题栏 -->
                    <Border Grid.Row="0" 
                           Padding="20,15"
                           BorderBrush="{DynamicResource ControlStrokeColorDefaultBrush}"
                           BorderThickness="0,0,0,1">
                        <Grid>
                            <TextBlock Text="Download Tasks" 
                                      FontSize="18" 
                                      FontWeight="SemiBold"/>
                            <ui:Button Icon="{ui:SymbolIcon Dismiss24}"
                                      Command="{Binding ToggleDownloadPanelCommand}"
                                      Appearance="Transparent"
                                      HorizontalAlignment="Right"/>
                        </Grid>
                    </Border>
                    
                    <!-- 任务列表 -->
                    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
                        <ItemsControl ItemsSource="{Binding DownloadTasks}" Margin="10">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <ui:Card Margin="0,0,0,10" Padding="15">
                                        <Grid>
                                            <Grid.RowDefinitions>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                            </Grid.RowDefinitions>
                                            
                                            <!-- 包名和状态 -->
                                            <Grid Grid.Row="0" Margin="0,0,0,10">
                                                <TextBlock Text="{Binding PackageName}" 
                                                          FontWeight="SemiBold"
                                                          TextTrimming="CharacterEllipsis"/>
                                                <ui:Badge Content="{Binding Status}"
                                                         Background="{Binding Status, Converter={StaticResource StatusToColorConverter}}"
                                                         HorizontalAlignment="Right"/>
                                            </Grid>
                                            
                                            <!-- 进度条 -->
                                            <ProgressBar Grid.Row="1"
                                                        Value="{Binding Progress}"
                                                        Maximum="100"
                                                        Height="6"
                                                        Margin="0,0,0,5"
                                                        Visibility="{Binding Status, Converter={StaticResource DownloadingToVisibilityConverter}}"/>
                                            
                                            <!-- 进度文本和操作 -->
                                            <Grid Grid.Row="2">
                                                <TextBlock Text="{Binding ProgressText}"
                                                          FontSize="12"
                                                          Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
                                                
                                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                                                    <!-- 取消按钮 -->
                                                    <ui:Button Icon="{ui:SymbolIcon Dismiss24}"
                                                              Command="{Binding DataContext.CancelDownloadCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                                              CommandParameter="{Binding Id}"
                                                              ToolTip="Cancel"
                                                              Appearance="Transparent"
                                                              Padding="5"
                                                              Visibility="{Binding Status, Converter={StaticResource DownloadingToVisibilityConverter}}"/>
                                                    
                                                    <!-- 重试按钮 -->
                                                    <ui:Button Icon="{ui:SymbolIcon ArrowClockwise24}"
                                                              Command="{Binding DataContext.RetryDownloadCommand, RelativeSource={RelativeSource AncestorType=Window}}"
                                                              CommandParameter="{Binding Id}"
                                                              ToolTip="Retry"
                                                              Appearance="Transparent"
                                                              Padding="5"
                                                              Visibility="{Binding Status, Converter={StaticResource FailedToVisibilityConverter}}"/>
                                                </StackPanel>
                                            </Grid>
                                        </Grid>
                                    </ui:Card>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </ScrollViewer>
                    
                    <!-- 底部操作栏 -->
                    <Border Grid.Row="2"
                           Padding="15"
                           BorderBrush="{DynamicResource ControlStrokeColorDefaultBrush}"
                           BorderThickness="0,1,0,0">
                        <ui:Button Content="Clear Completed"
                                  Command="{Binding ClearCompletedDownloadsCommand}"
                                  HorizontalAlignment="Stretch"/>
                    </Border>
                </Grid>
            </Border>
        </Grid>
    </Grid>
</ui:FluentWindow>
```

**包管理器界面更新**
```xaml
<!-- PackageManagerPage.xaml -->
<!-- 工具栏 -->
<StackPanel Orientation="Horizontal" Margin="0,0,0,15">
    <ui:Button Content="Refresh"
              Icon="{ui:SymbolIcon ArrowClockwise24}"
              Command="{Binding RefreshViewCommand}"
              Margin="0,0,10,0"/>
    
    <ui:Button Content="Download All"
              Icon="{ui:SymbolIcon ArrowDownloadMultiple24}"
              Command="{Binding DownloadAllPackagesCommand}"
              ToolTip="Add all uncached packages to download queue"/>
</StackPanel>

<!-- 包列表 - 未缓存包显示下载按钮 -->
<DataTemplate x:Key="UncachedPackageTemplate">
    <Grid>
        <!-- ... 包信息 ... -->
        
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <!-- 下载中状态 -->
            <StackPanel Orientation="Horizontal" 
                       Visibility="{Binding IsDownloading, Converter={StaticResource BoolToVisibilityConverter}}">
                <ProgressRing Width="20" Height="20" IsActive="True" Margin="0,0,10,0"/>
                <TextBlock Text="Downloading..." VerticalAlignment="Center"/>
            </StackPanel>
            
            <!-- 下载按钮 -->
            <ui:Button Content="Download"
                      Icon="{ui:SymbolIcon ArrowDownload24}"
                      Command="{Binding DataContext.DownloadPackageCommand, RelativeSource={RelativeSource AncestorType=Page}}"
                      CommandParameter="{Binding}"
                      Visibility="{Binding IsDownloading, Converter={StaticResource InvertBoolToVisibilityConverter}}"/>
        </StackPanel>
    </Grid>
</DataTemplate>
```

#### 6. 值转换器实现
```csharp
// DistroNexus.Desktop/Converters/DownloadStatusConverters.cs

/// <summary>
/// 下载状态转颜色转换器
/// </summary>
public class DownloadStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Downloading => new SolidColorBrush(Colors.DodgerBlue),
                DownloadStatus.Completed => new SolidColorBrush(Colors.Green),
                DownloadStatus.Failed => new SolidColorBrush(Colors.Red),
                DownloadStatus.Cancelled => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Orange)
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 下载中状态转可见性转换器
/// </summary>
public class DownloadingToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status == DownloadStatus.Downloading ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 失败状态转可见性转换器
/// </summary>
public class FailedToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status == DownloadStatus.Failed ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 数量转可见性转换器（大于0显示）
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

**注册转换器到 App.xaml**
```xaml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <converters:DownloadStatusToColorConverter x:Key="StatusToColorConverter"/>
        <converters:DownloadingToVisibilityConverter x:Key="DownloadingToVisibilityConverter"/>
        <converters:FailedToVisibilityConverter x:Key="FailedToVisibilityConverter"/>
        <converters:CountToVisibilityConverter x:Key="CountToVisibilityConverter"/>
        <converters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter"/>
        <converters:InvertBoolToVisibilityConverter x:Key="InvertBoolToVisibilityConverter"/>
    </ResourceDictionary>
</Application.Resources>
```

### 任务清单（4-6天）

**Phase 1: 核心服务实现（2天）**
- [ ] 扩展 `DistroPackage` 模型添加 `IsDownloading` 属性（0.5h）
- [ ] 创建 `DownloadTask.cs` 模型（2h）
  - 实现 `ObservableObject` 基类
  - 添加进度属性和状态管理
  - 实现 `ProgressText` 和 `FormatBytes` 辅助方法
- [ ] 创建 `IDownloadTaskManager` 接口（1h）
- [ ] 实现 `DownloadTaskManager.cs`（10h）
  - 任务集合管理（ObservableCollection）
  - `AddTask` 和 `AddTasks` 方法
  - `ProcessTaskAsync` 后台下载逻辑
  - 并发控制（SemaphoreSlim）
  - 重试逻辑和错误处理
  - `UpdatePackageCacheStatus` 更新 Catalog
  - 任务取消、重试、清除功能
  - 事件系统（TaskStatusChanged）

**Phase 2: ViewModel 集成（1天）**
- [ ] 扩展 `MainViewModel`（4h）
  - 注入 `IDownloadTaskManager`
  - 添加 `IsDownloadPanelVisible` 属性
  - 添加 `ActiveDownloadsCount` 属性
  - 实现 `ToggleDownloadPanel` 命令
  - 实现 `ClearCompletedDownloads` 命令
  - 订阅 `TaskStatusChanged` 事件
  - 添加 `CancelDownload` 和 `RetryDownload` 命令
- [ ] 修改 `PackageManagerViewModel`（4h）
  - 注入 `IDownloadTaskManager`
  - 实现 `DownloadPackage` 命令（单个下载）
  - 实现 `DownloadAllPackages` 命令（批量下载）
  - 订阅 `TaskStatusChanged` 更新包状态
  - 标记包为下载中状态

**Phase 3: UI 实现（1-2天）**
- [ ] 创建主窗口下载任务侧边栏（5h）
  - 修改 `MainWindow.xaml` 添加侧边栏
  - 实现任务列表 ItemsControl
  - 添加任务卡片模板（进度条、状态、操作按钮）
  - 实现滑入/滑出动画（可选）
- [ ] 添加工具栏下载任务按钮（2h）
  - 添加按钮图标
  - 实现徽章显示活动任务数
  - 绑定 `ToggleDownloadPanel` 命令
- [ ] 更新包管理器界面（3h）
  - 修改未缓存包模板显示下载按钮
  - 添加下载中状态指示器（ProgressRing）
  - 调整 "Download All" 按钮样式
- [ ] 创建值转换器（2h）
  - `DownloadStatusToColorConverter`
  - `DownloadingToVisibilityConverter`
  - `FailedToVisibilityConverter`
  - `CountToVisibilityConverter`
  - 注册到 `App.xaml` 资源字典

**Phase 4: 依赖注入配置（0.5天）**
- [ ] 注册服务（1h）
  - 在 `App.xaml.cs` 注册 `IDownloadTaskManager`
  - 设置为单例模式（Singleton）
- [ ] 更新 ViewModels 构造函数（1h）
  - `MainViewModel` 添加 `IDownloadTaskManager` 参数
  - `PackageManagerViewModel` 添加 `IDownloadTaskManager` 参数

**Phase 5: 测试（1-2天）**
- [ ] 单个下载测试（2h）
  - 测试单个包下载流程
  - 验证包状态自动更新
  - 测试下载失败情况
- [ ] 批量下载测试（2h）
  - 测试批量添加任务
  - 验证并发限制正常工作
  - 测试大量包（20+）的性能
- [ ] UI 交互测试（2h）
  - 测试下载任务面板展开/收起
  - 验证任务列表实时更新
  - 测试徽章数字更新
- [ ] 取消/重试测试（2h）
  - 测试取消正在下载的任务
  - 测试重试失败的任务
  - 测试清除已完成任务
- [ ] 状态同步测试（2h）
  - 验证包管理器界面状态同步
  - 测试多个界面同时打开的情况
  - 验证下载完成后刷新正确

### 关键实现要点

#### 1. 线程安全
- `DownloadTaskManager.Tasks` 使用 `ObservableCollection`，必须在 UI 线程更新
- 使用 `Application.Current.Dispatcher.Invoke()` 确保线程安全
- `SemaphoreSlim` 控制并发，避免资源竞争

#### 2. 状态同步
- 下载任务完成后，通过 `TaskStatusChanged` 事件通知所有订阅者
- `PackageManagerViewModel` 订阅事件，自动更新包的 `IsCached` 和 `LocalPath` 属性
- `DistroPackage` 继承 `ObservableObject`，属性变化自动通知 UI

#### 3. 取消机制
- 每个 `DownloadTask` 持有独立的 `CancellationTokenSource`
- 取消任务时调用 `CancellationTokenSource.Cancel()`
- `ProcessTaskAsync` 检查 `CancellationToken.IsCancellationRequested`

#### 4. 并发控制
```csharp
// 从设置读取最大并发数（默认3）
var maxConcurrent = settings.MaxConcurrentDownloads;
_semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

// 每个任务执行前获取信号量
await _semaphore.WaitAsync(cancellationToken);
try
{
    // 下载逻辑
}
finally
{
    _semaphore.Release(); // 释放信号量
}
```

#### 5. 错误处理
- 网络错误自动重试（根据 `MaxRetryAttempts` 设置）
- 失败任务保留在列表，显示错误信息
- 用户可手动重试失败的任务

#### 6. 性能优化
- 下载任务在 `Task.Run()` 中执行，不阻塞 UI 线程
- `ObservableCollection` 只在必要时通知 UI 更新
- 大文件下载使用流式传输，避免内存占用

---

## 🎯 功能 2: 从缓存快速安装

### 需求说明
从包管理器中已缓存的包直接跳转到安装向导，预填充参数并跳过发行版选择步骤。

### 核心实现

#### 1. 新增参数模型
```csharp
// DistroNexus.Core/Models/InstallParameters.cs
public class InstallParameters
{
    public DistroPackage? PreselectedPackage { get; set; }
    public string? InstanceName { get; set; }
    public bool SkipDistributionSelection { get; set; }
}
```

#### 2. ViewModel 扩展
```csharp
// PackageManagerViewModel.cs
[RelayCommand]
private void InstallCachedPackage(DistroPackage package)
{
    var parameters = new InstallParameters
    {
        PreselectedPackage = package,
        InstanceName = package.Name.Replace(" ", "-"),
        SkipDistributionSelection = true
    };
    
    // 导航到安装向导
    var mainWindow = Application.Current.MainWindow;
    if (mainWindow?.DataContext is MainViewModel vm)
        vm.ShowInstallWizardCommand.Execute(parameters);
}

// InstallWizardWorkflowViewModel.cs
public void Initialize(InstallParameters parameters)
{
    Context.SelectedPackage = parameters.PreselectedPackage;
    Context.InstanceName = parameters.InstanceName;
    
    // 如果跳过选择，移除 SelectDistributionStep
    if (parameters.SkipDistributionSelection)
    {
        var step = Steps.FirstOrDefault(s => s is SelectDistributionStep);
        if (step != null) Steps.Remove(step);
    }
}
```

#### 3. UI 更新
```xaml
<!-- PackageManagerPage.xaml - 已缓存包操作区 -->
<ui:Button Content="Install"
          Icon="{ui:SymbolIcon Add24}"
          Command="{Binding InstallCachedPackageCommand}"
          CommandParameter="{Binding}"
          Appearance="Primary"/>
```

### 任务清单（3-4天）

**Phase 1: 数据模型（0.5天）**
- [ ] 创建 `InstallParameters.cs`（1h）

**Phase 2: ViewModel 集成（1天）**
- [ ] 修改 `MainViewModel.ShowInstallWizard` 接受参数（2h）
- [ ] 实现 `PackageManagerViewModel.InstallCachedPackage`（2h）
- [ ] 修改 `InstallWizardWorkflowViewModel.Initialize`（3h）

**Phase 3: UI 实现（0.5天）**
- [ ] 修改 `PackageManagerPage.xaml` 添加按钮（1h）
- [ ] 调整缓存包显示模板（1h）

**Phase 4: 测试（1天）**
- [ ] 功能测试（2h）
- [ ] 参数预填充测试（2h）
- [ ] 步骤跳过测试（2h）
- [ ] 集成测试（2h）

---

## 🎯 功能 3: 安装向导快速模式

### 需求说明
提供 "Quick Install" 和 "Standard Install" 两种模式。快速模式使用 Root 用户、默认路径和 WSL 2，只需选择发行版和实例名。

### 核心实现

#### 1. 扩展模型
```csharp
// InstallParameters.cs
public enum InstallMode { Standard, Quick }

public class InstallParameters
{
    public InstallMode Mode { get; set; } = InstallMode.Standard;
    // ... 其他属性
}
```

#### 2. 模式选择对话框
```xaml
<!-- InstallModeSelectionDialog.xaml -->
<Grid>
    <!-- 快速安装卡片 -->
    <ui:Card Cursor="Hand">
        <StackPanel>
            <ui:SymbolIcon Symbol="Flash24"/>
            <TextBlock Text="Quick Install"/>
            <ui:Badge Content="Recommended"/>
            <!-- 特性列表：Root用户、默认路径、WSL 2、最少步骤 -->
        </StackPanel>
    </ui:Card>
    
    <!-- 标准安装卡片 -->
    <ui:Card Cursor="Hand">
        <StackPanel>
            <ui:SymbolIcon Symbol="Settings24"/>
            <TextBlock Text="Standard Install"/>
            <!-- 特性列表：自定义用户、选择路径、配置版本 -->
        </StackPanel>
    </ui:Card>
</Grid>
```

#### 3. 快速模式步骤
```csharp
// QuickInstallPathStep.cs
public class QuickInstallPathStep : WizardStepBase
{
    public override async Task<bool> ValidateAsync()
    {
        // 只验证实例名
        if (string.IsNullOrWhiteSpace(Context.InstanceName)) return false;
        
        // 自动生成路径
        var settings = await _settingsService.LoadSettingsAsync();
        Context.InstallPath = Path.Combine(settings.DefaultInstallPath, Context.InstanceName);
        
        return true;
    }
}

// Workflow 调整
protected override void InitializeSteps()
{
    if (_initialParameters?.Mode == InstallMode.Quick)
    {
        Steps.Add(new SelectDistributionStep(...));
        Steps.Add(new QuickInstallPathStep(...)); // 简化版路径步骤
        Steps.Add(new QuickReviewStep(...));      // 简化版审查
        Steps.Add(new ProgressStep(...));
        Steps.Add(new ResultStep(...));
        
        // 设置默认值
        Context.CreateUser = false; // Root
        Context.WslVersion = 2;     // WSL 2
    }
    else { /* 标准流程 */ }
}
```

### 任务清单（4-6天）

**Phase 1: 数据模型（0.5天）**
- [ ] 扩展 `InstallParameters` 添加 `InstallMode`（0.5h）

**Phase 2: 模式选择对话框（1天）**
- [ ] 创建 `InstallModeSelectionDialog.xaml`（3h）
- [ ] 创建 `InstallModeSelectionViewModel`（2h）
- [ ] 实现选择交互（1h）

**Phase 3: 快速模式步骤（1.5天）**
- [ ] 创建 `QuickInstallPathStep.cs` 和视图（5h）
- [ ] 创建 `QuickReviewStep.cs` 和视图（4h）

**Phase 4: Workflow 集成（1天）**
- [ ] 修改 `InitializeSteps` 添加模式判断（2h）
- [ ] 实现快速模式默认值（1h）
- [ ] 修改 `MainViewModel` 集成模式选择（1h）

**Phase 5: 测试（1-2天）**
- [ ] 快速模式完整流程（3h）
- [ ] 标准模式不受影响（2h）
- [ ] 模式切换测试（2h）
- [ ] 与缓存安装结合测试（2h）

---

## 🎯 功能 4: 安装进度实时日志

### 需求说明
在安装进度步骤中显示可展开的日志面板，实时流式显示 PowerShell 脚本输出，支持滚动和复制。

### 核心实现

#### 1. Service 层扩展
```csharp
// PowerShellService.cs
public async Task<PowerShellResult> ExecuteScriptWithLoggingAsync(
    string scriptPath,
    IProgress<string>? logProgress = null,
    IProgress<double>? percentProgress = null)
{
    using var ps = PowerShell.Create();
    
    // 订阅所有输出流
    ps.Streams.Information.DataAdded += (s, e) => {
        var log = $"[INFO] {ps.Streams.Information[e.Index].MessageData}\n";
        logProgress?.Report(log);
    };
    
    ps.Streams.Verbose.DataAdded += (s, e) => { /* 类似 */ };
    ps.Streams.Warning.DataAdded += (s, e) => { /* 类似 */ };
    ps.Streams.Error.DataAdded += (s, e) => { /* 类似 */ };
    
    // 执行脚本
    var output = await Task.Run(() => ps.Invoke());
    
    return new PowerShellResult { Success = !ps.HadErrors, Logs = logs };
}
```

#### 2. ViewModel 扩展
```csharp
// ProgressStep.cs
[ObservableProperty]
private string _logOutput = string.Empty;

[ObservableProperty]
private bool _isLogPanelExpanded;

public override async Task<StepResult> ExecuteAsync()
{
    var logProgress = new Progress<string>(logLine =>
    {
        LogOutput += logLine;
        // 自动滚动到底部
    });
    
    Context.InstallationResult = await _wslManager.InstallInstanceAsync(
        options,
        logProgress,    // 传递日志进度
        percentProgress);
}
```

#### 3. UI 实现
```xaml
<!-- ProgressStepView.xaml -->
<Expander Header="Show Installation Log" IsExpanded="{Binding IsLogPanelExpanded}">
    <Border Background="{DynamicResource CardBackgroundFillColorDefaultBrush}"
            BorderBrush="{DynamicResource CardStrokeColorDefaultBrush}"
            BorderThickness="1"
            MaxHeight="300">
        <ScrollViewer VerticalScrollBarVisibility="Auto">
            <TextBox Text="{Binding LogOutput, Mode=OneWay}"
                    IsReadOnly="True"
                    TextWrapping="Wrap"
                    FontFamily="Consolas"
                    FontSize="11"
                    Background="Transparent"
                    BorderThickness="0"/>
        </ScrollViewer>
    </Border>
</Expander>
```

### 任务清单（6-7天）

**Phase 1: Service 层（2天）**
- [ ] 修改 `PowerShellService` 支持日志捕获（5h）
- [ ] 实现流式输出订阅（3h）

**Phase 2: ViewModel 集成（2天）**
- [ ] 扩展 `ProgressStep` 添加日志属性（2h）
- [ ] 实现日志追加和滚动（3h）
- [ ] 修改 `WslManagerService` 传递日志进度（3h）

**Phase 3: UI 实现（1天）**
- [ ] 修改 `ProgressStepView.xaml` 添加日志面板（3h）
- [ ] 实现日志格式化和颜色（2h）
- [ ] 添加复制日志功能（1h）

**Phase 4: 测试（1-2天）**
- [ ] 日志捕获测试（3h）
- [ ] 实时更新测试（2h）
- [ ] UI 性能测试（大量日志）（3h）

---

## 🎯 功能 5: 独立的更新源功能

### 需求说明
在包管理器中添加独立的 "Update Sources" 按钮，与 "Refresh" 功能区分，明确更新在线目录。

### 核心实现

#### 1. ViewModel 扩展
```csharp
// PackageManagerViewModel.cs
[RelayCommand]
private async Task UpdateSourcesAsync()
{
    IsLoading = true;
    StatusMessage = "Updating distribution sources...";
    
    await _catalogService.RefreshCatalogAsync(); // 从远程更新
    await LoadCatalogAsync();                    // 重新加载
    
    MessageBox.Show("Distribution sources updated successfully.", "Success");
}

[RelayCommand]
private async Task RefreshViewAsync()
{
    // 只刷新当前视图，不更新远程源
    await LoadCatalogAsync();
}
```

#### 2. UI 更新
```xaml
<!-- PackageManagerPage.xaml -->
<StackPanel Orientation="Horizontal">
    <ui:Button Content="Refresh" 
              Icon="{ui:SymbolIcon ArrowClockwise24}"
              Command="{Binding RefreshViewCommand}"
              ToolTip="Refresh current view"/>
    
    <ui:Button Content="Update Sources" 
              Icon="{ui:SymbolIcon CloudSync24}"
              Command="{Binding UpdateSourcesCommand}"
              ToolTip="Update distribution catalog from remote source"
              Margin="10,0,0,0"/>
</StackPanel>
```

### 任务清单（1.5-2.5天）

**Phase 1: ViewModel 实现（0.5天）**
- [ ] 拆分 `RefreshCatalogAsync` 为两个命令（2h）
- [ ] 更新状态消息和错误处理（1h）

**Phase 2: UI 实现（0.5天）**
- [ ] 修改 `PackageManagerPage.xaml` 添加按钮（1h）
- [ ] 调整布局和图标（1h）

**Phase 3: 测试（0.5天）**
- [ ] 功能测试（1h）
- [ ] 网络错误测试（1h）
- [ ] 用户体验测试（1h）

---

## 🎯 功能 6: 设置自动保存

### 需求说明
在设置页面关闭或导航离开时，如果有未保存的更改，自动保存或提示用户。

### 核心实现

#### 1. ViewModel 扩展
```csharp
// SettingsViewModel.cs
[ObservableProperty]
private bool _isDirty;

partial void OnAnySettingChanged() => IsDirty = true;

public async Task<bool> PromptSaveIfDirtyAsync()
{
    if (!IsDirty) return true;
    
    var result = MessageBox.Show(
        "You have unsaved changes. Do you want to save them?",
        "Unsaved Changes",
        MessageBoxButton.YesNoCancel);
    
    if (result == MessageBoxResult.Yes)
    {
        await SaveSettingsAsync();
        return true;
    }
    
    return result != MessageBoxResult.Cancel;
}

[RelayCommand]
private async Task GoBackAsync()
{
    if (!await PromptSaveIfDirtyAsync()) return;
    
    // 导航回主页
    NavigateBack();
}
```

#### 2. UI 集成
```csharp
// SettingsPage.xaml.cs
protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
{
    if (DataContext is SettingsViewModel vm && vm.IsDirty)
    {
        var canLeave = await vm.PromptSaveIfDirtyAsync();
        if (!canLeave) e.Cancel = true;
    }
    
    base.OnNavigatingFrom(e);
}
```

### 任务清单（1.5天）

**Phase 1: ViewModel 实现（0.5天）**
- [ ] 添加 `IsDirty` 跟踪（2h）
- [ ] 实现 `PromptSaveIfDirtyAsync`（2h）

**Phase 2: UI 集成（0.5天）**
- [ ] 实现导航拦截（2h）
- [ ] 处理窗口关闭事件（1h）

**Phase 3: 测试（0.5天）**
- [ ] 保存提示测试（1h）
- [ ] 取消导航测试（1h）
- [ ] 自动保存测试（1h）

---

## 📅 开发阶段规划

### 第一阶段（10-13天）- 核心功能
**目标**: 完成 P0 高优先级功能

1. **周 1-2**: 功能 1 批量下载（4-6天）
2. **周 2**: 功能 2 快速安装（3-4天）
3. **周 3**: 功能 3 快速模式（4-6天）

**里程碑**: 用户可以批量下载、快速安装、使用快速模式

### 第二阶段（8-10天）- 用户体验增强
**目标**: 完成 P1 中优先级功能

1. **周 4-5**: 功能 4 实时日志（6-7天）
2. **周 5**: 功能 5 更新源（1.5-2.5天）

**里程碑**: 安装过程透明，源管理清晰

### 第三阶段（1.5天）- 便利性功能
**目标**: 完成 P2 低优先级功能

1. **周 5**: 功能 6 自动保存（1.5天）

**里程碑**: 设置管理更友好

---

## 🛠️ 技术依赖与风险

### 关键依赖
1. ✅ 现有 `ICatalogService` 接口
2. ✅ 现有 `IDownloadService` 支持进度报告
3. ⚠️ `PowerShellService` 需要扩展支持日志捕获
4. ⚠️ 向导框架需要支持动态步骤

### 潜在风险

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| 并发下载性能问题 | 中 | 中 | 添加并发限制，性能测试 |
| PowerShell 日志捕获不完整 | 中 | 中 | 使用多个流订阅，测试覆盖 |
| 向导状态管理复杂 | 低 | 高 | 充分单元测试，状态机设计 |
| UI 大量日志卡顿 | 中 | 低 | 使用虚拟化滚动 |

### 依赖注入配置

```csharp
// App.xaml.cs - 服务注册
services.AddSingleton<IBatchDownloadService, BatchDownloadService>();
services.AddSingleton<IPowerShellService, PowerShellService>();
// ... 其他服务
```

---

## 📝 文档与培训

### 用户文档更新
- [ ] 批量下载功能使用指南
- [ ] 快速安装模式说明
- [ ] 从缓存安装流程
- [ ] 查看安装日志方法

### 开发者文档
- [ ] 批量下载服务 API 文档
- [ ] 安装参数预填充机制
- [ ] 向导步骤自定义指南
- [ ] PowerShell 日志捕获实现

---

## 🔍 现有代码改进任务清单

在实现新功能之前，建议先修复现有代码中的架构违规问题，确保代码库符合 PowerShell 模块调用规范。

### Phase 0: 架构合规性修复（1-2天）

#### 任务 1：封装终端启动服务（2-3小时）🟡 P1

**问题位置**：`MainViewModel.cs` 行 440-473

**现状**：直接使用 `Process.Start` 打开 Windows Terminal
```csharp
// ❌ 违规代码
Process.Start(new ProcessStartInfo {
    FileName = "wt.exe",
    Arguments = $"-w 0 wsl -d {Name}"
});
```

**改进方案**：
1. 创建 `ITerminalService` 接口
   ```csharp
   // DistroNexus.Core/Interfaces/ITerminalService.cs
   public interface ITerminalService
   {
       Task<bool> OpenTerminalAsync(string instanceName, CancellationToken cancellationToken = default);
       Task<bool> OpenTerminalInDirectoryAsync(string instanceName, string workingDirectory, CancellationToken cancellationToken = default);
       Task<List<string>> GetAvailableTerminalsAsync(CancellationToken cancellationToken = default);
   }
   ```

2. 实现 `TerminalService` 通过 PowerShell 启动终端
   ```csharp
   // DistroNexus.Core/Services/TerminalService.cs
   public class TerminalService : ITerminalService
   {
       private readonly IPowerShellService _powerShell;
       
       public async Task<bool> OpenTerminalAsync(string instanceName, CancellationToken cancellationToken = default)
       {
           var script = $@"
               # 检测可用终端并启动
               if (Get-Command wt.exe -ErrorAction SilentlyContinue) {{
                   Start-Process wt.exe -ArgumentList '-w', '0', 'wsl', '-d', '{instanceName}'
               }} elseif (Get-Command cmd.exe -ErrorAction SilentlyContinue) {{
                   Start-Process cmd.exe -ArgumentList '/k', 'wsl', '-d', '{instanceName}'
               }} else {{
                   throw 'No terminal available'
               }}
               return $true
           ";
           
           var result = await _powerShell.ExecuteScriptAsync(script, cancellationToken);
           return result.ExitCode == 0;
       }
   }
   ```

3. 更新 `MainViewModel` 使用服务
   ```csharp
   private readonly ITerminalService _terminalService;
   
   [RelayCommand]
   private async Task OpenTerminalAsync()
   {
       await _terminalService.OpenTerminalAsync(SelectedInstance.Name);
   }
   ```

**验收标准**：
- [ ] `ITerminalService` 接口定义完成
- [ ] `TerminalService` 实现完成（通过 PowerShell）
- [ ] 在 DI 容器注册服务
- [ ] `MainViewModel` 移除直接 `Process.Start` 调用
- [ ] 功能测试通过（Windows Terminal、CMD 回退）

---

#### 任务 2：修复缓存文件操作（1-2小时）🟡 P1

**问题位置**：`SettingsViewModel.cs` 行 436-469

**现状**：直接使用 `File.Delete` 和 `Process.Start` 打开文件夹
```csharp
// ❌ 违规代码
if (File.Exists(package.FilePath))
    File.Delete(package.FilePath);

Process.Start(new ProcessStartInfo {
    FileName = cachePath,
    UseShellExecute = true
});
```

**改进方案**：
1. 使用现有 `ICatalogService.DeleteCachedPackageAsync()` 方法
2. 封装文件夹打开操作到 `ITerminalService` 或新建 `IShellService`

**实现步骤**：
```csharp
// SettingsViewModel.cs 修改
[RelayCommand]
private async Task DeleteCachedPackageAsync(CachedPackage package)
{
    try
    {
        // 使用服务方法删除
        await _catalogService.DeleteCachedPackageAsync(package.Id);
        
        // 刷新列表
        await LoadCachedPackagesAsync();
        
        StatusMessage = $"Deleted {package.Name}";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to delete cached package");
        StatusMessage = $"Error: {ex.Message}";
    }
}

[RelayCommand]
private async Task OpenCacheFolderAsync()
{
    var settings = await _settingsService.LoadSettingsAsync();
    
    // 通过 PowerShell 打开文件夹
    var script = $@"
        $path = '{settings.PackageCachePath}'
        if (Test-Path $path) {{
            explorer.exe $path
        }}
    ";
    
    await _powerShell.ExecuteScriptAsync(script);
}
```

**验收标准**：
- [ ] 移除直接 `File.Delete` 调用
- [ ] 使用 `ICatalogService.DeleteCachedPackageAsync()`
- [ ] 文件夹打开通过 PowerShell 执行
- [ ] 错误处理完善
- [ ] 功能测试通过

---

#### 任务 3：移除冗余目录创建逻辑（0.5小时）🟡 P1

**问题位置**：`PackageManagerViewModel.cs` 行 177-186

**现状**：ViewModel 重复创建下载目录（`DownloadService` 已处理）
```csharp
// ❌ 冗余代码
if (!Directory.Exists(downloadsPath))
    Directory.CreateDirectory(downloadsPath);
```

**改进方案**：
直接移除，依赖 `DownloadService.DownloadFileAsync` 的内部实现（已包含目录创建）

**验收标准**：
- [ ] 移除冗余的 `Directory.CreateDirectory` 调用
- [ ] 验证 `DownloadService` 确实自动创建目录
- [ ] 功能测试通过

---

#### 任务 4：（可选）封装路径验证服务（1-2小时）🟢 P2

**问题位置**：`InstallWizardViewModel.cs` 行 378-394

**现状**：表单验证直接使用 `Directory.Exists` 和 `Path.Combine`
```csharp
// 当前代码
if (Directory.Exists(instancePath)) { ... }
```

**改进方案**（可选）：
1. 创建 `IFileSystemService` 封装文件系统验证逻辑
2. 或保持现状（UI 层验证可接受）

**实现示例**：
```csharp
// DistroNexus.Core/Interfaces/IFileSystemService.cs
public interface IFileSystemService
{
    Task<bool> PathExistsAsync(string path);
    Task<bool> ValidateWritePermissionAsync(string path);
    Task<long> GetAvailableSpaceAsync(string path);
}

// 实现通过 PowerShell
public async Task<bool> PathExistsAsync(string path)
{
    var script = $@"
        Test-Path '{path}' | ConvertTo-Json
    ";
    var result = await _powerShell.ExecuteScriptAsync(script);
    return JsonSerializer.Deserialize<bool>(result.Output);
}
```

**验收标准**：
- [ ] 决定是否实现（可选）
- [ ] 如实现，创建接口和服务
- [ ] 更新 ViewModel 使用服务
- [ ] 测试通过

---

### 任务汇总和时间估算

| 任务 | 优先级 | 时间估算 | 必需性 |
|-----|-------|---------|--------|
| 1. 封装终端启动服务 | 🟡 P1 | 2-3h | 建议 |
| 2. 修复缓存文件操作 | 🟡 P1 | 1-2h | 建议 |
| 3. 移除冗余目录创建 | 🟡 P1 | 0.5h | 建议 |
| 4. 封装路径验证服务 | 🟢 P2 | 1-2h | 可选 |
| **总计** | - | **5-8.5小时** | **1-2天** |

**建议执行顺序**：
1. 任务 3（最简单，立即见效）
2. 任务 2（使用现有服务）
3. 任务 1（新增服务，最复杂）
4. 任务 4（可选，时间充裕时考虑）

**注意事项**：
- 这些任务与新功能开发并行，不影响主要开发计划
- 可以在实现新功能时逐步修复
- 建议在每个新功能的 PR 中至少修复一个相关违规问题

---

## ✅ 验收标准

### 功能 1: 后台下载任务管理
- [ ] 下载任务在后台运行，不阻塞 UI 交互
- [ ] 主窗口工具栏显示下载任务按钮（带活动任务数徽章）
- [ ] 可展开的下载任务侧边栏，显示所有任务
- [ ] 单个下载时自动创建任务并标记包为下载中
- [ ] 批量下载时自动添加所有未缓存包到队列
- [ ] 下载完成后自动更新包管理器界面的包状态
- [ ] 支持取消正在下载的任务
- [ ] 支持重试失败的任务
- [ ] 可清除已完成/失败的任务

### 功能 2: 快速安装
- [ ] 已缓存包显示 "Install" 按钮
- [ ] 点击后跳转到预填充向导
- [ ] 跳过发行版选择步骤
- [ ] 实例名自动生成且可修改

### 功能 3: 快速模式
- [ ] 显示模式选择对话框
- [ ] 快速模式只需 3 步（选择、命名、确认）
- [ ] 自动使用 Root、默认路径、WSL 2
- [ ] 标准模式保持完整流程

### 功能 4: 实时日志
- [ ] 安装进度显示可展开日志面板
- [ ] 实时流式显示 PowerShell 输出
- [ ] 支持滚动和自动滚动到底部
- [ ] 可以复制日志内容

### 功能 5: 更新源
- [ ] 独立的 "Update Sources" 按钮
- [ ] 与 "Refresh" 功能明确区分
- [ ] 显示更新进度和结果

### 功能 6: 自动保存
- [ ] 离开设置页时检测未保存更改
- [ ] 提示用户保存
- [ ] 支持取消导航

---

**文档维护**: 每个功能完成后更新此文档的完成状态
**代码审查**: 每个 Phase 完成后进行代码审查
**用户反馈**: Beta 测试后根据反馈调整优先级

---

## 📚 附录：PowerShell 脚本模式最佳实践

### A. 常见操作的 PowerShell 封装模式

#### 1. WSL 操作

```csharp
// ✅ 正确：通过 PowerShell 调用 wsl.exe
public async Task<List<WslInstance>> GetInstancesAsync()
{
    var script = @"
        # 获取 WSL 实例列表
        $wslOutput = wsl --list --verbose
        
        # 解析输出并转为 JSON
        $instances = @()
        foreach ($line in $wslOutput | Select-Object -Skip 1) {
            if ($line -match '^\s*(\*?)\s*(.+?)\s+(Stopped|Running)\s+(\d+)') {
                $instances += @{
                    Name = $Matches[2].Trim()
                    State = $Matches[3]
                    Version = $Matches[4]
                    IsDefault = $Matches[1] -eq '*'
                }
            }
        }
        
        $instances | ConvertTo-Json -Depth 10
    ";
    
    var result = await _powerShell.ExecuteScriptAsync(script);
    return JsonSerializer.Deserialize<List<WslInstance>>(result.Output);
}
```

#### 2. 文件系统操作

```csharp
// ✅ 正确：通过 PowerShell 操作文件
public async Task<bool> DeleteFileAsync(string filePath)
{
    var script = $@"
        $path = '{filePath.Replace("'", "''")}'  # 转义单引号
        
        try {{
            if (Test-Path $path) {{
                Remove-Item -Path $path -Force
                return $true
            }}
            return $false
        }}
        catch {{
            Write-Error $_.Exception.Message
            return $false
        }}
    ";
    
    var result = await _powerShell.ExecuteScriptAsync(script);
    return result.ExitCode == 0;
}
```

#### 3. 注册表操作

```csharp
// ✅ 正确：通过 PowerShell 读取注册表
public async Task<Dictionary<string, object>> GetWslRegistryDataAsync()
{
    var script = @"
        $regPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss'
        
        if (Test-Path $regPath) {
            $data = Get-ItemProperty -Path $regPath
            
            # 转换为哈希表
            $result = @{}
            $data.PSObject.Properties | ForEach-Object {
                if ($_.Name -notlike 'PS*') {
                    $result[$_.Name] = $_.Value
                }
            }
            
            $result | ConvertTo-Json -Depth 10
        } else {
            @{} | ConvertTo-Json
        }
    ";
    
    var result = await _powerShell.ExecuteScriptAsync(script);
    return JsonSerializer.Deserialize<Dictionary<string, object>>(result.Output);
}
```

#### 4. 进程管理

```csharp
// ✅ 正确：通过 PowerShell 启动进程
public async Task<bool> OpenFileExplorerAsync(string folderPath)
{
    var script = $@"
        $path = '{folderPath.Replace("'", "''")}'
        
        if (Test-Path $path) {{
            Start-Process explorer.exe -ArgumentList $path
            return $true
        }}
        return $false
    ";
    
    var result = await _powerShell.ExecuteScriptAsync(script);
    return result.ExitCode == 0;
}
```

#### 5. 网络操作

```csharp
// ✅ 正确：通过 PowerShell 检测网络连接
public async Task<bool> TestNetworkConnectivityAsync(string url)
{
    var script = $@"
        try {{
            $response = Invoke-WebRequest -Uri '{url}' -Method Head -TimeoutSec 5 -UseBasicParsing
            return $response.StatusCode -eq 200
        }}
        catch {{
            return $false
        }}
    ";
    
    var result = await _powerShell.ExecuteScriptAsync(script);
    return bool.TryParse(result.Output, out var isConnected) && isConnected;
}
```

---

### B. 错误处理模式

```csharp
// PowerShell 脚本中的标准错误处理
var script = @"
    $ErrorActionPreference = 'Stop'  # 遇到错误立即停止
    
    try {
        # 执行操作
        wsl --import MyDistro C:\WSL\MyDistro C:\Downloads\distro.tar
        
        # 返回成功结果
        @{
            Success = $true
            Message = 'Import completed'
        } | ConvertTo-Json
    }
    catch {
        # 返回错误信息
        @{
            Success = $false
            Error = $_.Exception.Message
            ErrorDetails = $_.ErrorDetails.Message
        } | ConvertTo-Json
    }
";

// C# 端解析结果
var result = await _powerShell.ExecuteScriptAsync(script);
var response = JsonSerializer.Deserialize<PowerShellResponse>(result.Output);

if (!response.Success)
{
    _logger.LogError("PowerShell operation failed: {Error}", response.Error);
    throw new InvalidOperationException(response.Error);
}
```

---

### C. 参数安全注入

```csharp
// ✅ 安全：使用参数化方式避免注入攻击
public async Task<bool> ImportWslDistroAsync(string name, string installPath, string tarPath)
{
    // 方式 1：在脚本中定义变量（推荐）
    var script = $@"
        # 在 PowerShell 中定义变量，避免直接拼接
        $distroName = '{EscapePowerShellString(name)}'
        $installPath = '{EscapePowerShellString(installPath)}'
        $tarPath = '{EscapePowerShellString(tarPath)}'
        
        # 参数验证
        if (-not (Test-Path $tarPath)) {{
            throw 'Tar file not found'
        }}
        
        # 执行操作
        wsl --import $distroName $installPath $tarPath
    ";
    
    var result = await _powerShell.ExecuteScriptAsync(script);
    return result.ExitCode == 0;
}

// 辅助方法：转义 PowerShell 字符串
private string EscapePowerShellString(string input)
{
    return input
        .Replace("'", "''")           // 单引号转义
        .Replace("`", "``")           // 反引号转义
        .Replace("$", "`$")           // 美元符号转义
        .Replace("\n", "`n")          // 换行转义
        .Replace("\r", "`r");         // 回车转义
}
```

---

### D. 性能优化模式

```csharp
// ✅ 批量操作：一次 PowerShell 调用完成多个任务
public async Task<BatchResult> PerformBatchOperationsAsync(List<string> instanceNames)
{
    var namesJson = JsonSerializer.Serialize(instanceNames);
    
    var script = $@"
        # 从 JSON 导入参数
        $names = '{namesJson}' | ConvertFrom-Json
        
        $results = @()
        foreach ($name in $names) {{
            try {{
                wsl --terminate $name
                $results += @{{
                    Name = $name
                    Success = $true
                }}
            }}
            catch {{
                $results += @{{
                    Name = $name
                    Success = $false
                    Error = $_.Exception.Message
                }}
            }}
        }}
        
        $results | ConvertTo-Json -Depth 10
    ";
    
    var result = await _powerShell.ExecuteScriptAsync(script);
    return JsonSerializer.Deserialize<BatchResult>(result.Output);
}
```

---

### E. 代码审查检查清单

在提交代码前，请确认：

- [ ] **所有系统调用都通过服务层**
  - 搜索 ViewModel 中的 `Process.Start`, `File.`, `Directory.`, `Registry.`
  - 确保没有直接 P/Invoke 调用

- [ ] **PowerShell 脚本参数已转义**
  - 使用 `EscapePowerShellString()` 或参数化方式
  - 防止注入攻击

- [ ] **错误处理完善**
  - PowerShell 脚本使用 `try/catch`
  - C# 端检查 `result.ExitCode`
  - 记录详细日志

- [ ] **返回结果使用 JSON 格式**
  - 便于解析复杂对象
  - 避免字符串解析错误

- [ ] **服务接口定义清晰**
  - 接口方法命名符合规范
  - 包含完整 XML 文档注释
  - 取消令牌支持

---

**文档版本**: v1.1 - 添加 PowerShell 模块调用规范  
**最后更新**: 2026-01-29
