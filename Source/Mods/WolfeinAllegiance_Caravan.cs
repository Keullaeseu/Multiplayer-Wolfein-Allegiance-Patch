using Multiplayer.API;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static void SyncCaravanAndUseEffects()
    {
        // World-map caravan actions run with world selection, not map selection (vanilla
        // TradeRequestComp.Fulfill uses None). HackDevice DoEffect stays MapSelected.
        SyncMethodByName("WolfeinAllegiance.WorldObjectComp_SupplyDelivery", "Fulfill", true,
            SyncContext.None);
        SyncMethodByName("WolfeinAllegiance.WorldObjectComp_PrepSupplyDelivery", "Fulfill", true,
            SyncContext.None);
        SyncMethodByName("WolfeinAllegiance.CompUseEffect_HackDevice", "DoEffect", true);
    }
}