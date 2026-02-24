# Docket Feature Specification

**Version:** 0.1.0  
**Status:** Draft  
**Last Updated:** 2026-02-24  
**Audience:** Contributors building or extending Docket  
**Companion documents:**
- `docs/01-spec/docket-domain-model.md` — entity definitions, constraints, state machines
- `docs/01-spec/docket-openapi.yaml` — API endpoint reference
- `docs/02-architecture/ADR-*.md` — architecture decisions and their rationale

---

## Purpose and Philosophy

Docket is a commitment record for recurring meetings. It answers one question with
structural certainty: *what did someone agree to do, in which meeting, and has it
been done?*

The problem it solves is specific. In Professional Services and project delivery,
meetings produce verbal commitments that are poorly captured, inconsistently
followed up, and impossible to audit after the fact. Scattered notes, email threads,
and memory are not enough. When a deliverable is missed or a decision is disputed,
there is no authoritative record of what was actually said.

Docket provides that record. It is not a project management tool, a task tracker,
or a calendar. It is a chain of custody for commitments made in meetings — from
the moment they are recorded to the moment they are resolved, with full history
preserved at every step.

**What Docket is not responsible for:**

Docket does not schedule meetings, send calendar invites, or manage projects.
It does not replace a task tracker — it complements one. It does not extract
commitments from transcripts automatically; that is Keeper's responsibility.
Docket is the record. Keeper is the agent that populates and monitors it.

---

## Actors

Four actors interact with Docket. Understanding their distinct concerns is
essential to implementing features correctly.

**Moderator.** The person who owns and chairs a meeting series. They create the
series, manage participants, create each Minutes record, and finalize it when the
meeting is over. They are accountable for the completeness and accuracy of the
record. There is exactly one moderator per series at any time.

**Invited Participant.** An active member of the meeting. They attend, contribute
to topics, and can own action items. They receive both the agenda (before the
meeting) and the finalized minutes (after). They can add topics and items to a
draft Minutes but cannot create or finalize Minutes records.

**Informed Participant.** A stakeholder who needs the record but does not attend.
They receive the finalized minutes email only. They cannot own action items. This
role exists because many organizational stakeholders need visibility into what was
committed without being in the room — executives, adjacent teams, auditors.

**Keeper.** The AI agent built on top of Docket. From Docket's perspective, Keeper
is an API client with moderator-level credentials. It creates action items from
extracted transcripts, appends provenance notes, and queries open items for
follow-up. Docket has no special knowledge of Keeper — Keeper uses the same API
available to any client.

---

## Feature Areas

1. [User Management](#1-user-management)
2. [Meeting Series Lifecycle](#2-meeting-series-lifecycle)
3. [Participant Management](#3-participant-management)
4. [Minutes Lifecycle](#4-minutes-lifecycle)
5. [Pre-Creation Gate](#5-pre-creation-gate)
6. [Attendance Recording](#6-attendance-recording)
7. [Topics](#7-topics)
8. [Info Items](#8-info-items)
9. [Action Items](#9-action-items)
10. [Action Item Notes](#10-action-item-notes)
11. [Labels](#11-labels)
12. [Finalization](#12-finalization)
13. [Carry-Forward](#13-carry-forward)
14. [Cross-Series Personal View](#14-cross-series-personal-view)
15. [Series Archival](#15-series-archival)

---

## 1. User Management

### Background

Docket maintains its own user table. In v1, users are created directly — there is
no federated identity (no OAuth, LDAP, or Azure AD). The schema reserves
`externalId` for future federation without requiring it now.

A user's identity in Docket is their email address. Display name is what appears
in emails, dashboards, and the API. There are no global roles — a user's
permissions are always relative to a specific MeetingSeries via their
SeriesParticipant role.

### Features

**1.1 — Create a user account**

A user can register with an email address and display name. Email addresses must
be unique across all users.

*Acceptance criteria:*
- POST /users with a valid email and displayName returns HTTP 201 and the created User record.
- A second POST /users with the same email returns HTTP 409.
- Email is stored in canonical lowercase form regardless of input casing.
- The created user has no SeriesParticipant memberships — they exist but belong to no series.

**1.2 — Retrieve a user**

Any authenticated caller may retrieve a user by ID.

*Acceptance criteria:*
- GET /users/{userId} returns the User record including id, email, displayName, and timestamps.
- GET /users/{userId} for a non-existent ID returns HTTP 404.

**1.3 — Update a user**

Users may update their own display name. The externalId field may be updated by
system callers to support future federation migrations.

*Acceptance criteria:*
- PATCH /users/{userId} with a new displayName updates the record and returns the updated User.
- A caller may not update another user's record (HTTP 403).
- Email address cannot be changed via PATCH.

---

## 2. Meeting Series Lifecycle

### Background

A MeetingSeries is the top-level container. It corresponds directly to a recurring
calendar series — the event you create in Outlook or Google Calendar and that recurs
weekly, monthly, or on whatever cadence the team agrees. Each occurrence of that
calendar event becomes one Minutes record within the series.

A Professional Services engagement typically involves multiple parallel series —
a steering committee series, a weekly status series, a team standup series. Each
is tracked independently. Cross-series coordination (grouping series into a named
engagement) is a v2 feature; see ADR-005.

A series is created when a recurring meeting program begins and archived when it
ends. It is never deleted — its history must be preserved.

### Features

**2.1 — Create a series**

A moderator creates a series with a name and optional project tag. The caller
automatically becomes the series moderator.

*Acceptance criteria:*
- POST /series with a name returns HTTP 201 and the created MeetingSeries.
- The caller is automatically added as a SeriesParticipant with role `moderator`.
- The series `status` is `active` on creation.
- `project` is optional. If omitted, the series has no project tag.
- `externalCalendarId` is optional. If provided, it is stored without validation — Docket does not verify that it corresponds to a real calendar object.

**2.2 — List series**

An authenticated caller sees all series they participate in, regardless of role.

*Acceptance criteria:*
- GET /series returns all MeetingSeries where the caller has a SeriesParticipant record.
- Results include series where the caller is moderator, invited, or informed.
- Results default to `active` series only. Passing `status=archived` returns archived series. Passing no filter returns active series only.
- A series the caller does not participate in does not appear in results, even if it exists.

**2.3 — Retrieve a series**

Any participant in a series may retrieve its full record.

*Acceptance criteria:*
- GET /series/{seriesId} returns the MeetingSeries record for any caller who is a participant.
- Non-participants receive HTTP 403, not HTTP 404 — existence is not revealed to non-participants.

**2.4 — Update a series**

Only the moderator may update a series name, project tag, or external calendar ID.

*Acceptance criteria:*
- PATCH /series/{seriesId} by the moderator updates the specified fields and returns the updated record.
- An invited or informed participant attempting PATCH receives HTTP 403.
- Updating a series does not affect any existing Minutes or their contents.
- An archived series cannot be updated — PATCH on an archived series returns HTTP 422.

---

## 3. Participant Management

### Background

Participants are managed at the series level, not the Minutes level. A participant's
role in the series determines what they can do in every Minutes under that series.
Roles are: `moderator` (one per series, owns the series), `invited` (active
participant, can own action items), and `informed` (receives minutes, cannot own
action items).

The role table is not just an authorization mechanism — it determines who receives
which emails. Getting participant management right is critical to the notification
features in Finalization (Feature Area 11).

### Features

**3.1 — Add a participant**

The moderator may add a user to a series with a specified role. A user can hold
only one role per series.

*Acceptance criteria:*
- POST /series/{seriesId}/participants with a valid userId and role returns HTTP 201.
- Adding a user who is already a participant returns HTTP 409.
- Only the series moderator may add participants; other callers receive HTTP 403.
- The userId must reference an existing User; a non-existent userId returns HTTP 422.
- All three roles (`moderator`, `invited`, `informed`) are valid on this endpoint. Adding a second `moderator` via this endpoint is rejected with HTTP 422 — use the role transfer endpoint instead.

**3.2 — List participants**

Any series participant may list the participants of their series.

*Acceptance criteria:*
- GET /series/{seriesId}/participants returns all SeriesParticipant records including the expanded User.
- Non-participants receive HTTP 403.

**3.3 — Change a participant's role**

The moderator may change any participant's role. Transferring the `moderator` role
to another user demotes the current moderator to `invited`.

*Acceptance criteria:*
- PATCH /series/{seriesId}/participants/{userId} by the moderator updates the role and returns the updated SeriesParticipant.
- If the target user is being promoted to `moderator`, the current moderator is simultaneously demoted to `invited`. Both changes are atomic — there is never a moment with zero or two moderators.
- A non-moderator attempting this operation receives HTTP 403.
- Setting a role to its current value is a no-op that returns HTTP 200.

**3.4 — Remove a participant**

The moderator may remove a participant. The moderator cannot remove themselves
while they hold the only moderator role.

*Acceptance criteria:*
- DELETE /series/{seriesId}/participants/{userId} removes the participant and returns HTTP 204.
- Attempting to remove the only moderator returns HTTP 422.
- Removing a participant does not alter any existing ActionItem where they are `responsibleId`. Those records are preserved as-is; the historical assignment remains valid.
- Non-moderators attempting removal receive HTTP 403.

---

## 4. Minutes Lifecycle

### Background

A Minutes record is the complete record of a single meeting instance. It exists
in two phases: before the meeting (a plan — scheduled date, expected participants,
agenda topics) and after (a historical record — who attended, what was discussed,
what was committed). Finalization is the irreversible transition between these
phases.

Contributors should resist the temptation to model Minutes as "the document written
after a meeting." It is the full lifecycle record of a planned event. Creating a
Minutes record before a meeting happens is correct and expected — that is how the
agenda is established and how the pre-meeting email is generated.

Minutes are never deleted. A Minutes record created for a meeting that was
cancelled, never happened, or was created by mistake is abandoned — not deleted.
The existence of the record is itself historically significant, and the required
abandonment note is the explanation. "This meeting was cancelled" or "Created in
error — hit return too soon" are both valid, honest reasons that belong in the
permanent record.

See ADR decisions in the architecture folder for the rationale behind finalization
being irreversible.

### Features

**4.1 — Create a Minutes**

The series moderator creates a Minutes record when a meeting is scheduled or has
occurred.

*Acceptance criteria:*
- POST /minutes with a valid seriesId and scheduledFor returns HTTP 201 and a draft Minutes.
- Only the series moderator may create Minutes; invited and informed participants receive HTTP 403.
- Creating Minutes for an archived series returns HTTP 422.
- Before creating, the system traverses the full linked-list chain for the series and checks for any Minutes with `status = draft`. If any are found, the request is rejected with HTTP 422, error code `UNRESOLVED_DRAFTS_EXIST`, and a `detail.unresolvedMinutes` array containing the id and scheduledFor of each orphaned draft. See Feature Area 5 for the pre-creation gate.
- On creation, the system automatically sets `previousMinutesId` to the most recently created Minutes in the series, forming the linked list. If this is the first Minutes in the series, `previousMinutesId` is null.
- On creation, carry-forward runs automatically (see Feature Area 13). The caller does not trigger carry-forward explicitly.
- `status` is always `draft` on creation.
- `version` is always `0` on creation.

**4.2 — Retrieve a Minutes**

Any series participant may retrieve a Minutes record with its full contents.

*Acceptance criteria:*
- GET /minutes/{minutesId} returns the Minutes with its topics, info items, action items, and attendees expanded.
- Non-participants receive HTTP 403.

**4.3 — Update a draft Minutes**

A draft Minutes may have its scheduled date, global note, or global note pin status
updated. Both moderators and invited participants may do this.

*Acceptance criteria:*
- PATCH /minutes/{minutesId} by a moderator or invited participant updates the specified fields.
- PATCH on a finalized Minutes returns HTTP 422 with error code `MINUTES_FINALIZED`.
- Informed participants attempting PATCH receive HTTP 403.
- Updating `scheduledFor` does not affect the linked-list chain — `previousMinutesId` is structural, not date-derived.

**4.4 — Abandon a draft Minutes**

A draft Minutes is abandoned when the moderator explicitly acknowledges that it
will never be finalized. Minutes are never deleted — abandonment is the only
non-finalization exit from draft status.

Common reasons: the meeting was cancelled, the meeting happened but was never
followed up on, the record was created in error, or the moderator was absent and
no one else had authority to finalize.

*Acceptance criteria:*
- POST /minutes/{minutesId}/abandon by the moderator transitions the Minutes to `abandoned` and returns HTTP 200.
- The request body must include a non-empty `note` field. A missing or empty note returns HTTP 422 with error code `ABANDONMENT_NOTE_REQUIRED`.
- `abandonedAt` is set to the current server timestamp. `abandonedBy` is set to the caller's user ID. `abandonmentNote` is set to the provided note text.
- All `open` and `deferred` ActionItems beneath the abandoned Minutes are atomically set to `abandoned`. This transition is system-initiated and cannot be reversed.
- Abandoned ActionItems are immutable. No caller may update their status, title, due date, priority, or labels after abandonment. Post-meeting ActionItemNote appends remain permitted.
- Attempting to abandon a finalized Minutes returns HTTP 422 with error code `MINUTES_FINALIZED`.
- Attempting to abandon an already-abandoned Minutes returns HTTP 422.
- Non-moderators attempting abandonment receive HTTP 403.
- Minutes are never deleted. There is no DELETE endpoint for Minutes records.

---

## 5. Pre-Creation Gate

### Background

The pre-creation gate is the mechanism that prevents orphaned draft Minutes from
accumulating silently. When a moderator creates a new Minutes for a series, the
system first checks whether any prior Minutes in the chain is still in draft status.
If any are found, the creation is blocked until the moderator resolves them.

This matters because there are real, common reasons a draft Minutes gets abandoned
without being explicitly closed: the moderator had back-to-back meetings, the
meeting was cancelled and everyone forgot the record existed, or the moderator was
absent and there was no moderator handoff. Without the gate, these orphaned drafts
accumulate and the chain of custody becomes unreliable. With the gate, the system
actively coerces the moderator to make a choice before moving forward.

The gate checks the full chain — not just the immediately prior Minutes. A finalized
Minutes between two draft Minutes does not exempt the earlier draft. Every node in
the chain must have a terminal status before a new one can be added.

### Features

**5.1 — Block new Minutes creation when unresolved drafts exist**

*Acceptance criteria:*
- POST /minutes is preceded by a full backwards traversal of the series' linked-list chain.
- If any Minutes with `status = draft` is found anywhere in the chain, the request is rejected with HTTP 422 and error code `UNRESOLVED_DRAFTS_EXIST`.
- The error response body includes `detail.unresolvedMinutes`: an array of objects each containing `id`, `scheduledFor`, and `status` for every orphaned draft found.
- The array is ordered chronologically (oldest first) so the moderator can work through them in order.
- If no unresolved drafts exist, the gate passes silently and creation proceeds normally.

**5.2 — Resolution options for an orphaned draft**

The moderator has two options for resolving an orphaned draft. Both are valid and
both result in a terminal state with an explanation on record.

*Acceptance criteria:*
- **Finalize:** POST /minutes/{minutesId}/finalize is available on any draft Minutes, including ones that were never discussed. A Minutes may be finalized with no topics, no attendees, and no action items — the finalized record simply has nothing in it. This is the correct resolution when the meeting happened but was not documented.
- **Abandon:** POST /minutes/{minutesId}/abandon is available on any draft Minutes. Requires a non-empty note. This is the correct resolution when the meeting was cancelled, never happened, or the record was created in error.
- After resolution, if other unresolved drafts remain in the chain, the next POST /minutes attempt will surface them. The moderator must work through all of them.

---

## 6. Attendance Recording

### Background

Attendance recording serves two purposes. First, it drives the Absent field in the
finalized minutes email — participants who were expected (invited or moderator role)
but did not attend are listed separately. Second, it drives the `assignedInAbsentia`
flag on action items — if an action item owner was not present, the system flags the
assignment automatically so the owner receives appropriate context when notified.

Informed participants are never recorded as attendees and never appear as absent —
they were never expected to attend.

### Features

**6.1 — Record attendance**

A moderator or invited participant records that a user attended the meeting.

*Acceptance criteria:*
- POST /minutes/{minutesId}/attendees with a valid userId records attendance and returns HTTP 201.
- The userId must be a `moderator` or `invited` SeriesParticipant. Attempting to record an `informed` participant as an attendee returns HTTP 422.
- Recording the same user twice returns HTTP 409.
- Attendance may only be recorded on a draft Minutes; attempting on a finalized Minutes returns HTTP 422.
- A user need not be recorded as an attendee before an action item can be assigned to them — attendance and assignment are independent.

**6.2 — Remove an attendance record**

An attendance record may be removed while the Minutes is still in draft status —
for example, if a moderator recorded someone who then had to leave before the meeting
started.

*Acceptance criteria:*
- DELETE /minutes/{minutesId}/attendees/{userId} returns HTTP 204.
- Removing attendance from a finalized Minutes returns HTTP 422.
- Removing attendance does not change the `assignedInAbsentia` flag on any existing action items — those were set at the time the item was created and reflect the state at that moment.

**6.3 — Derive absent participants**

Absent participants are not stored — they are derived. A participant is absent if
they are an `invited` or `moderator` SeriesParticipant but do not have a
MinutesAttendee record for this Minutes.

*Acceptance criteria:*
- GET /minutes/{minutesId} includes an `absent` array in the response, derived at read time.
- The `absent` array contains only `moderator` and `invited` participants — never `informed`.
- If all expected participants attended, `absent` is an empty array.

---

## 7. Topics

### Background

Topics are the agenda items of a meeting. They structure the discussion and anchor
both informational notes and action items. Every info item and every action item
belongs to a topic — there are no free-floating items in Docket.

Topics come in two types. An `adhoc` topic exists only in the Minutes it was created
on — it was specific to that meeting. A `recurring` topic carries forward
automatically to the next Minutes if it is unresolved at finalization — it is an
ongoing agenda item that the team revisits until it is closed. The distinction is
the team's acknowledgement that some work is done in a single meeting and some spans
many.

### Features

**7.1 — Add a topic**

A moderator or invited participant may add a topic to a draft Minutes.

*Acceptance criteria:*
- POST /minutes/{minutesId}/topics with a title returns HTTP 201 and the created Topic.
- `type` defaults to `adhoc` if not specified.
- `isOpen` is always `true` on creation.
- `isSkipped` is always `false` on creation.
- Topics may not be added to a finalized Minutes — this returns HTTP 422.
- `sortOrder` may be specified; if omitted, the topic is appended at the end.

**7.2 — Retrieve topics**

Any series participant may list the topics of a Minutes.

*Acceptance criteria:*
- GET /minutes/{minutesId}/topics returns topics in `sortOrder` order.
- Results include both open and closed topics by default. The `includeOpen` and `includeClosed` query parameters filter results independently.

**7.3 — Update a topic**

A moderator or invited participant may update a topic's title, type, open/skipped
status, sort order, or responsible user while the Minutes is in draft status.

*Acceptance criteria:*
- PATCH /topics/{topicId} by a moderator or invited participant updates the specified fields.
- PATCH on a topic within a finalized Minutes returns HTTP 422.
- Setting `isSkipped: true` on a topic that is `isOpen: false` returns HTTP 422 — a closed topic cannot be skipped.
- Setting `isOpen: false` simultaneously clears `isSkipped` if it was true — a closed topic is not skipped.
- Changing a topic's `type` from `adhoc` to `recurring` takes effect at the next finalization — it does not retroactively alter the current Minutes.

**7.4 — Delete a topic**

A moderator may delete a topic from a draft Minutes. Deleting a topic deletes all
its info items and action items.

*Acceptance criteria:*
- DELETE /topics/{topicId} returns HTTP 204 and removes the topic and all its children.
- Deleting a topic within a finalized Minutes returns HTTP 422.
- Deleting a carried-forward topic (one with a non-null `sourceTopicId`) does not affect the source topic in the prior Minutes. The source topic's record is unchanged.

---

## 8. Info Items

### Background

Info items are the "what we discussed" layer of a topic. They are free-text notes,
status updates, or records of decisions — anything that needs to appear in the
minutes but is not a commitment. They do not carry forward. They do not have owners
or due dates. They are the context that makes the action items comprehensible in
six months when someone reviews the record.

### Features

**8.1 — Add an info item**

A moderator or invited participant may add an info item to a topic in a draft Minutes.

*Acceptance criteria:*
- POST /topics/{topicId}/info-items with text returns HTTP 201 and the created InfoItem.
- Info items may not be added to a finalized Minutes — this returns HTTP 422.
- `pinnedDate` is optional. If provided, the item is rendered as a dated bullet in the finalized minutes output.

**8.2 — Update an info item**

An info item's text or pinned date may be updated while the parent Minutes is draft.

*Acceptance criteria:*
- PATCH /info-items/{infoItemId} updates the specified fields and returns the updated InfoItem.
- PATCH on an info item within a finalized Minutes returns HTTP 422.

**8.3 — Delete an info item**

An info item may be deleted while the parent Minutes is draft.

*Acceptance criteria:*
- DELETE /info-items/{infoItemId} returns HTTP 204.
- DELETE on an info item within a finalized Minutes returns HTTP 422.

---

## 9. Action Items

### Background

Action items are the core unit of value in Docket. Each one represents a commitment:
something a named person agreed to do, by a specific date, traceable to the meeting
in which it was made.

Every action item has exactly one owner (`responsibleId`). Shared ownership is not
supported — if two people need to do something, that is two action items. This is
intentional: shared ownership is where commitments go to die. "We will handle it"
means no one handles it. Docket forces the question.

The owner must be an `invited` or `moderator` participant in the series. An
`informed` participant cannot own an action item because they are, by definition,
a stakeholder rather than an active participant. If Keeper extracts a commitment
attributed to someone who is only `informed`, that is a data quality issue to flag
for review — not something Docket should silently permit.

### Features

**9.1 — Create an action item**

A moderator or invited participant may create an action item on a topic in a draft
Minutes.

*Acceptance criteria:*
- POST /topics/{topicId}/action-items with a title and responsibleId returns HTTP 201 and the created ActionItem.
- `responsibleId` must reference a `moderator` or `invited` participant in the parent series. Referencing an `informed` participant returns HTTP 422.
- `status` is always `open` on creation.
- `priority` defaults to 3 (Medium) if not specified.
- `isRecurring` defaults to `false` if not specified.
- If `responsibleId` references a user who has no MinutesAttendee record for the parent Minutes, the system sets `assignedInAbsentia: true` automatically. The caller may also set this explicitly.
- Action items may not be created on a finalized Minutes — this returns HTTP 422.

**9.2 — Update an action item**

An action item's title, due date, priority, status, or recurring flag may be updated
while the parent Minutes is in draft status.

*Acceptance criteria:*
- PATCH /action-items/{actionItemId} updates the specified fields and returns the updated ActionItem.
- Status transitions must follow the state machine: `open` → `done`, `open` ↔ `deferred`. Attempting an invalid transition (e.g. `done` → `open`, or any transition to `abandoned`) returns HTTP 422 with error code `INVALID_STATUS_TRANSITION`. `abandoned` is system-set only and cannot be requested by any caller.
- PATCH on an action item within a finalized Minutes returns HTTP 422 with error code `MINUTES_FINALIZED`.
- Changing `responsibleId` is not permitted via PATCH — reassignment requires creating a new action item. This preserves the historical record of who the commitment was made to.

**9.3 — Retrieve an action item**

Any series participant may retrieve a full action item record including its notes
and labels.

*Acceptance criteria:*
- GET /action-items/{actionItemId} returns the ActionItem with `notes` (chronological, oldest first) and `labels` expanded.
- Non-participants receive HTTP 403.

**9.4 — Retrieve an action item's full history**

The carry-forward mechanism creates a chain of ActionItem records across meetings.
This endpoint traverses that chain to reconstruct the full commitment timeline.

*Acceptance criteria:*
- GET /action-items/{actionItemId}/history returns an ordered array of ActionItem records from the original (oldest) to the current (newest).
- Each item in the array includes its own notes and labels.
- If the action item has no `sourceActionItemId` (it is the original), the array contains only that item.
- The endpoint accepts any item in the chain — not just the current one. Passing the ID of an intermediate item returns the full chain from the original to the most recent descendant.

**9.5 — Apply a label to an action item**

Labels may be applied to action items while the parent Minutes is in draft status.

*Acceptance criteria:*
- POST /action-items/{actionItemId}/labels with a labelId applies the label and returns HTTP 201.
- Applying a label that is already applied returns HTTP 409.
- Applying a label to an item in a finalized Minutes returns HTTP 422.

**9.6 — Remove a label from an action item**

Labels may be removed from action items while the parent Minutes is in draft status.

*Acceptance criteria:*
- DELETE /action-items/{actionItemId}/labels/{labelId} removes the label and returns HTTP 204.
- Removing a label from a finalized Minutes returns HTTP 422.
- Removing a label that is not applied returns HTTP 404.

---

## 10. Action Item Notes

### Background

Notes are the chronological log of an action item. They accumulate over time and
are never edited or deleted. The timestamp is part of the meaning of the note —
it records when something was said about this commitment, and that cannot be changed
retroactively.

The `phase` field is the mechanism that separates the meeting record from the
post-meeting record. The server sets it automatically — callers cannot declare their
own phase. This guarantees a hard structural boundary between what was known at the
meeting and what came after.

Notes may be appended to action items in both draft and finalized Minutes. This is
the one deliberate exception to the general immutability of finalized records, and
it exists for a specific reason: Keeper and human participants need to be able to
add follow-up context — a reminder sent, a status update, a newly discovered
dependency — without triggering a full re-finalization cycle. The `phase` field
ensures this post-meeting context is always clearly distinguishable from the
original record.

### Features

**10.1 — Append a note**

Any moderator or invited participant may append a note to an action item. Notes
may be appended regardless of whether the parent Minutes is draft or finalized.

*Acceptance criteria:*
- POST /action-items/{actionItemId}/notes with text returns HTTP 201 and the created ActionItemNote.
- The `phase` field is set by the server — it is not accepted in the request body.
- If the parent Minutes is `draft`, `phase` is set to `meeting`.
- If the parent Minutes is `finalized`, `phase` is set to `post-meeting`.
- `createdAt` is set by the server at the moment of creation and cannot be overridden by the caller.
- An `informed` participant attempting to append a note receives HTTP 403.
- Notes returned in GET responses are always ordered by `createdAt` ascending (oldest first).

**10.2 — Phase boundary at finalization**

The `meeting`-phase note set is closed permanently when the parent Minutes is
finalized. This is a passive guarantee — no explicit action is required — but the
system must enforce it.

*Acceptance criteria:*
- Any note appended after finalization receives `phase: post-meeting`, regardless of any caller-supplied value.
- There is no API operation that can change the `phase` of an existing note.
- The finalization email renders only `meeting`-phase notes. Post-meeting notes are excluded from the email snapshot.
- GET /action-items/{actionItemId} returns all notes (both phases) with `phase` visible on each, so callers can filter by phase if needed.

---

## 11. Labels

### Background

Labels are tags that classify action items. They come in two categories. Action
labels classify the type of commitment: `Decision` means the action item is also
a recorded decision; `Proposal` means it is under consideration but not yet decided;
`New` means it is appearing for the first time. Status labels are a traffic-light
health indicator for topics: `Status:GREEN`, `Status:YELLOW`, `Status:RED`.

Six system labels are seeded on installation and cannot be deleted. Moderators may
create custom labels for their organization's conventions.

### Features

**11.1 — List labels**

Any authenticated caller may list available labels.

*Acceptance criteria:*
- GET /labels returns all labels, both system and custom.
- The `category` query parameter filters to `action` or `status` labels only.
- System labels are identified by `isSystem: true`.

**11.2 — Create a custom label**

Any authenticated caller may create a custom label.

*Acceptance criteria:*
- POST /labels with a name and category returns HTTP 201 and the created Label.
- Label names must be unique. A duplicate name returns HTTP 409.
- `color` is optional. If provided, it must be a valid hex color string (`#RRGGBB`).
- Custom labels are created with `isSystem: false`.

**11.3 — Delete a custom label**

Custom labels may be deleted. System labels may not.

*Acceptance criteria:*
- DELETE /labels/{labelId} for a custom label returns HTTP 204.
- DELETE /labels/{labelId} for a system label returns HTTP 422 with error code `SYSTEM_LABEL`.
- Deleting a label removes it from all ActionItemLabel and TopicLabel junction records — it is no longer applied to any item. This is a cascade delete on the junction records, not on the items themselves.

---

## 12. Finalization

### Background

Finalization is the most important operation in Docket. It is the moment the
Minutes transitions from a working document to a permanent record. It is
irreversible, versioned, and has downstream effects on notifications, immutability,
and carry-forward.

The design is deliberate. Making finalization irreversible removes the temptation
to quietly revise history. If something was wrong in the record, that is addressed
by appending a post-meeting note to the relevant action items — not by editing the
original. The original stands.

Finalization also triggers notifications. These are decoupled from the act of
finalization itself — the moderator can finalize without sending any emails, or
choose to send emails to responsible parties only, to all participants, or both.
This matters in practice: a moderator may want to finalize a record immediately
after the meeting but send the email later, or may finalize a record for an
internal-only meeting that needs no notification.

### Features

**12.1 — Finalize a Minutes**

The series moderator finalizes a Minutes record, transitioning it permanently to
`finalized` status.

*Acceptance criteria:*
- POST /minutes/{minutesId}/finalize by the moderator returns HTTP 200 and the finalized Minutes.
- `status` transitions from `draft` to `finalized`. This transition is permanent.
- `version` is set to 1.
- `finalizedAt` is set to the current server timestamp.
- `finalizedBy` is set to the caller's user ID.
- After finalization, no writes are accepted to the Minutes or any of its Topics, InfoItems, or ActionItems. All such attempts return HTTP 422 with error code `MINUTES_FINALIZED`.
- Post-finalization note appends on ActionItems continue to be accepted (see Feature Area 10).
- Non-moderators attempting finalization receive HTTP 403.
- Attempting to finalize an already-finalized Minutes returns HTTP 422.

**12.2 — Notification on finalization**

The finalization request body specifies which notifications to send. Notifications
are sent after the finalization transition is committed — a notification failure
does not roll back the finalization.

*Acceptance criteria:*
- If `notifyResponsibles: true`, each user who is `responsibleId` on at least one `open` or `deferred` ActionItem in this Minutes receives an individual email listing their assigned items.
- The action items email includes: item title, topic name, priority (as text: "3 - Medium"), due date, labels, and the full `meeting`-phase note log only.
- If `notifyAll: true`, all series participants (moderator, invited, and informed) receive the full minutes email.
- The full minutes email includes: meeting name, project, date, version number, participant list, absent participant list, informed participant list, all topics with their info items and action items, and an Open Topics section (empty if all topics are resolved or skipped).
- Completed action items (`status: done`) are rendered with strikethrough in the full minutes email.
- `notifyResponsibles` and `notifyAll` are independent boolean flags. Both may be true, both may be false, or either may be set independently.
- Email delivery failure is logged but does not cause the API to return an error. The finalization is committed regardless.

**12.3 — Finalization with open recurring topics**

Finalization is not blocked by open recurring topics. A Minutes may be finalized
with unresolved recurring topics — those topics carry forward to the next meeting.

*Acceptance criteria:*
- POST /minutes/{minutesId}/finalize succeeds regardless of whether any Topics have `isOpen: true`.
- Open recurring topics are included in the Open Topics section of the full minutes email.
- The carry-forward mechanism runs when the *next* Minutes is created, not at the time of finalization (see Feature Area 13).

---

## 13. Carry-Forward

### Background

Carry-forward is the mechanism by which recurring topics and their open action items
automatically appear in the next meeting's Minutes. It is what makes Docket a
living record rather than a collection of isolated meeting snapshots.

When new Minutes are created for a series, the system looks at the most recent
finalized Minutes and carries forward any recurring topic that is still open.
Carry-forward creates new records — it does not modify the records from the prior
Minutes. The originating records are linked via `sourceTopicId` and
`sourceActionItemId`, creating an auditable chain across meetings.

This design preserves the integrity of the prior finalized record while giving the
new meeting its own working copy to update. The history of a recurring commitment
— when it was first made, which meetings it appeared in, when it was finally
resolved — is always fully reconstructable.

### Features

**13.1 — Automatic carry-forward on Minutes creation**

When a new Minutes is created for a series, the system automatically carries forward
all eligible topics and action items from the most recent finalized Minutes.

*Acceptance criteria:*
- On POST /minutes, after the new Minutes is created, the system queries the most recent finalized Minutes in the series (by `previousMinutesId` chain, not date).
- All Topics from that finalized Minutes where `type = recurring` AND `isOpen = true` are carried forward.
- For each carried-forward Topic, a new Topic record is created in the new Minutes with: the same `title`, `type`, and `responsibleId`; `isOpen: true`; `isSkipped: false`; and `sourceTopicId` set to the original Topic's ID.
- InfoItems from the original Topic are NOT carried forward — they belong to the meeting in which they were created.
- For each carried-forward Topic, all ActionItems where `status = open` are also carried forward.
- For each carried-forward ActionItem, a new ActionItem record is created with: the same `title`, `responsibleId`, `dueDate`, `priority`, `isRecurring`, and labels; `status: open`; `assignedInAbsentia: false` (reset — attendance for the new meeting is not yet known); and `sourceActionItemId` set to the original ActionItem's ID.
- The original ActionItem's `status` is set to `deferred`. Its record in the prior finalized Minutes is not otherwise modified.
- ActionItemNotes from the original ActionItem are NOT carried forward. The note log belongs to the item instance in which the notes were written. The full history is accessible via GET /action-items/{id}/history.

**13.2 — No finalized Minutes in the series**

If the new Minutes is the first in the series, or if no prior Minutes have been
finalized, carry-forward produces no results.

*Acceptance criteria:*
- If no finalized Minutes exist in the series, the new Minutes is created with no topics (empty, or with only topics the caller adds manually).
- Draft Minutes are not eligible for carry-forward — only the most recent *finalized* Minutes is the source.

**13.3 — Pinned global note carry-forward**

If the most recent finalized Minutes had `globalNotePinned: true`, its `globalNote`
text is copied to the new Minutes.

*Acceptance criteria:*
- On Minutes creation, if the prior finalized Minutes has `globalNotePinned: true`, the new Minutes is initialized with the same `globalNote` text and `globalNotePinned: false` (the pin is not carried — only the text).
- If the prior finalized Minutes has `globalNotePinned: false`, the new Minutes' `globalNote` is null.

---

## 14. Cross-Series Personal View

### Background

The cross-series personal view is the feature that makes Docket individually useful,
not just organizationally useful. Without it, a user would have to visit every
series they participate in to understand their own open commitments. With it, the
answer to "what have I agreed to do?" is a single API call.

This endpoint is what Keeper queries when building a user's daily briefing: "Good
morning. You have five open action items across three meeting series. Two are due
this week."

### Features

**14.1 — Get a user's open action items across all series**

Any authenticated caller may retrieve their own open action items. Moderators and
system callers may retrieve another user's open items.

*Acceptance criteria:*
- GET /users/{userId}/open-items returns all ActionItems where `responsibleId = userId` and `status = open`, across all MeetingSeries the user participates in. Items with `status = abandoned`, `done`, or `deferred` are excluded.
- Results are ordered by `dueDate` ascending (nulls last), then by `priority` ascending.
- Each result includes `seriesName` and `topicTitle` as denormalized fields so the caller can display context without additional requests.
- The `seriesId` query parameter filters results to a single series.
- The `project` query parameter filters results to series with a matching project tag.
- Archived series are included in results if they contain open items — an item's openness is not affected by the series status.
- A caller requesting another user's open items (not their own) receives HTTP 403 unless they are a system caller.

---

## 15. Series Archival

### Background

When a Professional Services engagement ends, its meeting series should be retired.
Archiving a series preserves its complete history — every Minutes, every topic, every
action item, every note — while making clear that the series is no longer active.
No new meetings can be added. No existing records can be changed.

Archiving is irreversible. There is no "unarchive." If an engagement unexpectedly
resumes, a new series is created. This prevents the ambiguity of a series that
oscillates between active and inactive states, which would make the historical
record harder to interpret.

### Features

**15.1 — Archive a series**

The series moderator archives a series, transitioning it from `active` to `archived`.

*Acceptance criteria:*
- POST /series/{seriesId}/archive by the moderator returns HTTP 200 and the archived MeetingSeries.
- `status` transitions from `active` to `archived`. This transition is permanent.
- Before archiving, the system checks for any draft Minutes in the series chain. If any unresolved drafts exist, the archive request is rejected with HTTP 422 and error code `UNRESOLVED_DRAFTS_EXIST`. The moderator must resolve all draft Minutes before a series can be archived.
- On archival, all `open` and `deferred` ActionItems across all Minutes in the series are atomically set to `abandoned`. This is system-initiated and irreversible.
- The existence of abandoned ActionItems at archival time is historically significant — it indicates commitments that were made and never fulfilled. These records must not be altered. If the underlying work still needs to happen in a future engagement, a new ActionItem should be created in a new series with `sourceActionItemId` pointing back to the abandoned item.
- After archival, POST /minutes for this series returns HTTP 422.
- After archival, PATCH /series/{seriesId} returns HTTP 422.
- After archival, all read operations (GET) continue to function normally — the full history remains queryable indefinitely.
- Non-moderators attempting archival receive HTTP 403.
- Attempting to archive an already-archived series returns HTTP 422.

---

## Appendix: Error Codes

All error responses include a `code` field for programmatic handling. Defined codes:

| Code | HTTP Status | Meaning |
|---|---|---|
| `MINUTES_FINALIZED` | 422 | Operation requires a draft Minutes but the Minutes is finalized |
| `MINUTES_ABANDONED` | 422 | Operation requires a draft Minutes but the Minutes is abandoned |
| `SERIES_ARCHIVED` | 422 | Operation requires an active series but the series is archived |
| `INVALID_STATUS_TRANSITION` | 422 | The requested status change violates the state machine |
| `SYSTEM_LABEL` | 422 | Cannot delete or modify a system label |
| `SINGLE_MODERATOR` | 422 | Operation would leave the series with no moderator |
| `INFORMED_OWNER` | 422 | An informed participant cannot own an action item |
| `INFORMED_ATTENDEE` | 422 | An informed participant cannot be recorded as an attendee |
| `UNRESOLVED_DRAFTS_EXIST` | 422 | One or more draft Minutes must be finalized or abandoned before this operation |
| `ABANDONMENT_NOTE_REQUIRED` | 422 | Abandoning a Minutes requires a non-empty note explaining why |
| `DUPLICATE_PARTICIPANT` | 409 | User is already a participant in this series |
| `DUPLICATE_ATTENDEE` | 409 | User is already recorded as an attendee for this Minutes |
| `DUPLICATE_LABEL` | 409 | A label with this name already exists |
| `LABEL_ALREADY_APPLIED` | 409 | This label is already applied to this action item |
| `EMAIL_EXISTS` | 409 | A user with this email address already exists |

---

## Appendix: What Is Not in Scope for v1

These are deliberate exclusions. They are not oversights. Each is a candidate for
a future version.

**Engagement-level grouping.** Multiple series that are part of the same client
engagement cannot be formally grouped in v1. The `project` string tag and
`externalCalendarId` field are the forward-compatibility hooks. See ADR-005.

**File attachments.** Minutes had file attachments in 4Minitz. Docket v1 does not.
Binary storage adds operational complexity disproportionate to v1 value.

**Recurrence scheduling.** Docket does not auto-create Minutes on a schedule.
The moderator creates Minutes when a meeting occurs or is scheduled. Calendar
integration is intentionally out of scope.

**Full-text search.** All text fields are plain text (no JSON blobs), so adding
search later is an indexing concern, not a schema concern.

**Multi-tenancy.** All data is scoped to a single Docket installation. An
Organization entity is the obvious multi-tenancy path but is deferred until the
single-tenant model is proven.

**Re-finalization.** The `version` field on Minutes is designed to support
re-finalization in a future version. In v1, finalization is strictly one-way.

**Keeper-specific endpoints.** Keeper uses the standard Docket API. There are no
Keeper-specific endpoints in v1. If patterns emerge from Keeper usage that suggest
dedicated endpoints would be valuable, those will be added with proper API
versioning.
