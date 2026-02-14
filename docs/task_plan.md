# Website Update Plan for v2.0.1

Last Updated: 2026-02-14
Owner: Copilot (GPT-5.3-Codex)

## Objective
Align the public documentation website with the v2.0.1 product baseline (.NET 10 + WPF rewrite, PowerShell module platform, template system, and updated distribution artifacts).

## Scope
- Docusaurus website content under `website/`
- Website requirement specification under `docs/specs/`
- Planning and analysis artifacts under `docs/`

## Phases

### Phase 1 — Repository Scan and Gap Analysis
Status: Completed
- Gather authoritative v2.0.1 release facts.
- Audit current website pages, blog, and site configuration.
- Identify mismatches, stale v1 references, and missing v2 pages.

### Phase 2 — Requirements Definition
Status: Completed
- Define mandatory updates for homepage, docs, blog, and links.
- Define bilingual (English + zh-Hans) synchronization requirements.
- Define acceptance criteria and out-of-scope constraints.

### Phase 3 — Handoff
Status: Completed
- Deliver requirement document path and summary.
- Propose implementation sequencing for next execution task.

## Deliverables
1. `docs/findings.md`
2. `docs/progress.md`
3. `docs/specs/website-v2-0-1-update-requirements.md`
4. `docs/development/website-v2-0-1-implementation-checklist.md`
5. `docs/development/website-v2-0-1-acceptance-checklist.md`

## Risks
- Existing website content still reflects v1 architecture and packaging.
- Potential inconsistency between English and Chinese pages.
- Repository links in website config may target incorrect owner namespace.
