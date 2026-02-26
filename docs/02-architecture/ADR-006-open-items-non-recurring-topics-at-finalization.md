# ADR-006: Behavior of Open Action Items on Non-Recurring Topics at Finalization

**Status**: Accpeted Option A - Keeper will need to handle the exception  
**Date**: 2026-02-26  
**Context**: Docket Azure stack, `MinutesEndpoints.CreateMinutes`, carry-forward implementation

---

## Context

During the development of carry-forward workflow tests, a gap was identified in the
finalization behavior of Minutes that contain open action items on non-recurring topics.

The carry-forward logic in `CreateMinutes` explicitly handles one case:

> Open action items on **Recurring** topics in the most recent finalized Minutes
> are carried forward into the new Minutes. The original item is marked `Deferred`
> (system-set), and a new `Open` item is created pointing back via `SourceActionItemId`.

This leaves a second case unspecified:

> What happens to open action items on **non-Recurring** topics when Minutes are
> finalized?

Currently, finalization is not gated on item status. Minutes can be finalized with
open action items on non-recurring topics, and those items are silently left in `Open`
status with no forward path. They do not carry forward, they are not automatically
abandoned, and no validation error is raised.

---

## The Design Principle at Stake

Docket's core commitment is that **everything has an endpoint**. No action item should
silently wither. The full set of valid terminal states is:

| Status | Meaning | Set by |
|---|---|---|
| `Completed` | Resolved successfully | Human |
| `Abandoned` | Explicitly closed without resolution | System (cascade) |
| `Deferred` | Unresolved, carried forward to next meeting | Human or System |

An item that is `Open` in finalized Minutes satisfies none of these. It is in a
permanent limbo — the transcript is closed and immutable, but the item has no
resolution and no forward path.

This violates the design principle and would undermine Keeper's ability to track
commitments reliably.

---

## Options

### Option A — Gate finalization on non-recurring open items

Prevent finalization if any non-recurring topic has open action items. The group
must explicitly resolve each item (Complete, Abandon, or Defer) before the transcript
can be closed.

**Pros:**
- Enforces the design principle strictly
- No silent gaps in the record
- Consistent with the meeting philosophy: a transcript is only closeable when
  the group has discharged its obligations to the agenda

**Cons:**
- More friction at finalization time
- Requires UI support to surface unresolved items clearly
- A group that genuinely cannot resolve an item must abandon it explicitly,
  which requires a note — this may feel bureaucratic for minor items

---

### Option B — Auto-abandon open items on non-recurring topics at finalization

When Minutes are finalized, any open action items on non-recurring topics are
automatically marked `Abandoned` with a system-generated note such as
*"Auto-abandoned at finalization — not discussed."*

**Pros:**
- No friction at finalization
- Every item reaches a terminal state
- Consistent with the series archive behavior (which already auto-abandons)

**Cons:**
- Silent abandonment may obscure real accountability gaps
- A system-generated abandonment note is less meaningful than a human one
- Could mask items that were genuinely forgotten rather than deliberately skipped

---

### Option C — Auto-defer open items on non-recurring topics at finalization

Treat non-recurring open items the same as recurring ones — carry them forward
automatically, converting the topic type to Recurring in the process, or placing
the items in a catch-all "Carried Forward" topic.

**Pros:**
- Nothing is lost
- Consistent behavior across all open items regardless of topic type

**Cons:**
- Contradicts the meaning of non-recurring — a Discussion topic was intended
  to be one-time
- Could pollute future meetings with stale items that were never meant to recur
- The original topic type distinction loses its meaning

---

### Option D — Require explicit human decision per item (hybrid of A and B)

At finalization, surface any open items on non-recurring topics and require
the user to choose: Complete, Abandon (with note), or Defer (which converts
the topic to Recurring and carries it forward).

**Pros:**
- Maximum intentionality — every item has a human decision
- Preserves the distinction between recurring and non-recurring topics
- Aligns most closely with the meeting philosophy

**Cons:**
- Highest friction
- Most UI work to implement well
- May be overkill for minor items

---

## Recommendation

**Option A** for the API layer, **Option D** as the UI experience built on top of it.

The API should gate finalization — returning `409 Conflict` if non-recurring open
items exist — and expose a clear error response that identifies the blocking items.
The UI (and Keeper) can then present the human decision required before finalization
can proceed.

This keeps the API honest and the responsibility for resolution explicit, while
allowing the UI to make the experience smooth.

---

## Current Behavior (as of this ADR)

Finalization succeeds regardless of open item status on non-recurring topics.
Open items on non-recurring topics in finalized Minutes are left in `Open` status
with no forward path. This is a known gap.

The carry-forward tests in `CarryForwardWorkflowTests.cs` include:

```csharp
[Fact]
public async Task CarryForward_NonRecurringTopic_DoesNotCarryForward()
```

This test currently passes because non-recurring topics are excluded from
carry-forward. It does **not** test what happens to the open items left behind —
that behavior is currently untested and undefined.

---

## Work Required to Implement Recommendation

1. Add a finalization gate in `FinalizeMinutes` endpoint:
   - Query for open action items on non-recurring topics in the Minutes
   - If any exist, throw a new exception (e.g. `UnresolvedNonRecurringItemsException`)
   - Map this exception to `409 Conflict` in `ExceptionHandlingMiddleware`
   - Include the blocking item IDs in the error response

2. Add a contract test:
   - `FinalizeMinutes_WithOpenNonRecurringItems_Returns409`

3. Add a workflow test:
   - `Finalize_WithMixedTopics_OnlyBlockedByNonRecurringOpenItems`

4. Update the OpenAPI spec to document the 409 response on `POST /minutes/{minutesId}/finalize`

---

## Related

- `MinutesEndpoints.cs` — `CreateMinutes` carry-forward logic
- `MinutesEndpoints.cs` — `FinalizeMinutes` (currently no item status gate)
- `CarryForwardWorkflowTests.cs` — carry-forward test suite
- Series archive cascade (auto-abandons all open items) — `SeriesEndpoints.cs`
- Minutes abandon cascade (auto-abandons all open items) — `MinutesEndpoints.cs`
