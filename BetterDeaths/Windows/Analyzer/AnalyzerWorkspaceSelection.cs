namespace BetterDeaths.Windows.Analyzer;

using BetterDeaths.Domain;
using System;

internal sealed class AnalyzerWorkspaceSelection
{
    private PullId? selectedPullId;
    private ActorId? selectedActorId;
    private TimeRange? selectedTimeRange;
    private AnalysisResultId? selectedAnalysisResultId;
    private string? selectedMechanicOccurrenceId;

    public event Action? Changed;

    public long Version { get; private set; }

    public PullId? SelectedPullId => selectedPullId;

    public ActorId? SelectedActorId => selectedActorId;

    public TimeRange? SelectedTimeRange => selectedTimeRange;

    public AnalysisResultId? SelectedAnalysisResultId => selectedAnalysisResultId;

    public string? SelectedMechanicOccurrenceId => selectedMechanicOccurrenceId;

    public void SelectPull(PullId? pullId)
    {
        if (selectedPullId == pullId)
        {
            return;
        }

        selectedPullId = pullId;
        selectedActorId = null;
        selectedTimeRange = null;
        selectedAnalysisResultId = null;
        selectedMechanicOccurrenceId = null;
        NotifyChanged();
    }

    public void SelectActor(ActorId? actorId)
    {
        if (selectedActorId == actorId)
        {
            return;
        }

        selectedActorId = actorId;
        NotifyChanged();
    }

    public void SelectTime(TimeRange? timeRange)
    {
        if (selectedTimeRange == timeRange)
        {
            return;
        }

        selectedTimeRange = timeRange;
        NotifyChanged();
    }

    public void SelectMechanicOccurrence(string? occurrenceId)
    {
        var normalized = string.IsNullOrWhiteSpace(occurrenceId)
            ? null
            : occurrenceId.Trim();
        if (string.Equals(selectedMechanicOccurrenceId, normalized, StringComparison.Ordinal))
        {
            return;
        }

        selectedMechanicOccurrenceId = normalized;
        NotifyChanged();
    }

    public void SelectResult(AnalysisResult? result)
    {
        if (result is null)
        {
            if (selectedAnalysisResultId is null)
            {
                return;
            }

            selectedAnalysisResultId = null;
            NotifyChanged();
            return;
        }

        var actorId = result.Actors.Count > 0
            ? result.Actors[0]
            : (ActorId?)null;
        var changed = selectedAnalysisResultId != result.Id ||
            selectedActorId != actorId ||
            selectedTimeRange != result.TimeRange;

        if (!changed)
        {
            return;
        }

        selectedAnalysisResultId = result.Id;
        selectedActorId = actorId;
        selectedTimeRange = result.TimeRange;
        NotifyChanged();
    }

    public void ClearContext()
    {
        if (selectedActorId is null &&
            selectedTimeRange is null &&
            selectedAnalysisResultId is null &&
            selectedMechanicOccurrenceId is null)
        {
            return;
        }

        selectedActorId = null;
        selectedTimeRange = null;
        selectedAnalysisResultId = null;
        selectedMechanicOccurrenceId = null;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Version++;
        Changed?.Invoke();
    }
}
