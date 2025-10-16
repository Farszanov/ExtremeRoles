
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;


using BepInEx.Unity.IL2CPP.Utils.Collections;
using TMPro;
using UnityEngine;

using ExtremeRoles.Extension.Task;
using ExtremeRoles.GameMode;
using ExtremeRoles.Helper;
using ExtremeRoles.Module.CustomMonoBehaviour.UIPart;
using ExtremeRoles.Module.SystemType;
using ExtremeRoles.Module.SystemType.OnemanMeetingSystem;
using ExtremeRoles.Patches.Controller;
using ExtremeRoles.Performance.Il2Cpp;
using ExtremeRoles.Resources;
using Il2CppInterop.Runtime.Attributes;

using Il2CppObject = Il2CppSystem.Object;



#nullable enable

namespace ExtremeRoles.Module.CustomMonoBehaviour.Minigames;

[Il2CppRegister]
public sealed class ExtremeSpawnSelectorMinigame : Minigame
{
	private bool selected;
	private readonly Controller controller = new Controller();
	private readonly List<SpriteButton> button = new List<SpriteButton>(3);

	private const float buttonYOffset = 0.25f;

	public readonly record struct SpawnPointInfo(string RoomName, string ImgName, float X, float Y)
	{
		[JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public Vector2 Vector => new Vector2(X, Y);
	}

	private static Dictionary<string, SpawnPointInfo[]>? spawnInfo;

	// AirShipの初期スポーンと同じくnew Vector2(-25f, 40f)にしておく
	private static Vector2 waitPos => new Vector2(-25f, 40f);
	public const string JsonPath = "ExtremeRoles.Resources.JsonData.RandomSpawnPoint.json";
	private const int buttonNum = 3;

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
	private TextMeshPro text;
	public ExtremeSpawnSelectorMinigame(IntPtr ptr) : base(ptr)
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
	{ }

	public override void Begin(PlayerTask? task)
	{
		string mapKey = Map.Name;
		if (mapKey == Map.SubmergedKey ||
			mapKey == Map.AirShipKey)
		{
			return;
		}

		if (spawnInfo == null)
		{
			var assembly = Assembly.GetCallingAssembly();
			using var stream = assembly.GetManifestResourceStream(JsonPath);

			if (stream is null) { return; }

			spawnInfo = JsonSerializer.Deserialize<Dictionary<string, SpawnPointInfo[]>>(stream);
		}

		if (spawnInfo == null||
			!spawnInfo.TryGetValue(mapKey, out var usePoints) ||
			usePoints == null)
		{
			return;
		}

		this.AbstractBegin(task);

		var shuffleedPoint = usePoints
			.OrderBy(x => RandomGenerator.Instance.Next())
			.Take(buttonNum)
			.OrderByDescending(x => x.X)
			.ThenByDescending(x => x.Y)
			.ToArray();

		string lowerMap = mapKey.ToLower();
		for (int i = 0; i < buttonNum; ++i)
		{
			var point = shuffleedPoint[i];

			GameObject obj = new GameObject($"selector_button_{i}");
			obj.transform.SetParent(base.transform);
			obj.transform.localPosition = new Vector3(2.5f * (i - 1), buttonYOffset);
			obj.SetActive(true);
			obj.layer = 5;

			var button = obj.AddComponent<SpriteButton>();

			string roomName = point.RoomName;
			string text =
				Enum.TryParse<SystemTypes>(roomName, true, out var systemRoomName) ?
				TranslationController.Instance.GetString(systemRoomName) :
				Tr.GetString(roomName);

			button.Text.text = text;
			button.Rend.sprite = UnityObjectLoader.LoadFromResources<Sprite>(
				string.Format(
					ObjectPath.ExtremeSelectorMinigameAssetFormat, lowerMap),
				string.Format(
					ObjectPath.ExtremeSelectorMinigameImgFormat, lowerMap, roomName));
			button.Colider.size = new Vector2(1.25f, 1.25f);
			button.OnClick = createSpawnAtAction(point.Vector, text);

			this.button.Add(button);
		}

		if (GameManager.Instance != null && GameManager.Instance.IsNormal())
		{
			foreach (NetworkedPlayerInfo playerInfo in GameData.Instance.AllPlayers.GetFastEnumerator())
			{
				if (playerInfo != null &&
					playerInfo.Object != null &&
					playerInfo.Object.TryGetComponent<DummyBehaviour>(out var dummy) &&
					!dummy.enabled &&
					!playerInfo.Disconnected)
				{
					var player = playerInfo.Object.NetTransform;

					player.transform.position = waitPos;
					player.Halt();
				}
			}
		}

		if (!ExtremeGameModeManager.Instance.ShipOption.Spawn.IsAutoSelectRandom)
		{

			base.StartCoroutine(runTimer().WrapToIl2Cpp());

			PlayerControl.HideCursorTemporarily();
			ConsoleJoystick.SetMode_Menu();
		}
		else
		{
			this.Close();
		}
	}

	public override void Close()
	{
		if (!this.selected)
		{
			spawnToRandom();
		}
		this.AbstractClose();
	}

	public void Awake()
	{
		this.text = Instantiate(Prefab.Text, base.transform);
		this.text.alignment = TextAlignmentOptions.Center;
		this.text.gameObject.SetActive(true);
		this.text.transform.localPosition = new Vector3(0.0f, -1.5f);
		this.text.fontSize = text.fontSizeMin = text.fontSizeMax = 4.0f;

		this.MyTask = null;
		this.multistageMinigameChecked = true;
		this.TransType = TransitionType.SlideBottom;
	}

	public void Update()
	{
		if (this.selected ||
			HudManager.Instance == null ||
			HudManager.Instance.Chat.IsOpenOrOpening)
		{
			return;
		}

		this.controller.Update();

		foreach (var button in this.button)
		{
			if (Controller.currentTouchType == Controller.TouchType.Joystick &&
				this.controller.CheckHover(button.Colider))
			{
				button.OnClick?.Invoke();
				return;
			}
		}
	}

	[HideFromIl2Cpp]
	public IEnumerator WaitForFinish()
	{
		yield return null;

		while (this.amClosing == CloseState.None)
		{
			yield return null;
		}
		yield break;
	}

	[HideFromIl2Cpp]
	private IEnumerator runTimer()
	{
		for (float time = 10.0f; time >= 0f; time -= Time.deltaTime)
		{
			this.text.text = TranslationController.Instance.GetString(
				StringNames.TimeRemaining, new Il2CppObject[] { Mathf.CeilToInt(time) });
			yield return null;
		}
		spawnToRandom();
		yield break;
	}

	[HideFromIl2Cpp]
	private Action createSpawnAtAction(Vector2 pos, string name)
	{
		return () =>
		{
			if (this.amClosing != CloseState.None)
			{
				return;
			}
			ExtremeRolesPlugin.Logger.LogInfo($"Player selected spawn point {name}");

			this.selected = true;
			PlayerControl localPlayer = PlayerControl.LocalPlayer;

			localPlayer.SetKinematic(true);
			localPlayer.NetTransform.SetPaused(true);
			Player.RpcUncheckSnap(localPlayer.PlayerId, pos, true);

			HudManager.Instance.PlayerCam.SnapToTarget();

			base.StopAllCoroutines();
			base.StartCoroutine(coSpawnAt(localPlayer).WrapToIl2Cpp());
		};
	}

	[HideFromIl2Cpp]
	private IEnumerator coSpawnAt(PlayerControl playerControl)
	{
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();

		playerControl.SetKinematic(false);
		playerControl.NetTransform.SetPaused(false);
		this.Close();

		yield break;
	}

	private void spawnToRandom()
	{
		int index = RandomGenerator.Instance.Next(0, this.button.Count);
		this.button[index].OnClick?.Invoke();
	}

	// 距離が近いという部分で1.0位近いかCheck
	public static bool IsCloseWaitPos(Vector2 pos)
		=> Vector2.Distance(pos, waitPos) <= 1.0f;

	public static IEnumerator WrapUpAndSpawn(ExileController instance)
	{
		ExileControllerWrapUpPatch.WrapUpPrefix();
		var player = instance.initData.networkedPlayer;
		if (instance.initData.networkedPlayer != null)
		{
			var @object = player.Object;
			if (@object != null)
			{
				@object.Exiled();
			}
			player.IsDead = true;
		}
		ExileControllerWrapUpPatch.WrapUpPostfix(player);

		if (TutorialManager.InstanceExists ||
			!GameManager.Instance.LogicFlow.IsGameOverDueToDeath())
		{
			if (!OnemanMeetingSystemManager.IsActive)
			{
				yield return WaiteSpawn();
			}
			instance.ReEnableGameplay();
		}
		Destroy(instance.gameObject);
		
		GameProgressSystem.Current = GameProgressSystem.Progress.Task;
		
		yield break;
	}

	public static IEnumerator WaiteSpawn()
	{
		GameProgressSystem.Current = GameProgressSystem.Progress.PreTask;

		GameObject obj = new GameObject("SpawnSelector");
		var spawnSelector = obj.AddComponent<ExtremeSpawnSelectorMinigame>();
		spawnSelector.transform.SetParent(Camera.main.transform, false);
		spawnSelector.transform.localPosition = new Vector3(0f, 0f, -600f);

		spawnSelector.Begin(null);

		yield return spawnSelector.WaitForFinish();
		yield break;
	}
}
