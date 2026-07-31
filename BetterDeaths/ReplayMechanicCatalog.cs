namespace BetterDeaths;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal enum ReplayMechanicAnchor
{
    Source,
    Target,
}

internal enum ReplayCatalogIdentifierKind
{
    Object,
    Action,
    Status,
    Icon,
    Tether,
}

internal readonly record struct ReplayMechanicGeometry(
    ReplayMechanicShape Shape,
    float Radius = 0.0f,
    float Length = 0.0f,
    float Width = 0.0f,
    float AngleDegrees = 0.0f);

internal readonly record struct ReplayCatalogAction(
    uint TerritoryId,
    string EncounterName,
    uint ActionId,
    string Name,
    ReplayMechanicGeometry? Geometry,
    ReplayMechanicAnchor Anchor);

internal readonly record struct ReplayCatalogIdentifier(
    uint TerritoryId,
    string EncounterName,
    ReplayCatalogIdentifierKind Kind,
    uint Id,
    string Name);

internal readonly record struct ResolvedReplayMechanic(
    string Label,
    ReplayMechanicGeometry Geometry,
    ReplayMechanicAnchor Anchor,
    bool IsKnown,
    string Provenance);

internal static class ReplayMechanicCatalog
{
    internal const string ActionSheetProvenance = "FFXIV Action sheet";

    public static bool TryResolve(
        uint territoryId,
        uint actionId,
        string actionName,
        byte castType,
        byte effectRange,
        byte xAxisModifier,
        sbyte range,
        bool targetArea,
        out ResolvedReplayMechanic mechanic)
    {
        if (BossModUltimateCatalog.TryGetAction(territoryId, actionId, out var catalogAction) &&
            catalogAction.Geometry is { } catalogGeometry)
        {
            mechanic = new ResolvedReplayMechanic(
                ResolveLabel(actionName, catalogAction.Name, actionId),
                catalogGeometry,
                catalogAction.Anchor,
                true,
                BossModUltimateCatalog.SourceProvenance);
            return true;
        }

        if (TryInferActionSheetGeometry(castType, effectRange, xAxisModifier, range, targetArea, out var sheetGeometry, out var anchor))
        {
            var fallbackName = BossModUltimateCatalog.TryGetAction(territoryId, actionId, out catalogAction)
                ? catalogAction.Name
                : string.Empty;
            mechanic = new ResolvedReplayMechanic(
                ResolveLabel(actionName, fallbackName, actionId),
                sheetGeometry,
                anchor,
                false,
                ActionSheetProvenance);
            return true;
        }

        mechanic = default;
        return false;
    }

    public static bool TryInferActionSheetGeometry(
        byte castType,
        byte effectRange,
        byte xAxisModifier,
        sbyte range,
        bool targetArea,
        out ReplayMechanicGeometry geometry,
        out ReplayMechanicAnchor anchor)
    {
        anchor = ReplayMechanicAnchor.Source;
        if (effectRange == 0)
        {
            geometry = default;
            return false;
        }

        switch (castType)
        {
            case 2:
            case 5:
            case 7:
                anchor = targetArea || castType == 7 || range != 0
                    ? ReplayMechanicAnchor.Target
                    : ReplayMechanicAnchor.Source;
                geometry = new ReplayMechanicGeometry(
                    ReplayMechanicShape.Circle,
                    Radius: effectRange + xAxisModifier);
                return true;
            case 3:
                geometry = new ReplayMechanicGeometry(
                    ReplayMechanicShape.Cone,
                    Radius: effectRange,
                    Length: effectRange,
                    AngleDegrees: 120.0f);
                return true;
            case 13:
                geometry = new ReplayMechanicGeometry(
                    ReplayMechanicShape.Cone,
                    Radius: effectRange,
                    Length: effectRange,
                    AngleDegrees: 90.0f);
                return true;
            case 4:
            case 12:
                geometry = new ReplayMechanicGeometry(
                    ReplayMechanicShape.Line,
                    Length: effectRange,
                    Width: Math.Max(1.0f, xAxisModifier));
                return true;
            default:
                geometry = default;
                return false;
        }
    }

    public static string ResolveLabel(string actionName, string catalogName, uint actionId)
    {
        if (!string.IsNullOrWhiteSpace(actionName) &&
            !actionName.StartsWith("_rsv_", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionName, "Unknown action", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionName, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return actionName;
        }

        if (!string.IsNullOrWhiteSpace(catalogName))
        {
            return HumanizeIdentifier(catalogName);
        }

        return $"Action {actionId}";
    }

    internal static string HumanizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return string.Empty;
        }

        var result = new StringBuilder(identifier.Length + 8);
        for (var index = 0; index < identifier.Length; index++)
        {
            var current = identifier[index];
            if (index > 0 &&
                ((char.IsUpper(current) && (char.IsLower(identifier[index - 1]) || char.IsDigit(identifier[index - 1]))) ||
                    (char.IsDigit(current) && !char.IsDigit(identifier[index - 1]))))
            {
                result.Append(' ');
            }

            result.Append(current == '_' ? ' ' : current);
        }

        return result.ToString().Trim();
    }
}

internal static partial class BossModUltimateCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<ulong, ReplayCatalogAction>> ActionsByKey = new(BuildActionLookup);
    private static readonly Lazy<IReadOnlyDictionary<
        (uint TerritoryId, ReplayCatalogIdentifierKind Kind, uint Id),
        ReplayCatalogIdentifier[]>> IdentifiersByKey = new(BuildIdentifierLookup);

    public static int ActionCount => GeneratedActions.Length;

    public static int IdentifierCount => GeneratedIdentifiers.Length;

    public static IReadOnlyList<string> EncounterNames => GeneratedIdentifiers
        .Select(identifier => identifier.EncounterName)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    public static bool TryGetAction(uint territoryId, uint actionId, out ReplayCatalogAction action)
    {
        return ActionsByKey.Value.TryGetValue(BuildKey(territoryId, actionId), out action);
    }

    public static IReadOnlyList<ReplayCatalogIdentifier> FindIdentifiers(
        uint territoryId,
        ReplayCatalogIdentifierKind kind,
        uint id)
    {
        return IdentifiersByKey.Value.TryGetValue((territoryId, kind, id), out var identifiers)
            ? identifiers
            : Array.Empty<ReplayCatalogIdentifier>();
    }

    private static IReadOnlyDictionary<ulong, ReplayCatalogAction> BuildActionLookup()
    {
        var result = new Dictionary<ulong, ReplayCatalogAction>();
        foreach (var action in GeneratedActions)
        {
            var key = BuildKey(action.TerritoryId, action.ActionId);
            if (!result.TryGetValue(key, out var existing) ||
                (existing.Geometry is null && action.Geometry is not null))
            {
                result[key] = action;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<
        (uint TerritoryId, ReplayCatalogIdentifierKind Kind, uint Id),
        ReplayCatalogIdentifier[]> BuildIdentifierLookup()
    {
        return GeneratedIdentifiers
            .GroupBy(identifier => (identifier.TerritoryId, identifier.Kind, identifier.Id))
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static ulong BuildKey(uint territoryId, uint actionId)
    {
        return ((ulong)territoryId << 32) | actionId;
    }
}
