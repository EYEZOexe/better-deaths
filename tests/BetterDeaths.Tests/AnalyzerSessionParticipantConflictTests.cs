namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Sessions;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;

public sealed class AnalyzerSessionParticipantConflictTests
{
    [Fact]
    public async Task ConflictingParticipantMetadataProducesDiagnosticWithoutDestroyingSession()
    {
        var pulls = new[] { Pull(1), Pull(2) };
        var controller = new AnalyzerSessionDataController(
            new Store(pulls),
            new AnalyzerEngine(new AnalyzerRegistry()),
            new ConflictingParticipantEnricher());

        var loaded = await controller.LoadAsync(new AnalyzerSessionRequest { TerritoryId = 777 });

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Session.Pulls.Count);
        var participant = Assert.Single(loaded.Session.Participants);
        Assert.Equal("First", participant.DisplayName);
        Assert.Contains(loaded.Diagnostics, diagnostic =>
            diagnostic.Kind == AnalyzerSessionDiagnosticKind.PullEnrichmentFailure &&
            diagnostic.Message.Contains("conflicting metadata", StringComparison.Ordinal));
    }

    private static RecordedPull Pull(int index) => new()
    {
        Id = new PullId(Guid.Parse($"40000000-0000-0000-0000-{index:D12}")),
        Metadata = new PullMetadata
        {
            TerritoryId = 777,
            TerritoryName = "Session Conflict Test",
            Duration = TimeSpan.FromSeconds(90),
            StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero).AddMinutes(index),
        },
        SchemaVersion = new PullSchemaVersion(1),
        Actors = [],
        Events = [],
        Provenance = new PullProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = $"test:participant-conflict:{index}",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        },
    };

    private sealed class ConflictingParticipantEnricher : IAnalyzerSessionPullEnricher
    {
        private int call;

        public AnalyzerSessionPullEnrichment Enrich(RecordedPull pull, AnalyzerRunResult run)
        {
            call++;
            return new AnalyzerSessionPullEnrichment
            {
                Participants =
                [
                    new SessionParticipant
                    {
                        Key = new SessionParticipantKey("same-player"),
                        DisplayName = call == 1 ? "First" : "Conflicting",
                    },
                ],
            };
        }
    }

    private sealed class Store(IEnumerable<RecordedPull> pulls) : IPullStore
    {
        private readonly Dictionary<PullId, RecordedPull> byId = pulls.ToDictionary(pull => pull.Id);

        public Task SaveAsync(RecordedPull pull, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<RecordedPull?>(byId[id]);

        public Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PullSummary> summaries = byId.Values
                .OrderBy(pull => pull.Metadata.StartedAt)
                .Select(pull => new PullSummary
                {
                    Id = pull.Id,
                    TerritoryId = pull.Metadata.TerritoryId,
                    TerritoryName = pull.Metadata.TerritoryName,
                    Duration = pull.Metadata.Duration,
                    StartedAt = pull.Metadata.StartedAt,
                    ActorCount = pull.Actors.Count,
                    EventCount = pull.Events.Count,
                    SourceKind = pull.Provenance.SourceKind,
                })
                .ToArray();
            return Task.FromResult(summaries);
        }

        public Task DeleteAsync(PullId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
