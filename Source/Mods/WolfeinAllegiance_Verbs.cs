using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static void SyncVerbs()
    {
        MP.RegisterSyncMethod(typeof(WolfeinAllegiance), nameof(SyncedVerbShot))
            .SetContext(SyncContext.MapSelected);

        PatchVerb("WolfeinAllegiance.Verb_MechGuidance", nameof(PreMechGuidanceShot));
        PatchVerb("WolfeinAllegiance.Verb_ReconSatellite", nameof(PreReconSatelliteShot));
    }

    private static void PatchVerb(string typeName, string prefixName)
    {
        var verbType = AccessTools.TypeByName(typeName);
        if (verbType == null)
        {
            Log.Warning($"{LogPrefix} Verb type not found: {typeName}");
            return;
        }

        var tryCastShot = AccessTools.DeclaredMethod(verbType, "TryCastShot");
        if (tryCastShot == null)
        {
            Log.Warning($"{LogPrefix} TryCastShot not found on: {typeName}");
            return;
        }

        MpCompat.harmony.Patch(tryCastShot,
            new HarmonyMethod(typeof(WolfeinAllegiance), prefixName));
    }

    private static bool PreMechGuidanceShot(Verb __instance, ref bool __result)
    {
        return PreVerbShot(__instance, ref __result);
    }

    private static bool PreReconSatelliteShot(Verb __instance, ref bool __result)
    {
        return PreVerbShot(__instance, ref __result);
    }

    private static bool PreVerbShot(Verb verb, ref bool result)
    {
        if (!MP.InInterface) return true;

        try
        {
            var owner = verb.DirectOwner as ThingComp;
            if (owner == null) return true;

            SyncedVerbShot(owner, verb.loadID);
            result = true;
            return false;
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} PreVerbShot failed: {exception}");
            return true;
        }
    }

    private static void SyncedVerbShot(ThingComp verbOwnerComp, string loadID)
    {
        if (verbOwnerComp == null || loadID == null) return;

        try
        {
            var verbOwner = verbOwnerComp as IVerbOwner;
            var verb = verbOwner?.VerbTracker?.AllVerbs?.FirstOrDefault(candidate => candidate.loadID == loadID);
            verb?.TryCastShot();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} SyncedVerbShot failed: {exception}");
        }
    }
}