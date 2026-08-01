namespace BetterDeaths;

public sealed class ReplayBoundsInferenceTests
{
    [Fact]
    public void FullPullPositionsDefineStableBoundsWithoutMechanicExtents()
    {
        var positions = new[]
        {
            CreatePosition(80.0f, 80.0f),
            CreatePosition(120.0f, 80.0f),
            CreatePosition(80.0f, 120.0f),
            CreatePosition(120.0f, 120.0f),
        };
        var mechanics = new[]
        {
            CreateMechanic(100.0f, 100.0f, ReplayMechanicShape.Circle, radius: 100.0f),
            CreateMechanic(150.0f, 100.0f, ReplayMechanicShape.Line, length: 100.0f, width: 12.0f),
        };
        var interiorWaymarks = new[]
        {
            CreateWorldMarker(96.0f, 96.0f, 0),
            CreateWorldMarker(104.0f, 96.0f, 1),
            CreateWorldMarker(96.0f, 104.0f, 2),
            CreateWorldMarker(104.0f, 104.0f, 3),
        };

        Assert.True(ReplayBoundsInference.TryInfer(positions, mechanics, interiorWaymarks, out var bounds));

        Assert.Equal(80.0f, bounds.MinX, precision: 2);
        Assert.Equal(120.0f, bounds.MaxX, precision: 2);
        Assert.Equal(80.0f, bounds.MinZ, precision: 2);
        Assert.Equal(120.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void InteriorWaymarksReinforceDefaultCenterWithoutDefiningEdges()
    {
        var waymarks = new[]
        {
            CreateWorldMarker(96.0f, 96.0f, 0),
            CreateWorldMarker(104.0f, 104.0f, 1),
        };

        Assert.True(ReplayBoundsInference.TryInfer([], [], waymarks, out var bounds));

        Assert.Equal(80.0f, bounds.MinX, precision: 2);
        Assert.Equal(120.0f, bounds.MaxX, precision: 2);
        Assert.Equal(80.0f, bounds.MinZ, precision: 2);
        Assert.Equal(120.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void MechanicSizeDoesNotPretendToBeAnArenaEdge()
    {
        var mechanics = new[]
        {
            CreateMechanic(100.0f, 100.0f, ReplayMechanicShape.Circle, radius: 100.0f),
        };

        Assert.True(ReplayBoundsInference.TryInfer([], mechanics, [], out var bounds));

        Assert.Equal(80.0f, bounds.MinX, precision: 2);
        Assert.Equal(120.0f, bounds.MaxX, precision: 2);
        Assert.Equal(80.0f, bounds.MinZ, precision: 2);
        Assert.Equal(120.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void Pull896StyleObservationsUseTheFullStandardArena()
    {
        var positions = new[]
        {
            CreatePosition(80.28f, 80.25f),
            CreatePosition(117.39f, 120.0f),
            CreateEnemyPosition(80.0f, 80.0f, isTargetable: true),
        };

        Assert.True(ReplayBoundsInference.TryInfer(positions, [], [], out var bounds));

        Assert.Equal(80.0f, bounds.MinX, precision: 2);
        Assert.Equal(120.0f, bounds.MaxX, precision: 2);
        Assert.Equal(80.0f, bounds.MinZ, precision: 2);
        Assert.Equal(120.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void NonstandardCoordinatesUseObservedCenter()
    {
        var positions = new[]
        {
            CreatePosition(380.0f, -420.0f),
            CreatePosition(416.0f, -386.0f),
        };

        Assert.True(ReplayBoundsInference.TryInfer(positions, [], [], out var bounds));

        Assert.Equal(378.0f, bounds.MinX, precision: 2);
        Assert.Equal(418.0f, bounds.MaxX, precision: 2);
        Assert.Equal(-423.0f, bounds.MinZ, precision: 2);
        Assert.Equal(-383.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void UntargetableEnemyHelpersDoNotExpandObservedBounds()
    {
        var positions = new[]
        {
            CreatePosition(82.0f, 82.0f),
            CreatePosition(118.0f, 118.0f),
            CreateEnemyPosition(500.0f, 500.0f, isTargetable: false),
        };

        Assert.True(ReplayBoundsInference.TryInfer(positions, [], [], out var bounds));

        Assert.Equal(80.0f, bounds.MinX, precision: 2);
        Assert.Equal(120.0f, bounds.MaxX, precision: 2);
        Assert.Equal(80.0f, bounds.MinZ, precision: 2);
        Assert.Equal(120.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void TargetableBossPositionDoesNotDefineTheArenaWhenPlayersAreAvailable()
    {
        var positions = new[]
        {
            CreatePosition(90.0f, 90.0f),
            CreatePosition(110.0f, 110.0f),
            CreateEnemyPosition(50.0f, 100.0f, isTargetable: true),
        };

        Assert.True(ReplayBoundsInference.TryInfer(positions, [], [], out var bounds));

        Assert.Equal(80.0f, bounds.MinX, precision: 2);
        Assert.Equal(120.0f, bounds.MaxX, precision: 2);
        Assert.Equal(80.0f, bounds.MinZ, precision: 2);
        Assert.Equal(120.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void RemoteTeleportPocketsDoNotExpandTheDominantArena()
    {
        var positions = Enumerable.Range(0, 400)
            .Select(index => CreatePosition(
                84.0f + (index % 33),
                84.0f + ((index * 7) % 33)))
            .Concat(Enumerable.Range(0, 30).Select(index => CreatePosition(
                index % 2 == 0 ? -100.0f : 300.0f,
                index % 3 == 0 ? -92.5f : 307.5f)))
            .ToList();

        Assert.True(ReplayBoundsInference.TryInfer(positions, [], [], out var bounds));

        Assert.Equal(80.0f, bounds.MinX, precision: 2);
        Assert.Equal(120.0f, bounds.MaxX, precision: 2);
        Assert.Equal(80.0f, bounds.MinZ, precision: 2);
        Assert.Equal(120.0f, bounds.MaxZ, precision: 2);
    }

    [Fact]
    public void SustainedEvidenceCanExpandADefaultCenteredArena()
    {
        var positions = Enumerable.Range(0, 100)
            .Select(index => CreatePosition(
                index % 2 == 0 ? 70.0f : 130.0f,
                index % 4 < 2 ? 70.0f : 130.0f))
            .ToList();

        Assert.True(ReplayBoundsInference.TryInfer(positions, [], [], out var bounds));

        Assert.Equal(70.0f, bounds.MinX, precision: 2);
        Assert.Equal(130.0f, bounds.MaxX, precision: 2);
        Assert.Equal(70.0f, bounds.MinZ, precision: 2);
        Assert.Equal(130.0f, bounds.MaxZ, precision: 2);
    }

    private static ReplayPositionSnapshot CreatePosition(float x, float z)
    {
        return new ReplayPositionSnapshot(
            DateTime.UtcNow,
            0.0f,
            $"actor:{x}:{z}",
            "Actor",
            ReplayActorKind.Player,
            0,
            1,
            1,
            "Job",
            x,
            0.0f,
            z,
            0.0f,
            1,
            0,
            1,
            false,
            true);
    }

    private static ReplayPositionSnapshot CreateEnemyPosition(float x, float z, bool isTargetable)
    {
        return new ReplayPositionSnapshot(
            DateTime.UtcNow,
            0.0f,
            $"enemy:{x}:{z}",
            "Enemy",
            ReplayActorKind.Enemy,
            0,
            1,
            0,
            string.Empty,
            x,
            0.0f,
            z,
            0.0f,
            1,
            0,
            1,
            false,
            isTargetable);
    }

    private static ReplayMechanicSnapshot CreateMechanic(
        float x,
        float z,
        ReplayMechanicShape shape,
        float radius = 0.0f,
        float length = 0.0f,
        float width = 0.0f)
    {
        return new ReplayMechanicSnapshot(
            DateTime.UtcNow,
            0.0f,
            1.0f,
            $"mechanic:{x}:{z}:{shape}",
            "Source",
            shape,
            x,
            0.0f,
            z,
            0.0f,
            radius,
            length,
            width,
            0.0f,
            "Mechanic",
            "test",
            1,
            0,
            false);
    }

    private static ReplayWorldMarkerSnapshot CreateWorldMarker(float x, float z, int markerIndex)
    {
        return new ReplayWorldMarkerSnapshot(
            DateTime.UtcNow,
            0.0f,
            markerIndex,
            $"Waymark {markerIndex}",
            true,
            x,
            0.0f,
            z);
    }
}
