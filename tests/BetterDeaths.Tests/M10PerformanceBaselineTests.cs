namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;
using System.Diagnostics;
using System.Text;

public sealed class M10PerformanceBaselineTests
{
    private const int LongPullEventCount = 50_000;
    private const int LongPullPositionCount = 4_000;
    private static readonly TimeSpan LongPullDuration = TimeSpan.FromMinutes(20);

    [Fact]
    public void FullPullRecorderAppendsAndFinalizesUltimateScaleFactsWithinGenerousBudget()
    {
        var recorder = new FullPullRecorder();
        var actor = new ActorId(1);
        var provenance = Provenance("perf:recorder");
        recorder.Begin(new PullStartContext
        {
            PullId = PullId(1),
            Metadata = Metadata("Recorder Performance"),
            SchemaVersion = new PullSchemaVersion(1),
            Provenance = PullProvenance("perf:recorder"),
            DutyActive = true,
        });
        recorder.MarkCombatObserved();
        recorder.RegisterActor(Player(actor));

        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < LongPullEventCount; index++)
        {
            recorder.Append(Action(index + 1L, actor, provenance));
        }

        var finalized = recorder.TryFinalize(new PullEndContext(LongPullDuration), out var pull);
        stopwatch.Stop();

        Assert.True(finalized);
        Assert.NotNull(pull);
        Assert.Equal(LongPullEventCount, pull.Events.Count);
        Assert.Equal(LongPullDuration, pull.Metadata.Duration);
        Assert.False(recorder.IsActive);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Appending/finalizing {LongPullEventCount:N0} canonical events took {stopwatch.Elapsed.TotalMilliseconds:F1}ms; expected under generous 5s regression budget.");
    }

    [Fact]
    public void CanonicalSerializerRoundTripsTwentyMinuteUltimateScalePullWithinGenerousBudget()
    {
        var pull = LongPull(2, LongPullEventCount, LongPullPositionCount);

        var stopwatch = Stopwatch.StartNew();
        var json = CanonicalPullSerializer.Serialize(pull);
        var restored = CanonicalPullSerializer.Deserialize(json);
        stopwatch.Stop();

        var bytes = Encoding.UTF8.GetByteCount(json);
        Assert.Equal(pull.Id, restored.Id);
        Assert.Equal(LongPullEventCount, restored.Events.Count);
        Assert.Equal(LongPullPositionCount, restored.Positions.Count);
        Assert.Equal(LongPullDuration, restored.Metadata.Duration);
        Assert.InRange(bytes, 1, 100 * 1024 * 1024);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Serializing/deserializing {LongPullEventCount:N0} events + {LongPullPositionCount:N0} positions ({bytes / 1024d / 1024d:F1} MiB JSON) took {stopwatch.Elapsed.TotalMilliseconds:F1}ms; expected under generous 10s regression budget.");
    }

    [Fact]
    public async Task CurrentDefaultAnalyzerCompositionProcessesLongPullWithinGenerousBudget()
    {
        var pull = LongPull(3, LongPullEventCount, positionCount: 0);
        var engine = AnalyzerWorkspaceEngineComposition.CreateDefault();

        var stopwatch = Stopwatch.StartNew();
        var run = await engine.AnalyzeAsync(pull);
        stopwatch.Stop();

        Assert.Empty(run.Failures);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Default analyzer composition processed {LongPullEventCount:N0} canonical events in {stopwatch.Elapsed.TotalMilliseconds:F1}ms; expected under generous 10s regression budget.");
    }

    [Fact]
    public async Task FileCanonicalPullStoreSaveLoadAndQueryRemainUsableAtMultiPullVolume()
    {
        const int pullCount = 20;
        const int eventsPerPull = 1_000;
        var root = Path.Combine(Path.GetTempPath(), $"better-deaths-m10-perf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            using var store = new FileCanonicalPullStore(root);
            var pulls = Enumerable.Range(0, pullCount)
                .Select(index => LongPull(100 + index, eventsPerPull, positionCount: 0))
                .ToArray();

            var stopwatch = Stopwatch.StartNew();
            foreach (var pull in pulls)
            {
                await store.SaveAsync(pull);
            }

            var summaries = await store.QueryAsync(new PullQuery { TerritoryId = 777, Limit = pullCount });
            foreach (var summary in summaries)
            {
                var restored = await store.LoadAsync(summary.Id);
                Assert.NotNull(restored);
                Assert.Equal(eventsPerPull, restored.Events.Count);
            }
            stopwatch.Stop();

            Assert.Equal(pullCount, summaries.Count);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(20),
                $"Saving, querying and reloading {pullCount} pulls × {eventsPerPull:N0} events took {stopwatch.Elapsed.TotalMilliseconds:F1}ms; expected under generous 20s regression budget.");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static RecordedPull LongPull(int identity, int eventCount, int positionCount)
    {
        var actor = new ActorId(1);
        var provenance = Provenance($"perf:{identity}");
        var events = Enumerable.Range(0, eventCount)
            .Select(index => (NormalizedEvent)Action(index + 1L, actor, provenance))
            .ToArray();
        var positions = Enumerable.Range(0, positionCount)
            .Select(index => new PositionSample
            {
                Sequence = eventCount + index + 1L,
                PullTime = TimeSpan.FromTicks(LongPullDuration.Ticks * index / Math.Max(1, positionCount)),
                ActorId = actor,
                X = 100.0f + index % 10,
                Y = 0.0f,
                Z = 100.0f + index % 7,
                Rotation = index % 360,
                Provenance = provenance,
            })
            .ToArray();

        return new RecordedPull
        {
            Id = PullId(identity),
            Metadata = Metadata("M10 Performance Fixture"),
            SchemaVersion = new PullSchemaVersion(1),
            Actors = [Player(actor)],
            Events = events,
            Positions = positions,
            Provenance = PullProvenance($"perf:{identity}"),
        };
    }

    private static ActionUseEvent Action(long sequence, ActorId actor, EventProvenance provenance) => new()
    {
        Id = new EventId(sequence),
        Sequence = sequence,
        PullTime = TimeSpan.FromTicks(LongPullDuration.Ticks * (sequence - 1) / LongPullEventCount),
        SourceActorId = actor,
        Provenance = provenance,
        ActionId = (uint)(10_000 + sequence % 32),
    };

    private static PullMetadata Metadata(string name) => new()
    {
        TerritoryId = 777,
        TerritoryName = name,
        Duration = LongPullDuration,
        StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero),
    };

    private static ActorRecord Player(ActorId id) => new()
    {
        Id = id,
        Name = "Performance Player",
        Kind = ActorKind.Player,
        JobAbbreviation = "BRD",
    };

    private static EventProvenance Provenance(string reference) => new()
    {
        SourceKind = PullDataSourceKind.DalamudLive,
        SourceReference = reference,
        Fidelity = CaptureFidelity.Exact,
        Confidence = 1.0f,
    };

    private static PullProvenance PullProvenance(string reference) => new()
    {
        SourceKind = PullDataSourceKind.DalamudLive,
        SourceReference = reference,
        Fidelity = CaptureFidelity.Exact,
        Confidence = 1.0f,
    };

    private static PullId PullId(int identity) =>
        new(Guid.Parse($"70000000-0000-0000-0000-{identity:D12}"));
}
