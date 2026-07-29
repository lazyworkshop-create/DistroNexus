using System.Reflection;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using DistroNexus.Desktop.Wizard.Steps;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class TemplateApplyStepTests
{
    [Fact]
    public async Task RecoveryDeclineWithoutConsent_DoesNotStartOperation()
    {
        var client = Client();
        client.Setup(x => x.PreviewTemplateApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateApplyPreviewResult(null, new RecoveryOffer(true, "Ubuntu", RecoveryOfferReason.TemplateApplication, "offer"), true, false, [], [], null));
        SetRecoveryConfirmation(() => false);
        try { await EnterAsync(client.Object); }
        finally { SetRecoveryConfirmation(null); }
        client.Verify(x => x.StartTemplateApplyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_UsesOnlyPreviewTokenAndRendersSucceededTerminalStatus()
    {
        var client = Client();
        client.Setup(x => x.PreviewTemplateApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateApplyPreviewResult("token", new RecoveryOffer(false, "Ubuntu", RecoveryOfferReason.TemplateApplication, ""), false, false, [], [], null));
        client.Setup(x => x.StartTemplateApplyAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyExecuteResult("op"));
        client.Setup(x => x.GetTemplateApplyOperationStatusAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyOperationStatus("op", TemplateOperationState.Succeeded, 1, 1, "setup", "done", null, ["setup"]));
        var step = await EnterAsync(client.Object);
        client.Verify(x => x.StartTemplateApplyAsync("token", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("applied", step.Context!.ResultMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptedRecoveryDecline_RepreviewsThenStartsOnlyReturnedToken()
    {
        var client = Client();
        client.SetupSequence(x => x.PreviewTemplateApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateApplyPreviewResult(null, new RecoveryOffer(true, "Ubuntu", RecoveryOfferReason.TemplateApplication, "offer"), true, false, [], [], null))
            .ReturnsAsync(new TemplateApplyPreviewResult("approved-token", new RecoveryOffer(true, "Ubuntu", RecoveryOfferReason.TemplateApplication, "offer"), false, false, [], [], null));
        client.Setup(x => x.StartTemplateApplyAsync("approved-token", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyExecuteResult("op"));
        client.Setup(x => x.GetTemplateApplyOperationStatusAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyOperationStatus("op", TemplateOperationState.Succeeded, 0, 0, null, "done", null, []));
        SetRecoveryConfirmation(() => true);
        try { await EnterAsync(client.Object); }
        finally { SetRecoveryConfirmation(null); }
        client.Verify(x => x.PreviewTemplateApplyAsync("Ubuntu", "template", It.IsAny<IReadOnlyDictionary<string, string>>(), false, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.PreviewTemplateApplyAsync("Ubuntu", "template", It.IsAny<IReadOnlyDictionary<string, string>>(), true, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(x => x.StartTemplateApplyAsync("approved-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublicCancelCommand_WaitsForOperationIdAndPreservesCancelledTerminalState()
    {
        var client = Client(); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource<TemplateApplyExecuteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Setup(x => x.PreviewTemplateApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyPreviewResult("token", new RecoveryOffer(false, "Ubuntu", RecoveryOfferReason.TemplateApplication, ""), false, false, [], [], null));
        client.Setup(x => x.StartTemplateApplyAsync("token", It.IsAny<CancellationToken>())).Returns(async () => { started.TrySetResult(); return await release.Task; });
        client.Setup(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyCancelResult("op", true, TemplateOperationState.Cancelled));
        client.Setup(x => x.GetTemplateApplyOperationStatusAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyOperationStatus("op", TemplateOperationState.Cancelled, 0, 1, null, "cancelled", null, []));
        var step = new TemplateApplyStep(client.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger>()); var workflow = new WizardWorkflow(); workflow.AddStep(step); workflow.Context.ApplyTemplateAfterInstall = true; workflow.Context.InstanceName = "Ubuntu"; workflow.Context.SelectedTemplate = new Template { Id = "template", Name = "Template" };
        var enter = workflow.StartAsync(); await started.Task; SetCancelConfirmation(() => true);
        try { var cancel = step.CancelCommand.ExecuteAsync(null); release.TrySetResult(new TemplateApplyExecuteResult("op")); await Task.WhenAll(enter, cancel); }
        finally { SetCancelConfirmation(null); }
        client.Verify(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(workflow.Context.InstallFailed);
        Assert.Contains("cancelled", workflow.Context.ResultMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(TemplateOperationState.Succeeded, false)]
    [InlineData(TemplateOperationState.Failed, false)]
    [InlineData(TemplateOperationState.Succeeded, true)]
    public async Task PublicCancelCommand_PreservesActualTerminalStateWhenCancellationIsRejectedOrFails(TemplateOperationState terminalState, bool cancellationThrows)
    {
        var client = Client();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<TemplateApplyExecuteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Setup(x => x.PreviewTemplateApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyPreviewResult("token", new RecoveryOffer(false, "Ubuntu", RecoveryOfferReason.TemplateApplication, ""), false, false, [], [], null));
        client.Setup(x => x.StartTemplateApplyAsync("token", It.IsAny<CancellationToken>())).Returns(async () => { started.TrySetResult(); return await release.Task; });
        if (cancellationThrows)
            client.Setup(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("cancel rejected"));
        else
            client.Setup(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyCancelResult("op", false, TemplateOperationState.Running));
        client.Setup(x => x.GetTemplateApplyOperationStatusAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyOperationStatus("op", terminalState, 1, 1, null, terminalState.ToString(), null, []));

        var step = new TemplateApplyStep(client.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger>());
        var workflow = new WizardWorkflow(); workflow.AddStep(step); workflow.Context.ApplyTemplateAfterInstall = true; workflow.Context.InstanceName = "Ubuntu"; workflow.Context.SelectedTemplate = new Template { Id = "template", Name = "Template" };
        var enter = workflow.StartAsync(); await started.Task; SetCancelConfirmation(() => true);
        try { var cancel = step.CancelCommand.ExecuteAsync(null); release.TrySetResult(new TemplateApplyExecuteResult("op")); await Task.WhenAll(enter, cancel); }
        finally { SetCancelConfirmation(null); }

        client.Verify(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(terminalState == TemplateOperationState.Failed, workflow.Context.InstallFailed);
        Assert.DoesNotContain("cancelled by user", workflow.Context.ResultMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, TemplateOperationState.Running)]
    [InlineData(true, TemplateOperationState.Succeeded)]
    [InlineData(true, TemplateOperationState.Cancelled)]
    public async Task Cancel_UsesTypedResultAndOnlyMapsActualCancelledTerminalState(bool accepted, TemplateOperationState state)
    {
        var client = Client();
        client.Setup(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyCancelResult("op", accepted, state));
        client.Setup(x => x.GetTemplateApplyOperationStatusAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyOperationStatus("op", state, 0, 1, null, state.ToString(), null, []));
        var step = new TemplateApplyStep(client.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger>()) { Context = new WizardContext() };
        await InvokePrivateAsync(step, "RequestCancellationAsync", "op");
        client.Verify(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(state == TemplateOperationState.Cancelled, step.Context.InstallFailed);
    }

    [Fact]
    public async Task CancelBeforeOperationId_StartsThenUsesDurableOperationId()
    {
        var client = Client();
        var step = new TemplateApplyStep(client.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger>());
        client.Setup(x => x.PreviewTemplateApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), false, It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyPreviewResult("token", new RecoveryOffer(false, "Ubuntu", RecoveryOfferReason.TemplateApplication, ""), false, false, [], [], null));
        client.Setup(x => x.StartTemplateApplyAsync("token", It.IsAny<CancellationToken>())).Returns(() => { typeof(TemplateApplyStep).GetField("_cancellationRequested", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(step, true); return Task.FromResult(new TemplateApplyExecuteResult("op")); });
        client.Setup(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyCancelResult("op", true, TemplateOperationState.Cancelled));
        client.Setup(x => x.GetTemplateApplyOperationStatusAsync("op", It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateApplyOperationStatus("op", TemplateOperationState.Cancelled, 0, 1, null, "cancelled", null, []));
        await EnterAsync(client.Object, step);
        client.Verify(x => x.CancelTemplateApplyAsync("op", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IPowerShellModuleClient> Client() => new();
    private static async Task<TemplateApplyStep> EnterAsync(IPowerShellModuleClient client, TemplateApplyStep? step = null)
    {
        step ??= new TemplateApplyStep(client, Mock.Of<Microsoft.Extensions.Logging.ILogger>());
        var workflow = new WizardWorkflow(); workflow.AddStep(step);
        workflow.Context.ApplyTemplateAfterInstall = true; workflow.Context.InstanceName = "Ubuntu"; workflow.Context.SelectedTemplate = new Template { Id = "template", Name = "Template" };
        await workflow.StartAsync(); return step;
    }
    private static async Task InvokePrivateAsync(object instance, string name, string argument)
    {
        var task = (Task)instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, [argument])!;
        await task;
    }
    private static void SetRecoveryConfirmation(Func<bool>? value) => typeof(TemplateApplyStep).GetProperty("RecoveryDeclineConfirmationOverride", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);
    private static void SetCancelConfirmation(Func<bool>? value) => typeof(TemplateApplyStep).GetProperty("CancelConfirmationOverride", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, value);
}
