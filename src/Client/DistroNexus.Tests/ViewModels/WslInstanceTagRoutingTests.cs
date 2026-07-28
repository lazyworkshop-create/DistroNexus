using System.Reflection;
using System.Reflection.Emit;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class WslInstanceTagRoutingTests
{
    [Fact]
    public void InstanceRemoveWorkflow_DoesNotCallBackupScheduleCleanup()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DistroNexus.Desktop", "ViewModels", "WslInstanceViewModel.cs"));
        Assert.DoesNotContain("RemoveBackupScheduleAsync", source);
        Assert.DoesNotContain("GetBackupSchedulesAsync", source);
    }
    [Fact]
    public void MainInstanceLoad_CallsTheTypedModuleClient()
    {
        var stateMachine = typeof(MainViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith("<LoadInstancesAsync>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Contains(nameof(IPowerShellModuleClient.GetInstancesAsync), CalledMethodNames(moveNext));
    }

    [Fact]
    public void MainRefresh_DoesNotBypassTheTypedInstanceList()
    {
        var stateMachine = typeof(MainViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith("<RefreshAsync>", StringComparison.Ordinal));
        var methods = CalledMethodNames(stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!);

        Assert.DoesNotContain("ForceRefreshInstanceAsync", methods);
    }

    [Theory]
    [InlineData("ForceRefreshAsync", nameof(IPowerShellModuleClient.GetInstancesAsync), "ForceRefreshInstanceAsync")]
    [InlineData("StartAsync", nameof(IPowerShellModuleClient.StartInstanceWithResultAsync), "StartInstanceWithKeepAliveAsync")]
    public void LifecyclePresentationOperations_UseTypedModuleClientWithoutCoveredManagerBypass(string operation, string requiredMethod, string forbiddenMethod)
    {
        var stateMachine = typeof(WslInstanceViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith($"<{operation}>", StringComparison.Ordinal));
        var methods = CalledMethodNames(stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!);

        Assert.Contains(requiredMethod, methods);
        Assert.DoesNotContain(forbiddenMethod, methods);
        if (operation == "ForceRefreshAsync") Assert.DoesNotContain("LoadDiskSizeAsync", methods);
    }

    [Fact]
    public void DiskSizePresentation_UsesTypedInstanceListWithoutManagerBypass()
    {
        var stateMachine = typeof(WslInstanceViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith("<LoadDiskSizeAsync>", StringComparison.Ordinal));
        var methods = CalledMethodNames(stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!);

        Assert.Contains(nameof(IPowerShellModuleClient.GetInstancesAsync), methods);
        Assert.DoesNotContain("GetInstanceDiskSizeAsync", methods);
    }

    [Fact]
    public async Task LoadDiskSizeAsync_RequestsTheMeasuredDiskProjectionThroughTheModuleClient()
    {
        var moduleClient = new Mock<IPowerShellModuleClient>(MockBehavior.Strict);
        moduleClient
            .Setup(client => client.GetInstancesAsync(
                It.Is<InstanceListRequest>(request => !request.SkipDiskSize && !request.ForceRefresh),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WslInstance { Name = "Ubuntu", Size = 2048 }]);
        var viewModel = new WslInstanceViewModel(
            new WslInstance { Name = "Ubuntu", State = "Running" },
            Mock.Of<ILogger>(),
            moduleClient.Object,
            Mock.Of<IDialogService>());

        await viewModel.LoadDiskSizeAsync();

        Assert.Equal(2048, viewModel.DiskSize);
        moduleClient.VerifyAll();
    }

    [Theory]
    [InlineData("RemoveAsync", nameof(IPowerShellModuleClient.PreviewRemoveInstanceAsync))]
    [InlineData("MoveAsync", nameof(IPowerShellModuleClient.PreviewMoveInstanceAsync))]
    [InlineData("RenameAsync", nameof(IPowerShellModuleClient.PreviewRenameInstanceAsync))]
    [InlineData("ExportInstanceAsync", nameof(IPowerShellModuleClient.PreviewExportInstanceAsync))]
    public void PathLifecycleOperations_UseTypedPreviewAndTokenExecution(string operation, string previewMethod)
    {
        var stateMachine = typeof(WslInstanceViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith($"<{operation}>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var methods = CalledMethodNames(moveNext).ToArray();
        Assert.Contains(previewMethod, methods);
        Assert.Contains(nameof(IPowerShellModuleClient.ExecuteLifecycleOperationAsync), methods);
        Assert.DoesNotContain("RemoveInstanceAsync", methods);
        Assert.DoesNotContain("MoveInstanceAsync", methods);
        Assert.DoesNotContain("RenameInstanceAsync", methods);
        Assert.DoesNotContain("ExportInstanceAsync", methods);
    }

    [Theory]
    [InlineData("AddTagAsync", "AddInstanceTagAsync")]
    [InlineData("RemoveTagAsync", "RemoveInstanceTagAsync")]
    public void TagMutations_CallTheTypedModuleClient(string operation, string moduleMethod)
    {
        var stateMachine = typeof(WslInstanceViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith($"<{operation}>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Contains(moduleMethod, CalledMethodNames(moveNext));
    }

    [Theory]
    [InlineData("StopAsync")]
    public void ConfirmationOperations_GetSettingsThroughTheTypedModuleClient(string operation)
    {
        var stateMachine = typeof(WslInstanceViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith($"<{operation}>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Contains(nameof(IPowerShellModuleClient.GetSettingsAsync), CalledMethodNames(moveNext));
    }

    [Fact]
    public void NamedSettingsPresentationTypes_DoNotReferenceISettingsService()
    {
        var namedTypes = new[] { typeof(MainViewModel), typeof(WslInstanceViewModel), typeof(SettingsViewModel) };

        Assert.All(namedTypes, type => Assert.DoesNotContain(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(ISettingsService)));
    }

    [Fact]
    public async Task InitializeAsync_LoadsUserPreferencesThroughTheModuleClient()
    {
        var moduleClient = new Mock<IPowerShellModuleClient>();
        moduleClient.Setup(client => client.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalSettings { Theme = "Light", Language = "zh-CN" });
        var viewModel = NewMainViewModel(moduleClient.Object);

        await viewModel.InitializeAsync();

        Assert.Equal("Light", viewModel.CurrentTheme);
        Assert.Equal("zh-CN", viewModel.CurrentLanguage);
        moduleClient.Verify(client => client.GetSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadInstancesCommand_LoadsTheDefaultInstancePreferenceThroughTheModuleClient()
    {
        var moduleClient = new Mock<IPowerShellModuleClient>();
        moduleClient.Setup(client => client.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        moduleClient.Setup(client => client.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalSettings { DefaultDistributionId = "Ubuntu" });
        var viewModel = NewMainViewModel(moduleClient.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.LoadInstancesCommand.ExecuteAsync(null));

        moduleClient.Verify(client => client.GetInstancesAsync(It.IsAny<CancellationToken>()), Times.Once);
        moduleClient.Verify(client => client.GetSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MainViewModel NewMainViewModel(IPowerShellModuleClient moduleClient) => new(
        Mock.Of<IServiceProvider>(), Mock.Of<ILogger<MainViewModel>>(), moduleClient, Mock.Of<IDialogService>());

    private static IEnumerable<string> CalledMethodNames(MethodInfo method)
    {
        var body = method.GetMethodBody()!.GetILAsByteArray()!;
        for (var index = 0; index < body.Length;)
        {
            var opcode = body[index++] == 0xfe ? TwoByteOpcodes[body[index++]] : OneByteOpcodes[body[index - 1]];
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(body, index);
                MethodBase? called = null;
                try { called = method.Module.ResolveMethod(token); } catch (ArgumentException) { }
                if (called is not null) yield return called.Name;
            }

            index += OperandSize(opcode.OperandType, body, index);
        }
    }

    private static int OperandSize(OperandType type, byte[] body, int index) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.InlineI or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(body, index) * 4),
        _ => throw new InvalidOperationException($"Unsupported IL operand type: {type}.")
    };

    private static readonly OpCode[] OneByteOpcodes = BuildOpcodes(false);
    private static readonly OpCode[] TwoByteOpcodes = BuildOpcodes(true);

    private static OpCode[] BuildOpcodes(bool twoByte)
    {
        var opcodes = new OpCode[256];
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opcode || (opcode.Size == 2) != twoByte) continue;
            opcodes[opcode.Value & 0xff] = opcode;
        }
        return opcodes;
    }
}
