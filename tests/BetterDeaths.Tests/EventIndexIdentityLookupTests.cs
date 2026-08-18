namespace BetterDeaths;

using BetterDeaths.Analysis.Index;
using BetterDeaths.Domain;

public sealed class EventIndexIdentityLookupTests
{
    [Fact]
    public void EventIndexProvidesStableIdLookupWithoutStreamRescan()
    {
        var first = Event(1);
        var second = Event(2);
        var index = new EventIndex([first, second]);

        Assert.True(index.TryGet(first.Id, out var found));
        Assert.Same(first, found);
        Assert.Same(second, index.GetRequired(second.Id));
        Assert.False(index.TryGet(new EventId(999), out _));
        Assert.Throws<KeyNotFoundException>(() => index.GetRequired(new EventId(999)));
    }

    private static ActionUseEvent Event(long sequence)
    {
        return new ActionUseEvent
        {
            Id = new EventId(sequence),
            Sequence = sequence,
            PullTime = TimeSpan.FromSeconds(sequence),
            Provenance = new EventProvenance
            {
                SourceKind = PullDataSourceKind.ImportedFile,
                SourceReference = "test:event-index-id",
                Fidelity = CaptureFidelity.Exact,
            },
            ActionId = 100,
        };
    }
}
