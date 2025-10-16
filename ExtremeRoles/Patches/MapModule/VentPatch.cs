using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using ExtremeRoles.Compat;
using ExtremeRoles.Extension.Il2Cpp;
using ExtremeRoles.Extension.VentModule;
using ExtremeRoles.GameMode;
using ExtremeRoles.Module.RoleAssign;
using ExtremeRoles.Module.SystemType;
using ExtremeRoles.Performance;
using ExtremeRoles.Roles;
using ExtremeRoles.Roles.API.Extension.State;
using ExtremeRoles.Roles.Solo.Impostor;
using HarmonyLib;
using System.Collections;
using UnityEngine;
using Il2CppEnumerator = Il2CppSystem.Collections.IEnumerator;


#nullable enable

namespace ExtremeRoles.Patches.MapModule;

[HarmonyPatch(typeof(Vent), nameof(Vent.UsableDistance), MethodType.Getter)]
public static class VentUsableDistancePatch
{
    public static bool Prefix(
        ref float __result)
    {
        if (ExtremeRoleManager.GameRole.Count == 0) { return true; }

        var underWarper = ExtremeRoleManager.GetSafeCastedLocalPlayerRole<UnderWarper>();

        if (underWarper == null ||
            !underWarper.IsAwake) { return true; }

        __result = underWarper.VentUseRange;

        return false;
    }
}

[HarmonyPatch]
public static class VentAnimationRemovePatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
	public static bool VentEnterVentPrefix(
		Vent __instance,
		[HarmonyArgument(0)] PlayerControl pc)
	{
		if (isRunOriginal(__instance, pc))
		{
			return true;
		}
		if (!__instance.EnterVentAnim)
		{
			return false;
		}
		if (pc.AmOwner)
		{
			Vent.currentVent =__instance;
			ConsoleJoystick.SetMode_Vent();
			if (Constants.ShouldPlaySfx())
			{
				SoundManager.Instance.StopSound(ShipStatus.Instance.VentEnterSound);
				SoundManager.Instance.PlaySound(ShipStatus.Instance.VentEnterSound, false, 1f, null).pitch = FloatRange.Next(0.8f, 1.2f);
				VibrationManager.Vibrate(
					0.4f, __instance.transform.position, 3.5f, 0.2f,
					VibrationManager.VibrationFalloff.None, null, false);
			}
		}
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
	public static bool VentExitVentPrefix(
		Vent __instance,
		ref Il2CppEnumerator __result,
		[HarmonyArgument(0)] PlayerControl pc)
	{
		if (isRunOriginal(__instance, pc))
		{
			return true;
		}
		__result = noExitVentAnim(__instance, pc).WrapToIl2Cpp();
		return false;
	}

	private static IEnumerator noExitVentAnim(Vent vent, PlayerControl pc)
	{
		if (pc.AmOwner)
		{
			Vent.currentVent = null;
		}
		if (!vent.ExitVentAnim)
		{
			yield break;
		}
		if (pc.AmOwner && Constants.ShouldPlaySfx())
		{
			AudioClip audioClip = ShipStatus.Instance.VentEnterSound;
			if (ShipStatus.Instance.VentExitSound)
			{
				audioClip = ShipStatus.Instance.VentExitSound;
			}
			else
			{
				SoundManager.Instance.StopSound(ShipStatus.Instance.VentEnterSound);
			}
			SoundManager.Instance.PlaySound(audioClip, false, 1f, null).pitch = FloatRange.Next(0.8f, 1.2f);
			VibrationManager.Vibrate(0.4f, vent.transform.position, 3.5f, 0.2f,
				VibrationManager.VibrationFalloff.None, null, false);
		}
	}

	private static bool isRunOriginal(Vent vent, PlayerControl pc)
		=>
		PlayerControl.LocalPlayer == null ||
		PlayerControl.LocalPlayer.PlayerId == pc.PlayerId ||
		vent.IsCanAnimate();
}

[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
public static class VentCanUsePatch
{
    public static bool Prefix(
        Vent __instance,
        ref float __result,
        [HarmonyArgument(0)] NetworkedPlayerInfo playerInfo,
        [HarmonyArgument(1)] out bool canUse,
        [HarmonyArgument(2)] out bool couldUse)
    {
        float num = float.MaxValue;
        PlayerControl player = playerInfo.Object;

        canUse = couldUse = false;

        if (ExtremeGameModeManager.Instance.ShipOption.Vent.Disable)
        {
            __result = num;
            return false;
        }

        if (__instance.myRend.sprite == null)
        {
            return false;
        }

        bool isCustomMapVent =
			CompatModManager.Instance.TryGetModMap(out var modMap) &&
			modMap.IsCustomVentUse(__instance);

        if (ExtremeRoleManager.GameRole.Count == 0)
        {
            if (isCustomMapVent)
            {
                (__result, canUse, couldUse) = modMap!.IsCustomVentUseResult(
                    __instance, playerInfo,
                    playerInfo.Role.IsImpostor || playerInfo.Role.Role is RoleTypes.Engineer);
                return false;
            }
            return true;
        }

        if (!ExtremeRoleManager.TryGetRole(playerInfo.PlayerId, out var role))
        {
            __result = num;
            return false;
        }

        bool roleCouldUse = role.CanUseVent();

        if (isCustomMapVent)
        {
            (__result, canUse, couldUse) = modMap!.IsCustomVentUseResult(
                __instance, playerInfo, roleCouldUse);
            return false;
        }

        bool inVent = player.inVent;
        bool hasCleanTask = Helper.Player.TryGetTaskType(
            player, TaskTypes.VentCleaning, out NormalPlayerTask task);

        couldUse = (
            !playerInfo.IsDead &&
            roleCouldUse &&
            (
                (
                    !hasCleanTask
                )
                ||
                (
                    !(task != null && task.Data[0] == __instance.Id)
                )
                ||
                (
                    inVent && Vent.currentVent == __instance
                )
            ) &&
            ExtremeGameModeManager.Instance.Usable.CanUseVent(role) &&
            (player.CanMove || inVent)
        );

        if (role.TryGetVanillaRoleId(out _))
        {
            couldUse =
                couldUse &&
                playerInfo.Role.CanUse(__instance.Cast<IUsable>());
        }

        if (ShipStatus.Instance.Systems.TryGetValue(
                SystemTypes.Ventilation, out var systemType) &&
			systemType.IsTryCast<VentilationSystem>(out var ventilationSystem) &&
			ventilationSystem.IsVentCurrentlyBeingCleaned(__instance.Id))
        {
			couldUse = false;
		}

		bool isWallHackVent = UnderWarper.IsWallHackVent;

		canUse = couldUse;
        if (canUse)
        {
            Vector2 playerPos = player.Collider.bounds.center;
            Vector3 position = __instance.transform.position;
            num = Vector2.Distance(playerPos, position);

            canUse &= (
                num <= __instance.UsableDistance &&
				(
					!PhysicsHelpers.AnythingBetween(
						player.Collider, playerPos, position,
						Constants.ShipOnlyMask, false) ||
					isWallHackVent
				));
        }

        __result = num;
        return false;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.SetOutline))]
public static class VentSetOutlinePatch
{
    public static bool Prefix(
        Vent __instance,
        [HarmonyArgument(0)] bool on,
        [HarmonyArgument(1)] bool mainTarget)
    {
        if (ExtremeRoleManager.GameRole.Count == 0) { return true; }

        var role = ExtremeRoleManager.GetLocalPlayerRole();

        if (role.IsVanillaRole() || role.IsImpostor()) { return true; }

        Color color = role.GetNameColor();

        __instance.myRend.material.SetFloat("_Outline", (float)(on ? 1 : 0));
        __instance.myRend.material.SetColor("_OutlineColor", color);
        __instance.myRend.material.SetColor("_AddColor", mainTarget ? color : Color.clear);

        return false;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.Use))]
public static class VentUsePatch
{
    public static bool Prefix(Vent __instance)
    {
        bool canUse;
        bool couldUse;

        PlayerControl localPlayer = PlayerControl.LocalPlayer;

        __instance.CanUse(
            localPlayer.Data,
            out canUse, out couldUse);

        // No need to execute the native method as using is disallowed anyways
        if (!canUse || localPlayer.walkingToVent) { return false; };

        bool isEnter = !localPlayer.inVent;

        if (__instance.IsModed())
        {
            __instance.SetButtons(isEnter);

            using (var caller = RPCOperator.CreateCaller(
                RPCOperator.Command.CustomVentUse))
            {
                caller.WritePackedInt(__instance.Id);
                caller.WriteByte(localPlayer.PlayerId);
                caller.WriteByte(isEnter ? byte.MaxValue : (byte)0);
            }
            RPCOperator.CustomVentUse(
                __instance.Id,
                localPlayer.PlayerId,
                isEnter ? byte.MaxValue : (byte)0);

            __instance.SetButtons(isEnter);

            return false;
        }

        if (GameProgressSystem.IsTaskPhase &&
			UnderWarper.IsNoAnimateVent)
        {
			UnderWarper.RpcUseVentWithNoAnimation(
					localPlayer, __instance.Id, isEnter);
			__instance.SetButtons(isEnter);
			return false;
		}

        if (isEnter)
        {
            localPlayer.MyPhysics.RpcEnterVent(__instance.Id);
        }
        else
        {
            localPlayer.MyPhysics.RpcExitVent(__instance.Id);
        }

        __instance.SetButtons(isEnter);
        return false;
    }
}
