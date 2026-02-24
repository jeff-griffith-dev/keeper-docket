# 4Minitz Archaeology Notes

**Source project:** [github.com/4minitz/4minitz](https://github.com/4minitz/4minitz)  
**Last meaningful activity:** ~2022  
**Analyzed by:** Jeff Griffith  
**Analysis date:** 2026-02-22  
**Status:** In progress

---

## What 4Minitz Was

4Minitz was a collaborative meeting minutes web application. Its core purpose was structured meeting minutes: creating an agenda before a meeting, recording what was discussed and decided during it, assigning action items to specific people with due dates, and distributing the finalized minutes afterward.

It was not a chat bot. It was not AI-powered. It was a disciplined web form with a good data model, email integration, and a real understanding of how meetings generate follow-on work.

It had real users. It had Docker support. It had LDAP integration, which means organizations were running it internally. The GitHub issues include substantive feature discussions and bug reports from people who were depending on it. This was not a toy project.

---

## What Killed It

**The framework.** 4Minitz was built on Meteor.js, a full-stack isomorphic JavaScript framework that peaked in popularity around 2015-2016. By 2020, Meteor had lost the ecosystem momentum battle to React/Next.js, Vue, and server-side frameworks with better cloud-native stories. Maintaining a Meteor application required Meteor-specific knowledge that fewer developers had and fewer wanted to acquire.

**No LLM.** The hardest part of 4Minitz's workflow was manual data entry. Someone had to be the designated note-taker. Someone had to convert the conversation into structured action items. This is exactly the friction that kills adoption — the tool requires effort at the moment people have the least capacity for it (the end of a meeting). Without AI extraction, the tool only works for disciplined teams with dedicated minute-takers.

**Maintainer bandwidth.** The commit history shows a small, dedicated team — likely one or two primary maintainers — who did impressive work but eventually moved on. No new maintainers emerged. Issues accumulated. The last substantive commits trail off in 2022.

---

## What 4Minitz Got Right

### The Data Model

The hierarchy is correct and worth preserving:

```
MeetingSeries
└── Minutes (one per meeting instance)
    └── Topic
        ├── InfoItem (what was discussed / decided)
        └── ActionItem (who does what by when)
            ├── responsible (owner)
            └── duedate
```

This is not an obvious design. Many tools flatten this into a task list with a "meeting" tag. The 4Minitz model preserves the *context* — you can always answer "where did this commitment come from?" by navigating back up the hierarchy. That's the chain of custody that makes Docket valuable.

### The "Finalize" Concept

4Minitz made a distinction between *draft* minutes and *finalized* minutes. Finalized minutes are immutable — they become the official record. This is the right call for Professional Services contexts where the minutes may be referenced in a dispute, a scope change discussion, or a contract amendment. The record needs to be trustworthy.

### Recurring Topics

Topics could be marked to carry forward to the next meeting if unresolved. This is subtle but powerful — it means the tool has memory between meetings, not just within them. An unresolved issue from three meetings ago is still visible. Nothing gets dropped by accident.

### Labels — Many-to-Many, Not an Enum

Topics can carry multiple labels simultaneously — a single ActionItem can be tagged `Decision`, `Proposal`, and `New` at the same time. Labels are not mutually exclusive. This has a direct impact on the Docket schema: the label relationship must be many-to-many, not a single enum field on the ActionItem record. A label table with a junction table to ActionItems is the correct model.

Labels observed in the running system: `Decision`, `Proposal`, `Routine`, `New`, `Status:GREEN`, `Status:RED`. The Status labels are particularly interesting — they appear to be topic-level health indicators separate from the item-level completion status.

### Email Distribution

4Minitz sent the agenda *before* the meeting and the finalized minutes *after*. Both are important. The pre-meeting agenda distribution forces structure before the meeting starts. The post-meeting distribution creates the official record and gets it to people who weren't in the room.

---

## What We're Replacing and Why

| 4Minitz approach | Keeper/Docket approach | Reason |
|---|---|---|
| Manual topic/action item entry | AI extraction from transcript | Removes friction at the hardest moment |
| Web form interface | Teams / Slack bot | Meets people where they already are |
| Email distribution | Direct 1:1 bot messages to owners | More personal, more immediate, trackable |
| No follow-up | Agent-driven reminders until Done | Closes the accountability loop |
| Meteor.js + MongoDB | .NET 9 / Python + SQL | Framework longevity and ecosystem health |
| Single implementation | Two stacks from same spec | Demonstrates tool generalization |
| No external API | Docket REST API | Enables integrations, standalone value |

---

## What We're Keeping (Conceptually)

- The MeetingSeries → Minutes → Topics → ActionItems hierarchy
- The Finalize / immutability concept
- The recurring/carry-forward topic concept
- The label/type system (`#Decision`, `#Action`, `#Risk`, `#Info`)
- The pre/post meeting distribution concept (adapted to bot messaging)
- The genuine respect for the meeting as a formal record-generating event

---

## Running 4Minitz for Reference

4Minitz can be run locally via Docker Compose for reference purposes:

```bash
git clone https://github.com/4minitz/4minitz.git
cd 4minitz
cp settings_sample.json settings.json
docker-compose up
```

The application runs at `http://localhost:3100` by default. Default credentials are in the `settings_sample.json` file.

**Note:** This is for archaeological reference only. The running instance is not connected to any of the Keeper/Docket infrastructure and is not modified. It is observed, not extended.

Screenshots and data model diagrams derived from the running instance will be added to this folder as the archaeology phase completes.

---

## Open Questions from Archaeology

- [x] **How does 4Minitz handle a commitment made about someone who wasn't in the meeting?**  
  Answered by screenshots. 4Minitz allows action items to be assigned to any participant registered in the MeetingSeries, regardless of attendance at a specific meeting instance. The system records non-attendance and flags the item — it does not prevent assignment. Robert Ballard was assigned an action item in a meeting he didn't attend; the system noted his absence and the item carried forward. Docket must support the same behavior: owner presence at a specific meeting is irrelevant to ownership of an action item.

- [ ] What happens to action items when a MeetingSeries is archived or closed?

- [x] **How does the recurring topic carry-forward actually work?**  
  Answered by screenshots. Topics carry forward by reference with new notes appended chronologically. The original action item remains the same record — new detail entries are added beneath it, each dated. It is not a copy. The history of the item is visible in a single view spanning multiple meetings. Docket's ActionItem history endpoint (`GET /items/{id}/history`) must return the full chronological note chain, not just status changes.

- [x] **What is the data model for participants vs. action item owners? Are they the same entity?**  
  Answered by screenshots. Participants are registered users of the system. There are three roles within a MeetingSeries: `Moderator` (meeting chair, owns the series), `Invited` (active participants, expected to attend, can own action items), and `Informed` (receive minutes, not expected to attend or own items). Action item owners are drawn from Invited participants. The Informed role maps to Docket's distribution list — people who receive the record but don't carry accountability.

---

## Screenshot Inventory

All screenshots captured from the live Docker instance on 2026-02-23. Files go in `/docs/00-archaeology/screenshots/`.

| Filename | Content | Key Finding |
|---|---|---|
| `001-0-RegisterAccount.jpg` | Account registration screen | User identity model: username, display name, email |
| `001-1-MainView001.jpg` | Main view — Meetings tab | Top-level: MeetingSeries list with last-minutes date and finalization status |
| `001-1-MainViewActionItems.jpg` | Main view — My Action Items tab | Cross-series personal dashboard; the source for Keeper's morning briefing |
| `002-CreateMeetingSeries.jpg` | Edit Meeting Series dialog | Three participant roles confirmed: Moderator, Invited, Informed |
| `003-CreateMeetingMinutes0202Meeting.jpg` | Draft minutes — first meeting | SEND AGENDA + FINALIZE MINUTES + DELETE MINUTES controls visible |
| `003-CreateMeetingMinutes0202Meeting002.jpg` | Draft minutes with topics expanded | InfoItem vs ActionItem distinction visible; Status:RED on absent participant's topic |
| `003-CreateMeetingMinutes0202MeetingFinalized.jpg` | Finalized minutes | "Version 1. Finalized on [timestamp] by [user]" — versioned, attributed, fields locked |
| `004-FollowupMeeting.jpg` | Second meeting — draft | Carry-forward confirmed: prior action item visible with new notes appended chronologically |
| `004-FollowupMeetingFinalized.jpg` | Second meeting — finalized | "Previous: 2026-02-02" link — meetings are explicitly chained, not just dated |
| `005-TopicDetails.jpg` | Topic detail view | Multi-label confirmed: Decision + Proposal + New on same item; finalized-on reference shown |

---

## Key Findings from Running the System

These are discoveries made by actually running 4Minitz that were not visible from reading the codebase description alone.

**The Informed role is a distribution list, not a participant role.** Informed users receive minutes but have no attendance expectation and cannot own action items. This directly maps to Docket's post-meeting distribution concept — some people need the record without being accountable to it.

**Action items can be assigned to absent participants.** The system does not prevent this. It records the absence and flags it, but the assignment stands. This is correct behavior for Professional Services: "Jeff will handle that" is a commitment regardless of whether Jeff was in the room. Keeper's extraction agent must not filter out commitments about people who weren't present — it must flag them for review.

**The "My Action Items" view crosses meeting series boundaries.** This is the view that makes the tool personally useful rather than just organizationally useful. A user can see everything they've committed to across all their meeting series in one place. Docket needs `GET /owner/{id}/open-items` as a first-class endpoint, not an afterthought.

**Labels are many-to-many.** A single action item carried three labels simultaneously in the screenshots. The Docket schema must use a junction table, not an enum column.

**Minutes are explicitly chained.** The follow-up meeting shows "Previous: 2026-02-02" as a navigable link. This isn't just sorting by date — it's an explicit linked-list structure between meeting instances within a series. The chain of custody runs not just up the hierarchy (Item → Topic → Minutes → Series) but also laterally across time (Minutes[n] → Minutes[n-1]).

**The note that will become a blog moment:** While testing carry-forward behavior, the following was entered as a meeting note in the live system: *"Discussion on open items like assigning actions to users who aren't present (Robert wasn't happy)."* This note — added to investigate an edge case — is now part of the finalized, immutable record of the archaeology session. It will appear in the blog post as the moment the tool demonstrated its own value proposition on the project documenting it.

---

## Archaeology Status

- [x] Identified project and confirmed inactive status
- [x] Analyzed cause of death (framework entropy + no LLM + maintainer bandwidth)
- [x] Documented data model hierarchy
- [x] Identified what to preserve and what to replace
- [x] Clone and run locally via Docker
- [x] Screenshot the UI — 10 screenshots captured
- [x] Answer open questions (3 of 4 resolved from screenshots)
- [ ] Confirm behavior when MeetingSeries is archived or closed
- [ ] Diagram the full data model from source code
- [ ] Extract feature list for `/docs/01-spec/`

---
