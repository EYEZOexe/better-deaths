namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Exports;
using BetterDeaths.Persistence;
using System.Reflection;

public sealed class M10CanonicalExportTests
{
    [Fact]
    public void CanonicalExportRoundTripsAndIsDeterministic()
    {
        var pull = CreatePull();
        var request = new CanonicalPullExportRequest
        {
            Pull = pull,
            Options = new CanonicalPullExportOptions { Mode = CanonicalPullExportMode.Canonical },
        };

        var first = CanonicalPullExporter.Export(request);
        var second = CanonicalPullExporter.Export(request);
        var restored = CanonicalPullSerializer.Deserialize(first.Payload);

        Assert.Equal(CanonicalPullExporter.CurrentExportPolicyVersion, first.ExportPolicyVersion);
        Assert.Equal(CanonicalPullExportMode.Canonical, first.Mode);
        Assert.Equal(pull.Id, first.ExportedPullId);
        Assert.Equal(first.Payload, second.Payload);
        Assert.Equal(pull.Id, restored.Id);
        Assert.Equal(pull.Metadata.StartedAt, restored.Metadata.StartedAt);
        Assert.Equal(pull.Actors.Select(actor => actor.Name), restored.Actors.Select(actor => actor.Name));
        Assert.Equal(pull.Events.Select(evt => evt.Id), restored.Events.Select(evt => evt.Id));
        Assert.Equal(pull.Events.Select(evt => evt.Sequence), restored.Events.Select(evt => evt.Sequence));
        Assert.Equal(pull.Provenance.SourceReference, restored.Provenance.SourceReference);
    }

    [Fact]
    public void AnonymizedExportRemovesDirectIdentitySourceReferencesAndAbsoluteTime()
    {
        var pull = CreatePull();

        var export = CanonicalPullExporter.Export(new CanonicalPullExportRequest
        {
            Pull = pull,
            Options = new CanonicalPullExportOptions { Mode = CanonicalPullExportMode.Anonymized },
        });
        var restored = CanonicalPullSerializer.Deserialize(export.Payload);

        Assert.NotEqual(pull.Id, export.ExportedPullId);
        Assert.Equal(export.ExportedPullId, restored.Id);
        Assert.Null(restored.Metadata.StartedAt);
        Assert.Equal("Player 1", restored.Actors.Single(actor => actor.Id == new ActorId(1)).Name);
        Assert.Equal("Player 2", restored.Actors.Single(actor => actor.Id == new ActorId(2)).Name);
        Assert.Equal("Pet 1", restored.Actors.Single(actor => actor.Id == new ActorId(3)).Name);
        Assert.Equal("Omega", restored.Actors.Single(actor => actor.Id == new ActorId(4)).Name);
        Assert.Equal("Unknown Actor 1", restored.Actors.Single(actor => actor.Id == new ActorId(5)).Name);
        Assert.Null(restored.Provenance.SourceReference);
        Assert.All(restored.Events, evt =>
        {
            Assert.Null(evt.ObservedAt);
            Assert.Null(evt.Provenance.SourceReference);
        });
        Assert.All(restored.Positions, sample => Assert.Null(sample.Provenance.SourceReference));
        Assert.All(restored.WorldMarkers, sample =>
        {
            Assert.Null(sample.Label);
            Assert.Null(sample.Provenance.SourceReference);
        });

        Assert.DoesNotContain("Alice Example", export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Bob Example", export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice's Carbuncle", export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Mystery Alice", export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(SourceReference, export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice custom marker", export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(pull.Id.Value.ToString(), export.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnonymizedExportPreservesActorRelationshipsAndEvidenceOrdering()
    {
        var pull = CreatePull();

        var export = CanonicalPullExporter.Export(new CanonicalPullExportRequest
        {
            Pull = pull,
            Options = new CanonicalPullExportOptions { Mode = CanonicalPullExportMode.Anonymized },
        });
        var restored = CanonicalPullSerializer.Deserialize(export.Payload);

        var pet = restored.Actors.Single(actor => actor.Id == new ActorId(3));
        Assert.Equal(new ActorId(1), pet.OwnerActorId);
        Assert.Equal(pull.Events.Select(evt => evt.Id), restored.Events.Select(evt => evt.Id));
        Assert.Equal(pull.Events.Select(evt => evt.Sequence), restored.Events.Select(evt => evt.Sequence));
        Assert.Equal(pull.Events.Select(evt => evt.PullTime), restored.Events.Select(evt => evt.PullTime));
        Assert.Equal(pull.Events.Select(evt => evt.SourceActorId), restored.Events.Select(evt => evt.SourceActorId));
        Assert.Equal(pull.Events.Select(evt => evt.TargetActorId), restored.Events.Select(evt => evt.TargetActorId));
        Assert.Equal(pull.Positions.Select(sample => sample.ActorId), restored.Positions.Select(sample => sample.ActorId));
    }

    [Fact]
    public void AnonymizedExportIsDeterministicAndDoesNotMutateOriginalPull()
    {
        var pull = CreatePull();
        var originalId = pull.Id;
        var originalStartedAt = pull.Metadata.StartedAt;
        var originalNames = pull.Actors.Select(actor => actor.Name).ToArray();
        var originalSourceReference = pull.Provenance.SourceReference;
        var originalObservedAt = pull.Events[0].ObservedAt;

        var request = new CanonicalPullExportRequest
        {
            Pull = pull,
            Options = new CanonicalPullExportOptions { Mode = CanonicalPullExportMode.Anonymized },
        };
        var first = CanonicalPullExporter.Export(request);
        var second = CanonicalPullExporter.Export(request);

        Assert.Equal(first.ExportedPullId, second.ExportedPullId);
        Assert.Equal(first.Payload, second.Payload);
        Assert.Equal(originalId, pull.Id);
        Assert.Equal(originalStartedAt, pull.Metadata.StartedAt);
        Assert.Equal(originalNames, pull.Actors.Select(actor => actor.Name).ToArray());
        Assert.Equal(originalSourceReference, pull.Provenance.SourceReference);
        Assert.Equal(originalObservedAt, pull.Events[0].ObservedAt);
        Assert.Equal("Alice custom marker", pull.WorldMarkers[0].Label);
    }

    [Fact]
    public void AnonymizedPullIdentityIsDerivedOnlyFromSanitizedCanonicalContent()
    {
        var firstPull = CreatePull();
        var secondPull = firstPull with
        {
            Id = new PullId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            Metadata = firstPull.Metadata with { StartedAt = StartedAt.AddDays(7) },
            Actors = firstPull.Actors.Select(actor => actor.Kind switch
            {
                ActorKind.Player => actor with { Name = $"Changed Player {actor.Id.Value}" },
                ActorKind.Pet => actor with { Name = "Changed Pet" },
                ActorKind.Unknown => actor with { Name = "Changed Unknown" },
                _ => actor,
            }).ToArray(),
            Events = firstPull.Events.Select(evt => evt with
            {
                ObservedAt = evt.ObservedAt?.AddDays(7),
                Provenance = evt.Provenance with { SourceReference = "fflogs:report:DifferentCode:fight:999" },
            }).ToArray(),
            Positions = firstPull.Positions.Select(sample => sample with
            {
                Provenance = sample.Provenance with { SourceReference = "local:different-position-source" },
            }).ToArray(),
            WorldMarkers = firstPull.WorldMarkers.Select(sample => sample with
            {
                Label = "Different custom marker",
                Provenance = sample.Provenance with { SourceReference = "local:different-marker-source" },
            }).ToArray(),
            Provenance = firstPull.Provenance with { SourceReference = "fflogs:report:DifferentCode:fight:999" },
        };

        var first = ExportAnonymized(firstPull);
        var second = ExportAnonymized(secondPull);

        Assert.Equal(first.ExportedPullId, second.ExportedPullId);
        Assert.Equal(first.Payload, second.Payload);
    }

    [Fact]
    public void ShareableAnonymizedExportCannotLeakRepresentativeCredentialMaterial()
    {
        const string clientId = "client-id-sensitive-value";
        const string clientSecret = "client-secret-sensitive-value";
        const string accessToken = "access-token-sensitive-value";
        const string authorizationHeader = "Bearer authorization-sensitive-value";
        var pull = CreatePull() with
        {
            Actors =
            [
                new ActorRecord { Id = new ActorId(1), Name = clientSecret, Kind = ActorKind.Player, JobAbbreviation = "DNC" },
                new ActorRecord { Id = new ActorId(2), Name = clientId, Kind = ActorKind.Pet, OwnerActorId = new ActorId(1) },
                new ActorRecord { Id = new ActorId(3), Name = authorizationHeader, Kind = ActorKind.Unknown },
                new ActorRecord { Id = new ActorId(4), Name = "Omega", Kind = ActorKind.Enemy },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.FFLogs,
                SourceReference = accessToken,
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
            },
            Events =
            [
                new DamageEvent
                {
                    Id = new EventId(1),
                    Sequence = 1,
                    PullTime = TimeSpan.FromSeconds(1),
                    ObservedAt = StartedAt.AddSeconds(1),
                    SourceActorId = new ActorId(4),
                    TargetActorId = new ActorId(1),
                    Provenance = new EventProvenance
                    {
                        SourceKind = PullDataSourceKind.FFLogs,
                        SourceReference = authorizationHeader,
                        Fidelity = CaptureFidelity.Exact,
                    },
                    Amount = 1000,
                    ActionId = 100,
                },
            ],
            Positions = [],
            WorldMarkers = [],
        };

        var export = ExportAnonymized(pull);

        Assert.DoesNotContain(clientId, export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(clientSecret, export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(accessToken, export.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(authorizationHeader, export.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportBoundaryAcceptsCanonicalDataOnlyAndHasNoSourceOrUiContractTypes()
    {
        var protectedTypes = new[]
        {
            typeof(CanonicalPullExportOptions),
            typeof(CanonicalPullExportRequest),
            typeof(CanonicalPullExportResult),
            typeof(CanonicalPullExporter),
        };

        foreach (var type in protectedTypes)
        {
            Assert.Equal("BetterDeaths.Exports", type.Namespace);
            foreach (var memberType in GetContractMemberTypes(type))
            {
                var description = memberType.ToString();
                Assert.DoesNotContain("BetterDeaths.Sources", description, StringComparison.Ordinal);
                Assert.DoesNotContain("BetterDeaths.Windows", description, StringComparison.Ordinal);
                Assert.DoesNotContain("Dalamud", description, StringComparison.Ordinal);
                Assert.DoesNotContain("ImGui", description, StringComparison.Ordinal);
            }
        }

        Assert.Equal(
            [typeof(RecordedPull), typeof(CanonicalPullExportOptions)],
            typeof(CanonicalPullExportRequest)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.PropertyType)
                .ToArray());
    }

    private static CanonicalPullExportResult ExportAnonymized(RecordedPull pull) =>
        CanonicalPullExporter.Export(new CanonicalPullExportRequest
        {
            Pull = pull,
            Options = new CanonicalPullExportOptions { Mode = CanonicalPullExportMode.Anonymized },
        });

    private static IEnumerable<Type> GetContractMemberTypes(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return property.PropertyType;
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            yield return field.FieldType;
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            yield return method.ReturnType;
            foreach (var parameter in method.GetParameters())
            {
                yield return parameter.ParameterType;
            }
        }
    }

    private static readonly DateTimeOffset StartedAt = new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);
    private const string SourceReference = "fflogs:report:SecretReportCode:fight:42";

    private static RecordedPull CreatePull()
    {
        return new RecordedPull
        {
            Id = new PullId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            Metadata = new PullMetadata
            {
                TerritoryId = 777,
                TerritoryName = "Dancing Mad Ultimate",
                Duration = TimeSpan.FromMinutes(12),
                StartedAt = StartedAt,
            },
            SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
            Actors =
            [
                new ActorRecord { Id = new ActorId(1), Name = "Alice Example", Kind = ActorKind.Player, ClassJobId = 38, JobAbbreviation = "DNC" },
                new ActorRecord { Id = new ActorId(2), Name = "Bob Example", Kind = ActorKind.Player, ClassJobId = 24, JobAbbreviation = "WHM" },
                new ActorRecord { Id = new ActorId(3), Name = "Alice's Carbuncle", Kind = ActorKind.Pet, OwnerActorId = new ActorId(1) },
                new ActorRecord { Id = new ActorId(4), Name = "Omega", Kind = ActorKind.Enemy },
                new ActorRecord { Id = new ActorId(5), Name = "Mystery Alice", Kind = ActorKind.Unknown },
            ],
            Events =
            [
                new DamageEvent
                {
                    Id = new EventId(10),
                    Sequence = 10,
                    PullTime = TimeSpan.FromSeconds(1),
                    ObservedAt = StartedAt.AddSeconds(1),
                    SourceActorId = new ActorId(4),
                    TargetActorId = new ActorId(1),
                    Provenance = EventProvenance(),
                    Amount = 50000,
                    ActionId = 100,
                },
                new HealEvent
                {
                    Id = new EventId(11),
                    Sequence = 11,
                    PullTime = TimeSpan.FromSeconds(2),
                    ObservedAt = StartedAt.AddSeconds(2),
                    SourceActorId = new ActorId(2),
                    TargetActorId = new ActorId(1),
                    Provenance = EventProvenance(),
                    Amount = 25000,
                    ActionId = 200,
                },
                new ActionUseEvent
                {
                    Id = new EventId(12),
                    Sequence = 12,
                    PullTime = TimeSpan.FromSeconds(3),
                    ObservedAt = StartedAt.AddSeconds(3),
                    SourceActorId = new ActorId(3),
                    TargetActorId = new ActorId(4),
                    Provenance = EventProvenance(),
                    ActionId = 300,
                },
            ],
            Positions =
            [
                new PositionSample
                {
                    Sequence = 20,
                    PullTime = TimeSpan.FromSeconds(4),
                    ActorId = new ActorId(1),
                    X = 100,
                    Y = 0,
                    Z = 100,
                    Provenance = EventProvenance(),
                },
            ],
            WorldMarkers =
            [
                new WorldMarkerSample
                {
                    Sequence = 21,
                    PullTime = TimeSpan.FromSeconds(5),
                    MarkerIndex = 1,
                    Label = "Alice custom marker",
                    Active = true,
                    X = 90,
                    Y = 0,
                    Z = 90,
                    Provenance = EventProvenance(),
                },
            ],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.FFLogs,
                SourceReference = SourceReference,
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
                ProducerVersion = "test-v1",
            },
        };
    }

    private static EventProvenance EventProvenance() => new()
    {
        SourceKind = PullDataSourceKind.FFLogs,
        SourceReference = SourceReference,
        Fidelity = CaptureFidelity.Exact,
        Confidence = 1.0f,
    };
}
