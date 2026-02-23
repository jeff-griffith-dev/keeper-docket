# ADR-003: Docket as a Standalone API Product

**Date:** 2026-02-22  
**Status:** Accepted  
**Author:** Jeff Griffith

---

## Context

ADR-002 established that Keeper and Docket are separate components. This ADR records the additional decision that Docket is designed and documented as a *standalone, independently distributable product* — not merely an internal implementation detail of Keeper.

The distinction matters because it changes how Docket is designed, documented, and versioned.

---

## Decision

Docket is a first-class product. It will be:

- Documented with a complete OpenAPI specification
- Versioned independently of Keeper (Docket v1 is stable before Keeper v1 ships)
- Deployable without Keeper (Docker Compose, Azure Container Apps, or bare metal)
- Usable by any HTTP client — Keeper, Copilot connectors, Power Automate, custom scripts, or humans with curl
- Licensed MIT, like the rest of the project

---

## What Docket Exposes

At minimum, Docket's API covers the following domain:

### Resources

**MeetingSeries**  
A recurring meeting context — a client engagement, a project, a team cadence. The top-level container. All Minutes belong to a MeetingSeries.

**Minutes**  
A single meeting instance. Has a date, a status (Draft / Finalized), a set of participants, and a set of Topics. Once Finalized, Minutes are immutable — this is the chain of custody guarantee.

**Topic**  
A discussion item within a meeting. Can be typed as `Info` (what was discussed or decided) or `Action` (what was committed to). Topics carry forward if unresolved — this is the "recurring topic" concept inherited from 4Minitz.

**ActionItem**  
The atomic unit of commitment. Properties: description, owner (one person), due date, status (Open / In Progress / Done / Deferred), and source (transcript reference if AI-extracted, or manual if entered by hand).

**Owner**  
A participant record. Has an identity (name, email, and optionally a Teams or Slack user ID for direct messaging). Owners are scoped to a MeetingSeries, not global — the same person can exist in multiple series without cross-contamination.

### Key API Behaviors

- `GET /series/{id}/open-items` — all unresolved ActionItems across all Minutes in a series, sorted by due date. This is Keeper's morning briefing query.
- `GET /series/{id}/overdue` — ActionItems past due date and not Done. This is Keeper's follow-up trigger.
- `POST /minutes/{id}/finalize` — locks a Minutes record. Returns 409 if already finalized.
- `GET /items/{id}/history` — full audit trail of status changes with timestamps. This is the chain of custody.

---

## The Copilot Integration Path

Docket's value as a standalone product is clearest in the Copilot integration scenario:

1. Organization uses Microsoft Copilot for meeting summaries today — no migration required
2. A lightweight Copilot connector (Power Automate flow or Azure Logic App) reads Copilot's action item output and POSTs it to `POST /docket/items`
3. Docket now holds a structured, persistent, queryable record of every commitment extracted by Copilot — with ownership, due dates, and status
4. When the organization is ready, Keeper replaces the Copilot connector as the write source — but the Docket history is preserved and continuous
5. There is no "migration event" — Keeper just starts writing to the same Docket that Copilot was writing to

This is the migration path that makes Docket adoptable without requiring organizational commitment to replace existing tooling first.

---

## Versioning Policy

Docket's API will follow semantic versioning.

- Breaking changes to the API contract require a major version bump and a deprecation notice
- Keeper will always document which minimum Docket version it requires
- The OpenAPI spec is the source of truth — not the implementation

---

## Alternatives Considered

**Docket as internal-only component**  
Rejected. It reduces the commercial and integration optionality without any compensating benefit. The additional design discipline required to make Docket a standalone product (clean API, proper versioning, deployment docs) is the same discipline that makes it a better component internally.

**Docket using an existing open standard (e.g., CalDAV VTODO)**  
Considered. Rejected because existing standards don't carry the meeting-native hierarchy (Series → Minutes → Topics → Items) or the chain-of-custody / finalization semantics that are Docket's differentiator. Standard task formats can be exported from Docket but are not the native model.

---

## Consequences

- The OpenAPI spec for Docket must be written and reviewed before any implementation begins
- Docket gets a dedicated blog post in the series focused on the data model and the API design decisions
- The spec lives at `/docs/01-spec/docket-api.yaml`
- Docket's Docker image will be published separately from Keeper's

---

## Amendments

*None yet.*
