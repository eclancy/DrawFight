using Godot;

/// <summary>
/// The ten non-special attacks every fighter starts from, authored once at MEDIUM weight and
/// retuned per fighter by <see cref="WeightProfiles"/>.
///
/// This is the rule from .ai/character-design.md made concrete: the normal attacks do not need
/// bespoke design per character. Designing ten moves from scratch is how a character takes a
/// month instead of an afternoon, and the four specials are where a character's identity
/// actually lives.
/// </summary>
public static class DefaultMoveset
{
	/// <summary>
	/// Builds a full moveset for a weight class. Specials are passed in because they are the
	/// part that is genuinely per character.
	/// </summary>
	public static MoveData[] Build(WeightClass weight, MoveData[] specials)
	{
		MoveScale scale = WeightProfiles.ScaleFor(weight);
		var moves = new MoveData[(int)MoveSlot.Count];

		moves[(int)MoveSlot.Jab] = Jab().Scaled(scale);
		moves[(int)MoveSlot.ForwardTilt] = ForwardTilt().Scaled(scale);
		moves[(int)MoveSlot.UpTilt] = UpTilt().Scaled(scale);
		moves[(int)MoveSlot.DownTilt] = DownTilt().Scaled(scale);
		moves[(int)MoveSlot.DashAttack] = DashAttack().Scaled(scale);

		moves[(int)MoveSlot.NeutralAir] = NeutralAir().Scaled(scale);
		moves[(int)MoveSlot.ForwardAir] = ForwardAir().Scaled(scale);
		moves[(int)MoveSlot.BackAir] = BackAir().Scaled(scale);
		moves[(int)MoveSlot.UpAir] = UpAir().Scaled(scale);
		moves[(int)MoveSlot.DownAir] = DownAir().Scaled(scale);

		// Specials are NOT scaled. They are authored per character from the kid's own
		// description, so scaling them would quietly overrule what he asked for.
		for (int i = 0; i < 4 && i < specials.Length; i++)
		{
			moves[(int)MoveSlot.NeutralSpecial + i] = specials[i];
		}

		return moves;
	}

	// --- Grounded ------------------------------------------------------------

	/// <summary>Fast, weak, high base and low growth: repositions reliably and never kills.</summary>
	static MoveData Jab() => new MoveData
	{
		MoveName = "Jab",
		StartupFrames = 4, ActiveFrames = 3, EndlagFrames = 11,
		Damage = 7.0f, BaseKnockback = 30.0f, KnockbackGrowth = 0.85f,
		LaunchAngleDegrees = 42.0f,
		HitboxOffset = new Vector2(58.0f, -18.0f), HitboxRadius = 42.0f,
	};

	/// <summary>The spacing tool. Longest reach of the grounded normals.</summary>
	static MoveData ForwardTilt() => new MoveData
	{
		MoveName = "Forward Tilt",
		StartupFrames = 8, ActiveFrames = 3, EndlagFrames = 16,
		Damage = 10.0f, BaseKnockback = 34.0f, KnockbackGrowth = 1.0f,
		LaunchAngleDegrees = 38.0f,
		HitboxOffset = new Vector2(86.0f, -22.0f), HitboxRadius = 48.0f,
	};

	/// <summary>Pops people straight up, which is how a juggle starts.</summary>
	static MoveData UpTilt() => new MoveData
	{
		MoveName = "Up Tilt",
		StartupFrames = 6, ActiveFrames = 4, EndlagFrames = 14,
		Damage = 8.0f, BaseKnockback = 28.0f, KnockbackGrowth = 1.05f,
		LaunchAngleDegrees = 84.0f,
		HitboxOffset = new Vector2(22.0f, -92.0f), HitboxRadius = 52.0f,
	};

	/// <summary>Low and fast, hits people trying to climb back onto the stage.</summary>
	static MoveData DownTilt() => new MoveData
	{
		MoveName = "Down Tilt",
		StartupFrames = 5, ActiveFrames = 3, EndlagFrames = 12,
		Damage = 7.5f, BaseKnockback = 26.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 24.0f,
		HitboxOffset = new Vector2(66.0f, 26.0f), HitboxRadius = 44.0f,
	};

	/// <summary>Committal, slides the whole way through, rewards building up speed.</summary>
	static MoveData DashAttack() => new MoveData
	{
		MoveName = "Dash Attack",
		StartupFrames = 8, ActiveFrames = 4, EndlagFrames = 21,
		Damage = 11.0f, BaseKnockback = 42.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 52.0f,
		HitboxOffset = new Vector2(70.0f, -16.0f), HitboxRadius = 50.0f,
		CarriesMomentum = true,
	};

	// --- Aerials -------------------------------------------------------------

	/// <summary>Quick, all-round, comes out fast enough to get you out of trouble.</summary>
	static MoveData NeutralAir() => new MoveData
	{
		MoveName = "Neutral Air",
		StartupFrames = 5, ActiveFrames = 6, EndlagFrames = 12,
		Damage = 8.0f, BaseKnockback = 26.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 45.0f,
		HitboxOffset = new Vector2(16.0f, -14.0f), HitboxRadius = 62.0f,
	};

	static MoveData ForwardAir() => new MoveData
	{
		MoveName = "Forward Air",
		StartupFrames = 9, ActiveFrames = 3, EndlagFrames = 16,
		Damage = 11.0f, BaseKnockback = 30.0f, KnockbackGrowth = 1.05f,
		LaunchAngleDegrees = 40.0f,
		HitboxOffset = new Vector2(82.0f, -18.0f), HitboxRadius = 48.0f,
	};

	/// <summary>The reliable finisher. Low base, high growth - does nothing until it kills.</summary>
	static MoveData BackAir() => new MoveData
	{
		MoveName = "Back Air",
		StartupFrames = 8, ActiveFrames = 3, EndlagFrames = 18,
		Damage = 12.0f, BaseKnockback = 20.0f, KnockbackGrowth = 1.22f,
		LaunchAngleDegrees = 36.0f,
		HitboxOffset = new Vector2(-78.0f, -18.0f), HitboxRadius = 48.0f,
	};

	static MoveData UpAir() => new MoveData
	{
		MoveName = "Up Air",
		StartupFrames = 6, ActiveFrames = 4, EndlagFrames = 13,
		Damage = 9.0f, BaseKnockback = 24.0f, KnockbackGrowth = 1.15f,
		LaunchAngleDegrees = 88.0f,
		HitboxOffset = new Vector2(8.0f, -86.0f), HitboxRadius = 54.0f,
	};

	/// <summary>
	/// Spikes: launches straight down with no recovery window. The most dangerous move in the
	/// game to be hit by off-stage, and the most committal to throw out.
	/// </summary>
	static MoveData DownAir() => new MoveData
	{
		MoveName = "Down Air",
		StartupFrames = 11, ActiveFrames = 3, EndlagFrames = 22,
		Damage = 13.0f, BaseKnockback = 28.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = -78.0f,
		HitboxOffset = new Vector2(14.0f, 72.0f), HitboxRadius = 50.0f,
		Spikes = true,
	};
}
