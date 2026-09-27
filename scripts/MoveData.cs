using Godot;

/// <summary>
/// One attack, as a .tres Resource. New content is authored as resources, not as new classes -
/// the same pattern SpellData uses in the sibling project.
///
/// EVERY DURATION HERE IS AN INTEGER FRAME COUNT at a fixed 60 Hz, never a float in seconds.
/// See .ai/fighting-design.md; retrofitting that rule later means rewriting every move.
/// </summary>
[GlobalClass]
public partial class MoveData : Resource
{
	[Export] public string MoveName { get; set; } = "Attack";

	// --- Timing, in frames ---------------------------------------------------

	/// <summary>Wind-up before the hitbox exists. This is the move's "speed".</summary>
	[Export] public int StartupFrames { get; set; } = 5;

	/// <summary>How long the hitbox is live.</summary>
	[Export] public int ActiveFrames { get; set; } = 3;

	/// <summary>Recovery after the hitbox vanishes. This is the move's risk.</summary>
	[Export] public int EndlagFrames { get; set; } = 14;

	public int TotalFrames => StartupFrames + ActiveFrames + EndlagFrames;

	// --- Damage and knockback ------------------------------------------------

	[Export] public float Damage { get; set; } = 8.0f;

	/// <summary>How hard it hits at 0%. High base = a reliable poke that pushes people away.</summary>
	[Export] public float BaseKnockback { get; set; } = 32.0f;

	/// <summary>How hard it scales with percent. High growth = a finisher.</summary>
	[Export] public float KnockbackGrowth { get; set; } = 1.0f;

	/// <summary>Degrees from +X, counter-clockwise. 45 is a standard diagonal launch.</summary>
	[Export] public float LaunchAngleDegrees { get; set; } = 45.0f;

	// --- Hitbox --------------------------------------------------------------
	// M1 supports one circular hitbox per move, positioned relative to the fighter and
	// mirrored by facing. M4 generalises this to a list of shapes with per-frame windows.

	/// <summary>Offset from the fighter's centre. +X is forward; mirrored automatically.</summary>
	[Export] public Vector2 HitboxOffset { get; set; } = new Vector2(62.0f, -20.0f);

	[Export] public float HitboxRadius { get; set; } = 44.0f;

	// --- Flags ---------------------------------------------------------------

	/// <summary>Ignores block reduction entirely. Use sparingly - this is a one-character thing.</summary>
	[Export] public bool Unblockable { get; set; } = false;

	/// <summary>Launches downward with no recovery window. Reserved for down-aerials at M4.</summary>
	[Export] public bool Spikes { get; set; } = false;

	/// <summary>
	/// A fast, weak poke. High-ish base and low growth means it repositions people reliably
	/// and never kills - the safe default shape for a jab.
	/// </summary>
	public static MoveData PlaceholderJab()
	{
		return new MoveData
		{
			MoveName = "Jab",
			StartupFrames = 4,
			ActiveFrames = 3,
			EndlagFrames = 11,
			Damage = 7.0f,
			BaseKnockback = 30.0f,
			KnockbackGrowth = 0.85f,
			LaunchAngleDegrees = 42.0f,
			HitboxOffset = new Vector2(58.0f, -18.0f),
			HitboxRadius = 42.0f,
		};
	}

	/// <summary>
	/// Slow, committal, and scales hard with percent - the shape of a finisher. Deliberately
	/// paired with the heavy placeholder fighter so M1 can be judged on whether a slow strong
	/// move and a fast weak one actually feel different.
	/// </summary>
	public static MoveData PlaceholderHeavySwing()
	{
		return new MoveData
		{
			MoveName = "Swing",
			StartupFrames = 11,
			ActiveFrames = 4,
			EndlagFrames = 24,
			Damage = 15.0f,
			BaseKnockback = 22.0f,
			KnockbackGrowth = 1.0f,
			LaunchAngleDegrees = 48.0f,
			HitboxOffset = new Vector2(76.0f, -24.0f),
			HitboxRadius = 58.0f,
		};
	}
}
