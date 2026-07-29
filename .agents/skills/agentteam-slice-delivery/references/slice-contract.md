# Slice Contract

Every plan contains a `Sources` section, a dependency order, and one complete block per slice:

```markdown
## Slice S01: <observable capability outcome>

### Status

Planned | In Progress | Blocked | Accepted | Committed

### Objective

State one user-visible, operator-visible, or contract-visible outcome. Do not name a technical layer alone.

### Sources

List exact requirement ids, design sections, decisions, and evidence paths.

### Dependencies

List prerequisite slice ids, or `None`.

### Allowed Paths

List only files/directories this slice may edit.

### Excluded Paths

List hard exclusions, including unrelated dirty files and unapproved operational surfaces.

### Contract and Documentation

State owned document changes and exact contract decisions the slice implements.

### Implementation Scope

State the production behavior, boundaries, error/authorization/state rules, and explicitly excluded behavior.

### Test Scope

State meaningful success, negative, boundary, and integration/runtime cases needed for this risk.

### Acceptance Criteria

- Use observable, binary criteria.
- Include failure/security criteria where relevant.

### Verification Commands

List exact fresh commands and any required real-environment evidence.

### Commit Boundary

State the files and outcome that make one cohesive commit.

### Out of Scope

State work explicitly deferred to a later slice or external owner.
```

Use stable ids (`S01`, `S02`). A slice may move `Planned -> In Progress -> Accepted -> Committed`, or `Planned/In Progress -> Blocked`. `Accepted` is a review verdict; `Committed` requires root verification and a real commit id. Do not mark a slice complete merely because code exists.
