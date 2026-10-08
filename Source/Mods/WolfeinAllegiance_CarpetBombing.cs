using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using RimWorld;
using Verse;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static void SyncCarpetBombing()
    {
        // CarpetBombing has two-step targeting, needs special handling (only sync the spawn step).
        var carpetType = AccessTools.TypeByName("WolfeinAllegiance.PermitWorker_CarpetBombing");
        if (carpetType == null)
        {
            Log.Warning($"{LogPrefix} Permit type not found: WolfeinAllegiance.PermitWorker_CarpetBombing");
            return;
        }

        var carpetOrder = AccessTools.DeclaredMethod(carpetType, "OrderForceTarget");
        if (carpetOrder == null)
            Log.Warning($"{LogPrefix} OrderForceTarget not found on PermitWorker_CarpetBombing");
        else
            MpCompat.harmony.Patch(carpetOrder,
                new HarmonyMethod(typeof(WolfeinAllegiance), nameof(PreCarpetBombingOrder)));

        MP.RegisterSyncMethod(typeof(WolfeinAllegiance), nameof(SyncedCarpetBombingSpawn))
            .SetContext(SyncContext.MapSelected);
    }

    private static bool PreCarpetBombingOrder(object __instance, LocalTargetInfo target)
    {
        if (!MP.InInterface) return true;

        try
        {
            var instanceType = __instance.GetType();
            var firstTargetField = FieldInHierarchy(instanceType, "firstTarget");
            if (firstTargetField == null) return true;

            var firstTarget = (LocalTargetInfo)firstTargetField.GetValue(__instance);
            if (!firstTarget.IsValid)
                // First click only selects start and re-opens targeter, keep local.
                return true;

            // Second click, check if it would spawn (distinct + within max length).
            // If not spawnable the original shows messages and re-targets, keep local.
            var maxLengthProperty = AccessTools.Property(instanceType, "MaxBombLength");
            var maxBombLength = 50f;
            if (maxLengthProperty != null) maxBombLength = (float)maxLengthProperty.GetValue(__instance);

            if (!target.IsValid || target.Cell == firstTarget.Cell) return true;

            if ((target.Cell - firstTarget.Cell).LengthHorizontal > maxBombLength) return true;

            var caller = (Pawn)FieldInHierarchy(instanceType, "caller")?.GetValue(__instance);
            var map = (Map)FieldInHierarchy(instanceType, "map")?.GetValue(__instance);
            var free = (bool)(FieldInHierarchy(instanceType, "free")?.GetValue(__instance) ?? false);
            var permitDef = ((RoyalTitlePermitWorker)__instance).def;
            var faction = (Faction)FieldInHierarchy(instanceType, "callingFaction")?.GetValue(__instance);

            if (caller == null || map == null || permitDef == null) return true;

            SyncedCarpetBombingSpawn(permitDef, caller, map, faction, free, firstTarget.Cell, target.Cell);
            // Clear local firstTarget so UI resets, remote clears inside synced spawn.
            firstTargetField.SetValue(__instance, LocalTargetInfo.Invalid);
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} PreCarpetBombingOrder failed: {exception}");
            return true;
        }
    }

    private static void SyncedCarpetBombingSpawn(RoyalTitlePermitDef permitDef, Pawn caller, Map map, Faction faction,
        bool free, IntVec3 start, IntVec3 end)
    {
        if (permitDef == null || caller == null || map == null) return;

        var worker = permitDef.Worker;
        if (worker == null) return;

        try
        {
            var workerType = worker.GetType();
            FieldInHierarchy(workerType, "caller")?.SetValue(worker, caller);
            FieldInHierarchy(workerType, "map")?.SetValue(worker, map);
            FieldInHierarchy(workerType, "free")?.SetValue(worker, free);
            if (faction != null) FieldInHierarchy(workerType, "callingFaction")?.SetValue(worker, faction);

            FieldInHierarchy(workerType, "firstTarget")?.SetValue(worker, new LocalTargetInfo(start));
            var spawnMethod = MethodInHierarchy(workerType, "SpawnShuttle");

            if (spawnMethod != null)
                spawnMethod.Invoke(worker, [start, end]);
            else
                // Fallback through OrderForceTarget second step.
                ((ITargetingSource)worker).OrderForceTarget(new LocalTargetInfo(end));

            // Consume permit exactly like the original second step.
            try
            {
                var royaltyPawn = caller;
                if (royaltyPawn?.royalty != null && faction != null)
                {
                    royaltyPawn.royalty.GetPermit(permitDef, faction)?.Notify_Used();
                    if (!free) royaltyPawn.royalty.TryRemoveFavor(faction, permitDef.royalAid.favorCost);
                }
            }
            catch (Exception exception)
            {
                Log.Error($"{LogPrefix} SyncedCarpetBombingSpawn permit consume failed: {exception}");
            }

            FieldInHierarchy(workerType, "firstTarget")?.SetValue(worker, LocalTargetInfo.Invalid);
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SyncedCarpetBombingSpawn failed: {exception}");
        }
    }
}