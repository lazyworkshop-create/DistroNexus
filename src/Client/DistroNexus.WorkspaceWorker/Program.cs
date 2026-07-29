using System.Diagnostics;
using System.Reflection;
using DistroNexus.Core.Services;

// The worker has exactly one caller-controlled value: the Core-issued opaque operation id.
if (args.Length != 1 || args[0].Length != 64 || args[0].Any(c => !Uri.IsHexDigit(c))) return 2;
var root = Environment.GetEnvironmentVariable("DISTRONEXUS_WORKSPACE_STORE_ROOT")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
var operations = new WorkspaceOperationStore(Path.Combine(root, "workspace-operations"));
try
{
    var operation = await operations.ReadAsync(args[0]);
    var bridge = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "DistroNexus.WorkspaceBridge.dll"));
    if (!File.Exists(bridge)) throw new InvalidOperationException();
    WorkspaceWorkerIdentity.EnsureApprovedBridge(AssemblyName.GetAssemblyName(bridge), Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException());
    var bridgeHost = Path.ChangeExtension(bridge, ".exe");
    if (!File.Exists(bridgeHost)) throw new InvalidOperationException();
    var info = new ProcessStartInfo(bridgeHost) { UseShellExecute = false, CreateNoWindow = true };
    info.ArgumentList.Add("--run-workspace-operation"); info.ArgumentList.Add(operation.OperationId); info.Environment["DISTRONEXUS_WORKSPACE_STORE_ROOT"] = root;
    using var process = Process.Start(info) ?? throw new InvalidOperationException();
    await process.WaitForExitAsync();
    return process.ExitCode;
}
catch (Exception)
{
    try { var operation = await operations.ReadAsync(args[0]); await operations.WriteAsync(operation with { IsTerminal = true, Outcome = "Failed", ErrorCode = "Workspace.WorkerStartFailed" }); } catch { }
    return 1;
}
