using System.Collections.Generic;

using UnityEngine;

using HarmonyLib;

using ExtremeRoles.Helper;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Extension.State;
using ExtremeRoles.Performance.Il2Cpp;

namespace ExtremeRoles.Patches.DebugPatch;


#if DEBUG
[HarmonyPatch(typeof(KeyboardJoystick), nameof(KeyboardJoystick.Update))]
public static class DebugTool
{
    private static List<PlayerControl> bots = new List<PlayerControl>();
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(KeyboardJoystick __instance)
    {
        // ExtremeRolesPlugin.Logger.LogInfo($"DebugMode: {ExtremeRolesPlugin.DebugMode.Value}");

        if (!ExtremeRolesPlugin.DebugMode.Value ||
            AmongUsClient.Instance == null ||
            PlayerControl.LocalPlayer == null) { return; }
        if (!AmongUsClient.Instance.AmHost) { return; }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            foreach (var (key, value) in TranslationController.Instance.currentLanguage.AllStrings)
            {
                Logging.Debug($"TransKey:{key}    Value:{value}");
            }
        }

        if (Key.IsShift() && Input.GetKeyDown(KeyCode.F2))
        {
            Logging.Debug("Set Wide Resolution");
            ResolutionManager.SetResolution(1680, 720, false);
        }

        // Spawn dummys
        if ((Input.GetKeyDown(KeyCode.F)) && GameSystem.IsLobby)
        {
            GameSystem.SpawnDummyPlayer();
        }

        // Terminate round
        if (Input.GetKeyDown(KeyCode.F1) && !GameSystem.IsLobby)
        {
            GameSystem.ForceEndGame();
        }

        // See All roles
        if (Input.GetKeyDown(KeyCode.K))
        {
            var dict = Roles.ExtremeRoleManager.GameRole;
            if (dict.Count == 0) { return; }

            foreach (KeyValuePair<byte, SingleRoleBase> value in dict)
            {
                Logging.Debug(
                    $"PlayerId:{value.Key}    AssignedTo:{value.Value.RoleName}   Team:{value.Value.Core.Team}");
            }
        }

        // See All task
        if (Input.GetKeyDown(KeyCode.P))
        {
            var dict = Roles.ExtremeRoleManager.GameRole;
            if (dict.Count == 0)
			{
				return;
			}
            foreach (var player in GameData.Instance.AllPlayers.GetFastEnumerator())
            {
                if (player == null ||
					!Roles.ExtremeRoleManager.TryGetRole(player.PlayerId, out var role) ||
					!role.HasTask())
                {
                    continue;
                }
                var (playerCompleted, playerTotal) = GameSystem.GetTaskInfo(player);
                Logging.Debug($"PlayerName:{player.PlayerName}  TotalTask:{playerTotal}   ComplatedTask:{playerCompleted}");
            }
        }

        // See Player TaskInfo
        if (Input.GetKeyDown(KeyCode.I))
        {
            var dict = Roles.ExtremeRoleManager.GameRole;
            if (dict.Count == 0)
			{
				return;
			}
			foreach (var player in GameData.Instance.AllPlayers.GetFastEnumerator())
			{
				if (player == null ||
					!Roles.ExtremeRoleManager.TryGetRole(player.PlayerId, out var role) ||
					!role.HasTask())
				{
					continue;
				}
				var (_, totalTask) = GameSystem.GetTaskInfo(player);
                if (totalTask == 0)
                {
                    int taskId = GameSystem.GetRandomCommonTaskId();
                    Logging.Debug($"PlayerName:{player.PlayerName}  AddTask:{taskId}");
                    GameSystem.SetTask(player, taskId);
                }

            }
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            var player = PlayerControl.LocalPlayer;
            GameSystem.CreateNoneReportableDeadbody(
                player, player.transform.position + new Vector3(0.75f, 0.75f));
        }
    }
}
#endif
