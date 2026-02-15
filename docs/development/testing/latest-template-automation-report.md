# Built-in Template Automation Test Summary (Latest Passing Run)

## Executive Summary
- **Run ID**: `174259-9f5bd124`
- **Timestamp**: `2026-02-14T17:42:59.0997878+08:00`
- **Mode**: `AllTemplates`
- **Target distro**: `Ubuntu`
- **Isolation mode**: `PerTemplateIsolatedImport`
- **Result**: **PASS** (`15/15 passed`, `0 failed`, `0 blocked`)

This report summarizes the latest full-catalog passing validation for built-in templates. The run was executed with per-template clean-instance isolation and automatic instance cleanup.

## Scope and Method
- Scope includes all built-in templates defined in `config/templates.json`.
- Each template test starts from a freshly imported temporary WSL2 instance.
- Each temporary instance is terminated and unregistered after the template test completes.
- Capability-gated templates were included (`IncludeCapabilityGated = true`).
- Validation outcome is based on template script execution plus runtime probes defined by the automation runner.

## Environment Snapshot
- WSL available: **Yes**
- WSL version check: **Success**
- Distro list check: **Success**
- Host default distro at run time: `Ubuntu`

## Result Details by Template
| Template ID | Template Name | Status | Duration (s) |
|---|---|---:|---:|
| `dotnet-dev` | .NET Development | Pass | 57 |
| `nodejs-dev` | Node.js Development | Pass | 21 |
| `python-dev` | Python Development | Pass | 217 |
| `docker-dev` | Docker Development | Pass | 30 |
| `fullstack-dev` | Fullstack Development | Pass | 219 |
| `dotnet-multi-sdk-dev` | .NET Multi-SDK Development | Pass | 32 |
| `nodejs-multi-version-dev` | Node.js Multi-Version Development | Pass | 22 |
| `python-multi-version-dev` | Python Multi-Version Development | Pass | 166 |
| `java-jvm-dev` | Java/JVM Development | Pass | 58 |
| `rust-dev` | Rust Development | Pass | 52 |
| `go-dev` | Go Development | Pass | 21 |
| `container-runtime-dev` | Container Runtime Development | Pass | 31 |
| `kubernetes-local-dev` | Kubernetes Local Development | Pass | 27 |
| `database-local-stack` | Database Local Stack | Pass | 42 |
| `ai-ml-gpu-dev` | AI/ML GPU Development | Pass | 55 |

## Aggregated Metrics
- Total templates: **15**
- Passed: **15**
- Failed: **0**
- Blocked: **0**
- Total measured execution time (sum of template durations): **1050s** (~**17m 30s**)

## Artifacts
- Summary: `docs/development/testing/results/20260214/174259-9f5bd124/summary.md`
- Manifest: `docs/development/testing/results/20260214/174259-9f5bd124/run-manifest.json`
- Test XML: `docs/development/testing/results/20260214/174259-9f5bd124/test-results.xml`
- Per-template logs: `docs/development/testing/results/20260214/174259-9f5bd124/logs/*.json`

## Retention Statement
Historical runs in `docs/development/testing/results/20260214/` were removed. Only the latest fully passing run (`174259-9f5bd124`) is retained.