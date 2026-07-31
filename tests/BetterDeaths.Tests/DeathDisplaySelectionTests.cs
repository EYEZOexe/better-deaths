namespace BetterDeaths;

public sealed class DeathDisplaySelectionTests
{
    [Fact]
    public void SelectIncludesEntireAtomicMultiHitBurst()
    {
        var burstAtUtc = new DateTime(2026, 7, 31, 2, 53, 12, DateTimeKind.Utc);
        var damageAmounts = new uint[] { 4_803, 36_999, 37_401, 37_633, 36_076, 39_262, 37_844 };
        var recentEvents = damageAmounts
            .Select((amount, index) => CreateDamageEvent(
                burstAtUtc.AddTicks(index * TimeSpan.TicksPerMillisecond / 10),
                amount,
                (uint)(index + 1)))
            .Prepend(CreateDamageEvent(burstAtUtc.AddSeconds(-1), 9_999, 100))
            .ToList();
        var logEvents = damageAmounts
            .Select((amount, index) => CreateLogEvent(
                burstAtUtc.AddMilliseconds(570).AddTicks(index * TimeSpan.TicksPerMillisecond / 10),
                amount))
            .Prepend(CreateLogEvent(burstAtUtc.AddSeconds(-1), 9_999))
            .Append(CreateLogEvent(burstAtUtc.AddMilliseconds(571), 1_052))
            .ToList();
        var lastAlive = new HpHistorySnapshot(
            burstAtUtc.AddMilliseconds(760),
            519.7f,
            115_378,
            18_119,
            226_488,
            []);
        var death = new PartyDeathRecord(
            burstAtUtc.AddMilliseconds(900),
            519.9f,
            "nai-la",
            "Nai La",
            6,
            23,
            "BRD",
            0,
            0,
            226_488,
            recentEvents[^1],
            recentEvents,
            [lastAlive],
            [])
        {
            FatalSequence = new FatalSequenceRecord(
                burstAtUtc.AddMilliseconds(-750),
                burstAtUtc.AddMilliseconds(1_400),
                lastAlive,
                [],
                logEvents),
        };

        var selection = DeathDisplaySelector.Select(death);
        var fatalGroup = Assert.Single(selection.FatalEvents);

        Assert.Equal(230_018UL, fatalGroup.Amount);
        Assert.Equal(226_488u, selection.Snapshot?.CurrentHp);
        Assert.Equal(3_530UL, fatalGroup.Amount - selection.Snapshot!.CurrentHp);
    }

    private static CombatEventRecord CreateDamageEvent(DateTime seenAtUtc, uint amount, uint ordinal)
    {
        return new CombatEventRecord(
            seenAtUtc,
            519.0f,
            "nai-la",
            "Nai La",
            6,
            0x4000_001F,
            "Chaos",
            47_864,
            "Cyclone",
            405,
            DeathEventKind.Damage,
            amount,
            226_488,
            38_503,
            226_488,
            DamageType.Magic,
            false,
            false,
            false,
            false,
            string.Empty,
            [],
            [])
        {
            EventIdentity = $"cyclone:{ordinal}",
            EventOrdinal = ordinal,
            HpSource = CombatEventHpSource.DirectCombatEventSnapshot,
        };
    }

    private static CombatLogEventRecord CreateLogEvent(DateTime seenAtUtc, uint amount)
    {
        return new CombatLogEventRecord(
            seenAtUtc,
            519.5f,
            "nai-la",
            "Nai La",
            6,
            "Chaos",
            "Nai La",
            510,
            "Cyclone",
            amount);
    }
}
