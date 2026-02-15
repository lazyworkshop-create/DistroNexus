namespace DistroNexus.Core.Exceptions;

/// <summary>
/// Base exception for all WSL-related operations.
/// Provides unified error handling across the DistroNexus application.
/// </summary>
public class WslException : Exception
{
    /// <summary>
    /// Gets the error code from the underlying WSL operation, if available.
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// Gets the error output from the WSL operation, if available.
    /// </summary>
    public string? ErrorOutput { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public WslException(string? message = null) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslException"/> class with a specified error code.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code from the WSL operation.</param>
    public WslException(string? message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslException"/> class with error details.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code from the WSL operation.</param>
    /// <param name="errorOutput">The error output from the WSL operation.</param>
    public WslException(string? message, int errorCode, string? errorOutput) : base(message)
    {
        ErrorCode = errorCode;
        ErrorOutput = errorOutput;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public WslException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WslException"/> class with error details and an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code from the WSL operation.</param>
    /// <param name="errorOutput">The error output from the WSL operation.</param>
    /// <param name="innerException">The inner exception.</param>
    public WslException(string? message, int errorCode, string? errorOutput, Exception? innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ErrorOutput = errorOutput;
    }
}
