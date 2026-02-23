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

### Labels

Topics could be labeled with types: `#Decision`, `#Action`, `#Info`, `#Risk`. The `#Risk` label in particular is underappreciated — in Professional Services, flagging something as a risk during a meeting and having that flag persist in the record is meaningful for project health tracking.

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

These are questions raised by the 4Minitz codebase that need answers before the Docket spec can be finalized:

- [ ] How does 4Minitz handle a commitment made *about* someone who wasn't in the meeting? (i.e., "Jeff will handle that" said by someone who is not Jeff)
- [ ] What happens to action items when a MeetingSeries is archived or closed?
- [ ] How does the recurring topic carry-forward actually work — is it a copy or a reference?
- [ ] What is the data model for participants vs. action item owners? Are they the same entity?

---

## Archaeology Status

- [x] Identified project and confirmed inactive status
- [x] Analyzed cause of death (framework entropy + no LLM + maintainer bandwidth)
- [x] Documented data model hierarchy
- [x] Identified what to preserve and what to replace
- [ ] Clone and run locally
- [ ] Screenshot the UI for blog post reference
- [ ] Diagram the full data model from source code
- [ ] Answer open questions above
- [ ] Extract feature list for `/docs/01-spec/`

---
