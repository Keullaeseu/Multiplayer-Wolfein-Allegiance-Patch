using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Multiplayer.Compat;
using Verse;

namespace MultiplayerWolfeinAllegiancePatch.Source.Mods;

/// <summary>
///     Multiplayer Patch for Wolfein Allegiance by leopoko,
///     Last Update: 1 Sep @ 3:08pm 2026
///     <see href="https://steamcommunity.com/sharedfiles/filedetails/?id=3707497233" />
/// </summary>
[MpCompatFor("leopoko.wolfeinallegiance")]
public partial class WolfeinAllegiance
{
    private const string LogPrefix = "[Multiplayer Wolfein Allegiance Patch]";

    public WolfeinAllegiance(ModContentPack content)
    {
        MpCompatPatchLoader.LoadPatch(this);
        LongEventHandler.ExecuteWhenFinished(LatePatch);
    }

    private static void LatePatch()
    {
        Log.Message($"{LogPrefix} Initializing...");

        try
        {
            SyncTargetedPermits();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed syncing targeted permits: {exception}");
        }

        try
        {
            SyncCarpetBombing();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed syncing carpet bombing: {exception}");
        }

        try
        {
            SyncUntargetedPermits();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed syncing untargeted permits: {exception}");
        }

        try
        {
            SyncCaravanAndUseEffects();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed syncing caravan/use effects: {exception}");
        }

        try
        {
            SyncDialogsAndEndings();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed syncing dialogs/endings: {exception}");
        }

        try
        {
            SyncVerbs();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed syncing verbs: {exception}");
        }

        try
        {
            SyncTrackerResets();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed syncing tracker resets: {exception}");
        }

        try
        {
            PatchRandomNumberGeneration();
        }
        catch (Exception exception)
        {
            Log.Error($"{LogPrefix} Failed patching RNG: {exception}");
        }

        Log.Message($"{LogPrefix} Initialized.");
    }

    private static void SyncMethodByName(string typeName, string methodName, bool cancelIfAnyArgNull = false,
        SyncContext context = SyncContext.MapSelected)
    {
        var targetType = AccessTools.TypeByName(typeName);
        if (targetType == null)
        {
            Log.Warning($"{LogPrefix} Type not found: {typeName}");
            return;
        }

        var method = AccessTools.DeclaredMethod(targetType, methodName);
        if (method == null)
        {
            Log.Warning($"{LogPrefix} Method not found: {typeName}:{methodName}");
            return;
        }

        var sync = MP.RegisterSyncMethod(method).SetContext(context);
        if (cancelIfAnyArgNull) sync.CancelIfAnyArgNull();
    }

    private static FieldInfo FieldInHierarchy(Type type, string fieldName)
    {
        while (type != null)
        {
            var field = AccessTools.DeclaredField(type, fieldName);
            if (field != null) return field;
            type = type.BaseType;
        }

        return null;
    }

    private static MethodInfo MethodInHierarchy(Type type, string methodName)
    {
        while (type != null)
        {
            var method = AccessTools.DeclaredMethod(type, methodName);
            if (method != null) return method;
            type = type.BaseType;
        }

        return null;
    }
}