using Docket.Domain.Entities;
using Docket.Domain.Enums;
using Docket.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Docket.Tests.Domain;

/// <summary>
/// Unit tests for Minutes and ActionItem state machine transitions.
/// These test domain logic directly — no HTTP, no database, no DI.
/// </summary>

// ---------------------------------------------------------------------------
// Minutes state machine
// ---------------------------------------------------------------------------

public class MinutesStateMachineTests
{
    // --- Finalize ---

    [Fact]
    public void Finalize_FromDraft_SetsStatusAndTimestamp()
    {
        var minutes = MakeMinutes();
        var actor = Guid.NewGuid();

        minutes.Finalize(actor);

        minutes.Status.Should().Be(MinutesStatus.Finalized);
        minutes.FinalizedAt.Should().NotBeNull();
        minutes.FinalizedBy.Should().Be(actor);
        minutes.Version.Should().Be(1);
    }

    [Fact]
    public void Finalize_AlreadyFinalized_ThrowsInvalidStatusTransition()
    {
        var minutes = MakeMinutes();
        minutes.Finalize(Guid.NewGuid());

        var act = () => minutes.Finalize(Guid.NewGuid());

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Finalize_Abandoned_ThrowsMinutesAbandoned()
    {
        var minutes = MakeMinutes();
        minutes.Abandon(Guid.NewGuid(), "test reason");

        var act = () => minutes.Finalize(Guid.NewGuid());

        act.Should().Throw<MinutesAbandonedException>();
    }

    // --- Abandon ---

    [Fact]
    public void Abandon_FromDraft_SetsStatusAndNote()
    {
        var minutes = MakeMinutes();
        var actor = Guid.NewGuid();

        minutes.Abandon(actor, "Meeting cancelled");

        minutes.Status.Should().Be(MinutesStatus.Abandoned);
        minutes.AbandonedAt.Should().NotBeNull();
        minutes.AbandonedBy.Should().Be(actor);
        minutes.AbandonmentNote.Should().Be("Meeting cancelled");
    }

    [Fact]
    public void Abandon_EmptyNote_ThrowsAbandonmentNoteRequired()
    {
        var minutes = MakeMinutes();

        var act = () => minutes.Abandon(Guid.NewGuid(), "   ");

        act.Should().Throw<AbandonmentNoteRequiredException>();
    }

    [Fact]
    public void Abandon_NullNote_ThrowsAbandonmentNoteRequired()
    {
        var minutes = MakeMinutes();

        var act = () => minutes.Abandon(Guid.NewGuid(), null!);

        act.Should().Throw<AbandonmentNoteRequiredException>();
    }

    [Fact]
    public void Abandon_AlreadyFinalized_ThrowsMinutesFinalized()
    {
        var minutes = MakeMinutes();
        minutes.Finalize(Guid.NewGuid());

        var act = () => minutes.Abandon(Guid.NewGuid(), "oops");

        act.Should().Throw<MinutesFinalizedException>();
    }

    [Fact]
    public void Abandon_AlreadyAbandoned_ThrowsMinutesAbandoned()
    {
        var minutes = MakeMinutes();
        minutes.Abandon(Guid.NewGuid(), "first reason");

        var act = () => minutes.Abandon(Guid.NewGuid(), "second reason");

        act.Should().Throw<MinutesAbandonedException>();
    }

    // --- EnsureEditable ---

    [Fact]
    public void EnsureEditable_Draft_DoesNotThrow()
    {
        var minutes = MakeMinutes();

        var act = () => minutes.EnsureEditable();

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureEditable_Finalized_ThrowsMinutesFinalized()
    {
        var minutes = MakeMinutes();
        minutes.Finalize(Guid.NewGuid());

        var act = () => minutes.EnsureEditable();

        act.Should().Throw<MinutesFinalizedException>();
    }

    [Fact]
    public void EnsureEditable_Abandoned_ThrowsMinutesAbandoned()
    {
        var minutes = MakeMinutes();
        minutes.Abandon(Guid.NewGuid(), "gone");

        var act = () => minutes.EnsureEditable();

        act.Should().Throw<MinutesAbandonedException>();
    }

    // --- IsEditable ---

    [Fact]
    public void IsEditable_Draft_IsTrue()
    {
        MakeMinutes().IsEditable.Should().BeTrue();
    }

    [Fact]
    public void IsEditable_Finalized_IsFalse()
    {
        var minutes = MakeMinutes();
        minutes.Finalize(Guid.NewGuid());
        minutes.IsEditable.Should().BeFalse();
    }

    [Fact]
    public void IsEditable_Abandoned_IsFalse()
    {
        var minutes = MakeMinutes();
        minutes.Abandon(Guid.NewGuid(), "gone");
        minutes.IsEditable.Should().BeFalse();
    }

    // --- Helper ---
    // Minutes.Status is private-set and starts as Draft by default,
    // so we construct a fresh instance and let the state machine drive state.

    private static Minutes MakeMinutes() => new()
    {
        Id = Guid.NewGuid(),
        SeriesId = Guid.NewGuid(),
        ScheduledFor = DateTimeOffset.UtcNow.AddDays(1)
    };
}

// ---------------------------------------------------------------------------
// ActionItem state machine
// ---------------------------------------------------------------------------

public class ActionItemStateMachineTests
{
    // --- MarkDone ---

    [Fact]
    public void MarkDone_FromOpen_SetsStatusToDone()
    {
        var item = MakeActionItem();

        item.MarkDone();

        item.Status.Should().Be(ActionItemStatus.Done);
    }

    [Fact]
    public void MarkDone_FromDeferred_ThrowsInvalidTransition()
    {
        // Deferred → Done is intentionally blocked: force Deferred → Open → Done
        // to require conscious thought rather than a casual tick-box.
        var item = MakeActionItem();
        item.Defer();

        var act = () => item.MarkDone();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkDone_AlreadyDone_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();
        item.MarkDone();

        var act = () => item.MarkDone();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void MarkDone_Abandoned_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();
        item.AbandonBySystem();

        var act = () => item.MarkDone();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    // --- Defer ---

    [Fact]
    public void Defer_FromOpen_SetsStatusToDeferred()
    {
        var item = MakeActionItem();

        item.Defer();

        item.Status.Should().Be(ActionItemStatus.Deferred);
    }

    [Fact]
    public void Defer_FromDeferred_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();
        item.Defer();

        var act = () => item.Defer();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Defer_Done_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();
        item.MarkDone();

        var act = () => item.Defer();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Defer_Abandoned_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();
        item.AbandonBySystem();

        var act = () => item.Defer();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    // --- Reopen ---

    [Fact]
    public void Reopen_FromDeferred_SetsStatusToOpen()
    {
        var item = MakeActionItem();
        item.Defer();

        item.Reopen();

        item.Status.Should().Be(ActionItemStatus.Open);
    }

    [Fact]
    public void Reopen_FromOpen_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();

        var act = () => item.Reopen();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Reopen_FromDone_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();
        item.MarkDone();

        var act = () => item.Reopen();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Reopen_Abandoned_ThrowsInvalidTransition()
    {
        var item = MakeActionItem();
        item.AbandonBySystem();

        var act = () => item.Reopen();

        act.Should().Throw<InvalidStatusTransitionException>();
    }

    // --- AbandonBySystem ---

    [Fact]
    public void AbandonBySystem_FromOpen_SetsAbandoned()
    {
        var item = MakeActionItem();

        item.AbandonBySystem();

        item.Status.Should().Be(ActionItemStatus.Abandoned);
    }

    [Fact]
    public void AbandonBySystem_FromDeferred_SetsAbandoned()
    {
        var item = MakeActionItem();
        item.Defer();

        item.AbandonBySystem();

        item.Status.Should().Be(ActionItemStatus.Abandoned);
    }

    [Fact]
    public void AbandonBySystem_AlreadyDone_IsIdempotent()
    {
        // AbandonBySystem returns silently for terminal states
        var item = MakeActionItem();
        item.MarkDone();

        item.AbandonBySystem();

        item.Status.Should().Be(ActionItemStatus.Done);
    }

    [Fact]
    public void AbandonBySystem_AlreadyAbandoned_IsIdempotent()
    {
        var item = MakeActionItem();
        item.AbandonBySystem();

        item.AbandonBySystem();

        item.Status.Should().Be(ActionItemStatus.Abandoned);
    }

    // --- MarkDeferredByCarryForward ---

    [Fact]
    public void MarkDeferredByCarryForward_FromOpen_SetsDeferred()
    {
        var item = MakeActionItem();

        item.MarkDeferredByCarryForward();

        item.Status.Should().Be(ActionItemStatus.Deferred);
    }

    [Fact]
    public void MarkDeferredByCarryForward_AlreadyDeferred_IsIdempotent()
    {
        var item = MakeActionItem();
        item.Defer();

        item.MarkDeferredByCarryForward();

        item.Status.Should().Be(ActionItemStatus.Deferred);
    }

    [Fact]
    public void MarkDeferredByCarryForward_Done_IsIdempotent()
    {
        var item = MakeActionItem();
        item.MarkDone();

        item.MarkDeferredByCarryForward();

        item.Status.Should().Be(ActionItemStatus.Done);
    }

    [Fact]
    public void MarkDeferredByCarryForward_Abandoned_IsIdempotent()
    {
        var item = MakeActionItem();
        item.AbandonBySystem();

        item.MarkDeferredByCarryForward();

        item.Status.Should().Be(ActionItemStatus.Abandoned);
    }

    // --- IsTerminal ---

    [Fact]
    public void IsTerminal_Open_IsFalse()
    {
        MakeActionItem().IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_Deferred_IsFalse()
    {
        var item = MakeActionItem();
        item.Defer();
        item.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_Done_IsTrue()
    {
        var item = MakeActionItem();
        item.MarkDone();
        item.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_Abandoned_IsTrue()
    {
        var item = MakeActionItem();
        item.AbandonBySystem();
        item.IsTerminal.Should().BeTrue();
    }

    // --- Helper ---

    private static ActionItem MakeActionItem() => new()
    {
        Id = Guid.NewGuid(),
        TopicId = Guid.NewGuid(),
        Title = "Test action item",
        ResponsibleId = Guid.NewGuid(),
        CreatedBy = Guid.NewGuid()
    };
}

// ---------------------------------------------------------------------------
// ActionItemNote phase assignment
// ---------------------------------------------------------------------------

public class ActionItemNotePhaseTests
{
    [Fact]
    public void NotePhase_Meeting_IsPreserved()
    {
        var note = MakeNote(NotePhase.Meeting);

        note.Phase.Should().Be(NotePhase.Meeting);
    }

    [Fact]
    public void NotePhase_PostMeeting_IsPreserved()
    {
        var note = MakeNote(NotePhase.PostMeeting);

        note.Phase.Should().Be(NotePhase.PostMeeting);
    }

    [Fact]
    public void NoteText_IsStored()
    {
        var note = MakeNote(NotePhase.Meeting);

        note.Text.Should().Be("Some note text");
    }

    private static ActionItemNote MakeNote(NotePhase phase) => new()
    {
        Id = Guid.NewGuid(),
        ActionItemId = Guid.NewGuid(),
        AuthorId = Guid.NewGuid(),
        Text = "Some note text",
        Phase = phase
    };
}
