using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RimWorld;
using Verse;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static readonly string[] TargetedPermitTypeNames =
    [
        "WolfeinAllegiance.PermitWorker_WelfareProgram",
        "WolfeinAllegiance.PermitWorker_WolfeinShuttle",
        "WolfeinAllegiance.PermitWorker_RegularArmy",
        "WolfeinAllegiance.PermitWorker_DangerClose",
        "WolfeinAllegiance.PermitWorker_Artillery",
        "WolfeinAllegiance.PermitWorker_Artillery2",
        "WolfeinAllegiance.PermitWorker_FoodDrop",
        "WolfeinAllegiance.PermitWorker_NightRaid",
        "WolfeinAllegiance.PermitWorker_LaborHelp",
        "WolfeinAllegiance.PermitWorker_EMPBombardment"
    ];

    private static void SyncTargetedPermits()
    {
        MP.RegisterSyncMethod(typeof(WolfeinAllegiance), nameof(SyncedPermitOrder))
            .SetContext(SyncContext.MapSelected);

        foreach (var typeName in TargetedPermitTypeNames)
        {
            var permitType = AccessTools.TypeByName(typeName);
            if (permitType == null)
            {
                Log.Warning($"{LogPrefix} Permit type not found: {typeName}");
                continue;
            }

            var orderMethod = AccessTools.DeclaredMethod(permitType, "OrderForceTarget");
            if (orderMethod == null)
            {
                Log.Warning($"{LogPrefix} OrderForceTarget not found on: {typeName}");
                continue;
            }

            MpCompat.harmony.Patch(orderMethod,
                new HarmonyMethod(typeof(WolfeinAllegiance), nameof(PrePermitOrderForceTarget)));
        }
    }

    private static bool PrePermitOrderForceTarget(object __instance, LocalTargetInfo target)
    {
        if (!MP.InInterface) return true;

        try
        {
            var instanceType = __instance.GetType();
            var caller = (Pawn)FieldInHierarchy(instanceType, "caller")?.GetValue(__instance);
            var map = (Map)FieldInHierarchy(instanceType, "map")?.GetValue(__instance);
            var free = (bool)(FieldInHierarchy(instanceType, "free")?.GetValue(__instance) ?? false);
            var permitDef = ((RoyalTitlePermitWorker)__instance).def;

            Faction faction = null;
            var callingFactionField = FieldInHierarchy(instanceType, "callingFaction")
                                      ?? FieldInHierarchy(instanceType, "calledFaction");
            if (callingFactionField != null) faction = (Faction)callingFactionField.GetValue(__instance);

            if (caller == null || map == null || permitDef == null) return true;

            SyncedPermitOrder(caller, map, faction, permitDef, free, target);
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} PrePermitOrderForceTarget failed: {exception}");
            return true;
        }
    }

    private static void SyncedPermitOrder(Pawn caller, Map map, Faction faction, RoyalTitlePermitDef permitDef,
        bool free, LocalTargetInfo target)
    {
        if (caller == null || map == null || permitDef == null) return;

        var worker = permitDef.Worker;
        if (worker == null) return;

        try
        {
            var workerType = worker.GetType();
            FieldInHierarchy(workerType, "caller")?.SetValue(worker, caller);
            FieldInHierarchy(workerType, "map")?.SetValue(worker, map);
            FieldInHierarchy(workerType, "free")?.SetValue(worker, free);

            if (faction != null)
            {
                FieldInHierarchy(workerType, "callingFaction")?.SetValue(worker, faction);
                FieldInHierarchy(workerType, "calledFaction")?.SetValue(worker, faction);
            }

            // Run outside interface so our prefix lets it through.
            ((ITargetingSource)worker).OrderForceTarget(target);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SyncedPermitOrder failed: {exception}");
        }
    }

    private static void SyncUntargetedPermits()
    {
        SyncMethodByName("WolfeinAllegiance.PermitWorker_SpyIntel", "GenerateRandomQuest");
        SyncMethodByName("WolfeinAllegiance.PermitWorker_ShuttleHack", "ExecuteShuttleHack");
        SyncMethodByName("WolfeinAllegiance.PermitWorker_AssaultTeam", "SpawnAssaultTeam");
        SyncMethodByName("WolfeinAllegiance.PermitWorker_DroneSwarm", "SpawnDrones");
    }
}