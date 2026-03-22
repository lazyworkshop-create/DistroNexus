using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Exception thrown when a WSL operation fails without a more specific typed exception.
/// </summary>
[Serializable]
public class WslOperationFailedException : WslOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationFailedException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="code">The structured error code.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationFailedException(string message, DistroNexusErrorCode code = DistroNexusErrorCode.UnknownError, string? operation = null, string? instanceName = null)
        : base(message, code, operation: operation, instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationFailedException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="code">The structured error code.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationFailedException(string message, Exception? innerException, DistroNexusErrorCode code = DistroNexusErrorCode.UnknownError, string? operation = null, string? instanceName = null)
        : base(message, innerException, code, operation: operation, instanceName: instanceName)
    {
    }
}
