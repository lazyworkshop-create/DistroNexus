using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Service for managing and applying templates to WSL instances.
/// </summary>
public interface ITemplateService
{
    /// <summary>Returns the explicit optional recovery-point offer shown before template mutation; it never performs a backup.</summary>
    Task<RecoveryOffer> GetRecoveryOfferAsync(string instanceName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RecoveryOffer(false, instanceName, RecoveryOfferReason.TemplateApplication, "RecoveryOffer.Unavailable"));
    /// <summary>
    /// Loads all available templates.
    /// </summary>
    /// <param name="forceReload">If true, bypasses cache and reloads templates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available templates</returns>
    Task<List<Template>> LoadTemplatesAsync(bool forceReload = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a template by its ID.
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The template if found, otherwise null</returns>
    Task<Template?> GetTemplateByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for templates matching a query.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching templates</returns>
    Task<List<Template>> SearchTemplatesAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the template catalog from remote source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RefreshTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a template to a WSL instance.
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="instanceName">WSL instance name</param>
    /// <param name="variables">Optional variable overrides</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Application result</returns>
    Task<TemplateApplicationResult> ApplyTemplateAsync(
        string templateId,
        string instanceName,
        Dictionary<string, string>? variables = null,
        IProgress<TemplateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a template for correctness.
    /// </summary>
    /// <param name="template">Template to validate</param>
    /// <param name="distributionName">Optional distribution name for compatibility check</param>
    /// <returns>Validation result</returns>
    Task<TemplateValidationResult> ValidateTemplateAsync(Template template, string? distributionName = null);

    /// <summary>
    /// Checks if a template is compatible with a distribution.
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="distributionName">Distribution name</param>
    /// <returns>True if compatible, otherwise false</returns>
    Task<bool> IsTemplateCompatibleAsync(string templateId, string distributionName);

    /// <summary>
    /// Adds a custom template.
    /// </summary>
    /// <param name="template">Template to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if added successfully</returns>
    Task<bool> AddCustomTemplateAsync(Template template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a custom template.
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if removed successfully</returns>
    Task<bool> RemoveCustomTemplateAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a template to a file.
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="exportPath">Export file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if exported successfully</returns>
    Task<bool> ExportTemplateAsync(string templateId, string exportPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a template from a file.
    /// </summary>
    /// <param name="importPath">Import file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The imported template if successful, otherwise null</returns>
    Task<Template?> ImportTemplateAsync(string importPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the application history for templates.
    /// </summary>
    /// <param name="instanceName">Optional instance name filter</param>
    /// <returns>List of application records</returns>
    Task<List<TemplateApplicationRecord>> GetApplicationHistoryAsync(string? instanceName = null);

    /// <summary>
    /// Gets the path to the templates cache directory.
    /// </summary>
    /// <returns>Templates cache path</returns>
    string GetTemplatesCachePath();

    /// <summary>
    /// Gets the path to the template scripts directory.
    /// </summary>
    /// <returns>Template scripts path</returns>
    string GetTemplateScriptsPath();
}
