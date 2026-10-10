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

	/// <summary>
	/// Standing on one leg with the other knee raised high, then that foot stamped down hard in
	/// front - one foot always on the floor. DoomBot's stomp quake.
	/// </summary>
	Stomp,

	/// <summary>Crouched to launch, then dead straight - legs together and pointing down - riding rocket boots.</summary>
	Rocket,

	/// <summary>
	/// Bending from the waist to set something down on the floor in front, both hands reaching
	/// down to it and the knees only a little bent, so a big fighter stays on his feet rather than
	/// kneeling. DoomBot's MiniBot.
	/// </summary>
	SetDown,

	/// <summary>
	/// The body itself driven forward point first, kept level: reared back with the front tipped
	/// up, then the hips thrust forward with the arms swept back. Every other thrust leans the
	/// torso forward, which points a long wedge of a body at the floor. Triguy's point attacks.
	/// </summary>
	PointThrust,

	/// <summary>
	/// A swan dive: crouched to spring, then stretched out long - legs together and pointed, arms
	/// swept back like wings. Played with <see cref="MoveData.Corkscrew"/>, which lays the body
	/// flat and spins it along its length. Flambe's dash attack.
	/// </summary>
	SwanDive,

	/// <summary>
	/// Riding a jackhammer down: both hands on its handles in front of his hips and his knees
	/// drawn up so his feet rest on it, the whole body juddering with it. Lug's down air.
	/// </summary>
	Jackhammer,

	/// <summary>
	/// Firing something out of his chest: arms swept back out of the way and the chest pushed out,
	/// head back. DoomBot's pocket missile, which comes out of the socket on his chest.
	/// </summary>
	ChestOut,

	/// <summary>
	/// Driving a wheelbarrow: leaning into it, both hands low on the handles in front of him. With
	/// MoveData.RunningLegs his legs run under him. Lug's wheelbarrow charge.
	/// </summary>
	PushBarrow,

	/// <summary>
	/// A one-handed cut: the blade carried up over the shoulder, then brought over and down to
	/// point straight ahead at chest height. A held weapon continues the forearm, so a punch pose
	/// points a sword at the sky; this one finishes with the blade level. EdgeLord's cut and katana.
	/// </summary>
	Slash,

	/// <summary>
	/// The return cut: the blade dropped low behind the hip, then swept up and out to point straight
	/// ahead at chest height - a backhand rising from below. EdgeLord's second jab.
	/// </summary>
	RisingCut,

	/// <summary>
	/// A stab at chest height: the elbow cocked back with the point already aimed ahead, then the
	/// arm driven straight out, the body leaning in. Shoulder plus elbow is the same in both poses
	/// and the torso does not move, so the blade stays level and only goes forward - a thrust, not
	/// a swing (see <see cref="LowThrust"/>). EdgeLord's thrust and rapier lunge.
	/// </summary>
	Thrust,

	/// <summary>
	/// A two-handed sword raised high behind the head, then brought over and down in front to
	/// point ahead, a little below level - a chop that stops at the target, not at the floor
	/// (<see cref="OverheadSlam"/> is the one that hits the ground). EdgeLord's claymore.
	/// </summary>
	Chop,

	/// <summary>
	/// <see cref="Slash"/> in the air: knees drawn up, the blade brought over from behind the head
	/// to point straight ahead. EdgeLord's forward air.
	/// </summary>
	AirSlash,

	/// <summary>
	/// In the air, knees drawn up, a heavy tool brought from over the head down in front, so it
	/// finishes pointing forward and down - a chop at whoever is in front of and below him. Lug's
	/// forward air.
	/// </summary>
	AirChop,

	/// <summary>
	/// A blade swung from in front round to point straight back at chest height, the body leaning
	/// back into it - a back air that cuts level behind him. <see cref="BackSlash"/> finishes higher,
	/// which suits a shovel and puts a sword over his own head. EdgeLord's back air.
	/// </summary>
	BackCut,
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
/// Everything here is played bigger than written (FighterRig.Drama), attacks coil a little past
/// their windup and carry on a little past their strike (SampleAttack), every joint follows its
/// pose on a spring (FighterRig.Follow), and clips curve through their keys (Pose.Curve). Author the
/// poses at their natural size; the drama is applied on top. See "Moving like a body" in
/// .ai/art-pipeline.md before adding one.
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
	// Only CarryOnShoulder sets the prop bone. A held weapon is stored pointing straight on from
	// the grip, so at rotation zero it continues the forearm outward - and wherever the arm swings,
	// it follows.

	// --- Locomotion ----------------------------------------------------------

	/// <summary>
	/// Standing. The feet stay exactly where they are and flat on the floor while the body bobs
	/// and sways above them: the knees give a little, the torso leans in and out over the hips,
	/// the head and arms follow. Eric's call (2026-10-04). Played with the rig standing
	/// (FighterRig.StandOnFeet), which works the legs out itself so each sole lies flat on the
	/// floor - so the legs below are only what the fighter falls back to off the ground, and the
	/// hip's height is a percentage of leg length (played 40% bigger, like every hip offset): the
	/// bob lowers him by about 5%.
	/// </summary>
	public static readonly AnimationClip Idle = new AnimationClip("idle", true,
		(0, new Pose(new Vector2(0, 1),
			(Torso, -2), (Head, 2),
			(AFU, -14), (AFL, -24), (ABU, 14), (ABL, -18),
			(LFU, -10), (LFL, 10), (LBU, 6), (LBL, -6))),
		(46, new Pose(new Vector2(0, 4),
			(Torso, 5), (Head, -4),
			(AFU, -5), (AFL, -32), (ABU, 7), (ABL, -26),
			(LFU, -10), (LFL, 10), (LBU, 6), (LBL, -6))),
		(92, new Pose(new Vector2(0, 1),
			(Torso, -2), (Head, 2),
			(AFU, -14), (AFL, -24), (ABU, 14), (ABL, -18),
			(LFU, -10), (LFL, 10), (LBU, 6), (LBL, -6))));

	/// <summary>
	/// The weapon in his hand carried on his shoulder, laid over whatever the rest of him is doing
	/// (FighterRig.CarryOnShoulder): the elbow down at his side, the fist up in front of his chest,
	/// and the wrist turned so the handle rests on top of the shoulder with its head behind his
	/// back - the one pose that turns the prop bone. The arm is set against the torso, so it stays
	/// on the shoulder however he leans. Lug's sledgehammer, while he stands, runs, crouches,
	/// blocks, lands and barges (FighterData.ShouldersProp; Eric's calls, 2026-10-08). Written a
	/// quarter smaller than shown, like every pose (FighterRig.Drama).
	/// </summary>
	public static void CarryOnShoulder(Pose pose)
	{
		pose.Set(AFU, 28.0f);
		pose.Set(AFL, -130.0f);
		pose.Set(RigBone.PropFront, -88.0f);
	}

	/// <summary>
	/// A hotshot standing about, laid over the idle (FighterRig.HandOnHip): the near hand planted on
	/// his hip with the elbow stuck out behind, the far fist up in front of his chest, leaning back
	/// with his chin up. The arms are set against the torso and the lean added to the idle's, so he
	/// still breathes. Flambe (FighterData.HandOnHip; Eric's call, 2026-10-08). Written a quarter
	/// smaller than shown, like every pose (FighterRig.Drama).
	/// </summary>
	public static void HandOnHip(Pose pose)
	{
		pose.Set(AFU, 44.0f);
		pose.Set(AFL, -80.0f);
		pose.Set(ABU, -24.0f);
		pose.Set(ABL, -72.0f);
		pose.Set(Torso, pose[Torso] + 5.0f);
		pose.Set(Head, pose[Head] + 7.0f);
	}

	/// <summary>
	/// Holding special to stretch or shrink: arms straight out level, legs straight down, so the
	/// change in his legs is the only thing moving. Circy's Stretch. The arms are written at 72
	/// degrees because every pose is played a quarter bigger (FighterRig.Drama), which takes
	/// them to level.
	/// </summary>
	public static readonly Pose TPose = new Pose(Vector2.Zero,
		(Torso, 0), (Head, 0),
		(AFU, -72), (AFL, 0), (ABU, 72), (ABL, 0),
		(LFU, 0), (LFL, 0), (LBU, 0), (LBL, 0));

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

	/// <summary>
	/// Leaving the ground: the legs straightening as they push off and the arms swinging forward
	/// and up to lift him - to about head height, not flung straight overhead - then the knees drawn
	/// up under him as he rises, the front one higher. Both arms straight up read as a cheer, not a
	/// jump (Eric, 2026-10-10: movement looked puppeteered and silly).
	/// </summary>
	public static readonly AnimationClip Jump = new AnimationClip("jump", false,
		(0, new Pose(new Vector2(0, 6),
			(Torso, -10), (Head, 6),
			(AFU, -60), (AFL, -30), (ABU, -36), (ABL, -30),
			(LFU, -16), (LFL, 24), (LBU, 22), (LBL, 26))),
		(10, new Pose(new Vector2(0, -6),
			(Torso, -4), (Head, -2),
			(AFU, -92), (AFL, -34), (ABU, -64), (ABL, -30),
			(LFU, -66), (LFL, 92), (LBU, -10), (LBL, 82))));

	/// <summary>
	/// Coming down: arms out for balance - the front one forward and up, the back one behind - and
	/// the legs reaching down for the floor, knees soft, the front foot a little ahead. A slow drift
	/// between two close poses, so he hangs in the air rather than holding a frozen split.
	/// </summary>
	public static readonly AnimationClip Fall = new AnimationClip("fall", true,
		(0, new Pose(Vector2.Zero,
			(Torso, 4), (Head, -2),
			(AFU, -70), (AFL, -26), (ABU, 54), (ABL, -18),
			(LFU, -24), (LFL, 30), (LBU, 14), (LBL, 36))),
		(30, new Pose(new Vector2(0, -2),
			(Torso, 7), (Head, -5),
			(AFU, -82), (AFL, -18), (ABU, 64), (ABL, -12),
			(LFU, -18), (LFL, 24), (LBU, 18), (LBL, 42))),
		(60, new Pose(Vector2.Zero,
			(Torso, 4), (Head, -2),
			(AFU, -70), (AFL, -26), (ABU, 54), (ABL, -18),
			(LFU, -24), (LFL, 30), (LBU, 14), (LBL, 36))));

	/// <summary>
	/// Touching down: the knees take the weight and the body folds forward over them, arms out,
	/// then he straightens. Deep enough to feel the landing, not so deep he looks dropped.
	/// </summary>
	public static readonly AnimationClip Land = new AnimationClip("land", false,
		(0, new Pose(new Vector2(0, 32),
			(Torso, -22), (Head, 14),
			(AFU, -50), (AFL, -44), (ABU, 40), (ABL, -34),
			(LFU, -40), (LFL, 78), (LBU, 34), (LBL, 80))),
		(9, new Pose(Vector2.Zero,
			(Torso, -2), (Head, 2),
			(AFU, -12), (AFL, -20), (ABU, 12), (ABL, -16),
			(LFU, -6), (LFL, 6), (LBU, 6), (LBL, 4))));

	/// <summary>
	/// Holding down on the ground: knees deep, body folded well forward, guard up. Low enough that
	/// it visibly ducks under a high attack, because the hurtbox really does shrink - to just over
	/// half the standing height (Fighter.CrouchHeight). The body only drops because the feet are
	/// planted (FighterRig.SetPlanted): bent legs pull the hip down to keep the soles on the floor.
	/// </summary>
	public static readonly AnimationClip Crouch = new AnimationClip("crouch", true,
		(0, new Pose(new Vector2(0, 40),
			(Torso, -34), (Head, 22),
			(AFU, -40), (AFL, -70), (ABU, 16), (ABL, -50),
			(LFU, -84), (LFL, 136), (LBU, 56), (LBL, 118))),
		(40, new Pose(new Vector2(0, 44),
			(Torso, -36), (Head, 24),
			(AFU, -36), (AFL, -74), (ABU, 18), (ABL, -54),
			(LFU, -86), (LFL, 140), (LBU, 58), (LBL, 122))),
		(80, new Pose(new Vector2(0, 40),
			(Torso, -34), (Head, 22),
			(AFU, -40), (AFL, -70), (ABU, 16), (ABL, -50),
			(LFU, -84), (LFL, 136), (LBU, 56), (LBL, 118))));

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
	/// Feet flat, like standing (see Idle), with the knees bent to drop the hips by about 8%. The
	/// shield round him does the blocking (Fighter.DrawShield), so the arms are only held ready in
	/// front, not crossed over the face.
	public static readonly AnimationClip Block = new AnimationClip("block", false,
		(0, new Pose(new Vector2(-2, 6),
			(Torso, -8), (Head, 4),
			(AFU, -40), (AFL, -46), (ABU, -26), (ABL, -40),
			(LFU, -18), (LFL, 18), (LBU, 6), (LBL, -6))));

	// --- Ledges --------------------------------------------------------------

	/// <summary>
	/// Hanging from a ledge: both arms up (the rig puts the hands on the corner -
	/// FighterRig.ReachArm), the body hanging against the wall, looking up, and the legs dangling,
	/// swinging a little. Eric's call, 2026-10-04: a hang that looks like holding on.
	/// </summary>
	public static readonly AnimationClip LedgeHang = new AnimationClip("ledge", true,
		(0, new Pose(Vector2.Zero,
			(Torso, -6), (Head, 12),
			(AFU, -150), (AFL, -10), (ABU, -140), (ABL, -10),
			(LFU, -8), (LFL, 10), (LBU, 6), (LBL, 6))),
		(40, new Pose(Vector2.Zero,
			(Torso, -2), (Head, 14),
			(AFU, -150), (AFL, -10), (ABU, -140), (ABL, -10),
			(LFU, 4), (LFL, 16), (LBU, -6), (LBL, 12))),
		(80, new Pose(Vector2.Zero,
			(Torso, -6), (Head, 12),
			(AFU, -150), (AFL, -10), (ABU, -140), (ABL, -10),
			(LFU, -8), (LFL, 10), (LBU, 6), (LBL, 6))));

	/// <summary>
	/// Pulling up over the lip: leaning in over it, one knee hauled up onto it and the arms
	/// pushing down. Held while Fighter moves the body up and over (TickLedgeAction).
	/// </summary>
	public static readonly Pose LedgeClimb = new Pose(new Vector2(0, 10),
		(Torso, -30), (Head, 18),
		(AFU, -70), (AFL, -50), (ABU, -60), (ABL, -46),
		(LFU, -96), (LFL, 104), (LBU, 20), (LBL, 40));

	// --- A machine's walk ------------------------------------------------------

	/// <summary>
	/// A robot's stomp, in place of a run (FighterData.Robotic): each leg swung straight and planted
	/// flat, the other knee hauled high on the passing step, arms swinging stiff against the legs
	/// and the body upright. Played stepped like everything a robot does, so each foot comes down in
	/// one hit - and the fighter makes the stomp land with dust, a thud and a jolt. DoomBot. Eric's
	/// call, 2026-10-04: "he should stomp when he walks". Contact on frames 0 and 12.
	/// </summary>
	public static readonly AnimationClip StompWalk = new AnimationClip("stomp", true,
		(0, new Pose(new Vector2(0, 4),
			(Torso, -4), (Head, 2),
			(AFU, 26), (AFL, -8), (ABU, -26), (ABL, -8),
			(LFU, -24), (LFL, 0), (LBU, 20), (LBL, 0))),
		(6, new Pose(new Vector2(0, -6),
			(Torso, -4), (Head, 2),
			(AFU, 0), (AFL, -8), (ABU, 0), (ABL, -8),
			(LFU, 0), (LFL, 0), (LBU, -56), (LBL, 64))),
		(12, new Pose(new Vector2(0, 4),
			(Torso, -4), (Head, 2),
			(AFU, -26), (AFL, -8), (ABU, 26), (ABL, -8),
			(LFU, 20), (LFL, 0), (LBU, -24), (LBL, 0))),
		(18, new Pose(new Vector2(0, -6),
			(Torso, -4), (Head, 2),
			(AFU, 0), (AFL, -8), (ABU, 0), (ABL, -8),
			(LFU, -56), (LFL, 64), (LBU, 0), (LBL, 0))),
		(24, new Pose(new Vector2(0, 4),
			(Torso, -4), (Head, 2),
			(AFU, 26), (AFL, -8), (ABU, -26), (ABL, -8),
			(LFU, -24), (LFL, 0), (LBU, 20), (LBL, 0))));

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

	// The leg straight out at hip height. It is written well short of level because the strike is
	// overshot (SampleAttack) and played bigger (FighterRig.Drama, and AnimationDrama in the legs):
	// written at -100 it finished pointing 40 to 60 degrees up, a high kick over the hitbox of
	// every move that uses it.
	static readonly Pose FrontKickStrike = new Pose(new Vector2(-6, -2),
		(Torso, 22), (Head, -8),
		(AFU, 20), (AFL, -20), (ABU, 50), (ABL, 20),
		(LFU, -66), (LFL, 10), (LBU, 10), (LBL, 4));

	static readonly Pose LowKickWindup = new Pose(new Vector2(-2, 22),
		(Torso, -18), (Head, 10),
		(AFU, -40), (AFL, -40), (ABU, 30), (ABL, 30),
		(LFU, -30), (LFL, 80), (LBU, 40), (LBL, 70));

	// The kicking leg reaches forward and a little down, along the floor; once overshot and played
	// bigger it is about level from a lowered hip. The front arm is flung up behind for balance,
	// which also keeps a held tool (Lug's hammer) out of the floor.
	static readonly Pose LowKickStrike = new Pose(new Vector2(4, 26),
		(Torso, -26), (Head, 14),
		(AFU, 70), (AFL, -20), (ABU, 40), (ABL, 20),
		(LFU, -53), (LFL, 13), (LBU, 50), (LBL, 60));

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

	// Set behind the barrow, knees bent to push; then leaning into it, hands low on the handles.
	static readonly Pose PushBarrowWindup = new Pose(new Vector2(-4, 8),
		(Torso, -16), (Head, 10),
		(AFU, -44), (AFL, -16), (ABU, -40), (ABL, -18),
		(LFU, -34), (LFL, 44), (LBU, 30), (LBL, 40));

	static readonly Pose PushBarrowStrike = new Pose(new Vector2(4, 6),
		(Torso, -26), (Head, 16),
		(AFU, -54), (AFL, -10), (ABU, -50), (ABL, -12),
		(LFU, -40), (LFL, 30), (LBU, 36), (LBL, 46));

	static readonly Pose ChestOutWindup = new Pose(new Vector2(-4, 4),
		(Torso, -6), (Head, 4),
		(AFU, 34), (AFL, -24), (ABU, 30), (ABL, -20),
		(LFU, -24), (LFL, 34), (LBU, 18), (LBL, 30));

	// Chest out: the torso leans back over hips pushed forward, arms held back behind him.
	static readonly Pose ChestOutStrike = new Pose(new Vector2(8, 0),
		(Torso, 16), (Head, -10),
		(AFU, 62), (AFL, -14), (ABU, 56), (ABL, -12),
		(LFU, -16), (LFL, 28), (LBU, 24), (LBL, 24));

	// Both arms reach down to the handles in front of him; knees up, feet on the hammer.
	static readonly Pose JackhammerWindup = new Pose(new Vector2(0, -6),
		(Torso, -8), (Head, 6),
		(AFU, -26), (AFL, 4), (ABU, -30), (ABL, 6),
		(LFU, -62), (LFL, 70), (LBU, -50), (LBL, 66));

	static readonly Pose JackhammerStrike = new Pose(new Vector2(0, 0),
		(Torso, -10), (Head, 8),
		(AFU, -22), (AFL, 2), (ABU, -26), (ABL, 4),
		(LFU, -58), (LFL, 74), (LBU, -46), (LBL, 70));

	static readonly Pose SwanDiveWindup = new Pose(new Vector2(-4, 14),
		(Torso, -24), (Head, 10),
		(AFU, 40), (AFL, -20), (ABU, 50), (ABL, -10),
		(LFU, -56), (LFL, 84), (LBU, 30), (LBL, 70));

	// Body straight as a dart; arms pressed back along his sides and legs together and pointed,
	// so the fire wraps one long shape. Arms swept out like wings stuck out of the fire (Eric,
	// 2026-10-10: arms and legs tighter to his body).
	static readonly Pose SwanDiveStrike = new Pose(new Vector2(0, 0),
		(Torso, 0), (Head, -4),
		(AFU, 14), (AFL, -2), (ABU, 10), (ABL, -2),
		(LFU, 2), (LFL, 0), (LBU, -2), (LBL, 0));

	static readonly Pose FlyingKickWindup = new Pose(new Vector2(-4, 12),
		(Torso, 16), (Head, -6),
		(AFU, -40), (AFL, -60), (ABU, 40), (ABL, 40),
		(LFU, -80), (LFL, 110), (LBU, 30), (LBL, 60));

	static readonly Pose FlyingKickStrike = new Pose(new Vector2(12, -12),
		(Torso, 30), (Head, -12),
		(AFU, 60), (AFL, -10), (ABU, 80), (ABL, 20),
		(LFU, -110), (LFL, 0), (LBU, 30), (LBL, 70));

	// The weapon raised overhead and a little back, the body leaning back just enough to load it.
	// The angles that reach the screen add up - the lean, the shoulder, the elbow, each played
	// bigger - so the elbow is nearly straight: leaning 24 degrees back with the elbow bent 40, the
	// forearm and the weapon pointed straight back, a body's length behind him (Eric, 2026-10-10).
	static readonly Pose OverheadSlamWindup = new Pose(new Vector2(-8, -12),
		(Torso, 10), (Head, -12),
		(AFU, -170), (AFL, -10), (ABU, -164), (ABL, -10),
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

	// The axe kick lands with the leg straight, heel first, out in front and a little below him.
	// With the knee bent, as it was, the shin hung straight down and it read as a knee.
	static readonly Pose FairStrike = new Pose(new Vector2(6, 4),
		(Torso, -34), (Head, 16),
		(AFU, -40), (AFL, -10), (ABU, 60), (ABL, 20),
		(LFU, -60), (LFL, 8), (LBU, 10), (LBL, 60));

	static readonly Pose BairWindup = new Pose(Vector2.Zero,
		(Torso, -18), (Head, 10),
		(AFU, -50), (AFL, -50), (ABU, 20), (ABL, 30),
		(LFU, -50), (LFL, 90), (LBU, -40), (LBL, 110));

	// The back leg straight out behind at hip height - written short of level for the same reason
	// as the front kick: at 105 it finished pointing 60 degrees up.
	static readonly Pose BairStrike = new Pose(new Vector2(-8, 0),
		(Torso, -38), (Head, 20),
		(AFU, -70), (AFL, -20), (ABU, -20), (ABL, -20),
		(LFU, -40), (LFL, 60), (LBU, 55), (LBL, 14));

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

	// Finishes with the weapon straight up over his head. The arms are past vertical once played
	// bigger, so the torso leans a little forward to stand them up: leaning back, as it did, laid
	// the weapon over behind him.
	static readonly Pose OverheadArcStrike = new Pose(new Vector2(0, -12),
		(Torso, -10), (Head, 6),
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
	// A stab along the floor, not a sweep: the blade points straight ahead the whole way and only
	// moves forward. Drawn back, the elbow is cocked with the upper arm behind him and the forearm
	// pointing ahead; then the arm straightens. Shoulder plus elbow is the same in both poses (and
	// the torso does not move), so the blade never swings through an arc on the way. Eric's call,
	// 2026-10-04: it was a full sweep. Low as well as level: the upper arm points down and forward
	// and the elbow keeps the blade flat, so the blade runs along just above the floor rather than
	// out from the shoulder. The total that holds it level allows for the body's lean and for every
	// pose being played bigger; with shoulder plus elbow at -88 it was -106, and the blade finished
	// tipped up 30 degrees.
	static readonly Pose LowThrustWindup = new Pose(new Vector2(-6, 30),
		(Torso, -24), (Head, 13),
		(AFU, 30), (AFL, -118), (ABU, 30), (ABL, -20),
		(LFU, -64), (LFL, 82), (LBU, 44), (LBL, 72));

	static readonly Pose LowThrustStrike = new Pose(new Vector2(14, 32),
		(Torso, -24), (Head, 13),
		(AFU, -28), (AFL, -60), (ABU, 50), (ABL, 10),
		(LFU, -70), (LFL, 70), (LBU, 50), (LBL, 60));

	// An arm flung up and forward, pointing where the summoned sword is to go - directing it.
	static readonly Pose PointUpWindup = new Pose(new Vector2(0, 12),
		(Torso, 14), (Head, -6),
		(AFU, 40), (AFL, -60), (ABU, 30), (ABL, -30),
		(LFU, -24), (LFL, 36), (LBU, 20), (LBL, 36));

	// Written short of where it ends up: the library's drama pushes it on to about 60 degrees
	// up, the angle the sword flies at. The body stays upright - leaning back, as it did, turned
	// the point up and behind him.
	static readonly Pose PointUpStrike = new Pose(new Vector2(0, -8),
		(Torso, 4), (Head, -18),
		(AFU, -100), (AFL, -2), (ABU, 40), (ABL, -20),
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

	// In the air: the weapon raised up behind, then swung down to hang straight under him. The
	// body stays upright and the arm reaches back under it, so the blade hangs below his middle -
	// leaning forward, as it did, hung it in front of his feet.
	static readonly Pose DownSwingWindup = new Pose(new Vector2(0, -6),
		(Torso, 12), (Head, -10),
		(AFU, 150), (AFL, -30), (ABU, 60), (ABL, -30),
		(LFU, -60), (LFL, 90), (LBU, -30), (LBL, 90));

	static readonly Pose DownSwingStrike = new Pose(new Vector2(0, 6),
		(Torso, 2), (Head, 10),
		(AFU, 56), (AFL, -35), (ABU, 40), (ABL, -10),
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

	// Stomp: weight on the back leg, the front knee pulled up high and the arms up for balance;
	// then the front foot driven down into the floor a step ahead, body leaning in behind it.
	static readonly Pose StompWindup = new Pose(Vector2.Zero,
		(Torso, 8), (Head, -6),
		(AFU, -110), (AFL, -30), (ABU, 70), (ABL, -20),
		(LFU, -100), (LFL, 100), (LBU, 4), (LBL, 2));

	static readonly Pose StompStrike = new Pose(Vector2.Zero,
		(Torso, -18), (Head, 10),
		(AFU, -30), (AFL, -10), (ABU, 36), (ABL, -10),
		(LFU, -30), (LFL, 8), (LBU, 18), (LBL, 12));

	// Rocket: crouched to push off, then straight as a rocket - legs together pointing down so the
	// jets fire straight out of the soles, arms pressed down by his sides.
	static readonly Pose RocketWindup = new Pose(Vector2.Zero,
		(Torso, -10), (Head, 6),
		(AFU, -20), (AFL, -30), (ABU, 20), (ABL, -30),
		(LFU, -50), (LFL, 90), (LBU, 30), (LBL, 80));

	static readonly Pose RocketStrike = new Pose(Vector2.Zero,
		(Torso, 0), (Head, -8),
		(AFU, 12), (AFL, -6), (ABU, -12), (ABL, -6),
		(LFU, -3), (LFL, 0), (LBU, 3), (LBL, 0));

	// --- Rolling along the floor -------------------------------------------------------------

	/// <summary>The start of a roll, curling up as it gets going.</summary>
	const float RollCurl = 0.18f;

	/// <summary>Where a roll, flat out since it curled up, starts to slow.</summary>
	const float RollCoast = 0.45f;

	/// <summary>
	/// How fast a dodge roll is going, as a fraction of its top speed, <paramref name="t"/> of the
	/// way through it: getting going while he curls up, flat out, then easing to a stop just as it
	/// ends - quick over its ground, and it does not skid on after it has uncurled (Eric,
	/// 2026-10-10: it was slow).
	/// </summary>
	public static float RollSpeed(float t)
	{
		if (t < RollCurl) return Mathf.Max(0.0f, t) / RollCurl;
		if (t <= RollCoast) return 1.0f;
		float u = Mathf.Clamp((t - RollCoast) / (1.0f - RollCoast), 0.0f, 1.0f);
		return 1.0f - u * u * (3.0f - 2.0f * u);
	}

	/// <summary>
	/// How far round a dodge roll has turned, 0 to 1, <paramref name="t"/> of the way through it:
	/// in step with the ground it has covered at <see cref="RollSpeed"/>, so the ball turns
	/// exactly as fast as it travels - rolling, not skidding - and comes upright as it stops.
	/// </summary>
	public static float RollTurned(float t)
	{
		static float Covered(float x)
		{
			if (x < RollCurl) return x * x / (2.0f * RollCurl);
			if (x <= RollCoast) return RollCurl * 0.5f + (x - RollCurl);
			float u = Mathf.Clamp((x - RollCoast) / (1.0f - RollCoast), 0.0f, 1.0f);
			return RollCurl * 0.5f + (RollCoast - RollCurl) + (1.0f - RollCoast) * (u - u * u * u + 0.5f * u * u * u * u);
		}
		return Covered(Mathf.Clamp(t, 0.0f, 1.0f)) / Covered(1.0f);
	}

	/// <summary>
	/// Curled up in a ball for a dodge roll: back rounded, chin tucked to the chest, knees pulled
	/// up to it and heels to the seat, both arms wrapped round the shins - the way anyone rolls,
	/// and round enough to roll (Eric, 2026-10-10: the fists were up in a guard, sticking out).
	/// </summary>
	public static readonly Pose Tuck = new Pose(new Vector2(0, 30),
		(Torso, -28), (Head, -18),
		(AFU, -46), (AFL, 30), (ABU, -54), (ABL, 34),
		(LFU, -88), (LFL, 120), (LBU, -80), (LBL, 120));

	// Holding it up to his chest, then a bow from the waist to put it down on the floor in
	// front - the legs barely bend, so both feet stay planted.
	static readonly Pose SetDownWindup = new Pose(new Vector2(0, 4),
		(Torso, 8), (Head, -6),
		(AFU, -70), (AFL, -60), (ABU, -60), (ABL, -60),
		(LFU, -10), (LFL, 12), (LBU, 10), (LBL, 12));

	static readonly Pose SetDownStrike = new Pose(new Vector2(4, 16),
		(Torso, -40), (Head, 18),
		(AFU, -44), (AFL, -8), (ABU, -36), (ABL, -8),
		(LFU, -20), (LFL, 24), (LBU, 16), (LBL, 20));

	// Reared back, front tipped up; then the hips drive forward and the body levels out behind
	// its point, arms swept back out of the way.
	static readonly Pose PointThrustWindup = new Pose(new Vector2(-8, 6),
		(Torso, 20), (Head, -8),
		(AFU, 30), (AFL, 20), (ABU, -20), (ABL, -10),
		(LFU, -20), (LFL, 30), (LBU, 20), (LBL, 20));

	static readonly Pose PointThrustStrike = new Pose(new Vector2(18, 2),
		(Torso, 4), (Head, -2),
		(AFU, 40), (AFL, 10), (ABU, 40), (ABL, 10),
		(LFU, -46), (LFL, 20), (LBU, 46), (LBL, 30));

	// --- Swords -----------------------------------------------------------------
	// A held weapon continues the forearm, so what a sword pose has to get right is the sum of
	// the torso and both arm joints, as played: the torso at 1.1 times as written, the arm at 1.25
	// times (a little more for a wilder fighter), and the strike overshot past its windup. A fist
	// can finish 40 degrees up and still land in front of the chest; a blade that far up is over
	// everyone's head. These are written to finish with the blade where the hit is.

	// Over the shoulder, then over and down to level.
	static readonly Pose SlashWindup = new Pose(new Vector2(-3, 2),
		(Torso, 12), (Head, -6),
		(AFU, -150), (AFL, -40), (ABU, -22), (ABL, -16),
		(LFU, -10), (LFL, 16), (LBU, 12), (LBL, 8));

	static readonly Pose SlashStrike = new Pose(new Vector2(6, 0),
		(Torso, -14), (Head, 8),
		(AFU, -84), (AFL, -20), (ABU, 34), (ABL, 24),
		(LFU, -26), (LFL, 10), (LBU, 20), (LBL, 14));

	// Low behind the hip, then up and out to level.
	static readonly Pose RisingCutWindup = new Pose(new Vector2(-2, 4),
		(Torso, 8), (Head, -4),
		(AFU, 50), (AFL, -10), (ABU, -30), (ABL, -30),
		(LFU, -12), (LFL, 14), (LBU, 14), (LBL, 8));

	static readonly Pose RisingCutStrike = new Pose(new Vector2(6, 0),
		(Torso, -16), (Head, 8),
		(AFU, -50), (AFL, -14), (ABU, 40), (ABL, 20),
		(LFU, -24), (LFL, 10), (LBU, 22), (LBL, 16));

	// Shoulder plus elbow -85 in both, torso -20 in both: level the whole way.
	static readonly Pose ThrustWindup = new Pose(new Vector2(-6, 4),
		(Torso, -20), (Head, 10),
		(AFU, 20), (AFL, -105), (ABU, 30), (ABL, 20),
		(LFU, -20), (LFL, 24), (LBU, 24), (LBL, 12));

	static readonly Pose ThrustStrike = new Pose(new Vector2(12, 2),
		(Torso, -20), (Head, 10),
		(AFU, -66), (AFL, -19), (ABU, 50), (ABL, 20),
		(LFU, -40), (LFL, 12), (LBU, 40), (LBL, 20));

	// Both hands on the hilt, raised high behind the head; then over and down, leaning into it.
	static readonly Pose ChopWindup = new Pose(new Vector2(-8, -12),
		(Torso, 10), (Head, -12),
		(AFU, -170), (AFL, -10), (ABU, -164), (ABL, -10),
		(LFU, -34), (LFL, 24), (LBU, 34), (LBL, 22));

	static readonly Pose ChopStrike = new Pose(new Vector2(10, 16),
		(Torso, -24), (Head, 14),
		(AFU, -102), (AFL, -13), (ABU, -96), (ABL, -13),
		(LFU, -46), (LFL, 50), (LBU, 40), (LBL, 50));

	static readonly Pose AirSlashWindup = new Pose(new Vector2(-2, -2),
		(Torso, 12), (Head, -8),
		(AFU, -150), (AFL, -40), (ABU, 40), (ABL, 30),
		(LFU, -40), (LFL, 70), (LBU, 20), (LBL, 60));

	static readonly Pose AirSlashStrike = new Pose(new Vector2(6, 4),
		(Torso, -14), (Head, 10),
		(AFU, -84), (AFL, -20), (ABU, 50), (ABL, 20),
		(LFU, -50), (LFL, 80), (LBU, 10), (LBL, 70));

	static readonly Pose AirChopWindup = new Pose(new Vector2(-2, -2),
		(Torso, 10), (Head, -8),
		(AFU, -170), (AFL, -10), (ABU, -160), (ABL, -10),
		(LFU, -40), (LFL, 70), (LBU, 20), (LBL, 60));

	static readonly Pose AirChopStrike = new Pose(new Vector2(6, 4),
		(Torso, -24), (Head, 14),
		(AFU, -72), (AFL, -6), (ABU, -66), (ABL, -6),
		(LFU, -50), (LFL, 80), (LBU, 10), (LBL, 70));

	// From in front, round to straight back, leaning back into it.
	static readonly Pose BackCutWindup = new Pose(new Vector2(2, 0),
		(Torso, -16), (Head, 8),
		(AFU, -70), (AFL, -30), (ABU, 20), (ABL, 20),
		(LFU, -50), (LFL, 90), (LBU, -30), (LBL, 100));

	static readonly Pose BackCutStrike = new Pose(new Vector2(-8, 0),
		(Torso, 10), (Head, -8),
		(AFU, 56), (AFL, -7), (ABU, 40), (ABL, 10),
		(LFU, -40), (LFL, 60), (LBU, 30), (LBL, 60));

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
			case AttackAnim.Stomp: return (StompWindup, StompStrike);
			case AttackAnim.Rocket: return (RocketWindup, RocketStrike);
			case AttackAnim.SetDown: return (SetDownWindup, SetDownStrike);
			case AttackAnim.PointThrust: return (PointThrustWindup, PointThrustStrike);
			case AttackAnim.SwanDive: return (SwanDiveWindup, SwanDiveStrike);
			case AttackAnim.Jackhammer: return (JackhammerWindup, JackhammerStrike);
			case AttackAnim.ChestOut: return (ChestOutWindup, ChestOutStrike);
			case AttackAnim.PushBarrow: return (PushBarrowWindup, PushBarrowStrike);
			case AttackAnim.Slash: return (SlashWindup, SlashStrike);
			case AttackAnim.RisingCut: return (RisingCutWindup, RisingCutStrike);
			case AttackAnim.Thrust: return (ThrustWindup, ThrustStrike);
			case AttackAnim.Chop: return (ChopWindup, ChopStrike);
			case AttackAnim.AirSlash: return (AirSlashWindup, AirSlashStrike);
			case AttackAnim.AirChop: return (AirChopWindup, AirChopStrike);
			case AttackAnim.BackCut: return (BackCutWindup, BackCutStrike);
			default: return (AttackWindup, AttackStrike);
		}
	}

	/// <summary>How far through the startup the windup is reached; the rest is anticipation.</summary>
	const float WindupReached = 0.55f;

	/// <summary>How much further than its windup an attack coils before it lets go.</summary>
	const float Anticipation = 0.3f;

	/// <summary>How far past its strike pose an attack swings, and holds, before recovering.</summary>
	const float FollowThrough = 0.12f;

	/// <summary>The most degrees a coil or a follow-through carries any limb past its pose.</summary>
	const float MaxCoil = 16.0f;

	/// <summary>How much of the endlag the follow-through spends carrying on past the strike, slowing.</summary>
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
				// Past the windup and still going: the coil - the arm drawn back a little further
				// than the windup pose itself, up to a limit, as a body winds up.
				float a = (t - WindupReached) / (1.0f - WindupReached);
				Pose.Overshoot(AttackRecover, windup, anticipation * a, MaxCoil, into);
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

		// After the hit the limb carries on a little past the strike, slowing, and is drawn back
		// while it is still going - never frozen out at full stretch, which is how a puppet holds a
		// pose (Eric, 2026-10-10). The recovery starts gently and finishes gently.
		float endlag = Mathf.Max(1, move.EndlagFrames);
		float e = Mathf.Clamp((moveFrame - activeEnd) / endlag, 0.0f, 1.0f);
		float carry = Mathf.Sin(Mathf.Min(1.0f, e / FollowThroughHold) * Mathf.Pi * 0.5f);
		Pose.Blend(windup, strike, 1.0f + followThrough, into);
		Pose.Overshoot(windup, into, 0.5f * followThrough * carry, MaxCoil, into);
		float back = Mathf.Clamp((e - FollowThroughHold * 0.5f) / (1.0f - FollowThroughHold * 0.5f), 0.0f, 1.0f);
		if (back > 0.0f) Pose.Blend(into, AttackRecover, back * back * (3.0f - 2.0f * back), into);
	}

	// --- Standing to hit ---------------------------------------------------------

	/// <summary>The standard attacks: the jab and the three tilts, and whatever follows them in a combo.</summary>
	public static bool IsStandard(MoveSlot slot) =>
		slot == MoveSlot.Jab || slot == MoveSlot.ForwardTilt || slot == MoveSlot.UpTilt || slot == MoveSlot.DownTilt;

	/// <summary>
	/// Whether an animation hits with a leg - a kick, a flip, the splits, a stomp. Those lift the
	/// kicking leg off the floor; every other attack keeps both feet down. A sweep or a stab along
	/// the floor only crouches, and stays on its feet.
	/// </summary>
	public static bool UsesLegs(AttackAnim anim)
	{
		switch (anim)
		{
			case AttackAnim.FrontKick:
			case AttackAnim.LowKick:
			case AttackAnim.FlyingKick:
			case AttackAnim.Split:
			case AttackAnim.Stomp:
			case AttackAnim.Nair:
			case AttackAnim.Fair:
			case AttackAnim.Bair:
			case AttackAnim.Uair:
			case AttackAnim.Dair:
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// A standard attack done with the arms keeps both feet planted where he stands: the legs are
	/// solved under him as they are standing still (FighterRig.StandOnFeet), the knees bending as
	/// the hips drop or shift into the hit, instead of being swung about by the attack pose (Eric,
	/// 2026-10-10: feet should stay on the ground). A kick still lifts its kicking leg; the other
	/// foot stays down.
	/// </summary>
	public static bool StandsThrough(MoveData move, MoveSlot slot) =>
		IsStandard(slot) && !UsesLegs(move.Anim) && move.Special != SpecialKind.Dash && !move.RunningLegs;

	/// <summary>
	/// A kick on the ground stands on its back leg: a pose that folds that leg right up - a flip
	/// kick borrowed from the air - gets a standing leg instead, so the kicker never hops off the
	/// floor (Eric, 2026-10-10). The kicking leg is left alone.
	/// </summary>
	public static void KeepSupportLeg(Pose pose)
	{
		if (pose[LBL] <= 70.0f) return;
		pose.Set(LBU, 10.0f);
		pose.Set(LBL, 6.0f);
	}

	/// <summary>
	/// What a standard attack on the ground changes in its sampled pose: a kick keeps a standing
	/// leg. Null if nothing - an attack done with the arms is stood through instead (StandsThrough).
	/// </summary>
	public static System.Action<Pose> GroundAdjust(MoveData move, MoveSlot slot) =>
		IsStandard(slot) && UsesLegs(move.Anim) ? KeepSupportLeg : null;

	/// <summary>Which arms throw a punch: the front, the back, or both (a two-handed shove).</summary>
	static (bool front, bool back) PunchingArms(AttackAnim anim)
	{
		switch (anim)
		{
			case AttackAnim.Punch:
			case AttackAnim.HeavyPunch:
			case AttackAnim.Lunge:
				return (true, false);
			case AttackAnim.PunchBack:
				return (false, true);
			case AttackAnim.PalmThrust:
				return (true, true);
			default:
				return (false, false);
		}
	}

	/// <summary>
	/// How far out a punch is at this frame, from the guard (0) to the hit (1): drawn back a little
	/// from the guard to load it (below 0), then driven out, fast and faster, to land on the first
	/// active frame, held a beat, and brought back to the guard along the same line.
	/// </summary>
	static float PunchExtension(MoveData move, float frame)
	{
		float startup = Mathf.Max(1, move.StartupFrames);
		float activeEnd = move.StartupFrames + move.ActiveFrames;
		if (frame <= startup)
		{
			float t = frame / startup;
			if (t < 0.6f)
			{
				float u = t / 0.6f;
				return -0.3f * u * u * (3.0f - 2.0f * u);
			}
			float v = (t - 0.6f) / 0.4f;
			return Mathf.Lerp(-0.3f, 0.9f, v * v);
		}
		if (frame <= activeEnd) return 1.0f;
		float r = (frame - activeEnd) / Mathf.Max(1, move.EndlagFrames);
		if (r < 0.15f) return 1.0f;
		float w = Mathf.Clamp((r - 0.15f) / 0.85f, 0.0f, 1.0f);
		return 1.0f - w * w * (3.0f - 2.0f * w);
	}

	/// <summary>
	/// A standard punch thrown like a boxer's (Eric, 2026-10-10: arms should extend outward, not
	/// straighten anyhow). The fist travels in a straight line from a guard in front of the
	/// shoulder out toward the hit - <paramref name="hit"/>, the hitbox's centre - as far as the
	/// arm nearly straight, but never past the far edge of the hit (<paramref name="hitRadius"/>),
	/// so a short jab still extends and still lands inside its hit; the shoulder and elbow are
	/// solved to carry it there
	/// (FighterRig.ReachArm), and the other hand held up at the guard. A hand holding a tool keeps
	/// its swing: the tool's head is what has to land on the hit. Laid over the attack pose, and
	/// eased in at the start and out at the end, so it hands back to the pose without a pop.
	/// </summary>
	public static void ThrowPunch(FighterRig rig, MoveData move, float frame, Vector2 hit, float hitRadius, int facing, bool frontHandEmpty)
	{
		(bool front, bool back) = PunchingArms(move.Anim);
		if (!front && !back) return;
		if (front && !frontHandEmpty) return;

		float startup = Mathf.Max(1, move.StartupFrames);
		float total = Mathf.Max(1, move.TotalFrames);
		float into = Mathf.Clamp(frame / (startup * 0.35f), 0.0f, 1.0f);
		float outOf = Mathf.Clamp((total - frame) / Mathf.Max(1.0f, move.EndlagFrames * 0.3f), 0.0f, 1.0f);
		float amount = Mathf.Min(into, outOf);
		if (amount <= 0.0f) return;
		float e = PunchExtension(move, frame);

		foreach ((RigBone upper, RigBone lower, bool punching, Vector2 guardAt) in new[]
		{
			(RigBone.ArmFrontUpper, RigBone.ArmFrontLower, front, new Vector2(0.34f, -0.2f)),
			(RigBone.ArmBackUpper, RigBone.ArmBackLower, back, new Vector2(0.38f, -0.22f)),
		})
		{
			Vector2? shoulder = rig.JointGlobal(upper);
			float length = rig.ArmLength(upper, lower);
			if (shoulder == null || length <= 0.0f) continue;
			Vector2 guard = shoulder.Value + new Vector2(guardAt.X * facing, guardAt.Y) * length;
			if (!punching)
			{
				rig.ReachArm(upper, lower, guard, amount);
				continue;
			}
			Vector2 toward = (hit - guard).Normalized();
			float reach = Mathf.Min(Mathf.Max((hit - guard).Length(), length * 0.92f), (hit - guard).Length() + hitRadius * 0.8f);
			Vector2 fist = e >= 0.0f
				? guard + toward * reach * e
				: guard - toward * length * -e * 0.6f;
			rig.ReachArm(upper, lower, fist, amount);
		}
	}

	/// <summary>
	/// Shows a move at a frame on a rig, exactly as a match does: sampled against the move's own
	/// frame counts, so the strike pose arrives on the frame the hitbox does - or, for a robot,
	/// snapped between its key poses and held (<see cref="RobotAttackKey"/>). The parade's film
	/// strip uses this too, so what it shows is what a fight shows. <paramref name="adjust"/>
	/// changes the sampled pose before it is shown - legs running under a wheelbarrow.
	/// </summary>
	public static void ShowAttack(FighterRig rig, MoveData move, float frame, float drama, Pose scratch,
		System.Action<Pose> adjust = null)
	{
		if (rig.Robotic)
		{
			(float keyFrame, int key) = RobotAttackKey(move, (int)frame);
			SampleAttack(move, keyFrame, scratch, drama: drama);
			rig.RobotApply(scratch, move, key, RobotAttackSnap);
			return;
		}
		SampleAttack(move, frame, scratch, drama: drama);
		adjust?.Invoke(scratch);
		rig.ApplyDirect(scratch, AttackBlend(move, frame));
	}

	/// <summary>
	/// Which key pose of the move a robot is on at this frame, and the move frame that shows it:
	/// the coil, the strike, the recovery. He snaps between them and holds each - see
	/// FighterRig.Robotic.
	/// </summary>
	public static (float frame, int key) RobotAttackKey(MoveData move, int frame)
	{
		int startup = move.StartupFrames;
		int activeEnd = startup + move.ActiveFrames;
		// The coil: snapped to at once and held.
		if (frame <= startup - RobotAttackSnap) return (startup, 0);
		// A move that turns through its active frames (a windmill) clicks round in steps.
		bool turning = move.Anim == AttackAnim.Windmill || move.Anim == AttackAnim.WideArc;
		if (turning && frame > startup && frame <= activeEnd)
		{
			int step = (frame - startup - 1) / 3;
			return (Mathf.Min(activeEnd, startup + 1 + (step + 1) * 3), 2 + step);
		}
		// The strike, fully out by the first active frame, held through the follow-through.
		int held = activeEnd + Mathf.CeilToInt(move.EndlagFrames * FollowThroughHold);
		if (frame <= held) return (startup + 1, 1);
		// And back, in one snap.
		return (move.TotalFrames, 1000);
	}

	/// <summary>Frames a robot's attack takes to snap from one key pose to the next.</summary>
	const int RobotAttackSnap = 2;

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
			return t < WindupReached ? 1.0f : 1.0f - 0.03f * (t - WindupReached) / (1.0f - WindupReached);
		}
		// A jackhammer judders the whole time it runs.
		if (moveFrame <= activeEnd && move.Anim == AttackAnim.Jackhammer) return 1.0f + 0.05f * Mathf.Sin(moveFrame * 2.6f);
		if (moveFrame <= activeEnd) return 1.03f;

		float e = Mathf.Clamp((moveFrame - activeEnd) / Mathf.Max(1, move.EndlagFrames), 0.0f, 1.0f);
		return Mathf.Lerp(1.03f, 1.0f, Mathf.Min(1.0f, e * 2.0f));
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
