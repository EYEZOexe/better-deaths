namespace BetterDeaths;

public sealed class DeathDisplaySelectionTests
{
    [Fact]
    public void GetLeadUpEventsUsesThirtySecondBoundary()
    {
        var deathAtUtc = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var resultTouchesWindow = CreateDamageEvent(deathAtUtc.AddSeconds(-31), 1_000, 1) with
        {
            ResultSeenAtUtc = deathAtUtc.AddSeconds(-29.9),
        };
        var atCutoff = CreateDamageEvent(deathAtUtc.AddSeconds(-30), 2_000, 2);
        var outsideWindow = CreateDamageEvent(deathAtUtc.AddMilliseconds(-30_001), 3_000, 3);
        var afterDeath = CreateDamageEvent(deathAtUtc.AddMilliseconds(1), 4_000, 4);
        var death = CreateDeath(
            deathAtUtc,
            [resultTouchesWindow, outsideWindow, atCutoff, afterDeath],
            []);

        var events = DeathDisplaySelector.GetLeadUpEvents(death);

        Assert.Equal(
            [resultTouchesWindow.EventIdentity, atCutoff.EventIdentity],
            events.Select(combatEvent => combatEvent.EventIdentity));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void GetLeadUpEventsHonorsSelectedDuration(int displaySeconds)
    {
        var deathAtUtc = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var atCutoff = CreateDamageEvent(deathAtUtc.AddSeconds(-displaySeconds), 2_000, 1);
        var outsideWindow = CreateDamageEvent(deathAtUtc.AddMilliseconds((-displaySeconds * 1_000) - 1), 3_000, 2);
        var death = CreateDeath(deathAtUtc, [outsideWindow, atCutoff], []);

        var events = DeathDisplaySelector.GetLeadUpEvents(death, displaySeconds);

        Assert.Equal([atCutoff.EventIdentity], events.Select(combatEvent => combatEvent.EventIdentity));
    }

    [Fact]
    public void GetLeadUpShieldCheckpointsReturnsShieldOnlyLossesAndGains()
    {
        var deathAtUtc = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
        var shieldLoss = CreateDamageEvent(deathAtUtc.AddSeconds(-2), 0, 1) with
        {
            CurrentHp = 150_537,
            ShieldHp = 72_476,
            ResultSeenAtUtc = deathAtUtc.AddSeconds(-1.3),
            ResultCurrentHp = 150_537,
            ResultShieldHp = 0,
            ResultMaxHp = 226_488,
        };
        var hpChange = shieldLoss with
        {
            EventIdentity = "hp-change",
            EventOrdinal = 2,
            ResultCurrentHp = 140_000,
        };
        var shieldGain = shieldLoss with
        {
            EventIdentity = "shield-gain",
            EventOrdinal = 3,
            ResultShieldHp = 80_000,
        };
        var visibleDamage = CreateDamageEvent(deathAtUtc.AddSeconds(-1), 20_000, 4);
        var death = CreateDeath(
            deathAtUtc,
            [shieldLoss, hpChange, shieldGain, visibleDamage],
            []);

        var checkpoints = DeathDisplaySelector.GetLeadUpShieldCheckpoints(death, 30);

        Assert.Equal(
            [shieldLoss.EventIdentity, shieldGain.EventIdentity],
            checkpoints.Select(combatEvent => combatEvent.EventIdentity));
        Assert.DoesNotContain(shieldLoss, DeathDisplaySelector.GetLeadUpEvents(death, 30));
        Assert.DoesNotContain(shieldGain, DeathDisplaySelector.GetLeadUpEvents(death, 30));
    }

    [Fact]
    public void SelectUsesHpSnapshotAtThirtySecondBoundary()
    {
        var deathAtUtc = new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
        var outsideWindow = new HpHistorySnapshot(
            deathAtUtc.AddMilliseconds(-30_001),
            69.999f,
            200_000,
            0,
            226_488,
            []);
        var atCutoff = new HpHistorySnapshot(
            deathAtUtc.AddSeconds(-30),
            70.0f,
            150_000,
            0,
            226_488,
            []);

        var selection = DeathDisplaySelector.Select(CreateDeath(deathAtUtc, [], [outsideWindow, atCutoff]));
        var outsideOnlySelection = DeathDisplaySelector.Select(CreateDeath(deathAtUtc, [], [outsideWindow]));

        Assert.Equal(atCutoff, selection.Snapshot);
        Assert.Null(outsideOnlySelection.Snapshot);
    }

    [Fact]
    public void LeadUpTimingPolicyPreservesCaptureAndAttributionBuffers()
    {
        Assert.Equal(10, LeadUpTimingPolicy.ShortDisplaySeconds);
        Assert.Equal(30, LeadUpTimingPolicy.DefaultDisplaySeconds);
        Assert.Equal(60, LeadUpTimingPolicy.MaximumDisplaySeconds);
        Assert.Equal(70, LeadUpTimingPolicy.CaptureSeconds);
        Assert.Equal(75, LeadUpTimingPolicy.LiveRetentionSeconds);
        Assert.Equal(10, LeadUpTimingPolicy.LateFatalCauseLookbackSeconds);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(30, 30)]
    [InlineData(60, 60)]
    [InlineData(0, 30)]
    [InlineData(45, 30)]
    public void LeadUpTimingPolicyNormalizesSavedDurations(int requestedSeconds, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, LeadUpTimingPolicy.NormalizeDisplaySeconds(requestedSeconds));
    }

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

    [Fact]
    public void SelectIncludesMultiSourceAtomicDamageWave()
    {
        var burstAtUtc = new DateTime(2026, 8, 4, 10, 0, 9, DateTimeKind.Utc);
        var damageAmounts = new uint[] { 30_724, 33_146, 33_270, 33_441, 31_856 };
        var recentEvents = damageAmounts
            .Select((amount, index) => CreateDamageEvent(
                burstAtUtc.AddMilliseconds(index * 7),
                amount,
                (uint)(index + 1)) with
            {
                SourceEntityId = (uint)(0x4000_0100 + index),
                ActionSequence = (uint)(8_869 + index),
                CurrentHp = 150_537,
                ShieldHp = 72_476,
                ResultSeenAtUtc = index == damageAmounts.Length - 1
                    ? burstAtUtc.AddSeconds(1)
                    : null,
                ResultCurrentHp = 0,
                ResultShieldHp = 0,
                ResultMaxHp = index == damageAmounts.Length - 1 ? 226_488u : 0,
            })
            .ToList();
        var lastAlive = new HpHistorySnapshot(
            burstAtUtc.AddMilliseconds(700),
            519.7f,
            150_537,
            0,
            226_488,
            []);
        var death = new PartyDeathRecord(
            burstAtUtc.AddSeconds(1),
            520.0f,
            "nai-la",
            "Nai La",
            3,
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
                burstAtUtc.AddMilliseconds(1_500),
                lastAlive,
                recentEvents,
                []),
        };

        var selection = DeathDisplaySelector.Select(death);
        var fatalGroup = Assert.Single(selection.FatalEvents);

        Assert.Equal(162_437UL, fatalGroup.Amount);
        Assert.Equal(150_537u, selection.Snapshot?.CurrentHp);
        Assert.Equal(72_476u, selection.Snapshot?.ShieldHp);
        Assert.Equal(11_900UL, fatalGroup.Amount - selection.Snapshot!.CurrentHp);
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

    private static PartyDeathRecord CreateDeath(
        DateTime seenAtUtc,
        IReadOnlyList<CombatEventRecord> recentEvents,
        IReadOnlyList<HpHistorySnapshot> hpHistory)
    {
        return new PartyDeathRecord(
            seenAtUtc,
            100.0f,
            "nai-la",
            "Nai La",
            6,
            23,
            "BRD",
            0,
            0,
            226_488,
            null,
            recentEvents,
            hpHistory,
            []);
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
