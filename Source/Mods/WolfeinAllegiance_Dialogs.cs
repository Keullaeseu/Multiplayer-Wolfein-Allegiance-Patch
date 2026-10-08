using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RimWorld;
using Verse;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static void SyncDialogsAndEndings()
    {
        // Side choice dialog picks a side via signal, must be synced. Dialog choices carry
        // no map selection (vanilla PatchQuestChoices.Choose uses None).
        SyncMethodByName("WolfeinAllegiance.QuestPart_SideChoiceDialog", "DoChoose", true,
            SyncContext.None);

        // Debug variant has no DoChoose method, its two Dialog_MessageBox button lambdas
        // inside Notify_QuestSignalReceived send the signals directly, sync those instead.
        // The lambdas capture only `this` (syncable QuestPart), so RegisterLambdaMethod is
        // correct here; RegisterLambdaDelegate is only for lambdas capturing locals.
        try
        {
            MpCompat.RegisterLambdaMethod("WolfeinAllegiance.QuestPart_DebugSideChoiceDialog",
                    "Notify_QuestSignalReceived", 0, 1)
                .Select(sync =>
                {
                    sync.CancelIfAnyArgNull();
                    return sync;
                })
                .ToList();
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not register debug side choice lambdas: {exception}");
        }

        // Victory shuttle departure is triggered from a gizmo, sync via shuttle Thing lookup.
        var victoryTrackerType = AccessTools.TypeByName("WolfeinAllegiance.VictoryShuttleTracker");
        if (victoryTrackerType != null)
        {
            var triggerDeparture = AccessTools.DeclaredMethod(victoryTrackerType, "TriggerDeparture");
            if (triggerDeparture != null)
            {
                MpCompat.harmony.Patch(triggerDeparture,
                    new HarmonyMethod(typeof(WolfeinAllegiance), nameof(PreVictoryDeparture)));
                MP.RegisterSyncMethod(typeof(WolfeinAllegiance), nameof(SyncedVictoryDeparture))
                    .SetContext(SyncContext.MapSelected)
                    .CancelIfAnyArgNull();
            }
            else
            {
                Log.Warning($"{LogPrefix} TriggerDeparture not found on VictoryShuttleTracker");
            }
        }
        else
        {
            Log.Warning($"{LogPrefix} Type not found: WolfeinAllegiance.VictoryShuttleTracker");
        }

        // Ending credits have no MP support, suppress them (quest still completes via signals).
        var endingShuttleType = AccessTools.TypeByName("WolfeinAllegiance.QuestPart_EndingShuttle");
        if (endingShuttleType != null)
        {
            var triggerVictory = AccessTools.DeclaredMethod(endingShuttleType, "TriggerVictory");
            if (triggerVictory != null)
                MpCompat.harmony.Patch(triggerVictory,
                    new HarmonyMethod(typeof(WolfeinAllegiance), nameof(PreTriggerVictory)));
        }
    }

    private static bool PreVictoryDeparture(object info)
    {
        if (!MP.InInterface) return true;

        try
        {
            var shuttleField = AccessTools.Field(info?.GetType(), "shuttle");
            var shuttle = (Thing)shuttleField?.GetValue(info);
            if (shuttle == null) return false;

            SyncedVictoryDeparture(shuttle);
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} PreVictoryDeparture failed: {exception}");
            return true;
        }
    }

    private static void SyncedVictoryDeparture(Thing shuttle)
    {
        if (shuttle == null) return;

        try
        {
            var trackerType = AccessTools.TypeByName("WolfeinAllegiance.VictoryShuttleTracker");
            if (trackerType == null) return;

            var tryGetInfo = AccessTools.DeclaredMethod(trackerType, "TryGetInfo");
            var triggerDeparture = AccessTools.DeclaredMethod(trackerType, "TriggerDeparture");
            if (tryGetInfo == null || triggerDeparture == null) return;

            object[] tryGetArgs = [shuttle, null];
            // TryGetInfo(Thing, out VictoryShuttleInfo) returns bool via reflection.
            var found = (bool)tryGetInfo.Invoke(null, tryGetArgs);
            if (!found) return;

            if (MP.IsInMultiplayer)
            {
                DepartWithoutCountdown(tryGetArgs[1]);
                return;
            }

            triggerDeparture.Invoke(null, [tryGetArgs[1]]);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SyncedVictoryDeparture failed: {exception}");
        }
    }

    private static void DepartWithoutCountdown(object info)
    {
        // Multiplayer mirror of VictoryShuttleTracker.TriggerDeparture without
        // ShipCountdown.InitiateCountdown: MP has no sync for the real-time countdown
        // ticker, so each client would reach CountdownEnded (and the game-over credits)
        // at slightly different times. ForceJob + quest signal + unregister + the
        // pending-destroy queue (drained by the synced TickPendingDestroy once
        // CountingDown is false) stay deterministic on all clients.
        try
        {
            var infoType = info?.GetType();
            if (infoType == null) return;
            var shuttle = (Thing)AccessTools.Field(infoType, "shuttle")?.GetValue(info);
            var quest = (Quest)AccessTools.Field(infoType, "quest")?.GetValue(info);
            var questTag = (string)AccessTools.Field(infoType, "questTag")?.GetValue(info);
            var comp = shuttle?.TryGetComp<CompShuttle>();
            if (comp?.shipParent == null) return;

            var transporter = comp.Transporter;
            var inner = transporter?.innerContainer;
            var colonists = inner != null
                ? inner.OfType<Pawn>().Where(pawn => pawn.IsColonist).ToList()
                : null;
            if (colonists == null || colonists.Count == 0)
            {
                Messages.Message("WA_Ending_NoColonistsLoaded".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.StoryWatcher.statsRecord.colonistsLaunched += colonists.Count;
            if (quest != null)
                foreach (var questPart in quest.PartsListForReading)
                {
                    var cleanupShip = AccessTools.Field(questPart.GetType(), "transportShip")?.GetValue(questPart);
                    if (questPart.GetType().Name == "QuestPart_WolfeinShipCleanup"
                        && ReferenceEquals(cleanupShip, comp.shipParent))
                    {
                        AccessTools.Field(questPart.GetType(), "transportShip")?.SetValue(questPart, null);
                        break;
                    }
                }

            comp.shipParent.ForceJob(ShipJobMaker.MakeShipJob(ShipJobDefOf.FlyAway));
            if (quest != null && quest.State == QuestState.Ongoing)
                Find.SignalManager.SendSignal(new Signal(questTag + ".SentWithExtraColonists"));

            var trackerType = AccessTools.TypeByName("WolfeinAllegiance.VictoryShuttleTracker");
            FieldInHierarchy(trackerType, "pendingDestroyColonists")?.SetValue(null, colonists);
            FieldInHierarchy(trackerType, "waitingForFade")?.SetValue(null, true);
            AccessTools.DeclaredMethod(trackerType, "Unregister")?.Invoke(null, [shuttle]);
            Messages.Message("WA_Ending_DepartLabel".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} DepartWithoutCountdown failed: {exception}");
        }
    }

    private static bool PreTriggerVictory()
    {
        if (!MP.IsInMultiplayer) return true;

        Messages.Message("Wolfein Allegiance ending reached (credits suppressed for multiplayer).",
            MessageTypeDefOf.PositiveEvent, false);
        return false;
    }
}