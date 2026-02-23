# ADR-001: Two-Stack Implementation

**Date:** 2026-02-22  
**Status:** Accepted  
**Author:** Jeff Griffith

---

## Context

This project demonstrates the revival and modernization of a dead open source application (4Minitz) using AI-assisted development tools. A single implementation would prove the concept works on one platform. It would not demonstrate the more important claim: that the constraint is no longer implementation speed or platform knowledge — it's the quality of the thinking and the spec.

Two implementations from the same spec, built in parallel, prove that claim.

There is also a practical audience consideration. The primary author works primarily in Microsoft/Azure environments. A demonstration that only produces an Azure implementation tells one story to one audience. A demonstration that produces *both* an Azure-native implementation and a platform-agnostic implementation — from the same domain model and spec — tells a different and more compelling story: the tools generalize, the thinking transfers, and the bottleneck has moved.

---

## Decision

Implement Keeper and Docket twice, from the same domain model and feature spec:

**Stack 1 — Azure-Native**  
Targeted at organizations already in the Microsoft 365 ecosystem. Designed to feel native to Teams, integrate with Azure AD/Entra ID, and deploy naturally to Azure infrastructure. This stack demonstrates depth on the Microsoft platform and is the implementation most likely to land in enterprise Professional Services contexts.

**Stack 2 — Portable**  
Targeted at organizations that need platform independence: mixed environments, clients on Slack, contractors outside M365, or teams that want to self-host. This stack demonstrates that Docket is genuinely platform-agnostic and that the same agent concept works outside the Azure ecosystem.

| Concern | Azure Stack | Portable Stack |
|---|---|---|
| Language | C# / .NET 9 | Python 3.12 |
| Bot interface | Azure Bot Framework → Teams | Slack Bolt SDK |
| AI extraction | Azure OpenAI + Semantic Kernel | OpenAI API / Ollama (swappable) |
| Database | Azure SQL + Entity Framework Core | PostgreSQL |
| Scheduling/reminders | Azure Durable Functions | Celery + Redis |
| Deployment | Azure Container Apps | Docker Compose |
| Auth | Azure AD / Entra ID | OAuth2 via Slack |

---

## What Both Stacks Share

- The Docket domain model: `MeetingSeries`, `Minutes`, `Topic`, `ActionItem`, `Owner`
- The Docket REST API contract (OpenAPI spec)
- The feature spec derived from 4Minitz archaeology
- The AI extraction prompt design (who committed to what, by when)
- The follow-up/reminder logic (expressed differently per stack, same behavior)

The shared elements live in `/src/core/` and `/docs/01-spec/`. Neither stack implementation begins until the core model is stable.

---

## Alternatives Considered

**Single implementation — Azure only**  
Rejected. Demonstrates platform depth but not tool generalization. Doesn't prove the point about where the bottleneck has moved.

**Single implementation — portable only**  
Rejected. Underserves the primary author's demonstrated platform and the most likely deployment target for Professional Services clients.

**Three or more stacks**  
Rejected at this time. Two is enough to prove the claim without diluting the implementation quality of either.

---

## Consequences

- The domain model and API spec must be finished before either stack implementation begins — there can be no drift between the two
- Blog posts covering implementation must be written per-stack, not interleaved, to keep each narrative coherent
- Any change to the core domain model after implementation begins must be applied to both stacks and recorded here as an amendment

---

## Amendments

*None yet.*
