using Godot;

/// <summary>
/// Every fighter is one of three weights, and the weight is not only a survivability stat - it
/// retunes the whole moveset. See .ai/character-design.md.
///
/// Heavy hits harder and slower, light hits faster and weaker, medium is the baseline the moves
/// are authored at. That relationship is what makes picking a weight a real choice rather than
/// a number on a select screen.
/// </summary>
public enum WeightClass
{
	Light,
	Medium,
	Heavy,
}

/// <summary>Multipliers applied to a baseline move to express a weight class.</summary>
public readonly struct MoveScale
{
	public readonly float Startup;
	public readonly float Endlag;
	public readonly float Damage;
	public readonly float Knockback;

	/// <summary>
	/// Scales knockback GROWTH, and moves opposite to damage on purpose.
	///
	/// Knockback already depends on damage, so scaling a heavy fighter's damage up raises its
	/// knockback for free. Scaling growth up as well double-dips, and the two compound: the
	/// first pass of this gave the heavy fighter a back air that KOed at 50%. Growth comes
	/// down for heavies to pay for the damage they gained.
	/// </summary>
	public readonly float Growth;

	public readonly float Range;

	public MoveScale(float startup, float endlag, float damage, float knockback, float growth, float range)
	{
		Startup = startup;
		Endlag = endlag;
		Damage = damage;
		Knockback = knockback;
		Growth = growth;
		Range = range;
	}
}

public static class WeightProfiles
{
	/// <summary>
	/// The numbers that turn one authored moveset into three. Deliberately not symmetrical:
	/// heavy pays more in endlag than it gains in startup, because a heavy fighter should be
	/// punishable when it misses rather than simply slower to start.
	/// </summary>
	public static MoveScale ScaleFor(WeightClass weight)
	{
		switch (weight)
		{
			case WeightClass.Light:
				return new MoveScale(0.80f, 0.84f, 0.76f, 0.88f, 1.08f, 0.92f);
			case WeightClass.Heavy:
				return new MoveScale(1.30f, 1.34f, 1.26f, 1.10f, 0.78f, 1.12f);
			default:
				return new MoveScale(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
		}
	}

	/// <summary>The body weight used by the knockback formula. 100 is average.</summary>
	public static float BodyWeight(WeightClass weight)
	{
		switch (weight)
		{
			case WeightClass.Light: return 82.0f;
			case WeightClass.Heavy: return 128.0f;
			default: return 100.0f;
		}
	}

	public static string Describe(WeightClass weight)
	{
		switch (weight)
		{
			case WeightClass.Light: return "Light";
			case WeightClass.Heavy: return "Heavy";
			default: return "Medium";
		}
	}

	/// <summary>
	/// The one hard balance rule, checked rather than trusted: no fighter is above average in
	/// both weight and run speed. A heavy fighter that also runs fast is simply better than
	/// everyone else, and that is the failure mode that ends couch multiplayer.
	/// </summary>
	public static bool BreaksWeightSpeedRule(WeightClass weight, float runSpeed, float mediumRunSpeed)
	{
		return weight == WeightClass.Heavy && runSpeed > mediumRunSpeed;
	}
}

/// <summary>
/// Every attack a fighter has. Specials are authored per character; everything else starts from
/// the shared default moveset and is retuned by weight class.
/// </summary>
public enum MoveSlot
{
	Jab,
	ForwardTilt,
	UpTilt,
	DownTilt,
	DashAttack,

	NeutralAir,
	ForwardAir,
	BackAir,
	UpAir,
	DownAir,

	// Flick the stick and press attack together. Slow, strong, and chargeable by holding attack.
	ForwardSmash,
	UpSmash,
	DownSmash,

	NeutralSpecial,
	SideSpecial,
	UpSpecial,
	DownSpecial,

	Count,
}
