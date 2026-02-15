using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Exception thrown when importing a WSL distribution fails.
/// </summary>
[Serializable]
public class WslImportFailedException : WslOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WslImportFailedException"/> class.
    /// </summary>
    /// <param name="instanceName">The name of the instance being imported.</param>
    /// <param name="packagePath">The path to the package file.</param>
    public WslImportFailedException(string instanceName, string packagePath)
        : base($"Failed to import WSL instance '{instanceName}' from '{packagePath}'.", operation: "ImportInstance", instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslImportFailedException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="instanceName">The name of the instance being imported.</param>
    public WslImportFailedException(string message, Exception? innerException, string? instanceName = null)
        : base(message, innerException, operation: "ImportInstance", instanceName: instanceName)
    {
    }
}
