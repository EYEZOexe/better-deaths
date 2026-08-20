namespace BetterDeaths;

using BetterDeaths.Analysis.Encounters.DancingMadUltimate;
using BetterDeaths.Analysis.Engine;
using BetterDeaths.Analysis.Jobs.Dancer;
using BetterDeaths.Domain;
using BetterDeaths.Exports;
using BetterDeaths.Persistence;
using BetterDeaths.Windows.Analyzer;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed partial class M11GoldenPullFixtureTests
{
    private const string FixtureFileName = "canonical-pull-fight3.anonymized.json";
    private const string FixtureSha256 = "D370AE57ECA46CFE97863D7F1E44D6E254A349DA74F228AC6838C7D7B80BB5FA";

    [Fact]
    public void FixtureIsDeterministicApprovedAnonymizedExport()
    {
        var payload = ReadFixturePayload();
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var pull = CanonicalPullSerializer.Deserialize(payload);
        var reexport = CanonicalPullExporter.Export(new CanonicalPullExportRequest
        {
            Pull = pull,
            Options = new CanonicalPullExportOptions { Mode = CanonicalPullExportMode.Anonymized },
        });

        Assert.Equal(FixtureSha256, payloadHash);
        Assert.Equal(payload, reexport.Payload);
        Assert.Equal(1, root.GetProperty("ExportPolicyVersion").GetInt32());
        Assert.Equal("anonymized", root.GetProperty("ExportMode").GetString());
        Assert.Null(pull.Metadata.StartedAt);
        Assert.Null(pull.Provenance.SourceReference);
        Assert.All(pull.Events, evt =>
        {
            Assert.Null(evt.ObservedAt);
            Assert.Null(evt.Provenance.SourceReference);
        });
        Assert.Empty(pull.Positions);
        Assert.Empty(pull.WorldMarkers);

        Assert.All(
            pull.Actors.Where(actor => actor.Kind == ActorKind.Player),
            actor => Assert.Matches(PlayerLabel(), actor.Name));
        Assert.All(
            pull.Actors.Where(actor => actor.Kind == ActorKind.Pet),
            actor => Assert.Matches(PetLabel(), actor.Name));
        Assert.All(
            pull.Actors.Where(actor => actor.OwnerActorId is not null),
            actor => Assert.Contains(pull.Actors, owner => owner.Id == actor.OwnerActorId));

        Assert.DoesNotContain("fflogs:report:", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("www.fflogs.com/reports", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FixturePreservesGoldenSemanticOrderingAndEvidenceTruths()
    {
        var pull = LoadFixture();

        Assert.Equal((uint)1_363, pull.Metadata.TerritoryId);
        Assert.Equal("Sigmascape V4.0", pull.Metadata.TerritoryName);
        Assert.Equal(TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(40), pull.Metadata.Duration);
        Assert.Equal(48, pull.Actors.Count);
        var actorIds = pull.Actors.Select(actor => actor.Id).ToHashSet();
        Assert.Equal(pull.Actors.Count, actorIds.Count);
        Assert.All(pull.Events, evt =>
        {
            if (evt.SourceActorId is { } sourceActorId)
            {
                Assert.Contains(sourceActorId, actorIds);
            }

            if (evt.TargetActorId is { } targetActorId)
            {
                Assert.Contains(targetActorId, actorIds);
            }
        });

        var primaryPartyJobs = new[]
        {
            "Dancer",
            "Gunbreaker",
            "Monk",
            "Paladin",
            "Pictomancer",
            "Sage",
            "Viper",
            "WhiteMage",
        };
        var primaryParty = pull.Actors
            .Where(actor => primaryPartyJobs.Contains(actor.JobAbbreviation, StringComparer.Ordinal))
            .ToArray();
        Assert.Equal(8, primaryParty.Length);
        Assert.Equal(primaryPartyJobs, primaryParty.Select(actor => actor.JobAbbreviation).OrderBy(job => job, StringComparer.Ordinal));
        Assert.Single(primaryParty, actor => actor.JobAbbreviation == "Dancer");
        Assert.Single(pull.Actors, actor => actor.Kind == ActorKind.Player && actor.JobAbbreviation == "LimitBreak");

        Assert.Equal(14_912, pull.Events.Count);
        Assert.Equal(Enumerable.Range(1, pull.Events.Count).Select(value => (long)value), pull.Events.Select(evt => evt.Sequence));
        Assert.Equal(Enumerable.Range(1, pull.Events.Count).Select(value => (long)value), pull.Events.Select(evt => evt.Id.Value));
        Assert.True(pull.Events.Zip(pull.Events.Skip(1), (left, right) => left.PullTime <= right.PullTime).All(ordered => ordered));
        AssertEventCount<DamageEvent>(pull, 5_177);
        AssertEventCount<HealEvent>(pull, 2_107);
        AssertEventCount<ActionUseEvent>(pull, 3_148);
        AssertEventCount<CastStartEvent>(pull, 351);
        AssertEventCount<StatusApplyEvent>(pull, 1_914);
        AssertEventCount<StatusRemoveEvent>(pull, 2_200);
        AssertEventCount<DeathEvent>(pull, 11);
        AssertEventCount<TargetabilityEvent>(pull, 4);

        var dancer = Assert.Single(primaryParty, actor => actor.JobAbbreviation == "Dancer");
        var dancerActionIds = pull.Events
            .Where(evt => evt.SourceActorId == dancer.Id)
            .Select(ActionId)
            .OfType<uint>()
            .ToHashSet();
        Assert.Contains((uint)15_997, dancerActionIds);
        Assert.Contains((uint)15_998, dancerActionIds);
        Assert.Contains((uint)16_011, dancerActionIds);
        Assert.Contains((uint)16_013, dancerActionIds);

        var statusIds = pull.Events.Select(StatusId).OfType<uint>().ToArray();
        Assert.Equal(16, statusIds.Count(statusId => statusId == 1_001_825));
        Assert.DoesNotContain((uint)1_825, statusIds);
        Assert.Equal((uint)1_825, DancerJobDefinition.Definition.Status(DancerJobDefinition.DevilmentStatus).StatusId);
        Assert.Contains((uint)1_005_084, statusIds);
        Assert.Contains((uint)1_005_085, statusIds);
        Assert.Contains((uint)1_005_086, statusIds);
        Assert.DoesNotContain(ForsakenDefinition.StackStatusId, statusIds);
        Assert.DoesNotContain(ForsakenDefinition.SpreadStatusId, statusIds);
        Assert.DoesNotContain(ForsakenDefinition.ConeStatusId, statusIds);

        var deaths = pull.Events.OfType<DeathEvent>().ToArray();
        var damage = pull.Events.OfType<DamageEvent>().ToArray();
        var deathsWithNearbyTargetDamage = deaths.Count(death => damage.Any(hit =>
            hit.TargetActorId == death.TargetActorId &&
            hit.PullTime <= death.PullTime &&
            hit.PullTime >= death.PullTime - TimeSpan.FromSeconds(10)));
        Assert.Equal(8, deathsWithNearbyTargetDamage);
        var representativeDeath = Assert.Single(deaths, death => death.Id == new EventId(14_571));
        var representativeDamage = Assert.Single(damage, hit => hit.Id == new EventId(14_520));
        Assert.Equal(representativeDeath.TargetActorId, representativeDamage.TargetActorId);
        Assert.InRange(
            representativeDamage.PullTime,
            representativeDeath.PullTime - TimeSpan.FromSeconds(10),
            representativeDeath.PullTime);
    }

    [Fact]
    public async Task FixtureRoundTripAndDefaultEngineOutcomesRemainStable()
    {
        var pull = LoadFixture();
        var reloaded = CanonicalPullSerializer.Deserialize(CanonicalPullSerializer.Serialize(pull));

        Assert.Equal(pull.Events.Count, reloaded.Events.Count);
        Assert.Equal(pull.Events.Select(evt => evt.Id), reloaded.Events.Select(evt => evt.Id));
        Assert.Equal(pull.Events.Select(evt => evt.Sequence), reloaded.Events.Select(evt => evt.Sequence));
        Assert.Equal(pull.Events.Select(evt => evt.PullTime), reloaded.Events.Select(evt => evt.PullTime));
        Assert.Equal(pull.Events.Select(evt => evt.GetType()), reloaded.Events.Select(evt => evt.GetType()));

        var run = await AnalyzerWorkspaceEngineComposition.CreateDefault().AnalyzeAsync(reloaded);

        Assert.Empty(run.Failures);
        Assert.Equal(22, run.Results.Count);
        Assert.Equal(11, run.Results.Count(result => result.AnalyzerId == "generic.death-raise-context"));
        Assert.Equal(11, run.Results.Count(result => result.AnalyzerId == "generic.healing-activity"));
        Assert.DoesNotContain(run.Results, result => result.AnalyzerId == ForsakenOpeningAssignmentAnalyzer.AnalyzerId);
        Assert.DoesNotContain(run.Skipped, skip => skip.AnalyzerId == ForsakenOpeningAssignmentAnalyzer.AnalyzerId);

        var dancerSkips = run.Skipped
            .Where(skip => skip.AnalyzerId is DancerCoreExecutionAnalyzer.AnalyzerId or DancerBurstAndUptimeAnalyzer.AnalyzerId)
            .OrderBy(skip => skip.AnalyzerId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, dancerSkips.Length);
        Assert.All(dancerSkips, skip => Assert.Equal(AnalyzerSkipReason.Unsupported, skip.Reason));
    }

    private static RecordedPull LoadFixture()
    {
        return CanonicalPullSerializer.Deserialize(ReadFixturePayload());
    }

    private static string ReadFixturePayload()
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "M11", FixtureFileName));
    }

    private static void AssertEventCount<TEvent>(RecordedPull pull, int expected)
        where TEvent : NormalizedEvent
    {
        Assert.Equal(expected, pull.Events.Count(evt => evt is TEvent));
    }

    private static uint? ActionId(NormalizedEvent evt)
    {
        return evt switch
        {
            DamageEvent damage => damage.ActionId,
            HealEvent heal => heal.ActionId,
            CastStartEvent cast => cast.ActionId,
            CastEndEvent cast => cast.ActionId,
            ActionUseEvent action => action.ActionId,
            RaiseEvent raise => raise.ActionId,
            _ => null,
        };
    }

    private static uint? StatusId(NormalizedEvent evt)
    {
        return evt switch
        {
            StatusApplyEvent apply => apply.StatusId,
            StatusRemoveEvent remove => remove.StatusId,
            _ => null,
        };
    }

    [GeneratedRegex("^Player [1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PlayerLabel();

    [GeneratedRegex("^Pet [1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PetLabel();
}
