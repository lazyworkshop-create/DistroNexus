using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Exception thrown when a WSL operation times out.
/// </summary>
[Serializable]
public class WslOperationTimeoutException : WslOperationException
{
    /// <summary>
    /// Gets the timeout duration in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationTimeoutException"/> class.
    /// </summary>
    /// <param name="operation">The operation that timed out.</param>
    /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationTimeoutException(string operation, int timeoutSeconds, string? instanceName = null)
        : base($"Operation '{operation}' timed out after {timeoutSeconds} seconds.", operation: operation, instanceName: instanceName)
    {
        TimeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="operation">The operation that timed out.</param>
    /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationTimeoutException(string message, string operation, int timeoutSeconds, string? instanceName = null)
        : base(message, operation: operation, instanceName: instanceName)
    {
        TimeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="operation">The operation that timed out.</param>
    /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    public WslOperationTimeoutException(string message, Exception? innerException, string operation, int timeoutSeconds, string? instanceName = null)
        : base(message, innerException, operation: operation, instanceName: instanceName)
    {
        TimeoutSeconds = timeoutSeconds;
    }
}
