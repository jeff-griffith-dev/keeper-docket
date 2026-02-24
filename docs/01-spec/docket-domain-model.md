# Docket Domain Model

**Version:** 0.1.0  
**Status:** Draft  
**Last Updated:** 2026-02-24  
**Source of Truth:** This document. The OpenAPI spec is derived from it, not the reverse.

---

## Purpose

This document defines the entities, relationships, constraints, and state machines
that govern the Docket data model. Both the Azure stack (.NET/Azure SQL) and the
Portable stack (Python/PostgreSQL) implement this model. Deviations between stacks
are bugs, not features.

Every decision here traces to either observed 4Minitz behavior (documented in
`/docs/00-archaeology/README.md`) or an explicit design choice recorded below.
Nothing is arbitrary.

---

## Entity Overview

```
User
└── SeriesParticipant ──────────────────────────┐
                                                 │
MeetingSeries (status: active | archived)        │
├── SeriesParticipant[] (role: moderator |        │
│                        invited | informed) ────┘
└── Minutes (status: draft | finalized)
    ├── previousMinutesId → Minutes (linked list)
    └── Topic (type: recurring | adhoc)
        ├── InfoItem[]
        └── ActionItem[]
            ├── responsibleId → User
            ├── ActionItemNote[]
            └── ActionItemLabel[] → Label
```

---

## Entities

---

### User

The identity record for anyone who interacts with Docket. In the initial version,
Docket manages its own user table. Federation (LDAP, Azure AD, OAuth) is a
future concern but the schema must not preclude it.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `email` | VARCHAR(255) | UNIQUE NOT NULL | Primary identifier for notifications |
| `displayName` | VARCHAR(255) | NOT NULL | Shown in UI and emails |
| `externalId` | VARCHAR(255) | NULLABLE, UNIQUE | Reserved for future federation |
| `createdAt` | TIMESTAMP | NOT NULL | |
| `updatedAt` | TIMESTAMP | NOT NULL | |

**Design note:** No `role` column at the User level. Authorization is contextual —
a user's permissions are determined by their `SeriesParticipant` role within a given
MeetingSeries, not by a global role. A moderator in one series is just an invited
participant in another.

---

### MeetingSeries

The top-level container. Represents an ongoing engagement, project, or recurring
meeting program. A Professional Services engagement maps to one MeetingSeries.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `name` | VARCHAR(255) | NOT NULL | e.g. "UX Team Review" |
| `project` | VARCHAR(255) | NULLABLE | Grouping label, not a foreign key |
| `status` | ENUM | NOT NULL, DEFAULT `active` | `active` or `archived` |
| `createdBy` | UUID | FK → User NOT NULL | Series moderator (initial) |
| `createdAt` | TIMESTAMP | NOT NULL | |
| `updatedAt` | TIMESTAMP | NOT NULL | |

**Status state machine:**

```
active ──► archived
```

Transition to `archived` is one-way. An archived series is read-only: no new Minutes
can be created, no existing records can be edited. The series and all its history
remain queryable indefinitely.

**Design note:** `project` is a plain string, not a foreign key to a Project entity.
This is intentional. Introducing a Project entity adds join complexity without
providing value at this stage. If Docket ever needs project-level aggregation across
series, a Project entity can be introduced then. For now, project is a tag.

**4Minitz gap addressed:** 4Minitz had no archive state. Docket adds it explicitly
so completed engagements can be retired without deletion.

---

### SeriesParticipant

The join entity between User and MeetingSeries, carrying the participant's role.
This is not a simple many-to-many — the role column carries behavioral constraints
that affect the entire system.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `seriesId` | UUID | FK → MeetingSeries NOT NULL | |
| `userId` | UUID | FK → User NOT NULL | |
| `role` | ENUM | NOT NULL | `moderator`, `invited`, `informed` |
| `addedAt` | TIMESTAMP | NOT NULL | |

**Unique constraint:** `(seriesId, userId)` — a user can hold only one role per series.

**Role semantics:**

| Role | Can create Minutes | Can add Topics/Items | Can own ActionItems | Receives emails |
|---|---|---|---|---|
| `moderator` | Yes | Yes | Yes | Yes |
| `invited` | No | Yes | Yes | Yes |
| `informed` | No | No | No | Yes (minutes only) |

**Constraint:** There must be exactly one `moderator` per MeetingSeries at all times.
Moderator transfer is allowed but the series cannot exist without one.

**ActionItem owner constraint:** `ActionItem.responsibleId` must reference a User
whose `SeriesParticipant.role` for the parent series is `moderator` or `invited`.
An `informed` participant cannot own an action item. This is enforced at the
application layer, not via a database constraint, because the relationship path
is too indirect for a simple FK check.

---

### Minutes

One instance of a meeting within a MeetingSeries. Corresponds to a single meeting
on a single date.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `seriesId` | UUID | FK → MeetingSeries NOT NULL | |
| `previousMinutesId` | UUID | FK → Minutes NULLABLE | Explicit linked list |
| `scheduledFor` | TIMESTAMP | NOT NULL | The meeting date/time |
| `status` | ENUM | NOT NULL, DEFAULT `draft` | `draft` or `finalized` |
| `version` | INTEGER | NOT NULL, DEFAULT 0 | Incremented on finalization |
| `finalizedAt` | TIMESTAMP | NULLABLE | Set on finalization |
| `finalizedBy` | UUID | FK → User NULLABLE | |
| `globalNote` | TEXT | NULLABLE | Pinnable note; propagates to next Minutes if pinned |
| `globalNotePinned` | BOOLEAN | NOT NULL, DEFAULT false | |
| `createdAt` | TIMESTAMP | NOT NULL | |
| `updatedAt` | TIMESTAMP | NOT NULL | |

**Status state machine:**

```
draft ──► finalized
```

Finalization is irreversible. A finalized Minutes record is immutable — no edits
to any child Topic, InfoItem, or ActionItem are permitted after finalization.
Immutability is enforced at the application layer.

**Linked list:** `previousMinutesId` is a self-referencing foreign key forming an
explicit chain. The chain is traversed (not date-filtered) when returning history.
There is no guarantee that Minutes dates are contiguous — meetings are cancelled,
rescheduled, skipped. The explicit link is the authoritative chain of custody.

**Version:** Starts at 0 (draft). Set to 1 on first finalization. The finalization
dialog in 4Minitz showed `V1` — this is that field. Reserved for potential
re-finalization workflows in future versions.

**globalNote / globalNotePinned:** A free-text note field at the meeting level.
If pinned, the text is copied to the next Minutes' `globalNote` on creation.
Unpinned notes stay with the Minutes they were written on.

**Absent participants:** Minutes does not have an `absentees` column. Absence is
derived: a User who is an `invited` or `moderator` SeriesParticipant but not listed
in a `MinutesAttendee` record for this Minutes instance is considered absent.

---

### MinutesAttendee

Records who actually attended a specific meeting. Drives the Absent field in the
finalized minutes email.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `minutesId` | UUID | FK → Minutes NOT NULL | |
| `userId` | UUID | FK → User NOT NULL | |

**Unique constraint:** `(minutesId, userId)`

**Derivation:** Absent = SeriesParticipants (moderator + invited) minus MinutesAttendees.
Informed participants are never listed as absent — they are never expected to attend.

---

### Topic

An agenda item within a Minutes instance. Topics are the unit of carry-forward.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `minutesId` | UUID | FK → Minutes NOT NULL | |
| `title` | VARCHAR(255) | NOT NULL | |
| `type` | ENUM | NOT NULL, DEFAULT `adhoc` | `recurring` or `adhoc` |
| `isOpen` | BOOLEAN | NOT NULL, DEFAULT true | false = resolved |
| `isSkipped` | BOOLEAN | NOT NULL, DEFAULT false | true = carried without discussion |
| `sortOrder` | INTEGER | NOT NULL | Display ordering within Minutes |
| `responsibleId` | UUID | FK → User NULLABLE | Topic-level owner (not item-level) |
| `createdAt` | TIMESTAMP | NOT NULL | |
| `updatedAt` | TIMESTAMP | NOT NULL | |

**Recurring vs. adhoc:** A `recurring` topic carries forward to the next Minutes
automatically when that Minutes is created. An `adhoc` topic exists only in the
Minutes it was created on.

**Carry-forward mechanism:** When new Minutes are created for a series, all
`recurring` Topics from the most recent finalized Minutes where `isOpen = true` are
referenced (not copied) in the new Minutes. This is implemented as a new Topic record
with a `sourceTopicId` foreign key pointing back to the originating topic — preserving
the chain while allowing the new instance to accumulate its own InfoItems and
ActionItems.

**isOpen / isSkipped:** These are not mutually exclusive states. A topic can be:
- `isOpen: true, isSkipped: false` — open, under active discussion
- `isOpen: true, isSkipped: true` — explicitly skipped this meeting, carries forward
- `isOpen: false, isSkipped: false` — resolved and closed
- `isOpen: false, isSkipped: true` — not a valid state (skipped implies open)

Finalization does not require all topics to be closed. A minutes with open recurring
topics can be finalized — those topics will carry to the next meeting.

---

### InfoItem

A note, status update, or discussion record within a Topic. Informational — not
a commitment.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `topicId` | UUID | FK → Topic NOT NULL | |
| `text` | TEXT | NOT NULL | |
| `pinnedDate` | DATE | NULLABLE | If set, appears as a dated bullet in output |
| `createdBy` | UUID | FK → User NOT NULL | |
| `createdAt` | TIMESTAMP | NOT NULL | |
| `updatedAt` | TIMESTAMP | NOT NULL | |

**Design note:** InfoItems are the "what we discussed" record. They do not carry
forward independently — they belong to the Topic instance they were created on.
When a recurring Topic carries forward, its InfoItems stay with the original Minutes.
New discussion goes into new InfoItems on the new Topic instance.

---

### ActionItem

A commitment: something someone agreed to do, by a specific date. The core unit
of value in Docket.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `topicId` | UUID | FK → Topic NOT NULL | |
| `title` | VARCHAR(500) | NOT NULL | The commitment, stated plainly |
| `responsibleId` | UUID | FK → User NOT NULL | Single owner |
| `dueDate` | DATE | NULLABLE | Target completion date |
| `priority` | INTEGER | NOT NULL, DEFAULT 3 | 1 (highest) to 5 (lowest) |
| `status` | ENUM | NOT NULL, DEFAULT `open` | `open`, `done`, `deferred` |
| `isRecurring` | BOOLEAN | NOT NULL, DEFAULT false | Carries forward if open at finalization |
| `sourceActionItemId` | UUID | FK → ActionItem NULLABLE | Carry-forward provenance |
| `assignedInAbsentia` | BOOLEAN | NOT NULL, DEFAULT false | Owner was not present |
| `createdBy` | UUID | FK → User NOT NULL | Who created the record |
| `createdAt` | TIMESTAMP | NOT NULL | |
| `updatedAt` | TIMESTAMP | NOT NULL | |

**Status state machine:**

```
open ──► done
open ──► deferred
deferred ──► open
```

`done` is terminal. A completed action item cannot be reopened — if the work needs
to be redone, a new ActionItem is created. `deferred` means intentionally postponed;
it can return to `open`.

**Priority:** 1-5 integer, displayed as "1 - Critical", "2 - High", "3 - Medium",
"4 - Low", "5 - Minimal". Default is 3 (Medium), matching 4Minitz behavior.

**assignedInAbsentia:** Set to `true` when the `responsibleId` user was not present
(i.e., not in `MinutesAttendee` for the parent Minutes). This flag is surfaced in
the action items email and in Keeper's follow-up messaging. It does not change
behavior — assignment in absentia is explicitly permitted — but it provides context
for the owner who receives unexpected notification.

**Carry-forward:** When new Minutes are created and a recurring Topic is carried
forward, any `open` ActionItems on that Topic are also carried forward. The carried
item is a new record with `sourceActionItemId` pointing to the item it came from.
The original item's `status` is set to `deferred`. This creates an auditable chain:
you can always reconstruct the full history of a commitment across meetings.

**Keeper implication:** The extraction agent must not filter ActionItems based on
attendee presence. "Jeff will handle that" is a valid commitment regardless of
whether Jeff was in the meeting.

---

### ActionItemNote

A timestamped log entry on an ActionItem. Notes accumulate chronologically and
are never deleted or edited — append-only.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `actionItemId` | UUID | FK → ActionItem NOT NULL | |
| `text` | TEXT | NOT NULL | |
| `authorId` | UUID | FK → User NOT NULL | |
| `createdAt` | TIMESTAMP | NOT NULL | Immutable — the timestamp is the record |

**Design note:** No `updatedAt`. Notes are append-only by design. The timestamp
is part of the meaning — "on this date, this was said about this commitment."
Editing a note would corrupt the audit trail.

The finalization email renders the complete note log at the moment of finalization.
Keeper appends notes automatically (e.g., "2026-02-24: Extracted from transcript —
Jeff confirmed he would handle the API migration by end of sprint").

---

### Label

A tag that can be applied to ActionItems (and potentially Topics). Two categories:
action labels (classify the type of commitment) and status labels (traffic-light
health indicator).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | UUID | PK | |
| `name` | VARCHAR(100) | UNIQUE NOT NULL | e.g. `Decision`, `Status:GREEN` |
| `category` | ENUM | NOT NULL | `action` or `status` |
| `color` | VARCHAR(7) | NULLABLE | Hex color for UI rendering |
| `isSystem` | BOOLEAN | NOT NULL, DEFAULT false | System labels cannot be deleted |
| `createdAt` | TIMESTAMP | NOT NULL | |

**System labels (seeded on install):**

| Name | Category | Notes |
|---|---|---|
| `Decision` | action | A commitment that is also a recorded decision |
| `Proposal` | action | Under consideration, not yet decided |
| `New` | action | First appearance in the record |
| `Status:GREEN` | status | On track |
| `Status:YELLOW` | status | At risk |
| `Status:RED` | status | Blocked or overdue |

Custom labels can be created by moderators. System labels cannot be deleted.

---

### ActionItemLabel

Junction table. Many-to-many between ActionItem and Label.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `actionItemId` | UUID | FK → ActionItem NOT NULL | |
| `labelId` | UUID | FK → Label NOT NULL | |

**Primary key:** `(actionItemId, labelId)`

**Design note:** This is the junction table that replaces the enum column that
4Minitz's behavior implied was insufficient. A single ActionItem can carry
`Decision` + `Proposal` + `New` simultaneously — observed in the archaeology.

---

### TopicLabel

Junction table. Many-to-many between Topic and Label. Topics can carry status
labels independently of their ActionItems.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `topicId` | UUID | FK → Topic NOT NULL | |
| `labelId` | UUID | FK → Label NOT NULL | |

**Primary key:** `(topicId, labelId)`

---

## State Machines Summary

```
MeetingSeries.status:   active ──────────────────► archived

Minutes.status:         draft ───────────────────► finalized

ActionItem.status:      open ────────────────────► done
                        open ◄───────────────────► deferred
```

No other state transitions exist. Any API operation that would violate these
transitions must be rejected with HTTP 422.

---

## Key Constraints Summary

1. A MeetingSeries must have exactly one `moderator` SeriesParticipant at all times.
2. `ActionItem.responsibleId` must be a `moderator` or `invited` participant in the parent series.
3. Finalized Minutes are immutable — no writes to Minutes, Topic, InfoItem, ActionItem, or ActionItemNote beneath a finalized Minutes.
4. Archived MeetingSeries are read-only — no new Minutes, no edits to any existing record.
5. ActionItemNotes are append-only — no updates or deletes.
6. Labels marked `isSystem = true` cannot be deleted.
7. `Minutes.previousMinutesId` must reference a Minutes in the same MeetingSeries.
8. A User can hold only one role per MeetingSeries (`SeriesParticipant` unique constraint on `(seriesId, userId)`).

---

## First-Class API Operations (Derived from Domain)

These are not obvious CRUD operations — they are behaviors the domain model
specifically exists to support.

| Operation | Method | Path | Notes |
|---|---|---|---|
| Finalize minutes | POST | `/minutes/{id}/finalize` | Body: `{ notifyResponsibles, notifyAll }`. Irreversible. |
| Get full item history | GET | `/action-items/{id}/history` | Traverses `sourceActionItemId` chain |
| Get my open items | GET | `/users/{id}/open-items` | Cross-series, first-class query |
| Get series timeline | GET | `/series/{id}/minutes` | Returns linked-list chain in order |
| Carry forward topics | POST | `/minutes` | Creating new Minutes triggers carry-forward |
| Archive series | POST | `/series/{id}/archive` | One-way. Series becomes read-only. |
| Append note | POST | `/action-items/{id}/notes` | Append-only. No PUT/PATCH on notes. |

---

## What This Model Does Not Include (Yet)

These are deliberate exclusions for v1, not oversights:

**Attachments.** 4Minitz had file attachments on Minutes. Docket v1 does not.
Binary storage introduces operational complexity disproportionate to v1 value.
The schema reserves space by not adding an `attachments` column — a future
`MinutesAttachment` entity can be added without migration pain.

**Recurrence scheduling.** Docket does not auto-create Minutes on a schedule.
The moderator creates Minutes when a meeting occurs. Scheduling belongs in a
calendar system (Outlook, Google Calendar), not in Docket.

**Full-text search.** Not in v1. The data model supports it (all text fields are
plain text, no JSON blobs) — adding search is an indexing concern, not a schema concern.

**Multi-tenancy / Organizations.** All data is currently scoped to a single Docket
installation. An `Organization` entity wrapping MeetingSeries is the obvious
multi-tenancy path but is deferred until the single-tenant model is proven.

**Audit log.** All entities carry `createdAt` / `updatedAt`. A full audit log of
who changed what and when is deferred to v2. The append-only `ActionItemNote`
table partially satisfies audit requirements for the most sensitive data.

---

## Relationship to Keeper

Docket has no knowledge of Keeper. Keeper is a client of the Docket API.

From Docket's perspective, Keeper is an API consumer that:
- Creates ActionItems (via the standard POST endpoint)
- Appends ActionItemNotes with extraction provenance
- Queries `GET /users/{id}/open-items` to build follow-up messages
- Sets `assignedInAbsentia = true` when the extraction agent detects absent assignment

Docket does not know or care that Keeper uses AI. The `assignedInAbsentia` flag and
the `ActionItemNote` provenance string are the only concessions to the AI extraction
use case, and both are useful to human users as well.
