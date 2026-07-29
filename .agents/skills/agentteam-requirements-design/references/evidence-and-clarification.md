# Evidence and Clarification

## Evidence matrix

| Question | Evidence needed | Current source | Finding | Confidence | Gap or next action |
| --- | --- | --- | --- | --- | --- |
| {{QUESTION}} | {{EVIDENCE_TYPE}} | {{PATH_OR_SYSTEM}} | {{FACT}} | Confirmed / Inferred | {{NEXT_ACTION}} |

Use current source code, tests, configuration, schemas, runtime traces, accepted decisions, and primary external documentation where applicable. Mark archived or legacy material as behavior evidence only unless the target repository assigns it authority.

## Clarification rules

- Ask for a user decision only when it materially changes scope, product behavior, authority, or external mutation.
- Resolve implementation uncertainty through repository evidence before asking.
- State a bounded assumption only when it is safe, reversible, and does not broaden scope.
- Record a closure item with owner, missing evidence, impact, and smallest next action when no safe conclusion exists.
- Separate confirmed current behavior from desired behavior and from design proposals.
