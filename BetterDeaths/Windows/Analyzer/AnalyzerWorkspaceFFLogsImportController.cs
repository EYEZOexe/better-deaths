namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Sources;
using BetterDeaths.Sources.FFLogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal sealed record AnalyzerWorkspaceFFLogsFightChoice
{
    public required int FightId { get; init; }

    public required string Name { get; init; }

    public required TimeSpan Start { get; init; }

    public required TimeSpan Duration { get; init; }

    public bool? Kill { get; init; }
}

internal sealed record AnalyzerWorkspaceFFLogsReportChoice
{
    public required string ReportCode { get; init; }

    public required IReadOnlyList<AnalyzerWorkspaceFFLogsFightChoice> Fights { get; init; }
}

internal sealed record AnalyzerWorkspaceImportError
{
    public required PullImportErrorCategory Category { get; init; }

    public required string Code { get; init; }

    public required string SafeMessage { get; init; }

    public TimeSpan? RetryAfter { get; init; }
}

internal sealed record AnalyzerWorkspaceFFLogsImportSnapshot
{
    public required long Generation { get; init; }

    public required bool IsBusy { get; init; }

    public AnalyzerWorkspaceFFLogsReportChoice? Report { get; init; }

    public PullId? ImportedPullId { get; init; }

    public AnalyzerWorkspaceImportError? Error { get; init; }
}

internal sealed record AnalyzerWorkspaceFFLogsOperationResult
{
    public required bool Applied { get; init; }

    public required AnalyzerWorkspaceFFLogsImportSnapshot Snapshot { get; init; }
}

internal sealed class AnalyzerWorkspaceFFLogsImportController : IDisposable
{
    private readonly object gate = new();
    private readonly IFFLogsImportSource source;
    private readonly IPullStore pullStore;
    private readonly IDisposable? ownedResource;

    private CancellationTokenSource? activeRequestCts;
    private AnalyzerWorkspaceFFLogsImportSnapshot snapshot = new()
    {
        Generation = 0,
        IsBusy = false,
    };
    private bool disposed;

    public AnalyzerWorkspaceFFLogsImportController(
        IFFLogsImportSource source,
        IPullStore pullStore,
        IDisposable? ownedResource = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pullStore);
        this.source = source;
        this.pullStore = pullStore;
        this.ownedResource = ownedResource;
    }

    public AnalyzerWorkspaceFFLogsImportSnapshot Snapshot
    {
        get
        {
            lock (gate)
            {
                return snapshot;
            }
        }
    }

    public async Task<AnalyzerWorkspaceFFLogsOperationResult> LoadReportAsync(
        string reportCode,
        CancellationToken cancellationToken = default)
    {
        var sanitizedCode = reportCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sanitizedCode))
        {
            return ApplyImmediateError(new AnalyzerWorkspaceImportError
            {
                Category = PullImportErrorCategory.InvalidRequest,
                Code = "fflogs.invalid_report_code",
                SafeMessage = "Enter an FFLogs report code before loading fights.",
            });
        }

        var operation = BeginOperation(cancellationToken, clearReport: true);
        try
        {
            var result = await source
                .LoadReportSelectionAsync(sanitizedCode, operation.Token)
                .ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            if (!result.IsSuccess)
            {
                return CompleteError(operation.Generation, ToWorkspaceError(result.Error!));
            }

            var sourceReport = result.Report!;
            var report = new AnalyzerWorkspaceFFLogsReportChoice
            {
                ReportCode = sourceReport.ReportCode,
                Fights = sourceReport.Fights.Select(fight => new AnalyzerWorkspaceFFLogsFightChoice
                {
                    FightId = fight.FightId,
                    Name = fight.Name,
                    Start = fight.Start,
                    Duration = fight.Duration,
                    Kill = fight.Kill,
                }).ToArray(),
            };
            return Complete(operation.Generation, current => current with
            {
                IsBusy = false,
                Report = report,
                ImportedPullId = null,
                Error = null,
            });
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
        {
            return StaleOrCancelled(operation.Generation);
        }
        catch (Exception)
        {
            return CompleteError(operation.Generation, new AnalyzerWorkspaceImportError
            {
                Category = PullImportErrorCategory.Unavailable,
                Code = "fflogs.workspace_report_failed",
                SafeMessage = "The FFLogs report could not be loaded.",
            });
        }
        finally
        {
            operation.Dispose();
        }
    }

    public async Task<AnalyzerWorkspaceFFLogsOperationResult> ImportFightAsync(
        string reportCode,
        int fightId,
        CancellationToken cancellationToken = default)
    {
        var sanitizedCode = reportCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sanitizedCode) || fightId <= 0)
        {
            return ApplyImmediateError(new AnalyzerWorkspaceImportError
            {
                Category = PullImportErrorCategory.InvalidRequest,
                Code = "fflogs.invalid_fight_request",
                SafeMessage = "Choose a valid FFLogs report fight before importing.",
            });
        }

        var operation = BeginOperation(cancellationToken, clearReport: false);
        try
        {
            var result = await source
                .LoadPullAsync(sanitizedCode, fightId, operation.Token)
                .ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            if (!result.IsSuccess)
            {
                return CompleteError(operation.Generation, ToWorkspaceError(result.Error!));
            }

            if (!IsCurrent(operation.Generation))
            {
                return StaleOrCancelled(operation.Generation);
            }

            var pull = result.Pull!;
            await pullStore.SaveAsync(pull, operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            return Complete(operation.Generation, current => current with
            {
                IsBusy = false,
                ImportedPullId = pull.Id,
                Error = null,
            });
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
        {
            return StaleOrCancelled(operation.Generation);
        }
        catch (Exception)
        {
            return CompleteError(operation.Generation, new AnalyzerWorkspaceImportError
            {
                Category = PullImportErrorCategory.Unavailable,
                Code = "fflogs.workspace_import_failed",
                SafeMessage = "The selected FFLogs fight could not be imported.",
            });
        }
        finally
        {
            operation.Dispose();
        }
    }

    public void Cancel()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            activeRequestCts?.Cancel();
            activeRequestCts?.Dispose();
            activeRequestCts = null;
            snapshot = snapshot with
            {
                Generation = snapshot.Generation + 1,
                IsBusy = false,
            };
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            activeRequestCts?.Cancel();
            activeRequestCts?.Dispose();
            activeRequestCts = null;
        }

        ownedResource?.Dispose();
    }

    private Operation BeginOperation(CancellationToken cancellationToken, bool clearReport)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeRequestCts?.Cancel();
            activeRequestCts?.Dispose();
            activeRequestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var generation = snapshot.Generation + 1;
            snapshot = snapshot with
            {
                Generation = generation,
                IsBusy = true,
                Report = clearReport ? null : snapshot.Report,
                ImportedPullId = null,
                Error = null,
            };
            return new Operation(generation, activeRequestCts.Token);
        }
    }

    private AnalyzerWorkspaceFFLogsOperationResult ApplyImmediateError(AnalyzerWorkspaceImportError error)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            activeRequestCts?.Cancel();
            activeRequestCts?.Dispose();
            activeRequestCts = null;
            snapshot = snapshot with
            {
                Generation = snapshot.Generation + 1,
                IsBusy = false,
                ImportedPullId = null,
                Error = error,
            };
            return new AnalyzerWorkspaceFFLogsOperationResult { Applied = true, Snapshot = snapshot };
        }
    }

    private AnalyzerWorkspaceFFLogsOperationResult CompleteError(long generation, AnalyzerWorkspaceImportError error)
    {
        return Complete(generation, current => current with
        {
            IsBusy = false,
            ImportedPullId = null,
            Error = error,
        });
    }

    private AnalyzerWorkspaceFFLogsOperationResult Complete(
        long generation,
        Func<AnalyzerWorkspaceFFLogsImportSnapshot, AnalyzerWorkspaceFFLogsImportSnapshot> update)
    {
        lock (gate)
        {
            if (disposed || generation != snapshot.Generation)
            {
                return new AnalyzerWorkspaceFFLogsOperationResult { Applied = false, Snapshot = snapshot };
            }

            snapshot = update(snapshot);
            return new AnalyzerWorkspaceFFLogsOperationResult { Applied = true, Snapshot = snapshot };
        }
    }

    private AnalyzerWorkspaceFFLogsOperationResult StaleOrCancelled(long generation)
    {
        lock (gate)
        {
            if (!disposed && generation == snapshot.Generation)
            {
                snapshot = snapshot with { IsBusy = false };
                return new AnalyzerWorkspaceFFLogsOperationResult { Applied = true, Snapshot = snapshot };
            }

            return new AnalyzerWorkspaceFFLogsOperationResult { Applied = false, Snapshot = snapshot };
        }
    }

    private bool IsCurrent(long generation)
    {
        lock (gate)
        {
            return !disposed && generation == snapshot.Generation;
        }
    }

    private static AnalyzerWorkspaceImportError ToWorkspaceError(PullImportError error)
    {
        return new AnalyzerWorkspaceImportError
        {
            Category = error.Category,
            Code = error.Code,
            SafeMessage = error.SafeMessage,
            RetryAfter = error.RetryAfter,
        };
    }

    private readonly record struct Operation(long Generation, CancellationToken Token) : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
