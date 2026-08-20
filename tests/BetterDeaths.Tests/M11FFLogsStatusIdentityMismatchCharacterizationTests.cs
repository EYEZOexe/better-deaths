namespace BetterDeaths;

using BetterDeaths.Analysis.Index;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Sources.FFLogs.Client;
using System.Text.Json;

public sealed class M11FFLogsStatusIdentityMismatchCharacterizationTests
{
    // M12-B sentinel: M12-A must not transform source status identities.
    [Fact]
    public void SourceEncodedDevilmentStatusCannotSatisfyCanonicalExactIdLookup()
    {
        const uint sourceEncodedStatusId = 1_001_825;
        var canonicalStatusId = DancerJobDefinition.Definition
            .Status(DancerJobDefinition.DevilmentStatus)
            .StatusId;
        var normalized = FFLogsEventNormalizer.Normalize(
            Import(
                Event(
                    2_000,
                    "applybuff",
                    """{"sourceID":10,"targetID":10,"abilityGameID":1001825,"duration":20000}""")),
            new PullSchemaVersion(1));

        Assert.Empty(normalized.SkippedEvents);
        var actor = Assert.Single(normalized.Pull.Actors, candidate => candidate.Kind == ActorKind.Player);
        var sourceStatus = Assert.IsType<StatusApplyEvent>(Assert.Single(normalized.Pull.Events));
        Assert.Equal(sourceEncodedStatusId, sourceStatus.StatusId);
        Assert.Equal((uint)1_825, canonicalStatusId);

        var sourceIndex = new StatusIntervalIndex(
            new EventIndex(normalized.Pull.Events),
            normalized.Pull.Metadata.Duration);
        Assert.Single(sourceIndex.ForActorStatus(actor.Id, sourceEncodedStatusId));
        Assert.Empty(sourceIndex.ForActorStatus(actor.Id, canonicalStatusId));

        // The control retains the same actor, timing, provenance, duration, and evidence ID. Only
        // the status identity changes to the canonical Dancer definition expected after M12.
        var canonicalStatus = sourceStatus with { StatusId = canonicalStatusId };
        var canonicalControl = normalized.Pull with { Events = [canonicalStatus] };
        Assert.Equal(sourceStatus with { StatusId = canonicalStatusId }, canonicalStatus);
        Assert.Equal(normalized.Pull.Actors, canonicalControl.Actors);

        var canonicalIndex = new StatusIntervalIndex(
            new EventIndex(canonicalControl.Events),
            canonicalControl.Metadata.Duration);
        Assert.Empty(canonicalIndex.ForActorStatus(actor.Id, sourceEncodedStatusId));
        Assert.Single(canonicalIndex.ForActorStatus(actor.Id, canonicalStatusId));
    }

    private static FFLogsFightImportData Import(params FFLogsEventEnvelope[] events)
    {
        var actors = new FFLogsReportActor[]
        {
            new()
            {
                Id = 10,
                Name = "Synthetic Player",
                Type = "Player",
                SubType = "Dancer",
            },
            new()
            {
                Id = 30,
                Name = "Synthetic Target",
                Type = "NPC",
                SubType = "Boss",
            },
        };
        var fight = new FFLogsFightMetadata
        {
            Id = 3,
            EncounterId = 999,
            Name = "Synthetic Encounter",
            StartTimeMilliseconds = 1_000,
            EndTimeMilliseconds = 60_000,
            GameZoneId = 1_363,
            GameZoneName = "Synthetic Zone",
        };
        var report = new FFLogsReportDocument
        {
            Report = new FFLogsReportMetadata
            {
                Code = "M11BTEST",
                StartTimeUnixMilliseconds = 1_700_000_000_000,
                EndTimeUnixMilliseconds = 1_700_000_060_000,
                Revision = 1,
            },
            Fights = [fight],
            Actors = actors,
        };

        return new FFLogsFightImportData
        {
            ReportDocument = report,
            Fight = fight,
            Events = events,
            Actors = actors,
        };
    }

    private static FFLogsEventEnvelope Event(double timestampMilliseconds, string type, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new FFLogsEventEnvelope
        {
            TimestampMilliseconds = timestampMilliseconds,
            Type = type,
            Payload = document.RootElement.Clone(),
        };
    }
}
