# keeper-web

The web client for Keeper/Docket. A standalone browser-based interface for
reviewing, managing, and acting on meeting commitments recorded in Docket.

---

## What This Is

Keeper captures commitments from meeting transcripts. Docket stores them with
a full audit trail. This client is how you interact with that record outside
of Slack — reviewing what was promised, tracking progress, drilling into history,
and managing the series of meetings that generate the commitments.

It is intentionally separate from the Keeper agent. Keeper is the capture tool.
This is the management tool. They share the Docket API but serve different moments
in the workflow.

---

## Design Principles

These principles were established during the prototype phase and should be
preserved as the client evolves.

**Attention first, then context, then action.**
The landing page answers "what needs me right now?" before showing anything else.
Series, meetings, and items are one, two, and three clicks away respectively.
The goal is two clicks to act on anything.

**Lists over calendars.**
Commitments are sequential obligations, not time-blocked events. A calendar
implies "I'll do it at 2pm." A list implies "this must be done." These are
fundamentally different things. The client is built around lists.

**Collapsed by default.**
Both draft and finalized meetings open with topics collapsed. The chips on each
topic row (open items, carried in) communicate what matters without expanding
everything. You scan first, drill second.

**Density with hierarchy.**
The client shows a lot of information, but visual weight communicates importance.
Overdue items are red. Deferred items are purple. Done items are dimmed.
The color system is the hierarchy — you should be able to read urgency without
reading text.

**Minimum 13px body text, 1.5 line height.**
This is a professional tool used by experienced people, often under time pressure.
Small text and tight spacing are false economy. These minimums are non-negotiable.

**Context-sensitive right-click menus.**
Every meaningful object (series, meeting, topic, action item) has a right-click
context menu with everything you can do to it. Primary actions are also available
inline on hover. Menus are for actions you do less often.

**Monospace for data.**
Dates, IDs, counts, status labels, and metadata use IBM Plex Mono. Prose and
titles use IBM Plex Sans. The distinction is consistent throughout — if it's
data, it's mono.

---

## Page Structure

```
/attention      My Attention — open items assigned to me, ordered by urgency
/series         Series list — all meeting series, accordion to minutes timeline
/minutes/{id}   Meeting detail — topics, items, attendees, carry-forward context
/items/{id}     Item detail — full lineage chain with outlined history timeline
```

### My Attention (`attention.html`)

The landing page. Shows all open action items assigned to the current user,
grouped by urgency:

- **Overdue** — past due date, highlighted in red
- **Due this week** — amber
- **No due date** — grey, lowest visual priority

Each row shows: what was committed, which series it came from, which meeting,
due date, priority, and inline done/defer actions on hover. The summary strip
at the top gives counts at a glance.

### Series List (`series.html`)

All active meeting series in one view, with an Archived tab for completed
engagements. Each series card shows:

- Name and project context
- Open item count (colour-coded by severity)
- Meeting count
- Current status (Draft in progress / Active / Archived)

Clicking a series header expands an accordion showing the minutes timeline —
newest at top, reading down into history. Each minutes row shows date, status,
topic/item chips, and carry-forward indicators.

**Draft minutes** expand to show attendees confirmed and items carrying in.
**Finalized minutes** expand to show the full topic and item record.
**Abandoned minutes** are dimmed in the timeline — present in the record,
not where attention goes.

### Meeting Detail (`minutes.html`)

Opens from a minutes row in the series accordion, or directly from an item's
breadcrumb. Shows the full record for one meeting.

The banner contains: meeting title, series link, date, status badge, stats
(topics / open items / carried in or deferred out), and the attendee strip
with count and individual pills (attended / invited / informed).

Topics are collapsed by default in both draft and finalized views. The chips
on each topic row communicate what's inside without opening it. A draft meeting
shows Finalize and Abandon actions in the banner. A finalized meeting shows a
read-only badge and an immutability notice.

Topic types have distinct visual treatment:
- **Recurring** — purple, carries forward automatically
- **Discussion** — blue, one-time
- **Informational** — green tint, no commitments expected
- **Skipped** — dashed border, dimmed, reason recorded

### Item Detail (`item.html`)

The deepest drill-down. Shows the full story of one commitment across its
entire meeting chain.

The banner contains: item title, responsible person, series, topic, status
badge, and an attributes strip (due date, priority, origin meeting, number
of meetings carried). A lineage chain strip shows every meeting the item
has passed through as clickable date nodes.

Below the banner, a **History** section shows an outlined timeline:

- Each meeting the item appeared in is a **chapter node** (bold, prominent)
- The node header shows: meeting name, date, status badge, item status at
  that point
- Below each node, indented, are:
  - A snapshot line (status/date metadata at that moment)
  - System carry events (purple strips: "carried forward from..." / "deferred to...")
  - Human notes in chronological order within the chapter

The sort order (newest first / oldest first) is toggled by the user and
persisted in localStorage between sessions.

---

## File Layout

```
keeper-web/
  prototypes/           Static HTML prototypes — the design record
    attention.html
    series.html
    minutes.html
    item.html
  src/                  Wired client (to be built)
  README.md
```

The prototypes are static HTML with stub data. They use HTMX (loaded from
CDN) for the interaction model, with JavaScript stubs where Docket API calls
will eventually go. They are the design specification for the wired client —
preserved as-is when the wired version is built.

---

## Technology

- **HTMX** — dynamic interactions without a JavaScript framework
- **IBM Plex Sans / IBM Plex Mono** — typography (Google Fonts)
- **Vanilla JS** — sort toggle, accordion, context menus, localStorage preference
- **Docket REST API** — all data comes from Docket; this client has no
  data layer of its own

No build tools. No node_modules. No framework. The prototypes open directly
in a browser from the filesystem.

---

## What's Next

1. Wire the four pages to the live Docket API — replace stub data with
   fetch calls, replace JavaScript stubs with HTMX attributes
2. Add the Submit Transcript page — paste or upload a transcript, invoke
   Keeper, review what was extracted before writing to Docket
3. User resolution — map Slack display names to Docket user IDs
4. Navigation — link the four pages together so you can follow a commitment
   from My Attention to its full history without using the browser back button
5. Mobile consideration — the layout is desktop-first; a responsive pass
   is planned for v2

---

## Related

- `src/docket-azure/` — the .NET API this client talks to
- `src/keeper-portable/` — the Slack agent that creates the records
- `docs/02-architecture/` — ADRs documenting the design decisions
  behind the data model this UI exposes
