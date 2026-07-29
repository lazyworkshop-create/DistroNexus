using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class WorkspaceRuntimeTests
{
    [Fact]
    public async Task DirectoryPreflight_RejectsInvalidPathWithoutStartingProcess()
    {
        var processes = new Mock<IProcessRunner>(MockBehavior.Strict);
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);

        var result = await runtime.CheckAsync(Definition(), new("directory", "../etc", true), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Workspace.Preflight.Invalid", result.Code);
        processes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DirectoryPreflight_ProbesValidatedPathWithExactArgumentVector()
    {
        ProcessRequest? captured = null;
        var processes = new Mock<IProcessRunner>();
        processes.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(Success());
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);

        var result = await runtime.CheckAsync(Definition(), new("directory", "/home/demo", true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal(["--distribution", "Ubuntu", "--exec", "test", "-d", "--", "/home/demo"], captured!.Arguments);
    }

    [Fact]
    public async Task ToolPreflight_UsesApprovedAbsoluteExecutableProbe()
    {
        ProcessRequest? captured = null;
        var processes = new Mock<IProcessRunner>();
        processes.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>())).Callback<ProcessRequest, CancellationToken>((request, _) => captured = request).ReturnsAsync(Success());
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);

        var result = await runtime.CheckAsync(Definition(), new("tool", "git", true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["--distribution", "Ubuntu", "--exec", "test", "-x", "/usr/bin/git"], captured!.Arguments);
    }

    [Fact]
    public async Task InvalidToolPreflight_DoesNotStartProcess()
    {
        var processes = new Mock<IProcessRunner>(MockBehavior.Strict);
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);

        var result = await runtime.CheckAsync(Definition(), new("tool", "git;id", true), CancellationToken.None);

        Assert.False(result.Succeeded);
        processes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BrowserAction_RejectsHttpsUrlWithoutHostBeforeStartingProcess()
    {
        var processes = new Mock<IProcessRunner>(MockBehavior.Strict);
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);

        var result = await runtime.ExecuteAsync(Definition(), new(Guid.NewGuid(), WorkspaceActionType.Browser, "browser", ["https:///path"]), CancellationToken.None);

        Assert.Equal(WorkspaceActionOutcome.Failed, result.Outcome);
        Assert.Equal("Workspace.Action.Invalid", result.Code);
        processes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TerminalAction_UsesExplicitWindowsTerminalNewTabBoundary()
    {
        ProcessRequest? captured = null;
        var processes = new Mock<IProcessRunner>();
        processes.Setup(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ProcessRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(Success());
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);

        var result = await runtime.ExecuteAsync(Definition(), new(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", []), CancellationToken.None);

        Assert.Equal(WorkspaceActionOutcome.Succeeded, result.Outcome);
        Assert.NotNull(captured);
        Assert.Equal("wt.exe", captured!.FileName);
        Assert.Equal(["new-tab", "--", "wsl.exe", "--distribution", "Ubuntu", "--cd", "/home/demo"], captured.Arguments);
    }

    [Fact]
    public async Task SystemdAction_RejectsUnapprovedOperationWithoutStartingProcess()
    {
        var processes = new Mock<IProcessRunner>(MockBehavior.Strict);
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);

        var result = await runtime.ExecuteAsync(Definition(), new(Guid.NewGuid(), WorkspaceActionType.Systemd, "systemd", ["enable", "demo.service"]), CancellationToken.None);

        Assert.Equal(WorkspaceActionOutcome.Failed, result.Outcome);
        Assert.Equal("Workspace.Action.Invalid", result.Code);
        processes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SystemdClose_AggregatesFailedServices()
    {
        var processes = new Mock<IProcessRunner>();
        processes.SetupSequence(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success())
            .ReturnsAsync(new ProcessResult(1, string.Empty, "failed", TimeSpan.Zero, false, false, false, null));
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, processes.Object);
        var definition = Definition() with { ClosePolicy = new(WorkspaceCloseMode.StopSelectedServices, ["one.service", "two.service"]) };

        var result = await runtime.CloseAsync(definition, CancellationToken.None);

        Assert.Equal(WorkspaceActionOutcome.Failed, result.Outcome);
        Assert.Equal("Workspace.Close.ServicesFailed", result.Code);
        Assert.Equal("two.service", result.Detail);
    }

    [Fact]
    public async Task TemplatePreflight_UsesInjectedCheckerAndTreatsUnavailableOptionalCheckAsNonBlocking()
    {
        var checker = new Mock<IWorkspaceTemplatePrerequisiteChecker>();
        checker.Setup(x => x.CheckAsync(It.IsAny<WorkspaceDefinition>(), "dev-template", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceTemplatePrerequisiteResult(true, true, "catalog.ok"));
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, new Mock<IProcessRunner>(MockBehavior.Strict).Object, checker.Object);

        var passed = await runtime.CheckAsync(Definition(), new("template", "dev-template", true), CancellationToken.None);
        var optionalUnavailable = await new WorkspaceRuntime(new Mock<IWslManagerService>().Object, new Mock<IProcessRunner>(MockBehavior.Strict).Object)
            .CheckAsync(Definition(), new("template", "dev-template", false), CancellationToken.None);

        Assert.Equal("Workspace.Preflight.TemplateSatisfied", passed.Code);
        Assert.True(passed.Succeeded);
        Assert.Equal("Workspace.Preflight.TemplateUnavailableOptional", optionalUnavailable.Code);
        Assert.True(optionalUnavailable.Succeeded);
    }

    [Fact]
    public async Task TemplatePreflight_RejectsUnsupportedIdentifierWithoutCallingChecker()
    {
        var checker = new Mock<IWorkspaceTemplatePrerequisiteChecker>(MockBehavior.Strict);
        var runtime = new WorkspaceRuntime(new Mock<IWslManagerService>().Object, new Mock<IProcessRunner>(MockBehavior.Strict).Object, checker.Object);

        var result = await runtime.CheckAsync(Definition(), new("template", "../template", true), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Workspace.Preflight.TemplateInvalid", result.Code);
    }

    private static WorkspaceDefinition Definition() => new(Guid.NewGuid(), "demo", "Ubuntu", "/home/demo", [], [new(Guid.NewGuid(), "launch", false, [new(Guid.NewGuid(), WorkspaceActionType.Terminal, "terminal", [])])], new(), WorkspaceTrustState.Trusted);
    private static ProcessResult Success() => new(0, string.Empty, string.Empty, TimeSpan.Zero, false, false, false, null);
}
