using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Exception thrown when exporting a WSL distribution fails.
/// </summary>
[Serializable]
public class WslExportFailedException : WslOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WslExportFailedException"/> class.
    /// </summary>
    /// <param name="instanceName">The name of the instance being exported.</param>
    /// <param name="destinationPath">The destination path for the export.</param>
    public WslExportFailedException(string instanceName, string destinationPath)
        : base($"Failed to export WSL instance '{instanceName}' to '{destinationPath}'.", operation: "ExportInstance", instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslExportFailedException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="instanceName">The name of the instance being exported.</param>
    public WslExportFailedException(string message, string instanceName)
        : base(message, operation: "ExportInstance", instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslExportFailedException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="instanceName">The name of the instance being exported.</param>
    public WslExportFailedException(string message, Exception? innerException, string? instanceName = null)
        : base(message, innerException, operation: "ExportInstance", instanceName: instanceName)
    {
    }
}
