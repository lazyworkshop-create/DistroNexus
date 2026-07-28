namespace DistroNexus.Core.Models;

public enum TerminalKind { Auto, WindowsTerminal, CommandPrompt }
public sealed record TerminalStatusResult(bool WindowsTerminalAvailable, bool CommandPromptAvailable, TerminalKind DefaultKind);
public sealed record TerminalLaunchResult(bool Succeeded, TerminalKind SelectedKind, string OutcomeCode);
