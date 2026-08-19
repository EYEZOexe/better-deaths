namespace BetterDeaths.Analysis.Jobs.Dancer;

using System;

// Data provenance:
// xivanalysis/xivanalysis @ f90bfac9ad9984354437b83e529f5dd709346413 (dawntrail)
// - src/data/ACTIONS/root/DNC.ts
// - src/data/STATUSES/root/DNC.ts
// See THIRD_PARTY_NOTICES.md for the MIT attribution.
internal static class DancerJobDefinition
{
    public const string JobKey = "dnc";
    public const string JobAbbreviation = "DNC";
    public const string StandardStepCooldownGroup = "standard-step-group";

    public const string Cascade = "cascade";
    public const string Fountain = "fountain";
    public const string ReverseCascade = "reverse-cascade";
    public const string Fountainfall = "fountainfall";
    public const string Windmill = "windmill";
    public const string Bladeshower = "bladeshower";
    public const string RisingWindmill = "rising-windmill";
    public const string Bloodshower = "bloodshower";
    public const string StandardStep = "standard-step";
    public const string TechnicalStep = "technical-step";
    public const string Emboite = "emboite";
    public const string Entrechat = "entrechat";
    public const string Jete = "jete";
    public const string Pirouette = "pirouette";
    public const string StandardFinish = "standard-finish";
    public const string SingleStandardFinish = "single-standard-finish";
    public const string DoubleStandardFinish = "double-standard-finish";
    public const string TechnicalFinish = "technical-finish";
    public const string SingleTechnicalFinish = "single-technical-finish";
    public const string DoubleTechnicalFinish = "double-technical-finish";
    public const string TripleTechnicalFinish = "triple-technical-finish";
    public const string QuadrupleTechnicalFinish = "quadruple-technical-finish";
    public const string SaberDance = "saber-dance";
    public const string ClosedPosition = "closed-position";
    public const string Ending = "ending";
    public const string Devilment = "devilment";
    public const string Flourish = "flourish";
    public const string Tillana = "tillana";
    public const string FanDanceIII = "fan-dance-iii";
    public const string FanDanceIV = "fan-dance-iv";
    public const string StarfallDance = "starfall-dance";
    public const string LastDance = "last-dance";
    public const string FinishingMove = "finishing-move";
    public const string DanceOfTheDawn = "dance-of-the-dawn";

    public const string SilkenSymmetry = "silken-symmetry";
    public const string SilkenFlow = "silken-flow";
    public const string FlourishingSymmetry = "flourishing-symmetry";
    public const string FlourishingFlow = "flourishing-flow";
    public const string ThreefoldFanDance = "threefold-fan-dance";
    public const string FourfoldFanDance = "fourfold-fan-dance";
    public const string StandardStepStatus = "standard-step-status";
    public const string TechnicalStepStatus = "technical-step-status";
    public const string StandardFinishStatus = "standard-finish-status";
    public const string TechnicalFinishStatus = "technical-finish-status";
    public const string ClosedPositionStatus = "closed-position-status";
    public const string DancePartnerStatus = "dance-partner-status";
    public const string StandardFinishPartnerStatus = "standard-finish-partner-status";
    public const string DevilmentStatus = "devilment-status";
    public const string FinishingMoveReady = "finishing-move-ready";
    public const string LastDanceReady = "last-dance-ready";
    public const string FlourishingStarfall = "flourishing-starfall";
    public const string DanceOfTheDawnReady = "dance-of-the-dawn-ready";

    public static JobDefinition Definition { get; } = new(
        JobKey,
        "Dancer",
        JobAbbreviation,
        actions:
        [
            Gcd(Cascade, 15989),
            Gcd(Fountain, 15990),
            Gcd(ReverseCascade, 15991),
            Gcd(Fountainfall, 15992),
            Gcd(Windmill, 15993),
            Gcd(Bladeshower, 15994),
            Gcd(RisingWindmill, 15995),
            Gcd(Bloodshower, 15996),
            Gcd(StandardStep, 15997, TimeSpan.FromSeconds(30), StandardStepCooldownGroup),
            Gcd(TechnicalStep, 15998, TimeSpan.FromSeconds(120)),
            Gcd(Emboite, 15999),
            Gcd(Entrechat, 16000),
            Gcd(Jete, 16001),
            Gcd(Pirouette, 16002),
            Gcd(StandardFinish, 16003),
            Gcd(SingleStandardFinish, 16191),
            Gcd(DoubleStandardFinish, 16192),
            Gcd(TechnicalFinish, 16004),
            Gcd(SingleTechnicalFinish, 16193),
            Gcd(DoubleTechnicalFinish, 16194),
            Gcd(TripleTechnicalFinish, 16195),
            Gcd(QuadrupleTechnicalFinish, 16196),
            Gcd(SaberDance, 16005),
            Ogcd(ClosedPosition, 16006, TimeSpan.FromSeconds(30)),
            Ogcd(Ending, 18073, TimeSpan.FromSeconds(1)),
            Ogcd(Devilment, 16011, TimeSpan.FromSeconds(120)),
            Ogcd(Flourish, 16013, TimeSpan.FromSeconds(60)),
            Gcd(Tillana, 25790),
            Ogcd(FanDanceIII, 16009, TimeSpan.FromSeconds(1)),
            Ogcd(FanDanceIV, 25791, TimeSpan.FromSeconds(1)),
            Gcd(StarfallDance, 25792),
            Gcd(LastDance, 36983),
            Gcd(FinishingMove, 36984, TimeSpan.FromSeconds(30), StandardStepCooldownGroup),
            Gcd(DanceOfTheDawn, 36985),
        ],
        statuses:
        [
            Status(SilkenSymmetry, 2693, TimeSpan.FromSeconds(30)),
            Status(SilkenFlow, 2694, TimeSpan.FromSeconds(30)),
            Status(FlourishingSymmetry, 3017, TimeSpan.FromSeconds(30)),
            Status(FlourishingFlow, 3018, TimeSpan.FromSeconds(30)),
            Status(ThreefoldFanDance, 1820, TimeSpan.FromSeconds(30)),
            Status(FourfoldFanDance, 2699, TimeSpan.FromSeconds(30)),
            Status(StandardStepStatus, 1818, TimeSpan.FromSeconds(15)),
            Status(TechnicalStepStatus, 1819, TimeSpan.FromSeconds(15)),
            Status(StandardFinishStatus, 1821, TimeSpan.FromSeconds(60)),
            Status(TechnicalFinishStatus, 1822, TimeSpan.FromSeconds(20)),
            Status(ClosedPositionStatus, 1823, null),
            Status(DancePartnerStatus, 1824, null),
            Status(StandardFinishPartnerStatus, 2105, TimeSpan.FromSeconds(60)),
            Status(DevilmentStatus, 1825, TimeSpan.FromSeconds(20)),
            Status(FinishingMoveReady, 3868, TimeSpan.FromSeconds(30)),
            Status(LastDanceReady, 3867, TimeSpan.FromSeconds(30)),
            Status(FlourishingStarfall, 2700, TimeSpan.FromSeconds(20)),
            Status(DanceOfTheDawnReady, 3869, TimeSpan.FromSeconds(30)),
        ]);

    private static JobActionDefinition Gcd(
        string key,
        uint actionId,
        TimeSpan? cooldown = null,
        string? cooldownGroupKey = null)
    {
        return new JobActionDefinition
        {
            Key = key,
            ActionId = actionId,
            IsGcd = true,
            Cooldown = cooldown,
            CooldownGroupKey = cooldownGroupKey,
        };
    }

    private static JobActionDefinition Ogcd(
        string key,
        uint actionId,
        TimeSpan? cooldown = null,
        string? cooldownGroupKey = null)
    {
        return new JobActionDefinition
        {
            Key = key,
            ActionId = actionId,
            IsGcd = false,
            Cooldown = cooldown,
            CooldownGroupKey = cooldownGroupKey,
        };
    }

    private static JobStatusDefinition Status(string key, uint statusId, TimeSpan? duration)
    {
        return new JobStatusDefinition
        {
            Key = key,
            StatusId = statusId,
            Duration = duration,
        };
    }
}
