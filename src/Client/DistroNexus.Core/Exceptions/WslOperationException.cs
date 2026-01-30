using System;

namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Base exception class for WSL operation failures.
/// </summary>
[Serializable]
public abstract class WslOperationException : Exception
{
    /// <summary>
    /// Gets the name of the operation that failed.
    /// </summary>
    public string? Operation { get; set; }

    /// <summary>
    /// Gets the name of the WSL instance related to this operation.
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    protected WslOperationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected WslOperationException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="operation">The operation that was being performed.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    protected WslOperationException(string message, string? operation = null, string? instanceName = null)
        : base(message)
    {
        Operation = operation;
        InstanceName = instanceName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslOperationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="operation">The operation that was being performed.</param>
    /// <param name="instanceName">The name of the WSL instance.</param>
    protected WslOperationException(string message, Exception? innerException, string? operation = null, string? instanceName = null)
        : base(message, innerException)
    {
        Operation = operation;
        InstanceName = instanceName;
    }
}
