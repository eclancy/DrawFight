using System;
using Godot;

/// <summary>
/// The knockback formula and everything derived from it. This is the single most important
/// piece of maths in the project - it is most of what makes the game feel like Smash rather
/// than like a physics demo. See .ai/fighting-design.md before changing any of it.
/// </summary>
public static class Knockback
{
	/// <summary>
	/// A simplified form of the Smash Ultimate knockback equation.
	///
	///   kb = ((p/10 + p*d/20) * (200/(w+100)) * 1.4 + 18) * s + b
	///
	/// The two knobs that give a move its character are <paramref name="baseKnockback"/> and
	/// <paramref name="knockbackGrowth"/>: high base + low growth is a reliable poke that never
	/// kills, low base + high growth is a finisher. High base AND high growth is a move that is
	/// always the correct choice - do not ship one.
	/// </summary>
	/// <param name="victimPercent">Victim percent AFTER this hit's damage has been applied.</param>
	/// <param name="damage">Damage dealt by the move, after any block reduction.</param>
	/// <param name="victimWeight">Victim weight; 100 is average, heavier flies less.</param>
	public static float Compute(
		float victimPercent,
		float damage,
		float victimWeight,
		float baseKnockback,
		float knockbackGrowth)
	{
		float weightFactor = 200.0f / (victimWeight + 100.0f);
		float percentTerm = victimPercent / 10.0f + (victimPercent * damage) / 20.0f;
		return (percentTerm * weightFactor * 1.4f + 18.0f) * knockbackGrowth + baseKnockback;
	}

	/// <summary>
	/// Hitstun is DERIVED from knockback, never authored per move. That is what makes combos
	/// emerge from the numbers instead of being scripted - a weak hit lets the victim recover
	/// first, a strong one lets the attacker follow up. Nobody designs the combos; this does.
	/// </summary>
	public static int HitstunFrames(float knockback)
	{
		return Mathf.Max(1, Mathf.RoundToInt(knockback * Tuning.HitstunFramesPerKnockback));
	}

	/// <summary>Frames both fighters freeze on contact. Scales with damage so big hits bite.</summary>
	public static int HitlagFrames(float damage)
	{
		return Mathf.Max(1, Mathf.RoundToInt(
			Tuning.HitlagBaseFrames + damage * Tuning.HitlagFramesPerDamage));
	}

	/// <summary>
	/// Launch velocity for a hit, including the victim's directional influence.
	///
	/// DI lets a victim angle their own trajectory slightly by holding a direction during
	/// hitstun. It is how a good player survives a hit that should have killed them, and it is
	/// worth having because it rewards him for learning something real rather than mashing.
	/// </summary>
	/// <param name="launchAngleDegrees">Move's launch angle, measured from +X, counter-clockwise.</param>
	/// <param name="facing">Attacker facing: +1 right, -1 left.</param>
	/// <param name="victimStick">Victim's held stick direction this frame, for DI.</param>
	public static Vector2 LaunchVelocity(
		float knockback,
		float launchAngleDegrees,
		int facing,
		Vector2 victimStick)
	{
		// Screen space has +Y down, so a "45 degrees upward" launch angle is -45 in radians here.
		float angle = -Mathf.DegToRad(launchAngleDegrees);
		Vector2 dir = new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));

		if (victimStick.LengthSquared() > 0.04f)
		{
			// DI rotates the launch vector toward perpendicular-to-stick, capped. The cross
			// product's sign picks which way the stick is pushing relative to the trajectory.
			Vector2 stick = victimStick.Normalized();
			float cross = dir.X * stick.Y - dir.Y * stick.X;
			float influence = Mathf.Clamp(cross, -1.0f, 1.0f);
			dir = dir.Rotated(Mathf.DegToRad(Tuning.DiMaxAngleDegrees) * influence);
		}

		return dir.Normalized() * knockback * Tuning.LaunchSpeedPerKnockback;
	}
}
