using Godot;

/// <summary>
/// Which animation an attack plays. Named by what the body does, not by which move uses it, so
/// one animation can serve several moves and a weight class can pick a different one.
/// </summary>
public enum AttackAnim
{
	Punch,
	PunchBack,
	Uppercut,
	FrontKick,
	LowKick,
	HeavyPunch,
	Lunge,
	SmashSwing,
	FlyingKick,
	OverheadSlam,
	UpSmash,
	Split,
	Nair,
	Fair,
	Bair,
	Uair,
	Dair,
	PalmThrust,
	LowSweep,
}

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
	// No pose sets the prop bone. A held weapon is stored pointing straight on from the grip, so
	// at rotation zero it continues the forearm outward - and wherever the arm swings, it follows.

	// --- Locomotion ----------------------------------------------------------

	public static readonly AnimationClip Idle = new AnimationClip("idle", true,
		(0, new Pose(Vector2.Zero,
			(Torso, -1), (Head, 1),
			(AFU, -11), (AFL, -15), (ABU, 11), (ABL, 15),
			(LFU, -13), (LFL, 7), (LBU, 13), (LBL, -7))),
		(46, new Pose(new Vector2(0, 5),
			(Torso, 2), (Head, -2),
			(AFU, -7), (AFL, -10), (ABU, 8), (ABL, 11),
			(LFU, -11), (LFL, 9), (LBU, 11), (LBL, -4))),
		(92, new Pose(Vector2.Zero,
			(Torso, -1), (Head, 1),
			(AFU, -11), (AFL, -15), (ABU, 11), (ABL, 15),
			(LFU, -13), (LFL, 7), (LBU, 13), (LBL, -7))));

	/// <summary>
	/// A four-pose cycle: contact, passing, contact mirrored, passing mirrored. Arms swing
	/// opposite the legs, which is the single cue that makes a walk read as a walk.
	/// </summary>
	/// A lower limb is only ever bent the way a real joint bends: a shin swings BACK from the
	/// knee (positive), a forearm swings FORWARD from the elbow (negative). The first version of
	/// this cycle had both backwards, which made a thick-limbed fighter look like he was running
	/// in reverse.
	public static readonly AnimationClip Run = new AnimationClip("run", true,
		// Contact: front leg reaching forward, back leg pushing off with its knee bent.
		(0, new Pose(new Vector2(0, 2),
			(Torso, -8), (Head, 5),
			(AFU, 34), (AFL, -40), (ABU, -36), (ABL, -50),
			(LFU, -36), (LFL, 8), (LBU, 30), (LBL, 40))),
		// Passing: front leg under the body, back leg swinging through, knee high and bent.
		(6, new Pose(new Vector2(0, -6),
			(Torso, -10), (Head, 6),
			(AFU, 14), (AFL, -45), (ABU, -14), (ABL, -45),
			(LFU, -4), (LFL, 6), (LBU, -10), (LBL, 75))),
		(12, new Pose(new Vector2(0, 2),
			(Torso, -8), (Head, 5),
			(AFU, -36), (AFL, -50), (ABU, 32), (ABL, -40),
			(LFU, 30), (LFL, 40), (LBU, -36), (LBL, 8))),
		(18, new Pose(new Vector2(0, -6),
			(Torso, -10), (Head, 6),
			(AFU, -14), (AFL, -45), (ABU, 14), (ABL, -45),
			(LFU, -10), (LFL, 75), (LBU, -4), (LBL, 6))),
		(24, new Pose(new Vector2(0, 2),
			(Torso, -8), (Head, 5),
			(AFU, 34), (AFL, -40), (ABU, -36), (ABL, -50),
			(LFU, -36), (LFL, 8), (LBU, 30), (LBL, 40))));

	public static readonly AnimationClip Jump = new AnimationClip("jump", false,
		(0, new Pose(new Vector2(0, 6),
			(Torso, -6), (Head, 3),
			(AFU, -30), (AFL, -20), (ABU, 26), (ABL, 18),
			(LFU, -14), (LFL, 34), (LBU, 10), (LBL, 38))),
		(10, new Pose(new Vector2(0, -4),
			(Torso, -4), (Head, 2),
			(AFU, -62), (AFL, -34), (ABU, -48), (ABL, -28),
			(LFU, -26), (LFL, 48), (LBU, -12), (LBL, 56))));

	public static readonly AnimationClip Fall = new AnimationClip("fall", true,
		(0, new Pose(Vector2.Zero,
			(Torso, 4), (Head, -3),
			(AFU, -58), (AFL, -22), (ABU, 54), (ABL, 20),
			(LFU, -20), (LFL, 16), (LBU, 22), (LBL, 22))),
		(24, new Pose(new Vector2(0, -3),
			(Torso, 6), (Head, -5),
			(AFU, -68), (AFL, -14), (ABU, 62), (ABL, 26),
			(LFU, -14), (LFL, 24), (LBU, 28), (LBL, 14))),
		(48, new Pose(Vector2.Zero,
			(Torso, 4), (Head, -3),
			(AFU, -58), (AFL, -22), (ABU, 54), (ABL, 20),
			(LFU, -20), (LFL, 16), (LBU, 22), (LBL, 22))));

	public static readonly AnimationClip Land = new AnimationClip("land", false,
		(0, new Pose(new Vector2(0, 34),
			(Torso, -20), (Head, 12),
			(AFU, -44), (AFL, -38), (ABU, 40), (ABL, 34),
			(LFU, -30), (LFL, 56), (LBU, 30), (LBL, 56))),
		(9, new Pose(Vector2.Zero,
			(Torso, -1), (Head, 1),
			(AFU, -9), (AFL, -13), (ABU, 9), (ABL, 13),
			(LFU, -4), (LFL, 3), (LBU, 4), (LBL, -3))));

	// --- Reacting ------------------------------------------------------------

	public static readonly AnimationClip Hurt = new AnimationClip("hurt", true,
		(0, new Pose(new Vector2(-4, 2),
			(Torso, 26), (Head, 16),
			(AFU, 58), (AFL, 34), (ABU, 74), (ABL, 40),
			(LFU, -34), (LFL, 26), (LBU, 16), (LBL, 30))),
		(14, new Pose(new Vector2(-6, -2),
			(Torso, 32), (Head, 10),
			(AFU, 72), (AFL, 22), (ABU, 62), (ABL, 52),
			(LFU, -22), (LFL, 38), (LBU, 26), (LBL, 18))),
		(28, new Pose(new Vector2(-4, 2),
			(Torso, 26), (Head, 16),
			(AFU, 58), (AFL, 34), (ABU, 74), (ABL, 40),
			(LFU, -34), (LFL, 26), (LBU, 16), (LBL, 30))));

	/// <summary>
	/// Arms tucked in front, weight low. Blocking has to be readable from across the couch,
	/// because "why am I taking damage" is otherwise an unanswerable question.
	/// </summary>
	public static readonly AnimationClip Block = new AnimationClip("block", false,
		(0, new Pose(new Vector2(-2, 10),
			(Torso, -12), (Head, 6),
			(AFU, -78), (AFL, -62), (ABU, -66), (ABL, -58),
			(LFU, -10), (LFL, 22), (LBU, 12), (LBL, 24))));

	// --- Attacking -----------------------------------------------------------
	// Attacks are not clips. They are three poses sampled against the MOVE's own frame counts,
	// so the visual strike lands on exactly the frame the hitbox goes live. An attack animation
	// that disagrees with its frame data makes the game lie to the player about what hit them.

	public static readonly Pose AttackWindup = new Pose(new Vector2(-3, 2),
		(Torso, 16), (Head, -6),
		(AFU, 62), (AFL, 54), (ABU, -22), (ABL, -16),
		(LFU, -10), (LFL, 16), (LBU, 12), (LBL, 8));

	public static readonly Pose AttackStrike = new Pose(new Vector2(6, 0),
		(Torso, -14), (Head, 8),
		(AFU, -84), (AFL, -12), (ABU, 34), (ABL, 24),
		(LFU, -26), (LFL, 10), (LBU, 20), (LBL, 14));

	/// <summary>
	/// The lunging variant, used by moves that carry their momentum. Weight is thrown forward
	/// and the trailing leg is left behind, so a dash attack reads as a committed charge
	/// rather than as a jab that happens to be sliding.
	/// </summary>
	public static readonly Pose LungeWindup = new Pose(new Vector2(-6, 6),
		(Torso, 24), (Head, -10),
		(AFU, 48), (AFL, 40), (ABU, -30), (ABL, -20),
		(LFU, -34), (LFL, 40), (LBU, 26), (LBL, 18));

	public static readonly Pose LungeStrike = new Pose(new Vector2(14, 4),
		(Torso, -30), (Head, 16),
		(AFU, -92), (AFL, -20), (ABU, 52), (ABL, 30),
		(LFU, -52), (LFL, 16), (LBU, 42), (LBL, 34));

	public static readonly Pose AttackRecover = new Pose(new Vector2(1, 3),
		(Torso, -4), (Head, 3),
		(AFU, -34), (AFL, -38), (ABU, 16), (ABL, 18),
		(LFU, -12), (LFL, 8), (LBU, 10), (LBL, 4));

	// --- Attack animations ----------------------------------------------------
	// One windup and one strike pose per kind of attack. A move names its animation in
	// MoveData.Anim, so which pose a move uses is data, like its damage. Every fighter shares
	// these, which is what makes a new drawing able to do every attack the day it is imported.

	static readonly Pose PunchBackWindup = new Pose(new Vector2(-2, 2),
		(Torso, 12), (Head, -4),
		(AFU, -20), (AFL, -40), (ABU, 50), (ABL, 40),
		(LFU, -12), (LFL, 14), (LBU, 14), (LBL, 8));

	static readonly Pose PunchBackStrike = new Pose(new Vector2(6, 0),
		(Torso, -18), (Head, 8),
		(AFU, 30), (AFL, -30), (ABU, -86), (ABL, -8),
		(LFU, -24), (LFL, 10), (LBU, 22), (LBL, 16));

	static readonly Pose UppercutWindup = new Pose(new Vector2(-2, 10),
		(Torso, 14), (Head, -4),
		(AFU, 30), (AFL, 70), (ABU, -20), (ABL, -30),
		(LFU, -20), (LFL, 30), (LBU, 20), (LBL, 30));

	static readonly Pose UppercutStrike = new Pose(new Vector2(4, -6),
		(Torso, -8), (Head, -10),
		(AFU, -165), (AFL, -15), (ABU, 30), (ABL, 20),
		(LFU, -10), (LFL, 6), (LBU, 26), (LBL, 30));

	static readonly Pose FrontKickWindup = new Pose(new Vector2(-4, 0),
		(Torso, 10), (Head, -2),
		(AFU, -30), (AFL, -40), (ABU, 30), (ABL, 20),
		(LFU, -60), (LFL, 90), (LBU, 8), (LBL, 6));

	static readonly Pose FrontKickStrike = new Pose(new Vector2(-6, -2),
		(Torso, 22), (Head, -8),
		(AFU, 20), (AFL, -20), (ABU, 50), (ABL, 20),
		(LFU, -100), (LFL, -4), (LBU, 10), (LBL, 4));

	static readonly Pose LowKickWindup = new Pose(new Vector2(-2, 22),
		(Torso, -18), (Head, 10),
		(AFU, -40), (AFL, -40), (ABU, 30), (ABL, 30),
		(LFU, -30), (LFL, 80), (LBU, 40), (LBL, 70));

	static readonly Pose LowKickStrike = new Pose(new Vector2(4, 26),
		(Torso, -26), (Head, 14),
		(AFU, -20), (AFL, -30), (ABU, 40), (ABL, 20),
		(LFU, -78), (LFL, -6), (LBU, 50), (LBL, 60));

	static readonly Pose HeavyPunchWindup = new Pose(new Vector2(-6, 4),
		(Torso, 24), (Head, -8),
		(AFU, 110), (AFL, 40), (ABU, -30), (ABL, -30),
		(LFU, -18), (LFL, 20), (LBU, 18), (LBL, 12));

	static readonly Pose HeavyPunchStrike = new Pose(new Vector2(10, 4),
		(Torso, -28), (Head, 12),
		(AFU, -92), (AFL, -6), (ABU, 50), (ABL, 30),
		(LFU, -40), (LFL, 14), (LBU, 34), (LBL, 26));

	static readonly Pose SmashSwingWindup = new Pose(new Vector2(-8, 4),
		(Torso, 30), (Head, -10),
		(AFU, 150), (AFL, 30), (ABU, 130), (ABL, 40),
		(LFU, -30), (LFL, 30), (LBU, 20), (LBL, 20));

	static readonly Pose SmashSwingStrike = new Pose(new Vector2(16, 6),
		(Torso, -36), (Head, 16),
		(AFU, -82), (AFL, -10), (ABU, -60), (ABL, -20),
		(LFU, -52), (LFL, 16), (LBU, 46), (LBL, 34));

	static readonly Pose FlyingKickWindup = new Pose(new Vector2(-4, 12),
		(Torso, 16), (Head, -6),
		(AFU, -40), (AFL, -60), (ABU, 40), (ABL, 40),
		(LFU, -80), (LFL, 110), (LBU, 30), (LBL, 60));

	static readonly Pose FlyingKickStrike = new Pose(new Vector2(12, -12),
		(Torso, 30), (Head, -12),
		(AFU, 60), (AFL, -10), (ABU, 80), (ABL, 20),
		(LFU, -110), (LFL, 0), (LBU, 30), (LBL, 70));

	static readonly Pose OverheadSlamWindup = new Pose(new Vector2(-4, -4),
		(Torso, 20), (Head, -14),
		(AFU, -175), (AFL, -20), (ABU, -165), (ABL, -20),
		(LFU, -14), (LFL, 10), (LBU, 16), (LBL, 10));

	static readonly Pose OverheadSlamStrike = new Pose(new Vector2(10, 16),
		(Torso, -44), (Head, 20),
		(AFU, -55), (AFL, -5), (ABU, -45), (ABL, -5),
		(LFU, -46), (LFL, 50), (LBU, 40), (LBL, 50));

	static readonly Pose UpSmashWindup = new Pose(new Vector2(0, 20),
		(Torso, -10), (Head, 6),
		(AFU, 20), (AFL, 20), (ABU, 20), (ABL, 20),
		(LFU, -40), (LFL, 80), (LBU, 30), (LBL, 70));

	static readonly Pose UpSmashStrike = new Pose(new Vector2(0, -10),
		(Torso, 4), (Head, -14),
		(AFU, -172), (AFL, -4), (ABU, -168), (ABL, -4),
		(LFU, -8), (LFL, 4), (LBU, 8), (LBL, 4));

	static readonly Pose SplitWindup = new Pose(new Vector2(0, 12),
		(Torso, -6), (Head, 4),
		(AFU, -50), (AFL, -60), (ABU, -40), (ABL, -60),
		(LFU, -30), (LFL, 60), (LBU, 30), (LBL, 60));

	static readonly Pose SplitStrike = new Pose(new Vector2(0, 30),
		(Torso, 0), (Head, 0),
		(AFU, -95), (AFL, -4), (ABU, 95), (ABL, 4),
		(LFU, -82), (LFL, -2), (LBU, 82), (LBL, 2));

	static readonly Pose NairWindup = new Pose(Vector2.Zero,
		(Torso, -10), (Head, 4),
		(AFU, -60), (AFL, -80), (ABU, 60), (ABL, 80),
		(LFU, -70), (LFL, 110), (LBU, -40), (LBL, 110));

	static readonly Pose NairStrike = new Pose(Vector2.Zero,
		(Torso, 0), (Head, 0),
		(AFU, -125), (AFL, -4), (ABU, 125), (ABL, 4),
		(LFU, -45), (LFL, -2), (LBU, 45), (LBL, 2));

	static readonly Pose FairWindup = new Pose(new Vector2(-2, -2),
		(Torso, 18), (Head, -10),
		(AFU, -170), (AFL, -30), (ABU, 40), (ABL, 30),
		(LFU, -40), (LFL, 70), (LBU, 20), (LBL, 60));

	static readonly Pose FairStrike = new Pose(new Vector2(6, 4),
		(Torso, -34), (Head, 16),
		(AFU, -40), (AFL, -10), (ABU, 60), (ABL, 20),
		(LFU, -60), (LFL, 60), (LBU, 10), (LBL, 60));

	static readonly Pose BairWindup = new Pose(Vector2.Zero,
		(Torso, -18), (Head, 10),
		(AFU, -50), (AFL, -50), (ABU, 20), (ABL, 30),
		(LFU, -50), (LFL, 90), (LBU, -40), (LBL, 110));

	static readonly Pose BairStrike = new Pose(new Vector2(-8, 0),
		(Torso, -38), (Head, 20),
		(AFU, -70), (AFL, -20), (ABU, -20), (ABL, -20),
		(LFU, -40), (LFL, 60), (LBU, 105), (LBL, -4));

	static readonly Pose UairWindup = new Pose(new Vector2(0, 4),
		(Torso, -10), (Head, 6),
		(AFU, 30), (AFL, 30), (ABU, 30), (ABL, 30),
		(LFU, -60), (LFL, 100), (LBU, -30), (LBL, 100));

	static readonly Pose UairStrike = new Pose(new Vector2(0, -6),
		(Torso, 34), (Head, -16),
		(AFU, 70), (AFL, 10), (ABU, 80), (ABL, 10),
		(LFU, -170), (LFL, -4), (LBU, -30), (LBL, 60));

	static readonly Pose DairWindup = new Pose(new Vector2(0, -8),
		(Torso, -6), (Head, 8),
		(AFU, -120), (AFL, -20), (ABU, -110), (ABL, -20),
		(LFU, -80), (LFL, 110), (LBU, -70), (LBL, 110));

	static readonly Pose DairStrike = new Pose(new Vector2(0, 10),
		(Torso, 0), (Head, 14),
		(AFU, -140), (AFL, -10), (ABU, -130), (ABL, -10),
		(LFU, 6), (LFL, -2), (LBU, -6), (LBL, 2));

	static readonly Pose PalmThrustWindup = new Pose(new Vector2(-6, 4),
		(Torso, 22), (Head, -8),
		(AFU, 70), (AFL, 60), (ABU, 60), (ABL, 60),
		(LFU, -20), (LFL, 20), (LBU, 24), (LBL, 10));

	static readonly Pose PalmThrustStrike = new Pose(new Vector2(12, 4),
		(Torso, -26), (Head, 10),
		(AFU, -90), (AFL, -2), (ABU, -84), (ABL, -2),
		(LFU, -40), (LFL, 10), (LBU, 40), (LBL, 20));

	static readonly Pose LowSweepWindup = new Pose(new Vector2(-4, 20),
		(Torso, -30), (Head, 14),
		(AFU, 80), (AFL, 20), (ABU, 20), (ABL, 20),
		(LFU, -40), (LFL, 80), (LBU, 50), (LBL, 60));

	static readonly Pose LowSweepStrike = new Pose(new Vector2(6, 24),
		(Torso, -40), (Head, 18),
		(AFU, -50), (AFL, -4), (ABU, 40), (ABL, 20),
		(LFU, -60), (LFL, 60), (LBU, 60), (LBL, 40));

	/// <summary>The windup and strike for an animation. The recover pose is shared by all.</summary>
	public static (Pose windup, Pose strike) PosesFor(AttackAnim anim)
	{
		switch (anim)
		{
			case AttackAnim.PunchBack: return (PunchBackWindup, PunchBackStrike);
			case AttackAnim.Uppercut: return (UppercutWindup, UppercutStrike);
			case AttackAnim.FrontKick: return (FrontKickWindup, FrontKickStrike);
			case AttackAnim.LowKick: return (LowKickWindup, LowKickStrike);
			case AttackAnim.HeavyPunch: return (HeavyPunchWindup, HeavyPunchStrike);
			case AttackAnim.Lunge: return (LungeWindup, LungeStrike);
			case AttackAnim.SmashSwing: return (SmashSwingWindup, SmashSwingStrike);
			case AttackAnim.FlyingKick: return (FlyingKickWindup, FlyingKickStrike);
			case AttackAnim.OverheadSlam: return (OverheadSlamWindup, OverheadSlamStrike);
			case AttackAnim.UpSmash: return (UpSmashWindup, UpSmashStrike);
			case AttackAnim.Split: return (SplitWindup, SplitStrike);
			case AttackAnim.Nair: return (NairWindup, NairStrike);
			case AttackAnim.Fair: return (FairWindup, FairStrike);
			case AttackAnim.Bair: return (BairWindup, BairStrike);
			case AttackAnim.Uair: return (UairWindup, UairStrike);
			case AttackAnim.Dair: return (DairWindup, DairStrike);
			case AttackAnim.PalmThrust: return (PalmThrustWindup, PalmThrustStrike);
			case AttackAnim.LowSweep: return (LowSweepWindup, LowSweepStrike);
			default: return (AttackWindup, AttackStrike);
		}
	}

	/// <summary>
	/// Samples an attack against a move's frame data. Startup eases into the windup, the strike
	/// is fully out on the first active frame, and endlag drifts back toward neutral.
	/// </summary>
	public static void SampleAttack(MoveData move, float moveFrame, Pose into, bool lunging = false)
	{
		(Pose windup, Pose strike) = PosesFor(lunging ? AttackAnim.Lunge : move.Anim);

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
