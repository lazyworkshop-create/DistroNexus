using System.Reflection;
using System.Reflection.Emit;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.ViewModels;

public sealed class SettingsViewModelRoutingTests
{
    [Fact]
    public async Task LoadSettingsCommand_LoadsModeledSettingsThroughTheModuleClient()
    {
        var client = new Mock<IPowerShellModuleClient>();
        client.Setup(x => x.GetBootstrapSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BootstrapSettingsResult(new GlobalSettings { Theme = "Dark", Language = "zh-CN", DefaultWslVersion = 1 }, "Ready"));
        client.Setup(x => x.GetStoreComplianceStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoreComplianceStatusResult(false, "Ready"));
        var viewModel = NewViewModel(client.Object);

        await viewModel.LoadSettingsCommand.ExecuteAsync(null);

        Assert.Equal("Dark", viewModel.Theme);
        Assert.Equal("zh-CN", viewModel.Language);
        Assert.Equal(1, viewModel.DefaultWslVersion);
        client.Verify(x => x.GetBootstrapSettingsAsync(It.IsAny<CancellationToken>()), Times.Once);
        viewModel.Dispose();
    }

    [Fact]
    public async Task SaveSettingsCommand_SendsTheTypedSettingsUpdateBeforePresentationCompletes()
    {
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Mock<IPowerShellModuleClient>();
        client.Setup(x => x.SaveSettingsAsync(It.Is<DistroNexusSettingsUpdate>(update => update.Theme == "Light"), It.IsAny<CancellationToken>()))
            .Callback(() => saveStarted.SetResult())
            .Returns(Task.Delay(Timeout.Infinite));
        var viewModel = NewViewModel(client.Object);
        viewModel.Theme = "Light";

        _ = viewModel.SaveSettingsCommand.ExecuteAsync(null);
        await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        client.Verify(x => x.SaveSettingsAsync(It.IsAny<DistroNexusSettingsUpdate>(), It.IsAny<CancellationToken>()), Times.Once);
        viewModel.Dispose();
    }

    [Fact]
    public async Task AutoSave_UsesTheTypedSettingsUpdate()
    {
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Mock<IPowerShellModuleClient>();
        client.Setup(x => x.SaveSettingsAsync(It.IsAny<DistroNexusSettingsUpdate>(), It.IsAny<CancellationToken>()))
            .Callback(() => saveStarted.SetResult())
            .Returns(Task.Delay(Timeout.Infinite));
        var viewModel = NewViewModel(client.Object);
        viewModel.IsDirty = true;

        typeof(SettingsViewModel).GetMethod("OnAutoSaveTimerElapsed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(viewModel, [null, null]);
        await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        client.Verify(x => x.SaveSettingsAsync(It.IsAny<DistroNexusSettingsUpdate>(), It.IsAny<CancellationToken>()), Times.Once);
        viewModel.Dispose();
    }

    [Theory]
    [InlineData("SaveSettingsAsync", nameof(IPowerShellModuleClient.SaveSettingsAsync))]
    [InlineData("ResetSettingsAsync", nameof(IPowerShellModuleClient.ResetSettingsAsync))]
    public void SettingsCommands_CallTheirTypedModuleMethods(string operation, string moduleMethod)
    {
        var stateMachine = typeof(SettingsViewModel).GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith($"<{operation}>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Contains(moduleMethod, CalledMethodNames(moveNext));
    }

    private static SettingsViewModel NewViewModel(IPowerShellModuleClient client) => new(
        Mock.Of<ICatalogService>(), Mock.Of<ILogger<SettingsViewModel>>(), Mock.Of<IWslManagerService>(), client,
        Mock.Of<IDialogService>());

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
