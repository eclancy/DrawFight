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

	public static void Blend(Pose a, Pose b, float t, Pose into)
	{
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			into.rotations[i] = Mathf.Lerp(a.rotations[i], b.rotations[i], t);
		}
		into.HipOffset = a.HipOffset.Lerp(b.HipOffset, t);
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
			// Smoothstep between keys, so a four-pose walk cycle does not look like it is
			// snapping between four drawings.
			Pose.Blend(keys[i].pose, keys[i + 1].pose, local * local * (3.0f - 2.0f * local), into);
			return;
		}

		into.CopyFrom(keys[keys.Count - 1].pose);
	}
}
