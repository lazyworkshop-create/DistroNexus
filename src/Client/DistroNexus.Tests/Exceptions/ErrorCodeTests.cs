using DistroNexus.Core.Exceptions;

namespace DistroNexus.Tests.Exceptions;

/// <summary>
/// Unit tests for the DistroNexusErrorCode enum and its integration with exceptions (E-09).
/// </summary>
public class ErrorCodeTests
{
    // -----------------------------------------------------------------------
    // Enum definition tests
    // -----------------------------------------------------------------------

    [Fact]
    public void DistroNexusErrorCode_HasInstanceLifecycleCodes_1xxx()
    {
        Assert.Equal(1001, (int)DistroNexusErrorCode.InstanceNotFound);
        Assert.Equal(1002, (int)DistroNexusErrorCode.InstanceAlreadyRunning);
        Assert.Equal(1003, (int)DistroNexusErrorCode.InstanceAlreadyStopped);
        Assert.Equal(1004, (int)DistroNexusErrorCode.InstanceAlreadyExists);
    }

    [Fact]
    public void DistroNexusErrorCode_HasDiskVhdxCodes_2xxx()
    {
        Assert.Equal(2001, (int)DistroNexusErrorCode.VhdxNotFound);
        Assert.Equal(2002, (int)DistroNexusErrorCode.VhdxAccessDenied);
        Assert.Equal(2003, (int)DistroNexusErrorCode.CompactionFailed);
    }

    [Fact]
    public void DistroNexusErrorCode_HasDockerCodes_3xxx()
    {
        Assert.Equal(3001, (int)DistroNexusErrorCode.DockerDesktopNotFound);
        Assert.Equal(3002, (int)DistroNexusErrorCode.DockerConfigWriteConflict);
    }

    [Fact]
    public void DistroNexusErrorCode_HasBackupExportCodes_4xxx()
    {
        Assert.Equal(4001, (int)DistroNexusErrorCode.ExportFailed);
        Assert.Equal(4002, (int)DistroNexusErrorCode.ImportFailed);
        Assert.Equal(4003, (int)DistroNexusErrorCode.BackupDestinationFull);
        Assert.Equal(4004, (int)DistroNexusErrorCode.ScheduleCreateFailed);
    }

    [Fact]
    public void DistroNexusErrorCode_HasConfigCodes_5xxx()
    {
        Assert.Equal(5001, (int)DistroNexusErrorCode.WslConfigReadFailed);
        Assert.Equal(5002, (int)DistroNexusErrorCode.WslConfigWriteFailed);
        Assert.Equal(5003, (int)DistroNexusErrorCode.RegistryAccessDenied);
    }

    [Fact]
    public void DistroNexusErrorCode_HasSystemCodes_9xxx()
    {
        Assert.Equal(9001, (int)DistroNexusErrorCode.WslNotInstalled);
        Assert.Equal(9002, (int)DistroNexusErrorCode.WslVersionTooLow);
        Assert.Equal(9999, (int)DistroNexusErrorCode.UnknownError);
    }

    // -----------------------------------------------------------------------
    // WslException carries DistroNexusErrorCode
    // -----------------------------------------------------------------------

    [Fact]
    public void WslException_CanCarry_DistroNexusCode()
    {
        var ex = new WslException("Instance not found", DistroNexusErrorCode.InstanceNotFound);
        Assert.Equal(DistroNexusErrorCode.InstanceNotFound, ex.Code);
        Assert.Equal(1001, (int)ex.Code);
    }

    [Fact]
    public void WslException_DefaultCode_IsUnknownError()
    {
        var ex = new WslException("Something went wrong");
        Assert.Equal(DistroNexusErrorCode.UnknownError, ex.Code);
    }

    // -----------------------------------------------------------------------
    // WslOperationException carries DistroNexusErrorCode
    // -----------------------------------------------------------------------

    [Fact]
    public void WslInstanceNotFoundException_HasCode_InstanceNotFound()
    {
        var ex = new WslInstanceNotFoundException("Ubuntu-22.04");
        Assert.Equal(DistroNexusErrorCode.InstanceNotFound, ex.Code);
    }

    [Fact]
    public void WslExportFailedException_HasCode_ExportFailed()
    {
        var ex = new WslExportFailedException("Ubuntu-22.04", "Access denied");
        Assert.Equal(DistroNexusErrorCode.ExportFailed, ex.Code);
    }

    [Fact]
    public void WslImportFailedException_HasCode_ImportFailed()
    {
        var ex = new WslImportFailedException("Ubuntu-22.04", "Invalid archive");
        Assert.Equal(DistroNexusErrorCode.ImportFailed, ex.Code);
    }

    // -----------------------------------------------------------------------
    // Error code string representation
    // -----------------------------------------------------------------------

    [Fact]
    public void DistroNexusErrorCode_ToString_ReturnsReadableName()
    {
        Assert.Equal("InstanceNotFound", DistroNexusErrorCode.InstanceNotFound.ToString());
        Assert.Equal("UnknownError", DistroNexusErrorCode.UnknownError.ToString());
    }

    [Fact]
    public void DistroNexusErrorCode_FullyQualifiedId_Format()
    {
        var code = DistroNexusErrorCode.InstanceNotFound;
        var fqid = $"DistroNexus.{code}";
        Assert.Equal("DistroNexus.InstanceNotFound", fqid);
    }
}
