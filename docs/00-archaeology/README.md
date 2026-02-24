# 4Minitz Archaeology

## What Is This?

Before writing a single line of Keeper/Docket code, we ran a live instance of
[4Minitz](https://github.com/4minitz/4minitz) in Docker and explored it systematically.
4Minitz is the dead open-source meeting minutes tool from which Keeper/Docket is spiritually
descended. The goal was to extract what they got right, identify what they missed,
and avoid reinventing decisions they already made well.

This directory contains screenshots, notes, and schema findings from that investigation.
Every design decision in Docket that traces back to 4Minitz behavior is documented here
so the reasoning is preserved.

---

## Running 4Minitz Locally

```bash
docker pull 4minitz/4minitz
docker run -d -p 3100:3100 4minitz/4minitz
# open http://localhost:3100
```

The instance used for this archaeology was run on 2026-02-24.

---

## Data Model (Extracted from Live Behavior)

```
MeetingSeries
└── Minutes (one per meeting instance, explicitly chained as linked list)
    └── Topic
        ├── InfoItem  (discussion notes, status updates, decisions recorded)
        └── ActionItem (commitment: who / what / by when)
            ├── responsible (single owner, must be an Invited participant)
            ├── duedate
            ├── priority (1-5 numeric, displayed as e.g. "3 - Medium")
            ├── labels[] (many-to-many — junction table, not enum)
            └── notes[] (chronological, timestamped log entries)
```

### Participant Roles (Three-Tier Model)

| Role | Attends | Can Own Action Items | Receives Minutes |
|---|---|---|---|
| Moderator | Yes | Yes | Yes |
| Invited | Yes | Yes | Yes |
| Informed | No | No | Yes |

"Informed" participants receive the finalized minutes email but have no attendance
expectation and cannot be assigned action items. This maps directly to a distribution
list concept: stakeholders who need the record without being in the room.

---

## Key Behaviors Observed

### Finalization

Finalization is the terminal state transition for a set of Minutes. It is:

- **Irreversible** — no edit or delete after finalization
- **Versioned** — the finalized email header shows `2026-02-02 (V1)`, confirming explicit versioning
- **Decoupled from notification** — the finalization dialog offers two independently toggleable checkboxes:
  - "Send Action Items To Responsibles" (individual per-owner email)
  - "Send Meeting Minutes (Information Items & Action Items)" (full minutes to all participants)

  This matters: you can finalize without notifying, or choose which notifications to send.
  Docket's `POST /minutes/{id}/finalize` should accept notification control flags.

There is **no archive state** for a MeetingSeries. 4Minitz has no concept of closing or
retiring a series — finalization applies only to individual Minutes. The series simply
continues until deleted. This is a gap: **Docket must add `archived` as a first-class
MeetingSeries state** so Professional Services engagements can be retired without
deleting the commitment history.

### Carry-Forward (Recurring Topics)

Topics marked as recurring carry forward automatically to the next meeting's Minutes.
Carry-forward is **by reference with appended notes**, not a copy — the same record
accumulates a chronological note chain across multiple meetings. Full history is always
visible.

The visual treatment of a carried-forward item distinguishes:
- **Open/unresolved** — teal/cyan background highlight, unchecked checkbox
- **Completed** — gray background, checked checkbox with strikethrough in email output

This visual distinction survives finalization and appears in the "My Action Items"
dashboard post-finalization.

### Skipped vs. Open Recurring Items

A recurring topic can be explicitly skipped (carried to the next meeting without
discussion) or left open (discussed but not resolved). Both states carry forward.
The finalization dialog does not block on open recurring items — you can finalize
with unresolved recurring topics present.

### Absent Participant Assignment

Action items can be assigned to participants who were not present at the meeting.
The system records the absence but permits the assignment. The moderator adds a note
explaining the assignment was made in absentia.

**Keeper extraction agent implication:** Do not filter commitments about people who
weren't in the meeting. "Jeff will handle that" is a valid extractable commitment
regardless of Jeff's presence. Flag for review, but do not discard.

### Labels Are Many-to-Many

A single ActionItem or Topic can carry multiple labels simultaneously (observed:
`Decision` + `Proposal` + `New` on one item; `Decision` alone on another).

**Schema implication:** Docket needs a junction table (`ActionItemLabels`), not an
enum column. Built-in label types observed: `Decision`, `Proposal`, `New`,
`Status:GREEN`, `Status:YELLOW`, `Status:RED`. The status labels appear to function
as a traffic-light health indicator for topics, distinct from the action-type labels.

### Minutes Are a Linked List

Individual Minutes instances are explicitly linked to their predecessor via a
"Previous: YYYY-MM-DD" reference. This is not just date sorting — it is a
formal linked-list structure. `GET /minutes/{id}/history` must traverse this chain,
not just filter by date range.

### Cross-Series Personal Dashboard

"My Action Items" is a first-class view showing all open action items across all
MeetingSeries for the logged-in user. This is not an optional reporting feature —
it is the primary mechanism by which individual users track their own commitments.

**API implication:** `GET /owner/{id}/open-items` is a first-class Docket endpoint,
not a derived query.

### Chronological Note Log on Action Items

Each ActionItem carries a timestamped note log, displayed chronologically. Notes are
appended over time as context accumulates. The finalization email renders the full
log at the moment of finalization — the email is a snapshot of the complete
commitment history, not just the current state.

Example from the live session:
> 2026-02-24: Ask Jeff Griffith to periodically review the email settings and
> maintain documentation

### Email Notifications

Two email types confirmed from live observation:

**Agenda Email** (sent before the meeting):
- Meeting name, project, date
- Invited participants
- Agenda topics (may be empty if topics not yet added)
- Links: Open Series, Open Minutes

**Finalized Minutes Email** (sent on finalization):
- Meeting name, project, date, version number
- Participants / Absent / Informed (three separate fields)
- Discussed Topics with nested Action Items
- Completed action items rendered with **strikethrough**
- Open Topics section (shows "None" if all resolved or skipped)
- Links: Open Series, Open Minutes

**Action Items Email** (sent to each responsible on finalization, if enabled):
- One email per responsible, listing only their assigned items
- Each item: Topic, Priority, Due Date, Responsible, Labels
- Full chronological Details log for each item

### Finalized Minutes Email as the Archival Record

4Minitz has no export or archive feature. The finalized minutes email — combined with
the linked "Open Minutes" deep link — functions as the de facto archival artifact.
The email is suitable for PDF export (the only "archive copy" the system produces).
Docket should generate a structured representation suitable for PDF/HTML export at
finalization, since the email alone is fragile as a long-term record.

---

## Open Questions — All Resolved

| Question | Answer |
|---|---|
| What happens when a MeetingSeries is archived? | No archive state exists. Series continues until deleted. **Docket must add this.** |
| Can action items be assigned to absent participants? | Yes. Flagged in record, allowed by system. |
| How does carry-forward work mechanically? | Reference with chronological note chain, not copy. |
| What is the participant/owner model? | Three roles: Moderator, Invited (can own), Informed (cannot own). |

---

## Implications for Docket Schema

Collected schema decisions driven by archaeology findings:

1. **Labels**: Junction table (`ActionItemLabels`), not enum. Support system labels (Status:GREEN/YELLOW/RED) and action labels (Decision, Proposal) as distinct label types.
2. **Notes**: Separate `ActionItemNote` entity with `(itemId, timestamp, text, authorId)`. Not a text blob.
3. **Minutes chaining**: `Minutes.previousMinutesId` foreign key, not just a date field.
4. **MeetingSeries state**: Add `status` enum: `active | archived`. Archived series are read-only but not deleted.
5. **Participant roles**: `SeriesParticipant(seriesId, userId, role: moderator | invited | informed)`.
6. **Owner constraint**: ActionItem `responsibleId` must reference an `invited` or `moderator` participant in the parent series.
7. **Finalization versioning**: `Minutes.version` integer, incremented on each finalize (supports re-finalization after correction if we choose to allow it).
8. **Notification flags**: `POST /minutes/{id}/finalize` body includes `{ notifyResponsibles: bool, notifyAll: bool }`.
9. **Cross-series owner query**: `GET /owner/{userId}/open-items` as first-class endpoint.
10. **Pre-meeting scheduling**: `Minutes.scheduledFor` timestamp to drive agenda email timing.

---

## Screenshots

All screenshots are in `/docs/00-archaeology/screenshots/`.

| File | What It Shows |
|---|---|
| `001-0-RegisterAccount.jpg` | Registration / first login |
| `001-1-MainView001.jpg` | Main dashboard — Meetings tab |
| `001-1-MainViewActionItems.jpg` | Main dashboard — My Action Items tab (cross-series view) |
| `002-CreateMeetingSeries.jpg` | Creating a new MeetingSeries |
| `003-CreateMeetingMinutes0202Meeting.jpg` | Creating Minutes for a meeting instance |
| `003-CreateMeetingMinutes0202Meeting002.jpg` | Minutes with topics added |
| `003-CreateMeetingMinutes0202MeetingFinalized.jpg` | Finalized Minutes view |
| `004-FollowupMeeting.jpg` | Second meeting — recurring topics carried forward |
| `004-FollowupMeetingFinalized.jpg` | Second meeting finalized |
| `005-TopicDetails.jpg` | Topic detail — many-to-many labels visible |
| `010-AgendaEmail.jpg` | Agenda email sent before the meeting |
| `011-SkippedRecurringMeetingItem.jpg` | Open vs. completed carry-forward visual treatment (teal = open) |
| `012-FinalizingMeetingSkippedRecurringItem.jpg` | Finalization dialog — decoupled notification checkboxes |
| `013-FinalizedMinutesActionItems.jpg` | Action items email sent to responsible on finalization |
| `014-FInalizedMinutesAgendaItems.jpg` | Full minutes email — strikethrough for done, version number, role breakdown |
| `015-ActionItemsAfterFinalize.jpg` | My Action Items dashboard post-finalization |

---

## What 4Minitz Got Right

- Immutable finalized record with explicit versioning
- Three-tier participant model (Moderator / Invited / Informed)
- Carry-forward by reference, not copy — history accumulates naturally
- Chronological note log on action items — commitment history preserved
- Cross-series personal dashboard — makes the tool individually valuable
- Decoupled notifications — finalize and notify are separate decisions
- Explicit Minutes linked list — chain of custody is structural, not incidental

## What 4Minitz Missed (Docket Will Address)

- No archive state for MeetingSeries — completed engagements must be deleted or left open
- No structured export — email is the only archival artifact
- No API — integrations are impossible without scraping
- No external participant support — LDAP/internal users only, no cross-org meeting support
- No AI extraction layer — commitments require manual entry
- Framework entropy (Meteor.js) — made long-term maintenance unsustainable
