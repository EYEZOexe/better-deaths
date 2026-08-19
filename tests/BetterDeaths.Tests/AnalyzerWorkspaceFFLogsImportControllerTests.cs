namespace BetterDeaths;

using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using BetterDeaths.Sources;
using BetterDeaths.Sources.FFLogs;
using BetterDeaths.Windows.Analyzer;

public sealed class AnalyzerWorkspaceFFLogsImportControllerTests
{
    [Fact]
    public async Task ReportSelectionProjectsSourceDataIntoWorkspaceChoices()
    {
        var source = new FakeSource
        {
            ReportResult = FFLogsReportSelectionResult.Success(new FFLogsReportSelection
            {
                ReportCode = "REPORT123",
                Fights =
                [
                    new FFLogsFightSelection
                    {
                        FightId = 2,
                        Name = "Second Pull",
                        Start = TimeSpan.FromSeconds(30),
                        Duration = TimeSpan.FromSeconds(90),
                        Kill = false,
                    },
                ],
            }),
        };
        using var controller = new AnalyzerWorkspaceFFLogsImportController(source, new MemoryStore());

        var result = await controller.LoadReportAsync("  REPORT123  ");

        Assert.True(result.Applied);
        Assert.False(result.Snapshot.IsBusy);
        Assert.Null(result.Snapshot.Error);
        Assert.Equal("REPORT123", result.Snapshot.Report?.ReportCode);
        var fight = Assert.Single(result.Snapshot.Report!.Fights);
        Assert.Equal(2, fight.FightId);
        Assert.Equal("Second Pull", fight.Name);
        Assert.Equal("REPORT123", source.LastReportCode);
    }

    [Fact]
    public async Task SuccessfulImportSavesCanonicalPullAndReturnsSelectionId()
    {
        var pull = Pull(new PullId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));
        var source = new FakeSource { PullResult = PullImportResult.Success(pull) };
        var store = new MemoryStore();
        using var controller = new AnalyzerWorkspaceFFLogsImportController(source, store);

        var result = await controller.ImportFightAsync("REPORT123", 42);

        Assert.True(result.Applied);
        Assert.Equal(pull.Id, result.Snapshot.ImportedPullId);
        Assert.Equal(pull, await store.LoadAsync(pull.Id));
        Assert.Equal(("REPORT123", 42), source.LastFightRequest);
    }

    [Fact]
    public async Task IntegrationErrorRemainsSeparateAndUsesOnlySafeMessage()
    {
        var source = new FakeSource
        {
            PullResult = PullImportResult.Failure(new PullImportError
            {
                Category = PullImportErrorCategory.Authorization,
                Code = "fflogs.private_report_unavailable",
                SafeMessage = "This report is unavailable with the current authorization.",
            }),
        };
        using var controller = new AnalyzerWorkspaceFFLogsImportController(source, new MemoryStore());

        var result = await controller.ImportFightAsync("PRIVATE", 3);

        Assert.True(result.Applied);
        Assert.Null(result.Snapshot.ImportedPullId);
        Assert.Equal(PullImportErrorCategory.Authorization, result.Snapshot.Error?.Category);
        Assert.Equal("fflogs.private_report_unavailable", result.Snapshot.Error?.Code);
        Assert.DoesNotContain("token", result.Snapshot.Error?.SafeMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NewerReportRequestPreventsStaleCompletionFromReplacingSnapshot()
    {
        var source = new ControlledSource();
        using var controller = new AnalyzerWorkspaceFFLogsImportController(source, new MemoryStore());

        var first = controller.LoadReportAsync("OLD");
        await source.FirstStarted.Task;
        var second = controller.LoadReportAsync("NEW");
        await source.SecondStarted.Task;

        source.SecondCompletion.SetResult(Report("NEW"));
        var secondResult = await second;
        source.FirstCompletion.SetResult(Report("OLD"));
        var firstResult = await first;

        Assert.True(secondResult.Applied);
        Assert.False(firstResult.Applied);
        Assert.Equal("NEW", controller.Snapshot.Report?.ReportCode);
    }

    [Fact]
    public async Task CancellationDoesNotAllowCancelledOperationToReplaceNewerState()
    {
        var source = new ControlledSource();
        using var controller = new AnalyzerWorkspaceFFLogsImportController(source, new MemoryStore());

        var first = controller.LoadReportAsync("OLD");
        await source.FirstStarted.Task;
        controller.Cancel();

        source.FirstCompletion.SetResult(Report("OLD"));
        var result = await first;

        Assert.False(result.Applied);
        Assert.Null(controller.Snapshot.Report);
        Assert.False(controller.Snapshot.IsBusy);
    }

    [Fact]
    public async Task UnexpectedSourceExceptionBecomesGenericSafeWorkspaceError()
    {
        var source = new ThrowingSource("SUPER_SECRET_VALUE");
        using var controller = new AnalyzerWorkspaceFFLogsImportController(source, new MemoryStore());

        var result = await controller.LoadReportAsync("REPORT123");

        Assert.Equal(PullImportErrorCategory.Unavailable, result.Snapshot.Error?.Category);
        Assert.DoesNotContain("SUPER_SECRET_VALUE", result.Snapshot.Error?.SafeMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidRequestsDoNotInvokeSource()
    {
        var source = new FakeSource();
        using var controller = new AnalyzerWorkspaceFFLogsImportController(source, new MemoryStore());

        var report = await controller.LoadReportAsync("   ");
        var fight = await controller.ImportFightAsync("REPORT", 0);

        Assert.Equal(PullImportErrorCategory.InvalidRequest, report.Snapshot.Error?.Category);
        Assert.Equal(PullImportErrorCategory.InvalidRequest, fight.Snapshot.Error?.Category);
        Assert.Equal(0, source.ReportCalls);
        Assert.Equal(0, source.PullCalls);
    }

    private static FFLogsReportSelectionResult Report(string code)
    {
        return FFLogsReportSelectionResult.Success(new FFLogsReportSelection
        {
            ReportCode = code,
            Fights = [],
        });
    }

    private static RecordedPull Pull(PullId id)
    {
        return new RecordedPull
        {
            Id = id,
            Metadata = new PullMetadata
            {
                TerritoryId = 1,
                TerritoryName = "Imported",
                Duration = TimeSpan.FromSeconds(60),
            },
            SchemaVersion = new PullSchemaVersion(1),
            Actors = [],
            Events = [],
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.FFLogs,
                SourceReference = "fflogs:fixture",
            },
        };
    }

    private sealed class FakeSource : IFFLogsImportSource
    {
        public FFLogsReportSelectionResult ReportResult { get; init; } = Report("REPORT123");

        public PullImportResult PullResult { get; init; } = PullImportResult.Success(Pull(PullId.New()));

        public int ReportCalls { get; private set; }

        public int PullCalls { get; private set; }

        public string? LastReportCode { get; private set; }

        public (string Code, int FightId)? LastFightRequest { get; private set; }

        public ValueTask<FFLogsReportSelectionResult> LoadReportSelectionAsync(string reportCode, CancellationToken cancellationToken = default)
        {
            ReportCalls++;
            LastReportCode = reportCode;
            return ValueTask.FromResult(ReportResult);
        }

        public ValueTask<PullImportResult> LoadPullAsync(string reportCode, int fightId, CancellationToken cancellationToken = default)
        {
            PullCalls++;
            LastFightRequest = (reportCode, fightId);
            return ValueTask.FromResult(PullResult);
        }
    }

    private sealed class ControlledSource : IFFLogsImportSource
    {
        private int reportCallCount;

        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<FFLogsReportSelectionResult> FirstCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<FFLogsReportSelectionResult> SecondCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<FFLogsReportSelectionResult> LoadReportSelectionAsync(string reportCode, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref reportCallCount);
            if (call == 1)
            {
                FirstStarted.SetResult();
                return await FirstCompletion.Task;
            }

            SecondStarted.SetResult();
            return await SecondCompletion.Task;
        }

        public ValueTask<PullImportResult> LoadPullAsync(string reportCode, int fightId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingSource(string message) : IFFLogsImportSource
    {
        public ValueTask<FFLogsReportSelectionResult> LoadReportSelectionAsync(string reportCode, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }

        public ValueTask<PullImportResult> LoadPullAsync(string reportCode, int fightId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class MemoryStore : IPullStore
    {
        private readonly Dictionary<PullId, RecordedPull> pulls = [];

        public Task SaveAsync(RecordedPull pull, CancellationToken cancellationToken = default)
        {
            pulls[pull.Id] = pull;
            return Task.CompletedTask;
        }

        public Task<RecordedPull?> LoadAsync(PullId id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(pulls.TryGetValue(id, out var pull) ? pull : null);
        }

        public Task<IReadOnlyList<PullSummary>> QueryAsync(PullQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PullSummary>>([]);
        }

        public Task DeleteAsync(PullId id, CancellationToken cancellationToken = default)
        {
            pulls.Remove(id);
            return Task.CompletedTask;
        }
    }
}
