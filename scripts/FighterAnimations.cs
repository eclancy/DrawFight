using Godot;

/// <summary>
/// THE shared animation library. Authored once against the skeleton in <see cref="RigBone"/>,
/// and inherited by every fighter for free - this is the payoff for fixing the bone hierarchy,
/// and the reason adding a character is an afternoon rather than a week.
///
/// Sign convention, worth re-reading before editing any number here: POSITIVE swings a limb
/// BACKWARD, negative swings it FORWARD. Fighters always face right in their own local space.
/// </summary>
public static class FighterAnimations
{
	// Shorthand so the pose tables below stay readable.
	const RigBone Torso = RigBone.Torso;
	const RigBone Head = RigBone.Head;
	const RigBone AFU = RigBone.ArmFrontUpper;
	const RigBone AFL = RigBone.ArmFrontLower;
	const RigBone ABU = RigBone.ArmBackUpper;
	const RigBone ABL = RigBone.ArmBackLower;
	const RigBone LFU = RigBone.LegFrontUpper;
	const RigBone LFL = RigBone.LegFrontLower;
	const RigBone LBU = RigBone.LegBackUpper;
	const RigBone LBL = RigBone.LegBackLower;
	const RigBone Prop = RigBone.PropFront;

	// --- Locomotion ----------------------------------------------------------

	public static readonly AnimationClip Idle = new AnimationClip("idle", true,
		(0, new Pose(Vector2.Zero,
			(Torso, -1), (Head, 1),
			(AFU, -11), (AFL, -15), (ABU, 11), (ABL, 15),
			(LFU, -13), (LFL, 7), (LBU, 13), (LBL, -7),
			(Prop, -14))),
		(46, new Pose(new Vector2(0, 5),
			(Torso, 2), (Head, -2),
			(AFU, -7), (AFL, -10), (ABU, 8), (ABL, 11),
			(LFU, -11), (LFL, 9), (LBU, 11), (LBL, -4),
			(Prop, -8))),
		(92, new Pose(Vector2.Zero,
			(Torso, -1), (Head, 1),
			(AFU, -11), (AFL, -15), (ABU, 11), (ABL, 15),
			(LFU, -13), (LFL, 7), (LBU, 13), (LBL, -7),
			(Prop, -14))));

	/// <summary>
	/// A four-pose cycle: contact, passing, contact mirrored, passing mirrored. Arms swing
	/// opposite the legs, which is the single cue that makes a walk read as a walk.
	/// </summary>
	public static readonly AnimationClip Run = new AnimationClip("run", true,
		(0, new Pose(new Vector2(0, 2),
			(Torso, -8), (Head, 5),
			(AFU, 34), (AFL, 26), (ABU, -36), (ABL, -30),
			(LFU, -36), (LFL, 12), (LBU, 30), (LBL, -34),
			(Prop, 18))),
		(6, new Pose(new Vector2(0, -6),
			(Torso, -10), (Head, 6),
			(AFU, 14), (AFL, 18), (ABU, -14), (ABL, -20),
			(LFU, -6), (LFL, -34), (LBU, 6), (LBL, -12),
			(Prop, 10))),
		(12, new Pose(new Vector2(0, 2),
			(Torso, -8), (Head, 5),
			(AFU, -36), (AFL, -28), (ABU, 32), (ABL, 26),
			(LFU, 30), (LFL, -34), (LBU, -36), (LBL, 12),
			(Prop, -30))),
		(18, new Pose(new Vector2(0, -6),
			(Torso, -10), (Head, 6),
			(AFU, -14), (AFL, -20), (ABU, 14), (ABL, 18),
			(LFU, 6), (LFL, -12), (LBU, -6), (LBL, -34),
			(Prop, -16))),
		(24, new Pose(new Vector2(0, 2),
			(Torso, -8), (Head, 5),
			(AFU, 34), (AFL, 26), (ABU, -36), (ABL, -30),
			(LFU, -36), (LFL, 12), (LBU, 30), (LBL, -34),
			(Prop, 18))));

	public static readonly AnimationClip Jump = new AnimationClip("jump", false,
		(0, new Pose(new Vector2(0, 6),
			(Torso, -6), (Head, 3),
			(AFU, -30), (AFL, -20), (ABU, 26), (ABL, 18),
			(LFU, -14), (LFL, 34), (LBU, 10), (LBL, 38),
			(Prop, -24))),
		(10, new Pose(new Vector2(0, -4),
			(Torso, -4), (Head, 2),
			(AFU, -62), (AFL, -34), (ABU, -48), (ABL, -28),
			(LFU, -26), (LFL, 48), (LBU, -12), (LBL, 56),
			(Prop, -52))));

	public static readonly AnimationClip Fall = new AnimationClip("fall", true,
		(0, new Pose(Vector2.Zero,
			(Torso, 4), (Head, -3),
			(AFU, -58), (AFL, -22), (ABU, 54), (ABL, 20),
			(LFU, -20), (LFL, 16), (LBU, 22), (LBL, 22),
			(Prop, -40))),
		(24, new Pose(new Vector2(0, -3),
			(Torso, 6), (Head, -5),
			(AFU, -68), (AFL, -14), (ABU, 62), (ABL, 26),
			(LFU, -14), (LFL, 24), (LBU, 28), (LBL, 14),
			(Prop, -48))),
		(48, new Pose(Vector2.Zero,
			(Torso, 4), (Head, -3),
			(AFU, -58), (AFL, -22), (ABU, 54), (ABL, 20),
			(LFU, -20), (LFL, 16), (LBU, 22), (LBL, 22),
			(Prop, -40))));

	public static readonly AnimationClip Land = new AnimationClip("land", false,
		(0, new Pose(new Vector2(0, 34),
			(Torso, -20), (Head, 12),
			(AFU, -44), (AFL, -38), (ABU, 40), (ABL, 34),
			(LFU, -30), (LFL, 56), (LBU, 30), (LBL, 56),
			(Prop, -36))),
		(9, new Pose(Vector2.Zero,
			(Torso, -1), (Head, 1),
			(AFU, -9), (AFL, -13), (ABU, 9), (ABL, 13),
			(LFU, -4), (LFL, 3), (LBU, 4), (LBL, -3),
			(Prop, -12))));

	// --- Reacting ------------------------------------------------------------

	public static readonly AnimationClip Hurt = new AnimationClip("hurt", true,
		(0, new Pose(new Vector2(-4, 2),
			(Torso, 26), (Head, 16),
			(AFU, 58), (AFL, 34), (ABU, 74), (ABL, 40),
			(LFU, -34), (LFL, 26), (LBU, 16), (LBL, 30),
			(Prop, 40))),
		(14, new Pose(new Vector2(-6, -2),
			(Torso, 32), (Head, 10),
			(AFU, 72), (AFL, 22), (ABU, 62), (ABL, 52),
			(LFU, -22), (LFL, 38), (LBU, 26), (LBL, 18),
			(Prop, 52))),
		(28, new Pose(new Vector2(-4, 2),
			(Torso, 26), (Head, 16),
			(AFU, 58), (AFL, 34), (ABU, 74), (ABL, 40),
			(LFU, -34), (LFL, 26), (LBU, 16), (LBL, 30),
			(Prop, 40))));

	/// <summary>
	/// Arms tucked in front, weight low. Blocking has to be readable from across the couch,
	/// because "why am I taking damage" is otherwise an unanswerable question.
	/// </summary>
	public static readonly AnimationClip Block = new AnimationClip("block", false,
		(0, new Pose(new Vector2(-2, 10),
			(Torso, -12), (Head, 6),
			(AFU, -78), (AFL, -62), (ABU, -66), (ABL, -58),
			(LFU, -10), (LFL, 22), (LBU, 12), (LBL, 24),
			(Prop, -70))));

	// --- Attacking -----------------------------------------------------------
	// Attacks are not clips. They are three poses sampled against the MOVE's own frame counts,
	// so the visual strike lands on exactly the frame the hitbox goes live. An attack animation
	// that disagrees with its frame data makes the game lie to the player about what hit them.

	public static readonly Pose AttackWindup = new Pose(new Vector2(-3, 2),
		(Torso, 16), (Head, -6),
		(AFU, 62), (AFL, 54), (ABU, -22), (ABL, -16),
		(LFU, -10), (LFL, 16), (LBU, 12), (LBL, 8),
		(Prop, 74));

	public static readonly Pose AttackStrike = new Pose(new Vector2(6, 0),
		(Torso, -14), (Head, 8),
		(AFU, -84), (AFL, -12), (ABU, 34), (ABL, 24),
		(LFU, -26), (LFL, 10), (LBU, 20), (LBL, 14),
		(Prop, -66));

	/// <summary>
	/// The lunging variant, used by moves that carry their momentum. Weight is thrown forward
	/// and the trailing leg is left behind, so a dash attack reads as a committed charge
	/// rather than as a jab that happens to be sliding.
	/// </summary>
	public static readonly Pose LungeWindup = new Pose(new Vector2(-6, 6),
		(Torso, 24), (Head, -10),
		(AFU, 48), (AFL, 40), (ABU, -30), (ABL, -20),
		(LFU, -34), (LFL, 40), (LBU, 26), (LBL, 18),
		(Prop, 60));

	public static readonly Pose LungeStrike = new Pose(new Vector2(14, 4),
		(Torso, -30), (Head, 16),
		(AFU, -92), (AFL, -20), (ABU, 52), (ABL, 30),
		(LFU, -52), (LFL, 16), (LBU, 42), (LBL, 34),
		(Prop, -74));

	public static readonly Pose AttackRecover = new Pose(new Vector2(1, 3),
		(Torso, -4), (Head, 3),
		(AFU, -34), (AFL, -38), (ABU, 16), (ABL, 18),
		(LFU, -12), (LFL, 8), (LBU, 10), (LBL, 4),
		(Prop, -30));

	/// <summary>
	/// Samples an attack against a move's frame data. Startup eases into the windup, the strike
	/// is fully out on the first active frame, and endlag drifts back toward neutral.
	/// </summary>
	public static void SampleAttack(MoveData move, float moveFrame, Pose into, bool lunging = false)
	{
		Pose windup = lunging ? LungeWindup : AttackWindup;
		Pose strike = lunging ? LungeStrike : AttackStrike;

		float startup = Mathf.Max(1, move.StartupFrames);
		float activeEnd = move.StartupFrames + move.ActiveFrames;

		if (moveFrame <= startup)
		{
			float t = moveFrame / startup;
			Pose.Blend(AttackRecover, windup, t * t, into);
			return;
		}

		if (moveFrame <= activeEnd)
		{
			// Most hits connect on the first active frame, and hitlag then freezes the puppet on
			// whatever pose it has - so the strike must already be fully out, not on its way.
			Pose.Blend(windup, strike, 1.0f, into);
			return;
		}

		float endlag = Mathf.Max(1, move.EndlagFrames);
		float e = Mathf.Clamp((moveFrame - activeEnd) / endlag, 0.0f, 1.0f);
		Pose.Blend(strike, AttackRecover, e * e * (3.0f - 2.0f * e), into);
	}

	/// <summary>
	/// How hard the rig chases an attack pose. Active frames snap: easing there leaves the arm a
	/// third extended on the frame the hitbox connects. Startup and endlag still ease, so entering
	/// and leaving a move does not pop.
	/// </summary>
	public static float AttackBlend(MoveData move, float moveFrame)
	{
		bool active = moveFrame > move.StartupFrames
			&& moveFrame <= move.StartupFrames + move.ActiveFrames;
		return active ? 1.0f : 0.6f;
	}
}
