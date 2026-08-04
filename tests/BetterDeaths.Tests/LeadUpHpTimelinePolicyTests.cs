namespace BetterDeaths;

public sealed class LeadUpHpTimelinePolicyTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 4, 8, 23, 19, DateTimeKind.Utc);

    [Fact]
    public void DelayedDamageResultDoesNotReappearAfterSequencedHeal()
    {
        var state = new LeadUpHpTimelineState();
        state.ResolveSample(new LeadUpHpValue(205_237, 38_995, 205_237), BaseTime.AddMilliseconds(-100));

        var damage = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            132_161,
            205_237,
            38_995,
            actionSequence: 86_602) with
        {
            ActionName = "Forsaken Bonds",
            ResultSeenAtUtc = BaseTime.AddMilliseconds(952),
            ResultCurrentHp = 73_076,
            ResultShieldHp = 0,
            ResultMaxHp = 205_237,
        };
        var damageResolution = state.ResolveEvent(
            damage,
            new LeadUpHpValue(205_237, 38_995, 205_237),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(73_076, 0, 205_237), damageResolution.After);
        Assert.True(damageResolution.UsesCapturedResult);

        var firstStaleSample = state.ResolveSample(
            new LeadUpHpValue(205_237, 38_995, 205_237),
            BaseTime.AddMilliseconds(259));
        Assert.Equal(new LeadUpHpValue(73_076, 0, 205_237), firstStaleSample.Value);

        var unorderedHeal = CreateEvent(
            BaseTime.AddMilliseconds(371),
            DeathEventKind.Heal,
            9_435,
            205_237,
            38_995,
            hpSource: CombatEventHpSource.LatestPriorSample);
        var unorderedHealResolution = state.ResolveEvent(
            unorderedHeal,
            new LeadUpHpValue(205_237, 38_995, 205_237),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(73_076, 0, 205_237), unorderedHealResolution.After);
        Assert.True(unorderedHealResolution.UnconfirmedUnsequencedHeal);

        var hotTick = CreateEvent(
            BaseTime.AddMilliseconds(501),
            DeathEventKind.Heal,
            28_744,
            205_237,
            38_995,
            hpSource: CombatEventHpSource.LatestPriorSample);
        var hotTickResolution = state.ResolveEvent(
            hotTick,
            new LeadUpHpValue(205_237, 38_995, 205_237),
            null,
            true);
        Assert.Equal(new LeadUpHpValue(73_076, 0, 205_237), hotTickResolution.After);
        Assert.True(hotTickResolution.UnconfirmedUnsequencedHeal);

        var sequencedHeal = CreateEvent(
            BaseTime.AddMilliseconds(622),
            DeathEventKind.Heal,
            64_314,
            205_237,
            0,
            actionSequence: 86_605);
        var sequencedHealResolution = state.ResolveEvent(
            sequencedHeal,
            new LeadUpHpValue(205_237, 0, 205_237),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(73_076, 0, 205_237), sequencedHealResolution.Before);
        Assert.Equal(new LeadUpHpValue(137_390, 0, 205_237), sequencedHealResolution.After);

        var laterUnorderedHeal = CreateEvent(
            BaseTime.AddMilliseconds(623),
            DeathEventKind.Heal,
            14_204,
            205_237,
            38_995,
            hpSource: CombatEventHpSource.LatestPriorSample);
        var laterUnorderedHealResolution = state.ResolveEvent(
            laterUnorderedHeal,
            new LeadUpHpValue(205_237, 38_995, 205_237),
            null,
            true);
        Assert.Equal(new LeadUpHpValue(137_390, 0, 205_237), laterUnorderedHealResolution.After);
        Assert.True(laterUnorderedHealResolution.UnconfirmedUnsequencedHeal);

        var splitUpdate = state.ResolveSample(
            new LeadUpHpValue(205_237, 0, 205_237),
            BaseTime.AddMilliseconds(778));
        var delayedDamageResult = state.ResolveSample(
            new LeadUpHpValue(73_076, 0, 205_237),
            BaseTime.AddMilliseconds(952));

        Assert.Equal(new LeadUpHpValue(137_390, 0, 205_237), splitUpdate.Value);
        Assert.Equal(new LeadUpHpValue(137_390, 0, 205_237), delayedDamageResult.Value);
        Assert.True(splitUpdate.UsedReconstructedValue);
        Assert.True(delayedDamageResult.UsedReconstructedValue);
    }

    [Fact]
    public void CapturedShieldAndHpLossUsesLinkedResult()
    {
        var state = CreateState(new LeadUpHpValue(205_237, 38_995, 205_237));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 132_161, 205_237, 38_995) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(900),
            ResultCurrentHp = 73_076,
            ResultShieldHp = 0,
            ResultMaxHp = 205_237,
        };

        var resolution = state.ResolveEvent(
            damage,
            new LeadUpHpValue(205_237, 38_995, 205_237),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(73_076, 0, 205_237), resolution.After);
        Assert.True(resolution.UsesCapturedResult);
    }

    [Fact]
    public void HpLossCanRemainValidWhenResultAlsoContainsNewShield()
    {
        var state = CreateState(new LeadUpHpValue(200, 20, 250));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 200, 20, maxHp: 250) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 150,
            ResultShieldHp = 30,
            ResultMaxHp = 250,
        };

        var resolution = state.ResolveEvent(
            damage,
            new LeadUpHpValue(200, 20, 250),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(150, 30, 250), resolution.After);
        Assert.True(resolution.UsesCapturedResult);
    }

    [Fact]
    public void ResultContainingLaterHealingFallsBackToCalculatedDamage()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 200));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 20, 100, 0, maxHp: 200) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 120,
            ResultShieldHp = 0,
            ResultMaxHp = 200,
        };

        var resolution = state.ResolveEvent(
            damage,
            new LeadUpHpValue(100, 0, 200),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(80, 0, 200), resolution.After);
        Assert.False(resolution.UsesCapturedResult);
        Assert.True(resolution.UsedCalculatedResult);
    }

    [Fact]
    public void DamageWithoutLinkedResultReducesHpWithoutInventingShieldLoss()
    {
        var state = CreateState(new LeadUpHpValue(100, 30, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 100, 30);

        var resolution = state.ResolveEvent(
            damage,
            new LeadUpHpValue(100, 30, 100),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(50, 30, 100), resolution.After);
        Assert.False(resolution.UsesCapturedResult);
        Assert.True(resolution.UsedCalculatedResult);
    }

    [Fact]
    public void ShieldCheckpointUpdatesShieldWithoutCreatingHpLoss()
    {
        var state = CreateState(new LeadUpHpValue(150_537, 72_476, 226_488));
        var checkpoint = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            0,
            150_537,
            72_476,
            maxHp: 226_488) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 150_537,
            ResultShieldHp = 0,
            ResultMaxHp = 226_488,
        };

        var accepted = state.TryResolveShieldCheckpoint(checkpoint);

        Assert.True(accepted);
        Assert.Equal(new LeadUpHpValue(150_537, 0, 226_488), state.CurrentValue);
    }

    [Fact]
    public void ShieldCheckpointRejectsHpChanges()
    {
        var state = CreateState(new LeadUpHpValue(150_537, 72_476, 226_488));
        var checkpoint = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            0,
            150_537,
            72_476,
            maxHp: 226_488) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 140_000,
            ResultShieldHp = 0,
            ResultMaxHp = 226_488,
        };

        var accepted = state.TryResolveShieldCheckpoint(checkpoint);

        Assert.False(accepted);
        Assert.Equal(new LeadUpHpValue(150_537, 72_476, 226_488), state.CurrentValue);
    }

    [Fact]
    public void ShieldCheckpointCanApplyShieldGainWithoutChangingHp()
    {
        var state = CreateState(new LeadUpHpValue(150_537, 0, 226_488));
        var checkpoint = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            0,
            150_537,
            0,
            maxHp: 226_488) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 150_537,
            ResultShieldHp = 30_786,
            ResultMaxHp = 226_488,
        };

        var accepted = state.TryResolveShieldCheckpoint(checkpoint);

        Assert.True(accepted);
        Assert.Equal(new LeadUpHpValue(150_537, 30_786, 226_488), state.CurrentValue);
    }

    [Fact]
    public void ShieldCheckpointRejectsChangeOppositeTheTrustedDirection()
    {
        var state = CreateState(new LeadUpHpValue(150_537, 90_000, 226_488));
        var capturedGain = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            0,
            150_537,
            72_476,
            maxHp: 226_488) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 150_537,
            ResultShieldHp = 80_000,
            ResultMaxHp = 226_488,
        };

        var accepted = state.TryResolveShieldCheckpoint(capturedGain);

        Assert.False(accepted);
        Assert.Equal(new LeadUpHpValue(150_537, 90_000, 226_488), state.CurrentValue);
    }

    [Fact]
    public void ShieldCheckpointRejectsAStaleHpState()
    {
        var state = CreateState(new LeadUpHpValue(140_000, 72_476, 226_488));
        var checkpoint = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            0,
            150_537,
            72_476,
            maxHp: 226_488) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 150_537,
            ResultShieldHp = 0,
            ResultMaxHp = 226_488,
        };

        var accepted = state.TryResolveShieldCheckpoint(checkpoint);

        Assert.False(accepted);
        Assert.Equal(new LeadUpHpValue(140_000, 72_476, 226_488), state.CurrentValue);
    }

    [Fact]
    public void OlderShieldVariantCannotRestoreShieldAtRepeatedHp()
    {
        var state = CreateState(new LeadUpHpValue(100, 30, 100));
        var firstCheckpoint = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            0,
            100,
            30) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(300),
            ResultCurrentHp = 100,
            ResultShieldHp = 20,
            ResultMaxHp = 100,
        };
        var secondCheckpoint = CreateEvent(
            BaseTime.AddMilliseconds(500),
            DeathEventKind.Damage,
            0,
            100,
            20) with
        {
            EventIdentity = "event-2",
            EventOrdinal = 2,
            ResultSeenAtUtc = BaseTime.AddMilliseconds(800),
            ResultCurrentHp = 100,
            ResultShieldHp = 10,
            ResultMaxHp = 100,
        };
        var damage = CreateEvent(
            BaseTime.AddSeconds(1),
            DeathEventKind.Damage,
            20,
            100,
            10) with
        {
            EventIdentity = "event-3",
            EventOrdinal = 3,
        };

        Assert.True(state.TryResolveShieldCheckpoint(firstCheckpoint));
        Assert.True(state.TryResolveShieldCheckpoint(secondCheckpoint));
        state.ResolveEvent(damage, new LeadUpHpValue(100, 10, 100), null, true);
        var delayedOldShield = state.ResolveSample(
            new LeadUpHpValue(100, 30, 100),
            BaseTime.AddMilliseconds(1_200));

        Assert.Equal(new LeadUpHpValue(80, 10, 100), delayedOldShield.Value);
        Assert.True(delayedOldShield.UsedReconstructedValue);
    }

    [Fact]
    public void LethalDamageWithoutLinkedResultClearsTheDeathState()
    {
        var state = CreateState(new LeadUpHpValue(40, 30, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 40, 30);

        var resolution = state.ResolveEvent(
            damage,
            new LeadUpHpValue(40, 30, 100),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(0, 0, 100), resolution.After);
        Assert.True(resolution.UsedCalculatedResult);
    }

    [Fact]
    public void SharedMultiHitResultIsAppliedOnlyToLastEffect()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 100));
        var first = CreateEvent(BaseTime, DeathEventKind.Damage, 30, 100, 0, actionSequence: 500) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(600),
            ResultCurrentHp = 50,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };
        var second = first with
        {
            Amount = 20,
            EventIdentity = "event-2",
            EventOrdinal = 2,
        };

        Assert.True(LeadUpHpTimelineState.SharesDamageBurst(first, second));
        var combined = LeadUpHpTimelineState.CombineDamageBurst(first, second);
        var resolution = state.ResolveEvent(
            combined,
            new LeadUpHpValue(100, 0, 100),
            null,
            true);

        Assert.Equal(50u, combined.Amount);
        Assert.Equal(new LeadUpHpValue(50, 0, 100), resolution.After);
        Assert.True(resolution.UsesCapturedResult);
    }

    [Fact]
    public void MultiSourceAtomicBurstUsesCombinedDamageAndFinalResult()
    {
        var damageAmounts = new uint[] { 30_724, 33_146, 33_270, 33_441, 31_856 };
        var events = damageAmounts
            .Select((amount, index) => CreateEvent(
                BaseTime.AddMilliseconds(index * 7),
                DeathEventKind.Damage,
                amount,
                150_537,
                72_476,
                actionSequence: (uint)(8_869 + index),
                maxHp: 226_488) with
            {
                SourceEntityId = (uint)(0x4000_0100 + index),
                SourceName = "Chaos",
                ActionId = 47_864,
                ActionName = "Cyclone",
                EventIdentity = $"cyclone-{index}",
                EventOrdinal = (uint)(3_194 + index),
                ResultSeenAtUtc = index == damageAmounts.Length - 1
                    ? BaseTime.AddMilliseconds(1_070)
                    : null,
                ResultCurrentHp = 0,
                ResultShieldHp = 0,
                ResultMaxHp = index == damageAmounts.Length - 1 ? 226_488u : 0,
            })
            .ToList();

        var bursts = LeadUpHpTimelineState.CombineDamageBursts(events);
        var burst = Assert.Single(bursts);
        Assert.Equal(162_437u, burst.Amount);
        Assert.Equal(BaseTime.AddMilliseconds(1_070), burst.ResultSeenAtUtc);

        var state = CreateState(new LeadUpHpValue(150_537, 72_476, 226_488));
        var burstResolution = state.ResolveEvent(
            burst,
            new LeadUpHpValue(150_537, 72_476, 226_488),
            null,
            true);
        var delayedUltima = CreateEvent(
            BaseTime.AddMilliseconds(920),
            DeathEventKind.Damage,
            7_237,
            117_267,
            0,
            actionSequence: 8_881,
            maxHp: 226_488);
        var ultimaResolution = state.ResolveEvent(
            delayedUltima,
            new LeadUpHpValue(117_267, 0, 226_488),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(0, 0, 226_488), burstResolution.After);
        Assert.True(burstResolution.UsesCapturedResult);
        Assert.Equal(new LeadUpHpValue(0, 0, 226_488), ultimaResolution.Before);
        Assert.Equal(new LeadUpHpValue(0, 0, 226_488), ultimaResolution.After);
    }

    [Fact]
    public void ShieldCheckpointKeepsPull965WaveAlignedWithItsCapturedDeathResult()
    {
        var state = CreateState(new LeadUpHpValue(150_537, 72_476, 226_488));
        var checkpoint = CreateEvent(
            BaseTime.AddMilliseconds(-500),
            DeathEventKind.Damage,
            0,
            150_537,
            72_476,
            maxHp: 226_488) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(200),
            ResultCurrentHp = 150_537,
            ResultShieldHp = 0,
            ResultMaxHp = 226_488,
        };
        var burst = CreateEvent(
            BaseTime,
            DeathEventKind.Damage,
            162_437,
            150_537,
            72_476,
            maxHp: 226_488) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(1_000),
            ResultCurrentHp = 0,
            ResultShieldHp = 0,
            ResultMaxHp = 226_488,
        };

        Assert.True(state.TryResolveShieldCheckpoint(checkpoint));
        var resolution = state.ResolveEvent(
            burst,
            new LeadUpHpValue(150_537, 72_476, 226_488),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(150_537, 0, 226_488), resolution.Before);
        Assert.Equal(new LeadUpHpValue(0, 0, 226_488), resolution.After);
        Assert.True(resolution.UsesCapturedResult);
    }

    [Fact]
    public void SimilarDamageOutsideAtomicWindowIsNotCombined()
    {
        var first = CreateEvent(BaseTime, DeathEventKind.Damage, 20, 100, 0, actionSequence: 10);
        var second = first with
        {
            SeenAtUtc = BaseTime.AddMilliseconds(101),
            ActionSequence = 11,
            EventIdentity = "event-2",
            EventOrdinal = 2,
        };

        var bursts = LeadUpHpTimelineState.CombineDamageBursts([first, second]);

        Assert.Equal(2, bursts.Count);
    }

    [Fact]
    public void DamageWithDifferentCapturedPreHitStateIsNotCombined()
    {
        var first = CreateEvent(BaseTime, DeathEventKind.Damage, 20, 100, 0, actionSequence: 10);
        var second = first with
        {
            SeenAtUtc = BaseTime.AddMilliseconds(10),
            CurrentHp = 80,
            ActionSequence = 11,
            EventIdentity = "event-2",
            EventOrdinal = 2,
        };

        var bursts = LeadUpHpTimelineState.CombineDamageBursts([first, second]);

        Assert.Equal(2, bursts.Count);
    }

    [Fact]
    public void OverlappingHitResultIsNotAppliedToEarlierDamage()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 100));
        var first = CreateEvent(BaseTime, DeathEventKind.Damage, 20, 100, 0, actionSequence: 10) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(800),
            ResultCurrentHp = 50,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };
        var second = CreateEvent(BaseTime.AddMilliseconds(100), DeathEventKind.Damage, 30, 100, 0, actionSequence: 11) with
        {
            EventIdentity = "event-2",
            EventOrdinal = 2,
            ResultSeenAtUtc = BaseTime.AddMilliseconds(900),
            ResultCurrentHp = 50,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };

        var firstResolution = state.ResolveEvent(
            first,
            new LeadUpHpValue(100, 0, 100),
            null,
            true);
        var secondResolution = state.ResolveEvent(
            second,
            new LeadUpHpValue(100, 0, 100),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(80, 0, 100), firstResolution.After);
        Assert.False(firstResolution.UsesCapturedResult);
        Assert.Equal(new LeadUpHpValue(80, 0, 100), secondResolution.Before);
        Assert.Equal(new LeadUpHpValue(50, 0, 100), secondResolution.After);
        Assert.True(secondResolution.UsesCapturedResult);
    }

    [Fact]
    public void LethalResultKeepsLaterStaleEventsAndSamplesAtZero()
    {
        var state = CreateState(new LeadUpHpValue(50, 0, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 60, 50, 0) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(500),
            ResultCurrentHp = 0,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };
        var lethal = state.ResolveEvent(damage, new LeadUpHpValue(50, 0, 100), null, true);

        var lateHeal = CreateEvent(BaseTime.AddMilliseconds(300), DeathEventKind.Heal, 25, 50, 0);
        var healResolution = state.ResolveEvent(lateHeal, new LeadUpHpValue(50, 0, 100), null, true);
        var staleSample = state.ResolveSample(new LeadUpHpValue(50, 0, 100), BaseTime.AddSeconds(1));

        Assert.Equal(new LeadUpHpValue(0, 0, 100), lethal.After);
        Assert.Equal(new LeadUpHpValue(0, 0, 100), healResolution.After);
        Assert.Equal(new LeadUpHpValue(0, 0, 100), staleSample.Value);
    }

    [Fact]
    public void InvulnerabilityResultAtOneHpDoesNotCreateDamageTransition()
    {
        var state = CreateState(new LeadUpHpValue(1, 0, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 1, 0) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(400),
            ResultCurrentHp = 1,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };

        var resolution = state.ResolveEvent(damage, new LeadUpHpValue(1, 0, 100), null, true);

        Assert.Equal(resolution.Before, resolution.After);
        Assert.True(resolution.UsesCapturedResult);
        Assert.False(resolution.HpOrShieldDecreased);
    }

    [Fact]
    public void SupersededValueCanBecomeTrustedAgainAfterHoldWindow()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 100, 0) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(500),
            ResultCurrentHp = 50,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };
        state.ResolveEvent(damage, new LeadUpHpValue(100, 0, 100), null, true);

        var held = state.ResolveSample(new LeadUpHpValue(100, 0, 100), BaseTime.AddSeconds(1));
        var accepted = state.ResolveSample(new LeadUpHpValue(100, 0, 100), BaseTime.AddSeconds(2.1));

        Assert.Equal(50u, held.Value.CurrentHp);
        Assert.True(held.UsedReconstructedValue);
        Assert.Equal(100u, accepted.Value.CurrentHp);
        Assert.False(accepted.UsedReconstructedValue);
    }

    [Fact]
    public void MatchingHpReadingConfirmsUnsequencedHeal()
    {
        var state = CreateState(new LeadUpHpValue(50, 0, 100));
        var heal = CreateEvent(
            BaseTime,
            DeathEventKind.Heal,
            20,
            50,
            0,
            hpSource: CombatEventHpSource.LatestPriorSample);

        var resolution = state.ResolveEvent(
            heal,
            new LeadUpHpValue(50, 0, 100),
            new LeadUpHpValue(70, 0, 100),
            true);

        Assert.Equal(new LeadUpHpValue(70, 0, 100), resolution.After);
        Assert.False(resolution.UnconfirmedUnsequencedHeal);
    }

    [Fact]
    public void UnconfirmedUnsequencedHealDoesNotAdvanceTrustedHp()
    {
        var state = CreateState(new LeadUpHpValue(50, 0, 100));
        var heal = CreateEvent(
            BaseTime,
            DeathEventKind.Heal,
            20,
            50,
            0,
            hpSource: CombatEventHpSource.LatestPriorSample);

        var resolution = state.ResolveEvent(
            heal,
            new LeadUpHpValue(50, 0, 100),
            null,
            true);

        Assert.Equal(new LeadUpHpValue(50, 0, 100), resolution.After);
        Assert.True(resolution.UnconfirmedUnsequencedHeal);
    }

    [Fact]
    public void UnsequencedHealWithoutAnyHpBaselineIsUnconfirmed()
    {
        var state = new LeadUpHpTimelineState();
        var heal = CreateEvent(
            BaseTime,
            DeathEventKind.Heal,
            20,
            0,
            0,
            hpSource: CombatEventHpSource.NoPreHitSample) with
        {
            MaxHp = 0,
        };

        var resolution = state.ResolveEvent(
            heal,
            new LeadUpHpValue(0, 0, 0),
            null,
            true);

        Assert.False(resolution.After.IsAvailable);
        Assert.True(resolution.UnconfirmedUnsequencedHeal);
    }

    [Fact]
    public void MismatchedHpReadingDoesNotConfirmUnsequencedHeal()
    {
        var state = CreateState(new LeadUpHpValue(50, 0, 100));
        var heal = CreateEvent(
            BaseTime,
            DeathEventKind.Heal,
            20,
            50,
            0,
            hpSource: CombatEventHpSource.LatestPriorSample);

        var resolution = state.ResolveEvent(
            heal,
            new LeadUpHpValue(50, 0, 100),
            new LeadUpHpValue(80, 0, 100),
            true);

        Assert.Equal(new LeadUpHpValue(50, 0, 100), resolution.After);
        Assert.True(resolution.UnconfirmedUnsequencedHeal);
    }

    [Fact]
    public void MatchingHpReadingConfirmsUnsequencedHealAfterStaleCapturedState()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 100, 0) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(700),
            ResultCurrentHp = 50,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };
        state.ResolveEvent(damage, new LeadUpHpValue(100, 0, 100), null, true);

        var heal = CreateEvent(
            BaseTime.AddMilliseconds(200),
            DeathEventKind.Heal,
            20,
            100,
            0,
            hpSource: CombatEventHpSource.LatestPriorSample);
        var resolution = state.ResolveEvent(
            heal,
            new LeadUpHpValue(100, 0, 100),
            new LeadUpHpValue(70, 0, 100),
            true);

        Assert.Equal(new LeadUpHpValue(50, 0, 100), resolution.Before);
        Assert.Equal(new LeadUpHpValue(70, 0, 100), resolution.After);
        Assert.True(resolution.UsedReconstructedBefore);
        Assert.False(resolution.UnconfirmedUnsequencedHeal);
    }

    [Fact]
    public void OverhealedUnsequencedHealIsNotShownAsConfirmed()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 100));
        var heal = CreateEvent(
            BaseTime,
            DeathEventKind.Heal,
            20,
            100,
            0,
            hpSource: CombatEventHpSource.LatestPriorSample);

        var resolution = state.ResolveEvent(
            heal,
            new LeadUpHpValue(100, 0, 100),
            new LeadUpHpValue(100, 0, 100),
            true);

        Assert.Equal(new LeadUpHpValue(100, 0, 100), resolution.After);
        Assert.True(resolution.UnconfirmedUnsequencedHeal);
    }

    [Fact]
    public void SplitHpSampleCanUpdateShieldWithoutRestoringStaleHp()
    {
        var state = CreateState(new LeadUpHpValue(100, 30, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 100, 30) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(500),
            ResultCurrentHp = 50,
            ResultShieldHp = 0,
            ResultMaxHp = 100,
        };
        state.ResolveEvent(damage, new LeadUpHpValue(100, 30, 100), null, true);

        var splitSample = state.ResolveSample(
            new LeadUpHpValue(100, 10, 100),
            BaseTime.AddMilliseconds(300));

        Assert.Equal(new LeadUpHpValue(50, 10, 100), splitSample.Value);
        Assert.True(splitSample.UsedReconstructedValue);
    }

    [Fact]
    public void ChangedMaxHpCanReanchorAtCapturedSample()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 100));
        var damage = CreateEvent(BaseTime, DeathEventKind.Damage, 20, 100, 0) with
        {
            ResultSeenAtUtc = BaseTime.AddMilliseconds(500),
            ResultCurrentHp = 50,
            ResultShieldHp = 0,
            ResultMaxHp = 80,
        };
        var damageResolution = state.ResolveEvent(
            damage,
            new LeadUpHpValue(100, 0, 100),
            null,
            true);
        var changedMaxSample = state.ResolveSample(
            new LeadUpHpValue(50, 0, 80),
            BaseTime.AddMilliseconds(500));

        Assert.Equal(new LeadUpHpValue(80, 0, 100), damageResolution.After);
        Assert.Equal(new LeadUpHpValue(50, 0, 80), changedMaxSample.Value);
        Assert.False(changedMaxSample.UsedReconstructedValue);
    }

    [Fact]
    public void RepeatedTransitionFromSameHpRefreshesItsStaleWindow()
    {
        var state = CreateState(new LeadUpHpValue(100, 0, 100));
        var firstDamage = CreateEvent(BaseTime, DeathEventKind.Damage, 50, 100, 0);
        state.ResolveEvent(firstDamage, new LeadUpHpValue(100, 0, 100), null, true);

        var heal = CreateEvent(BaseTime.AddSeconds(1), DeathEventKind.Heal, 50, 50, 0, actionSequence: 2);
        state.ResolveEvent(heal, new LeadUpHpValue(50, 0, 100), null, true);

        var secondDamage = CreateEvent(BaseTime.AddSeconds(1.5), DeathEventKind.Damage, 20, 100, 0);
        state.ResolveEvent(secondDamage, new LeadUpHpValue(100, 0, 100), null, true);
        var staleSample = state.ResolveSample(new LeadUpHpValue(100, 0, 100), BaseTime.AddSeconds(2.5));

        Assert.Equal(80u, staleSample.Value.CurrentHp);
        Assert.True(staleSample.UsedReconstructedValue);
    }

    private static LeadUpHpTimelineState CreateState(LeadUpHpValue initial)
    {
        var state = new LeadUpHpTimelineState();
        state.ResolveSample(initial, BaseTime.AddSeconds(-1));
        return state;
    }

    private static CombatEventRecord CreateEvent(
        DateTime seenAtUtc,
        DeathEventKind kind,
        uint amount,
        uint currentHp,
        uint shieldHp,
        uint actionSequence = 0,
        CombatEventHpSource hpSource = CombatEventHpSource.DirectCombatEventSnapshot,
        uint maxHp = 0)
    {
        return new CombatEventRecord(
            seenAtUtc,
            0,
            "member",
            "Player",
            0,
            1,
            "Source",
            10,
            kind == DeathEventKind.Damage ? "Damage" : "Heal",
            0,
            kind,
            amount,
            currentHp,
            shieldHp,
            maxHp > 0 ? maxHp : Math.Max(currentHp, 100u),
            DamageType.Magic,
            false,
            false,
            false,
            false,
            string.Empty,
            [],
            [])
        {
            EventIdentity = "event-1",
            EventOrdinal = 1,
            ActionSequence = actionSequence,
            HpSource = hpSource,
        };
    }
}
