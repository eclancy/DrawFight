using Godot;

/// <summary>
/// The numbers behind <see cref="FighterData.HasHeat"/> - DoomBot's furnace - and the one move
/// that spends it, <see cref="SpecialKind.Vent"/>.
///
/// Heat is a loop the player steers: fight to build it up, then choose when to let it out.
/// Letting it out at full heat is the strongest thing he can do; waiting too long for the
/// perfect moment overheats him and stalls him in the open. Both ends of that are his: "the
/// furnace blast" and "overheats" are from his own character sheet.
///
/// All timing is in frames at 60 Hz, like everything else.
/// </summary>
public static class Heat
{
	public const float Max = 100.0f;

	/// <summary>Every attack he starts warms him a little - swinging is work.</summary>
	public const float PerAttack = 3.0f;

	/// <summary>Per point of damage he deals, from any of his moves or hazards. The main source.</summary>
	public const float PerDamageDealt = 0.9f;

	/// <summary>Per point of damage he takes. Being hit stokes him a little, so a losing fight still builds.</summary>
	public const float PerDamageTaken = 0.35f;

	/// <summary>How long after he last gained heat before he starts cooling.</summary>
	public const int CoolDelayFrames = 120;

	/// <summary>Cooling per frame once he has been idle: about six heat a second.</summary>
	public const float CoolPerFrame = 0.1f;

	/// <summary>How long he can sit at full heat before he overheats - five seconds to use it.</summary>
	public const int OverheatAfterFrames = 300;

	/// <summary>How long an overheat stalls him: no control, steam pouring off him.</summary>
	public const int OverheatStallFrames = 70;

	/// <summary>An overheat lets some heat out as steam, but not all - he is still warm after.</summary>
	public const float AfterOverheat = 35.0f;

	/// <summary>Below this much heat a vent is only a puff of steam, not flame.</summary>
	public const float FlameFrom = 0.25f;

	/// <summary>Heat is rounded to this many steps when a vent is built, so each size is made once.</summary>
	public const int VentSteps = 10;

	/// <summary>
	/// The vent move at a heat fraction (0..1), from the move as authored, which is the full-heat
	/// version. Below <see cref="FlameFrom"/> it is a weak puff of steam that only pushes people
	/// off; from there to full it grows from a small fire to the full blast, burning everyone it
	/// catches. Knockback growth rises with it but base knockback stays put, so even the biggest
	/// blast does not have both high (see RegressionChecks.CheckNoMoveIsAlwaysCorrect).
	/// </summary>
	public static MoveData VentAt(MoveData full, float heat)
	{
		MoveData vent = full.Scaled(new MoveScale(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f));
		vent.Sound = "";

		if (heat < FlameFrom)
		{
			vent.MoveName = full.MoveName + " (steam)";
			vent.Damage = 3.0f;
			vent.BaseKnockback = 30.0f;
			vent.KnockbackGrowth = 0.3f;
			vent.LaunchAngleDegrees = 30.0f;
			vent.FxRadius = 90.0f;
			vent.FxFlame = false;
			vent.FxColor = new Color(0.92f, 0.93f, 0.95f);
			vent.BurnFrames = 0;
			vent.BurnDamage = 0.0f;
			return vent;
		}

		float t = (heat - FlameFrom) / (1.0f - FlameFrom);
		vent.MoveName = $"{full.MoveName} ({Mathf.RoundToInt(heat * 100.0f)}% heat)";
		vent.Damage = Mathf.Lerp(7.0f, full.Damage, t);
		vent.KnockbackGrowth = Mathf.Lerp(0.55f, full.KnockbackGrowth, t);
		vent.FxRadius = Mathf.Lerp(110.0f, full.FxRadius, t);
		vent.BurnFrames = Mathf.RoundToInt(Mathf.Lerp(60.0f, full.BurnFrames, t));
		vent.BurnDamage = Mathf.Lerp(2.0f, full.BurnDamage, t);
		return vent;
	}
}
