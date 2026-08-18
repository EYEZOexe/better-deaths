namespace BetterDeaths;

using BetterDeaths.Capture.FullPull;
using BetterDeaths.Domain;
using BetterDeaths.Persistence;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class Plugin
{
    private const string CanonicalPullStorageDirectoryName = "analyzer-pulls";

    private readonly FullPullRecorder fullPullRecorder = new();
    private readonly LiveSnapshotDeltaTracker fullPullSnapshotDeltaTracker = new();
    private readonly object canonicalPullSaveLock = new();

    private DalamudLiveEventNormalizer? fullPullNormalizer;
    private FileCanonicalPullStore? canonicalPullStore;
    private Task canonicalPullSaveTask = Task.CompletedTask;

    private void EnsureFullPullCaptureStarted(DateTime startedAtUtc)
    {
        if (fullPullRecorder.IsActive || !IsDutyCaptureActive() || IsPvPCaptureBlocked())
        {
            return;
        }

        var pullId = PullId.New();
        var startedAt = ToDateTimeOffset(startedAtUtc);
        var territoryId = currentPullTerritoryId == 0 ? currentTerritoryId : currentPullTerritoryId;
        var territoryName = currentPullTerritoryId == 0 ? currentTerritoryName : currentPullTerritoryName;
        fullPullSnapshotDeltaTracker.Reset();
        fullPullRecorder.Begin(new PullStartContext
        {
            PullId = pullId,
            Metadata = new PullMetadata
            {
                TerritoryId = territoryId,
                TerritoryName = territoryName,
                StartedAt = startedAt,
                Duration = TimeSpan.Zero,
            },
            SchemaVersion = new PullSchemaVersion(CanonicalPullSerializer.CurrentPullSchemaVersion),
            Provenance = new PullProvenance
            {
                SourceKind = PullDataSourceKind.DalamudLive,
                SourceReference = $"local:{pullId.Value:N}",
                Fidelity = CaptureFidelity.Exact,
                Confidence = 1.0f,
                ProducerVersion = GetCurrentPluginVersionForSavedData(),
            },
            DutyActive = true,
        });
        fullPullNormalizer = new DalamudLiveEventNormalizer(
            fullPullRecorder,
            startedAt,
            $"local:{pullId.Value:N}");
    }

    private void MarkFullPullCombatObserved(DateTime now)
    {
        EnsureFullPullCaptureStarted(now);
        if (fullPullRecorder.IsActive)
        {
            fullPullRecorder.MarkCombatObserved();
        }
    }

    private void CaptureFullPullActionEffects(RawActionEffectPacket packet)
    {
        if (!HasCanonicalCombatEffect(packet))
        {
            return;
        }

        EnsurePullStarted(packet.SeenAtUtc);
        MarkFullPullCombatObserved(packet.SeenAtUtc);
        if (fullPullNormalizer is null)
        {
            return;
        }

        var source = CreateFullPullActorReference(packet.CasterEntityId, packet.CasterName, ActorKind.Enemy);
        CaptureFullPullStatusSnapshot(packet.SeenAtUtc, source, packet.SourceSnapshot);
        var actionUseCaptured = false;
        foreach (var target in packet.Targets)
        {
            var targetEntityId = GetRawTargetEntityId(target.TargetId);
            var targetActor = CreateFullPullActorReference(
                targetEntityId,
                targetEntityId == 0 ? null : GetEntityDisplayName(targetEntityId),
                ActorKind.Enemy);
            CaptureFullPullStatusSnapshot(packet.SeenAtUtc, targetActor, target.TargetSnapshot);

            foreach (var effect in target.Effects)
            {
                var eventKind = GetEventKind((ActionEffectKind)effect.Type);
                var liveKind = eventKind switch
                {
                    DeathEventKind.Damage => LiveActionEffectKind.Damage,
                    DeathEventKind.Heal => LiveActionEffectKind.Heal,
                    _ => (LiveActionEffectKind?)null,
                };
                if (liveKind is null)
                {
                    continue;
                }

                if (!actionUseCaptured)
                {
                    TryAppendFullPullFact(new LiveActionEffectFact
                    {
                        ObservedAt = ToDateTimeOffset(packet.SeenAtUtc),
                        Kind = LiveActionEffectKind.ActionUse,
                        Source = source,
                        Target = targetActor,
                        ActionId = packet.ActionId,
                        Fidelity = CaptureFidelity.Exact,
                    });
                    actionUseCaptured = true;
                }

                TryAppendFullPullFact(new LiveActionEffectFact
                {
                    ObservedAt = ToDateTimeOffset(packet.SeenAtUtc),
                    Kind = liveKind.Value,
                    Source = source,
                    Target = targetActor,
                    ActionId = packet.ActionId,
                    Amount = CalculateRawActionEffectAmount(effect),
                    IsCritical = (effect.Param0 & 0x20) == 0x20,
                    IsDirectHit = (effect.Param0 & 0x40) == 0x40,
                    Fidelity = CaptureFidelity.Exact,
                });
            }
        }

        foreach (var pose in packet.ReplayPoses)
        {
            var actor = CreateFullPullActorReference(
                pose.EntityId,
                pose.ActorName,
                pose.ActorKind == ReplayActorKind.Player ? ActorKind.Player : ActorKind.Enemy,
                pose.ClassJobId == 0 ? null : pose.ClassJobId);
            if (actor is null)
            {
                continue;
            }

            TryAppendFullPullFact(new LivePositionFact
            {
                ObservedAt = ToDateTimeOffset(pose.SeenAtUtc),
                Actor = actor,
                X = pose.Position.X,
                Y = pose.Position.Y,
                Z = pose.Position.Z,
                Rotation = pose.Rotation,
                Fidelity = CaptureFidelity.Sampled,
            });

            var targetability = fullPullSnapshotDeltaTracker.ObserveTargetability(
                ToDateTimeOffset(pose.SeenAtUtc),
                actor,
                pose.IsTargetable);
            if (targetability is not null)
            {
                TryAppendFullPullFact(targetability);
            }
        }
    }

    private void CaptureFullPullStatusSnapshot(
        DateTime seenAtUtc,
        LiveActorReference? target,
        RawCombatSnapshot? snapshot)
    {
        if (fullPullNormalizer is null || target is null || snapshot is null)
        {
            return;
        }

        var observedStatuses = new List<LiveObservedStatus>(snapshot.Statuses.Count);
        foreach (var status in snapshot.Statuses)
        {
            if (status.StatusId == 0)
            {
                continue;
            }

            var source = status.SourceId == 0
                ? null
                : CreateFullPullActorReference(
                    status.SourceId,
                    GetEntityDisplayName(status.SourceId),
                    ActorKind.Enemy);
            observedStatuses.Add(new LiveObservedStatus
            {
                Source = source,
                StatusId = status.StatusId,
                Stacks = status.StackCount,
                RemainingDuration = status.RemainingTime > 0.0f
                    ? TimeSpan.FromSeconds(status.RemainingTime)
                    : null,
            });
        }

        foreach (var fact in fullPullSnapshotDeltaTracker.ObserveStatuses(
                     ToDateTimeOffset(seenAtUtc),
                     target,
                     observedStatuses))
        {
            TryAppendFullPullFact(fact);
        }
    }

    private void CaptureFullPullDeath(PartyMemberSnapshot member, DateTime seenAtUtc)
    {
        if (fullPullNormalizer is null)
        {
            return;
        }

        var target = CreateFullPullActorReference(
            member.EntityId,
            member.MemberName,
            ActorKind.Player,
            member.ClassJobId);
        if (target is null)
        {
            return;
        }

        TryAppendFullPullFact(new LiveDeathFact
        {
            ObservedAt = ToDateTimeOffset(seenAtUtc),
            Target = target,
            Fidelity = CaptureFidelity.Exact,
        });
    }

    private void FinalizeCurrentFullPull(string reason)
    {
        try
        {
            ResolveRawCombatQueues(DateTime.UtcNow);
            if (!fullPullRecorder.IsActive)
            {
                return;
            }

            var duration = TimeSpan.FromSeconds(Math.Max(0.0f, CurrentPullElapsedSeconds));
            if (!fullPullRecorder.TryFinalize(new PullEndContext(duration), out var pull) || pull is null)
            {
                fullPullNormalizer = null;
                return;
            }

            fullPullNormalizer = null;
            QueueCanonicalPullSave(pull);
            AddDebugLog($"Finalized canonical pull {pull.Id.Value:N} ({reason}, {pull.Events.Count:N0} events).");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not finalize Better Deaths canonical full pull for {Reason}.", reason);
            ResetFullPullCapture();
        }
    }

    private void ResetFullPullCapture()
    {
        fullPullRecorder.Reset();
        fullPullNormalizer = null;
        fullPullSnapshotDeltaTracker.Reset();
    }

    private void QueueCanonicalPullSave(RecordedPull pull)
    {
        var store = GetCanonicalPullStore();
        lock (canonicalPullSaveLock)
        {
            canonicalPullSaveTask = canonicalPullSaveTask
                .ContinueWith(
                    _ => SaveCanonicalPullSafeAsync(store, pull),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    private static async Task SaveCanonicalPullSafeAsync(FileCanonicalPullStore store, RecordedPull pull)
    {
        try
        {
            await store.SaveAsync(pull).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not persist Better Deaths canonical pull {PullId}.", pull.Id.Value);
        }
    }

    private bool WaitForCanonicalPullSaves(TimeSpan timeout)
    {
        Task saveTask;
        lock (canonicalPullSaveLock)
        {
            saveTask = canonicalPullSaveTask;
        }

        try
        {
            return saveTask.Wait(timeout);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not finish Better Deaths canonical pull persistence during shutdown.");
            return false;
        }
    }

    private void DisposeCanonicalPullCapture()
    {
        _ = WaitForCanonicalPullSaves(TimeSpan.FromSeconds(10));
        canonicalPullStore?.Dispose();
        canonicalPullStore = null;
    }

    private FileCanonicalPullStore GetCanonicalPullStore()
    {
        return canonicalPullStore ??= new FileCanonicalPullStore(
            Path.Combine(PluginInterface.ConfigDirectory.FullName, CanonicalPullStorageDirectoryName));
    }

    private LiveActorReference? CreateFullPullActorReference(
        uint entityId,
        string? fallbackName,
        ActorKind fallbackKind,
        uint? fallbackClassJobId = null)
    {
        if (entityId is 0 or InvalidActorEntityId or uint.MaxValue)
        {
            return null;
        }

        var member = FindCurrentMemberByEntityId(entityId);
        if (member is not null)
        {
            return CreatePartyActorReference(member);
        }

        try
        {
            var gameObject = ObjectTable.SearchByEntityId(entityId);
            if (gameObject is not null)
            {
                var owner = gameObject.OwnerId == 0
                    ? null
                    : FindCurrentMemberByEntityId(gameObject.OwnerId);
                var player = gameObject as IPlayerCharacter;
                var name = string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
                    ? fallbackName
                    : gameObject.Name.TextValue;
                var actorKind = player is not null
                    ? ActorKind.Player
                    : owner is not null
                        ? ActorKind.Pet
                        : fallbackKind;
                var stableId = gameObject.GameObjectId != 0
                    ? $"object:{gameObject.GameObjectId:X16}"
                    : $"entity:{entityId:X8}:slot:{gameObject.ObjectIndex}";
                return new LiveActorReference
                {
                    StableKey = stableId,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Entity {entityId:X8}" : name,
                    Kind = actorKind,
                    ClassJobId = player?.ClassJob.RowId ?? fallbackClassJobId,
                    Owner = owner is null ? null : CreatePartyActorReference(owner),
                };
            }
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "Could not resolve canonical actor identity for {EntityId:X8}.", entityId);
        }

        return new LiveActorReference
        {
            StableKey = $"entity:{entityId:X8}:{fallbackName ?? "unknown"}",
            Name = string.IsNullOrWhiteSpace(fallbackName) ? $"Entity {entityId:X8}" : fallbackName,
            Kind = fallbackKind,
            ClassJobId = fallbackClassJobId,
        };
    }

    private static LiveActorReference CreatePartyActorReference(PartyMemberSnapshot member)
    {
        return new LiveActorReference
        {
            StableKey = $"party:{member.MemberKey}",
            Name = member.MemberName,
            Kind = ActorKind.Player,
            ClassJobId = member.ClassJobId,
        };
    }

    private void TryAppendFullPullFact(LiveActionEffectFact fact)
    {
        TryAppendFullPull(() => fullPullNormalizer?.Append(fact));
    }

    private void TryAppendFullPullFact(LivePositionFact fact)
    {
        TryAppendFullPull(() => fullPullNormalizer?.Append(fact));
    }

    private void TryAppendFullPullFact(LiveStatusFact fact)
    {
        TryAppendFullPull(() => fullPullNormalizer?.Append(fact));
    }

    private void TryAppendFullPullFact(LiveTargetabilityFact fact)
    {
        TryAppendFullPull(() => fullPullNormalizer?.Append(fact));
    }

    private void TryAppendFullPullFact(LiveDeathFact fact)
    {
        TryAppendFullPull(() => fullPullNormalizer?.Append(fact));
    }

    private static uint GetRawTargetEntityId(RawTargetId targetId)
    {
        if (targetId.ObjectId != 0)
        {
            return targetId.ObjectId;
        }

        return targetId.Id is > 0 and <= uint.MaxValue
            ? (uint)targetId.Id
            : 0;
    }

    private static bool HasCanonicalCombatEffect(RawActionEffectPacket packet)
    {
        return packet.Targets.Any(target => target.Effects.Any(effect =>
        {
            var eventKind = GetEventKind((ActionEffectKind)effect.Type);
            return eventKind is DeathEventKind.Damage or DeathEventKind.Heal;
        }));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime utc)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    private static void TryAppendFullPull(Action append)
    {
        try
        {
            append();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not normalize a Better Deaths full-pull capture fact.");
        }
    }
}
