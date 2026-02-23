# ADR-002: Keeper / Docket Architectural Split

**Date:** 2026-02-22  
**Status:** Accepted  
**Author:** Jeff Griffith

---

## Context

Early in the design process, the application was conceived as a single system: a meeting accountability bot that extracts action items and tracks them. The natural implementation would be one application with one database, one interface, and one deployment unit.

During design review, a question emerged: *"What does this do that Copilot doesn't? Did products like Copilot kill this?"*

That question, taken seriously, produced a more interesting answer than expected — and with it, a better architecture.

---

## The Insight

Microsoft Copilot and similar tools are excellent at producing meeting summaries. They have a hard boundary: they operate within their ecosystem, they produce output once (at meeting end), and they do not follow up. The accountability gap — WHO said they would do WHAT by WHEN, tracked over time, with reminders, across organizational boundaries — is not closed by Copilot.

But more importantly: the *record* of commitments is valuable independent of how it gets populated. If Docket is a clean API, then Copilot can write to it. Other tools can write to it. A human typing in a web form can write to it. The source of the commitment record doesn't matter — what matters is that the record is structured, queryable, auditable, and persistent.

This realization produced the split:

**Keeper** is the agent. It is opinionated about interface (Teams or Slack), about AI tooling (Azure OpenAI or OpenAI/Ollama), and about workflow (scan transcript → extract commitments → file to Docket → follow up). Keeper talks to people. Keeper is active.

**Docket** is the record. It has no opinions about where commitments came from. It exposes a REST API. It stores MeetingSeries, Minutes, Topics, ActionItems, and Owners. It can be queried by Keeper, by Copilot via a connector, by a Power Automate flow, or by a human with curl. Docket is passive. Docket remembers.

---

## Decision

Keeper and Docket are separate, independently deployable components with a defined API boundary between them.

```
[Meeting transcript]
        │
        ▼
   [ KEEPER ]  ←── You talk to Keeper
   The agent       Keeper processes, reasons, follows up
        │
        │  REST API calls
        ▼
   [ DOCKET ]  ←── Keeper writes to Docket
   The record       Anyone can read from Docket
        │           Copilot can write to Docket
        ▼           Power Automate can write to Docket
  [Persistent       A human form can write to Docket
   data store]
```

Keeper depends on Docket. Docket does not depend on Keeper.

---

## The Internal Conversation That Confirmed This

During design, the following interaction was imagined — unprompted — as a natural way to use the finished product:

> *"Keeper, what's up today?"*  
> *"Docket says you have five items. Overnight I scanned the transcripts and your email, and sent three items to Docket. Docket flagged three of them as updates to existing items that will need your review and comment today, and there are two other items from last week that Docket says need your attention."*

That interaction is only coherent if Keeper and Docket are distinct. Keeper synthesizes and speaks. Docket holds the authoritative record. Neither one does the other's job.

---

## Alternatives Considered

**Single monolithic application**  
Rejected. Couples the agent behavior to the data model, makes Docket unsellable as a standalone component, and makes it impossible to integrate with external tools like Copilot without rewriting the whole system.

**Separate applications with shared database**  
Rejected. Coupling through the database creates the same problems as a monolith without the clarity of a monolith. The API boundary is what makes the split meaningful.

**Docket as a thin wrapper around an existing task manager (Planner, Jira, etc.)**  
Considered seriously. Rejected because existing task managers do not have the meeting-native data model (MeetingSeries → Minutes → Topics → ActionItems with full chain of custody from transcript). They are task stores, not commitment records. The distinction matters for auditability.

---

## Consequences

- Docket must be fully specced and stable before Keeper development begins in earnest
- Docket gets its own versioned OpenAPI spec in `/docs/01-spec/`
- The blog series will have a dedicated post on Docket as a standalone product, separate from the Keeper agent posts
- Future commercial consideration: Docket can be licensed, sold, or deployed independently of Keeper

---

## Amendments

*None yet.*
