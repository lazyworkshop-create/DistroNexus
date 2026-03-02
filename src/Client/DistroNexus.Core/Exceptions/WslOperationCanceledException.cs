using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Exception thrown when a WSL operation is canceled.
/// </summary>
[Serializable]
public class WslOperationCanceledException : WslOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationCanceledException"/> class.
    /// </summary>
    /// <param name="operation">The operation that was canceled.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationCanceledException(string operation, string? instanceName = null)
        : base($"Operation '{operation}' was canceled.", DistroNexusErrorCode.UnknownError, operation: operation, instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationCanceledException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="operation">The operation that was canceled.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationCanceledException(string message, string operation, string? instanceName = null)
        : base(message, DistroNexusErrorCode.UnknownError, operation: operation, instanceName: instanceName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationCanceledException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="operation">The operation that was canceled.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationCanceledException(string message, Exception? innerException, string operation, string? instanceName = null)
        : base(message, innerException, DistroNexusErrorCode.UnknownError, operation: operation, instanceName: instanceName)
    {
    }
}
