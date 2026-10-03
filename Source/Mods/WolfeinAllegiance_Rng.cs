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
            "WolfeinAllegiance.QuestRewardHelper:AddItemsChoice",
            "WolfeinAllegiance.QuestRewardHelper:AddWolfeinEquipment",
            "WolfeinAllegiance.CompUseEffect_HackDevice:DoEffect"
        ];

        foreach (var methodName in pushPopMethods)
        {
            var method = AccessTools.DeclaredMethod(methodName) ?? AccessTools.Method(methodName);
            if (method == null)
            {
                Log.Warning($"{LogPrefix} RNG method not found: {methodName}");
                continue;
            }

            PatchingUtilities.PatchPushPopRand(method);
        }

        // Prisoner-to-shuttle float menu action captures colonist/prisoner/shuttle/transporter
        // locals and queues a HaulToTransporter job. Must go through RegisterLambdaDelegate,
        // not RegisterLambdaMethod, so the captured fields are synced (same rule as the
        // Wolfein Race patch float menus: AncientUrbanRuins/GiddyUp2 pattern).
        try
        {
            MpCompat.RegisterLambdaDelegate("WolfeinAllegiance.Patch_FloatMenu_LoadPrisonerToShuttle", "Postfix", 0);
        }
        catch (Exception exception)
        {
            Log.Warning($"{LogPrefix} Could not register prisoner shuttle float menu lambda: {exception}");
        }
    }
}