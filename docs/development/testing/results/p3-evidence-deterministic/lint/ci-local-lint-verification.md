# Lint CI/Local Consistency Verification

## Command
- Local command: Test-DistroNexusTemplateMetadata -ReportPath docs/development/testing/results/p3-evidence-deterministic/lint/lint-pass.json
- Strict fail sample command: Test-DistroNexusTemplateMetadata -ConfigPath docs/development/testing/results/p3-evidence-deterministic/lint/invalid-templates.json -Strict -ReportPath docs/development/testing/results/p3-evidence-deterministic/lint/lint-fail.json

## Output Contract Check
- Both outputs contain: SchemaVersion, Status, ConfigPath, StrictMode, GeneratedAt, Summary, Violations.
- Fail sample exits via strict-mode exception and still produces deterministic JSON report.

## Conclusion
- Local execution contract is deterministic and CI-compatible.
