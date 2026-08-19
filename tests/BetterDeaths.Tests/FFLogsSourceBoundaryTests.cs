namespace BetterDeaths;

using BetterDeaths.Analysis.Engine;
using BetterDeaths.Domain;
using BetterDeaths.Sources;
using BetterDeaths.Sources.FFLogs;
using System.Reflection;
using System.Text.Json;

public sealed class FFLogsSourceBoundaryTests
{
    [Fact]
    public void ReportFightReferenceIsSanitizedForCanonicalProvenance()
    {
        var reference = FFLogsSourceReference.Create("  ABC/123\nDEF  ", 42);

        Assert.Equal("fflogs:report:ABC%2F123%0ADEF:fight:42", reference);
        Assert.DoesNotContain('\n', reference);
        Assert.DoesNotContain("ABC/123", reference, StringComparison.Ordinal);
    }

    [Fact]
    public void PullRequestRequiresReportCodeAndPositiveFightId()
    {
        new FFLogsPullSourceRequest
        {
            ReportCode = "ABC123",
            FightId = 7,
        }.Validate();

        Assert.Throws<ArgumentException>(() => new FFLogsPullSourceRequest
        {
            ReportCode = " ",
            FightId = 7,
        }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new FFLogsPullSourceRequest
        {
            ReportCode = "ABC123",
            FightId = 0,
        }.Validate());
    }

    [Fact]
    public void AccessTokenDoesNotExposeSecretThroughStringOrJsonSerialization()
    {
        const string secret = "super-secret-access-token";
        var token = new FFLogsAccessToken(secret);

        Assert.Equal(secret, token.RevealForAuthorizationHeader());
        Assert.DoesNotContain(secret, token.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(token), StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationErrorsAreStructuredAndContainOnlySafeMessages()
    {
        var privateReport = FFLogsIntegrationErrors.PrivateReportUnavailable();
        var rateLimited = FFLogsIntegrationErrors.RateLimited(TimeSpan.FromSeconds(30));
        var network = FFLogsIntegrationErrors.NetworkFailure(FFLogsOperation.LoadFightEvents);

        Assert.Equal(PullImportErrorCategory.Authorization, privateReport.Category);
        Assert.Equal("fflogs.private_report_unavailable", privateReport.Code);
        Assert.DoesNotContain("token", privateReport.SafeMessage, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(PullImportErrorCategory.RateLimited, rateLimited.Category);
        Assert.Equal(TimeSpan.FromSeconds(30), rateLimited.RetryAfter);

        Assert.Equal(PullImportErrorCategory.Network, network.Category);
        Assert.Equal("fflogs.network.load_fight_events", network.Code);
        Assert.DoesNotContain("exception", network.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PullImportResultContainsExactlyOneOutcome()
    {
        var pull = MinimalPull();
        var success = PullImportResult.Success(pull);
        var failure = PullImportResult.Failure(FFLogsIntegrationErrors.ReportNotFound());

        Assert.True(success.IsSuccess);
        Assert.Same(pull, success.Pull);
        Assert.Null(success.Error);

        Assert.False(failure.IsSuccess);
        Assert.Null(failure.Pull);
        Assert.Equal(PullImportErrorCategory.NotFound, failure.Error?.Category);
    }

    [Fact]
    public void DomainAndAnalysisContractsDoNotReferenceFFLogsIntegrationTypes()
    {
        var assembly = typeof(RecordedPull).Assembly;
        var protectedTypes = assembly.GetTypes()
            .Where(type => type.Namespace is not null &&
                (type.Namespace.StartsWith("BetterDeaths.Domain", StringComparison.Ordinal) ||
                 type.Namespace.StartsWith("BetterDeaths.Analysis", StringComparison.Ordinal)))
            .ToArray();

        Assert.NotEmpty(protectedTypes);
        foreach (var type in protectedTypes)
        {
            Assert.DoesNotContain("BetterDeaths.Sources.FFLogs", type.FullName ?? type.Name, StringComparison.Ordinal);

            foreach (var memberType in GetContractMemberTypes(type))
            {
                Assert.DoesNotContain(
                    "BetterDeaths.Sources.FFLogs",
                    memberType.ToString(),
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void FFLogsDtoContractsStayInsideSourceNamespace()
    {
        var sourceTypes = new[]
        {
            typeof(FFLogsPullSourceRequest),
            typeof(FFLogsReportMetadata),
            typeof(FFLogsFightMetadata),
            typeof(FFLogsEventEnvelope),
            typeof(FFLogsAccessToken),
        };

        Assert.All(sourceTypes, type => Assert.Equal("BetterDeaths.Sources.FFLogs", type.Namespace));
    }

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

    private static RecordedPull MinimalPull()
    {
        return new RecordedPull
        {
            Id = PullId.New(),
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Test",
                Duration = TimeSpan.FromSeconds(10),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.FFLogs,
                SourceReference = FFLogsSourceReference.Create("ABC123", 1),
                Fidelity = CaptureFidelity.Exact,
            },
        };
    }
}
