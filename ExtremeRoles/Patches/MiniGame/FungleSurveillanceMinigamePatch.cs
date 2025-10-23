using ExtremeRoles.Module.RoleAssign;
using ExtremeRoles.Module.SystemType;
using ExtremeRoles.Module.SystemType.SecurityDummySystem;
using ExtremeRoles.Roles.API.Extension.State;
using HarmonyLib;

namespace ExtremeRoles.Patches.MiniGame;

[HarmonyPatch(typeof(FungleSurveillanceMinigame), nameof(FungleSurveillanceMinigame.Begin))]
public static class FungleSurveillanceMinigameBeginPatch
{
	public static void Postfix()
	{
		if (SecurityDummySystemManager.TryGet(out var system) &&
			system.IsActive)
		{
			system.PostfixBegin();
		}
	}
}

[HarmonyPatch(typeof(FungleSurveillanceMinigame), nameof(FungleSurveillanceMinigame.Close))]
public static class FungleSurveillanceMinigameClosePatch
{
	public static void Postfix()
	{
		if (SecurityDummySystemManager.TryGet(out var system) &&
			system.IsActive)
		{
			system.PostfixClose();
		}
	}
}

[HarmonyPatch(typeof(FungleSurveillanceMinigame), nameof(FungleSurveillanceMinigame.Update))]
public static class FungleSurveillanceMinigameePatch
{
    public static bool Prefix(FungleSurveillanceMinigame __instance)
    {
        if (!GameProgressSystem.IsTaskPhase)
		{
			return true;
		}
		__instance.viewport.enabled =
			Roles.ExtremeRoleManager.GetLocalPlayerRole().CanUseSecurity() ||
			SecurityHelper.IsAbilityUse();

		return true;
    }
    public static void Postfix(FungleSurveillanceMinigame __instance)
    {
        SecurityHelper.PostUpdate(__instance);
    }
}
