using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Converts configuration-boundary failures into the application's stable error contract.</summary>
public static class ConfigurationErrorMapper
{
    public static WslOperationFailedException ToOperationException(Exception exception, string operation, string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is WslOperationException wslFailure)
        {
            return new WslOperationFailedException(SensitiveDataRedactor.Redact(wslFailure.Message), exception,
                wslFailure.Code, operation: wslFailure.Operation ?? "DistributionConfiguration." + operation,
                instanceName: wslFailure.InstanceName ?? instanceName);
        }

        if (exception is WslException wslException)
        {
            return new WslOperationFailedException(SensitiveDataRedactor.Redact(wslException.Message), exception,
                wslException.Code, operation: "DistributionConfiguration." + operation, instanceName: instanceName);
        }

        var (code, detail) = exception switch
        {
            ConfigurationConflictException => (DistroNexusErrorCode.StoreRevisionConflict, exception.Message),
            ConfigurationValidationException validation => (DistroNexusErrorCode.ValidationFailed,
                string.Join(Environment.NewLine, validation.Diagnostics.Select(d => $"{d.Code} (line {d.Line}): {d.Message}"))),
            ConfigurationTransportException transport => (TransportCode(transport.Code), transport.Message),
            IOException => (operation.Equals("read", StringComparison.OrdinalIgnoreCase)
                ? DistroNexusErrorCode.WslConfigReadFailed : DistroNexusErrorCode.WslConfigWriteFailed, exception.Message),
            _ => (operation.Equals("read", StringComparison.OrdinalIgnoreCase)
                ? DistroNexusErrorCode.WslConfigReadFailed : DistroNexusErrorCode.WslConfigWriteFailed, exception.Message)
        };

        return new WslOperationFailedException(SensitiveDataRedactor.Redact(detail), exception, code,
            operation: "DistributionConfiguration." + operation, instanceName: instanceName);
    }

    private static DistroNexusErrorCode TransportCode(string value) => value.EndsWith(".timeout", StringComparison.OrdinalIgnoreCase)
        ? DistroNexusErrorCode.OperationTimeout
        : value.EndsWith(".truncated", StringComparison.OrdinalIgnoreCase)
            ? DistroNexusErrorCode.ProcessOutputLimitExceeded
            : value.EndsWith(".start", StringComparison.OrdinalIgnoreCase)
                ? DistroNexusErrorCode.ProcessStartFailed
                : value.StartsWith("config.read", StringComparison.OrdinalIgnoreCase)
                    ? DistroNexusErrorCode.WslConfigReadFailed
                    : DistroNexusErrorCode.WslConfigWriteFailed;
}
