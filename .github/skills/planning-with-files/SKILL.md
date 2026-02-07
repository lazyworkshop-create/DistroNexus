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

### 1. `task_plan.md` (总指挥)
```
# Task Plan
## Goal
[One-sentence description of the end goal]

## Phases
- [ ] **Phase 1: Research** (Current)
- [ ] **Phase 2: Specification**
- [ ] **Phase 3: Implementation**
- [ ] **Phase 4: Testing & Review**

## Current Phase Details
[What needs to happen next]
```

### 2. `findings.md` (知识库)
```
# Findings
## Tech Stack
- [Key technology decisions and reasons]

## API / Data Contracts
- [Interface definitions, endpoints, schemas]

## Decisions Log
| Decision | Reason | Date |
|----------|--------|------|
```

### 3. `progress.md` (流水账)
```
# Progress Log
## [Date/Phase]
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