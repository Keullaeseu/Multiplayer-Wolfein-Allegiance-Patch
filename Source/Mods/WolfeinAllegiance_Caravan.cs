namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

public partial class WolfeinAllegiance
{
    private static void SyncCaravanAndUseEffects()
    {
        SyncMethodByName("WolfeinAllegiance.WorldObjectComp_SupplyDelivery", "Fulfill", true);
        SyncMethodByName("WolfeinAllegiance.WorldObjectComp_PrepSupplyDelivery", "Fulfill", true);
        SyncMethodByName("WolfeinAllegiance.CompUseEffect_HackDevice", "DoEffect", true);
    }
}