using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Exception thrown when a WSL instance is not found.
/// </summary>
[Serializable]
public class WslInstanceNotFoundException : WslOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WslInstanceNotFoundException"/> class.
    /// </summary>
    /// <param name="instanceName">The name of the instance that was not found.</param>
    public WslInstanceNotFoundException(string instanceName)
        : base($"WSL instance '{instanceName}' not found.", DistroNexusErrorCode.InstanceNotFound, operation: "GetInstance", instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslInstanceNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="instanceName">The name of the instance that was not found.</param>
    public WslInstanceNotFoundException(string message, string instanceName)
        : base(message, DistroNexusErrorCode.InstanceNotFound, operation: "GetInstance", instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslInstanceNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="instanceName">The name of the instance that was not found.</param>
    public WslInstanceNotFoundException(string message, Exception? innerException, string? instanceName = null)
        : base(message, innerException, DistroNexusErrorCode.InstanceNotFound, operation: "GetInstance", instanceName: instanceName)
    {
    }
}
