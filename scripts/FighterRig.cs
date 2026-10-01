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
	readonly Sprite2D[] sprites = new Sprite2D[(int)RigBone.Count];
	readonly Vector2[] restOffsets = new Vector2[(int)RigBone.Count];

	/// <summary>
	/// A whole drawing shown as-is instead of the puppet, for a moment a kid drew as a pose -
	/// Circy looking out at the player before he turns into a bomb. Or an effect drawing, like
	/// his laser, that the game sizes to fit the move.
	/// </summary>
	public sealed class PoseArt
	{
		public Texture2D Texture;

		/// <summary>The point in the texture that stands on the fighter's feet (or its centre, for an effect).</summary>
		public Vector2 Anchor;

		/// <summary>How wide the body is in this drawing, so it can be matched to the puppet's.</summary>
		public float BallWidth;

		/// <summary>Size this pose the same as another one, keeping the size the kid drew it relative to that.</summary>
		public string ScaleLike;
	}

	readonly Dictionary<string, PoseArt> poses = new Dictionary<string, PoseArt>();

	/// <summary>
	/// Drawings hung on an existing bone and shown only at certain moments - Lug's hard hat,
	/// which goes on when he blocks. They add no bones, so the shared skeleton never changes.
	/// </summary>
	readonly Dictionary<string, Sprite2D> extras = new Dictionary<string, Sprite2D>();
	float ballWidth;

	float puppetScale = 1.0f;
	float squash = 1.0f;
	int facingSign = 1;
	readonly Pose exaggerated = new Pose();

	/// <summary>
	/// Every pose is pushed this much further from rest when it is shown. Authored poses read
	/// stiff on a cutout puppet - parts only rotate, nothing bends - so the whole library is
	/// played bigger than it is written. One knob for "more dramatic".
	/// </summary>
	public const float Drama = 1.25f;
	public const float HipDrama = 1.4f;

	/// <summary>A fighter's own multiplier on top of <see cref="Drama"/> - see FighterData.AnimationDrama.</summary>
	public float DramaScale = 1.0f;
	float bodyHalfHeight;
	float legStretch = 1.0f;

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
		ballWidth = root.ContainsKey("ballWidth") ? (float)root["ballWidth"] : 0.0f;
		LoadPoses(root, baseDir);

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
			// A far arm hangs from the torso bone, and children draw after their parent's own
			// sprite - which put it in FRONT of the body. Moving it ahead of that sprite puts
			// it behind, so it comes from the far side of the body the way a far arm should.
			// A drawing can say a far limb stays in front ("inFront" in the manifest) - DoomBot's
			// arm comes out of a hole in the FRONT of his chest.
			bool inFront = entry.ContainsKey("inFront") && (bool)entry["inFront"];
			if (RigBones.IsBackLimb(bone) && !inFront && parent != this && parent.GetChildCount() > 1
				&& parent.GetChild(0) is Sprite2D)
			{
				parent.MoveChild(node, 0);
			}
			bones[(int)bone] = node;
			present[(int)bone] = true;
			restOffsets[(int)bone] = node.Position;

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
			if (RigBones.IsBackLimb(bone) && !inFront)
			{
				sprite.Modulate = new Color(darken, darken, darken);
			}

			node.AddChild(sprite);
			sprites[(int)bone] = sprite;
		}

		LoadExtras(root, parts, baseDir);

		Loaded = true;
		Play(FighterAnimations.Idle);
		return true;
	}

	void LoadExtras(Godot.Collections.Dictionary root, Godot.Collections.Dictionary parts, string baseDir)
	{
		extras.Clear();
		if (!root.ContainsKey("extras")) return;

		foreach (Godot.Collections.Dictionary entry in (Godot.Collections.Array)root["extras"])
		{
			if (!RigBones.TryParse((string)entry["bone"], out RigBone bone) || !present[(int)bone]) continue;

			string partName = (string)entry["part"];
			if (!parts.ContainsKey(partName)) continue;
			var part = (Godot.Collections.Dictionary)parts[partName];
			var texture = GD.Load<Texture2D>(baseDir.PathJoin((string)part["texture"]));
			if (texture == null) continue;

			var pivot = (Godot.Collections.Array)part["pivot"];
			var offset = (Godot.Collections.Array)entry["offset"];
			var sprite = new Sprite2D
			{
				Texture = texture,
				Centered = false,
				Offset = new Vector2(-(float)pivot[0], -(float)pivot[1]),
				Position = new Vector2((float)offset[0], (float)offset[1]),
				// Most extras only appear for a moment (a hard hat); "always" ones are part of the
				// drawing that has to sit over another part, like the socket DoomBot's arm comes out of.
				Visible = entry.ContainsKey("always") && (bool)entry["always"],
			};
			bones[(int)bone].AddChild(sprite);
			extras[(string)entry["name"]] = sprite;
		}
	}

	/// <summary>Shows or hides an extra drawing by name. Unknown names are ignored.</summary>
	public void SetExtraVisible(string name, bool visible)
	{
		if (extras.TryGetValue(name, out Sprite2D sprite)) sprite.Visible = visible;
	}

	/// <summary>
	/// Mirrors an extra about its pivot. A turning frame shown for the far half of a turn is the
	/// near half's frame the other way round, so half the frames do the whole circle.
	/// </summary>
	public void SetExtraMirrored(string name, bool mirrored)
	{
		if (extras.TryGetValue(name, out Sprite2D sprite)) sprite.Scale = new Vector2(mirrored ? -1.0f : 1.0f, 1.0f);
	}

	public bool HasExtra(string name) => extras.ContainsKey(name);

	/// <summary>
	/// Hides one bone's own drawing and nothing else - the bone still moves, and whatever hangs
	/// from it still shows. How a spin swaps the body for a turning frame, and how a stretched
	/// arm replaces the drawn one while it is out.
	/// </summary>
	public void SetPartVisible(RigBone bone, bool visible)
	{
		Sprite2D sprite = sprites[(int)bone];
		if (sprite != null) sprite.Visible = visible;
	}

	Texture2D defaultProp;
	Vector2 defaultPropOffset;
	string shownProp = "";

	/// <summary>
	/// Puts one of the fighter's pose drawings in its hand instead of its usual prop - a weapon
	/// for one move. Pose art is stored tip-up and anchored at the grip, so it is turned half a
	/// circle to hang from the hand the way a prop part does. Empty or unknown puts the usual prop
	/// back. (An empty hand is the caller hiding the part - see SetPartVisible.)
	/// </summary>
	public void ShowProp(string poseName)
	{
		poseName ??= "";
		if (poseName == shownProp) return;
		Sprite2D sprite = sprites[(int)RigBone.PropFront];
		if (sprite == null) return;

		if (defaultProp == null)
		{
			defaultProp = sprite.Texture;
			defaultPropOffset = sprite.Offset;
		}
		shownProp = poseName;

		PoseArt art = PoseArtFor(poseName);
		if (art != null)
		{
			sprite.Texture = art.Texture;
			sprite.Offset = -art.Anchor;
			sprite.Rotation = Mathf.Pi;
		}
		else
		{
			sprite.Texture = defaultProp;
			sprite.Offset = defaultPropOffset;
			sprite.Rotation = 0.0f;
		}
	}

	/// <summary>
	/// Where the business end of whatever is in the hand is right now, in global coordinates:
	/// the head of the hammer, the point of the sword. The parade reports it so hitboxes can be
	/// put where the weapon actually is. Null if there is nothing in the hand.
	/// </summary>
	public Vector2? PropHeadGlobal()
	{
		Sprite2D sprite = sprites[(int)RigBone.PropFront];
		if (sprite == null || !sprite.Visible || sprite.Texture == null) return null;
		Vector2 size = sprite.Texture.GetSize();
		Vector2 grip = -sprite.Offset;
		// Held weapons are stored tip-up and turned; the default prop is stored tip-down.
		Vector2 head = Mathf.IsZeroApprox(sprite.Rotation)
			? new Vector2(grip.X, size.Y * 0.88f)
			: new Vector2(grip.X, size.Y * 0.14f);
		return sprite.GlobalTransform * (head - grip);
	}

	/// <summary>Where a part's joint is in its texture, so it can be drawn stretched outside the rig.</summary>
	public Vector2 PartPivot(RigBone bone)
	{
		Sprite2D sprite = sprites[(int)bone];
		return sprite != null ? -sprite.Offset : Vector2.Zero;
	}

	void LoadPoses(Godot.Collections.Dictionary root, string baseDir)
	{
		poses.Clear();
		if (!root.ContainsKey("poses")) return;

		foreach (var pair in (Godot.Collections.Dictionary)root["poses"])
		{
			var entry = (Godot.Collections.Dictionary)pair.Value;
			var texture = GD.Load<Texture2D>(baseDir.PathJoin((string)entry["texture"]));
			if (texture == null) continue;

			var anchor = (Godot.Collections.Array)entry["anchor"];
			poses[(string)pair.Key] = new PoseArt
			{
				Texture = texture,
				Anchor = new Vector2((float)anchor[0], (float)anchor[1]),
				BallWidth = entry.ContainsKey("ballWidth") ? (float)entry["ballWidth"] : 0.0f,
				ScaleLike = entry.ContainsKey("scaleLike") ? (string)entry["scaleLike"] : null,
			};
		}
	}

	public PoseArt PoseArtFor(string name) => name != null && poses.TryGetValue(name, out PoseArt art) ? art : null;

	/// <summary>
	/// World pixels per texture pixel for a body pose: its body drawn the same width as the
	/// puppet's, so swapping between puppet and pose never makes him pop bigger or smaller.
	/// </summary>
	public float PoseScale(string name)
	{
		PoseArt art = PoseArtFor(name);
		if (art == null) return 0.0f;
		if (art.ScaleLike != null && art.ScaleLike != name) return PoseScale(art.ScaleLike);
		if (art.BallWidth <= 0.0f || ballWidth <= 0.0f) return puppetScale;
		return puppetScale * ballWidth / art.BallWidth;
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
		puppetScale = scale;
		this.bodyHalfHeight = bodyHalfHeight;
		ApplyScale();
		PlaceHip();
	}

	/// <summary>World pixels per canonical unit, after <see cref="Normalise"/>.</summary>
	public float PuppetScale => puppetScale;

	/// <summary>How much taller the puppet is than as drawn, in world pixels, at this stretch.</summary>
	public float LegGrowth(float stretch) => LegLength * (stretch - 1.0f) * puppetScale;

	/// <summary>
	/// Lengthens or shortens both legs, for a fighter who stretches. The leg ART is scaled along
	/// its own length and nothing else - no part is redrawn - and the knees move with it, so the
	/// joints still line up. The hip rises by the same amount so the feet stay on the floor.
	/// </summary>
	public void SetLegStretch(float stretch, float newBodyHalfHeight)
	{
		if (!Loaded) return;
		legStretch = stretch;
		bodyHalfHeight = newBodyHalfHeight;

		foreach (RigBone bone in new[]
		{
			RigBone.LegBackUpper, RigBone.LegBackLower, RigBone.LegFrontUpper, RigBone.LegFrontLower,
		})
		{
			int i = (int)bone;
			if (sprites[i] != null) sprites[i].Scale = new Vector2(1.0f, stretch);
		}

		foreach (RigBone knee in new[] { RigBone.LegBackLower, RigBone.LegFrontLower })
		{
			int i = (int)knee;
			if (present[i]) bones[i].Position = restOffsets[i] * stretch;
		}

		PlaceHip();
	}

	float roll;

	/// <summary>
	/// Turns the whole puppet about the middle of the fighter's body, for a roll or a flip. Zero
	/// is upright. Turning about the hip instead would swing the body round the feet.
	/// </summary>
	public void SetRoll(float radians)
	{
		if (Mathf.IsEqualApprox(radians, roll)) return;
		roll = radians;
		PlaceHip();
	}

	void PlaceHip()
	{
		var hip = new Vector2(0.0f, bodyHalfHeight - LegLength * legStretch * puppetScale * squash);
		Position = hip.Rotated(roll);
		Rotation = roll;
	}

	void ApplyScale()
	{
		// Squash keeps the area: taller is thinner, shorter is wider.
		float width = puppetScale / Mathf.Sqrt(Mathf.Max(0.2f, squash));
		Scale = new Vector2(width * facingSign, puppetScale * squash);
	}

	/// <summary>
	/// Squash and stretch the whole puppet: above 1 is taller and thinner (a jump, a strike),
	/// below 1 shorter and wider (a landing, a windup). The feet stay on the floor. This is the
	/// oldest trick in animation for making a stiff drawing feel alive.
	/// </summary>
	public void SetSquash(float amount)
	{
		if (Mathf.IsEqualApprox(amount, squash)) return;
		squash = amount;
		ApplyScale();
		PlaceHip();
	}

	/// <summary>
	/// The drawn texture on a bone, for a fighter drawn as one piece while its limbs are hidden -
	/// Circy rolling, or curled into a bomb.
	/// </summary>
	public Texture2D PartTexture(RigBone bone) => sprites[(int)bone]?.Texture;

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
		Pose.Exaggerate(pose, Drama, HipDrama * DramaScale, exaggerated, DramaScale);
		Pose.Blend(current, exaggerated, blend, current);

		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			if (!present[i]) continue;
			var bone = (RigBone)i;

			if (bone == RigBone.Hip)
			{
				bones[i].Position = current.HipOffset;
				continue;
			}

			// The torso and head point UP from their joints, not down, so the same rotation that
			// swings a hanging limb backward tips them FORWARD. Every pose in the library is
			// written with one convention for every bone - negative leans toward the facing -
			// so for these two the sign is flipped here, once, rather than in every pose.
			float degrees = current[bone];
			if (bone == RigBone.Torso || bone == RigBone.Head) degrees = -degrees;
			bones[i].Rotation = Mathf.DegToRad(degrees);
		}
	}

	/// <summary>Flips the whole puppet. A profile drawing turning around is just a mirror.</summary>
	public void SetFacing(int facing)
	{
		int sign = facing >= 0 ? 1 : -1;
		if (sign == facingSign) return;
		facingSign = sign;
		ApplyScale();
	}
}
