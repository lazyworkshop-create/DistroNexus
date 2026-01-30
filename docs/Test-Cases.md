# Test Cases Catalog

## Overview
Comprehensive test case inventory for DistroNexus automated testing.

## PowerShell Private Functions

### Cache.ps1
| Test ID | Function | Scenario | Expected Result | Status |
|---------|----------|----------|-----------------|--------|
| CACHE-001 | Get-InstanceCache | Valid cache exists (< 10 min) | Returns cached instances | ✅ |
| CACHE-002 | Get-InstanceCache | Cache expired (> 10 min) | Returns null | ✅ |
| CACHE-003 | Get-InstanceCache | Cache file missing | Returns null | ✅ |
| CACHE-004 | Get-InstanceCache | Corrupted JSON | Returns null, no exception | ✅ |
| CACHE-005 | Set-InstanceCache | Create new cache | File created with timestamp | ✅ |
| CACHE-006 | Set-InstanceCache | Overwrite existing cache | File updated | ✅ |
| CACHE-007 | Set-InstanceCache | Empty instance list | Cache with zero count | ✅ |
| CACHE-008 | Update-InstanceCache | Remove cache file | File deleted | ✅ |
| CACHE-009 | Clear-InstanceCache | Remove cache file | File deleted gracefully | ✅ |

### PackageHandler.ps1
| Test ID | Function | Scenario | Expected Result | Status |
|---------|----------|----------|-----------------|--------|
| PKG-001 | Test-PackageFormat | .tar file | Returns true | ✅ |
| PKG-002 | Test-PackageFormat | .tar.gz file | Returns true | ✅ |
| PKG-003 | Test-PackageFormat | .appx file | Returns true | ✅ |
| PKG-004 | Test-PackageFormat | .zip file | Returns true | ✅ |
| PKG-005 | Test-PackageFormat | Unsupported format | Returns false | ✅ |
| PKG-006 | Get-PackageFormat | .tar.gz | Returns "TarGz" | ✅ |
| PKG-007 | Get-PackageFormat | .appxbundle | Returns "AppxBundle" | ✅ |
| PKG-008 | Get-PackageFormat | Unknown | Returns "Unknown" | ✅ |
| PKG-009 | Test-TarCommand | Windows 10/11 | Returns true | ✅ |

### TerminalLauncher.ps1
| Test ID | Function | Scenario | Expected Result | Status |
|---------|----------|----------|-----------------|--------|
| TERM-001 | Find-TerminalPath | Prefer CMD | Returns CMD path | ✅ |
| TERM-002 | Find-TerminalPath | Auto (WT available) | Returns WT or CMD | ✅ |
| TERM-003 | Find-TerminalPath | WT not found | Fallback to CMD | ✅ |
| TERM-004 | Invoke-Terminal | Launch with instance name | Process started | ✅ |
| TERM-005 | Invoke-Terminal | With StartPath | StartPath in command | ✅ |
| TERM-006 | Invoke-Terminal | WhatIf parameter | No process started | ✅ |
| TERM-007 | Test-TerminalAvailable | CMD check | Returns true | ✅ |
| TERM-008 | Get-AvailableTerminals | List all | Contains CMD | ✅ |

## PowerShell Public Cmdlets

### Get-DistroNexusInstance
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| INST-001 | Default (use cache if valid) | Returns cached instances | ✅ |
| INST-002 | -ForceUpdate | Bypass cache | ✅ |
| INST-003 | -Name "Ubuntu*" | Filtered by wildcard | ✅ |
| INST-004 | -IncludeRelease | Bypass cache, include release info | ✅ |
| INST-005 | Cache not found | Query WSL directly | ✅ |

### Save-DistroNexusPackage
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| SAVE-001 | -Family "Ubuntu" | Download all Ubuntu distros | 📋 |
| SAVE-002 | -All | Download all distros | 📋 |
| SAVE-003 | Concurrent downloads | Max 5 parallel jobs | 📋 |

## C# Services

### PowerShellService.ExecuteModuleCmdletAsync
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| PS-001 | Null cmdlet name | ArgumentNullException | ✅ |
| PS-002 | Valid cmdlet | Returns result | ✅ |
| PS-003 | With parameters | Parameters formatted | ✅ |
| PS-004 | Module not found | UsedModule=false, error set | ✅ |
| PS-005 | ParseAsJson=true | ParsedObjects populated | ✅ |
| PS-006 | ForceRefresh=true | ForceUpdate parameter added | ✅ |
| PS-007 | LogVerbose=true | Verbose parameter added | ✅ |
| PS-008 | Timeout exceeded | OperationCanceledException | ✅ |
| PS-009 | Cancellation token | Handles gracefully | ✅ |

### WslManagerService
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| WSL-001 | GetInstancesAsync (module available) | Uses module | 📋 |
| WSL-002 | GetInstancesAsync (module fails) | Fallback to inline script | 📋 |
| WSL-003 | GetInstancesAsync (with cache) | Cached result faster | 📋 |

## C# Models

### ModuleCallOptions
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| OPT-001 | Default constructor | Default values set | ✅ |
| OPT-002 | Set TimeoutSeconds | Value updated | ✅ |
| OPT-003 | Object initializer | All properties set | ✅ |

### PowerShellScriptResult (Enhanced)
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| RES-001 | ParsedObjects default | Null | ✅ |
| RES-002 | ParsedObjects with JSON array | List populated | ✅ |
| RES-003 | ParsedObjects with complex objects | Structure preserved | ✅ |
| RES-004 | UsedModule default | False | ✅ |
| RES-005 | UsedModule set to true | Value updated | ✅ |
| RES-006 | Success with UsedModule=true | Module execution confirmed | ✅ |

## Integration Tests

### WPF ↔ PowerShell Module
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| INT-001 | Call module from WPF | Successful execution | 📋 |
| INT-002 | Module not available | Fallback works | 📋 |

### Cache Mechanism
| Test ID | Scenario | Expected Result | Status |
|---------|----------|-----------------|--------|
| CACHE-INT-001 | First call (cold) | Baseline performance | 📋 |
| CACHE-INT-002 | Second call (cached) | 5x+ faster | 📋 |

## Test Status Legend
- ✅ Implemented and passing
- 🔄 In progress
- 📋 Planned
- ❌ Failed (needs attention)

## Coverage Summary

| Category | Test Count | Passing | Coverage |
|----------|------------|---------|----------|
| PowerShell Private | 17 | 17 | ~70% |
| PowerShell Public | 5 | 5 | ~40% |
| C# Services | 9 | 9 | ~75% |
| C# Models | 6 | 6 | ~90% |
| Integration | 3 | 0 | 0% (planned) |
| **Total** | **40** | **37** | **~65%** |

---

**Last Updated**: 2026-01-30  
**Next Review**: Weekly during active development
