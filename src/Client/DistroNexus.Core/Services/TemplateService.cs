using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for applying templates to WSL instances.
/// </summary>
public class TemplateService : ITemplateService
{
    private static readonly JsonSerializerOptions TemplateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<TemplateService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IPowerShellService _powerShellService;
    private readonly HttpClient _httpClient;
    private List<Template>? _cachedTemplates;
    private readonly string _templatesCachePath;
    private readonly string _userTemplatesDirectory;
    private readonly string _localTemplatesPath;
    private readonly string _applicationHistoryPath;

    public TemplateService(
        ILogger<TemplateService> logger,
        ISettingsService settingsService,
        IPowerShellService powerShellService,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DistroNexus");
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        _templatesCachePath = Path.Combine(appFolder, "templates.json");
        _userTemplatesDirectory = Path.Combine(appFolder, "templates");
        _applicationHistoryPath = Path.Combine(appFolder, "template-application-history.json");

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localTemplatesPath = FindLocalTemplatesPath(baseDir);
    }

    private static string FindLocalTemplatesPath(string baseDir)
    {
        string[] possiblePaths =
        [
            Path.Combine(baseDir, "config", "templates.json"),
            Path.Combine(baseDir, @"..\config\templates.json"),
            Path.Combine(baseDir, @"..\..\config\templates.json"),
            Path.Combine(baseDir, @"..\..\..\config\templates.json"),
            Path.Combine(baseDir, @"..\..\..\..\config\templates.json"),
            Path.Combine(baseDir, @"..\..\..\..\..\config\templates.json")
        ];

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        return Path.Combine(baseDir, "config", "templates.json");
    }

    public async Task<List<Template>> LoadTemplatesAsync(bool forceReload = false, CancellationToken cancellationToken = default)
    {
        if (!forceReload && _cachedTemplates != null)
        {
            return _cachedTemplates;
        }

        var templates = new List<Template>();

        var userTemplates = await LoadTemplatesFromPathAsync(_templatesCachePath, false, true, cancellationToken);
        if (userTemplates.Count > 0)
        {
            templates.AddRange(userTemplates);
            _logger.LogInformation("Loaded {Count} templates from AppData: {Path}", userTemplates.Count, _templatesCachePath);
        }
        else
        {
            var localTemplates = await LoadTemplatesFromPathAsync(_localTemplatesPath, true, false, cancellationToken);
            templates.AddRange(localTemplates);
            _logger.LogInformation("Loaded {Count} templates from local config fallback: {Path}", localTemplates.Count, _localTemplatesPath);
        }

        templates = templates
            .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        _cachedTemplates = templates;
        return templates;
    }

    private async Task<List<Template>> LoadTemplatesFromPathAsync(
        string path,
        bool isOfficial,
        bool isCustom,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var parsed = ParseTemplatesJson(json);
            if (parsed == null)
            {
                return [];
            }

            foreach (var template in parsed)
            {
                template.IsOfficial = isOfficial;
                template.IsCustom = isCustom;
            }

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load templates from {Path}", path);
            return [];
        }
    }

    private async Task SaveUserTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTemplates == null) return;
        var userTemplates = _cachedTemplates.Where(t => t.IsCustom).ToList();
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(userTemplates, options);
        
        var dir = Path.GetDirectoryName(_templatesCachePath);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(_templatesCachePath, json, cancellationToken);
    }

    public async Task<Template?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var templates = await LoadTemplatesAsync(false, cancellationToken);
        return templates.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<Template>> SearchTemplatesAsync(string query, CancellationToken cancellationToken = default)
    {
        var templates = await LoadTemplatesAsync(false, cancellationToken);
        if (string.IsNullOrWhiteSpace(query)) return templates;
        return templates.Where(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    t.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
    }

    public async Task<TemplateApplicationResult> ApplyTemplateAsync(string templateId, string instanceName, Dictionary<string, string>? variables = null, IProgress<TemplateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateByIdAsync(templateId, cancellationToken);
        if (template == null) throw new ArgumentException($"Template {templateId} not found");

        var result = new TemplateApplicationResult { ExecutedScripts = new List<string>(), Errors = new List<string>() };
        var startTime = DateTime.Now;
        var instanceHistoryRecord = new TemplateApplicationRecord
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            InstanceName = instanceName,
            AppliedAt = DateTime.Now,
            Success = false,
            LogFilePath = _applicationHistoryPath
        };

        _logger.LogInformation(
            "Applying template {TemplateId} ({TemplateName}) to instance {InstanceName}; Origin={Origin}",
            template.Id,
            template.Name,
            instanceName,
            template.IsCustom ? "custom" : "official");

        variables ??= new Dictionary<string, string>();
        
        reportProgress(0, "Initiating template application...", 0, template.Scripts.Count);

        try
        {
            int scriptIndex = 0;
            foreach (var script in template.Scripts.OrderBy(s => s.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();
                scriptIndex++;
                reportProgress((double)scriptIndex / template.Scripts.Count * 100, $"Executing script: {script.Name}", scriptIndex, template.Scripts.Count, script.Name);
                var scriptStopwatch = Stopwatch.StartNew();

                try
                {
                    var execution = await ExecuteScriptAsync(
                        script,
                        instanceName,
                        variables,
                        cancellationToken,
                        progress == null
                            ? null
                            : line => reportProgress(
                                (double)scriptIndex / template.Scripts.Count * 100,
                                $"Executing script: {script.Name}",
                                scriptIndex,
                                template.Scripts.Count,
                                script.Name,
                                line));
                    result.ExecutedScripts.Add(script.Name);
                    scriptStopwatch.Stop();

                    reportProgress(
                        (double)scriptIndex / template.Scripts.Count * 100,
                        $"Executed: {script.Name}",
                        scriptIndex,
                        template.Scripts.Count,
                        script.Name,
                        execution.Output);

                    _logger.LogInformation(
                        "Template script executed; TemplateId={TemplateId}; ScriptName={ScriptName}; Phase={Phase}; Source={Source}; Result={Result}; DurationMs={DurationMs}",
                        template.Id,
                        script.Name,
                        script.Phase,
                        execution.Source,
                        "Success",
                        scriptStopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    scriptStopwatch.Stop();
                    if (script.ContinueOnError)
                    {
                        _logger.LogWarning(ex, "Script {ScriptName} failed, but continue on error is enabled.", script.Name);
                        result.Errors.Add($"Script {script.Name} failed: {ex.Message}");
                        _logger.LogWarning(
                            "Template script executed; TemplateId={TemplateId}; ScriptName={ScriptName}; Phase={Phase}; Source={Source}; Result={Result}; DurationMs={DurationMs}; Error={Error}",
                            template.Id,
                            script.Name,
                            script.Phase,
                            string.IsNullOrWhiteSpace(script.ScriptPath) ? "Content" : "ScriptPath",
                            "Failed-Continue",
                            scriptStopwatch.ElapsedMilliseconds,
                            ex.Message);
                    }
                    else
                    {
                        _logger.LogError(
                            ex,
                            "Template script executed; TemplateId={TemplateId}; ScriptName={ScriptName}; Phase={Phase}; Source={Source}; Result={Result}; DurationMs={DurationMs}",
                            template.Id,
                            script.Name,
                            script.Phase,
                            string.IsNullOrWhiteSpace(script.ScriptPath) ? "Content" : "ScriptPath",
                            "Failed-Stop",
                            scriptStopwatch.ElapsedMilliseconds);
                        throw;
                    }
                }
            }
            result.Success = true;
            result.Message = "Template applied successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply template {TemplateId}", templateId);
            result.Success = false;
            result.Message = ex.Message;
            result.Errors.Add(ex.Message);
        }
        finally
        {
            result.Duration = DateTime.Now - startTime;
            instanceHistoryRecord.Success = result.Success;
            instanceHistoryRecord.ExecutedScripts = result.ExecutedScripts;
            instanceHistoryRecord.Errors = result.Errors;
            instanceHistoryRecord.Duration = result.Duration;

            await AppendApplicationHistoryAsync(instanceHistoryRecord, cancellationToken);
        }

        return result;

        void reportProgress(double percent, string message, int current, int total, string currentScript = "", string latestOutput = "")
        {
             progress?.Report(new TemplateProgress
             {
                 PercentComplete = percent,
                 StatusMessage = message,
                 CompletedScripts = current,
                 TotalScripts = total,
                 CurrentScript = currentScript,
                 LatestOutput = latestOutput
             });
        }
    }

    public Task RefreshTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return LoadTemplatesAsync(true, cancellationToken);
    }

    public Task<TemplateValidationResult> ValidateTemplateAsync(Template template, string? distributionName = null)
    {
        var result = new TemplateValidationResult { IsValid = true };
        if (string.IsNullOrWhiteSpace(template.Id)) result.IsValid = false;
        
        if (!string.IsNullOrEmpty(distributionName) && template.CompatibleDistros != null && template.CompatibleDistros.Count > 0)
        {
            // Simple check: compatible if list contains distro name (case-insensitive partial match for simplicity)
            bool isCompatible = template.CompatibleDistros.Any(d => distributionName.Contains(d, StringComparison.OrdinalIgnoreCase));
            if (!isCompatible)
            {
                result.Warnings.Add($"Template may not be compatible with {distributionName}");
            }
        }
        
        return Task.FromResult(result);
    }

    public async Task<bool> IsTemplateCompatibleAsync(string templateId, string distributionName)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null) return false;
        
        if (template.CompatibleDistros == null || template.CompatibleDistros.Count == 0) return true;
        
        return template.CompatibleDistros.Any(d => distributionName.Contains(d, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> AddCustomTemplateAsync(Template template, CancellationToken cancellationToken = default)
    {
        var templates = await LoadTemplatesAsync(false, cancellationToken);
        
        // Check if exists
        var existingIndex = templates.FindIndex(t => t.Id == template.Id && t.IsCustom);
        if (existingIndex >= 0)
        {
            templates[existingIndex] = template;
        }
        else
        {
            templates.Add(template);
        }

        template.IsCustom = true;
        
        _cachedTemplates = templates;
        await SaveUserTemplatesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveCustomTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var templates = await LoadTemplatesAsync(false, cancellationToken);
        var toRemove = templates.FirstOrDefault(t => t.Id == templateId && t.IsCustom);
        
        if (toRemove != null)
        {
            templates.Remove(toRemove);
            _cachedTemplates = templates;
            await SaveUserTemplatesAsync(cancellationToken);
            return true;
        }
        return false;
    }

    public async Task<bool> ExportTemplateAsync(string templateId, string exportPath, CancellationToken cancellationToken = default)
    {
         var template = await GetTemplateByIdAsync(templateId, cancellationToken);
        if (template == null) return false;

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(template, options);
            await File.WriteAllTextAsync(exportPath, json, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export template {Id} to {Path}", templateId, exportPath);
            return false;
        }
    }

    public async Task<Template?> ImportTemplateAsync(string importPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(importPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(importPath, cancellationToken);
            var template = JsonSerializer.Deserialize<Template>(json, TemplateJsonOptions);
            
            if (template != null)
            {
                // Ensure it is marked as custom
                template.IsCustom = true;
                template.IsOfficial = false;

                // Check for ID conflict with official templates
                var templates = await LoadTemplatesAsync(false, cancellationToken);
                if (templates.Any(t => t.Id == template.Id && t.IsOfficial))
                {
                    template.Id += "_Imported";
                    template.Name += " (Imported)";
                }

                await AddCustomTemplateAsync(template, cancellationToken);
                return template;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import template from {Path}", importPath);
        }
        return null;
    }

    public async Task<List<TemplateApplicationRecord>> GetApplicationHistoryAsync(string? instanceName = null)
    {
        var history = await LoadApplicationHistoryAsync();

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return history.OrderByDescending(r => r.AppliedAt).ToList();
        }

        return history
            .Where(r => r.InstanceName.Equals(instanceName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.AppliedAt)
            .ToList();
    }

    public string GetTemplatesCachePath() => _templatesCachePath;

    public string GetTemplateScriptsPath() => _userTemplatesDirectory;

    
    private async Task<ScriptExecutionResult> ExecuteScriptAsync(
        TemplateScript script,
        string instanceName,
        Dictionary<string, string> variables,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        string scriptContent = script.Content;
        if (string.IsNullOrWhiteSpace(scriptContent) && !string.IsNullOrWhiteSpace(script.ScriptPath))
        {
            var resolvedScriptPath = ResolveAndValidateScriptPath(script.ScriptPath);
            if (resolvedScriptPath == null)
            {
                throw new FileNotFoundException($"Script file not found for path: {script.ScriptPath}");
            }

            scriptContent = await File.ReadAllTextAsync(resolvedScriptPath, cancellationToken);
        }
        
        foreach (var kvp in variables)
        {
            scriptContent = scriptContent.Replace($"${{{kvp.Key}}}", kvp.Value);
        }
        
        if (script.Type == TemplateScriptType.Bash)
        {
            // Escape single quotes for bash -c '...' encapsulation
            var escapedContent = scriptContent.Replace("'", "'\\''");
            var command = $"wsl -d {instanceName} -- bash -c '{escapedContent}'";
              var output = await ExecuteWithTimeoutAsync(command, script.TimeoutSeconds, cancellationToken, onOutputLine);
            return new ScriptExecutionResult(string.IsNullOrWhiteSpace(script.ScriptPath) ? "Content" : "ScriptPath", output);
        }
        else if (script.Type == TemplateScriptType.PowerShell) 
        {
               var output = await ExecuteWithTimeoutAsync(scriptContent, script.TimeoutSeconds, cancellationToken, onOutputLine);
             return new ScriptExecutionResult(string.IsNullOrWhiteSpace(script.ScriptPath) ? "Content" : "ScriptPath", output);
        }

        return new ScriptExecutionResult("Content", string.Empty);
    }

    private string? ResolveAndValidateScriptPath(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return null;
        }

        if (Path.IsPathRooted(scriptPath))
        {
            throw new InvalidOperationException($"Absolute script path is not allowed: {scriptPath}");
        }

        var allowedRoots = new List<string>
        {
            Path.GetFullPath(_userTemplatesDirectory),
            Path.GetFullPath(Path.GetDirectoryName(_localTemplatesPath) ?? string.Empty)
        };

        var candidatePaths = new[]
        {
            Path.Combine(_userTemplatesDirectory, scriptPath),
            Path.Combine(Path.GetDirectoryName(_localTemplatesPath) ?? string.Empty, scriptPath)
        };

        foreach (var candidate in candidatePaths)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (!allowedRoots.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Script path traversal detected: {scriptPath}");
            }

            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private async Task<List<TemplateApplicationRecord>> LoadApplicationHistoryAsync()
    {
        if (!File.Exists(_applicationHistoryPath))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_applicationHistoryPath);
            return JsonSerializer.Deserialize<List<TemplateApplicationRecord>>(json, TemplateJsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read template application history from {Path}", _applicationHistoryPath);
            return [];
        }
    }

    private async Task AppendApplicationHistoryAsync(TemplateApplicationRecord record, CancellationToken cancellationToken)
    {
        var history = await LoadApplicationHistoryAsync();
        history.Add(record);

        var retentionCutoff = DateTime.Now.AddDays(-30);
        history = history.Where(h => h.AppliedAt >= retentionCutoff).ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(history, options);
        await File.WriteAllTextAsync(_applicationHistoryPath, json, cancellationToken);
    }

    private async Task<string> ExecuteWithTimeoutAsync(
        string command,
        int timeoutSeconds,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        _ = timeoutSeconds;
        if (onOutputLine == null)
        {
            return await _powerShellService.ExecuteScriptAsync(command, cancellationToken);
        }

        return await _powerShellService.ExecuteScriptStreamingAsync(command, onOutputLine, line => onOutputLine($"[ERR] {line}"), cancellationToken);
    }

    private sealed record ScriptExecutionResult(string Source, string Output);

    private List<Template>? ParseTemplatesJson(string json)
    {
       try {
           return JsonSerializer.Deserialize<List<Template>>(json, TemplateJsonOptions);
       } catch (Exception ex) {
           _logger.LogError(ex, "Error parsing templates json");
           return null;
       }
    }
}
