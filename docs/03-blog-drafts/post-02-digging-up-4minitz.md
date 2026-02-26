# Digging Up 4Minitz: What a Dead Codebase Teaches You

*Post 2 of the Keeper/Docket series — reviving dead open source with modern AI tools*

---

Before writing a line of new code, I spent time with the old code. That's the part people skip, and it's usually why rewrites fail — you rebuild the surface without understanding what was actually valuable underneath.

4Minitz is worth understanding. The team who built it weren't just solving a toy problem. They had real users, real feedback on LDAP integration and Docker deployment, and a data model that held up under scrutiny. The fact that it went quiet around 2022 isn't an indictment of the idea — it's what happens when a framework ages out and the maintainers move on.

So I cloned the repo, stood it up in Docker, and spent time actually using it before I touched the design.

## Getting It Running

The setup is straightforward if you have Docker: clone, run `docker-compose up`, navigate to localhost. The stack is Meteor.js on the backend with MongoDB, and Meteor requires its own runtime — Docker is the practical path unless you want to install the Meteor toolchain separately.

Once it's running, you create an account, create a Meeting Series, and start working. The UI is dated but functional. And within about ten minutes of actual use, you start to see what the team got right.

## What the Screenshots Revealed

I captured 16 screenshots across a full workflow — creating a series, scheduling meetings, adding topics and action items, finalizing minutes, and following up in the next meeting. Here's what stood out.

**The participant model is richer than it looks.** When you create a Meeting Series, you assign participants in three roles: Moderator, Invited, and Informed. Invited participants are active — they attend, they get assigned action items, their presence is tracked. Informed participants receive the finalized minutes but aren't expected to participate or own items. That distinction matters: it's the difference between someone who's accountable and someone who just needs to know. Docket needed all three roles.

**You can assign action items to people who weren't in the room.** This one took a deliberate test to confirm. I created an action item owned by Robert Ballard (a placeholder participant) and then deliberately didn't mark him as present at the meeting. The system allowed it, and the minutes reflected his absence with a note: "Could not discuss this because Robert was not present." The assignment stood. This is the right behavior — in Professional Services, decisions about someone's work happen all the time without that person in the meeting — and Keeper's extraction logic needed to handle it the same way.

**Finalization creates an immutable, versioned record.** The finalized minutes header reads: "Version 1. Finalized on [date] by [name]." The form fields grey out. You can still view and print, but you can't edit. This isn't just a UI affordance — it's a chain of custody. The record says who closed it, when, and what was known at that moment. That's the model Docket's `Minutes.Finalize()` method enforces.

**The carry-forward mechanism is visible in the UI.** Open action items from a prior meeting appear in the follow-up meeting with teal/cyan highlighting to distinguish them from new items. Completed items show with a grey background and strikethrough. Your eye goes straight to what's unresolved. That visual language is a design decision worth preserving conceptually — even if the implementation is different.

**The finalization email decouples notification from closure.** The finalization dialog has two independent toggles: "Send Action Items To Responsibles" and "Send Meeting Minutes." Both default to on, but they're separate. You can finalize without triggering emails, or notify without finalizing. Docket doesn't send email directly — that's Keeper's job — but the decoupling principle is right. The API accepts a notification flag on finalization rather than hardwiring the two together.

**Labels are many-to-many, not a single field.** A single action item can carry multiple labels simultaneously: Decision, Proposal, and New all on the same item. This isn't a minor detail — it changes the database schema. A single enum column on ActionItem would be wrong. The correct model is a junction table. Catching this from screenshots rather than assumptions saved a migration later.

## What the Codebase Got Right

The data model reflects real thinking about the problem. The core hierarchy — Meeting Series → Minutes → Topics → Items — maps cleanly to how meetings actually work. A series is an ongoing engagement. A minutes record is a single meeting instance, covering both the pre-meeting plan and the post-meeting record. Topics organize the agenda. Items within topics are either informational (what was discussed or decided) or actionable (who does what by when).

That distinction between Info Items and Action Items is subtle but important. Not everything discussed in a meeting creates an obligation. Some things are just context — decisions made, things reported, background shared. Collapsing those into a single item type loses information. 4Minitz kept them separate, and Docket does too.

The "finalize minutes" workflow is also correct. Minutes aren't just a note — they're a legal record of what happened and what was promised. They should be editable until formally closed, and immutable after. The version stamp and the finalizer attribution make the record defensible. In a Professional Services context, that matters.

## What It Got Wrong (Or Left Unfinished)

**No archive state for a Meeting Series.** A series just... keeps going. There's no way to formally retire an engagement while preserving its history. In practice, old series accumulate. Docket adds an explicit `archived` status on `MeetingSeries` so old engagements can be retired cleanly — and archived series trigger abandonment of any remaining open action items, which is itself a meaningful signal.

**No pre-creation gate on new Minutes.** Nothing stops you from creating Minutes for a new meeting while a prior draft is still sitting unresolved. In 4Minitz this is a minor friction point. In an agentic system it's a correctness problem — Keeper needs to know the chain is clean before it can reason about it. Docket blocks creation of new Minutes if any prior draft in the chain is unresolved, and requires explicit abandonment (with a mandatory note) before proceeding.

**No cross-series view.** The "My Action Items" dashboard shows open items across all series for the logged-in user, which is useful. But there's no concept of an engagement that spans multiple series — no way to say "these five recurring meetings are all part of the same client project." That's a harder problem and the right call for v1 is to defer it, but it needs to be a conscious deferral with a migration path, not an accident. [ADR-005](https://github.com/jeff-griffith-dev/keeper-docket/blob/main/docs/02-architecture/ADR-005-meetingseries-scope.md) in the repo covers exactly that decision.

## What This Fed Into the Design

The archaeology produced ten concrete schema decisions that went directly into Docket's domain model: see [docs/00-archaeology](https://github.com/jeff-griffith-dev/keeper-docket/blob/main/docs/00-archaeology/README.md)

The participant role model became `SeriesParticipant` with a `ParticipantRole` enum covering Moderator, Invited, and Informed. Attendance tracking became a first-class `MinutesAttendee` entity — separate from the participant list — so the system can derive `assignedInAbsentia` mechanically rather than requiring manual input. Labels became a proper junction table. The linked-list chain between Minutes records became an explicit `PreviousMinutesId` foreign key rather than date ordering, because meetings get cancelled, rescheduled, and abandoned and you can't rely on dates being contiguous.

The most useful single artifact from the archaeology wasn't a code pattern — it was a note I added to a test action item while investigating whether 4Minitz handles absent participants: "Discussion on open items like assigning actions to users who aren't present (Robert wasn't happy)." I'd created a fictional absent participant named Robert Ballard, assigned him an action item, and then documented his fictional frustration about it — inside the tool being archaeologized, while investigating the exact behavior in question. Sometimes the test data tells the story better than the analysis does.

## The Before and After

Here's the concrete before-and-after from the archaeology phase:

**What 4Minitz got right that Docket preserves:** The Series → Minutes → Topic → Item hierarchy. The Moderator/Invited/Informed participant roles. The Info Item vs. Action Item distinction. The finalization workflow with version stamping and immutability. The carry-forward mechanism for recurring items. The per-item owner and due date.

**What Docket adds that 4Minitz didn't have:** A formal `archived` status on MeetingSeries. A pre-creation gate that enforces chain consistency. An explicit `abandoned` terminal state on both Minutes and ActionItems, with required notes. A `NotePhase` field that keeps pre-finalization and post-finalization notes structurally distinct. A standalone REST API designed to be consumed by agents and external tools, not just a web UI.

**What Keeper adds that neither had:** The extraction layer — reading a transcript and turning it into structured commitments without anyone typing. The follow-up loop — reminders until items are closed. The cross-meeting intelligence — eventually, the ability to reason about commitments across series and recognize when the same engagement appears under multiple meeting names.

That last part is still ahead. The next post covers the architecture decisions — why two stacks, why the Keeper/Docket split, and what it means for the competitive landscape when your main alternative is Microsoft Copilot.

---

*Next: [Post 3 — One Spec, Two Stacks: The Architecture Decisions Behind Keeper/Docket]*
