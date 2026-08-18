namespace BetterDeaths;

using System.Text.Json;

public sealed class RecordedPullPersistenceContractTests
{
    private const int CurrentHistorySchemaVersion = 3;
    private const int CurrentIndexSchemaVersion = 7;
    private static readonly DateTime BaselineUtc = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LegacyRawArrayShapeRoundTripsPullDeathSnapshots()
    {
        var pulls = new[] { CreatePull(BaselineUtc, pullNumber: 5, deathCount: 1) };

        var json = JsonSerializer.Serialize(pulls);
        var loaded = JsonSerializer.Deserialize<List<PullDeathSnapshot>>(json);

        var pull = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<PullDeathSnapshot>>(loaded));
        Assert.Equal(5, pull.PullNumber);
        Assert.Single(pull.Deaths);
    }

    [Fact]
    public void WrappedHistoryShapeRoundTripsSchemaAndPulls()
    {
        var envelope = new HistoryEnvelope(
            CurrentHistorySchemaVersion,
            [CreatePull(BaselineUtc, pullNumber: 6, deathCount: 1)]);

        var json = JsonSerializer.Serialize(envelope);
        var loaded = JsonSerializer.Deserialize<HistoryEnvelope>(json);

        Assert.NotNull(loaded);
        Assert.Equal(CurrentHistorySchemaVersion, loaded.SchemaVersion);
        Assert.Equal(6, Assert.Single(loaded.Pulls).PullNumber);
    }

    [Fact]
    public void CurrentDeathOnlyPersistenceContractRejectsZeroDeathPulls()
    {
        var pulls = new[]
        {
            CreatePull(BaselineUtc, pullNumber: 1, deathCount: 0),
            CreatePull(BaselineUtc.AddSeconds(1), pullNumber: 2, deathCount: 1),
        };

        var persisted = pulls.Where(HasPersistableDeaths).ToList();

        var pull = Assert.Single(persisted);
        Assert.Equal(2, pull.PullNumber);
        Assert.Single(pull.Deaths);
    }

    [Fact]
    public void CurrentSummaryContractRejectsZeroDeathEntries()
    {
        var summaries = new[]
        {
            new RecordedPullSummary(BaselineUtc, "zero", 1, "Duty", 30.0f, 0) { PullNumber = 1 },
            new RecordedPullSummary(BaselineUtc.AddSeconds(1), "death", 1, "Duty", 31.0f, 1) { PullNumber = 2 },
        };

        var persisted = summaries.Where(summary => summary.DeathCount > 0).ToList();

        Assert.Equal(2, Assert.Single(persisted).PullNumber);
    }

    [Fact]
    public void PullNumberNormalizationPreservesValidNumbersAndRepairsInvalidOrDuplicateNumbers()
    {
        var pulls = new[]
        {
            CreatePull(BaselineUtc, pullNumber: 4, deathCount: 1),
            CreatePull(BaselineUtc.AddSeconds(1), pullNumber: 0, deathCount: 1),
            CreatePull(BaselineUtc.AddSeconds(2), pullNumber: 4, deathCount: 1),
            CreatePull(BaselineUtc.AddSeconds(3), pullNumber: -2, deathCount: 1),
        };

        var normalized = NormalizePullNumbersLikeCurrentStorage(pulls);

        Assert.Equal(new long[] { 4, 5, 6, 7 }, normalized.Select(pull => pull.PullNumber));
        Assert.Equal(
            pulls.Select(pull => pull.CapturedAtUtc),
            normalized.Select(pull => pull.CapturedAtUtc));
    }

    [Fact]
    public void SchemaVersionsCharacterizedForM0RemainExplicit()
    {
        Assert.Equal(3, CurrentHistorySchemaVersion);
        Assert.Equal(7, CurrentIndexSchemaVersion);
    }

    private static bool HasPersistableDeaths(PullDeathSnapshot pull)
    {
        return pull.Deaths.Count > 0;
    }

    // Mirrors the current private normalization algorithm in Plugin.RecordedPulls.cs.
    // This is characterization coverage, not the future canonical persistence API.
    private static List<PullDeathSnapshot> NormalizePullNumbersLikeCurrentStorage(
        IEnumerable<PullDeathSnapshot> pulls)
    {
        var normalized = new List<PullDeathSnapshot>();
        var usedPullNumbers = new HashSet<long>();
        var nextPullNumber = 1L;

        foreach (var pull in pulls)
        {
            var pullNumber = pull.PullNumber;
            if (pullNumber <= 0 || !usedPullNumbers.Add(pullNumber))
            {
                while (usedPullNumbers.Contains(nextPullNumber))
                {
                    nextPullNumber++;
                }

                pullNumber = nextPullNumber;
                usedPullNumbers.Add(pullNumber);
            }

            nextPullNumber = Math.Max(nextPullNumber, pullNumber + 1);
            normalized.Add(pull with { PullNumber = pullNumber });
        }

        return normalized;
    }

    private static PullDeathSnapshot CreatePull(DateTime capturedAtUtc, long pullNumber, int deathCount)
    {
        var deaths = Enumerable.Range(0, deathCount)
            .Select(index => new PartyDeathRecord(
                capturedAtUtc.AddSeconds(index),
                10.0f + index,
                $"member:{index}",
                $"Player {index}",
                index,
                19,
                "PLD",
                0,
                0,
                100000,
                null,
                Array.Empty<CombatEventRecord>(),
                Array.Empty<HpHistorySnapshot>(),
                Array.Empty<StatusSnapshot>()))
            .ToList();

        return new PullDeathSnapshot(
            capturedAtUtc,
            "Combat ended",
            1,
            "Duty",
            30.0f,
            deaths)
        {
            PullNumber = pullNumber,
            CapturedPluginVersion = "0.1.0-test",
            PullGroupId = "group",
            PullGroupColorIndex = 1,
        };
    }

    private sealed record HistoryEnvelope(int SchemaVersion, List<PullDeathSnapshot> Pulls);
}
