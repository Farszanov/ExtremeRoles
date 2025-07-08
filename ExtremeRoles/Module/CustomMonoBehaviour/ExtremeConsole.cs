using System;

using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using ExtremeRoles.Module.Interface;
using ExtremeRoles.Performance;

#nullable enable

namespace ExtremeRoles.Module.CustomMonoBehaviour;

[Il2CppRegister([ typeof(IUsable) ])]
public sealed class ExtremeConsole : MonoBehaviour, IAmongUs.IUsable
{
	public interface IBehavior
	{
		public float CoolTime { get; }

		public bool IsCheckWall { get; }

		public bool CanUse(NetworkedPlayerInfo pc);

		public void Use();
	}

	[HideFromIl2Cpp]
	public IBehavior? Behavior { get; set; }
	public SpriteRenderer? Image { get; private set; }

	public float UsableDistance => 1.0f;

	public float PercentCool => this.Behavior is null ? 0.0f : this.Behavior.CoolTime;

	public ImageNames UseIcon => ImageNames.UseButton;

	public ExtremeConsole(IntPtr ptr) : base(ptr)
	{
	}

	public void Awake()
	{
		if (!base.TryGetComponent<SpriteRenderer>(out var rend))
		{
			rend = base.gameObject.AddComponent<SpriteRenderer>();
		}
		this.Image = rend;
	}

	public void SetOutline(bool on, bool mainTarget)
	{
		/* マテリアル周りがちゃんと作ってないので無効化しておく
		if (this.Image != null)
		{
			this.Image.material.SetFloat("_Outline", on ? 1 : 0);
			this.Image.material.SetColor("_OutlineColor", Color.yellow);
			this.Image.material.SetColor("_AddColor", mainTarget ? Color.yellow : Color.clear);
		}
		*/
	}

	public float CanUse(NetworkedPlayerInfo pc, out bool canUse, out bool couldUse)
	{
		canUse = false;
		couldUse = false;

		float result = float.MaxValue;

		if (this.Behavior is null ||
			pc.Object == null ||
			!this.Behavior.CanUse(pc))
		{
			return result;
		}

		couldUse = true;
		canUse = couldUse;

		var playerPos = pc.Object.GetTruePosition();
		var objPos = base.transform.position;

		result = Vector2.Distance(playerPos, objPos);
		canUse &= result <= this.UsableDistance;
		if (this.Behavior.IsCheckWall)
		{
			canUse &= !PhysicsHelpers.AnythingBetween(
				playerPos, objPos, Constants.ShadowMask, false);
		}

		return result;
	}

	public void Use()
	{
		PlayerControl? player = PlayerControl.LocalPlayer;
		if (player == null ||
			player.Data == null)
		{
			return;
		}

		this.CanUse(player.Data, out bool canUse, out bool _);

		if (!canUse || this.Behavior is null || Minigame.Instance != null)
		{
			return;
		}
		this.Behavior.Use();
	}
}

/* 後でConsoleの継承に変える
[Il2CppRegister]
public sealed class __ExtremeConsole : Console
{
	public ExtremeConsole.IBehavior? Behavior { get; set; }

	public void Awake()
	{
		this.usableDistance = 1.0f;
		if (!base.TryGetComponent<SpriteRenderer>(out var rend))
		{
			rend = base.gameObject.AddComponent<SpriteRenderer>();
		}
		this.Image = rend;
	}

	public override float CanUse(NetworkedPlayerInfo pc, out bool canUse, out bool couldUse)
	{
		canUse = false;
		couldUse = false;

		if (this.Behavior is null)
		{
			return float.MaxValue;
		}
		return this.Behavior.CanUse(pc, out canUse, out couldUse);
	}

	public override void Use()
	{
		if (this.Behavior is null)
		{
			return;
		}
		this.Behavior.Use();
	}
}
*/