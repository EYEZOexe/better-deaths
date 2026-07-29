namespace BetterDeaths;

using System;

internal enum ReplaySampleSelectionKind
{
    None,
    Previous,
    Next,
    Interpolate,
}

internal readonly record struct ReplaySampleSelection(ReplaySampleSelectionKind Kind, float Interpolation)
{
    public static ReplaySampleSelection Select(
        DateTime? previousAtUtc,
        DateTime? nextAtUtc,
        DateTime selectedAtUtc,
        TimeSpan trailingHold,
        TimeSpan leadingHold,
        TimeSpan maximumInterpolationGap)
    {
        if (previousAtUtc is null)
        {
            return nextAtUtc is { } first &&
                first >= selectedAtUtc &&
                first - selectedAtUtc <= leadingHold
                ? new ReplaySampleSelection(ReplaySampleSelectionKind.Next, 1.0f)
                : default;
        }

        var previous = previousAtUtc.Value;
        if (nextAtUtc is null || nextAtUtc.Value == previous)
        {
            return selectedAtUtc >= previous &&
                selectedAtUtc - previous <= trailingHold
                ? new ReplaySampleSelection(ReplaySampleSelectionKind.Previous, 0.0f)
                : default;
        }

        var next = nextAtUtc.Value;
        if (selectedAtUtc <= previous)
        {
            return new ReplaySampleSelection(ReplaySampleSelectionKind.Previous, 0.0f);
        }

        if (selectedAtUtc >= next)
        {
            return new ReplaySampleSelection(ReplaySampleSelectionKind.Next, 1.0f);
        }

        var previousAge = selectedAtUtc - previous;
        var nextLead = next - selectedAtUtc;
        var sampleGap = next - previous;
        if (sampleGap > maximumInterpolationGap)
        {
            if (previousAge <= trailingHold)
            {
                return new ReplaySampleSelection(ReplaySampleSelectionKind.Previous, 0.0f);
            }

            return nextLead <= leadingHold
                ? new ReplaySampleSelection(ReplaySampleSelectionKind.Next, 1.0f)
                : default;
        }

        var interpolation = Math.Clamp(
            (float)(previousAge.TotalSeconds / Math.Max(0.001, sampleGap.TotalSeconds)),
            0.0f,
            1.0f);
        return new ReplaySampleSelection(ReplaySampleSelectionKind.Interpolate, interpolation);
    }
}
