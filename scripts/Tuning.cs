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
	// See .ai/fighting-design.md. Do not make it subtle.

	public const float HitlagBaseFrames = 3.0f;
	public const float HitlagFramesPerDamage = 0.5f;

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
	/// After a KO the fighter is gone for this long before they reappear, so the blast has the
	/// screen to itself and everyone has a beat to take in what happened.
	/// </summary>
	public const int RespawnDelayFrames = 80;
	public const int RespawnInvulnFrames = 120;
}
