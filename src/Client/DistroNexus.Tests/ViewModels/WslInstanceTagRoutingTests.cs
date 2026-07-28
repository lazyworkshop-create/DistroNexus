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
    public void MainInstanceLoad_CallsTheTypedModuleClient()
    {
        var stateMachine = typeof(MainViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith("<LoadInstancesAsync>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Contains(nameof(IPowerShellModuleClient.GetInstancesAsync), CalledMethodNames(moveNext));
    }

    [Theory]
    [InlineData("StopAsync")]
    [InlineData("ExportInstanceAsync")]
    public void StopOperations_CallTheTypedModuleClient(string operation)
    {
        var stateMachine = typeof(WslInstanceViewModel)
            .GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith($"<{operation}>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Contains(nameof(IPowerShellModuleClient.StopInstanceAsync), CalledMethodNames(moveNext));
    }

    [Theory]
    [InlineData("AddTagAsync", "AddInstanceTagAsync")]
    [InlineData("RemoveTagAsync", "RemoveInstanceTagAsync")]
    [InlineData("RenameAsync", "RenameInstanceTagsAsync")]
    [InlineData("RemoveAsync", "SetInstanceTagsAsync")]
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
    [InlineData("RemoveAsync")]
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
        Mock.Of<IWslManagerService>(),
        Mock.Of<INavigationService>(),
        Mock.Of<IDownloadTaskManager>(),
        Mock.Of<IServiceProvider>(),
        Mock.Of<ILogger<MainViewModel>>(),
        Mock.Of<IWslEventWatcher>(),
        moduleClient,
        Mock.Of<IBackupService>(),
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
