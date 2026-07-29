using System.Reflection;
using System.Reflection.Emit;
using DistroNexus.Core.Interfaces;
using DistroNexus.Desktop.ViewModels;

namespace DistroNexus.Tests.ViewModels;

public sealed class SourceManagerViewModelRoutingTests
{
    [Theory]
    [InlineData("InitializeAsync", nameof(IPowerShellModuleClient.GetCatalogSourcesAsync))]
    [InlineData("RefreshSourcesAsync", nameof(IPowerShellModuleClient.GetCatalogSourcesAsync))]
    [InlineData("SaveSourceAsync", nameof(IPowerShellModuleClient.AddCatalogSourceAsync))]
    [InlineData("SaveSourceAsync", nameof(IPowerShellModuleClient.UpdateCatalogSourceAsync))]
    [InlineData("RemoveSourceAsync", nameof(IPowerShellModuleClient.RemoveCatalogSourceAsync))]
    [InlineData("ToggleSourceActiveAsync", nameof(IPowerShellModuleClient.SetCatalogSourceActiveAsync))]
    [InlineData("TestSourceAsync", nameof(IPowerShellModuleClient.TestCatalogSourceAsync))]
    [InlineData("ResetToDefaultsAsync", nameof(IPowerShellModuleClient.ResetCatalogSourcesAsync))]
    [InlineData("MoveSourceUpAsync", nameof(IPowerShellModuleClient.ReorderCatalogSourcesAsync))]
    [InlineData("MoveSourceDownAsync", nameof(IPowerShellModuleClient.ReorderCatalogSourcesAsync))]
    public void Handler_CallsItsClosedTypedModuleOperation(string handler, string moduleMethod)
    {
        var stateMachine = typeof(SourceManagerViewModel).GetNestedTypes(BindingFlags.NonPublic)
            .Single(type => type.Name.StartsWith($"<{handler}>", StringComparison.Ordinal));
        var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Contains(moduleMethod, CalledMethodNames(moveNext));
    }

    [Fact]
    public void ViewModel_DoesNotKeepACatalogSourceManagerDependency()
    {
        Assert.DoesNotContain(typeof(SourceManagerViewModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(ICatalogSourceManager));
        Assert.DoesNotContain(typeof(SourceManagerViewModel).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ICatalogSourceManager));
    }

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
