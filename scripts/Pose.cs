using System.Collections.Generic;
using Godot;

/// <summary>
/// The bones of the shared skeleton, in the order they are drawn: back limbs, then the torso
/// and everything hanging off it, then the front limbs.
///
/// This enum IS the shared skeleton. Every fighter has exactly these bones, which is what lets
/// one animation library drive all of them. Do not add per-character bones - see the
/// non-humanoid escape hatch in .ai/art-pipeline.md.
/// </summary>
public enum RigBone
{
	Hip,
	LegBackUpper,
	LegBackLower,
	LegFrontUpper,
	LegFrontLower,
	Torso,
	ArmBackUpper,
	ArmBackLower,
	Head,
	ArmFrontUpper,
	ArmFrontLower,
	PropFront,
	Count,
}

public static class RigBones
{
	/// <summary>Maps a manifest bone name ("ArmFront_Upper") onto the enum. Case and underscores ignored.</summary>
	public static bool TryParse(string name, out RigBone bone)
	{
		string key = name.Replace("_", "").ToLowerInvariant();
		foreach (RigBone candidate in System.Enum.GetValues(typeof(RigBone)))
		{
			if (candidate == RigBone.Count) continue;
			if (candidate.ToString().ToLowerInvariant() == key)
			{
				bone = candidate;
				return true;
			}
		}
		bone = RigBone.Count;
		return false;
	}

	public static bool IsBackLimb(RigBone bone)
	{
		return bone == RigBone.ArmBackUpper || bone == RigBone.ArmBackLower
			|| bone == RigBone.LegBackUpper || bone == RigBone.LegBackLower;
	}
}

/// <summary>
/// One frame of the skeleton: a rotation per bone, in degrees, plus a hip offset in canonical
/// units for bobbing and crouching.
///
/// A rotation of zero is the canonical rest orientation the art is authored in - limbs hanging
/// straight down, torso up, head up. POSITIVE ROTATION SWINGS A LIMB BACKWARD (away from the
/// direction the fighter faces), negative swings it forward. Godot's Y axis points down, so
/// (0,1) rotated by +t is (-sin t, cos t), which is leftward - and fighters face right.
/// </summary>
public sealed class Pose
{
	readonly float[] rotations = new float[(int)RigBone.Count];

	public Vector2 HipOffset;

	public Pose() { }

	public Pose(Vector2 hipOffset, params (RigBone bone, float degrees)[] values)
	{
		HipOffset = hipOffset;
		foreach ((RigBone bone, float degrees) in values)
		{
			rotations[(int)bone] = degrees;
		}
	}

	public float this[RigBone bone] => rotations[(int)bone];

	/// <summary>Sets one joint - for a pose worked out each frame rather than authored, like a windmill.</summary>
	public void Set(RigBone bone, float degrees) => rotations[(int)bone] = degrees;

	/// <summary>
	/// A smooth curve through four poses, at <paramref name="t"/> (0 to 1) between
	/// <paramref name="b"/> and <paramref name="c"/>: Catmull-Rom, so a joint passes through each
	/// key at the speed its neighbours give it rather than stopping there.
	/// </summary>
	public static void Curve(Pose a, Pose b, Pose c, Pose d, float t, Pose into)
	{
		float t2 = t * t, t3 = t2 * t;
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			into.rotations[i] = CatmullRom(a.rotations[i], b.rotations[i], c.rotations[i], d.rotations[i], t, t2, t3);
		}
		into.HipOffset = new Vector2(
			CatmullRom(a.HipOffset.X, b.HipOffset.X, c.HipOffset.X, d.HipOffset.X, t, t2, t3),
			CatmullRom(a.HipOffset.Y, b.HipOffset.Y, c.HipOffset.Y, d.HipOffset.Y, t, t2, t3));
	}

	static float CatmullRom(float p0, float p1, float p2, float p3, float t, float t2, float t3) =>
		0.5f * (2.0f * p1 + (p2 - p0) * t + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 + (3.0f * p1 - p0 - 3.0f * p2 + p3) * t3);

	/// <summary>
	/// <paramref name="to"/>, carried on past itself the way it came from <paramref name="from"/> by
	/// <paramref name="amount"/> of the swing - but no joint more than <paramref name="maxDegrees"/>,
	/// and the spine and neck a third of that. A coil or a follow-through is a little more of the
	/// same movement; scaled by the size of the swing, an arm swung half a turn overhead was carried
	/// on another fifty degrees, round behind the back (Eric, 2026-10-10).
	/// </summary>
	public static void Overshoot(Pose from, Pose to, float amount, float maxDegrees, Pose into)
	{
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			var bone = (RigBone)i;
			float limit = bone == RigBone.Torso || bone == RigBone.Head ? maxDegrees / 3.0f : maxDegrees;
			float extra = Mathf.Clamp((to.rotations[i] - from.rotations[i]) * amount, -limit, limit);
			into.rotations[i] = to.rotations[i] + extra;
		}
		into.HipOffset = to.HipOffset + (to.HipOffset - from.HipOffset) * amount;
	}

	public static void Blend(Pose a, Pose b, float t, Pose into)
	{
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			into.rotations[i] = Mathf.Lerp(a.rotations[i], b.rotations[i], t);
		}
		into.HipOffset = a.HipOffset.Lerp(b.HipOffset, t);
	}

	/// <summary>
	/// Pushes every joint further from rest by <paramref name="amount"/>, and the hip further by
	/// <paramref name="hipAmount"/>. Joints are clamped just past straight up, so an arm already
	/// raised overhead does not swing on round behind the back.
	/// </summary>
	/// <param name="fighterDrama">
	/// A fighter's own extra drama (FighterData.AnimationDrama). It goes almost all into the legs:
	/// a wider stance and a higher knee read as energy, while an arm pushed further than it was
	/// posed stops pointing where the attack goes - a forward thrust tipped up into the air.
	/// </param>
	public static void Exaggerate(Pose source, float amount, float hipAmount, Pose into, float fighterDrama = 1.0f)
	{
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			// The spine and neck get a fraction of it: a limb swung further reads as energy, a
			// whole body tipped further reads as falling over.
			var bone = (RigBone)i;
			bool core = bone == RigBone.Torso || bone == RigBone.Head;
			bool leg = bone == RigBone.LegFrontUpper || bone == RigBone.LegFrontLower
				|| bone == RigBone.LegBackUpper || bone == RigBone.LegBackLower;
			float extra = leg ? fighterDrama : 1.0f + (fighterDrama - 1.0f) * 0.15f;
			float k = core ? 1.0f + (amount - 1.0f) * 0.4f : amount * extra;
			into.rotations[i] = Mathf.Clamp(source.rotations[i] * k, -185.0f, 185.0f);
		}
		into.HipOffset = source.HipOffset * hipAmount;
	}

	public void CopyFrom(Pose other)
	{
		System.Array.Copy(other.rotations, rotations, rotations.Length);
		HipOffset = other.HipOffset;
	}
}

/// <summary>
/// A sequence of poses on a frame timeline. Durations are in frames at the fixed 60 Hz tick,
/// like every other duration in the game.
/// </summary>
public sealed class AnimationClip
{
	public readonly string Name;
	public readonly bool Loops;
	readonly List<(int frame, Pose pose)> keys = new List<(int, Pose)>();

	public AnimationClip(string name, bool loops, params (int frame, Pose pose)[] keyframes)
	{
		Name = name;
		Loops = loops;
		keys.AddRange(keyframes);
	}

	public int LengthFrames => keys.Count == 0 ? 1 : keys[keys.Count - 1].frame;

	/// <summary>The key before or after the ones being blended, for the curve's tangents.</summary>
	Pose Neighbour(int index)
	{
		int last = keys.Count - 1;
		if (index >= 0 && index <= last) return keys[index].pose;
		if (!Loops) return keys[Mathf.Clamp(index, 0, last)].pose;
		// Looping, the last key is the first again, so before the first comes the one before the
		// last, and after the last comes the second.
		return index < 0 ? keys[Mathf.Max(0, last + index)].pose : keys[Mathf.Min(last, index - last)].pose;
	}

	public void Sample(float frame, Pose into)
	{
		if (keys.Count == 0) return;
		if (keys.Count == 1)
		{
			into.CopyFrom(keys[0].pose);
			return;
		}

		float t = Loops ? Mathf.PosMod(frame, LengthFrames) : Mathf.Clamp(frame, 0, LengthFrames);

		for (int i = 0; i < keys.Count - 1; i++)
		{
			if (t > keys[i + 1].frame) continue;

			float span = keys[i + 1].frame - keys[i].frame;
			float local = span <= 0.0f ? 0.0f : (t - keys[i].frame) / span;
			// A smooth curve through the keys, so a joint carries on through each one. Easing in
			// and out of every key, as this did, stopped the whole body dead four times a stride:
			// a walk that snapped between four drawings (Eric, 2026-10-10: it looked puppeteered).
			// A loop wraps round for its neighbours (its last key repeats its first); a one-shot
			// clip holds its ends, so it still eases out of its first pose and into its last.
			Pose.Curve(Neighbour(i - 1), keys[i].pose, keys[i + 1].pose, Neighbour(i + 2), local, into);
			return;
		}

		into.CopyFrom(keys[keys.Count - 1].pose);
	}
}
