using System.Collections;
using HarmonyLib;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static readonly (string TypeName, string FieldName)[] TrackerListsToClear =
    [
        ("WolfeinAllegiance.QuestPart_RecurringRaid", "activeRaiders"),
        ("WolfeinAllegiance.QuestPart_EnemyReinforcement", "activeMonitors"),
        ("WolfeinAllegiance.QuestPart_EndingReinforcement", "activeMonitors"),
        ("WolfeinAllegiance.QuestPart_ThingPlacedMonitor", "activeMonitors")
    ];

    private static void SyncTrackerResets()
    {
        // GameComponent_Allegiance.ResetStaticTrackers clears Quest/Npc/Pickup/Victory/VIP/NightWaiters
        // lists on new game and load, but not the four raid/reinforcement/monitor lists above.
        // Without this, starting a new game in the same session leaves stale monitors behind.
        var gameComponentType = AccessTools.TypeByName("WolfeinAllegiance.GameComponent_Allegiance");
        if (gameComponentType == null)
        {
            Log.Warning($"{LogPrefix} Type not found: WolfeinAllegiance.GameComponent_Allegiance");
            return;
        }

        var resetMethod = AccessTools.DeclaredMethod(gameComponentType, "ResetStaticTrackers");
        if (resetMethod == null)
        {
            Log.Warning($"{LogPrefix} ResetStaticTrackers not found on GameComponent_Allegiance");
            return;
        }

        MpCompat.harmony.Patch(resetMethod,
            postfix: new HarmonyMethod(typeof(WolfeinAllegiance), nameof(PostResetStaticTrackers)));
    }

    private static void PostResetStaticTrackers()
    {
        try
        {
            foreach (var (typeName, fieldName) in TrackerListsToClear)
            {
                var listType = AccessTools.TypeByName(typeName);
                if (listType == null) continue;

                var field = FieldInHierarchy(listType, fieldName);
                if (field?.GetValue(null) is IList list) list.Clear();
            }
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} PostResetStaticTrackers failed: {exception}");
        }
    }
}