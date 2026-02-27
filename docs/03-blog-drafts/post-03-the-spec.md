# From Dead Code to Living Spec

*Post 3 of the Keeper/Docket series — reviving dead open source with modern AI tools*

---

The archaeology was done. I had screenshots of 4Minitz in operation, a clear picture of what its data model got right, and a list of the things it got wrong or never finished. The next question was the one that actually matters: how do you turn that analysis into something you can build from?

The answer turned out to be more interesting than I expected.

## The Temptation to Just Start Building

When you have a working reference implementation — even a dead one — the instinct is to clone and modify. You know what the thing does. You can see the database schema. Why not just start there?

Two reasons.

First, the original schema carries the original assumptions. 4Minitz was built for a specific workflow on a specific framework at a specific moment in time. Its data model reflects those constraints, not the problem domain. Cloning it means inheriting the constraints along with the good ideas.

Second, and more practically: I wasn't building one thing. I was building a system that would drive two completely different implementations — one Azure-native, one portable — from a single design. You can't do that by cloning. You need a specification that's independent of any particular stack.

So before writing a line of application code, I wrote the spec.

## What the Spec Actually Is

Docket's specification lives in two places in the repo: the OpenAPI document and the Architecture Decision Records.

The OpenAPI document is the contract. It defines 38 operations across the full resource hierarchy: series, minutes, topics, action items, info items, users, labels. Every endpoint, every request body, every response shape, every error code. It was written before any implementation existed — and that ordering mattered.

The ADRs are the reasoning. Each one is short — a page, sometimes less — and follows a consistent structure: context, options considered, decision made, consequences accepted. They answer the question that code can never answer on its own: *why does it work this way instead of some other way?*

Together they form a complete picture of the system. The OpenAPI document tells you what Docket does. The ADRs tell you why it does it that way and what was deliberately left out.

## How AI Changed the Spec Process

I've written API specs before without AI assistance. The mechanical part — defining resource shapes, enumerating error codes, writing consistent naming conventions across 38 endpoints — is the kind of work that's easy to understand and tedious to do. AI handles that well.

But the more interesting contribution was in the design conversations.

When I was working through the state machine for action items — the rules governing what status transitions are legal, what's system-set versus human-set, what terminal states mean — I wasn't dictating answers. I was thinking out loud, and the AI was a thinking partner that could hold the full context of what we'd discussed, push back when something was inconsistent, and surface implications I hadn't considered.

The distinction between human-set Deferred and system-set Deferred is a good example. A human marks an item Deferred because the group decided it couldn't be resolved in this meeting but must be resolved in a future one. The system marks an item Deferred because minutes were finalized with the item still open — it carries forward automatically. Both produce the same outcome: the item appears as Open in the next meeting. But they mean different things as audit events.

I wouldn't have gotten to that distinction by reading the 4Minitz code. It came out of a design conversation where the AI kept asking "what does this mean in the record?" until we'd worked out every case.

## The Decisions That Shaped Everything

A few spec decisions are worth calling out because they cascaded through the entire design.

**Everything has an endpoint.** No action item can silently wither. Every item must reach one of three terminal states: Completed (resolved), Abandoned (explicitly closed without resolution), or Deferred (unresolved, carrying forward). This isn't a feature — it's a constraint that the API enforces. If you try to finalize minutes with unresolved items on non-recurring topics, you get a 409 with the blocking item IDs. The system won't let you lose track of something.

**The audit trail is the product.** Docket isn't primarily a task manager. It's a record that tasks were committed to, tracked, and ultimately resolved or not. The lineage chain — the ability to follow an action item from its origin meeting through every carry-forward to its current state — is what makes Docket worth using over a shared spreadsheet. The spec was designed around that capability, not bolted onto it.

**Series and Minutes are different things.** A Series is a recurring engagement — the Bridge Project, the Weekly Standup, the Quarterly Review. Minutes are a single meeting within that series. The distinction seems obvious but it has real consequences: participants belong to a Series, not to Minutes. Carry-forward logic operates on the most recent finalized Minutes within a Series. Archiving a Series cascades to abandon all open items across all its Minutes. Getting this boundary right in the spec prevented a lot of confusion in the implementation.

**Draft Minutes are a first-class state.** When Keeper processes a transcript, it creates a Series and a Draft Minutes record, then populates it with topics and action items. The group can review and correct what was captured before finalizing. Finalization is explicit and intentional — it locks the record, triggers notifications, and initiates the carry-forward logic for the next meeting. This means the API has to support the full lifecycle of a Draft: create, populate, finalize, or abandon. That's more surface area than a simpler design would require, but it's the right model for how meetings actually work.

## What Writing the Spec First Actually Bought

By the time I started writing implementation code, there were no design decisions left to make. The state machine was fully specified. The error contract was defined — which exceptions map to which HTTP status codes, what the response body looks like, which fields are present in every error response. The resource hierarchy was settled. The carry-forward rules were documented.

The implementation became a translation exercise: take the spec and express it in .NET. That's not a trivial exercise, but it's a qualitatively different kind of work than simultaneously designing and building. You're not making decisions while writing code. You're just writing code.

The test suite reflects this. The 167 tests covering the Azure implementation aren't testing that I built something — they're testing that the implementation matches the spec. State machine tests verify that every valid transition works and every invalid one is rejected. Contract tests verify that every endpoint returns the right status codes and response shapes. Workflow tests verify that the carry-forward audit trail works as documented.

When a test fails, it's unambiguous: either the test is wrong, or the implementation diverges from the spec. There's no third option where the spec itself is uncertain.

## What's In the Repo

The full specification is in the repo now:

- `/docs/01-spec/openapi.yaml` — the complete API contract, 38 operations
- `/docs/02-architecture/` — the ADR folder; currently nine records covering everything from the choice of SQLite for development to the behavior of open items on non-recurring topics at finalization

The ADRs are worth reading even if you're not building this. They're short, they document real tradeoffs, and they're honest about what was decided and what was deliberately deferred. ADR-009 — the one about what happens when you try to finalize a meeting with unresolved items — is a good example of the kind of decision that sounds simple until you work through the cases.

---

The spec was the hardest post to write because it's the least visible part of the project. Nobody sees the OpenAPI document. Nobody reads the ADRs unless they're joining the project. But everything that came after — the implementation, the tests, the agent, the UI — was built on top of it. Get the spec right and the rest of the project has a foundation. Skip it and you're making design decisions in production.

Next post covers the implementation: how the .NET stack went from spec to 38 running endpoints, what the state machine looks like in code, and where the AI tools were genuinely useful versus where they needed supervision.

---

*Next: [Post 4 — Building Docket: From Spec to 38 Running Endpoints]*
