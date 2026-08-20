namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Sources.FFLogs.Client;
using System.Text.Json;

public sealed class FFLogsEventNormalizerTests
{
    [Fact]
    public void NormalizesRepresentativeFightIntoCanonicalPull()
    {
        var import = Fight(
            Actors:
            [
                new FFLogsReportActor { Id = 10, Name = "Player One", Type = "Player", SubType = "Dancer" },
                new FFLogsReportActor { Id = 20, Name = "Pet One", Type = "Pet", PetOwnerId = 10 },
                new FFLogsReportActor { Id = 30, Name = "Boss", Type = "NPC", SubType = "Boss" },
            ],
            Events:
            [
                Event(1300, "heal", """{"sourceID":10,"targetID":10,"abilityGameID":200,"amount":5000}"""),
                Event(1100, "damage", """{"sourceID":30,"targetID":10,"abilityGameID":100,"amount":12000,"critical":true}"""),
                Event(1200, "cast", """{"sourceID":20,"targetID":30,"abilityGameID":300}"""),
                Event(1400, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":400,"duration":15000,"stack":2}"""),
                Event(1500, "removebuff", """{"sourceID":10,"targetID":10,"abilityGameID":400}"""),
                Event(1600, "death", """{"sourceID":30,"targetID":10}"""),
                Event(1700, "resurrect", """{"sourceID":10,"targetID":10,"abilityGameID":500}"""),
                Event(1800, "targetabilityupdate", """{"sourceID":30,"targetID":30,"targetable":false}"""),
            ]);

        var result = FFLogsEventNormalizer.Normalize(import, new PullSchemaVersion(1));
        var pull = result.Pull;

        Assert.Empty(result.SkippedEvents);
        Assert.Equal(PullDataSourceKind.FFLogs, pull.Provenance.SourceKind);
        Assert.Equal("fflogs:report:REPORT123:fight:42", pull.Provenance.SourceReference);
        Assert.Equal((uint)777, pull.Metadata.TerritoryId);
        Assert.Equal("Test Zone", pull.Metadata.TerritoryName);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), pull.Metadata.Duration);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_001_000), pull.Metadata.StartedAt);

        Assert.Equal(3, pull.Actors.Count);
        var player = Assert.Single(pull.Actors, actor => actor.Name == "Player One");
        var pet = Assert.Single(pull.Actors, actor => actor.Name == "Pet One");
        var boss = Assert.Single(pull.Actors, actor => actor.Name == "Boss");
        Assert.Equal(ActorKind.Player, player.Kind);
        Assert.Equal("DNC", player.JobAbbreviation);
        Assert.Equal(ActorKind.Pet, pet.Kind);
        Assert.Equal(player.Id, pet.OwnerActorId);
        Assert.Equal(ActorKind.Enemy, boss.Kind);

        Assert.Equal(8, pull.Events.Count);
        Assert.Equal(Enumerable.Range(1, 8).Select(value => (long)value), pull.Events.Select(evt => evt.Sequence));
        Assert.Equal(Enumerable.Range(1, 8).Select(value => new EventId(value)), pull.Events.Select(evt => evt.Id));
        Assert.Equal(
            new[] { 100d, 200d, 300d, 400d, 500d, 600d, 700d, 800d },
            pull.Events.Select(evt => evt.PullTime.TotalMilliseconds));

        var damage = Assert.IsType<DamageEvent>(pull.Events[0]);
        Assert.Equal(12000, damage.Amount);
        Assert.Equal((uint)100, damage.ActionId);
        Assert.True(damage.IsCritical);
        Assert.Equal(boss.Id, damage.SourceActorId);
        Assert.Equal(player.Id, damage.TargetActorId);

        var action = Assert.IsType<ActionUseEvent>(pull.Events[1]);
        Assert.Equal((uint)300, action.ActionId);
        Assert.Equal(pet.Id, action.SourceActorId);
        var heal = Assert.IsType<HealEvent>(pull.Events[2]);
        Assert.Equal(5000, heal.Amount);
        var apply = Assert.IsType<StatusApplyEvent>(pull.Events[3]);
        Assert.Equal((uint)400, apply.StatusId);
        Assert.Equal((ushort)2, apply.Stacks);
        Assert.Equal(TimeSpan.FromSeconds(15), apply.Duration);
        Assert.IsType<StatusRemoveEvent>(pull.Events[4]);
        Assert.IsType<DeathEvent>(pull.Events[5]);
        Assert.IsType<RaiseEvent>(pull.Events[6]);
        Assert.False(Assert.IsType<TargetabilityEvent>(pull.Events[7]).IsTargetable);
        Assert.All(pull.Events, evt => Assert.Equal(PullDataSourceKind.FFLogs, evt.Provenance.SourceKind));
    }

    [Fact]
    public void PlayerTypedPetOwnerActorCannotAcquireAPlayerJob()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(
                Actors:
                [
                    new FFLogsReportActor
                    {
                        Id = 10,
                        Name = "Owner",
                        Type = "Player",
                        SubType = "WhiteMage",
                    },
                    new FFLogsReportActor
                    {
                        Id = 20,
                        Name = "Player-Typed Pet",
                        Type = "Player",
                        SubType = "Dancer",
                        PetOwnerId = 10,
                    },
                    new FFLogsReportActor { Id = 30, Name = "Boss", Type = "NPC" },
                ],
                Events:
                [
                    Event(
                        1200,
                        "damage",
                        """{"sourceID":20,"targetID":30,"abilityGameID":1,"amount":100}"""),
                ]),
            new PullSchemaVersion(1));

        Assert.Empty(result.SkippedEvents);
        var pet = Assert.Single(result.Pull.Actors, actor => actor.Name == "Player-Typed Pet");
        Assert.Equal(ActorKind.Pet, pet.Kind);
        Assert.Null(pet.JobAbbreviation);
        Assert.NotNull(pet.OwnerActorId);
    }

    [Fact]
    public void DistinctNpcInstancesRemainDistinctWhilePlayerInstanceNoiseCollapsesToOneActor()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(
                Actors:
                [
                    new FFLogsReportActor { Id = 10, Name = "Player One", Type = "Player", SubType = "Dancer" },
                    new FFLogsReportActor { Id = 30, Name = "Twin Add", Type = "NPC", SubType = "NPC" },
                ],
                Events:
                [
                    Event(1100, "damage", """{"sourceID":30,"sourceInstanceID":1,"targetID":10,"targetInstanceID":1,"abilityGameID":1,"amount":100}"""),
                    Event(1200, "damage", """{"sourceID":30,"sourceInstanceID":2,"targetID":10,"targetInstanceID":2,"abilityGameID":2,"amount":200}"""),
                    Event(1300, "heal", """{"sourceID":10,"sourceInstanceId":1,"targetID":10,"targetInstance":7,"abilityGameID":3,"amount":50}"""),
                ]),
            new PullSchemaVersion(1));

        var player = Assert.Single(result.Pull.Actors, actor => actor.Kind == ActorKind.Player);
        var adds = result.Pull.Actors.Where(actor => actor.Name == "Twin Add").ToArray();
        Assert.Equal(2, adds.Length);
        Assert.NotEqual(adds[0].Id, adds[1].Id);

        var firstDamage = Assert.IsType<DamageEvent>(result.Pull.Events[0]);
        var secondDamage = Assert.IsType<DamageEvent>(result.Pull.Events[1]);
        Assert.NotEqual(firstDamage.SourceActorId, secondDamage.SourceActorId);
        Assert.Equal(player.Id, firstDamage.TargetActorId);
        Assert.Equal(player.Id, secondDamage.TargetActorId);

        var heal = Assert.IsType<HealEvent>(result.Pull.Events[2]);
        Assert.Equal(player.Id, heal.SourceActorId);
        Assert.Equal(player.Id, heal.TargetActorId);
    }

    [Fact]
    public void UnreferencedReportActorsDoNotBloatSelectedFightActorDirectory()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(
                Actors:
                [
                    new FFLogsReportActor { Id = 10, Name = "Player One", Type = "Player", SubType = "Dancer" },
                    new FFLogsReportActor { Id = 30, Name = "Boss", Type = "NPC", SubType = "Boss" },
                    new FFLogsReportActor { Id = 99, Name = "Other Fight NPC", Type = "NPC", SubType = "NPC" },
                ],
                Events:
                [
                    Event(1200, "damage", """{"sourceID":30,"sourceInstanceID":1,"targetID":10,"abilityGameID":1,"amount":100}"""),
                ]),
            new PullSchemaVersion(1));

        Assert.Equal(2, result.Pull.Actors.Count);
        Assert.DoesNotContain(result.Pull.Actors, actor => actor.Name == "Other Fight NPC");
    }

    [Fact]
    public void DeterministicOrderingUsesTimestampThenOriginalSourceOrderAcrossFlattenedPages()
    {
        var events = new[]
        {
            Event(1500, "damage", """{"sourceID":30,"targetID":10,"abilityGameID":1,"amount":100}"""),
            Event(1200, "damage", """{"sourceID":30,"targetID":10,"abilityGameID":2,"amount":200}"""),
            Event(1200, "heal", """{"sourceID":10,"targetID":10,"abilityGameID":3,"amount":300}"""),
        };

        var first = FFLogsEventNormalizer.Normalize(Fight(Events: events), new PullSchemaVersion(1)).Pull;
        var second = FFLogsEventNormalizer.Normalize(Fight(Events: events), new PullSchemaVersion(1)).Pull;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Events.Select(evt => evt.GetType()), second.Events.Select(evt => evt.GetType()));
        Assert.Equal(new uint?[] { 2, 3, 1 }, first.Events.Select(ActionId));
        Assert.Equal(first.Events.Select(evt => evt.Id), second.Events.Select(evt => evt.Id));
        Assert.Equal(first.Events.Select(evt => evt.SourceActorId), second.Events.Select(evt => evt.SourceActorId));
    }

    [Fact]
    public void DeduplicatesOnlyExplicitSourceIdentityAndPreservesOtherwiseIdenticalEvents()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(Events:
            [
                Event(1200, "damage", """{"eventID":"same","sourceID":30,"targetID":10,"abilityGameID":1,"amount":100}"""),
                Event(1200, "damage", """{"eventID":"same","sourceID":30,"targetID":10,"abilityGameID":1,"amount":100}"""),
                Event(1300, "heal", """{"sourceID":10,"targetID":10,"abilityGameID":2,"amount":50}"""),
                Event(1300, "heal", """{"sourceID":10,"targetID":10,"abilityGameID":2,"amount":50}"""),
            ]),
            new PullSchemaVersion(1));

        Assert.Equal(3, result.Pull.Events.Count);
        var skipped = Assert.Single(result.SkippedEvents);
        Assert.Contains("duplicate explicit", skipped.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedOrInsufficientEventsAreExplicitlySkippedWithoutInventingFacts()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(Events:
            [
                Event(900, "damage", """{"sourceID":30,"targetID":10,"amount":1}"""),
                Event(1200, "mystery", """{"sourceID":30,"targetID":10}"""),
                Event(1300, "damage", """{"sourceID":30,"targetID":10}"""),
                Event(1400, "begincast", """{"sourceID":30,"targetID":10,"abilityGameID":99}"""),
            ]),
            new PullSchemaVersion(1));

        Assert.Empty(result.Pull.Events);
        Assert.Equal(4, result.SkippedEvents.Count);
        Assert.Contains(result.SkippedEvents, skipped => skipped.Reason.Contains("outside selected fight", StringComparison.Ordinal));
        Assert.Contains(result.SkippedEvents, skipped => skipped.Reason.Contains("unsupported", StringComparison.Ordinal));
        Assert.Contains(result.SkippedEvents, skipped => skipped.Reason.Contains("missing amount", StringComparison.Ordinal));
        Assert.Contains(result.SkippedEvents, skipped => skipped.Reason.Contains("lacks action or duration", StringComparison.Ordinal));
    }

    [Fact]
    public void ReferencedActorsWithoutMasterDataRemainUnknownPlaceholders()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(
                Actors: [],
                Events:
                [
                    Event(1200, "damage", """{"sourceID":91,"targetID":92,"abilityGameID":1,"amount":100}"""),
                ]),
            new PullSchemaVersion(1));

        Assert.Equal(2, result.Pull.Actors.Count);
        Assert.All(result.Pull.Actors, actor => Assert.Equal(ActorKind.Unknown, actor.Kind));
        Assert.Contains(result.Pull.Actors, actor => actor.Name == "FFLogs Actor 91");
        Assert.Contains(result.Pull.Actors, actor => actor.Name == "FFLogs Actor 92");
    }

    [Fact]
    public void StablePullIdentityIncludesReportFightAndRevision()
    {
        var baseline = FFLogsEventNormalizer.Normalize(Fight(), new PullSchemaVersion(1)).Pull.Id;
        var same = FFLogsEventNormalizer.Normalize(Fight(), new PullSchemaVersion(1)).Pull.Id;
        var newRevision = FFLogsEventNormalizer.Normalize(
            Fight(Report: new FFLogsReportMetadata
            {
                Code = "REPORT123",
                StartTimeUnixMilliseconds = 1_700_000_000_000,
                EndTimeUnixMilliseconds = 1_700_000_100_000,
                Revision = 8,
            }),
            new PullSchemaVersion(1)).Pull.Id;

        Assert.Equal(baseline, same);
        Assert.NotEqual(baseline, newRevision);
    }

    [Fact]
    public void CataloguedEncodedStatusesMapExactlyWhileUnknownAndActionIdentitiesRemainUnchanged()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(
                Abilities:
                [
                    Ability(1_001_825),
                    Ability(1_005_084),
                    Ability(15_997),
                ],
                Events:
                [
                    Event(1100, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":1001825,"duration":20000}"""),
                    Event(1200, "removebuff", """{"sourceID":10,"targetID":10,"abilityGameID":1001825}"""),
                    Event(1300, "applydebuff", """{"sourceID":30,"targetID":10,"abilityGameID":1005084,"duration":9999000}"""),
                    Event(1400, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":1009999,"duration":15000}"""),
                    Event(1500, "cast", """{"sourceID":10,"targetID":30,"abilityGameID":15997}"""),
                    Event(1600, "cast", """{"sourceID":10,"targetID":30,"abilityGameID":1001825}"""),
                ]),
            new PullSchemaVersion(1));

        Assert.Empty(result.SkippedEvents);
        var statuses = result.Pull.Events.OfType<StatusApplyEvent>().ToArray();
        Assert.Equal((uint)1_825, statuses[0].StatusId);
        Assert.Equal(TimeSpan.FromSeconds(20), statuses[0].Duration);
        Assert.Equal((uint)5_084, statuses[1].StatusId);
        Assert.Null(statuses[1].Duration);
        Assert.Equal((uint)1_009_999, statuses[2].StatusId);
        Assert.Equal(TimeSpan.FromSeconds(15), statuses[2].Duration);
        Assert.Equal((uint)1_825, Assert.Single(result.Pull.Events.OfType<StatusRemoveEvent>()).StatusId);
        Assert.Equal(
            new uint[] { 15_997, 1_001_825 },
            result.Pull.Events.OfType<ActionUseEvent>().Select(action => action.ActionId));

        Assert.Equal(3, result.AbilityIdentityDiagnostics.Count);
        var uncataloguedDiagnostic = Assert.Single(
            result.AbilityIdentityDiagnostics,
            diagnostic => diagnostic.Classification == FFLogsAbilityIdentityClassification.UncataloguedPreserved);
        Assert.Equal(3, uncataloguedDiagnostic.InputIndex);
        Assert.Equal((uint)1_009_999, uncataloguedDiagnostic.SourceId);
        Assert.Equal(
            FFLogsAbilityIdentityClassification.UncataloguedPreserved,
            uncataloguedDiagnostic.Classification);
        Assert.Equal(
            new uint[] { 15_997, 1_001_825 },
            result.AbilityIdentityDiagnostics
                .Where(diagnostic => diagnostic.Classification == FFLogsAbilityIdentityClassification.CataloguedSourceIdentity)
                .Select(diagnostic => diagnostic.SourceId));
        var sentinelDiagnostic = Assert.Single(result.StatusDurationDiagnostics);
        Assert.Equal(2, sentinelDiagnostic.InputIndex);
        Assert.Equal(9_999_000, sentinelDiagnostic.SourceDurationMilliseconds);
        Assert.Equal(
            FFLogsStatusDurationClassification.IndefiniteSentinelUnavailable,
            sentinelDiagnostic.Classification);
    }

    [Fact]
    public void StatusDurationNormalizationRejectsNegativeAndExactSentinelButPreservesOtherFiniteValues()
    {
        var result = FFLogsEventNormalizer.Normalize(
            Fight(
                Abilities: [Ability(400)],
                Events:
                [
                    Event(1100, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":400,"duration":-1}"""),
                    Event(1200, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":400,"duration":9999000}"""),
                    Event(1300, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":400,"duration":0}"""),
                    Event(1400, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":400,"duration":20000}"""),
                    Event(1500, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":400,"duration":10000000}"""),
                    Event(1600, "begincast", """{"sourceID":10,"targetID":30,"abilityGameID":400,"duration":9999000}"""),
                    Event(1700, "applybuff", """{"sourceID":10,"targetID":10,"abilityGameID":400}"""),
                ]),
            new PullSchemaVersion(1));

        Assert.Empty(result.SkippedEvents);
        Assert.Equal(7, result.AbilityIdentityDiagnostics.Count);
        Assert.All(
            result.AbilityIdentityDiagnostics,
            diagnostic => Assert.Equal(
                FFLogsAbilityIdentityClassification.CataloguedSourceIdentity,
                diagnostic.Classification));
        Assert.Equal(
            new TimeSpan?[]
            {
                null,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(20),
                TimeSpan.FromSeconds(10_000),
                null,
            },
            result.Pull.Events.OfType<StatusApplyEvent>().Select(status => status.Duration));
        Assert.Equal(
            TimeSpan.FromSeconds(9_999),
            Assert.Single(result.Pull.Events.OfType<CastStartEvent>()).CastDuration);
        Assert.Equal(2, result.StatusDurationDiagnostics.Count);
        Assert.Contains(
            result.StatusDurationDiagnostics,
            diagnostic => diagnostic.InputIndex == 0 &&
                diagnostic.SourceDurationMilliseconds == -1 &&
                diagnostic.Classification == FFLogsStatusDurationClassification.NegativeUnavailable);
        Assert.Contains(
            result.StatusDurationDiagnostics,
            diagnostic => diagnostic.InputIndex == 1 &&
                diagnostic.SourceDurationMilliseconds == 9_999_000 &&
                diagnostic.Classification == FFLogsStatusDurationClassification.IndefiniteSentinelUnavailable);
        Assert.DoesNotContain(
            result.StatusDurationDiagnostics,
            diagnostic => diagnostic.InputIndex is >= 2 and <= 6);
    }

    private static uint? ActionId(NormalizedEvent evt)
    {
        return evt switch
        {
            DamageEvent damage => damage.ActionId,
            HealEvent heal => heal.ActionId,
            ActionUseEvent action => action.ActionId,
            _ => null,
        };
    }

    private static FFLogsFightImportData Fight(
        FFLogsReportMetadata? Report = null,
        IReadOnlyList<FFLogsReportActor>? Actors = null,
        IReadOnlyList<FFLogsReportAbility>? Abilities = null,
        IReadOnlyList<FFLogsEventEnvelope>? Events = null)
    {
        var report = Report ?? new FFLogsReportMetadata
        {
            Code = "REPORT123",
            StartTimeUnixMilliseconds = 1_700_000_000_000,
            EndTimeUnixMilliseconds = 1_700_000_100_000,
            Revision = 7,
        };
        var fight = new FFLogsFightMetadata
        {
            Id = 42,
            EncounterId = 1234,
            Name = "Test Encounter",
            StartTimeMilliseconds = 1000,
            EndTimeMilliseconds = 2000,
            GameZoneId = 777,
            GameZoneName = "Test Zone",
        };
        return new FFLogsFightImportData
        {
            ReportDocument = new FFLogsReportDocument
            {
                Report = report,
                Fights = [fight],
                Abilities = Abilities ?? Array.Empty<FFLogsReportAbility>(),
            },
            Fight = fight,
            Actors = Actors ??
            [
                new FFLogsReportActor { Id = 10, Name = "Player One", Type = "Player", SubType = "Dancer" },
                new FFLogsReportActor { Id = 30, Name = "Boss", Type = "NPC", SubType = "Boss" },
            ],
            Events = Events ?? Array.Empty<FFLogsEventEnvelope>(),
        };
    }

    private static FFLogsReportAbility Ability(uint gameId)
    {
        return new FFLogsReportAbility
        {
            GameId = gameId,
            Name = $"Ability {gameId}",
            Icon = "synthetic-icon.png",
            Type = "Synthetic Type",
        };
    }

    private static FFLogsEventEnvelope Event(double timestamp, string type, string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return new FFLogsEventEnvelope
        {
            TimestampMilliseconds = timestamp,
            Type = type,
            Payload = document.RootElement.Clone(),
        };
    }
}
