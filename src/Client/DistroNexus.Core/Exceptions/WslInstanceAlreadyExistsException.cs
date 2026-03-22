using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Exception thrown when trying to create a WSL instance that already exists.
/// </summary>
[Serializable]
public class WslInstanceAlreadyExistsException : WslOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WslInstanceAlreadyExistsException"/> class.
    /// </summary>
    /// <param name="instanceName">The name of the instance that already exists.</param>
    public WslInstanceAlreadyExistsException(string instanceName)
        : base($"WSL instance '{instanceName}' already exists.", DistroNexusErrorCode.InstanceAlreadyExists, operation: "CreateInstance", instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslInstanceAlreadyExistsException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="instanceName">The name of the instance that already exists.</param>
    public WslInstanceAlreadyExistsException(string message, string instanceName)
        : base(message, DistroNexusErrorCode.InstanceAlreadyExists, operation: "CreateInstance", instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslInstanceAlreadyExistsException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="instanceName">The name of the instance that already exists.</param>
    public WslInstanceAlreadyExistsException(string message, Exception? innerException, string? instanceName = null)
        : base(message, innerException, DistroNexusErrorCode.InstanceAlreadyExists, operation: "CreateInstance", instanceName: instanceName)
    {
    }
}
