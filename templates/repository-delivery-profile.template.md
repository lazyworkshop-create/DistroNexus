# Repository Delivery Profile: {{REPOSITORY_NAME}}

> Codex: fill this from the target repository's current evidence. For every unknown value, write `TODO: <evidence needed>` instead of guessing.

## Authority

- Project root and primary branch: {{REPOSITORY_ROOT}}; {{PRIMARY_BRANCH}}
- Project purpose and technology baseline: {{PROJECT_PURPOSE}}; {{TECHNOLOGY_BASELINE}}
- Repository instructions: {{AGENTS_PATH}}
- Requirements: {{REQUIREMENTS_PATHS}}
- Design and decisions: {{DESIGN_AND_DECISION_PATHS}}
- Status and documentation ownership: {{STATUS_AND_DOC_OWNERSHIP}}

## Project Topology

- Production source roots: {{SOURCE_ROOTS}}
- Test and acceptance roots: {{TEST_AND_ACCEPTANCE_ROOTS}}
- Configuration/schema roots: {{CONFIG_AND_SCHEMA_ROOTS}}
- Deployment/package/release roots: {{DELIVERY_ROOTS}}
- Historical/reference roots and policy: {{ARCHIVE_REFERENCE_ROOTS_AND_POLICY}}

## Scope and permissions

- Requested capability: {{REQUEST_SCOPE}}
- Explicit exclusions: {{EXCLUDED_SCOPE}}
- Allowed mutations: {{MUTATION_PERMISSION}}
- Deployment/release permission: {{DEPLOYMENT_PERMISSION}}
- Existing changes to preserve: {{UNRELATED_CHANGES}}
- Sensitive paths and redaction policy: {{SENSITIVE_DATA_POLICY}}

## Runtime and evidence

- Main runtime surfaces: {{RUNTIME_SURFACES}}
- Data/schema evidence: {{DATA_EVIDENCE}}
- Behavior evidence: {{BEHAVIOR_EVIDENCE}}
- Archive/reference policy: {{ARCHIVE_POLICY}}

## Verification and delivery

- Targeted tests: {{TARGETED_TEST_COMMANDS}}
- Additional checks: {{ADDITIONAL_VERIFICATION}}
- Build, lint/static analysis, and package commands: {{BUILD_LINT_PACKAGE_COMMANDS}}
- Database/runtime/UI acceptance commands: {{ACCEPTANCE_COMMANDS}}
- External evidence needed: {{EXTERNAL_EVIDENCE_REQUIREMENT}}
- Commit convention: {{COMMIT_CONVENTION}}
- Release/readiness entry points and rollback procedure: {{RELEASE_ENTRY_POINTS_AND_ROLLBACK}}
- Open blockers: {{OPEN_ITEMS}}
