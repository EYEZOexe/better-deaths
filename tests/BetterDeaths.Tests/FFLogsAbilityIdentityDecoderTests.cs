namespace BetterDeaths;

using BetterDeaths.Sources.FFLogs;

public sealed class FFLogsAbilityIdentityDecoderTests
{
    [Theory]
    [InlineData(1_001_825u, 1_825u)]
    [InlineData(1_005_084u, 5_084u)]
    [InlineData(1_005_085u, 5_085u)]
    [InlineData(1_005_086u, 5_086u)]
    public void ExactCataloguedStatusMappingsAreVerifiedWithoutArithmetic(uint sourceId, uint canonicalId)
    {
        var decoder = Decoder(Ability(sourceId));

        var resolution = decoder.Resolve(sourceId, FFLogsAbilityEventCategory.Status);

        Assert.Equal(sourceId, resolution.SourceId);
        Assert.Equal(canonicalId, resolution.CanonicalId);
        Assert.Equal(FFLogsAbilityIdentityClassification.VerifiedStatusMapping, resolution.Classification);
        Assert.Null(resolution.DiagnosticReason);
    }

    [Fact]
    public void MappingRequiresExactCatalogPresenceAndDoesNotGeneralizeTheNumericFamily()
    {
        var decoder = Decoder(Ability(1_001_826));

        var absentKnownMapping = decoder.Resolve(1_001_825, FFLogsAbilityEventCategory.Status);
        var nearbyCataloguedStatus = decoder.Resolve(1_001_826, FFLogsAbilityEventCategory.Status);

        Assert.Equal((uint)1_001_825, absentKnownMapping.CanonicalId);
        Assert.Equal(
            FFLogsAbilityIdentityClassification.UncataloguedPreserved,
            absentKnownMapping.Classification);
        Assert.NotNull(absentKnownMapping.DiagnosticReason);

        Assert.Equal((uint)1_001_826, nearbyCataloguedStatus.CanonicalId);
        Assert.Equal(
            FFLogsAbilityIdentityClassification.CataloguedSourceIdentity,
            nearbyCataloguedStatus.Classification);
        Assert.Contains("no verified canonical mapping", nearbyCataloguedStatus.DiagnosticReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionIdentitiesAlwaysPassThroughEvenWhenTheirNumberMatchesAnEncodedStatus()
    {
        var decoder = Decoder(Ability(1_001_825), Ability(15_997));

        var encodedNumberAsAction = decoder.Resolve(1_001_825, FFLogsAbilityEventCategory.Action);
        var normalDancerAction = decoder.Resolve(15_997, FFLogsAbilityEventCategory.Action);

        Assert.Equal((uint)1_001_825, encodedNumberAsAction.CanonicalId);
        Assert.Equal((uint)15_997, normalDancerAction.CanonicalId);
        Assert.All(
            new[] { encodedNumberAsAction, normalDancerAction },
            resolution =>
            {
                Assert.Equal(
                    FFLogsAbilityIdentityClassification.CataloguedSourceIdentity,
                    resolution.Classification);
                Assert.Contains("preserved unchanged", resolution.DiagnosticReason, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void UnknownOrSyntheticIdentityIsPreservedWithExplicitDiagnosticClassification()
    {
        var decoder = Decoder();

        var resolution = decoder.Resolve(500_000, FFLogsAbilityEventCategory.Action);

        Assert.Equal((uint)500_000, resolution.SourceId);
        Assert.Equal((uint)500_000, resolution.CanonicalId);
        Assert.Equal(
            FFLogsAbilityIdentityClassification.UncataloguedPreserved,
            resolution.Classification);
        Assert.Contains("not present", resolution.DiagnosticReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingCatalogEntriesFailInsteadOfSelectingOneByOrder()
    {
        var abilities = new[]
        {
            Ability(1_001_825, name: "First"),
            Ability(1_001_825, name: "Conflicting"),
        };

        Assert.Throws<InvalidOperationException>(() => Decoder(abilities));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(9_999_000, null)]
    [InlineData(0.0, 0.0)]
    [InlineData(20_000.0, 20_000.0)]
    [InlineData(9_998_999.0, 9_998_999.0)]
    [InlineData(10_000_000.0, 10_000_000.0)]
    public void StatusDurationResolutionClassifiesOnlyProvenSentinelsAndInvalidNegatives(
        double sourceMilliseconds,
        double? expectedMilliseconds)
    {
        var resolution = Decoder().ResolveStatusDuration(sourceMilliseconds);

        Assert.Equal(expectedMilliseconds, resolution.Duration?.TotalMilliseconds);
        if (sourceMilliseconds < 0)
        {
            Assert.Equal(FFLogsStatusDurationClassification.NegativeUnavailable, resolution.Classification);
            Assert.Contains("negative", resolution.DiagnosticReason, StringComparison.OrdinalIgnoreCase);
        }
        else if (sourceMilliseconds == 9_999_000)
        {
            Assert.Equal(FFLogsStatusDurationClassification.IndefiniteSentinelUnavailable, resolution.Classification);
            Assert.Contains("indefinite sentinel", resolution.DiagnosticReason, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(FFLogsStatusDurationClassification.Preserved, resolution.Classification);
            Assert.Null(resolution.DiagnosticReason);
        }
    }

    [Fact]
    public void MissingStatusDurationStaysUnavailableWithoutInventingADiagnostic()
    {
        var resolution = Decoder().ResolveStatusDuration(durationMilliseconds: null);

        Assert.Null(resolution.Duration);
        Assert.Equal(FFLogsStatusDurationClassification.Missing, resolution.Classification);
        Assert.Null(resolution.DiagnosticReason);
    }

    private static FFLogsAbilityIdentityDecoder Decoder(params FFLogsReportAbility[] abilities)
    {
        return new FFLogsAbilityIdentityDecoder(abilities);
    }

    private static FFLogsReportAbility Ability(uint gameId, string name = "Synthetic Ability")
    {
        return new FFLogsReportAbility
        {
            GameId = gameId,
            Name = name,
            Icon = "synthetic-icon.png",
            Type = "Synthetic Type",
        };
    }
}
