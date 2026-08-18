namespace BetterDeaths;

using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;

public sealed class TargetabilityIndexValidationTests
{
    [Fact]
    public void RepeatedSameStateObservationCannotMoveBackwardInPullTime()
    {
        var actor = new ActorId(1);
        NormalizedEvent[] events =
        [
            Event(1, 5, actor, true),
            Event(2, 10, actor, true),
            Event(3, 8, actor, true),
        ];

        var error = Assert.Throws<InvalidOperationException>(() =>
            new TargetabilityIndex(new EventIndex(events), TimeSpan.FromSeconds(20)));

        Assert.Contains("moved backwards", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TargetabilityEvent Event(long sequence, double seconds, ActorId actor, bool isTargetable)
    {
        return new TargetabilityEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(seconds),
            SourceActorId = actor,
            TargetActorId = actor,
            Provenance = new EventProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:targetability-order",
                Fidelity = CaptureFidelity.Exact,
            },
            IsTargetable = isTargetable,
        };
    }
}
