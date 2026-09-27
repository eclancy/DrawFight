using System.Collections.Generic;
using Godot;

/// <summary>
/// A cutout puppet: the sliced parts of one drawing, hung on the shared skeleton and posed by
/// the shared animation library. Built from a rig.json manifest - see .ai/art-pipeline.md for
/// the canonical orientation contract that manifest and this class both depend on.
///
/// The rig knows nothing about fighting. It is handed a pose and it draws it.
/// </summary>
public partial class FighterRig : Node2D
{
	readonly Node2D[] bones = new Node2D[(int)RigBone.Count];
	readonly bool[] present = new bool[(int)RigBone.Count];

	readonly Pose current = new Pose();
	readonly Pose target = new Pose();

	AnimationClip clip;
	float clipFrame;

	/// <summary>Canonical units from hip to crown, straight out of the manifest.</summary>
	public float CanonicalHeight { get; private set; } = 1.0f;

	/// <summary>Canonical units from hip to foot. Decides where the hip sits above the feet.</summary>
	public float LegLength { get; private set; }

	public bool Loaded { get; private set; }

	/// <summary>
	/// Builds the puppet. Returns false rather than throwing if the manifest is missing, so a
	/// fighter with no art still plays as a rectangle instead of taking the match down.
	/// </summary>
	public bool Load(string rigPath)
	{
		if (!FileAccess.FileExists(rigPath))
		{
			GD.PushWarning($"FighterRig: no manifest at {rigPath}");
			return false;
		}

		string text = FileAccess.GetFileAsString(rigPath);
		var json = new Json();
		if (json.Parse(text) != Error.Ok)
		{
			GD.PushError($"FighterRig: {rigPath} is not valid JSON - {json.GetErrorMessage()}");
			return false;
		}

		var root = (Godot.Collections.Dictionary)json.Data;
		string baseDir = rigPath.GetBaseDir();

		CanonicalHeight = (float)root["canonicalHeight"];
		LegLength = (float)root["legLength"];
		float darken = root.ContainsKey("backLimbDarken") ? (float)root["backLimbDarken"] : 0.7f;

		var parts = (Godot.Collections.Dictionary)root["parts"];

		// Bones are walked in manifest order, which is also back-to-front draw order: Godot
		// renders a node and then its children in order, so appending in this sequence puts the
		// back limbs behind the torso and the front limbs in front of it with no z-index games.
		foreach (Godot.Collections.Dictionary entry in (Godot.Collections.Array)root["bones"])
		{
			string boneName = (string)entry["name"];
			if (!RigBones.TryParse(boneName, out RigBone bone))
			{
				GD.PushWarning($"FighterRig: manifest names an unknown bone '{boneName}' - ignored");
				continue;
			}

			var offsetArray = (Godot.Collections.Array)entry["offset"];
			var node = new Node2D
			{
				Name = boneName,
				Position = new Vector2((float)offsetArray[0], (float)offsetArray[1]),
			};

			Variant parentValue = entry["parent"];
			Node2D parent = this;
			if (parentValue.VariantType == Variant.Type.String
				&& RigBones.TryParse(parentValue.AsString(), out RigBone parentBone)
				&& bones[(int)parentBone] != null)
			{
				parent = bones[(int)parentBone];
			}

			parent.AddChild(node);
			bones[(int)bone] = node;
			present[(int)bone] = true;

			Variant partValue = entry["part"];
			if (partValue.VariantType != Variant.Type.String) continue;

			string partName = partValue.AsString();
			if (!parts.ContainsKey(partName)) continue;

			var part = (Godot.Collections.Dictionary)parts[partName];
			var texture = GD.Load<Texture2D>(baseDir.PathJoin((string)part["texture"]));
			if (texture == null)
			{
				GD.PushWarning($"FighterRig: missing texture for part '{partName}'");
				continue;
			}

			var pivotArray = (Godot.Collections.Array)part["pivot"];
			var sprite = new Sprite2D
			{
				Texture = texture,
				Centered = false,
				// Putting the joint at the bone's origin is the whole trick: rotating the bone
				// then swings the part about its joint rather than about its corner.
				Offset = new Vector2(-(float)pivotArray[0], -(float)pivotArray[1]),
			};

			// Darkening the far limbs is how flat cutout animation has always faked depth. It
			// costs nothing and it is why a drawing with only one arm still reads correctly.
			if (RigBones.IsBackLimb(bone))
			{
				sprite.Modulate = new Color(darken, darken, darken);
			}

			node.AddChild(sprite);
		}

		Loaded = true;
		Play(FighterAnimations.Idle);
		return true;
	}

	/// <summary>
	/// Scales the puppet so that crown-to-feet matches <paramref name="targetHeight"/>, and
	/// places the hip so the feet land on the bottom of the fighter's body box.
	///
	/// Without this, how big a fighter looks is decided by how close the camera was held when
	/// the drawing was photographed, which is both arbitrary and unfixable later.
	/// </summary>
	public void Normalise(float targetHeight, float bodyHalfHeight, float visualScale)
	{
		float total = CanonicalHeight + LegLength;
		if (total <= 0.0f) return;

		float scale = targetHeight / total * visualScale;
		Scale = new Vector2(scale, scale);
		Position = new Vector2(0.0f, bodyHalfHeight - LegLength * scale);
	}

	public void Play(AnimationClip next, bool restart = false)
	{
		if (clip == next && !restart) return;
		clip = next;
		clipFrame = 0.0f;
	}

	/// <summary>Advances the current clip. <paramref name="rate"/> scales playback, for speed-matched runs.</summary>
	public void Advance(float rate = 1.0f)
	{
		if (!Loaded || clip == null) return;
		clipFrame += rate;
		clip.Sample(clipFrame, target);
		ApplyPose(target, 0.45f);
	}

	/// <summary>
	/// Poses the rig at one explicit frame of a clip. Only the parade uses this - a one-shot
	/// clip left to run ends on its resting pose, which is exactly the frame that shows nothing.
	/// </summary>
	public void PoseAt(AnimationClip which, float frame, float blend = 0.5f)
	{
		if (!Loaded || which == null) return;
		clip = null;
		which.Sample(frame, target);
		ApplyPose(target, blend);
	}

	/// <summary>Poses the rig directly, bypassing clips. Used by attacks, which follow move frames.</summary>
	public void ApplyDirect(Pose pose, float blend = 0.6f)
	{
		if (!Loaded) return;
		clip = null;
		ApplyPose(pose, blend);
	}

	void ApplyPose(Pose pose, float blend)
	{
		// Blending toward the target rather than snapping to it smooths the joins between
		// states, so a fighter landing out of a launch does not pop from tumbling to standing.
		Pose.Blend(current, pose, blend, current);

		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			if (!present[i]) continue;
			var bone = (RigBone)i;

			if (bone == RigBone.Hip)
			{
				bones[i].Position = current.HipOffset;
				continue;
			}

			bones[i].Rotation = Mathf.DegToRad(current[bone]);
		}
	}

	/// <summary>Flips the whole puppet. A profile drawing turning around is just a mirror.</summary>
	public void SetFacing(int facing)
	{
		Vector2 s = Scale;
		Scale = new Vector2(Mathf.Abs(s.X) * (facing >= 0 ? 1.0f : -1.0f), s.Y);
	}
}
