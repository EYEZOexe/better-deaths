namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Exports;
using BetterDeaths.Persistence;
using System.Text.Json.Nodes;

public sealed class M10CanonicalExportEnvelopeTests
{
    [Theory]
    [InlineData(0, "canonical")]
    [InlineData(1, "anonymized")]
    public void ExportPayloadCarriesExplicitPolicyVersionAndMode(
        int modeValue,
        string expectedMode)
    {
        var mode = (CanonicalPullExportMode)modeValue;
        var export = CanonicalPullExporter.Export(new CanonicalPullExportRequest
        {
            Pull = MinimalPull(),
            Options = new CanonicalPullExportOptions { Mode = mode },
        });

        var root = JsonNode.Parse(export.Payload)!.AsObject();

        Assert.Equal(
            CanonicalPullExporter.CurrentExportPolicyVersion,
            root["ExportPolicyVersion"]!.GetValue<int>());
        Assert.Equal(expectedMode, root["ExportMode"]!.GetValue<string>());
        Assert.Equal(export.ExportedPullId, CanonicalPullSerializer.Deserialize(export.Payload).Id);
    }

    private static RecordedPull MinimalPull() => new()
    {
        Id = new PullId(Guid.Parse("99999999-8888-7777-6666-555555555555")),
        Metadata = new PullMetadata
        {
            TerritoryId = 777,
            TerritoryName = "M10 Export Envelope",
            Duration = TimeSpan.FromSeconds(30),
            StartedAt = new DateTimeOffset(2026, 8, 19, 18, 0, 0, TimeSpan.Zero),
        },
        SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
        Actors =
        [
            new ActorRecord
            {
                Id = new ActorId(1),
                Name = "Player Identity",
                Kind = ActorKind.Player,
            },
        ],
        Events = Array.Empty<NormalizedEvent>(),
        Provenance = new PullProvenance
        {
            SourceKind = PullDataSourceKind.DalamudLive,
            SourceReference = "local:99999999888877776666555555555555",
            Fidelity = CaptureFidelity.Exact,
            Confidence = 1.0f,
        },
    };
}
