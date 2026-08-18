namespace BetterDeaths;

using System.Text.Json;

public sealed class RecordedPullReplayPersistenceTests
{
    [Fact]
    public void PullDeathSnapshotRoundTripPreservesRepresentativeReplayPayload()
    {
        var capturedAt = new DateTime(2026, 8, 18, 12, 34, 56, DateTimeKind.Utc);
        var status = new StatusSnapshot(
            1234,
            "Test Debuff",
            5678,
            0xABCDEF01,
            2,
            7.5f);

        var snapshot = new PullDeathSnapshot(
            capturedAt,
            "Combat ended",
            987,
            "Test Territory",
            321.5f,
            Array.Empty<PartyDeathRecord>())
        {
            PullNumber = 42,
            CapturedPluginVersion = "0.1.0-test",
            PullGroupId = "group-id",
            PullGroupColorIndex = 3,
            ReplayPositions =
            [
                new ReplayPositionSnapshot(
                    capturedAt.AddSeconds(-4),
                    317.5f,
                    "member:1",
                    "Player One",
                    ReplayActorKind.Player,
                    0,
                    0x10000001,
                    38,
                    "DRG",
                    101.25f,
                    0.5f,
                    -52.75f,
                    1.25f,
                    123456,
                    7890,
                    130000,
                    false,
                    true)
                {
                    SampleSource = ReplayPositionSampleSource.ActionEffectTarget,
                },
            ],
            ReplayMarkers =
            [
                new ReplayMarkerSnapshot(
                    capturedAt.AddSeconds(-3),
                    318.5f,
                    "member:1",
                    "Player One",
                    ReplayActorKind.Player,
                    0,
                    0x10000001,
                    38,
                    "DRG",
                    17,
                    0x00110022)
                {
                    RemainingTime = 4.25f,
                },
            ],
            ReplayMechanics =
            [
                new ReplayMechanicSnapshot(
                    capturedAt.AddSeconds(-2),
                    319.5f,
                    6.0f,
                    "enemy:1",
                    "Boss",
                    ReplayMechanicShape.Tower,
                    100.0f,
                    0.0f,
                    -50.0f,
                    0.75f,
                    5.0f,
                    10.0f,
                    3.0f,
                    90.0f,
                    "Tower",
                    "ActorControl",
                    0x1234,
                    0x5678,
                    true),
            ],
            ReplayWorldMarkers =
            [
                new ReplayWorldMarkerSnapshot(
                    capturedAt.AddSeconds(-1.5),
                    320.0f,
                    2,
                    "C",
                    true,
                    95.0f,
                    0.0f,
                    -45.0f),
            ],
            ReplayMitigations =
            [
                new ReplayMitigationSnapshot(
                    capturedAt.AddSeconds(-1),
                    320.5f,
                    "member:1",
                    "Player One",
                    0,
                    38,
                    "DRG",
                    7549,
                    "Test Mitigation",
                    12345,
                    PossibleMitigationScope.Party,
                    15.0f,
                    [status]),
            ],
            ReplayDebuffs =
            [
                new ReplayDebuffSnapshot(
                    capturedAt.AddSeconds(-0.5),
                    321.0f,
                    "member:1",
                    "Player One",
                    0,
                    38,
                    "DRG",
                    status,
                    true),
            ],
            ReplayDebuffsCaptured = true,
        };

        var json = JsonSerializer.Serialize(snapshot);
        var roundTripped = JsonSerializer.Deserialize<PullDeathSnapshot>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(snapshot.PullNumber, roundTripped.PullNumber);
        Assert.Equal(snapshot.CapturedPluginVersion, roundTripped.CapturedPluginVersion);
        Assert.Equal(snapshot.PullGroupId, roundTripped.PullGroupId);
        Assert.Equal(snapshot.PullGroupColorIndex, roundTripped.PullGroupColorIndex);
        Assert.True(roundTripped.ReplayDebuffsCaptured);

        var position = Assert.Single(roundTripped.ReplayPositions);
        Assert.Equal("member:1", position.ActorKey);
        Assert.Equal(317.5f, position.PullElapsedSeconds);
        Assert.Equal(0x10000001u, position.EntityId);
        Assert.Equal(38u, position.ClassJobId);
        Assert.Equal(101.25f, position.X);
        Assert.Equal(-52.75f, position.Z);
        Assert.Equal(ReplayPositionSampleSource.ActionEffectTarget, position.SampleSource);
        Assert.True(position.IsTargetable);

        var marker = Assert.Single(roundTripped.ReplayMarkers);
        Assert.Equal(17u, marker.MarkerId);
        Assert.Equal(0x00110022u, marker.RawMarkerId);
        Assert.Equal(4.25f, marker.RemainingTime);

        var mechanic = Assert.Single(roundTripped.ReplayMechanics);
        Assert.Equal(ReplayMechanicShape.Tower, mechanic.Shape);
        Assert.Equal("Tower", mechanic.Label);
        Assert.Equal(0x1234u, mechanic.RawEventId);
        Assert.Equal(0x5678u, mechanic.RawState);
        Assert.True(mechanic.IsKnown);

        var worldMarker = Assert.Single(roundTripped.ReplayWorldMarkers);
        Assert.Equal(2, worldMarker.MarkerIndex);
        Assert.Equal("C", worldMarker.Label);
        Assert.True(worldMarker.Active);

        var mitigation = Assert.Single(roundTripped.ReplayMitigations);
        Assert.Equal(7549u, mitigation.ActionId);
        Assert.Equal(PossibleMitigationScope.Party, mitigation.Scope);
        Assert.Equal(15.0f, mitigation.DurationSeconds);
        Assert.Equal(status, Assert.Single(mitigation.Statuses));

        var debuff = Assert.Single(roundTripped.ReplayDebuffs);
        Assert.Equal(status, debuff.Status);
        Assert.True(debuff.Active);
    }
}
