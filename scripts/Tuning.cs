using Godot;

/// <summary>
/// Global feel constants shared by every fighter. Per-fighter numbers live on
/// <see cref="FighterData"/>; per-move numbers live on <see cref="MoveData"/>. What is here is
/// the stuff that must be identical for everyone or the game stops being legible.
///
/// M1 exists to tune these by feel. Expect every number in this file to change.
/// </summary>
public static class Tuning
{
	// --- Knockback -> motion -------------------------------------------------

	/// <summary>Pixels/second of launch speed per unit of knockback from the formula.</summary>
	public const float LaunchSpeedPerKnockback = 14.0f;

	/// <summary>Deceleration applied to launch velocity, px/s^2. Lower = floatier launches.</summary>
	public const float LaunchDecay = 1900.0f;

	/// <summary>Hitstun frames per unit of knockback. This is what makes combos exist.</summary>
	public const float HitstunFramesPerKnockback = 0.25f;

	// --- Impact feel ---------------------------------------------------------
	// Hitlag is the single biggest contributor to a hit feeling like it has weight.
	// See .ai/fighting-design.md. It was 3 + damage * 0.5 and that froze the fight too long:
	// throwing out an attack and moving on is what makes a match flow. A jab now freezes for 3
	// frames rather than 5, a heavy smash for about 8 rather than 13 - still felt, never a stall.

	public const float HitlagBaseFrames = 2.0f;
	public const float HitlagFramesPerDamage = 0.3f;

	public const float ScreenShakePerKnockback = 0.11f;
	public const float ScreenShakeMax = 26.0f;
	public const float ScreenShakeDecayPerSecond = 52.0f;

	// --- Defence -------------------------------------------------------------
	// Blocking reduces, it never negates. The cost of blocking is paid in chip damage
	// raising your percent. There is deliberately no shield health here.

	public const float BlockDamageMultiplier = 0.30f;
	public const float BlockKnockbackMultiplier = 0.20f;
	public const int BlockReleaseLagFrames = 5;

	// --- Directional influence ----------------------------------------------

	/// <summary>Max degrees a victim can angle their own launch by holding a direction.</summary>
	public const float DiMaxAngleDegrees = 18.0f;

	// --- Input forgiveness ---------------------------------------------------
	// All three are non-negotiable per .ai/fighting-design.md.

	public const int CoyoteFrames = 5;
	public const int JumpBufferFrames = 6;
	public const int AttackBufferFrames = 6;

	// --- Match rules ---------------------------------------------------------

	public const int DefaultStocks = 3;
	public const int RespawnFreezeFrames = 30;

	/// <summary>
	/// Falling is gentler than rising: every fighter's top fall speed is scaled by this, and
	/// gravity starts at FallStartGravity of full strength when a fall begins and builds to full
	/// over FallRampFrames. The top of a jump hangs, then speeds up - so falling off something
	/// never feels like being yanked down.
	/// </summary>
	public const float FallSpeedScale = 0.8f;
	public const float FallStartGravity = 0.45f;
	public const int FallRampFrames = 26;

	/// <summary>
	/// Every fighter's gravity is scaled by this, so everyone hangs in the air longer: a fighter
	/// knocked off the stage falls more slowly and has more time to get back. Jumps are launched
	/// at <see cref="JumpScale"/> of their speed to match, so every jump still reaches exactly the
	/// height it did - height is v^2 / 2g - and only the time in the air changes.
	/// </summary>
	public const float GravityScale = 0.8f;

	/// <summary>
	/// The square root of <see cref="GravityScale"/>: what keeps jump heights where they were. A
	/// launch is slowed by it too, and its slowdown (LaunchDecay) by GravityScale, so a launched
	/// fighter flies exactly the path they used to - the same KO percents - only about 12% more
	/// slowly, which is more time to steer it and get back.
	/// </summary>
	public static readonly float JumpScale = Mathf.Sqrt(GravityScale);

	/// <summary>
	/// After a KO the fighter is gone for this long before they reappear, so the blast has the
	/// screen to itself and everyone has a beat to take in what happened.
	/// </summary>
	public const int RespawnDelayFrames = 80;
	public const int RespawnInvulnFrames = 120;
}
