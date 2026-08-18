namespace BetterDeaths.Domain;

public enum PullDataSourceKind
{
    DalamudLive,
    FFLogs,
    SavedFile,
    ImportedFile,
}

public enum CaptureFidelity
{
    Exact,
    Derived,
    Sampled,
    Inferred,
}

public sealed record PullProvenance
{
    public required PullDataSourceKind SourceKind { get; init; }

    public string? SourceReference { get; init; }

    public CaptureFidelity Fidelity { get; init; } = CaptureFidelity.Exact;

    public float Confidence { get; init; } = 1.0f;

    public string? ProducerVersion { get; init; }
}

public sealed record EventProvenance
{
    public required PullDataSourceKind SourceKind { get; init; }

    public string? SourceReference { get; init; }

    public CaptureFidelity Fidelity { get; init; } = CaptureFidelity.Exact;

    public float Confidence { get; init; } = 1.0f;
}
