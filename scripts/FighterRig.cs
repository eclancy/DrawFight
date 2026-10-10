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

		/// <summary>
		/// How tall the whole figure stands in this drawing, so it can be matched to the puppet's
		/// height - for a drawing of the fighter at a different angle, where there is no single
		/// body part to match the width of. Triguy's front-on taunts.
		/// </summary>
		public float FigureHeight;
	}

	readonly Dictionary<string, PoseArt> poses = new Dictionary<string, PoseArt>();

	/// <summary>
	/// Drawings hung on an existing bone, some shown only at certain moments - the glint on Lug's
	/// hard hat while it is armour. They add no bones, so the shared skeleton never changes.
	/// </summary>
	readonly Dictionary<string, Sprite2D> extras = new Dictionary<string, Sprite2D>();
	float ballWidth;

	/// <summary>How tall the figure stands in the main drawing, crown to feet (rig.json "figureHeight").</summary>
	float figureHeight;

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

	/// <summary>
	/// The weapon in his hand rides on his shoulder over whatever pose is shown
	/// (FighterAnimations.CarryOnShoulder). Whoever drives the rig sets it each frame.
	/// </summary>
	public bool CarryOnShoulder;
	readonly Pose carried = new Pose();

	/// <summary>
	/// Standing about with a hand on his hip, over the idle (FighterAnimations.HandOnHip). Whoever
	/// drives the rig sets it each frame.
	/// </summary>
	public bool HandOnHip;
	float bodyHalfHeight;
	float legStretch = 1.0f;
	float frontLegReach = 1.0f;

	readonly Pose current = new Pose();
	readonly Pose target = new Pose();

	// --- Moving like a body ------------------------------------------------------------

	/// <summary>
	/// How heavy he is to move (FighterData.Weight): a heavy body is slower to get going and slower
	/// to stop, so a heavyweight's limbs swing on further and settle later, and a lightweight is
	/// snappier. 1 is a middleweight.
	/// </summary>
	public float Inertia = 1.0f;

	/// <summary>
	/// How fast each joint is turning, in degrees a frame, and the hip moving, for the springs
	/// that carry the puppet toward each pose (<see cref="Follow"/>).
	/// </summary>
	readonly float[] spin = new float[(int)RigBone.Count];
	Vector2 hipSpeed;

	/// <summary>
	/// How each joint follows its pose: (stiffness, damping) of a spring toward it. A body moves
	/// from the middle out - the hips and spine lead, the shoulder follows, the elbow after it and
	/// the hand last, each one carried a little past where it stops and settling back - and that
	/// lag down the chain is most of what separates a body from a puppet, where every joint turns
	/// at once and stops dead. Eric's call, 2026-10-10: movement looked puppeteered. Tuned at the
	/// clips' usual pace; a caller asking for quicker or slower scales the stiffness.
	/// </summary>
	static (float stiffness, float damping) SpringFor(RigBone bone)
	{
		switch (bone)
		{
			case RigBone.Torso: return (0.30f, 1.00f);
			case RigBone.Head: return (0.22f, 0.80f);
			case RigBone.ArmFrontUpper:
			case RigBone.ArmBackUpper: return (0.26f, 0.78f);
			case RigBone.ArmFrontLower:
			case RigBone.ArmBackLower: return (0.20f, 0.62f);
			case RigBone.PropFront: return (0.17f, 0.56f);
			case RigBone.LegFrontUpper:
			case RigBone.LegBackUpper: return (0.32f, 0.98f);
			case RigBone.LegFrontLower:
			case RigBone.LegBackLower: return (0.28f, 0.88f);
			default: return (0.30f, 1.00f);
		}
	}

	/// <summary>
	/// Carries <see cref="current"/> toward <paramref name="goal"/> on a spring per joint. A blend of
	/// 1 snaps there - a strike has to be fully out on the frame its hit lands, because hitlag
	/// freezes whatever is showing - and leaves a little of the swing in the joint, so what follows
	/// carries on through rather than stopping dead. Below 1, a blend is how hard the caller wants
	/// it chased: the clips' usual 0.45 is the springs as tuned, more is quicker.
	/// </summary>
	void Follow(Pose goal, float blend)
	{
		if (blend >= 0.999f)
		{
			for (int i = 0; i < (int)RigBone.Count; i++)
			{
				// A joint thrown right round in one frame (a windmill's arm wrapping past straight
				// down) is not a swing to carry on.
				float jump = goal[(RigBone)i] - current[(RigBone)i];
				spin[i] = Mathf.Abs(jump) > 180.0f ? 0.0f : Mathf.Clamp(jump * 0.25f, -10.0f, 10.0f);
			}
			hipSpeed = ((goal.HipOffset - current.HipOffset) * 0.25f).LimitLength(6.0f);
			current.CopyFrom(goal);
			return;
		}

		float pace = Mathf.Clamp(blend / 0.45f, 0.4f, 2.2f) / Inertia;
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			var bone = (RigBone)i;
			(float stiffness, float damping) = SpringFor(bone);
			float k = Mathf.Min(0.9f, stiffness * pace * pace);
			// Damping past 1 takes away more than the joint's whole speed each frame, so it swings
			// back the other way every frame: a jitter, and with a quick blend on a light fighter,
			// a joint flung further each frame until the body flips over (a dodge roll's curl).
			float c = Mathf.Min(1.0f, damping * pace);
			// The poses are written in continuous angles - an arm swung up over the head goes on
			// past straight up to -185 - so the spring follows the plain difference. Wrapped, an arm
			// raised over the head went the other way round, back behind his body.
			float off = goal[bone] - current[bone];
			spin[i] += k * off - c * spin[i];
			current.Set(bone, current[bone] + spin[i]);
		}
		float hipK = Mathf.Min(0.9f, 0.30f * pace * pace);
		float hipC = Mathf.Min(1.0f, 1.0f * pace);
		hipSpeed += hipK * (goal.HipOffset - current.HipOffset) - hipC * hipSpeed;
		current.HipOffset += hipSpeed;
	}

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
		limbDarken = darken;
		ballWidth = root.ContainsKey("ballWidth") ? (float)root["ballWidth"] : 0.0f;
		figureHeight = root.ContainsKey("figureHeight") ? (float)root["figureHeight"] : 0.0f;
		stanceSpread = root.ContainsKey("stance") ? (float)root["stance"] : 0.0f;
		legsOnBody = root.ContainsKey("legsOnBody") && (bool)root["legsOnBody"];
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
			// A drawing can say a far limb stays in front ("inFront" in the manifest), for a limb
			// drawn coming out of the front of the body. DoomBot's did, and read as a second front
			// arm in a fight, so think twice before using it.
			bool inFront = entry.ContainsKey("inFront") && (bool)entry["inFront"];
			// Any part can also be put under the drawing it hangs from ("behindParent"): a weapon
			// held in a fist, so the fingers close over the grip. Lug's tools (Eric, 2026-10-08).
			bool behindParent = entry.ContainsKey("behindParent") && (bool)entry["behindParent"];
			if (((RigBones.IsBackLimb(bone) && !inFront) || behindParent) && parent != this
				&& parent.GetChildCount() > 1 && parent.GetChild(0) is Sprite2D)
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
			// costs nothing and it is why a drawing with only one arm still reads correctly. A limb
			// can set its own ("darken": 1 is none) - Lug's far arm, which comes from behind his
			// chest and needs no shade to read as behind it (Eric, 2026-10-08).
			if (RigBones.IsBackLimb(bone) && !inFront)
			{
				float shade = entry.ContainsKey("darken") ? (float)entry["darken"] : darken;
				sprite.Modulate = new Color(shade, shade, shade);
			}

			node.AddChild(sprite);
			sprites[(int)bone] = sprite;
			handRows[(int)bone] = part.ContainsKey("hand") ? (float)part["hand"] : -1.0f;
			if (part.ContainsKey("soleSlope")) soleSlopes[(int)bone] = (float)part["soleSlope"];
			if (bone == RigBone.PropFront) defaultPropEmpty = part.ContainsKey("empty") && (bool)part["empty"];
		}

		LoadExtras(root, parts, baseDir);
		LoadPoints(root);

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
				// Most extras only appear for a moment (a hard hat's glint); "always" ones are part of the
				// drawing that has to sit over another part, like a socket an "inFront" limb comes out of.
				Visible = entry.ContainsKey("always") && (bool)entry["always"],
			};
			bones[(int)bone].AddChild(sprite);
			extras[(string)entry["name"]] = sprite;

			// A foot: a drawing hung on the end of a shin at the ankle, which the rig keeps level
			// on the floor (LevelFeet) and stands on (StandOnFeet). Far ones darken like their leg.
			if (entry.ContainsKey("foot") && (bool)entry["foot"])
			{
				feet[(int)bone] = sprite;
				if (RigBones.IsBackLimb(bone)) sprite.Modulate = new Color(limbDarken, limbDarken, limbDarken);
			}
		}
	}

	float limbDarken = 0.7f;

	/// <summary>The foot hung on each lower leg, if the drawing has its feet cut separately.</summary>
	readonly Sprite2D[] feet = new Sprite2D[(int)RigBone.Count];

	bool HasFeet => feet[(int)RigBone.LegBackLower] != null && feet[(int)RigBone.LegFrontLower] != null;

	/// <summary>
	/// Feet kept level on the ground, as an ankle would: each foot turned back by however much its
	/// shin is turned, as far as he is planted - so in the air a kick still points its boot. And
	/// only as far as an ankle bends: a shin swung further than <see cref="AnkleBend"/> takes its
	/// foot with it, so a leg stretched back in a lunge stands on its toe and a kick leads with its
	/// heel, instead of a level boot meeting a nearly flat shin side-on (Eric, 2026-10-09, on
	/// DoomBot's slab boots).
	/// </summary>
	void LevelFeet()
	{
		float limit = Mathf.DegToRad(AnkleBend);
		foreach ((RigBone upper, RigBone lower) in new[] { (RigBone.LegBackUpper, RigBone.LegBackLower), (RigBone.LegFrontUpper, RigBone.LegFrontLower) })
		{
			Sprite2D foot = feet[(int)lower];
			if (foot == null) continue;
			float level = -(bones[(int)upper].Rotation + bones[(int)lower].Rotation);
			foot.Rotation = Mathf.Clamp(level, -limit, limit) * plantBlend;
		}
	}

	/// <summary>How far, in degrees, a foot turns against its shin to stay flat on the floor.</summary>
	const float AnkleBend = 25.0f;

	/// <summary>
	/// For a forearm: the texture row where the hand begins (rig.json "hand"), or -1. Below it is
	/// hand, above it is arm - so an arm can be stretched without stretching the hand on its end.
	/// </summary>
	readonly float[] handRows = new float[(int)RigBone.Count];

	public float HandRow(RigBone bone) => handRows[(int)bone];

	/// <summary>
	/// The bottom of a leg's drawing right now, in global coordinates, and the way the leg points
	/// there - where a rocket jet comes out of a boot. Null if the drawing has no such leg.
	/// </summary>
	public (Vector2 sole, Vector2 down)? Sole(RigBone lowerLeg)
	{
		Sprite2D sprite = feet[(int)lowerLeg] ?? sprites[(int)lowerLeg];
		if (sprite == null || sprite.Texture == null) return null;
		Vector2 size = sprite.Texture.GetSize();
		Transform2D t = sprite.GlobalTransform;
		Vector2 sole = t * (new Vector2(-sprite.Offset.X, size.Y) + sprite.Offset);
		return (sole, t.BasisXform(Vector2.Down).Normalized());
	}

	// --- Planted feet ------------------------------------------------------------------

	bool planted;
	float plantBlend;

	/// <summary>
	/// Standing on something: after every pose the body is moved up or down so the lowest point
	/// of the legs - a sole, or a knee in a kneel - rests exactly on the floor. A pose then never
	/// floats a fighter off the ground or sinks him into it, and a bent-legged pose - a crouch -
	/// really is lower. Eased in and out over a few frames so taking off or landing never pops.
	/// </summary>
	public void SetPlanted(bool on) => planted = on;

	/// <summary>
	/// How far the hip has to move down (positive) for the lowest point of the legs to touch the
	/// floor, in canonical units. The floor is where straight legs put the feet - PlaceHip puts the
	/// rig so the hip is LegLength above it.
	/// </summary>
	float FloorGap()
	{
		float lowest = float.MinValue;
		foreach (RigBone lower in FloorParts) lowest = Mathf.Max(lowest, LowestInk(lower));
		foreach (Sprite2D foot in feet) if (foot != null) lowest = Mathf.Max(lowest, LowestInk(foot));
		return lowest == float.MinValue ? 0.0f : LegLength * legStretch - lowest;
	}

	/// <summary>
	/// The lowest drawn point of one part, in the rig's own space; float.MinValue if it is not
	/// showing. A hidden forearm - out on a tether, or swapped for a turning frame - is not here
	/// to touch anything.
	/// </summary>
	float LowestInk(RigBone bone) => LowestInk(sprites[(int)bone]);

	float LowestInk(Sprite2D sprite)
	{
		if (sprite == null || sprite.Texture == null || !sprite.Visible) return float.MinValue;
		Transform2D t = InRig(sprite);
		float lowest = float.MinValue;
		foreach (Vector2 point in InkEdge(sprite.Texture))
		{
			lowest = Mathf.Max(lowest, (t * (point + sprite.Offset)).Y);
		}
		return lowest;
	}

	// --- Standing on flat feet -----------------------------------------------------------

	bool standing;
	float standBlend;

	/// <summary>
	/// Standing still - idle, blocking, a stance. Each foot is put flat on the floor and kept
	/// where it is, and the knees bend to make that so, however the body above moves; the pose
	/// only says how low the hips are. See <see cref="StandOnFeet"/>.
	/// </summary>
	public void SetStanding(bool on) => standing = on;

	/// <summary>
	/// No fighter has an ankle - a foot is drawn on the end of its shin - so a foot is flat at
	/// exactly one angle of its shin, and that angle is read off the drawing (<see cref="SoleOf"/>).
	/// So, standing: each shin is turned until its sole is level, and the thigh is turned until
	/// that sole sits on the floor - the knee bending forward as far as the hips are low. Legs of
	/// two lengths both reach the floor, and lowering the hips bends the knees instead of sinking
	/// the feet. Then the body is slid so the feet, between them, stay exactly where standing up
	/// straight puts them: the body moves over the feet, never the feet under the body. Eric's
	/// call, 2026-10-04, after DoomBot stood on the toe of a tilted boot.
	///
	/// The hips' height comes from the pose, read here as a percentage of leg length, so one
	/// standing pose bobs every fighter alike.
	///
	/// One exception. A leg drawn much longer than the other with no foot on the end - EdgeLord's
	/// back leg, which ends in a point - would have to fold right up at the knee to match; with
	/// no foot to keep level it is angled back, straight, instead.
	/// </summary>
	void StandOnFeet(float blend)
	{
		int hip = (int)RigBone.Hip;
		if (!present[hip]) return;
		Vector2 at = bones[hip].Position;
		at.Y = current.HipOffset.Y * 0.01f * LegLength * legStretch;

		if (HasFeet)
		{
			StandOnAnkles(at, blend);
			return;
		}

		// The floor, for now, is as far down as the shorter leg reaches standing straight with its
		// foot level: that leg straight, the longer one's knee bent to match. Planting then moves
		// the whole of him onto the real floor.
		float? backReach = Reach(RigBone.LegBackUpper, RigBone.LegBackLower);
		float? frontReach = Reach(RigBone.LegFrontUpper, RigBone.LegFrontLower);
		if (backReach == null || frontReach == null) return;
		float floor = Mathf.Min(backReach.Value, frontReach.Value);
		float spare = LegLength * legStretch * 0.03f;
		bool backLong = backReach.Value > floor + spare;
		bool frontLong = frontReach.Value > floor + spare;

		var back = SolveLeg(RigBone.LegBackUpper, RigBone.LegBackLower, at, floor, -1, backLong);
		var front = SolveLeg(RigBone.LegFrontUpper, RigBone.LegFrontLower, at, floor, 1, frontLong);
		if (back == null || front == null) return;

		// Where the feet are, between them, standing straight: the body is slid to keep them there.
		var backRest = SolveLeg(RigBone.LegBackUpper, RigBone.LegBackLower, Vector2.Zero, floor, -1, backLong);
		var frontRest = SolveLeg(RigBone.LegFrontUpper, RigBone.LegFrontLower, Vector2.Zero, floor, 1, frontLong);
		float slide = (backRest.Value.footX + frontRest.Value.footX - back.Value.footX - front.Value.footX) * 0.5f;
		at.X += slide;

		bones[hip].Position = bones[hip].Position.Lerp(at, blend);
		foreach ((RigBone upper, RigBone lower, (float thigh, float shin, float footX)? leg) in new[]
		{
			(RigBone.LegBackUpper, RigBone.LegBackLower, back),
			(RigBone.LegFrontUpper, RigBone.LegFrontLower, front),
		})
		{
			bones[(int)upper].Rotation = Mathf.LerpAngle(bones[(int)upper].Rotation, leg.Value.thigh, blend);
			bones[(int)lower].Rotation = Mathf.LerpAngle(bones[(int)lower].Rotation, leg.Value.shin - leg.Value.thigh, blend);
		}
	}

	/// <summary>
	/// Standing for a drawing whose feet are cut separately (DoomBot's boots): each ankle stays
	/// exactly where it is when he stands straight - under its hip - with its foot level on the
	/// floor (LevelFeet), and the thigh and shin bend between hip and ankle, knee forward, as low as
	/// the pose puts the hips. With an ankle the shins can lean, so he stays balanced over his feet
	/// instead of leaning back on upright shins. A longer leg reaches the same floor by bending.
	/// Eric's call, 2026-10-04.
	/// </summary>
	void StandOnAnkles(Vector2 at, float blend)
	{
		int hip = (int)RigBone.Hip;
		var legs = new[] { (RigBone.LegBackUpper, RigBone.LegBackLower), (RigBone.LegFrontUpper, RigBone.LegFrontLower) };

		// Where each ankle is standing straight, and how far below it its foot reaches: the floor
		// is as low as the shorter leg's foot comes.
		float floor = float.MaxValue;
		foreach ((RigBone upper, RigBone lower) in legs)
		{
			Vector2 ankle = bones[(int)upper].Position + bones[(int)lower].Position + feet[(int)lower].Position;
			floor = Mathf.Min(floor, ankle.Y + FootDepth(feet[(int)lower]));
		}

		// The hips shift a little with the pose - forward into a punch, back to load one - over feet
		// that stay where they are, so the knees take it. Standing still the pose has no shift.
		at.X = current.HipOffset.X * HipShift;
		bones[hip].Position = bones[hip].Position.Lerp(at, blend);
		foreach ((RigBone upper, RigBone lower) in legs)
		{
			Sprite2D foot = feet[(int)lower];
			Vector2 knee = bones[(int)lower].Position;
			Vector2 shin = foot.Position;
			Vector2 joint = at + bones[(int)upper].Position;
			Vector2 rest = bones[(int)upper].Position + knee + shin;
			float spread = lower == RigBone.LegBackLower ? stanceSpread : -stanceSpread;
			var target = new Vector2(rest.X + spread, floor - FootDepth(foot));

			float l1 = knee.Length(), l2 = shin.Length();
			Vector2 d = target - joint;
			float reach = Mathf.Clamp(d.Length(), Mathf.Abs(l1 - l2) + 0.5f, l1 + l2 - 0.01f);
			float bend = Mathf.Acos(Mathf.Clamp((l1 * l1 + reach * reach - l2 * l2) / (2.0f * l1 * reach), -1.0f, 1.0f));
			float toward = d.Angle();
			// Knee forward: of the two ways the leg can bend, the one with the knee further ahead.
			float thighDir = toward - bend;
			if (Mathf.Cos(toward + bend) > Mathf.Cos(toward - bend)) thighDir = toward + bend;
			Vector2 kneeAt = joint + Vector2.Right.Rotated(thighDir) * l1;
			float thigh = thighDir - knee.Angle();
			float shinTurn = (target - kneeAt).Angle() - shin.Angle();
			bones[(int)upper].Rotation = Mathf.LerpAngle(bones[(int)upper].Rotation, thigh, blend);
			bones[(int)lower].Rotation = Mathf.LerpAngle(bones[(int)lower].Rotation, shinTurn - thigh, blend);
		}
	}

	/// <summary>
	/// How much further apart than his hips a fighter with feet stands (rig.json "stance", canonical
	/// units each way): the far foot that much ahead, the near one that much behind. Zero stands each
	/// ankle under its hip. Flambe stands with his feet apart, a hotshot (Eric, 2026-10-08).
	/// </summary>
	float stanceSpread;

	/// <summary>How much of a pose's sideways hip offset is kept while standing on planted feet.</summary>
	const float HipShift = 0.6f;

	/// <summary>How far below its ankle a level foot's drawing reaches.</summary>
	float FootDepth(Sprite2D foot)
	{
		float lowest = 0.0f;
		foreach (Vector2 point in InkEdge(foot.Texture)) lowest = Mathf.Max(lowest, (point + foot.Offset).Y);
		return lowest;
	}

	/// <summary>
	/// One leg standing with its hips at <paramref name="hipAt"/>: the thigh's turn, the shin's
	/// turn in the rig's space, and where the foot comes down across the floor. <paramref name="way"/>
	/// is the way the leg leans if it has to be angled straight: -1 back, 1 forward.
	/// </summary>
	(float thigh, float shin, float footX)? SolveLeg(RigBone upper, RigBone lower, Vector2 hipAt, float floor, int way, bool longer)
	{
		(float shin, Vector2 soleTurned, Vector2 sole, bool foot)? flat = LevelSole(lower);
		if (flat == null || !present[(int)upper]) return null;
		float shin = flat.Value.shin;
		Vector2 soleTurned = flat.Value.soleTurned;
		Vector2 joint = hipAt + bones[(int)upper].Position;
		Vector2 knee = bones[(int)lower].Position;

		if (longer && !flat.Value.foot)
		{
			// Straight, and leaning the given way until the end of it touches the floor.
			Vector2 leg = knee + flat.Value.sole;
			float reach = Mathf.Acos(Mathf.Clamp((floor - joint.Y) / Mathf.Max(1.0f, leg.Length()), -1.0f, 1.0f));
			float tilt = Mathf.Atan2(leg.X, leg.Y);
			float a = tilt - reach, b = tilt + reach;
			float angle = (leg.Rotated(a).X - leg.Rotated(b).X) * way > 0.0f ? a : b;
			return (angle, angle, joint.X + leg.Rotated(angle).X);
		}

		// The knee, straight up from a sole on the floor; the thigh turned to reach it, knee forward.
		float length = knee.Length();
		if (length < 1.0f) return null;
		float drop = floor - soleTurned.Y - joint.Y;
		float spread = Mathf.Acos(Mathf.Clamp(drop / length, -1.0f, 1.0f));
		float lean = Mathf.Atan2(knee.X, knee.Y);
		float thigh = lean - spread;
		float other = lean + spread;
		if (knee.Rotated(other).X > knee.Rotated(thigh).X) thigh = other;

		float footX = joint.X + knee.Rotated(thigh).X + soleTurned.X;
		return (thigh, shin, footX);
	}

	/// <summary>
	/// The shin's turn that lays its sole level, the sole from the knee once turned and as drawn,
	/// and whether there is a foot at all.
	/// </summary>
	(float shin, Vector2 soleTurned, Vector2 sole, bool foot)? LevelSole(RigBone lower)
	{
		Sprite2D sprite = sprites[(int)lower];
		if (!present[(int)lower] || sprite == null || sprite.Texture == null) return null;
		(Vector2 point, float slope, bool foot) = SoleOf(sprite.Texture);
		if (soleSlopes[(int)lower] is float drawn) slope = drawn;
		Transform2D t = sprite.Transform;
		Vector2 sole = t * (point + sprite.Offset);
		// Levelled as drawn, not as stretched: a leg stretched long is scaled along its length,
		// which tips its foot steeper - and turning the shin far enough to level that laid it on the
		// floor (Circy standing tall; Eric, 2026-10-08). So the shin turns as far as the foot needs
		// at its drawn size, and a stretched foot sits a little off level instead.
		Vector2 along = new Vector2(1.0f, slope).Rotated(sprite.Rotation);
		float shin = -Mathf.Atan2(along.Y, along.X);
		return (shin, sole.Rotated(shin), sole, foot);
	}

	/// <summary>How far below the hips a leg's level sole comes with the leg straight.</summary>
	float? Reach(RigBone upper, RigBone lower)
	{
		(float shin, Vector2 soleTurned, Vector2 sole, bool foot)? flat = LevelSole(lower);
		if (flat == null || !present[(int)upper]) return null;
		return bones[(int)upper].Position.Y + bones[(int)lower].Position.Length() + flat.Value.soleTurned.Y;
	}

	/// <summary>
	/// A shin's sole slope given by its drawing (rig.json "soleSlope") instead of read off it, for
	/// a leg whose bottom looks like a foot and is not - Circy's front leg, a diagonal with a tick
	/// on the end. Zero stands the shin as drawn.
	/// </summary>
	readonly float?[] soleSlopes = new float?[(int)RigBone.Count];

	/// <summary>Soles already read, by the texture's file - see <see cref="InkEdge"/> for why by path.</summary>
	static readonly Dictionary<string, (Vector2 point, float slope, bool foot)> soles = new Dictionary<string, (Vector2, float, bool)>();

	/// <summary>
	/// The bottom of a foot: a point on it, and how steeply it slopes (down per pixel across), in
	/// texture pixels - read off the lowest drawn pixel of each column in the bottom fifth or so
	/// of a shin drawing, which takes in a whole drawn foot: a boot, an L, or Circy's diagonal
	/// stroke with a tick on the end. A boot's flat bottom slopes 0; a foot drawn pointing down at
	/// the toe slopes positive. A leg that simply ends - in a point, like EdgeLord's, or a round
	/// stick end - has no foot to lay flat: anything less than twice as wide as the leg itself
	/// counts as level. And it is kept within 40 degrees either way.
	/// </summary>
	static (Vector2 point, float slope, bool foot) SoleOf(Texture2D texture)
	{
		string key = texture.ResourcePath;
		if (soles.TryGetValue(key, out var known)) return known;

		Vector2 size = texture.GetSize();
		known = (new Vector2(size.X * 0.5f, size.Y), 0.0f, false);
		Image image = texture.GetImage();
		if (image != null)
		{
			if (image.IsCompressed()) image.Decompress();
			int w = image.GetWidth();
			int h = image.GetHeight();
			// How wide the leg itself is: the middle width of the drawing's rows through its top half.
			var widths = new List<float>();
			for (int y = 0; y < h / 2; y += 2)
			{
				int left = 0;
				while (left < w && image.GetPixel(left, y).A < 0.4f) left++;
				if (left == w) continue;
				int right = w - 1;
				while (right > left && image.GetPixel(right, y).A < 0.4f) right--;
				widths.Add(right - left + 1);
			}
			widths.Sort();
			float legWidth = widths.Count > 0 ? widths[widths.Count / 2] : 0.0f;

			var bottoms = new List<Vector2>();
			float lowest = 0.0f;
			for (int x = 0; x < w; x++)
			{
				int y = h - 1;
				while (y >= 0 && image.GetPixel(x, y).A < 0.4f) y--;
				if (y < 0) continue;
				bottoms.Add(new Vector2(x, y + 1));
				lowest = Mathf.Max(lowest, y + 1);
			}
			// Only the foot: the columns that come down within the bottom fifth of the drawing.
			float band = Mathf.Max(6.0f, h * 0.22f);
			bottoms.RemoveAll(p => p.Y < lowest - band);
			if (bottoms.Count >= 3)
			{
				Vector2 mean = Vector2.Zero;
				foreach (Vector2 p in bottoms) mean += p;
				mean /= bottoms.Count;
				float sxx = 0.0f, sxy = 0.0f;
				foreach (Vector2 p in bottoms)
				{
					sxx += (p.X - mean.X) * (p.X - mean.X);
					sxy += (p.X - mean.X) * (p.Y - mean.Y);
				}
				float left = float.MaxValue, right = float.MinValue;
				foreach (Vector2 p in bottoms) { left = Mathf.Min(left, p.X); right = Mathf.Max(right, p.X); }
				bool foot = right - left >= legWidth * 2.0f;
				float slope = foot && sxx > 1.0f ? Mathf.Clamp(sxy / sxx, -0.84f, 0.84f) : 0.0f;
				var scale = new Vector2(size.X / Mathf.Max(1, w), size.Y / Mathf.Max(1, h));
				known = (mean * scale, slope * scale.Y / scale.X, foot);
			}
		}
		soles[key] = known;
		return known;
	}

	/// <summary>
	/// Squashed short, a fighter's legs can end up shorter than his arms - Circy at his smallest -
	/// and a hand hanging below his feet would be what the floor holds up, leaving the feet in the
	/// air. So, standing with short legs, each arm is swung out from the body, a few degrees at a
	/// time, until its hand clears his feet: arms out for balance, feet on the floor. Eric's call,
	/// 2026-10-04.
	/// </summary>
	void KeepHandsAboveFeet()
	{
		float soles = Mathf.Max(Mathf.Max(LowestInk(RigBone.LegBackLower), LowestInk(RigBone.LegFrontLower)),
			Mathf.Max(LowestInk(feet[(int)RigBone.LegBackLower]), LowestInk(feet[(int)RigBone.LegFrontLower])));
		if (soles == float.MinValue) return;
		float clear = soles - LegLength * 0.04f;
		// The front arm swings out forward (negative), the back arm back (positive).
		foreach ((RigBone upper, RigBone lower, float outward) in new[]
		{
			(RigBone.ArmFrontUpper, RigBone.ArmFrontLower, -1.0f),
			(RigBone.ArmBackUpper, RigBone.ArmBackLower, 1.0f),
		})
		{
			if (!present[(int)upper]) continue;
			for (int step = 0; step < 20 && LowestInk(lower) > clear; step++)
			{
				bones[(int)upper].Rotation += Mathf.DegToRad(5.0f) * outward;
			}
		}
	}

	/// <summary>
	/// What can rest on the floor: the feet, and the hands too - a crouch that hangs an arm below
	/// the feet puts the hand on the floor rather than through it.
	/// </summary>
	static readonly RigBone[] FloorParts =
	{
		RigBone.LegBackLower, RigBone.LegFrontLower, RigBone.ArmBackLower, RigBone.ArmFrontLower,
	};

	/// <summary>
	/// The box round every part of the body that is showing - not a held weapon, a hat or any
	/// other extra - in <paramref name="space"/>'s coordinates. How big a fighter is, for framing
	/// a portrait.
	/// </summary>
	public Rect2 BodyBounds(Node2D space)
	{
		Transform2D toSpace = space.GetGlobalTransform().AffineInverse();
		Rect2? bounds = null;
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			Sprite2D sprite = sprites[i];
			if (sprite == null || sprite.Texture == null || !sprite.IsVisibleInTree() || (RigBone)i == RigBone.PropFront) continue;
			Transform2D t = toSpace * sprite.GetGlobalTransform();
			Rect2 rect = sprite.GetRect();
			foreach (Vector2 corner in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), new Vector2(rect.Position.X, rect.End.Y), rect.End })
			{
				Vector2 p = t * corner;
				bounds = bounds == null ? new Rect2(p, Vector2.Zero) : bounds.Value.Expand(p);
			}
		}
		foreach (Sprite2D foot in feet)
		{
			if (foot == null || !foot.IsVisibleInTree()) continue;
			Transform2D t = toSpace * foot.GetGlobalTransform();
			Rect2 rect = foot.GetRect();
			foreach (Vector2 corner in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), new Vector2(rect.Position.X, rect.End.Y), rect.End })
			{
				Vector2 p = t * corner;
				bounds = bounds == null ? new Rect2(p, Vector2.Zero) : bounds.Value.Expand(p);
			}
		}
		return bounds ?? new Rect2();
	}

	/// <summary>
	/// How wide the feet stand, in world pixels: from the back of one foot to the front of the
	/// other, as the legs hang at rest. What the fighter's collision shape stands on.
	/// </summary>
	public float StanceWidth()
	{
		float left = float.MaxValue, right = float.MinValue;
		foreach (Sprite2D sprite in new[] { sprites[(int)RigBone.LegBackLower], sprites[(int)RigBone.LegFrontLower],
			feet[(int)RigBone.LegBackLower], feet[(int)RigBone.LegFrontLower] })
		{
			if (sprite == null || sprite.Texture == null) continue;
			Transform2D t = InRig(sprite);
			Rect2 rect = sprite.GetRect();
			foreach (Vector2 corner in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), new Vector2(rect.Position.X, rect.End.Y), rect.End })
			{
				float x = (t * corner).X;
				left = Mathf.Min(left, x);
				right = Mathf.Max(right, x);
			}
		}
		return left == float.MaxValue ? 0.0f : (right - left) * puppetScale;
	}

	/// <summary>A node's transform in the rig's own space, through every bone above it.</summary>
	Transform2D InRig(Node2D node)
	{
		Transform2D t = node.Transform;
		for (Node parent = node.GetParent(); parent != this && parent is Node2D above; parent = above.GetParent())
		{
			t = above.Transform * t;
		}
		return t;
	}

	/// <summary>
	/// Ink outlines already read, by the texture's file - shared, so a second copy of a rig reads
	/// nothing. Keyed by path rather than by the texture itself, which a static would otherwise
	/// keep alive past the end of the game.
	/// </summary>
	static readonly Dictionary<string, Vector2[]> inkEdges = new Dictionary<string, Vector2[]>();

	/// <summary>
	/// The outline of a part's ink - its leftmost and rightmost drawn pixel every couple of rows -
	/// in texture pixels. What touches the floor is the drawing, not the corners of the picture it
	/// sits in: a boot turned up on its toe would otherwise hover by the empty paper behind its
	/// heel. Read from the image once and kept; the picture's corners if it cannot be read.
	/// </summary>
	static Vector2[] InkEdge(Texture2D texture)
	{
		string key = texture.ResourcePath;
		if (inkEdges.TryGetValue(key, out Vector2[] known)) return known;

		Vector2 size = texture.GetSize();
		var points = new List<Vector2>();
		Image image = texture.GetImage();
		if (image != null)
		{
			if (image.IsCompressed()) image.Decompress();
			int w = image.GetWidth();
			int h = image.GetHeight();
			var scale = new Vector2(size.X / Mathf.Max(1, w), size.Y / Mathf.Max(1, h));
			for (int y = h - 1; y >= 0; y -= 2)
			{
				int left = 0;
				while (left < w && image.GetPixel(left, y).A < 0.4f) left++;
				if (left == w) continue;
				int right = w - 1;
				while (right > left && image.GetPixel(right, y).A < 0.4f) right--;
				points.Add(new Vector2(left, y + 1) * scale);
				points.Add(new Vector2(right + 1, y + 1) * scale);
			}
		}
		if (points.Count == 0)
		{
			points.AddRange(new[] { Vector2.Zero, new Vector2(size.X, 0.0f), new Vector2(0.0f, size.Y), size });
		}
		known = points.ToArray();
		inkEdges[key] = known;
		return known;
	}

	/// <summary>Named spots on the drawing, each on a bone and in its canonical units from the joint.</summary>
	readonly Dictionary<string, (RigBone bone, Vector2 offset)> points = new Dictionary<string, (RigBone, Vector2)>();

	void LoadPoints(Godot.Collections.Dictionary root)
	{
		points.Clear();
		if (!root.ContainsKey("points")) return;
		foreach (Godot.Collections.Dictionary entry in (Godot.Collections.Array)root["points"])
		{
			if (!RigBones.TryParse((string)entry["bone"], out RigBone bone) || !present[(int)bone]) continue;
			var offset = (Godot.Collections.Array)entry["offset"];
			points[(string)entry["name"]] = (bone, new Vector2((float)offset[0], (float)offset[1]));
		}
	}

	/// <summary>
	/// Where a named spot on the drawing is right now, in global coordinates - DoomBot's eye, the
	/// tip of an antenna. It rides on its bone, so it follows every pose, the facing and the
	/// squash. Null if this drawing does not name that spot.
	/// </summary>
	public Vector2? PointGlobal(string name)
	{
		if (string.IsNullOrEmpty(name) || !points.TryGetValue(name, out var point)) return null;
		return bones[(int)point.bone].GlobalTransform * point.offset;
	}

	/// <summary>Where a bone's joint is right now, in global coordinates. Null if the drawing has no such bone.</summary>
	public Vector2? JointGlobal(RigBone bone) => present[(int)bone] ? bones[(int)bone].GlobalPosition : null;

	/// <summary>
	/// Turns his head round to look behind him - the head and everything on it (a hard hat)
	/// mirrored about the neck, which is how a side-on cutout looks over its shoulder. A fighter
	/// whose face is drawn on his body (EdgeLord's V, Triguy's triangle, Circy's ball) has the
	/// body drawing turned round instead, about the hip; his limbs stay where they hang.
	/// </summary>
	public void SetLookingBack(bool back)
	{
		float x = back ? -1.0f : 1.0f;
		if (sprites[(int)RigBone.Head] != null)
		{
			bones[(int)RigBone.Head].Scale = new Vector2(x, 1.0f);
			return;
		}
		Sprite2D body = sprites[(int)RigBone.Torso];
		if (body == null) return;
		body.Scale = new Vector2(x, body.Scale.Y);
		// Limbs that come out of the body's edges turn round with it (Triguy's, off his corners);
		// on a body drawn about even either side of its middle, they are already on it. Moving
		// EdgeLord's arm to the other side of his V carried his back-air sword half a body further
		// back than its hit (2026-10-10).
		bodyTurned = back && legsOnBody;
	}

	/// <summary>The body drawing is turned round (SetLookingBack), and every limb that comes out of its edges with it.</summary>
	bool bodyTurned;
	bool limbsMoved;

	/// <summary>
	/// The limbs come out of the body's edges rather than from the hip and shoulders
	/// (rig.json "legsOnBody"), so they move with the body when it tips or turns round: Triguy's,
	/// out at the corners of his triangle. Hung from the hip, a tipped triangle swung clean off
	/// his legs.
	/// </summary>
	bool legsOnBody;

	/// <summary>
	/// Puts each limb where it comes out of the body: turned round with a turned-round body, and,
	/// for legs that come out of the body (<see cref="legsOnBody"/>), carried round as it tips.
	/// The limbs' own swings are untouched - only where each one starts.
	/// </summary>
	void PlaceLimbRoots()
	{
		// Once moved, they are put back where they belong when the body turns back again.
		if (!bodyTurned && !legsOnBody && !limbsMoved) return;
		limbsMoved = bodyTurned || legsOnBody;
		float mirror = bodyTurned ? -1.0f : 1.0f;
		float tip = present[(int)RigBone.Torso] ? bones[(int)RigBone.Torso].Rotation : 0.0f;
		foreach (RigBone bone in new[] { RigBone.ArmBackUpper, RigBone.ArmFrontUpper, RigBone.LegBackUpper, RigBone.LegFrontUpper })
		{
			int i = (int)bone;
			if (!present[i]) continue;
			var at = new Vector2(restOffsets[i].X * mirror, restOffsets[i].Y);
			bool leg = bone == RigBone.LegBackUpper || bone == RigBone.LegFrontUpper;
			// An arm hangs from the body bone already, so it tips with it; a leg hangs from the hip.
			if (leg && legsOnBody) at = at.Rotated(tip);
			bones[i].Position = at;
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
		// An empty hand - a prop slot that only exists so a move can fill it - holds nothing.
		if (defaultPropEmpty && string.IsNullOrEmpty(shownProp)) return null;
		Vector2 size = sprite.Texture.GetSize();
		Vector2 grip = -sprite.Offset;
		// Held weapons are stored tip-up and turned; the default prop is stored tip-down.
		Vector2 head = Mathf.IsZeroApprox(sprite.Rotation)
			? new Vector2(grip.X, size.Y * 0.88f)
			: new Vector2(grip.X, size.Y * 0.14f);
		return sprite.GlobalTransform * (head - grip);
	}

	/// <summary>
	/// Where his two fists are, in global coordinates: the front hand where whatever he holds is
	/// gripped (the prop bone), and the back hand the same way along its own forearm. Null if the
	/// drawing has no prop bone to say where a fist is.
	/// </summary>
	public (Vector2 front, Vector2 back)? HandsGlobal()
	{
		int prop = (int)RigBone.PropFront;
		int backArm = (int)RigBone.ArmBackLower;
		if (!present[prop] || !present[backArm]) return null;
		return (bones[prop].GlobalPosition, bones[backArm].GlobalTransform * restOffsets[prop]);
	}

	/// <summary>Whether the front hand is empty with this move's prop: "-", or nothing put in a blank hand.</summary>
	public bool HandEmpty(string propArt) =>
		propArt == "-" || (string.IsNullOrEmpty(propArt) && defaultPropEmpty) || !present[(int)RigBone.PropFront];

	/// <summary>Shoulder to the end of the hand, on screen, for an arm as drawn.</summary>
	public float ArmLength(RigBone upper, RigBone lower)
	{
		Sprite2D forearm = sprites[(int)lower];
		if (!present[(int)upper] || !present[(int)lower] || forearm?.Texture == null) return 0.0f;
		float l1 = bones[(int)lower].Position.Length() * bones[(int)upper].GlobalTransform.Y.Length();
		float l2 = (forearm.Texture.GetSize().Y + forearm.Offset.Y) * forearm.Scale.Y * bones[(int)lower].GlobalTransform.Y.Length();
		return l1 + l2;
	}

	/// <summary>The hand's own prop is a blank (rig.json "empty"): nothing there until a move puts something in it.</summary>
	bool defaultPropEmpty;

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
				FigureHeight = entry.ContainsKey("figureHeight") ? (float)entry["figureHeight"] : 0.0f,
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
		// A whole figure drawn from another angle stands as tall as the puppet does.
		if (art.FigureHeight > 0.0f && figureHeight > 0.0f) return puppetScale * figureHeight / art.FigureHeight;
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

		ApplyLegLengths();
		PlaceHip();
	}

	/// <summary>
	/// Stretches the front leg alone, on top of both legs' stretch - a kick shot out long to the
	/// hit. Like <see cref="SetLegStretch"/>, the art is only scaled along its own length. The hip
	/// does not move: the other leg is still the one standing.
	/// </summary>
	public void SetFrontLegReach(float reach)
	{
		if (!Loaded || Mathf.IsEqualApprox(reach, frontLegReach)) return;
		frontLegReach = reach;
		ApplyLegLengths();
	}

	/// <summary>
	/// Points the front leg, hip to foot and straight, at a spot - blended in by
	/// <paramref name="amount"/> over whatever the pose has it doing. A stretched kick goes where
	/// its hit is, whichever way the shared kick pose happens to point. Call it after the pose is
	/// applied; the next pose puts the leg back.
	/// </summary>
	public void AimFrontLeg(Vector2 targetGlobal, float amount)
	{
		int upper = (int)RigBone.LegFrontUpper;
		int lower = (int)RigBone.LegFrontLower;
		if (!present[upper] || amount <= 0.0f || bones[upper].GetParent() is not Node2D parent) return;
		// A hanging limb points down its own +Y, so the turn that aims it is the target's angle
		// less a quarter turn - worked out in the parent's space, which carries the mirroring.
		Vector2 toward = parent.ToLocal(targetGlobal) - bones[upper].Position;
		float aim = toward.Angle() - Mathf.Pi * 0.5f;
		bones[upper].Rotation = Mathf.LerpAngle(bones[upper].Rotation, aim, amount);
		if (present[lower]) bones[lower].Rotation = Mathf.Lerp(bones[lower].Rotation, 0.0f, amount);
	}

	/// <summary>
	/// Puts an arm's hand on a spot - the shoulder, elbow and hand solved as two bones, the elbow
	/// bending down and out - blended in by <paramref name="amount"/> over the pose. How hands
	/// hold a ledge: on the corner, whatever the pose and however big the fighter. Call it after
	/// the pose is applied. Out of reach, the arm points straight at it.
	/// </summary>
	public void ReachArm(RigBone upper, RigBone lower, Vector2 targetGlobal, float amount)
	{
		if (!present[(int)upper] || !present[(int)lower] || amount <= 0.0f) return;
		if (bones[(int)upper].GetParent() is not Node2D parent) return;
		Sprite2D forearm = sprites[(int)lower];
		if (forearm == null || forearm.Texture == null) return;

		Vector2 shoulder = bones[(int)upper].Position;
		Vector2 target = parent.ToLocal(targetGlobal);
		Vector2 elbowRest = bones[(int)lower].Position;
		float l1 = elbowRest.Length();
		// The hand: the far end of the forearm drawing, down its length from the elbow.
		float l2 = (forearm.Texture.GetSize().Y + forearm.Offset.Y) * forearm.Scale.Y;
		if (l1 < 1.0f || l2 < 1.0f) return;

		Vector2 d = target - shoulder;
		float reach = Mathf.Clamp(d.Length(), Mathf.Abs(l1 - l2) + 0.5f, l1 + l2 - 0.01f);
		float bend = Mathf.Acos(Mathf.Clamp((l1 * l1 + reach * reach - l2 * l2) / (2.0f * l1 * reach), -1.0f, 1.0f));
		float toward = d.Angle();
		// Elbow down: of the two ways the arm can bend, the one with the elbow lower.
		float armDir = Mathf.Sin(toward + bend) > Mathf.Sin(toward - bend) ? toward + bend : toward - bend;
		Vector2 elbow = shoulder + Vector2.Right.Rotated(armDir) * l1;
		float upperTurn = armDir - elbowRest.Angle();
		float lowerTurn = (target - elbow).Angle() - Mathf.Pi * 0.5f - upperTurn;
		bones[(int)upper].Rotation = Mathf.LerpAngle(bones[(int)upper].Rotation, upperTurn, amount);
		bones[(int)lower].Rotation = Mathf.LerpAngle(bones[(int)lower].Rotation, lowerTurn, amount);
	}

	static readonly RigBone[] LegParts = { RigBone.LegBackUpper, RigBone.LegBackLower, RigBone.LegFrontUpper, RigBone.LegFrontLower };

	void ApplyLegLengths()
	{
		foreach (RigBone bone in LegParts)
		{
			int i = (int)bone;
			bool front = bone == RigBone.LegFrontUpper || bone == RigBone.LegFrontLower;
			float k = legStretch * (front ? frontLegReach : 1.0f);
			if (sprites[i] != null) sprites[i].Scale = new Vector2(1.0f, k);
			// The knees move with it - down the leg only: a knee drawn off to one side (a bowed
			// leg) stays on its side.
			if ((bone == RigBone.LegBackLower || bone == RigBone.LegFrontLower) && present[i])
			{
				bones[i].Position = new Vector2(restOffsets[i].X, restOffsets[i].Y * k);
			}
		}
	}

	float roll;
	bool rollingOnFloor;

	/// <summary>
	/// Turns the whole puppet about the middle of the fighter's body, for a roll or a flip. Zero
	/// is upright. Turning about the hip instead would swing the body round the feet.
	/// <paramref name="alongFloor"/> rolls it along the floor like a ball instead: turned about the
	/// middle of the curled-up body, with whatever part of him is lowest touching the floor the
	/// whole way round - his feet, his back, his head. Turned about the middle of the standing
	/// body, a ball curled up below it swings up off the floor and back down - a somersault in
	/// the air, not a roll (Eric, 2026-10-10).
	/// </summary>
	public void SetRoll(float radians, bool alongFloor = false)
	{
		// Rolling along the floor follows the pose as it curls up, so it is placed every frame.
		if (Mathf.IsEqualApprox(radians, roll) && !alongFloor && !rollingOnFloor) return;
		roll = radians;
		rollingOnFloor = alongFloor;
		PlaceHip();
	}

	void PlaceHip()
	{
		var hip = new Vector2(0.0f, bodyHalfHeight - LegLength * legStretch * puppetScale * squash);
		if (rollingOnFloor && CurledBall(hip, out Vector2 centre))
		{
			// Turned about its middle, then set down so its lowest point is on the floor.
			float lowest = float.MinValue;
			foreach (Vector2 p in ballPoints) lowest = Mathf.Max(lowest, (p - centre).Rotated(roll).Y);
			var middle = new Vector2(centre.X, bodyHalfHeight - lowest);
			Position = middle + (hip - centre).Rotated(roll);
		}
		else
		{
			Position = hip.Rotated(roll);
		}
		Rotation = roll;
	}

	/// <summary>The outline of the curled-up body, as <see cref="CurledBall"/> last found it.</summary>
	readonly List<Vector2> ballPoints = new List<Vector2>();

	/// <summary>
	/// The middle of the body as it is posed now, as if upright with its hip at
	/// <paramref name="hip"/>, in the fighter's own space - the ball a curled-up fighter rolls on -
	/// with the corners of every part of it in <see cref="ballPoints"/>. Every part of the body
	/// counts, not what he holds. Each part's box is taken a little in from its edges, which are
	/// mostly empty paper round the drawing, so the ball does not ride on air.
	/// </summary>
	bool CurledBall(Vector2 hip, out Vector2 centre)
	{
		var upright = new Transform2D(0.0f, Scale, 0.0f, hip);
		ballPoints.Clear();
		Rect2? bounds = null;
		void Take(Sprite2D sprite, bool body = true)
		{
			if (sprite == null || sprite.Texture == null || !sprite.Visible) return;
			Transform2D t = upright * InRig(sprite);
			Rect2 rect = sprite.GetRect().Grow(-0.1f * Mathf.Min(sprite.GetRect().Size.X, sprite.GetRect().Size.Y));
			foreach (Vector2 corner in new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), new Vector2(rect.Position.X, rect.End.Y), rect.End })
			{
				Vector2 p = t * corner;
				ballPoints.Add(p);
				if (body) bounds = bounds == null ? new Rect2(p, Vector2.Zero) : bounds.Value.Expand(p);
			}
		}
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			if ((RigBone)i == RigBone.PropFront) continue;
			Take(sprites[i]);
			Take(feet[i]);
		}
		// A hammer on his shoulder rolls with him and cannot go through the floor either, though
		// the ball still turns about his body.
		if (CarryOnShoulder) Take(sprites[(int)RigBone.PropFront], false);
		centre = bounds?.GetCenter() ?? Vector2.Zero;
		return bounds != null;
	}

	void ApplyScale()
	{
		// Squash keeps the area: taller is thinner, shorter is wider.
		float width = puppetScale / Mathf.Sqrt(Mathf.Max(0.2f, squash));
		Scale = new Vector2(width * facingSign * spinWidth, puppetScale * squash);
	}

	float spinWidth = 1.0f;

	/// <summary>
	/// Turning about the long axis of the body, as a cutout can: the drawing narrows to an edge
	/// and opens out again mirrored, which reads as rolling over. 1 is face on, -1 is the back.
	/// Laid flat by a roll, this is a corkscrew. Never quite zero, so it never vanishes.
	/// </summary>
	public void SetSpinWidth(float width)
	{
		float w = Mathf.Abs(width) < 0.12f ? 0.12f * (width < 0.0f ? -1.0f : 1.0f) : width;
		if (Mathf.IsEqualApprox(w, spinWidth)) return;
		spinWidth = w;
		ApplyScale();
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
		if (Robotic)
		{
			AdvanceRobot();
			return;
		}
		clip.Sample(clipFrame, target);
		ApplyPose(target, 0.45f);
	}

	/// <summary>
	/// Where the current clip is showing, in frames - for a robot, the key pose he has stopped on
	/// (or is snapping away from). Fighter's stomps watch this, so one lands as a foot comes down.
	/// </summary>
	public float ShownClipFrame => Robotic ? (robotFrames >= robotSnap ? robotToFrame : robotFromFrame) : clipFrame;
	public AnimationClip CurrentClip => clip;

	// --- Moving like a machine ----------------------------------------------------------

	/// <summary>
	/// Animated like a robot (FighterData.Robotic): every animation is a run of key poses, and he
	/// snaps from one to the next - a few frames at one constant speed, so the move starts and
	/// stops dead, with no easing in or out - and then holds it, perfectly still, until the next.
	/// His idle and the like stop on a new pose every <see cref="RobotBeat"/> frames, each joint
	/// set to a whole <see cref="RobotAngle"/> degrees, so his poses are sharp angles rather than
	/// curves, and a small sway is a held pose and then one clean click. His attacks snap to the
	/// coil, hold it, snap to the strike as it lands, hold, and snap back (FighterAnimations.RobotAttackKey).
	/// Eric's calls, 2026-10-04 and 2026-10-09: quick moves, sudden stops, sharp angles - never
	/// jitter. An eased bob every few frames, and beats that shortened as he walked faster, both
	/// read as shaking.
	/// </summary>
	public bool Robotic;

	/// <summary>Frames between a robot's key poses in a clip.</summary>
	const int RobotBeat = 12;

	/// <summary>Frames a robot takes to snap from one key pose to the next.</summary>
	const int RobotSnap = 3;

	/// <summary>The steps his joints stop on, as written (played a quarter bigger: 15 on screen).</summary>
	const float RobotAngle = 12.0f;

	/// <summary>The stomping walk's key poses: knee up, foot down, every 6 frames of its cycle.</summary>
	const float StompKey = 6.0f;

	readonly Pose robotFrom = new Pose();
	readonly Pose robotTo = new Pose();
	readonly Pose robotShown = new Pose();
	object robotOwner;
	int robotKey = int.MinValue;
	int robotFrames;
	int robotSnap = RobotSnap;
	float robotFromFrame, robotToFrame;

	void AdvanceRobot()
	{
		bool walk = clip == FighterAnimations.StompWalk;
		float length = walk ? StompKey : RobotBeat;
		int key = Mathf.FloorToInt(clipFrame / length);
		if (!ReferenceEquals(robotOwner, clip) || key != robotKey)
		{
			// The pose one beat ahead is the one he snaps to now and holds.
			clip.Sample((key + 1) * length, target);
			// The walk keeps its own angles - its feet have to land - and so does anything held still.
			if (!walk) Snapped(target);
			robotFromFrame = robotToFrame;
			robotToFrame = (key + 1) * length;
			StartSnap(target, clip, key, walk ? 2 : RobotSnap);
		}
		ShowSnap();
	}

	/// <summary>
	/// Shows a robot's attack: <paramref name="key"/> is the pose, and <paramref name="keyId"/> which
	/// key of the move it is. A new key starts a snap to it from wherever he is; the same key again
	/// carries the snap on, then holds.
	/// </summary>
	public void RobotApply(Pose key, object owner, int keyId, int snapFrames)
	{
		if (!Loaded) return;
		clip = null;
		if (!ReferenceEquals(robotOwner, owner) || keyId != robotKey) StartSnap(key, owner, keyId, snapFrames);
		ShowSnap();
	}

	void StartSnap(Pose key, object owner, int keyId, int snapFrames)
	{
		robotFrom.CopyFrom(robotShown);
		robotTo.CopyFrom(key);
		robotOwner = owner;
		robotKey = keyId;
		robotFrames = 0;
		robotSnap = Mathf.Max(1, snapFrames);
	}

	void ShowSnap()
	{
		robotFrames++;
		// One constant speed the whole way, and a dead stop.
		float t = Mathf.Min(1.0f, robotFrames / (float)robotSnap);
		Pose.Blend(robotFrom, robotTo, t, robotShown);
		ApplyPose(robotShown, 1.0f);
	}

	/// <summary>Every joint stopped on a whole step: a robot's poses are sharp angles.</summary>
	static void Snapped(Pose pose)
	{
		for (int i = 0; i < (int)RigBone.Count; i++)
		{
			var bone = (RigBone)i;
			if (bone == RigBone.Hip) continue;
			pose.Set(bone, Mathf.Round(pose[bone] / RobotAngle) * RobotAngle);
		}
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
		if (CarryOnShoulder || HandOnHip)
		{
			carried.CopyFrom(pose);
			if (HandOnHip) FighterAnimations.HandOnHip(carried);
			if (CarryOnShoulder) FighterAnimations.CarryOnShoulder(carried);
			pose = carried;
		}

		// Blending toward the target rather than snapping to it smooths the joins between
		// states, so a fighter landing out of a launch does not pop from tumbling to standing.
		Pose.Exaggerate(pose, Drama, HipDrama * DramaScale, exaggerated, DramaScale);
		Follow(exaggerated, blend);

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

		PlaceLimbRoots();
		standBlend = Mathf.MoveToward(standBlend, standing && planted ? 1.0f : 0.0f, 0.2f);
		if (standBlend > 0.0f) StandOnFeet(standBlend);

		plantBlend = Mathf.MoveToward(plantBlend, planted ? 1.0f : 0.0f, 0.25f);
		LevelFeet();
		if (planted && legStretch < 1.0f) KeepHandsAboveFeet();
		if (plantBlend > 0.0f && present[(int)RigBone.Hip])
		{
			bones[(int)RigBone.Hip].Position += new Vector2(0.0f, FloorGap() * plantBlend);
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
