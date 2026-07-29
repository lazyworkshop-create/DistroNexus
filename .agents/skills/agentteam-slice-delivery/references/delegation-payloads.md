# Delegation Payloads

## Implementation payload

Give `agentteam-slice-implementer` this complete payload:

```text
Mode: parent-managed implementation
Working directory: <absolute path>
Slice plan: <path>, slice id: <SNN>
Base commit: <sha>
Authoritative sources: <requirements, design, decisions, status, evidence paths>
Allowed paths: <list>
Excluded paths: <list>
Acceptance criteria: <copied criteria>
Required verification: <exact commands and external evidence requirement>
Permissions: <allowed mutations>; no stage/commit/deploy/publish/external writes
Task: implement only this slice vertically, then return the structured implementation report.
```

Reject an implementation report that does not include `slice_id`, `base_commit`, `changed_files`, requirements/design coverage, documentation decisions, production behavior, meaningful tests, exact command outcomes, verification limits, and blockers/remaining items.

## Acceptance-review payload

Give `agentteam-contract-reviewer` this complete payload:

```text
Mode: slice_completion_acceptance
Working directory: <absolute path>
Slice plan: <path>, slice id: <SNN>
Base commit and diff boundary: <sha>..HEAD plus uncommitted changes
Authoritative sources: <requirements, design, decisions, status, evidence paths>
Allowed/excluded paths: <lists>
Acceptance criteria and verification commands: <copied values>
Implementation report: <verbatim report>
Task: independently inspect the complete current diff and return exactly ACCEPTED, REWORK_REQUIRED, or BLOCKED.
```

Require findings first, then base/diff boundary, verification performed and limits, an acceptance-criteria evidence matrix, and the smallest next action for non-accepted verdicts.
