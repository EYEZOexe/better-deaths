namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Persistence;

public sealed class CanonicalPullStoreMutationSafetyTests
{
    [Fact]
    public async Task SaveDoesNotWriteDetailWhenExistingIndexSchemaIsUnsupported()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var existing = CreatePull("abababab-abab-abab-abab-abababababab");
        var attempted = CreatePull("bcbcbcbc-bcbc-bcbc-bcbc-bcbcbcbcbcbc");

        await store.SaveAsync(existing);
        await MakeIndexIncompatibleAsync(directory.Path);

        await Assert.ThrowsAsync<CanonicalPullCompatibilityException>(() => store.SaveAsync(attempted));

        Assert.False(File.Exists(GetDetailPath(directory.Path, attempted.Id)));
        Assert.True(File.Exists(GetDetailPath(directory.Path, existing.Id)));
    }

    [Fact]
    public async Task DeleteDoesNotRemoveDetailWhenExistingIndexSchemaIsUnsupported()
    {
        using var directory = new TemporaryDirectory();
        using var store = new FileCanonicalPullStore(directory.Path);
        var existing = CreatePull("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd");

        await store.SaveAsync(existing);
        await MakeIndexIncompatibleAsync(directory.Path);

        await Assert.ThrowsAsync<CanonicalPullCompatibilityException>(() => store.DeleteAsync(existing.Id));

        Assert.True(File.Exists(GetDetailPath(directory.Path, existing.Id)));
    }

    private static async Task MakeIndexIncompatibleAsync(string root)
    {
        var indexPath = Path.Combine(root, "canonical-pulls.index.json");
        var currentJson = await File.ReadAllTextAsync(indexPath);
        var incompatible = currentJson.Replace(
            $"\"SchemaVersion\":{FileCanonicalPullStore.CurrentIndexSchemaVersion}",
            "\"SchemaVersion\":999",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(indexPath, incompatible);
    }

    private static RecordedPull CreatePull(string id)
    {
        var player = new ActorRecord
        {
            Id = new ActorId(1),
            Name = "Player",
            Kind = ActorKind.Player,
        };
        var enemy = new ActorRecord
        {
            Id = new ActorId(2),
            Name = "Boss",
            Kind = ActorKind.Enemy,
        };

        return new RecordedPull
        {
            Id = new PullId(Guid.Parse(id)),
            Metadata = new PullMetadata
            {
                TerritoryId = 123,
                TerritoryName = "Test Duty",
                Duration = TimeSpan.FromSeconds(5),
            },
            SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
            Actors = [player, enemy],
            Events =
            [
                new DamageEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(1),
                    SourceActorId = enemy.Id,
                    TargetActorId = player.Id,
                    Provenance = new EventProvenance
                    {
                        SourceKind = PullDataSourceKind.DalamudLive,
                        Fidelity = CaptureFidelity.Exact,
                    },
                    Amount = 1000,
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }

    private static string GetDetailPath(string root, PullId id)
    {
        return Path.Combine(root, "canonical-pull-details", $"{id.Value:N}.json");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "BetterDeaths.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
