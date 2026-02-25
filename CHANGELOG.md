# Changelog

All meaningful changes to this project are documented here.  
Format: `## [date] — description` followed by what was done, why, and what comes next.

The philosophy: this log reflects what was *actually done*, not what was planned.  
If something isn't here, it doesn't exist yet.

---

## [2026-02-22] — Project Established

### What happened
- Repository created at `github.com/jeff-griffith-dev/keeper-docket`
- Full folder structure scaffolded
- README written with honest current-state status table
- CHANGELOG initialized (this entry)
- Architecture Decision Records created for the two foundational decisions:
  - [ADR-001: Two-Stack Implementation](docs/02-architecture/ADR-001-two-stack.md)
  - [ADR-002: Keeper/Docket Split](docs/02-architecture/ADR-002-keeper-docket-split.md)
  - [ADR-003: Docket as Standalone API](docs/02-architecture/ADR-003-docket-standalone-api.md)
- 4Minitz archaeology notes initialized

### Why
The project does not begin when the first line of code is written.  
It begins when the reasoning is recorded. These files are the first evidence.

### What comes next
- Complete the 4Minitz archaeology: clone, run, diagram the data model
- Write the feature spec derived from what 4Minitz got right
- Draft the Docket domain model: MeetingSeries / Minutes / Topics / ActionItems / Owners
- Publish Post 1 on the blog — motivation, plan, and first evidence

---

## [2026-02-23] — Archaeology and Start Docket Design

### What happened
- 4Minitz repository cloned and user interface reviewed
- Feature spec completed
- Complete domain model design

### Why
There were several important questions about what happens when a meeting is over that needed to be answered.  

### What comes next
- Document the archaeology results
- Draft the Docket domain model: MeetingSeries / Minutes / Topics / ActionItems / Owners
- Publish Post 1 on the blog — motivation, plan, and first evidence

---

## [2026-02-24] — Docket Implementation and Testing

### What happened
- Docket data model and APIs completed
- Catchup the documentation
  - [00-archaeology/README.md](docs/00-archaeology/README.md)
- Architecture Decision Records created after considering changes to the Azure platform:
  - [ADR-004: AI Orchestration Layer](docs/02-architecture/ADR-004-ai-orchestration.md)
- Architecture Decision Records created after considering the question "Is an Engagement one meeting or many":
  - [ADR-005: MeetingSeries Scope and Engagement-Level Grouping](docs/02-architecture/ADR-005-meetingseries-scope.md)
- Published Post 1 on the blog — motivation, plan, and first evidence

### Why
THe data model design highlighted several questions about "What are Minutes?" and "What happens to defered/abandoned items when a meeting is finalized?"

### What comes next
- Clean up the Docket code and write test code for it
- Start the Azure implementation for Keeper

---
