name: planning-with-files
description: >
  Persistent file-based planning for complex multi-step tasks.
  Creates and maintains task_plan.md, findings.md, and progress.md
  in the project root directory.
  USE FOR: tasks requiring 3+ steps, research, building projects,
  multi-file refactoring, or any work spanning multiple conversations.
  DO NOT USE FOR: simple questions, single-file edits, quick lookups,
  or tasks completable in one step.
---

# Planning with Files

## Core Principle
Context Window = RAM (volatile, limited)
Filesystem = Disk (persistent, unlimited)
→ Write anything important to disk.

## When Starting a Complex Task
Create these 3 files in the **project root directory**:

### 1. `task_plan.md` (Master Plan)
```
# Task Plan
## Metadata
- Created At: [YYYY-MM-DD HH:mm:ss ±HH:mm]
- Last Updated At: [YYYY-MM-DD HH:mm:ss ±HH:mm]

## Goal
[One-sentence description of the end goal]

## Phases
- [ ] **Phase 1: Research**
  - Started At: [YYYY-MM-DD HH:mm:ss ±HH:mm]
  - Completed At: [YYYY-MM-DD HH:mm:ss ±HH:mm or N/A]
- [ ] **Phase 2: Specification**
  - Started At: [YYYY-MM-DD HH:mm:ss ±HH:mm]
  - Completed At: [YYYY-MM-DD HH:mm:ss ±HH:mm or N/A]
- [ ] **Phase 3: Implementation**
  - Started At: [YYYY-MM-DD HH:mm:ss ±HH:mm]
  - Completed At: [YYYY-MM-DD HH:mm:ss ±HH:mm or N/A]
- [ ] **Phase 4: Testing & Review**
  - Started At: [YYYY-MM-DD HH:mm:ss ±HH:mm]
  - Completed At: [YYYY-MM-DD HH:mm:ss ±HH:mm or N/A]

## Current Phase Details
- Updated At: [YYYY-MM-DD HH:mm:ss ±HH:mm]
- [What needs to happen next]
```

### 2. `findings.md` (Knowledge Base)
```
# Findings
## Metadata
- Created At: [YYYY-MM-DD HH:mm:ss ±HH:mm]
- Last Updated At: [YYYY-MM-DD HH:mm:ss ±HH:mm]

## Tech Stack
- [Key technology decisions and reasons]

## API / Data Contracts
- [Interface definitions, endpoints, schemas]

## Research Notes
- Time: [YYYY-MM-DD HH:mm:ss ±HH:mm]
  - Source: [file/log/command]
  - Observation: [what was found]

## Decisions Log
| Time | Decision | Reason |
|------|----------|--------|
```

### 3. `progress.md` (Progress Journal)
```
# Progress Log
## Entry [YYYY-MM-DD HH:mm:ss ±HH:mm] - [Phase]
- Start Time: [YYYY-MM-DD HH:mm:ss ±HH:mm]
- End Time: [YYYY-MM-DD HH:mm:ss ±HH:mm]
- Duration: [e.g., 00:18:42]
- Action: [what was done]
- Result: [outcome]
- Errors: [any issues encountered]
- Fix: [how they were resolved]
```

## Rules
1. **Read plan first** — Before any major action, read task_plan.md
2. **2-Search Rule** — After every 2 search or file-read operations,
   save important findings to findings.md before continuing
3. **Log ALL errors** — Track failed attempts to avoid repeating them
4. **Update status** — After completing a phase, check off the box
   in task_plan.md and note the next step
5. **Detailed time required** — Every update in all three files must include
  full timestamp(s): `YYYY-MM-DD HH:mm:ss ±HH:mm`
6. **Time consistency** — `Last Updated At` must be refreshed on each write;
  `End Time` must be >= `Start Time`; durations should be explicitly recorded