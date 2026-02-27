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
The data model design highlighted several questions about "What are Minutes?" and "What happens to defered/abandoned items when a meeting is finalized?"

### What comes next
- Clean up the Docket code and write test code for it
- Start the Azure implementation for Keeper

---

## [2026-02-26] — Docket API Contract, State Machine and Workflow Testing, Start Keeper

### What happened
Clause an I have been building out test suites to validate and verify that Docket does what we expect it to do. I've learned from other projects that AI, like people, doesn't like to write validation code. There's some justification for this; I've seen functions with 80 lines of validation code that precede the three lines of code that do the work of the function. This was the time to write them; before there's a lot of code that breaks when an API has to change. And they provide examples for others who want to use Docket stand-alone.

- 41 state machine tests
-  9 workflow tests
- 13 carry-forward tests
-  2 finalization gate tests
- 46 contract tests
- 56 integration/other tests 

- 167 tests, all green.

- Keeper for Slack completed
- 4 prototype web pages for Keeper Desktop client

### Why
These test suites verify that the 38 endpoints do what we say they do, and they validate that what we said they do is the right thing to be doing. They also provide useful commentary about the design and provide examples.

The contract tests found four missing validations found that were fixed, two authorization behaviors clarified (403 vs 404) and one history endpoint semantics pinned down (ActionItems history returns a list containing the item with an empty history, not an empty list).

From Claude: "The contract tests caught real gaps — missing validation, wrong status codes, a carry-forward implementation that didn't match the intended design for human-deferred items. Those are exactly the kinds of bugs that would have been painful to diagnose from the Keeper side.
Docket is solid. The API speaks the right language, the state machine is correct, the carry-forward audit trail works as designed, and the test suite gives anyone a clear green/red signal on the full surface."

THe original plan was to implement Keeper on the Microsoft platform first. However, there are real costs to setting that up and it wasn't clear how much time it would take to get the first-article running. As a result, I shifted to the Python/Slack stack. It took less than than one hour from concept to parsing a transcript into Docket to put that version of Keeper together. But, once I had data from Keeper in Docket, the other half of the problem had to be addressed; how do I see the items that have been assigned to me. So, Clause and I got busy creating the prototype pages for the web client.

### What comes next
- Wire up the Keeper web client

---

