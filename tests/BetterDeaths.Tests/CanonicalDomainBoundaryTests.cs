namespace BetterDeaths;

using BetterDeaths.Domain;
using System.Reflection;

public sealed class CanonicalDomainBoundaryTests
{
    [Fact]
    public void CanonicalDomainPublicContractsDoNotExposeForbiddenIntegrationTypes()
    {
        var domainTypes = typeof(RecordedPull).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(RecordedPull).Namespace)
            .ToList();
        var forbiddenTokens = new[]
        {
            "Dalamud",
            "ImGui",
            "FFLogsClient",
            "FFLogsDto",
            "HttpClient",
        };

        Assert.NotEmpty(domainTypes);

        foreach (var type in domainTypes)
        {
            Assert.DoesNotContain(forbiddenTokens, token => ContainsToken(type, token));

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain(
                    forbiddenTokens,
                    token => property.PropertyType.ToString().Contains(token, StringComparison.Ordinal));
            }
        }
    }

    private static bool ContainsToken(Type type, string token)
    {
        return (type.FullName ?? type.Name).Contains(token, StringComparison.Ordinal);
    }
}
