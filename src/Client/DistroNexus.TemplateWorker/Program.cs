using System.Diagnostics;
using System.Reflection;
using DistroNexus.Core.Services;

// The fixed worker accepts only a Core-issued opaque operation id and delegates to the fixed bridge host.
if (args.Length != 1 || args[0].Length != 64 || args[0].Any(c => !Uri.IsHexDigit(c))) return 2;
var root = Environment.GetEnvironmentVariable("DISTRONEXUS_TEMPLATE_STORE_ROOT")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DistroNexus");
var bridgeAssembly = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "DistroNexus.WorkspaceBridge.dll"));
var bridge = Path.ChangeExtension(bridgeAssembly, ".exe");
if (!File.Exists(bridgeAssembly) || !File.Exists(bridge)) return 1;
try { TemplateWorkerIdentity.EnsureApprovedBridge(AssemblyName.GetAssemblyName(bridgeAssembly), Assembly.GetExecutingAssembly().GetName().Version ?? throw new InvalidOperationException()); } catch { return 1; }
var info = new ProcessStartInfo(bridge) { UseShellExecute = false, CreateNoWindow = true };
info.ArgumentList.Add("--run-template-operation");
info.ArgumentList.Add(args[0]);
info.Environment["DISTRONEXUS_WORKSPACE_STORE_ROOT"] = root;
using var process = Process.Start(info);
if (process is null) return 1;
await process.WaitForExitAsync();
return process.ExitCode;
