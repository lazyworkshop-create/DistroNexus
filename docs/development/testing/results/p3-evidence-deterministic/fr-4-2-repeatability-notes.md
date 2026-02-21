# FR-4.2 Deterministic Repeat-Run Notes

## Run Configuration
- Script: `tools/collect-p2-test-evidence.ps1`
- Parameters: `-Phase P3 -DeterministicPathMode -EvidenceId p3-evidence-deterministic -UpdateChecklist:$false`

## Repeat-Run Verification
- Run #1 output root: `docs/development/testing/results/p3-evidence-deterministic/`
- Run #2 output root: `docs/development/testing/results/p3-evidence-deterministic/`
- Both runs produced the same deterministic file structure and key artifact names:
  - `automation-sample/regression-diff.json`
  - `automation-sample/summary.md`
  - `automation-sample/index.md`
  - `lint/lint-pass.json`
  - `lint/lint-fail.json`
  - `lint/ci-local-lint-verification.md`
  - `p3-evidence-bundle.json`
  - `p3-test-evidence-proof.md`
  - `acceptance-evidence-index.md`

## Conclusion
- Deterministic naming strategy is validated for repeat runs.
- Output references remain repository-relative.
