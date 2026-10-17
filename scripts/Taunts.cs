using Godot;

/// <summary>
/// Taunts: a pose held for a moment, with a line in a speech bubble. They do nothing - no
/// hitbox, no effect on the fight - except show off, and they leave the fighter wide open, which
/// is the point: taunting is a risk you take because it is funny. Every fighter has three, each
/// with its own line (Eric, 2026-10-16): taunt with the stick up, sideways or down for one of
/// them, or with it still for any, never the same one twice running.
/// </summary>
public static class Taunts
{
	/// <summary>
	/// A taunt using one of the shared attack animations, held for about a second. Timed like a
	/// move so it runs through the same code: a quick wind-up, a long hold, a short recovery.
	/// </summary>
	public static MoveData Make(AttackAnim anim, string propArt = "", string extra = "") => new MoveData
	{
		MoveName = "Taunt",
		Anim = anim,
		StartupFrames = 8, ActiveFrames = 52, EndlagFrames = 12,
		Damage = 0.0f, BaseKnockback = 0.0f, KnockbackGrowth = 0.0f,
		HitboxRadius = 0.0f,
		PropArt = propArt,
		ShowExtra = extra,
	};

	/// <summary>A taunt that strikes one of the fighter's own posed drawings (picked at random from them).</summary>
	public static MoveData Posed(params string[] poses)
	{
		MoveData taunt = Make(AttackAnim.Spread);
		taunt.PoseArts = poses;
		return taunt;
	}

	/// <summary>
	/// A taunt struck as one of the shared story poses - a flex, a bow, a fist in the air - with
	/// empty hands: whatever he carries is put away for it.
	/// </summary>
	public static MoveData Pose(string pose, string extra = "")
	{
		MoveData taunt = Make(AttackAnim.Spread, propArt: "-", extra: extra);
		taunt.TauntPose = pose;
		return taunt;
	}
}
