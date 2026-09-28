using Godot;

/// <summary>
/// The four specials for each example fighter.
///
/// These are placeholders built to the shape a real kid's answers would take, so the machinery
/// is ready when the character sheets come back. When they do, a character's specials get
/// written here from HIS description - see .ai/character-design.md - and these go away with the
/// stick figures.
///
/// Two rules hold whatever a kid asks for:
///   - the UP special must give real vertical recovery, always, or the fighter is unplayable
///   - the NEUTRAL special is the signature, built as literally as it can be built
/// </summary>
public static class Specials
{
	// =========================================================================
	// SWIFT - fire
	// =========================================================================

	public static MoveData[] Fire()
	{
		return new[] { Fireball(), FlameDash(), FlareJump(), EmberTrap() };
	}

	/// <summary>Neutral: the signature. A fireball, built as literally as it can be built.</summary>
	static MoveData Fireball() => new MoveData
	{
		MoveName = "Fireball",
		StartupFrames = 12, ActiveFrames = 2, EndlagFrames = 22,
		Damage = 9.0f, BaseKnockback = 26.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = 44.0f,
		HitboxOffset = new Vector2(60.0f, -20.0f), HitboxRadius = 36.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 1050.0f, SpecialGravity = 240.0f, SpecialLifetime = 78,
		FxColor = new Color(0.97f, 0.52f, 0.20f), FxRadius = 34.0f,
	};

	/// <summary>Side: closes distance wrapped in flame. Approach tool.</summary>
	static MoveData FlameDash() => new MoveData
	{
		MoveName = "Flame Dash",
		StartupFrames = 10, ActiveFrames = 8, EndlagFrames = 24,
		Damage = 10.0f, BaseKnockback = 34.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 48.0f,
		HitboxOffset = new Vector2(44.0f, -16.0f), HitboxRadius = 56.0f,
		CarriesMomentum = true,
		Special = SpecialKind.Dash,
		SpecialSpeed = 1450.0f,
		FxColor = new Color(0.99f, 0.66f, 0.24f), FxRadius = 40.0f,
	};

	/// <summary>
	/// Up: the recovery. Rides a jet of fire upward. Non-negotiable that this gets you back
	/// to the stage - a fighter who cannot recover from below is unplayable whatever else it has.
	/// </summary>
	static MoveData FlareJump() => new MoveData
	{
		MoveName = "Flare Jump",
		StartupFrames = 7, ActiveFrames = 10, EndlagFrames = 20,
		Damage = 8.0f, BaseKnockback = 28.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = 76.0f,
		HitboxOffset = new Vector2(10.0f, -30.0f), HitboxRadius = 52.0f,
		Special = SpecialKind.Recovery,
		SpecialRise = 1750.0f, SpecialSpeed = 340.0f,
		FxColor = new Color(0.99f, 0.78f, 0.30f), FxRadius = 42.0f,
	};

	/// <summary>Down: leaves a patch of fire burning on the ground. Zoning and ledge control.</summary>
	static MoveData EmberTrap() => new MoveData
	{
		MoveName = "Ember Trap",
		StartupFrames = 14, ActiveFrames = 2, EndlagFrames = 26,
		Damage = 6.0f, BaseKnockback = 40.0f, KnockbackGrowth = 0.6f,
		LaunchAngleDegrees = 70.0f,
		HitboxOffset = new Vector2(48.0f, 20.0f), HitboxRadius = 44.0f,
		Special = SpecialKind.Trap,
		SpecialLifetime = 260,
		FxColor = new Color(0.95f, 0.42f, 0.22f), FxRadius = 46.0f,
	};

	// =========================================================================
	// LUG - heavy weapons
	// =========================================================================

	public static MoveData[] HeavyWeapons()
	{
		return new[] { HammerThrow(), AnvilCharge(), HammerJump(), AnvilDrop() };
	}

	/// <summary>Neutral: the signature. Hurls the hammer, which arcs heavily and hits hard.</summary>
	static MoveData HammerThrow() => new MoveData
	{
		MoveName = "Hammer Throw",
		StartupFrames = 19, ActiveFrames = 2, EndlagFrames = 30,
		Damage = 15.0f, BaseKnockback = 36.0f, KnockbackGrowth = 1.05f,
		LaunchAngleDegrees = 44.0f,
		HitboxOffset = new Vector2(74.0f, -26.0f), HitboxRadius = 44.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 820.0f, SpecialGravity = 900.0f, SpecialLifetime = 110,
		FxColor = new Color(0.46f, 0.50f, 0.62f), FxRadius = 40.0f,
	};

	/// <summary>Side: shoulders an anvil and charges. Slow to start, very hard to stop.</summary>
	static MoveData AnvilCharge() => new MoveData
	{
		MoveName = "Anvil Charge",
		StartupFrames = 16, ActiveFrames = 10, EndlagFrames = 32,
		Damage = 16.0f, BaseKnockback = 44.0f, KnockbackGrowth = 1.0f,
		LaunchAngleDegrees = 42.0f,
		HitboxOffset = new Vector2(62.0f, -18.0f), HitboxRadius = 64.0f,
		CarriesMomentum = true,
		Special = SpecialKind.Dash,
		SpecialSpeed = 1180.0f,
		FxColor = new Color(0.42f, 0.46f, 0.58f), FxRadius = 46.0f,
	};

	/// <summary>
	/// Up: the recovery. Swings the hammer down hard enough to throw himself up. A heavy
	/// fighter still needs a real way home, and this is deliberately the shortest one in the
	/// game rather than a missing one.
	/// </summary>
	static MoveData HammerJump() => new MoveData
	{
		MoveName = "Hammer Jump",
		StartupFrames = 10, ActiveFrames = 8, EndlagFrames = 26,
		Damage = 13.0f, BaseKnockback = 32.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 80.0f,
		HitboxOffset = new Vector2(16.0f, -34.0f), HitboxRadius = 58.0f,
		Special = SpecialKind.Recovery,
		SpecialRise = 1480.0f, SpecialSpeed = 260.0f,
		FxColor = new Color(0.50f, 0.54f, 0.66f), FxRadius = 46.0f,
	};

	/// <summary>Down: drops an anvil. Spikes whatever is underneath, which off-stage is lethal.</summary>
	static MoveData AnvilDrop() => new MoveData
	{
		MoveName = "Anvil Drop",
		StartupFrames = 15, ActiveFrames = 3, EndlagFrames = 30,
		Damage = 17.0f, BaseKnockback = 30.0f, KnockbackGrowth = 0.85f,
		LaunchAngleDegrees = -80.0f,
		HitboxOffset = new Vector2(26.0f, 44.0f), HitboxRadius = 48.0f,
		Spikes = true,
		Special = SpecialKind.Drop,
		SpecialSpeed = 260.0f, SpecialGravity = 2600.0f, SpecialLifetime = 120,
		FxColor = new Color(0.38f, 0.42f, 0.54f), FxRadius = 44.0f,
	};
}
