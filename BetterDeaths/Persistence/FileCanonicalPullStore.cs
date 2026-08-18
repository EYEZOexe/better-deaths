namespace BetterDeaths.Persistence;

using BetterDeaths.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal sealed class FileCanonicalPullStore : IPullStore, IDisposable
{
    internal const int CurrentIndexSchemaVersion = 1;

    private const string IndexFileName = "canonical-pulls.index.json";
    private const string DetailsDirectoryName = "canonical-pull-details";
    private const string BackupSuffix = ".bak";
    private const string TempSuffix = ".tmp";

    private readonly string rootDirectory;
    private readonly string detailsDirectory;
    private readonly string indexPath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public FileCanonicalPullStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        detailsDirectory = Path.Combine(this.rootDirectory, DetailsDirectoryName);
        indexPath = Path.Combine(this.rootDirectory, IndexFileName);
    }

    public async Task SaveAsync(RecordedPull pull, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var serialized = CanonicalPullSerializer.Serialize(pull);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var summaries = (await LoadIndexWithRecoveryAsync(cancellationToken).ConfigureAwait(false)).ToList();

            Directory.CreateDirectory(rootDirectory);
            Directory.CreateDirectory(detailsDirectory);
            await WriteAtomicAsync(GetDetailPath(pull.Id), serialized, cancellationToken).ConfigureAwait(false);

            summaries.RemoveAll(summary => summary.Id == pull.Id);
            summaries.Add(CreateSummary(pull));
            SortSummaries(summaries);
            await SaveIndexAsync(summaries, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var primary = await TryLoadPullFileAsync(GetDetailPath(id), id, cancellationToken).ConfigureAwait(false);
            if (primary is not null)
            {
                return primary;
            }

            return await TryLoadPullFileAsync(GetDetailPath(id) + BackupSuffix, id, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<PullSummary>> QueryAsync(
        PullQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IEnumerable<PullSummary> summaries = await LoadIndexWithRecoveryAsync(cancellationToken).ConfigureAwait(false);
            if (query.TerritoryId is { } territoryId)
            {
                summaries = summaries.Where(summary => summary.TerritoryId == territoryId);
            }

            var limit = Math.Max(0, query.Limit);
            return summaries
                .OrderByDescending(summary => summary.StartedAt ?? DateTimeOffset.MinValue)
                .ThenBy(summary => summary.Id.Value)
                .Take(limit)
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DeleteAsync(PullId id, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var summaries = (await LoadIndexWithRecoveryAsync(cancellationToken).ConfigureAwait(false)).ToList();

            DeleteIfExists(GetDetailPath(id));
            DeleteIfExists(GetDetailPath(id) + BackupSuffix);
            DeleteIfExists(GetDetailPath(id) + TempSuffix);

            summaries.RemoveAll(summary => summary.Id == id);
            await SaveIndexAsync(summaries, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        gate.Dispose();
    }

    private static PullSummary CreateSummary(RecordedPull pull)
    {
        return new PullSummary
        {
            Id = pull.Id,
            TerritoryId = pull.Metadata.TerritoryId,
            TerritoryName = pull.Metadata.TerritoryName,
            Duration = pull.Metadata.Duration,
            StartedAt = pull.Metadata.StartedAt,
            ActorCount = pull.Actors.Count,
            EventCount = pull.Events.Count,
            SourceKind = pull.Provenance.SourceKind,
        };
    }

    private async Task<IReadOnlyList<PullSummary>> LoadIndexWithRecoveryAsync(CancellationToken cancellationToken)
    {
        var primary = await TryLoadIndexFileAsync(indexPath, cancellationToken).ConfigureAwait(false);
        if (primary is not null)
        {
            return primary;
        }

        var backup = await TryLoadIndexFileAsync(indexPath + BackupSuffix, cancellationToken).ConfigureAwait(false);
        if (backup is not null)
        {
            return backup;
        }

        return await RebuildIndexFromDetailsAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<PullSummary>?> TryLoadIndexFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<CanonicalPullIndexEnvelope>(json);
            if (envelope is null || envelope.Pulls is null)
            {
                return null;
            }

            if (envelope.SchemaVersion != CurrentIndexSchemaVersion)
            {
                throw new CanonicalPullCompatibilityException(
                    $"Unsupported canonical pull index schema {envelope.SchemaVersion}; expected {CurrentIndexSchemaVersion}.");
            }

            return envelope.Pulls;
        }
        catch (CanonicalPullCompatibilityException)
        {
            throw;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<PullSummary>> RebuildIndexFromDetailsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(detailsDirectory))
        {
            return Array.Empty<PullSummary>();
        }

        var summaries = new List<PullSummary>();
        foreach (var detailPath in Directory.EnumerateFiles(detailsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pull = await TryLoadPullFileAsync(detailPath, expectedId: null, cancellationToken).ConfigureAwait(false);
            if (pull is not null)
            {
                summaries.Add(CreateSummary(pull));
            }
        }

        SortSummaries(summaries);
        return summaries;
    }

    private static async Task<RecordedPull?> TryLoadPullFileAsync(
        string path,
        PullId? expectedId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var pull = CanonicalPullSerializer.Deserialize(json);
            return expectedId is null || pull.Id == expectedId.Value
                ? pull
                : null;
        }
        catch (CanonicalPullCompatibilityException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task SaveIndexAsync(IReadOnlyList<PullSummary> summaries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rootDirectory);
        var json = JsonSerializer.Serialize(
            new CanonicalPullIndexEnvelope(CurrentIndexSchemaVersion, summaries.ToList()));
        await WriteAtomicAsync(indexPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + TempSuffix;
        var backupPath = path + BackupSuffix;
        var bytes = Encoding.UTF8.GetBytes(content);

        await using (var stream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
        {
            File.Copy(path, backupPath, overwrite: true);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static void SortSummaries(List<PullSummary> summaries)
    {
        summaries.Sort((left, right) =>
        {
            var timeComparison = Nullable.Compare(right.StartedAt, left.StartedAt);
            return timeComparison != 0
                ? timeComparison
                : left.Id.Value.CompareTo(right.Id.Value);
        });
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetDetailPath(PullId id)
    {
        return Path.Combine(detailsDirectory, $"{id.Value:N}.json");
    }

    private sealed record CanonicalPullIndexEnvelope(int SchemaVersion, List<PullSummary>? Pulls);
}
