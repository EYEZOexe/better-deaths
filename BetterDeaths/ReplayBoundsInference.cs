namespace BetterDeaths;

using System;
using System.Collections.Generic;

internal readonly record struct ReplayWorldBounds(float MinX, float MaxX, float MinZ, float MaxZ);

internal static class ReplayBoundsInference
{
    private const float DefaultCenterX = 100.0f;
    private const float DefaultCenterZ = 100.0f;
    private const float DefaultHalfRange = 20.0f;
    private const float DefaultNeighborhoodOverflow = 5.0f;
    private const float DefaultCenterTolerance = 3.0f;

    public static bool TryInfer(
        IReadOnlyList<ReplayPositionSnapshot> positions,
        IReadOnlyList<ReplayMechanicSnapshot> mechanics,
        IReadOnlyList<ReplayWorldMarkerSnapshot> worldMarkers,
        out ReplayWorldBounds bounds)
    {
        var hasBounds = false;
        var hasObservedPosition = false;
        var minX = 0.0f;
        var maxX = 0.0f;
        var minZ = 0.0f;
        var maxZ = 0.0f;

        foreach (var position in positions)
        {
            if (!ShouldUsePosition(position))
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

    private static bool ShouldUsePosition(ReplayPositionSnapshot position)
    {
        return float.IsFinite(position.X) &&
            float.IsFinite(position.Z) &&
            (position.ActorKind != ReplayActorKind.Enemy || position.IsTargetable);
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
}
