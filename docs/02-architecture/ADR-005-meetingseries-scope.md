# ADR-005: MeetingSeries Scope and Engagement-Level Grouping

**Status:** Accepted  
**Date:** 2026-02-24  
**Author:** Jeff Griffith  

---

## Context

During domain model review, a structural question emerged about the mapping between
real-world meeting organization and Docket's `MeetingSeries` entity.

The initial spec comment — "A Professional Services engagement maps to one
MeetingSeries" — turned out to be wrong. A Professional Services engagement is
typically a *cluster* of meeting series running in parallel:

- Monthly steering committee with executive stakeholders
- Weekly status meeting with the client project manager
- Daily or weekly team progress standups
- Ad-hoc meetings for specific business requirements
- Scheduled sessions with integration or dependency teams

Each of these has its own rhythm, its own participant list, and its own minutes
chain. They are not the same meeting — they are genuinely independent tracking
units that happen to serve a common engagement.

The problem this creates: a commitment can be effectively made in one series
(e.g., the PM-level steering meeting where decisions are made) but land on a person
who only attends a different series (e.g., the technical team standup). The
organization behaves as though the commitment was communicated, but the affected
person finds out laterally or after the fact. This is a known failure mode in
Professional Services delivery.

Docket v1 cannot solve this problem directly. The question is whether the v1
design should preserve a clean path to solving it later.

---

## Decision

**A `MeetingSeries` maps to a single calendar series** — the unit a user would
attach to a recurring event in Outlook or Google Calendar. It is an independent
tracking unit with its own participant list, its own Minutes chain, and its own
scope. It has no knowledge of sibling series.

**Engagement-level grouping is deferred to v2.** The coordination problem — "these
five series are all part of the same client engagement" — is real, but solving it
requires either a human actively maintaining cross-series links or an AI with
visibility across multiple series simultaneously. That is Keeper's job, not a v1
Docket schema concern.

**Two forward-compatibility hooks are added to `MeetingSeries` at no meaningful cost:**

1. `externalCalendarId` (VARCHAR, nullable) — stores the Outlook or Google Calendar
   series ID. This gives Keeper a machine-readable signal to propose engagement
   groupings: "these three series share an organizer and overlapping participants —
   are they the same engagement?"

2. `project` (VARCHAR, nullable, already in model) — a plain string tag in v1.
   When an `Engagement` entity is introduced in v2, this field becomes a migration
   path: existing series with matching `project` strings can be grouped under the
   new entity with a data migration, not a schema redesign.

**The v2 path is explicitly defined and reserved:** When engagement-level grouping
is introduced, it will be implemented as a new `Engagement` entity above
`MeetingSeries`. The migration will add `MeetingSeries.engagementId` as a nullable
foreign key. Existing series without an engagement assignment remain valid —
standalone series are a legitimate use case, not a degenerate one.

---

## Rationale

### Why not add the Engagement entity now?

The engagement-level coordination problem requires Keeper to be meaningful — a
human cannot practically maintain cross-series links at the volume and fidelity
needed to make the feature useful. Building the `Engagement` entity before Keeper
exists would produce a feature with no good way to populate it. The data model
should follow the capability, not precede it.

### Why not use a self-referencing parentSeriesId on MeetingSeries?

A self-reference would imply a tree structure, which is the wrong shape. An
engagement is a flat group of peer series, not a hierarchy. A tree also makes
queries unnecessarily complex. The `Engagement` entity (flat grouping table with
a junction) is the right shape when we get there.

### Why keep the project string tag at all?

It provides immediate v1 value for filtering and display without introducing
relational complexity. Users naturally think in project terms ("this is the
Contoso work") and the tag lets them express that. The migration path to a proper
foreign key is straightforward because the string values are user-defined and
consistent within a team.

### Why add externalCalendarId now?

It costs one nullable column. The value is asymmetric: without it, Keeper has
no programmatic hook to propose engagement groupings based on calendar data.
With it, the grouping problem becomes tractable without requiring users to
manually declare relationships. The column is nullable and unused in v1 — it
imposes no behavioral obligations on the v1 implementation.

---

## Consequences

**Accepted limitations in v1:**
- A commitment made in one series that affects a participant in another series
  cannot be automatically linked. Cross-series coordination requires manual
  note-taking or Keeper's future engagement-awareness feature.
- The `GET /users/{id}/open-items` endpoint shows a user's complete open item
  load across all series, but has no engagement-level grouping or filtering.
  This is the best available cross-series view in v1.

**Reserved for v2:**
- `Engagement` entity with name, description, and status
- `MeetingSeries.engagementId` nullable FK
- `GET /engagements/{id}/open-items` — cross-series open items for an engagement
- `GET /engagements/{id}/series` — all series belonging to an engagement
- Keeper feature: propose engagement groupings based on `externalCalendarId`,
  shared participants, and organizer identity

**Schema change required in v1 (small):**
- Add `externalCalendarId VARCHAR(500) NULLABLE` to `MeetingSeries`
- This is the only change to the domain model resulting from this ADR

---

## Alternatives Considered

**Add Engagement entity in v1.**  
Rejected. No good population mechanism exists until Keeper's cross-series awareness
is built. A schema feature with no UX path to fill it creates confusion and dead
weight.

**Self-referencing parentSeriesId on MeetingSeries.**  
Rejected. Wrong shape (tree vs. flat group). Adds query complexity without
adding modeling accuracy.

**Cross-series ActionItem references (lightweight linking).**  
A moderator could reference a `sourceMinutesId` from a different series when
creating an ActionItem, providing ad-hoc cross-linking without a formal hierarchy.
Not rejected outright — this could be a useful escape hatch in v1 alongside this
decision. Deferred pending feedback from actual usage patterns.

---

## References

- `docs/01-spec/docket-domain-model.md` — domain model this ADR amends
- `docs/00-archaeology/README.md` — 4Minitz had no engagement concept; this
  problem was not in scope for it
- ADR-002 — Keeper/Docket architectural split; Keeper is the intended solver
  for cross-series coordination
