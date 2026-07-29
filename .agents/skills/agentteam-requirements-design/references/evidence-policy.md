# Evidence Policy

Classify evidence before drawing a conclusion:

| Class | Use | Limitation |
| --- | --- | --- |
| Current implementation | Current source, tests, configuration, schemas, and verified runtime traces. | May show behavior but not desired product intent. |
| Data/schema evidence | Tables, API schemas, stored procedures, mappings, and verified structures. | Does not prove user-visible behavior alone. |
| Behavior evidence | Captured UI/API behavior, logs, workflow traces, and controlled acceptance probes. | Must identify environment and time. |
| Active design/decision/status | Accepted current technical or product direction. | Verify it is not superseded. |
| Historical/legacy material | Parity, migration, and reconstruction support. | Never use as a live contract until the conclusion is promoted to active docs. |

Create a standalone trace when evidence itself is the deliverable, sources conflict, provenance blocks design, or reviewers need a reusable source map. Keep small stable evidence inline in the owning clarification/design/contract. Stop and create a closure item when evidence conflicts, an important field/transition/ownership rule remains inferred, or active conclusions would rely only on historical material.

Promote stable conclusions into the active requirements, design, decision, plan, or status record; do not leave implementation authority only in discovery notes.
