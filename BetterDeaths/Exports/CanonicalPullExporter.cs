namespace BetterDeaths.Exports;

using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

internal enum CanonicalPullExportMode
{
    Canonical,
    Anonymized,
}

internal sealed record CanonicalPullExportOptions
{
    public CanonicalPullExportMode Mode { get; init; } = CanonicalPullExportMode.Canonical;
}

internal sealed record CanonicalPullExportRequest
{
    public required RecordedPull Pull { get; init; }

    public CanonicalPullExportOptions Options { get; init; } = new();
}

internal sealed record CanonicalPullExportResult
{
    public required int ExportPolicyVersion { get; init; }

    public required CanonicalPullExportMode Mode { get; init; }

    public required PullId ExportedPullId { get; init; }

    public required string Payload { get; init; }
}

internal static class CanonicalPullExporter
{
    public const int CurrentExportPolicyVersion = 1;

    public static CanonicalPullExportResult Export(CanonicalPullExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Pull);
        ArgumentNullException.ThrowIfNull(request.Options);

        var exportedPull = request.Options.Mode switch
        {
            CanonicalPullExportMode.Canonical => request.Pull,
            CanonicalPullExportMode.Anonymized => Anonymize(request.Pull),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Options.Mode, "Unsupported canonical export mode."),
        };

        return new CanonicalPullExportResult
        {
            ExportPolicyVersion = CurrentExportPolicyVersion,
            Mode = request.Options.Mode,
            ExportedPullId = exportedPull.Id,
            Payload = SerializeExportPayload(exportedPull, request.Options.Mode),
        };
    }

    private static RecordedPull Anonymize(RecordedPull pull)
    {
        var sensitiveActorNames = BuildAnonymizedActorNames(pull.Actors);
        var actors = pull.Actors
            .Select(actor => sensitiveActorNames.TryGetValue(actor.Id, out var replacement)
                ? actor with { Name = replacement }
                : actor)
            .ToArray();
        var events = pull.Events
            .Select(evt => evt with
            {
                ObservedAt = null,
                Provenance = Sanitize(evt.Provenance),
            })
            .ToArray();
        var positions = pull.Positions
            .Select(sample => sample with { Provenance = Sanitize(sample.Provenance) })
            .ToArray();
        var markers = pull.WorldMarkers
            .Select(sample => sample with
            {
                Label = null,
                Provenance = Sanitize(sample.Provenance),
            })
            .ToArray();

        var sanitized = pull with
        {
            Id = new PullId(Guid.Empty),
            Metadata = pull.Metadata with { StartedAt = null },
            Actors = actors,
            Events = events,
            Positions = positions,
            WorldMarkers = markers,
            Provenance = Sanitize(pull.Provenance),
        };

        return sanitized with { Id = CreateAnonymizedPullId(sanitized) };
    }

    private static IReadOnlyDictionary<ActorId, string> BuildAnonymizedActorNames(IReadOnlyList<ActorRecord> actors)
    {
        var replacements = new Dictionary<ActorId, string>();
        var playerIndex = 0;
        var petIndex = 0;
        var unknownIndex = 0;

        foreach (var actor in actors.OrderBy(actor => actor.Id.Value))
        {
            switch (actor.Kind)
            {
                case ActorKind.Player:
                    replacements[actor.Id] = $"Player {++playerIndex}";
                    break;
                case ActorKind.Pet:
                    replacements[actor.Id] = $"Pet {++petIndex}";
                    break;
                case ActorKind.Unknown:
                    // Unknown actor classification is not strong enough evidence that a display name is non-player.
                    replacements[actor.Id] = $"Unknown Actor {++unknownIndex}";
                    break;
            }
        }

        return replacements;
    }

    private static PullProvenance Sanitize(PullProvenance provenance)
    {
        return provenance with { SourceReference = null };
    }

    private static EventProvenance Sanitize(EventProvenance provenance)
    {
        return provenance with { SourceReference = null };
    }

    private static string SerializeExportPayload(RecordedPull pull, CanonicalPullExportMode mode)
    {
        var root = JsonNode.Parse(CanonicalPullSerializer.Serialize(pull))!.AsObject();
        root["ExportPolicyVersion"] = CurrentExportPolicyVersion;
        root["ExportMode"] = mode switch
        {
            CanonicalPullExportMode.Canonical => "canonical",
            CanonicalPullExportMode.Anonymized => "anonymized",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported canonical export mode."),
        };
        return root.ToJsonString();
    }

    private static PullId CreateAnonymizedPullId(RecordedPull sanitizedPull)
    {
        var canonicalSanitizedPayload = CanonicalPullSerializer.Serialize(sanitizedPull);
        var material = Encoding.UTF8.GetBytes(
            $"better-deaths:canonical-export:v{CurrentExportPolicyVersion}:{canonicalSanitizedPayload}");
        var hash = SHA256.HashData(material);
        return new PullId(new Guid(hash.AsSpan(0, 16)));
    }
}
