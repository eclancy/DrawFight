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
	OverheadArc,
	BuildDown,
	Spin,
	LowThrust,
	PointUp,
	Spread,
	SummonLow,
	DownSwing,
	BackSlash,
	HangUp,
	Soar,

	/// <summary>
	/// Both arms spinning round at the shoulders like a windmill, leaning into a charge. Not a
	/// windup and a strike: through the active frames the arms keep turning, one hit per turn.
	/// DoomBot's dash attack.
	/// </summary>
	Windmill,

	/// <summary>
	/// A wide, slow swing of a two-handed weapon: drawn right back behind him, then swept up over
	/// his head and down in front through the active frames, the arms following a hitbox that
	/// travels the same arc (<see cref="MoveData.SweepDegrees"/>). EdgeLord's up smash.
	/// </summary>
	WideArc,
}

/// <summary>
/// THE shared animation library. Authored once against the skeleton in <see cref="RigBone"/>,
/// and inherited by every fighter for free - this is the payoff for fixing the bone hierarchy,
/// and the reason adding a character is an afternoon rather than a week.
///
/// Sign convention, worth re-reading before editing any number here: POSITIVE swings a limb
/// BACKWARD, negative swings it FORWARD. Fighters always face right in their own local space.
/// That holds for every bone, the torso and head included - negative leans the body into the
/// facing direction - because FighterRig flips those two when it applies them (they point up
/// from their joint, where the limbs hang down).
///
/// Everything here is played bigger than written (FighterRig.Drama), attacks coil past their
/// windup and hold past their strike (SampleAttack), and the fighter squashes and stretches
/// (Fighter.TargetSquash). Author the poses at their natural size; the drama is applied on top.
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
			(Torso, -2), (Head, 2),
			(AFU, -14), (AFL, -24), (ABU, 14), (ABL, -18),
			(LFU, -14), (LFL, 10), (LBU, 14), (LBL, 6))),
		(46, new Pose(new Vector2(0, 9),
			(Torso, 4), (Head, -4),
			(AFU, -6), (AFL, -32), (ABU, 8), (ABL, -26),
			(LFU, -12), (LFL, 16), (LBU, 12), (LBL, 12))),
		(92, new Pose(Vector2.Zero,
			(Torso, -2), (Head, 2),
			(AFU, -14), (AFL, -24), (ABU, 14), (ABL, -18),
			(LFU, -14), (LFL, 10), (LBU, 14), (LBL, 6))));

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
		(0, new Pose(new Vector2(0, 4),
			(Torso, -16), (Head, 8),
			(AFU, 44), (AFL, -46), (ABU, -46), (ABL, -60),
			(LFU, -46), (LFL, 10), (LBU, 36), (LBL, 50))),
		// Passing: front leg under the body, back leg swinging through, knee high and bent.
		(6, new Pose(new Vector2(0, -10),
			(Torso, -18), (Head, 9),
			(AFU, 16), (AFL, -55), (ABU, -16), (ABL, -55),
			(LFU, -4), (LFL, 8), (LBU, -30), (LBL, 95))),
		(12, new Pose(new Vector2(0, 4),
			(Torso, -16), (Head, 8),
			(AFU, -46), (AFL, -60), (ABU, 42), (ABL, -46),
			(LFU, 36), (LFL, 50), (LBU, -46), (LBL, 10))),
		(18, new Pose(new Vector2(0, -10),
			(Torso, -18), (Head, 9),
			(AFU, -16), (AFL, -55), (ABU, 16), (ABL, -55),
			(LFU, -30), (LFL, 95), (LBU, -4), (LBL, 8))),
		(24, new Pose(new Vector2(0, 4),
			(Torso, -16), (Head, 8),
			(AFU, 44), (AFL, -46), (ABU, -46), (ABL, -60),
			(LFU, -46), (LFL, 10), (LBU, 36), (LBL, 50))));

	public static readonly AnimationClip Jump = new AnimationClip("jump", false,
		(0, new Pose(new Vector2(0, 12),
			(Torso, -12), (Head, 8),
			(AFU, -70), (AFL, -40), (ABU, -50), (ABL, -30),
			(LFU, -30), (LFL, 70), (LBU, 20), (LBL, 80))),
		(10, new Pose(new Vector2(0, -10),
			(Torso, -6), (Head, -6),
			(AFU, -150), (AFL, -20), (ABU, -130), (ABL, -20),
			(LFU, -64), (LFL, 104), (LBU, -22), (LBL, 112))));

	public static readonly AnimationClip Fall = new AnimationClip("fall", true,
		(0, new Pose(Vector2.Zero,
			(Torso, 8), (Head, -6),
			(AFU, -112), (AFL, -30), (ABU, 100), (ABL, 30),
			(LFU, -30), (LFL, 40), (LBU, 30), (LBL, 52))),
		(24, new Pose(new Vector2(0, -4),
			(Torso, 13), (Head, -11),
			(AFU, -132), (AFL, -12), (ABU, 122), (ABL, 42),
			(LFU, -18), (LFL, 62), (LBU, 40), (LBL, 30))),
		(48, new Pose(Vector2.Zero,
			(Torso, 8), (Head, -6),
			(AFU, -112), (AFL, -30), (ABU, 100), (ABL, 30),
			(LFU, -30), (LFL, 40), (LBU, 30), (LBL, 52))));

	public static readonly AnimationClip Land = new AnimationClip("land", false,
		(0, new Pose(new Vector2(0, 46),
			(Torso, -30), (Head, 18),
			(AFU, -62), (AFL, -50), (ABU, 52), (ABL, -40),
			(LFU, -46), (LFL, 92), (LBU, 40), (LBL, 92))),
		(9, new Pose(Vector2.Zero,
			(Torso, -2), (Head, 2),
			(AFU, -12), (AFL, -20), (ABU, 12), (ABL, -16),
			(LFU, -6), (LFL, 6), (LBU, 6), (LBL, 4))));

	/// <summary>
	/// Holding down on the ground: knees deep, body folded low, guard up. Low enough that it
	/// visibly ducks under a high attack, because the hurtbox really does shrink.
	/// </summary>
	public static readonly AnimationClip Crouch = new AnimationClip("crouch", true,
		(0, new Pose(new Vector2(0, 40),
			(Torso, -20), (Head, 10),
			(AFU, -44), (AFL, -62), (ABU, 20), (ABL, -44),
			(LFU, -60), (LFL, 110), (LBU, 30), (LBL, 108))),
		(40, new Pose(new Vector2(0, 44),
			(Torso, -22), (Head, 12),
			(AFU, -40), (AFL, -66), (ABU, 22), (ABL, -48),
			(LFU, -62), (LFL, 114), (LBU, 32), (LBL, 112))),
		(80, new Pose(new Vector2(0, 40),
			(Torso, -20), (Head, 10),
			(AFU, -44), (AFL, -62), (ABU, 20), (ABL, -44),
			(LFU, -60), (LFL, 110), (LBU, 30), (LBL, 108))));

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

	static readonly Pose OverheadSlamWindup = new Pose(new Vector2(-10, -16),
		(Torso, 24), (Head, -20),
		(AFU, -170), (AFL, -40), (ABU, -164), (ABL, -40),
		(LFU, -34), (LFL, 24), (LBU, 34), (LBL, 22));

	// The arms are measured from the torso, and the torso is pitched well forward into the
	// slam, so the arms swing far forward too - or the hammer ends up at his own feet.
	static readonly Pose OverheadSlamStrike = new Pose(new Vector2(10, 16),
		(Torso, -44), (Head, 20),
		(AFU, -92), (AFL, -5), (ABU, -82), (ABL, -5),
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

	// A big slow arc: the weapon starts low behind him and is swung up and over his head.
	static readonly Pose OverheadArcWindup = new Pose(new Vector2(-4, 18),
		(Torso, 10), (Head, -6),
		(AFU, 70), (AFL, 10), (ABU, 60), (ABL, 10),
		(LFU, -40), (LFL, 60), (LBU, 30), (LBL, 60));

	static readonly Pose OverheadArcStrike = new Pose(new Vector2(0, -12),
		(Torso, 16), (Head, -26),
		(AFU, -175), (AFL, -5), (ABU, -170), (ABL, -5),
		(LFU, -10), (LFL, 6), (LBU, 12), (LBL, 6));

	// Leaning down to hammer something into place at his own feet. Feet apart and planted -
	// nothing crossed.
	static readonly Pose BuildDownWindup = new Pose(new Vector2(0, 8),
		(Torso, 6), (Head, -10),
		(AFU, -160), (AFL, -30), (ABU, -150), (ABL, -30),
		(LFU, -26), (LFL, 30), (LBU, 26), (LBL, 30));

	static readonly Pose BuildDownStrike = new Pose(new Vector2(4, 30),
		(Torso, -46), (Head, 16),
		(AFU, -60), (AFL, -10), (ABU, -50), (ABL, -10),
		(LFU, -40), (LFL, 70), (LBU, 40), (LBL, 70));

	// Spinning on the spot. The body stays upright - the turning frames do the turning - so only
	// the legs move: a low, wide stance to spin on, then up onto it.
	static readonly Pose SpinWindup = new Pose(new Vector2(0, 14),
		(Torso, 0), (Head, 0),
		(AFU, -85), (AFL, -10), (ABU, 85), (ABL, -10),
		(LFU, -30), (LFL, 34), (LBU, 30), (LBL, 34));

	static readonly Pose SpinStrike = new Pose(new Vector2(0, -2),
		(Torso, 0), (Head, 0),
		(AFU, -90), (AFL, 0), (ABU, 90), (ABL, 0),
		(LFU, -16), (LFL, 6), (LBU, 16), (LBL, 6));

	// Crouched, drawing the weapon back at hip height, then driving it straight out along the
	// floor - a thrust, not a sweep.
	static readonly Pose LowThrustWindup = new Pose(new Vector2(-6, 28),
		(Torso, -24), (Head, 12),
		(AFU, 30), (AFL, -70), (ABU, 30), (ABL, -20),
		(LFU, -60), (LFL, 90), (LBU, 40), (LBL, 80));

	// The arm is pushed well past horizontal because the body is leaning hard into the thrust;
	// together they point the blade straight along the floor.
	static readonly Pose LowThrustStrike = new Pose(new Vector2(14, 32),
		(Torso, -18), (Head, 10),
		(AFU, -104), (AFL, -2), (ABU, 50), (ABL, 10),
		(LFU, -70), (LFL, 70), (LBU, 50), (LBL, 60));

	// An arm flung up and forward, pointing where the summoned sword is to go - directing it.
	static readonly Pose PointUpWindup = new Pose(new Vector2(0, 12),
		(Torso, 14), (Head, -6),
		(AFU, 40), (AFL, -60), (ABU, 30), (ABL, -30),
		(LFU, -24), (LFL, 36), (LBU, 20), (LBL, 36));

	// Written short of where it ends up: the library's drama pushes it on to about 60 degrees
	// up, the angle the sword flies at.
	static readonly Pose PointUpStrike = new Pose(new Vector2(0, -8),
		(Torso, 16), (Head, -18),
		(AFU, -124), (AFL, -2), (ABU, 40), (ABL, -20),
		(LFU, -12), (LFL, 6), (LBU, 14), (LBL, 6));

	// Curled up tight, then thrown wide open - arms and legs out like a star - as the ring of
	// daggers flies out from him.
	static readonly Pose SpreadWindup = new Pose(new Vector2(0, 10),
		(Torso, -8), (Head, 8),
		(AFU, -60), (AFL, -100), (ABU, -40), (ABL, -100),
		(LFU, -80), (LFL, 110), (LBU, -50), (LBL, 110));

	static readonly Pose SpreadStrike = new Pose(new Vector2(0, -6),
		(Torso, 6), (Head, -8),
		(AFU, -135), (AFL, 0), (ABU, 135), (ABL, 0),
		(LFU, -55), (LFL, 0), (LBU, 55), (LBL, 0));

	// Both arms raised high, then slammed down and out to the sides, calling something up out of
	// the floor either side of him. Feet planted wide.
	static readonly Pose SummonLowWindup = new Pose(new Vector2(0, -8),
		(Torso, 10), (Head, -12),
		(AFU, -170), (AFL, -10), (ABU, -160), (ABL, -10),
		(LFU, -20), (LFL, 20), (LBU, 20), (LBL, 20));

	static readonly Pose SummonLowStrike = new Pose(new Vector2(0, 26),
		(Torso, -18), (Head, 10),
		(AFU, -45), (AFL, -4), (ABU, 50), (ABL, -4),
		(LFU, -40), (LFL, 40), (LBU, 40), (LBL, 40));

	// In the air: the weapon raised up behind, then swung down to hang straight under him.
	static readonly Pose DownSwingWindup = new Pose(new Vector2(0, -6),
		(Torso, 12), (Head, -10),
		(AFU, 150), (AFL, -30), (ABU, 60), (ABL, -30),
		(LFU, -60), (LFL, 90), (LBU, -30), (LBL, 90));

	static readonly Pose DownSwingStrike = new Pose(new Vector2(0, 6),
		(Torso, -22), (Head, 14),
		(AFU, -8), (AFL, -2), (ABU, 40), (ABL, -10),
		(LFU, -40), (LFL, 60), (LBU, -10), (LBL, 70));

	// A weapon swung from in front round to behind him, leaning back into it - a back air that is
	// a cut rather than a kick.
	static readonly Pose BackSlashWindup = new Pose(new Vector2(2, 0),
		(Torso, -16), (Head, 8),
		(AFU, -70), (AFL, -30), (ABU, 20), (ABL, 20),
		(LFU, -50), (LFL, 90), (LBU, -30), (LBL, 100));

	static readonly Pose BackSlashStrike = new Pose(new Vector2(-8, 0),
		(Torso, 22), (Head, -12),
		(AFU, 100), (AFL, -4), (ABU, 40), (ABL, 10),
		(LFU, -40), (LFL, 60), (LBU, 30), (LBL, 60));

	// Reaching up and back for a rope overhead, then hanging from it with both hands, legs
	// dangling and knees drawn up a little.
	static readonly Pose HangUpWindup = new Pose(new Vector2(0, 10),
		(Torso, 12), (Head, -10),
		(AFU, 150), (AFL, -20), (ABU, 130), (ABL, -20),
		(LFU, -30), (LFL, 40), (LBU, 20), (LBL, 40));

	static readonly Pose HangUpStrike = new Pose(new Vector2(0, -4),
		(Torso, 0), (Head, -14),
		(AFU, -176), (AFL, 0), (ABU, -172), (ABL, 0),
		(LFU, -24), (LFL, 34), (LBU, 10), (LBL, 44));

	// Flying: crouched to spring up, then arms swept back like the wings and legs trailing.
	static readonly Pose SoarWindup = new Pose(new Vector2(0, 16),
		(Torso, -12), (Head, 8),
		(AFU, 40), (AFL, -30), (ABU, 30), (ABL, -30),
		(LFU, -50), (LFL, 80), (LBU, 20), (LBL, 70));

	static readonly Pose SoarStrike = new Pose(new Vector2(0, -6),
		(Torso, -8), (Head, -10),
		(AFU, 120), (AFL, 10), (ABU, 110), (ABL, 10),
		(LFU, 6), (LFL, 24), (LBU, 26), (LBL, 36));

	// Windmill: arms drawn back, then leaning in at a run while they spin. The arms in the strike
	// pose are only where the spin starts; WindmillAt turns them.
	static readonly Pose WindmillWindup = new Pose(new Vector2(-4, 6),
		(Torso, 18), (Head, -8),
		(AFU, 120), (AFL, -10), (ABU, -60), (ABL, -10),
		(LFU, -30), (LFL, 40), (LBU, 24), (LBL, 20));

	static readonly Pose WindmillStrike = new Pose(new Vector2(10, 4),
		(Torso, -24), (Head, 12),
		(AFU, -90), (AFL, -6), (ABU, 90), (ABL, -6),
		(LFU, -48), (LFL, 16), (LBU, 40), (LBL, 34));

	/// <summary>Degrees the arms turn each frame of a windmill: a full turn every eight frames.</summary>
	const float WindmillSpeed = 45.0f;

	/// <summary>
	/// The windmill's active frames: the strike pose with both arms swung round, half a turn
	/// apart. Angles are wrapped into -180..180, since a joint only ever needs to point somewhere.
	/// </summary>
	static void WindmillAt(float framesIn, Pose into)
	{
		into.CopyFrom(WindmillStrike);
		float front = Mathf.Wrap(-90.0f - WindmillSpeed * framesIn, -180.0f, 180.0f);
		into.Set(AFU, front);
		into.Set(ABU, Mathf.Wrap(front + 180.0f, -180.0f, 180.0f));
	}

	// Wide arc: the body coiled back with the weapon low behind, then leaning through into the
	// front by the end. The arms in these poses are only where the swing starts and ends;
	// WideArcAt points them along the arc in between.
	static readonly Pose WideArcWindup = new Pose(new Vector2(-4, 14),
		(Torso, 14), (Head, -8),
		(AFU, 110), (AFL, -8), (ABU, 104), (ABL, -8),
		(LFU, -36), (LFL, 50), (LBU, 30), (LBL, 50));

	static readonly Pose WideArcStrike = new Pose(new Vector2(6, 8),
		(Torso, -20), (Head, 10),
		(AFU, -66), (AFL, -6), (ABU, -60), (ABL, -6),
		(LFU, -44), (LFL, 30), (LBU, 36), (LBL, 30));

	/// <summary>
	/// Where a sweeping hitbox is pointing at <paramref name="t"/> (0 to 1 through the active
	/// frames), in screen degrees for a fighter facing right: 0 forward, -90 straight up, -180
	/// straight back. The middle is the direction of <see cref="MoveData.HitboxOffset"/> from the
	/// swing pivot.
	/// </summary>
	public static float SweepAngle(MoveData move, Vector2 pivot, float t)
	{
		Vector2 mid = move.HitboxOffset - pivot;
		float middle = Mathf.RadToDeg(mid.Angle());
		return middle + move.SweepDegrees * (t - 0.5f);
	}

	/// <summary>How far through a sweep's active frames this frame is, 0 to 1.</summary>
	public static float SweepT(MoveData move, float moveFrame) =>
		Mathf.Clamp((moveFrame - move.StartupFrames - 1) / Mathf.Max(1, move.ActiveFrames - 1), 0.0f, 1.0f);

	/// <summary>
	/// The frame an attack is shown at in the parade: its first active frame, or for a sweep, the
	/// middle of it - where the hitbox the parade rings actually is.
	/// </summary>
	public static float ShowFrame(MoveData move) =>
		move.SweepDegrees != 0.0f
			? move.StartupFrames + 1 + (move.ActiveFrames - 1) * 0.5f
			: move.StartupFrames + 1;

	/// <summary>
	/// A wide arc's active frames: the body leans through from back to front while both arms -
	/// and the weapon, which continues the forearm - point along the arc at the hitbox. A joint's
	/// angle is relative to the torso, so the torso's own lean is taken back out; and a limb
	/// hanging straight down is 0, so a direction in screen degrees is that minus 90.
	/// </summary>
	static void WideArcAt(MoveData move, float moveFrame, Pose into)
	{
		float t = SweepT(move, moveFrame);
		Pose.Blend(WideArcWindup, WideArcStrike, t, into);
		// The pivot here only steers the angle; Fighter uses the same one for the hitbox itself.
		float aim = SweepAngle(move, SweepPivot, t);
		float arm = Mathf.Wrap(aim - 90.0f + into[Torso], -180.0f, 180.0f);
		into.Set(AFU, arm);
		into.Set(ABU, arm + 6.0f);
	}

	/// <summary>The point a sweeping hitbox turns round, in match units from the body centre: near the shoulders.</summary>
	public static readonly Vector2 SweepPivot = new Vector2(0.0f, -30.0f);

	/// <summary>Curled up in a ball - knees to the chest, arms wrapped in - for a dodge roll.</summary>
	public static readonly Pose Tuck = new Pose(new Vector2(0, 30),
		(Torso, -36), (Head, 24),
		(AFU, -70), (AFL, -110), (ABU, -50), (ABL, -110),
		(LFU, -110), (LFL, 140), (LBU, -90), (LBL, 140));

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
			case AttackAnim.OverheadArc: return (OverheadArcWindup, OverheadArcStrike);
			case AttackAnim.BuildDown: return (BuildDownWindup, BuildDownStrike);
			case AttackAnim.Spin: return (SpinWindup, SpinStrike);
			case AttackAnim.LowThrust: return (LowThrustWindup, LowThrustStrike);
			case AttackAnim.PointUp: return (PointUpWindup, PointUpStrike);
			case AttackAnim.Spread: return (SpreadWindup, SpreadStrike);
			case AttackAnim.SummonLow: return (SummonLowWindup, SummonLowStrike);
			case AttackAnim.DownSwing: return (DownSwingWindup, DownSwingStrike);
			case AttackAnim.BackSlash: return (BackSlashWindup, BackSlashStrike);
			case AttackAnim.HangUp: return (HangUpWindup, HangUpStrike);
			case AttackAnim.Soar: return (SoarWindup, SoarStrike);
			case AttackAnim.Windmill: return (WindmillWindup, WindmillStrike);
			case AttackAnim.WideArc: return (WideArcWindup, WideArcStrike);
			default: return (AttackWindup, AttackStrike);
		}
	}

	/// <summary>How far through the startup the windup is reached; the rest is anticipation.</summary>
	const float WindupReached = 0.55f;

	/// <summary>How much further than its windup an attack coils before it lets go.</summary>
	const float Anticipation = 0.3f;

	/// <summary>How far past its strike pose an attack swings, and holds, before recovering.</summary>
	const float FollowThrough = 0.12f;

	/// <summary>How much of the endlag is spent holding the follow-through.</summary>
	const float FollowThroughHold = 0.3f;

	/// <summary>
	/// Samples an attack against a move's frame data, in the four beats that make a hit read:
	///   - the windup, reached early - just over halfway through the startup
	///   - anticipation: it keeps coiling a little further until the moment it lets go
	///   - the strike, fully out and slightly overshot on the first active frame
	///   - follow-through: held past the strike for a beat, then an ease back to neutral
	/// A hit that goes straight from windup to strike to rest looks like a diagram of a hit.
	/// </summary>
	/// <param name="drama">
	/// A fighter's AnimationDrama: a wilder fighter coils further before it lets go and swings
	/// further through - the timing beats of the hit, rather than the angles of the pose.
	/// </param>
	public static void SampleAttack(MoveData move, float moveFrame, Pose into, bool lunging = false, float drama = 1.0f)
	{
		float anticipation = Anticipation * drama * drama;
		float followThrough = FollowThrough * drama * drama;
		(Pose windup, Pose strike) = PosesFor(lunging ? AttackAnim.Lunge : move.Anim);

		float startup = Mathf.Max(1, move.StartupFrames);
		float activeEnd = move.StartupFrames + move.ActiveFrames;

		if (moveFrame <= startup)
		{
			float t = moveFrame / startup;
			if (t < WindupReached)
			{
				float w = t / WindupReached;
				Pose.Blend(AttackRecover, windup, 1.0f - (1.0f - w) * (1.0f - w), into);
			}
			else
			{
				// Past the windup and still going: blending beyond 1 extrapolates, which is the
				// coil - the arm drawn back further than the windup pose itself.
				float a = (t - WindupReached) / (1.0f - WindupReached);
				Pose.Blend(AttackRecover, windup, 1.0f + anticipation * a, into);
			}
			return;
		}

		if (moveFrame <= activeEnd && move.Anim == AttackAnim.Windmill)
		{
			WindmillAt(moveFrame - startup, into);
			return;
		}

		if (moveFrame <= activeEnd && move.Anim == AttackAnim.WideArc)
		{
			WideArcAt(move, moveFrame, into);
			return;
		}

		if (moveFrame <= activeEnd)
		{
			// Most hits connect on the first active frame, and hitlag then freezes the puppet on
			// whatever pose it has - so the strike must already be fully out, not on its way.
			Pose.Blend(windup, strike, 1.0f + followThrough, into);
			return;
		}

		float endlag = Mathf.Max(1, move.EndlagFrames);
		float e = Mathf.Clamp((moveFrame - activeEnd) / endlag, 0.0f, 1.0f);
		if (e < FollowThroughHold)
		{
			Pose.Blend(windup, strike, 1.0f + followThrough, into);
			return;
		}

		float r = (e - FollowThroughHold) / (1.0f - FollowThroughHold);
		Pose.Blend(strike, AttackRecover, r * r * (3.0f - 2.0f * r), into);
	}

	/// <summary>
	/// Squash and stretch for an attack at this frame: compressed while coiling, stretched long
	/// through the strike, back to normal as it recovers. 1 is neither.
	/// </summary>
	public static float AttackSquash(MoveData move, float moveFrame)
	{
		float startup = Mathf.Max(1, move.StartupFrames);
		float activeEnd = move.StartupFrames + move.ActiveFrames;

		if (moveFrame <= startup)
		{
			float t = moveFrame / startup;
			return t < WindupReached ? 1.0f : 1.0f - 0.08f * (t - WindupReached) / (1.0f - WindupReached);
		}
		if (moveFrame <= activeEnd) return 1.08f;

		float e = Mathf.Clamp((moveFrame - activeEnd) / Mathf.Max(1, move.EndlagFrames), 0.0f, 1.0f);
		return Mathf.Lerp(1.08f, 1.0f, Mathf.Min(1.0f, e * 2.0f));
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
