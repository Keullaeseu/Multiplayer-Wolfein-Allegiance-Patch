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
        // Side choice dialog picks a side via signal, must be synced.
        SyncMethodByName("WolfeinAllegiance.QuestPart_SideChoiceDialog", "DoChoose", true);

        // Debug variant has no DoChoose method, its two Dialog_MessageBox button lambdas
        // inside Notify_QuestSignalReceived send the signals directly, sync those instead.
        try
        {
            MpCompat.RegisterLambdaMethod("WolfeinAllegiance.QuestPart_DebugSideChoiceDialog",
                "Notify_QuestSignalReceived", 0, 1);
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

            // Register gizmo lambda so clicking depart syncs (captures info struct).
            try
            {
                MpCompat.RegisterLambdaMethod("WolfeinAllegiance.Patch_VictoryShuttleGizmos", "Postfix", 0);
            }
            catch (Exception exception)
            {
                Log.Warning($"{LogPrefix} Could not register victory gizmo lambda: {exception}");
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
            if (shuttle == null) return true;

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

            triggerDeparture.Invoke(null, [tryGetArgs[1]]);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SyncedVictoryDeparture failed: {exception}");
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