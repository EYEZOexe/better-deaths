namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;

public sealed class GenericIntervalIndexTests
{
    [Fact]
    public void TargetabilityCoverageKeepsPreEvidenceTimeUnknown()
    {
        var actor = new ActorId(1);
        var index = Targetability(
            TimeSpan.FromSeconds(40),
            TargetabilityEvent(1, 10, actor, isTargetable: false),
            TargetabilityEvent(2, 20, actor, isTargetable: true));

        var intervals = index.ForActor(actor);
        Assert.Equal(2, intervals.Count);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)), Range(intervals[0]));
        Assert.False(intervals[0].IsTargetable);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40)), Range(intervals[1]));
        Assert.True(intervals[1].IsTargetable);

        var coverage = index.GetCoverage(actor);
        Assert.Equal(TimeSpan.FromSeconds(20), coverage.TargetableDuration);
        Assert.Equal(TimeSpan.FromSeconds(10), coverage.UntargetableDuration);
        Assert.Equal(TimeSpan.FromSeconds(10), coverage.UnknownDuration);
        Assert.Equal(0.75, coverage.KnownFraction, 6);
        Assert.Equal(new[] { new EventId(1), new EventId(2) }, coverage.EvidenceEventIds);
    }

    [Fact]
    public void TargetabilityRepeatedStateDoesNotCreateFakeTransition()
    {
        var actor = new ActorId(1);
        var index = Targetability(
            TimeSpan.FromSeconds(30),
            TargetabilityEvent(1, 5, actor, isTargetable: true),
            TargetabilityEvent(2, 10, actor, isTargetable: true),
            TargetabilityEvent(3, 20, actor, isTargetable: false));

        var intervals = index.ForActor(actor);
        Assert.Equal(2, intervals.Count);
        Assert.Equal(new[] { new EventId(1), new EventId(2) }, intervals[0].EvidenceEventIds);
        Assert.Equal(TimeSpan.FromSeconds(15), intervals[0].Duration);
        Assert.Equal(new[] { new EventId(3) }, intervals[1].EvidenceEventIds);
    }

    [Fact]
    public void TargetabilityCoverageClipsToRequestedWindow()
    {
        var actor = new ActorId(1);
        var index = Targetability(
            TimeSpan.FromSeconds(40),
            TargetabilityEvent(1, 10, actor, isTargetable: true),
            TargetabilityEvent(2, 20, actor, isTargetable: false),
            TargetabilityEvent(3, 30, actor, isTargetable: true));

        var coverage = index.GetCoverage(
            actor,
            new TimeRange(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(25)));

        Assert.Equal(TimeSpan.FromSeconds(5), coverage.TargetableDuration);
        Assert.Equal(TimeSpan.FromSeconds(5), coverage.UntargetableDuration);
        Assert.Equal(TimeSpan.Zero, coverage.UnknownDuration);
    }

    [Fact]
    public void TargetabilityEventWithoutActorIsRejected()
    {
        var evt = new TargetabilityEvent
        {
            Id = new EventId(1),
            Sequence = 1,
            PullTime = TimeSpan.FromSeconds(1),
            Provenance = Provenance(),
            IsTargetable = true,
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            new TargetabilityIndex(new EventIndex([evt]), TimeSpan.FromSeconds(10)));

        Assert.Contains("does not reference an actor", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDurationCreatesKnownBoundedInterval()
    {
        var actor = new ActorId(1);
        var apply = StatusApply(1, 5, actor, statusId: 100, durationSeconds: 10);
        var index = Statuses(TimeSpan.FromSeconds(30), apply);
        var key = new StatusIntervalKey(actor, 100, null);

        var interval = Assert.Single(index.ForKey(key));
        Assert.Equal(TimeSpan.FromSeconds(5), interval.Start);
        Assert.Equal(TimeSpan.FromSeconds(15), interval.End);
        Assert.Equal(StatusIntervalEndReason.DurationExpired, interval.EndReason);
        Assert.True(interval.CoverageKnownThroughEnd);
        Assert.Equal(new[] { apply.Id }, interval.EvidenceEventIds);
        Assert.Equal(TimeSpan.FromSeconds(10), index.GetKnownActiveDuration(key));
    }

    [Fact]
    public void StatusWithoutDurationOrRemovalKeepsEndUncertain()
    {
        var actor = new ActorId(1);
        var apply = StatusApply(1, 5, actor, statusId: 100, durationSeconds: null);
        var index = Statuses(TimeSpan.FromSeconds(30), apply);
        var key = new StatusIntervalKey(actor, 100, null);

        var interval = Assert.Single(index.ForKey(key));
        Assert.Equal(TimeSpan.FromSeconds(30), interval.End);
        Assert.Equal(StatusIntervalEndReason.PullEndedWithUnknownStatusEnd, interval.EndReason);
        Assert.False(interval.CoverageKnownThroughEnd);
        Assert.Equal(TimeSpan.Zero, index.GetKnownActiveDuration(key));
    }

    [Fact]
    public void KnownStatusDurationBeyondPullProvesCoverageThroughPullEnd()
    {
        var actor = new ActorId(1);
        var index = Statuses(
            TimeSpan.FromSeconds(20),
            StatusApply(1, 5, actor, statusId: 100, durationSeconds: 30));
        var key = new StatusIntervalKey(actor, 100, null);

        var interval = Assert.Single(index.ForKey(key));
        Assert.Equal(TimeSpan.FromSeconds(20), interval.End);
        Assert.Equal(StatusIntervalEndReason.PullEndedBeforeKnownExpiry, interval.EndReason);
        Assert.True(interval.CoverageKnownThroughEnd);
        Assert.Equal(TimeSpan.FromSeconds(15), index.GetKnownActiveDuration(key));
    }

    [Fact]
    public void StatusReapplyClosesOldIntervalAndStartsNewOne()
    {
        var actor = new ActorId(1);
        var first = StatusApply(1, 5, actor, statusId: 100, durationSeconds: 20);
        var second = StatusApply(2, 10, actor, statusId: 100, durationSeconds: 5);
        var index = Statuses(TimeSpan.FromSeconds(30), first, second);
        var key = new StatusIntervalKey(actor, 100, null);

        var intervals = index.ForKey(key);
        Assert.Equal(2, intervals.Count);
        Assert.Equal(StatusIntervalEndReason.Reapplied, intervals[0].EndReason);
        Assert.Equal(new[] { first.Id, second.Id }, intervals[0].EvidenceEventIds);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)), Range(intervals[0]));
        Assert.Equal(StatusIntervalEndReason.DurationExpired, intervals[1].EndReason);
        Assert.Equal(new TimeRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15)), Range(intervals[1]));
    }

    [Fact]
    public void StatusRemovalClosesIntervalWithBothEvidenceEvents()
    {
        var actor = new ActorId(1);
        var source = new ActorId(2);
        var apply = StatusApply(1, 5, actor, statusId: 100, durationSeconds: null, source);
        var remove = StatusRemove(2, 12, actor, statusId: 100, source);
        var index = Statuses(TimeSpan.FromSeconds(30), apply, remove);
        var key = new StatusIntervalKey(actor, 100, source);

        var interval = Assert.Single(index.ForKey(key));
        Assert.Equal(StatusIntervalEndReason.Removed, interval.EndReason);
        Assert.True(interval.CoverageKnownThroughEnd);
        Assert.Equal(new[] { apply.Id, remove.Id }, interval.EvidenceEventIds);
        Assert.Equal(TimeSpan.FromSeconds(7), index.GetKnownActiveDuration(key));
    }

    [Fact]
    public void StatusIntervalsRemainDistinctBySourceActor()
    {
        var actor = new ActorId(1);
        var sourceA = new ActorId(2);
        var sourceB = new ActorId(3);
        var index = Statuses(
            TimeSpan.FromSeconds(30),
            StatusApply(1, 5, actor, 100, 10, sourceA),
            StatusApply(2, 6, actor, 100, 10, sourceB));

        Assert.Single(index.ForKey(new StatusIntervalKey(actor, 100, sourceA)));
        Assert.Single(index.ForKey(new StatusIntervalKey(actor, 100, sourceB)));
        Assert.Equal(2, index.ForActorStatus(actor, 100).Count);
    }

    [Fact]
    public void UnmatchedStatusRemovalIsRecordedRatherThanClosingAnotherSource()
    {
        var actor = new ActorId(1);
        var sourceA = new ActorId(2);
        var sourceB = new ActorId(3);
        var apply = StatusApply(1, 5, actor, 100, 10, sourceA);
        var unrelatedRemove = StatusRemove(2, 7, actor, 100, sourceB);
        var index = Statuses(TimeSpan.FromSeconds(30), apply, unrelatedRemove);

        Assert.Equal(new[] { unrelatedRemove.Id }, index.UnmatchedRemovalEventIds);
        Assert.Single(index.ForKey(new StatusIntervalKey(actor, 100, sourceA)));
    }

    [Fact]
    public async Task AnalyzerEngineSharesOneIntervalIndexInstanceAcrossModules()
    {
        TargetabilityIndex? firstTargetability = null;
        StatusIntervalIndex? firstStatuses = null;
        TargetabilityIndex? secondTargetability = null;
        StatusIntervalIndex? secondStatuses = null;
        var registry = new AnalyzerRegistry();
        registry.Register(new ContextCaptureModule("generic.capture-a", context =>
        {
            firstTargetability = context.Targetability;
            firstStatuses = context.Statuses;
        }));
        registry.Register(new ContextCaptureModule("generic.capture-b", context =>
        {
            secondTargetability = context.Targetability;
            secondStatuses = context.Statuses;
        }));
        var pull = Pull(
            TimeSpan.FromSeconds(20),
            TargetabilityEvent(1, 0, new ActorId(2), true),
            StatusApply(2, 2, new ActorId(1), 100, 5));

        var run = await new AnalyzerEngine(registry).AnalyzeAsync(pull);

        Assert.Empty(run.Failures);
        Assert.Same(firstTargetability, secondTargetability);
        Assert.Same(firstStatuses, secondStatuses);
        Assert.NotNull(firstTargetability);
        Assert.NotNull(firstStatuses);
    }

    private static TargetabilityIndex Targetability(TimeSpan duration, params NormalizedEvent[] events)
    {
        return new TargetabilityIndex(new EventIndex(events), duration);
    }

    private static StatusIntervalIndex Statuses(TimeSpan duration, params NormalizedEvent[] events)
    {
        return new StatusIntervalIndex(new EventIndex(events), duration);
    }

    private static TargetabilityEvent TargetabilityEvent(
        long sequence,
        double seconds,
        ActorId actor,
        bool isTargetable)
    {
        return new TargetabilityEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = actor,
            TargetActorId = actor,
            Provenance = Provenance(),
            IsTargetable = isTargetable,
        };
    }

    private static StatusApplyEvent StatusApply(
        long sequence,
        double seconds,
        ActorId actor,
        uint statusId,
        double? durationSeconds,
        ActorId? source = null)
    {
        return new StatusApplyEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = actor,
            Provenance = Provenance(),
            StatusId = statusId,
            Duration = durationSeconds is { } duration ? TimeSpan.FromSeconds(duration) : null,
        };
    }

    private static StatusRemoveEvent StatusRemove(
        long sequence,
        double seconds,
        ActorId actor,
        uint statusId,
        ActorId? source = null)
    {
        return new StatusRemoveEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = source,
            TargetActorId = actor,
            Provenance = Provenance(),
            StatusId = statusId,
        };
    }

    private static RecordedPull Pull(TimeSpan duration, params NormalizedEvent[] events)
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Interval Test",
                Duration = duration,
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors =
            [
                new ActorRecord { Id = new ActorId(1), Name = "Player", Kind = ActorKind.Player },
                new ActorRecord { Id = new ActorId(2), Name = "Boss", Kind = ActorKind.Enemy },
            ],
            Events = events,
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:intervals",
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static EventProvenance Provenance()
    {
        return new EventProvenance
        {
            SourceKind = PullDataSourceKind.ImportedFile,
            SourceReference = "test:intervals",
            Fidelity = CaptureFidelity.Exact,
        };
    }

    private static TimeRange Range(TargetabilityInterval interval)
    {
        return new TimeRange(interval.Start, interval.End);
    }

    private static TimeRange Range(StatusInterval interval)
    {
        return new TimeRange(interval.Start, interval.End);
    }

    private sealed class ContextCaptureModule(string id, Action<AnalyzerContext> capture) : IAnalyzerModule
    {
        public string Id => id;

        public AnalyzerScope Scope => AnalyzerScope.Generic;

        public IReadOnlyCollection<string> Dependencies => Array.Empty<string>();

        public bool Supports(AnalyzerContext context)
        {
            return true;
        }

        public ValueTask AnalyzeAsync(
            AnalyzerContext context,
            IAnalysisResultSink results,
            CancellationToken cancellationToken)
        {
            capture(context);
            return ValueTask.CompletedTask;
        }
    }
}
