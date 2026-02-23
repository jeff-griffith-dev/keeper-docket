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
