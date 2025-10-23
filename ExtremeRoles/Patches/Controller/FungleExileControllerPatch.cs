
using System.Collections;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using UnityEngine;

using ExtremeRoles.GameMode;
using ExtremeRoles.Module.CustomMonoBehaviour.Minigames;
using ExtremeRoles.Module.SystemType;

using Il2CppEnumerator = Il2CppSystem.Collections.IEnumerator;

namespace ExtremeRoles.Patches.Controller;

[HarmonyPatch(typeof(FungleExileController), nameof(SkeldExileController.Animate))]
public static class FungleExileControllerAnimePatch
{
	public static bool Prefix(FungleExileController __instance, ref Il2CppEnumerator __result)
	{
		var spawnOpt = ExtremeGameModeManager.Instance.ShipOption.Spawn;

		GameProgressSystem.Current = GameProgressSystem.Progress.Exiled;

		if (spawnOpt.EnableSpecialSetting &&
			spawnOpt.Fungle)
		{
			__result = animateWithRandomSpawn(__instance).WrapToIl2Cpp();
			return false;
		}
		return true;
	}

	private static IEnumerator animateWithRandomSpawn(FungleExileController __instance)
	{
		var sound = SoundManager.Instance;
		var hud = HudManager.Instance;

		sound.PlayNamedSound("ejection_beach_sfx", __instance.ambience, true, SoundManager.Instance.SfxChannel);
		if (__instance.initData == null ||
			__instance.initData.outfit == null)
		{
			__instance.Player.gameObject.SetActive(false);
			__instance.raftAnimation.SetActive(false);
		}
		if (__instance.initData != null &&
			__instance.initData.outfit != null &&
			__instance.EjectSound != null)
		{
			sound.PlaySound(__instance.EjectSound, false, 1f, SoundManager.Instance.SfxChannel);
		}

		if (hud != null)
		{
			yield return hud.CoFadeFullScreen(Color.black, Color.clear, 0.2f, false);
		}
		yield return Effects.Wait(0.5f);
		yield return Effects.All(
		[
			__instance.FadeBlackRaftAndPlayer(),
			__instance.HandleText(0.2f, 2f)
		]);
		if (__instance.initData != null &&
			__instance.initData.confirmImpostor)
		{
			__instance.ImpostorText.gameObject.SetActive(true);
		}

		yield return Effects.Bloop(0f, __instance.ImpostorText.transform, 1f, 0.5f);
		yield return new WaitForSeconds(2f);

		if (hud != null)
		{
			yield return hud.CoFadeFullScreen(Color.clear, Color.black, 0.2f, false);
		}
		else
		{
			yield return Effects.Wait(0.2f);
		}

		SoundManager.Instance.StopNamedSound("ejection_beach_sfx");
		SoundManager.Instance.StopNamedSound("ejection_fire_sfx");

		yield return ExtremeSpawnSelectorMinigame.WrapUpAndSpawn(__instance);

		yield break;
	}
}
