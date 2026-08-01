namespace BetterDeaths;

internal static class GoofyDeathText
{
    public const string FatalEventName = "Skill Issue";

    private static readonly string[] SlangTerms =
    [
        "Ligma",
        "Sugma",
        "Drillma",
        "Bofa",
        "Bophades",
        "Bophedes",
        "Sugondese",
        "Ligondese",
        "Ugondese",
        "Grabahan",
        "SawCon",
        "Sakon",
        "Sakkon",
        "Suckon",
        "Sekon",
        "Gargalon",
        "Mind Goblin",
        "Goblin",
        "Gulpin",
        "Chewons",
        "E10",
        "Eaton",
        "Bofa Dee",
        "Sergeant Botha",
        "DN",
        "SoDN",
        "UCD",
        "CDs",
        "Dee",
        "Dees",
        "Deez",
        "Grabba",
        "Bophadese",
    ];

    private static readonly string[] PostmortemLines =
    [
        "Postmortem: Felfor the oldest mechanic in the book.",
        "Postmortem: Fitness? That damage into the HP bar was ambitious.",
        "Postmortem: Imagine draggin' that hit into this pull.",
        "Postmortem: Botha HP and shields have filed for leave.",
        "Postmortem: Norway that was surviving.",
        "Postmortem: Check these combat logs.",
        "Postmortem: What came in the mail? This death recap.",
        "Postmortem: Leaving the arena was not the mitigation plan.",
        "Postmortem: Cravin' a cleaner pull.",
        "Postmortem: Eaton damage for breakfast.",
    ];

    public static string GetSlangTerm(uint actionId, string? actionName)
    {
        var stableValue = actionId != 0
            ? actionId
            : GetStableTextHash(actionName);
        return SlangTerms[stableValue % SlangTerms.Length];
    }

    public static string FormatFatalEventName(string actualActionName)
    {
        return FormatAliasedActionName(FatalEventName, actualActionName);
    }

    public static string FormatLeadUpEventName(uint actionId, string actualActionName)
    {
        return FormatAliasedActionName(GetSlangTerm(actionId, actualActionName), actualActionName);
    }

    public static string GetPostmortemLine(long deathTicks, string? memberKey)
    {
        var stableValue = unchecked((uint)deathTicks) ^
            unchecked((uint)(deathTicks >> 32)) ^
            GetStableTextHash(memberKey);
        return PostmortemLines[stableValue % PostmortemLines.Length];
    }

    private static string FormatAliasedActionName(string alias, string actualActionName)
    {
        return string.IsNullOrWhiteSpace(actualActionName)
            ? alias
            : $"{alias} ({actualActionName})";
    }

    private static uint GetStableTextHash(string? value)
    {
        var hash = 2166136261u;
        foreach (var character in value ?? string.Empty)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash;
    }
}
