namespace BetterDeaths;

using System;
using System.Collections.Generic;
using System.Linq;

internal readonly record struct ReplayWorldBounds(float MinX, float MaxX, float MinZ, float MaxZ);

internal static class ReplayBoundsInference
{
    private const float DefaultCenterX = 100.0f;
    private const float DefaultCenterZ = 100.0f;
    private const float DefaultHalfRange = 20.0f;
    private const float DefaultNeighborhoodOverflow = 5.0f;
    private const float DefaultCenterTolerance = 3.0f;
    private const float DefaultInferenceNeighborhoodHalfRange = 60.0f;
    private const float DefaultInferenceMinimumShare = 0.60f;
    private const float DefaultInferenceTrimFraction = 0.01f;
    private const float DefaultInferenceHistogramBinSize = 1.0f;
    private const int DefaultInferenceHistogramBinCount = 121;

    public static bool TryInfer(
        IReadOnlyList<ReplayPositionSnapshot> positions,
        IReadOnlyList<ReplayMechanicSnapshot> mechanics,
        IReadOnlyList<ReplayWorldMarkerSnapshot> worldMarkers,
        out ReplayWorldBounds bounds)
    {
        var hasBounds = false;
        var hasObservedPosition = false;
        var observedPositionCount = 0;
        var defaultNeighborhoodPositionCount = 0;
        var minX = 0.0f;
        var maxX = 0.0f;
        var minZ = 0.0f;
        var maxZ = 0.0f;
        Span<int> defaultXCounts = stackalloc int[DefaultInferenceHistogramBinCount];
        Span<float> defaultXMinimums = stackalloc float[DefaultInferenceHistogramBinCount];
        Span<float> defaultXMaximums = stackalloc float[DefaultInferenceHistogramBinCount];
        Span<int> defaultZCounts = stackalloc int[DefaultInferenceHistogramBinCount];
        Span<float> defaultZMinimums = stackalloc float[DefaultInferenceHistogramBinCount];
        Span<float> defaultZMaximums = stackalloc float[DefaultInferenceHistogramBinCount];
        defaultXMinimums.Fill(float.PositiveInfinity);
        defaultXMaximums.Fill(float.NegativeInfinity);
        defaultZMinimums.Fill(float.PositiveInfinity);
        defaultZMaximums.Fill(float.NegativeInfinity);
        var hasUsablePlayerPosition = positions.Any(position =>
            position.ActorKind == ReplayActorKind.Player &&
            float.IsFinite(position.X) &&
            float.IsFinite(position.Z));

        foreach (var position in positions)
        {
            if (!ShouldUsePosition(position, hasUsablePlayerPosition))
            {
                continue;
            }

            Include(
                position.X,
                position.X,
                position.Z,
                position.Z,
                ref hasBounds,
                ref minX,
                ref maxX,
                ref minZ,
                ref maxZ);
            hasObservedPosition = true;
            observedPositionCount++;
            if (IsInsideDefaultInferenceNeighborhood(position.X, position.Z))
            {
                defaultNeighborhoodPositionCount++;
                AddCoordinateToHistogram(
                    position.X,
                    DefaultCenterX - DefaultInferenceNeighborhoodHalfRange,
                    defaultXCounts,
                    defaultXMinimums,
                    defaultXMaximums);
                AddCoordinateToHistogram(
                    position.Z,
                    DefaultCenterZ - DefaultInferenceNeighborhoodHalfRange,
                    defaultZCounts,
                    defaultZMinimums,
                    defaultZMaximums);
            }
        }

        if (hasObservedPosition &&
            defaultNeighborhoodPositionCount >= observedPositionCount * DefaultInferenceMinimumShare)
        {
            var trimCount = (int)MathF.Floor(defaultNeighborhoodPositionCount * DefaultInferenceTrimFraction);
            var observedMinX = GetHistogramLowerBound(defaultXCounts, defaultXMinimums, trimCount);
            var observedMaxX = GetHistogramUpperBound(defaultXCounts, defaultXMaximums, trimCount);
            var observedMinZ = GetHistogramLowerBound(defaultZCounts, defaultZMinimums, trimCount);
            var observedMaxZ = GetHistogramUpperBound(defaultZCounts, defaultZMaximums, trimCount);
            var robustHalfRange = MathF.Max(
                DefaultHalfRange,
                MathF.Max(
                    MathF.Max(MathF.Abs(observedMinX - DefaultCenterX), MathF.Abs(observedMaxX - DefaultCenterX)),
                    MathF.Max(MathF.Abs(observedMinZ - DefaultCenterZ), MathF.Abs(observedMaxZ - DefaultCenterZ))));
            bounds = new ReplayWorldBounds(
                DefaultCenterX - robustHalfRange,
                DefaultCenterX + robustHalfRange,
                DefaultCenterZ - robustHalfRange,
                DefaultCenterZ + robustHalfRange);
            return true;
        }

        // Full-pull actor history is the stable viewport evidence. When it is absent,
        // mechanic origins and waymarks can locate the action but cannot define edges.
        if (!hasObservedPosition)
        {
            foreach (var mechanic in mechanics)
            {
                if (!float.IsFinite(mechanic.X) || !float.IsFinite(mechanic.Z))
                {
                    continue;
                }

                Include(
                    mechanic.X,
                    mechanic.X,
                    mechanic.Z,
                    mechanic.Z,
                    ref hasBounds,
                    ref minX,
                    ref maxX,
                    ref minZ,
                    ref maxZ);
            }

            foreach (var marker in worldMarkers)
            {
                if (!marker.Active ||
                    !float.IsFinite(marker.X) ||
                    !float.IsFinite(marker.Z))
                {
                    continue;
                }

                Include(
                    marker.X,
                    marker.X,
                    marker.Z,
                    marker.Z,
                    ref hasBounds,
                    ref minX,
                    ref maxX,
                    ref minZ,
                    ref maxZ);
            }
        }

        if (!hasBounds)
        {
            bounds = default;
            return false;
        }

        var observedCenterX = (minX + maxX) * 0.5f;
        var observedCenterZ = (minZ + maxZ) * 0.5f;
        var useDefaultCenter = IsCompatibleWithDefaultCenter(
            minX,
            maxX,
            minZ,
            maxZ,
            observedCenterX,
            observedCenterZ);
        var centerX = useDefaultCenter ? DefaultCenterX : observedCenterX;
        var centerZ = useDefaultCenter ? DefaultCenterZ : observedCenterZ;
        var halfRange = MathF.Max(
            DefaultHalfRange,
            MathF.Max(
                MathF.Max(MathF.Abs(minX - centerX), MathF.Abs(maxX - centerX)),
                MathF.Max(MathF.Abs(minZ - centerZ), MathF.Abs(maxZ - centerZ))));
        bounds = new ReplayWorldBounds(
            centerX - halfRange,
            centerX + halfRange,
            centerZ - halfRange,
            centerZ + halfRange);
        return true;
    }

    private static bool ShouldUsePosition(ReplayPositionSnapshot position, bool preferPlayerPositions)
    {
        return float.IsFinite(position.X) &&
            float.IsFinite(position.Z) &&
            (!preferPlayerPositions || position.ActorKind == ReplayActorKind.Player) &&
            (position.ActorKind != ReplayActorKind.Enemy || position.IsTargetable);
    }

    private static bool IsInsideDefaultInferenceNeighborhood(float x, float z)
    {
        return MathF.Abs(x - DefaultCenterX) <= DefaultInferenceNeighborhoodHalfRange &&
            MathF.Abs(z - DefaultCenterZ) <= DefaultInferenceNeighborhoodHalfRange;
    }

    private static bool IsCompatibleWithDefaultCenter(
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        float observedCenterX,
        float observedCenterZ)
    {
        var neighborhoodHalfRange = DefaultHalfRange + DefaultNeighborhoodOverflow;
        var insideDefaultNeighborhood =
            minX >= DefaultCenterX - neighborhoodHalfRange &&
            maxX <= DefaultCenterX + neighborhoodHalfRange &&
            minZ >= DefaultCenterZ - neighborhoodHalfRange &&
            maxZ <= DefaultCenterZ + neighborhoodHalfRange;
        var observedCenterNearDefault =
            MathF.Abs(observedCenterX - DefaultCenterX) <= DefaultCenterTolerance &&
            MathF.Abs(observedCenterZ - DefaultCenterZ) <= DefaultCenterTolerance;
        return insideDefaultNeighborhood || observedCenterNearDefault;
    }

    private static void Include(
        float candidateMinX,
        float candidateMaxX,
        float candidateMinZ,
        float candidateMaxZ,
        ref bool hasBounds,
        ref float minX,
        ref float maxX,
        ref float minZ,
        ref float maxZ)
    {
        if (!hasBounds)
        {
            minX = candidateMinX;
            maxX = candidateMaxX;
            minZ = candidateMinZ;
            maxZ = candidateMaxZ;
            hasBounds = true;
            return;
        }

        minX = MathF.Min(minX, candidateMinX);
        maxX = MathF.Max(maxX, candidateMaxX);
        minZ = MathF.Min(minZ, candidateMinZ);
        maxZ = MathF.Max(maxZ, candidateMaxZ);
    }

    private static void AddCoordinateToHistogram(
        float value,
        float minimum,
        Span<int> counts,
        Span<float> minimums,
        Span<float> maximums)
    {
        var index = Math.Clamp(
            (int)MathF.Floor((value - minimum) / DefaultInferenceHistogramBinSize),
            0,
            counts.Length - 1);
        counts[index]++;
        minimums[index] = MathF.Min(minimums[index], value);
        maximums[index] = MathF.Max(maximums[index], value);
    }

    private static float GetHistogramLowerBound(
        ReadOnlySpan<int> counts,
        ReadOnlySpan<float> minimums,
        int trimCount)
    {
        for (var index = 0; index < counts.Length; index++)
        {
            if (counts[index] == 0)
            {
                continue;
            }

            if (trimCount < counts[index])
            {
                return minimums[index];
            }

            trimCount -= counts[index];
        }

        return DefaultCenterX - DefaultInferenceNeighborhoodHalfRange;
    }

    private static float GetHistogramUpperBound(
        ReadOnlySpan<int> counts,
        ReadOnlySpan<float> maximums,
        int trimCount)
    {
        for (var index = counts.Length - 1; index >= 0; index--)
        {
            if (counts[index] == 0)
            {
                continue;
            }

            if (trimCount < counts[index])
            {
                return maximums[index];
            }

            trimCount -= counts[index];
        }

        return DefaultCenterX + DefaultInferenceNeighborhoodHalfRange;
    }
}
