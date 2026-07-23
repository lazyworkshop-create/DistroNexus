using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for applying templates to WSL instances.
/// </summary>
public class TemplateService : ITemplateService
{
    private const int MissingDistributionRetryCount = 4;
    private const int MissingDistributionRetryDelayMs = 300;

    private static readonly JsonSerializerOptions TemplateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<TemplateService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IPowerShellService _powerShellService;
    private readonly HttpClient _httpClient;
    private readonly IRecoveryOfferService? _recoveryOfferService;
    private readonly ITemplateMarketplaceService? _marketplaceService;
    private List<Template>? _cachedTemplates;
    private readonly string _templatesCachePath;
    private readonly string _userTemplatesDirectory;
    private readonly string _localTemplatesPath;
    private readonly string _applicationHistoryPath;

    public TemplateService(
        ILogger<TemplateService> logger,
        ISettingsService settingsService,
        IPowerShellService powerShellService,
        HttpClient httpClient,
        IRecoveryOfferService? recoveryOfferService = null,
        ITemplateMarketplaceService? marketplaceService = null,
        string? appDataDirectory = null,
        string? localTemplatesPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _recoveryOfferService = recoveryOfferService;
        _marketplaceService = marketplaceService;

        var appFolder = appDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        _templatesCachePath = Path.Combine(appFolder, "templates.json");
        _userTemplatesDirectory = Path.Combine(appFolder, "templates");
        _applicationHistoryPath = Path.Combine(appFolder, "template-application-history.json");

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _localTemplatesPath = localTemplatesPath ?? FindLocalTemplatesPath(baseDir);
    }

    private static string FindLocalTemplatesPath(string baseDir)
    {
        return AppResourcePathResolver.FindFileInBaseOrParents(baseDir, Path.Combine("config", "templates.json"));
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

        if (_marketplaceService is not null)
        {
            foreach (var entry in await _marketplaceService.DiscoverAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var manifest = entry.Manifest;
                    var materialized = entry.CanExecute ? LoadVerifiedMarketplaceTemplate(entry, manifest) : null;
                    templates.Add(materialized ?? new Template
                    {
                        Id = manifest.Id, Name = manifest.Name, Version = manifest.Version,
                        Description = entry.ExecutionReason,
                        Category = "Marketplace", IsCustom = true, SourceUrl = entry.Source.Url,
                        PublisherFingerprint = manifest.PublisherFingerprint, TrustState = entry.TrustState,
                        Capabilities = manifest.Capabilities.ToList(), ArtifactSha256 = manifest.ArtifactSha256,
                        IsRemoteV2 = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Marketplace discovery could not be refreshed; existing local templates remain available.");
                }
            }
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
                if (isOfficial)
                {
                    // Built-in templates are product-owned content, not mutable marketplace
                    // sources. Keep their provenance visible alongside marketplace templates.
                    template.SourceUrl = "distronexus://built-in";
                    template.PublisherFingerprint = "DistroNexus";
                    template.TrustState = TemplateTrustState.BuiltIn;
                }
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
                                    t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    t.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    t.ScenarioTags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
    }

    public async Task<TemplateApplicationResult> ApplyTemplateAsync(string templateId, string instanceName, Dictionary<string, string>? variables = null, IProgress<TemplateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateByIdAsync(templateId, cancellationToken);
        if (template == null)
            throw new WslOperationFailedException(
                $"Template {templateId} not found.",
                DistroNexusErrorCode.TemplateNotFound,
                operation: "ApplyTemplate",
                instanceName: instanceName);

        // A catalog entry alone, including legacy v1 imports, is never a script execution
        // authority. Only the reviewed v2 artifact's template.json is materialized above.
        if (template.IsRemoteV2)
            throw new WslOperationFailedException("Remote marketplace content must be downloaded, verified, and materialized before execution.", DistroNexusErrorCode.TemplateTrustRequired, "ApplyTemplate", instanceName);

        TemplateManifestV2? marketplaceManifest = null;
        if (template.TrustState != TemplateTrustState.BuiltIn && !string.IsNullOrWhiteSpace(template.SourceUrl))
        {
            if (_marketplaceService is null)
                throw new WslOperationFailedException("Marketplace execution service is unavailable.", DistroNexusErrorCode.TemplateTrustRequired, "ApplyTemplate", instanceName);
            marketplaceManifest = await _marketplaceService.GetAuthorizedManifestForExecutionAsync(template.SourceUrl, template.Id, template.MarketplaceManifestDigest, template.ArtifactSha256, cancellationToken).ConfigureAwait(false) ?? throw new WslOperationFailedException("No exact reviewed marketplace manifest is available.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "ApplyTemplate", instanceName);
            var artifact = await _marketplaceService.GetVerifiedArtifactForExecutionAsync(template.SourceUrl, marketplaceManifest, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(template.ArtifactSha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetFullPath(template.MarketplaceArtifactRoot), Path.GetFullPath(artifact.RootPath), StringComparison.OrdinalIgnoreCase))
                throw new WslOperationFailedException("Marketplace template materialization does not match the reviewed artifact.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "ApplyTemplate", instanceName);
        }

        var result = new TemplateApplicationResult { ExecutedScripts = new List<string>(), Errors = new List<string>() };
        if (template.Capabilities.Any(x => x is TemplateCapability.Root or TemplateCapability.FilesystemPaths or TemplateCapability.ServiceChanges))
            result.RecoveryRecommendation = "A recovery point is recommended before this high-impact template execution.";
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

        // Preserve declaration/preflight evidence with the install record. Do not persist raw
        // preflight commands: they can contain user data and are not needed to establish the
        // declared contract later.
        var declaration = await ValidateTemplateAsync(template, instanceName).ConfigureAwait(false);
        var preflightErrors = template.PreflightChecks.Where(x => x.Required && (string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Command)))
            .Select(x => "Required preflight declaration is incomplete: " + (string.IsNullOrWhiteSpace(x.Id) ? "<missing id>" : x.Id)).ToArray();
        instanceHistoryRecord.DeclaredHealthSnapshot = new TemplateDeclaredHealthSnapshot(
            declaration.IsValid && preflightErrors.Length == 0,
            declaration.Errors.Concat(preflightErrors).Select(SensitiveDataRedactor.Redact).ToArray(),
            template.PreflightChecks.Where(x => x.Required).Select(x => x.Id).Order(StringComparer.Ordinal).ToArray(),
            template.PreflightChecks.Select(x => x.Id).Order(StringComparer.Ordinal).ToArray(),
            template.Version,
            template.Scripts.OrderBy(x => x.Order).Select(x => x.Name).ToArray(),
            RuntimePreflightContracts: template.PreflightChecks
                .Where(IsRuntimeHealthSafePreflight)
                .Select(x => new TemplateRuntimePreflightContract(x.Id, x.Required, x.Command.Trim()))
                .OrderBy(x => x.Id, StringComparer.Ordinal).ToArray());

        _logger.LogInformation(
            "Applying template {TemplateId} ({TemplateName}) to instance {InstanceName}; Origin={Origin}",
            template.Id,
            template.Name,
            instanceName,
            template.IsCustom ? "custom" : "official");

        variables ??= new Dictionary<string, string>();
        var effectiveVariables = CreateEffectiveVariables(template, variables);
        instanceHistoryRecord.ResolvedVariables = effectiveVariables.ToDictionary(x => x.Key, x => SensitiveDataRedactor.Redact(x.Value), StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(template.SourceUrl))
            instanceHistoryRecord.MarketplaceProvenance = new TemplateMarketplaceApplicationProvenance(template.SourceUrl, template.PublisherFingerprint, template.ArtifactSha256, template.Version);

        _logger.LogInformation(
            "Template execution selections; TemplateId={TemplateId}; InstallMode={InstallMode}; Selections={Selections}; OutputArtifacts={OutputArtifacts}",
            template.Id,
            template.InstallMode,
            JsonSerializer.Serialize(instanceHistoryRecord.ResolvedVariables),
            JsonSerializer.Serialize(template.OutputArtifacts.Select(a => new { a.Type, a.Path, a.Optional })));
        
        reportProgress(0, "Initiating template application...", 0, template.Scripts.Count);

        try
        {
            if (template.PreflightChecks.Count > 0)
            {
                reportProgress(0, "Running preflight checks...", 0, template.Scripts.Count);
                await ExecutePreflightChecksAsync(template, instanceName, effectiveVariables, cancellationToken, progress);
            }

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
                        template,
                        instanceName,
                        effectiveVariables,
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
                        result.Errors.Add($"Script {script.Name} failed: {SensitiveDataRedactor.Redact(ex.Message)}");
                        _logger.LogWarning(
                            "Template script executed; TemplateId={TemplateId}; ScriptName={ScriptName}; Phase={Phase}; Source={Source}; Result={Result}; DurationMs={DurationMs}; Error={Error}",
                            template.Id,
                            script.Name,
                            script.Phase,
                            string.IsNullOrWhiteSpace(script.ScriptPath) ? "Content" : "ScriptPath",
                            "Failed-Continue",
                            scriptStopwatch.ElapsedMilliseconds,
                            SensitiveDataRedactor.Redact(ex.Message));
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
            result.Message = SensitiveDataRedactor.Redact(ex.Message);
            result.Errors.Add(SensitiveDataRedactor.Redact(ex.Message));
        }
        finally
        {
            result.Duration = DateTime.Now - startTime;
            instanceHistoryRecord.Success = result.Success;
            instanceHistoryRecord.ExecutedScripts = result.ExecutedScripts;
            instanceHistoryRecord.Errors = result.Errors;
            instanceHistoryRecord.Duration = result.Duration;
            // This is install-time evidence, not a later catalog validation. A successful
            // install whose declared scripts did not actually run is a durable drift finding.
            if (instanceHistoryRecord.DeclaredHealthSnapshot is { } persistedDeclaration)
                instanceHistoryRecord.DeclaredHealthSnapshot = persistedDeclaration with { AppliedScriptIds = result.ExecutedScripts.Order(StringComparer.Ordinal).ToArray() };

            await AppendApplicationHistoryAsync(instanceHistoryRecord, cancellationToken);
        }

        // Promotion is deliberately the last successful application step. A failed,
        // cancelled, or partially executed candidate leaves the prior known-good pointer
        // intact and remains only a reviewed candidate for diagnostics/retry.
        if (result.Success && template.TrustState != TemplateTrustState.BuiltIn && !string.IsNullOrWhiteSpace(template.SourceUrl) && _marketplaceService is not null)
        {
            if (marketplaceManifest is not null) await _marketplaceService.CompleteSuccessfulExecutionAsync(template.SourceUrl, marketplaceManifest, cancellationToken).ConfigureAwait(false);
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

    public Task<RecoveryOffer> GetRecoveryOfferAsync(string instanceName, CancellationToken cancellationToken = default) =>
        _recoveryOfferService?.GetOfferAsync(instanceName, RecoveryOfferReason.TemplateApplication, cancellationToken)
        ?? Task.FromResult(new RecoveryOffer(false, instanceName, RecoveryOfferReason.TemplateApplication, "RecoveryOffer.Unavailable"));

    // Health Center may only replay fixed existence checks.  Everything else is evaluated only
    // during the user-authorized template application, never during a background health scan.
    private static bool IsRuntimeHealthSafePreflight(TemplatePreflightCheck check) =>
        check.Type == TemplateScriptType.Bash && TemplateRuntimePreflightEvaluator.IsSafeCommand(check.Command);

    public Task RefreshTemplatesAsync(CancellationToken cancellationToken = default)
    {
        return LoadTemplatesAsync(true, cancellationToken);
    }

    public Task<TemplateValidationResult> ValidateTemplateAsync(Template template, string? distributionName = null)
    {
        var result = new TemplateValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(template.Id))
        {
            result.Errors.Add("Template Id is required.");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            result.Errors.Add("Template Name is required.");
        }

        if (template.Scripts.Count == 0)
        {
            result.Errors.Add("At least one script is required.");
        }

        foreach (var script in template.Scripts)
        {
            if (string.IsNullOrWhiteSpace(script.Content) && string.IsNullOrWhiteSpace(script.ScriptPath))
            {
                result.Errors.Add($"Script '{script.Name}' must specify Content or ScriptPath.");
            }
        }

        if (template.InstallMode == TemplateInstallMode.VersionManager && template.VersionOptions.Count == 0)
        {
            result.Warnings.Add("InstallMode is VersionManager but VersionOptions is empty.");
        }

        var categoryNeedsScenario = new[] { "CloudNative", "DataAndAI", "Database", "DevOps", "Platform" };
        if (categoryNeedsScenario.Contains(template.Category, StringComparer.OrdinalIgnoreCase) && template.ScenarioTags.Count == 0)
        {
            result.Warnings.Add($"Category '{template.Category}' should declare at least one ScenarioTag.");
        }

        foreach (var versionOption in template.VersionOptions)
        {
            if (string.IsNullOrWhiteSpace(versionOption.Key))
            {
                result.Errors.Add("Version option key is required.");
                continue;
            }

            if (versionOption.Required)
            {
                var hasDefaultSelection = template.DefaultSelections.ContainsKey(versionOption.Key) ||
                                          !string.IsNullOrWhiteSpace(versionOption.DefaultValue);
                if (!hasDefaultSelection)
                {
                    result.Warnings.Add($"Version option '{versionOption.Key}' has no default selection.");
                }
            }
        }
        
        if (!string.IsNullOrEmpty(distributionName) && template.CompatibleDistros != null && template.CompatibleDistros.Count > 0)
        {
            // Simple check: compatible if list contains distro name (case-insensitive partial match for simplicity)
            bool isCompatible = template.CompatibleDistros.Any(d => distributionName.Contains(d, StringComparison.OrdinalIgnoreCase));
            if (!isCompatible)
            {
                result.Warnings.Add($"Template may not be compatible with {distributionName}");
            }
        }

        result.IsValid = result.Errors.Count == 0;
        
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
        Template template,
        string instanceName,
        Dictionary<string, string> variables,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        string? resolvedScriptPath = null;
        string scriptContent = script.Content;
        if (string.IsNullOrWhiteSpace(scriptContent) && !string.IsNullOrWhiteSpace(script.ScriptPath))
        {
            resolvedScriptPath = ResolveAndValidateScriptPath(script.ScriptPath, template.MarketplaceArtifactRoot);
            if (resolvedScriptPath == null)
            {
                throw new FileNotFoundException($"Script file not found for path: {script.ScriptPath}");
            }

            scriptContent = await File.ReadAllTextAsync(resolvedScriptPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(template.SourceUrl))
            {
                var expected = template.MarketplaceExecutableFiles.SingleOrDefault(x => string.Equals(x.Path.Replace('\\', '/'), script.ScriptPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
                await using var executable = File.OpenRead(resolvedScriptPath);
                var executableHash = Convert.ToHexString(await SHA256.HashDataAsync(executable, cancellationToken)).ToLowerInvariant();
                if (expected is null || !string.Equals(executableHash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new WslOperationFailedException("Marketplace executable file no longer matches the reviewed manifest.", DistroNexusErrorCode.TemplateArtifactIntegrityFailed, "ExecuteTemplateScript", instanceName);
            }
        }
        
        foreach (var kvp in variables)
        {
            scriptContent = scriptContent.Replace($"${{{kvp.Key}}}", kvp.Value);
        }
        
        if (script.Type == TemplateScriptType.Bash)
        {
            scriptContent = NormalizeBashScriptContent(scriptContent);
            var scriptDirectoryPath = string.IsNullOrWhiteSpace(resolvedScriptPath)
                ? null
                : Path.GetDirectoryName(resolvedScriptPath);
            var scriptFileName = string.IsNullOrWhiteSpace(resolvedScriptPath)
                ? null
                : Path.GetFileName(resolvedScriptPath);
            var command = BuildBashExecutionCommand(scriptContent, instanceName, scriptDirectoryPath, scriptFileName);
                        var output = await ExecuteWithDistributionRetryAsync(
                                command,
                                script.TimeoutSeconds,
                                instanceName,
                                cancellationToken,
                                onOutputLine);
            return new ScriptExecutionResult(string.IsNullOrWhiteSpace(script.ScriptPath) ? "Content" : "ScriptPath", output);
        }
        else if (script.Type == TemplateScriptType.PowerShell) 
        {
               var output = await ExecuteWithTimeoutAsync(scriptContent, script.TimeoutSeconds, cancellationToken, onOutputLine);
             return new ScriptExecutionResult(string.IsNullOrWhiteSpace(script.ScriptPath) ? "Content" : "ScriptPath", output);
        }

        return new ScriptExecutionResult("Content", string.Empty);
    }

    private string? ResolveAndValidateScriptPath(string scriptPath, string? immutableArtifactRoot = null)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return null;
        }

        if (Path.IsPathRooted(scriptPath))
        {
            throw new WslOperationFailedException(
                $"Absolute script path is not allowed: {scriptPath}",
                DistroNexusErrorCode.TemplateScriptFailed,
                operation: "ResolveTemplateScript");
        }

        if (!string.IsNullOrWhiteSpace(immutableArtifactRoot))
        {
            var root = Path.GetFullPath(immutableArtifactRoot) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(immutableArtifactRoot, scriptPath));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new WslOperationFailedException("Marketplace executable path is outside the immutable artifact root or unavailable.", DistroNexusErrorCode.TemplateScriptFailed, "ResolveTemplateScript");
            return path;
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
                throw new WslOperationFailedException(
                    $"Script path traversal detected: {scriptPath}",
                    DistroNexusErrorCode.TemplateScriptFailed,
                    operation: "ResolveTemplateScript");
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

        return await _powerShellService.ExecuteScriptStreamingAsync(command, onOutputLine, line => onOutputLine($"[STDERR] {line}"), cancellationToken);
    }

    private async Task<string> ExecuteWithDistributionRetryAsync(
        string command,
        int timeoutSeconds,
        string instanceName,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        for (var attempt = 1; attempt <= MissingDistributionRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await ExecuteWithTimeoutAsync(command, timeoutSeconds, cancellationToken, onOutputLine);
            }
            catch (InvalidOperationException ex) when (IsDistributionNotFoundError(ex.Message) && attempt < MissingDistributionRetryCount)
            {
                _logger.LogWarning(
                    ex,
                    "Template script target distribution not ready yet. Instance={InstanceName}; Attempt={Attempt}/{MaxAttempts}. Retrying...",
                    instanceName,
                    attempt,
                    MissingDistributionRetryCount);

                await Task.Delay(MissingDistributionRetryDelayMs, cancellationToken);
            }
        }

        throw new WslOperationFailedException(
            $"PowerShell script failed: There is no distribution with the supplied name: {instanceName}",
            DistroNexusErrorCode.InstanceNotFound,
            operation: "ExecuteTemplateScript",
            instanceName: instanceName);
    }

    private static bool IsDistributionNotFoundError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalizedMessage = message.Replace("\0", string.Empty, StringComparison.Ordinal);
        return normalizedMessage.Contains("There is no distribution with the supplied name", StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<string, string> CreateEffectiveVariables(Template template, Dictionary<string, string> runtimeVariables)
    {
        var effective = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in template.Variables)
        {
            effective[kv.Key] = kv.Value;
        }

        foreach (var kv in template.DefaultSelections)
        {
            effective[kv.Key] = kv.Value;
        }

        foreach (var option in template.VersionOptions)
        {
            if (string.IsNullOrWhiteSpace(option.Key))
            {
                continue;
            }

            if (!effective.ContainsKey(option.Key) && !string.IsNullOrWhiteSpace(option.DefaultValue))
            {
                effective[option.Key] = option.DefaultValue;
            }
        }

        foreach (var kv in runtimeVariables)
        {
            effective[kv.Key] = kv.Value;
        }

        return effective;
    }

    private async Task ExecutePreflightChecksAsync(
        Template template,
        string instanceName,
        Dictionary<string, string> variables,
        CancellationToken cancellationToken,
        IProgress<TemplateProgress>? progress)
    {
        foreach (var check in template.PreflightChecks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ShouldRunPreflightCheck(check, variables))
            {
                continue;
            }

            var command = BuildPreflightCommand(check, instanceName);
            try
            {
                var output = await ExecuteWithTimeoutAsync(command, 60, cancellationToken);
                progress?.Report(new TemplateProgress
                {
                    PercentComplete = 0,
                    StatusMessage = $"Preflight passed: {check.Name}",
                    LatestOutput = output
                });
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(check.ErrorMessage)
                    ? $"Preflight check failed: {check.Name}. {ex.Message}"
                    : check.ErrorMessage;

                if (check.Required)
                {
                    throw new WslOperationFailedException(
                        message,
                        ex,
                        DistroNexusErrorCode.TemplateScriptFailed,
                        operation: "TemplatePreflight",
                        instanceName: instanceName);
                }

                _logger.LogWarning(ex, "Optional preflight check failed: {CheckName}", check.Name);
            }
        }
    }

    private static bool ShouldRunPreflightCheck(TemplatePreflightCheck check, Dictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(check.AppliesToVariable))
        {
            return true;
        }

        if (!variables.TryGetValue(check.AppliesToVariable, out var value))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(check.AppliesToValue))
        {
            return true;
        }

        return string.Equals(value, check.AppliesToValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPreflightCommand(TemplatePreflightCheck check, string instanceName)
    {
        if (check.Type == TemplateScriptType.Bash)
        {
            return BuildBashExecutionCommand(NormalizeBashScriptContent(check.Command), instanceName);
        }

        return check.Command;
    }

    private static string BuildBashExecutionCommand(
        string scriptContent,
        string instanceName,
        string? scriptDirectoryPath = null,
        string? scriptFileName = null)
    {
        var stagedPaths = StageBashScriptForExecution(scriptContent, scriptDirectoryPath, scriptFileName);
        var escapedInstanceName = EscapeForPowerShellSingleQuotedString(instanceName);
        var escapedStagedScriptWslPath = EscapeForPowerShellSingleQuotedString(stagedPaths.StagedScriptWslPath);
        var escapedStagingRootWindowsPath = EscapeForPowerShellSingleQuotedString(stagedPaths.StagingRootWindowsPath);
        return $"wsl -d '{escapedInstanceName}' -- bash '{escapedStagedScriptWslPath}'; $exitCode = $LASTEXITCODE; Remove-Item -LiteralPath '{escapedStagingRootWindowsPath}' -Recurse -Force -ErrorAction SilentlyContinue; if ($exitCode -ne 0) {{ exit $exitCode }}";
    }

    private static StagedBashScriptPaths StageBashScriptForExecution(string scriptContent, string? scriptDirectoryPath, string? scriptFileName)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "DistroNexus", "template-stage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingRoot);

        string stagedScriptWindowsPath;
        if (!string.IsNullOrWhiteSpace(scriptDirectoryPath) && Directory.Exists(scriptDirectoryPath) && !string.IsNullOrWhiteSpace(scriptFileName))
        {
            var templateRootDirectory = Directory.GetParent(scriptDirectoryPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(templateRootDirectory) && Directory.Exists(templateRootDirectory))
            {
                CopyDirectoryTreeWithNormalizedLineEndings(templateRootDirectory, stagingRoot);

                var relativeScriptPath = Path.GetRelativePath(templateRootDirectory, Path.Combine(scriptDirectoryPath, scriptFileName));
                stagedScriptWindowsPath = Path.Combine(stagingRoot, relativeScriptPath);
            }
            else
            {
                var fallbackScriptDirectory = Path.Combine(stagingRoot, "script");
                Directory.CreateDirectory(fallbackScriptDirectory);
                stagedScriptWindowsPath = Path.Combine(fallbackScriptDirectory, scriptFileName);
            }
        }
        else
        {
            var fallbackScriptDirectory = Path.Combine(stagingRoot, "script");
            Directory.CreateDirectory(fallbackScriptDirectory);
            var targetScriptFileName = string.IsNullOrWhiteSpace(scriptFileName) ? "script.sh" : scriptFileName;
            stagedScriptWindowsPath = Path.Combine(fallbackScriptDirectory, targetScriptFileName);
        }

        var stagedScriptDirectory = Path.GetDirectoryName(stagedScriptWindowsPath);
        if (!string.IsNullOrWhiteSpace(stagedScriptDirectory))
        {
            Directory.CreateDirectory(stagedScriptDirectory);
        }

        File.WriteAllText(stagedScriptWindowsPath, scriptContent, new UTF8Encoding(false));

        return new StagedBashScriptPaths(
            stagingRoot,
            stagedScriptWindowsPath,
            ConvertWindowsPathToWslPath(stagedScriptWindowsPath));
    }

    private static void CopyDirectoryTreeWithNormalizedLineEndings(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var sourceFile in Directory.GetFiles(sourceDirectory))
        {
            var fileName = Path.GetFileName(sourceFile);
            var targetFile = Path.Combine(targetDirectory, fileName);
            var content = NormalizeBashScriptContent(File.ReadAllText(sourceFile));
            File.WriteAllText(targetFile, content, new UTF8Encoding(false));
        }

        foreach (var sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
        {
            var directoryName = Path.GetFileName(sourceSubDirectory);
            var targetSubDirectory = Path.Combine(targetDirectory, directoryName);
            CopyDirectoryTreeWithNormalizedLineEndings(sourceSubDirectory, targetSubDirectory);
        }
    }

    private static string NormalizeBashScriptContent(string scriptContent)
    {
        var normalized = scriptContent.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        if (normalized.Length > 0 && normalized[0] == '\uFEFF')
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    private static string EscapeForPowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }

    private static string ConvertWindowsPathToWslPath(string windowsPath)
    {
        var fullPath = Path.GetFullPath(windowsPath);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root) || root.Length < 2 || root[1] != ':')
        {
            throw new WslOperationFailedException(
                $"Unsupported Windows path for WSL conversion: {windowsPath}",
                DistroNexusErrorCode.TemplateScriptFailed,
                operation: "ConvertWindowsPathToWslPath");
        }

        var drive = char.ToLowerInvariant(root[0]);
        var relativePath = fullPath[root.Length..].Replace('\\', '/');
        return $"/mnt/{drive}/{relativePath}";
    }

    private sealed record StagedBashScriptPaths(string StagingRootWindowsPath, string StagedScriptWindowsPath, string StagedScriptWslPath);

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

    private Template? LoadVerifiedMarketplaceTemplate(TemplateMarketplaceEntry entry, TemplateManifestV2 manifest)
    {
        var path = entry.KnownGoodArtifact is null ? null : Path.Combine(entry.KnownGoodArtifact.RootPath, "template.json");
        if (path is null || !File.Exists(path) || new FileInfo(path).Length > 1024 * 1024) return null;
        try
        {
            var template = JsonSerializer.Deserialize<Template>(File.ReadAllText(path), TemplateJsonOptions);
            if (template is null || !string.Equals(template.Id, manifest.Id, StringComparison.Ordinal) || template.Scripts.Count == 0) return null;
            template.SourceUrl = entry.Source.Url;
            template.PublisherFingerprint = manifest.PublisherFingerprint;
            template.TrustState = entry.TrustState;
            template.Capabilities = manifest.Capabilities.ToList();
            template.ArtifactSha256 = manifest.ArtifactSha256;
            template.MarketplaceManifestDigest = entry.ManifestDigest;
            template.MarketplaceArtifactRoot = entry.KnownGoodArtifact!.RootPath;
            template.MarketplaceExecutableFiles = manifest.ExecutableFiles.ToList();
            template.IsRemoteV2 = false;
            template.IsCustom = true;
            template.IsOfficial = false;
            return template;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Verified marketplace artifact has an invalid template definition; it remains browse-only.");
            return null;
        }
    }
}
