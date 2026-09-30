using HarmonyLib;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static void PatchRandomNumberGeneration()
    {
        string[] pushPopMethods =
        [
            "WolfeinAllegiance.Bombardment_EMP:Tick",
            "WolfeinAllegiance.Bombardment_DangerClose:Tick",
            "WolfeinAllegiance.CarpetBombingShuttle:Tick",
            "WolfeinAllegiance.QuestPart_RecurringRaid:CheckTick",
            "WolfeinAllegiance.QuestPart_RecurringRaid:ExecuteRaid",
            "WolfeinAllegiance.QuestPart_TriggerFriendlyDrop:DoFriendlyDrop",
            "WolfeinAllegiance.QuestPart_WolfeinShuttleReinforcement:DoReinforcement",
            "WolfeinAllegiance.QuestPart_EnemyReinforcement:SpawnEnemyReinforcement",
            "WolfeinAllegiance.PermitWorker_RegularArmy:DeployArmy",
            "WolfeinAllegiance.PermitWorker_RegularArmy:DoSmoke",
            "WolfeinAllegiance.MapComponent_EscapeIntro:SpawnPawns",
            "WolfeinAllegiance.MapComponent_EscapeIntro:DegradeEquipmentQuality",
            "WolfeinAllegiance.LordToil_FriendlyArtillery:Init",
            "WolfeinAllegiance.QuestRewardHelper:GenerateReward",
            "WolfeinAllegiance.CompUseEffect_HackDevice:DoEffect"
        ];

        foreach (var methodName in pushPopMethods)
        {
            var method = AccessTools.DeclaredMethod(methodName) ?? AccessTools.Method(methodName);
            if (method == null) continue;

            PatchingUtilities.PatchPushPopRand(method);
        }

        // Prisoner-to-shuttle float menu creates Haul jobs, jobs are synced by MP core,
        // but guard the option generator from running stale shuttle lookups in interface.
        try
        {
            MpCompat.RegisterLambdaMethod("WolfeinAllegiance.Patch_FloatMenu_LoadPrisonerToShuttle", "Postfix", 0);
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not register prisoner shuttle float menu lambda: {exception}");
        }
    }
}