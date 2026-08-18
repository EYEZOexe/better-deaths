namespace BetterDeaths.Persistence;

using BetterDeaths.Domain;
using System;
using System.IO;
using System.Text.Json;

internal sealed class CanonicalPullCompatibilityException : InvalidDataException
{
    public CanonicalPullCompatibilityException(string message)
        : base(message)
    {
    }
}

internal static class CanonicalPullSerializer
{
    public const int CurrentFileSchemaVersion = 1;

    public const int CurrentPullSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public static string Serialize(RecordedPull pull)
    {
        ArgumentNullException.ThrowIfNull(pull);
        ValidatePullSchema(pull);

        return JsonSerializer.Serialize(
            new CanonicalPullEnvelope(CurrentFileSchemaVersion, pull),
            JsonOptions);
    }

    public static RecordedPull Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        CanonicalPullEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<CanonicalPullEnvelope>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Canonical pull JSON is invalid.", exception);
        }

        if (envelope is null)
        {
            throw new InvalidDataException("Canonical pull JSON did not contain a pull envelope.");
        }

        if (envelope.FileSchemaVersion != CurrentFileSchemaVersion)
        {
            throw new CanonicalPullCompatibilityException(
                $"Unsupported canonical pull file schema {envelope.FileSchemaVersion}; expected {CurrentFileSchemaVersion}.");
        }

        if (envelope.Pull is null)
        {
            throw new InvalidDataException("Canonical pull envelope did not contain a pull.");
        }

        ValidatePullSchema(envelope.Pull);
        return envelope.Pull;
    }

    private static void ValidatePullSchema(RecordedPull pull)
    {
        if (pull.SchemaVersion.Value != CurrentPullSchemaVersion)
        {
            throw new CanonicalPullCompatibilityException(
                $"Unsupported canonical pull schema {pull.SchemaVersion.Value}; expected {CurrentPullSchemaVersion}.");
        }
    }

    private sealed record CanonicalPullEnvelope(int FileSchemaVersion, RecordedPull? Pull);
}
