using System.Collections.Generic;
using Godot;
using RigArt = FighterRig.PoseArt;

public enum FighterState
{
	Grounded,
	Airborne,
	Attacking,
	Dodging,
	LedgeHang,
	Hitstun,

	/// <summary>Knocked over and rolling, for a fighter with TumblesWhenHit. No control.</summary>
	Tumbling,

	Respawning,
	Eliminated,
}

/// <summary>
/// A single fighter. At M1 this draws itself as a coloured rectangle; at M2 the rectangle is
/// replaced by a rigged cutout of a real drawing and nothing in this file should need to care.
///
/// All timing here is in integer frames at the fixed 60 Hz physics tick - see Tuning.cs.
/// </summary>
public partial class Fighter : CharacterBody2D
{
	public FighterData Data { get; private set; }
	public IInputSource Controller { get; set; }
	public MatchManager Match { get; set; }
	public int PlayerIndex { get; private set; }

	public float Percent { get; private set; }
	public int Stocks { get; set; }
	public FighterState State { get; private set; } = FighterState.Airborne;
	public int Facing { get; private set; } = 1;
	public bool IsBlocking { get; private set; }

	// --- Frame counters ------------------------------------------------------

	int hitlagFrames;
	int hitstunFrames;
	int invulnFrames;
	int blockReleaseLagFrames;

	int coyoteFrames;
	int jumpBufferFrames;
	int attackBufferFrames;

	int airJumpsUsed;

	/// <summary>
	/// The up special can be used once per trip into the air. Landing, or catching a ledge,
	/// gives it back. Without this a recovery move chained into itself flies anywhere forever.
	/// </summary>
	bool upSpecialUsed;

	/// <summary>
	/// After an up special, falling with nothing left: no attacks, no specials, no jump, no dodge -
	/// only drifting left and right - until he lands, catches a ledge or is hit. The recovery is
	/// the last thing a fighter gets on the way home. Eric's call, 2026-10-04.
	/// </summary>
	bool helpless;
	public bool IsHelpless => helpless;

	/// <summary>A platform-building down special, like the up special, is once per trip into the air.</summary>
	bool buildUsed;

	/// <summary>The platform this fighter last built, so building another replaces it.</summary>
	BuiltPlatform builtPlatform;

	/// <summary>
	/// Standing on a platform you built is standing, but it is not the ground: it gives back no
	/// jumps and no specials. Only real ground or a ledge does.
	/// </summary>
	bool standingOnBuilt;

	// --- Active move ---------------------------------------------------------

	MoveData currentMove;
	MoveSlot currentSlot;
	int moveFrame;
	int specialBufferFrames;
	int dodgeStartFrames;
	bool hazardSpawned;
	int burstFired;
	readonly HashSet<Fighter> alreadyHitThisMove = new HashSet<Fighter>();

	// --- Misc ----------------------------------------------------------------

	/// <summary>Sideways drag with no input. Low on purpose: launches must still carry.</summary>
	const float AirDrag = 320.0f;

	Vector2 lastStick;
	int respawnFreezeFrames;
	Vector2 respawnPoint;

	FighterRig rig;
	readonly Pose attackPose = new Pose();
	int landFrames;

	/// <summary>
	/// Holding down on the ground. Stops walking, shrinks the hurtbox so high attacks pass over,
	/// and attacking from it is the down tilt - a sweep.
	/// </summary>
	public bool IsCrouching { get; private set; }

	/// <summary>
	/// How much of the standing height is left to hit while crouched. Just over half: a jab or a
	/// tilt aimed at the chest goes over the top of someone ducking.
	/// </summary>
	const float CrouchHeight = 0.55f;

	const int JumpStretchFrames = 10;
	int jumpStretchFrames;
	float squashAmount = 1.0f;

	float TargetSquash()
	{
		if (State == FighterState.Attacking && currentMove != null)
		{
			return FighterAnimations.AttackSquash(currentMove, moveFrame);
		}
		if (jumpStretchFrames > 0) return 1.0f + 0.16f * jumpStretchFrames / JumpStretchFrames;
		if (State == FighterState.Grounded && landFrames > 0) return 1.0f - 0.2f * landFrames / 9.0f;
		if (IsBlocking) return 0.94f;
		if (State == FighterState.Grounded && IsCrouching) return 0.94f;
		return 1.0f;
	}

	/// <summary>
	/// Frames left ignoring soft platforms after a deliberate drop-through. Long enough to
	/// clear the thickest platform at fall speed, short enough that the next one still catches.
	/// </summary>
	int dropThroughFrames;

	/// <summary>Whether the surface underfoot is a soft platform, so it can be dropped through.</summary>
	bool standingOnOneWay;

	/// <summary>Stick deflection below this reads as centred. Above it, it is a speed dial.</summary>
	const float MoveDeadzone = 0.08f;

	/// <summary>Fraction of top speed that counts as running, for dash-attack purposes.</summary>
	const float DashThreshold = 0.55f;

	// --- Dodging ---------------------------------------------------------------

	int dodgeFrames;
	Vector2 dodgeVelocity;

	const int RollFrames = 26;
	const int SpotDodgeFrames = 22;
	const int AirDodgeFrames = 28;

	/// <summary>Frames of invulnerability inside a dodge, starting a few frames in.</summary>
	const int DodgeInvulnStart = 4;
	const int DodgeInvulnEnd = 17;

	// --- Ledges ----------------------------------------------------------------

	Vector2 heldLedge;
	int ledgeCooldownFrames;

	/// <summary>How close a falling fighter has to be to a corner to catch it.</summary>
	const float LedgeSnapRadius = 76.0f;

	/// <summary>Frames after letting go before the same fighter can grab again.</summary>
	const int LedgeRegrabCooldown = 26;

	const int LedgeGrabInvulnFrames = 24;

	// --- Smash attacks and combos -----------------------------------------------

	/// <summary>
	/// A smash is the stick FLICKED and attack pressed together, as in Smash Bros. Pushing the
	/// stick first and pressing attack a moment later is a tilt. The difference is only how long
	/// ago the stick went from centre to full - this many frames or fewer is a flick. It was 4,
	/// and up smashes kept coming out as up tilts (Eric, 2026-10-04).
	/// </summary>
	const int SmashFlickFrames = 7;

	/// <summary>
	/// How many frames a flick may take to travel from the centre to the edge. A real thumb takes
	/// two or three; requiring it in one frame turned most flicks into tilts.
	/// </summary>
	const int FlickTravelFrames = 4;
	int centreFramesX;
	int centreFramesY;

	/// <summary>How far the windup is from the hit when a smash holds to charge.</summary>
	const int ChargeHoldBeforeHit = 3;

	/// <summary>A full second of charge, for up to 40% more damage.</summary>
	const int MaxChargeFrames = 60;
	const float MaxChargeBonus = 0.4f;

	int flickFramesX = 99;
	int flickFramesY = 99;
	Vector2 previousStick;
	int chargeFrames;
	bool comboQueued;

	float ChargeScale => 1.0f + ((currentMove?.ChargeDamage ?? 1.0f + MaxChargeBonus) - 1.0f) * chargeFrames / MaxChargeFrames;

	bool IsCharging =>
		State == FighterState.Attacking && currentMove != null && currentMove.Chargeable
		&& moveFrame == Mathf.Max(1, currentMove.StartupFrames - ChargeHoldBeforeHit);

	// --- Size, for a fighter with a Resize special -------------------------------

	/// <summary>The hurtbox as it is right now. Differs from Data.BodySize only while resized.</summary>
	Vector2 bodySize;
	ConvexPolygonShape2D bodyShape;

	/// <summary>
	/// How wide his feet stand, which is how wide the bottom of his collision shape is - see
	/// <see cref="ShapeBody"/>.
	/// </summary>
	float footWidth;
	int sizeLevel = SizeLevels.Normal;
	int resizeStickDir;
	readonly Dictionary<MoveData, MoveData> sizedMoves = new Dictionary<MoveData, MoveData>();

	float RunSpeed => Data.RunSpeed * SizeLevels.Speed(sizeLevel);

	// --- Tumbling, for a fighter with TumblesWhenHit ---------------------------

	/// <summary>
	/// A hit this hard knocks a tumbling fighter over. Below it a jab is just a jab, so being
	/// poked at low percent does not take control away every time.
	/// </summary>
	const float TumbleKnockback = 60.0f;

	/// <summary>How easily a knocked-over fighter keeps rolling. Low, so a roll can carry off the edge.</summary>
	const float TumbleFriction = 900.0f;

	int tumbleFrames;
	int pendingTumbleFrames;
	bool drawnAsBall;
	float rollAngle;

	// --- Grappling hook and bomb -------------------------------------------------

	Vector2 hookPoint;
	bool hookFired;
	bool bombDetonated;

	// --- EdgeLord: blink, grab, spin ----------------------------------------------

	/// <summary>Specials marked OncePerAirtime that have been used since the last landing.</summary>
	readonly HashSet<MoveData> usedThisAirtime = new HashSet<MoveData>();

	/// <summary>Whether a swung drawing with a ReleaseDrop has been let go of this move.</summary>
	bool swingReleased;

	/// <summary>Where a Blink started, for the trail it leaves across the gap.</summary>
	Vector2 blinkFrom;
	bool blinkStarted;

	/// <summary>Who this fighter's tether has caught, and where they were caught.</summary>
	Fighter grabbed;
	Vector2 grabPoint;
	int grabFrame;

	/// <summary>Who is holding this fighter, if anyone. Held, they go where they are put.</summary>
	Fighter heldBy;
	int heldFrames;

	/// <summary>
	/// A held fighter is let go after this long whatever happens, so a bug can never pin someone.
	/// Long enough for the slowest hold there is: DoomBot's reel-in and the kick that follows it.
	/// </summary>
	const int MaxHeldFrames = 60;

	/// <summary>The follow-up a command grab hands over to once its victim is reeled in - DoomBot's kick.</summary>
	MoveData grabKick;

	// --- Heat, burning and rusty joints -------------------------------------------

	/// <summary>0 to <see cref="Heat.Max"/>, for a fighter with <see cref="FighterData.HasHeat"/>.</summary>
	float heat;
	int framesSinceHeat;
	int framesAtMaxHeat;
	int overheatFrames;
	readonly Dictionary<int, MoveData> ventMoves = new Dictionary<int, MoveData>();

	/// <summary>On fire: frames left, percent per tick, and frames to the next tick.</summary>
	int burnFrames;
	float burnTick;
	int burnTickTimer;
	const int BurnTickFrames = 15;

	/// <summary>Whether the move in progress has hit anyone yet - blocked counts - for whiff lag.</summary>
	bool hitSomethingThisMove;

	/// <summary>Extra frames added to the end of this move because it missed (rusty joints).</summary>
	int whiffExtraFrames;

	// --- Air dash, spinning hits, bouncing, sizes ------------------------------------

	/// <summary>Frames left in an air dash, the line it is on, and where it started (see FighterData.AirDashSpeed).</summary>
	int airDashFrames;
	Vector2 airDashVelocity;
	Vector2 airDashFrom;
	int airDashLinger;
	const int AirDashFrames = 14;

	/// <summary>A somersault turned by a hit that spins its victim (MoveData.SpinVictim).</summary>
	int spinVictimFrames;
	int spinVictimTotal;
	int spinVictimDir = 1;

	/// <summary>Frames left of the squash a ball takes bouncing off the floor.</summary>
	int ballSquashFrames;
	const int BallSquashFrames = 8;

	/// <summary>A ball dropped onto the floor faster than this bounces, keeping this much of the speed.</summary>
	const float BounceFrom = 520.0f;
	const float BounceKeep = 0.45f;

	/// <summary>Normal attacks resized for each size, made once and kept - see SizedNormal.</summary>
	readonly Dictionary<(MoveData, int), MoveData> sizedNormals = new Dictionary<(MoveData, int), MoveData>();

	/// <summary>How fast this blink crosses, and whether it is going to one of his traps.</summary>
	Vector2 blinkVelocity;
	bool blinkToTrap;

	/// <summary>A planted blade stands with its middle this far above the middle of whoever planted it.</summary>
	const float BladeStandOffset = 6.0f;

	// --- Posed drawings, slipping, the trampoline -------------------------------------

	/// <summary>
	/// Which of a move's posed drawings (MoveData.PoseArts) it is showing this time, and the dice
	/// that pick it. System.Random, not a Godot object: see CpuInputSource for why.
	/// </summary>
	string chosenPose;
	System.Random poseRng = new System.Random(13);

	/// <summary>Frames left of standing in someone's puddle - while it lasts, his feet barely grip.</summary>
	int slipFrames;

	/// <summary>How much grip is left in a puddle: a fraction of the usual friction and acceleration.</summary>
	const float SlipGrip = 0.15f;

	/// <summary>Where a bouncing recovery's trampoline stands (its mat), and frames left of showing it.</summary>
	Vector2 trampolineAt;
	int trampolineFrames;
	int trampolineShown;
	const int TrampolineLinger = 30;

	float GroundFriction => Data.GroundFriction * (slipFrames > 0 ? SlipGrip : 1.0f);
	float GroundAcceleration => Data.GroundAcceleration * (slipFrames > 0 ? SlipGrip : 1.0f);

	/// <summary>Standing in a puddle this frame - see SpecialKind.Puddle.</summary>
	public void Slip() => slipFrames = 2;

	/// <summary>
	/// Counts up every frame, hitlag included. Effects that flicker - smoke, flames, electricity -
	/// are hashed from it, so they move without ever being random.
	/// </summary>
	int fxFrames;

	/// <summary>The spot on the drawing the last beam came out of, for as long as it stays attached.</summary>
	string activeBeamFrom = "";

	public float HeatFraction => Data.HasHeat ? heat / Heat.Max : 0.0f;
	public bool IsOverheated => overheatFrames > 0;
	public bool IsBurning => burnFrames > 0;

	/// <summary>How far round a Spin has gone, in degrees, and whether it has come back to the front.</summary>
	float spinAngle;
	bool spinDone;

	/// <summary>The angles the turning frames were drawn at. Beyond 180 a frame is shown mirrored.</summary>
	static readonly float[] TurnAngles = { 0.0f, 45.0f, 78.0f, 120.0f, 180.0f };
	static readonly string[] TurnNames = { "turn0", "turn1", "turn2", "turn3", "turn4" };

	/// <summary>Hazards still out for a move with a MaxOut, oldest first.</summary>
	readonly Dictionary<MoveData, List<Hazard>> outHazards = new Dictionary<MoveData, List<Hazard>>();

	/// <summary>
	/// A layer drawn over the puppet. _Draw paints under it, which is right for trails and for a
	/// blade passing behind him - but a blade coming round in front, a menu and a glint must sit
	/// on top.
	/// </summary>
	Node2D overlay;

	/// <summary>Solid ground (layer 1) plus soft platforms (layer 2).</summary>
	const uint GroundMask = 0b11;

	/// <summary>Solid ground only. What a fighter collides with while dropping through.</summary>
	const uint SolidOnlyMask = 0b01;

	public void Configure(FighterData data, IInputSource controller, int playerIndex, MatchManager match)
	{
		Data = data;
		Controller = controller;
		PlayerIndex = playerIndex;
		Match = match;
		Stocks = Tuning.DefaultStocks;
		poseRng = new System.Random(playerIndex * 977 + 13);

		// Fighters collide with the stage (layer 1) but never with each other, which is how
		// platform fighters behave - two players standing on the same tile is normal.
		CollisionLayer = 0;
		CollisionMask = GroundMask;

		bodySize = data.BodySize;
		bodyShape = new ConvexPolygonShape2D();
		AddChild(new CollisionShape2D { Shape = bodyShape });

		if (!string.IsNullOrEmpty(data.RigPath))
		{
			rig = new FighterRig();
			AddChild(rig);
			if (rig.Load(data.RigPath))
			{
				// The puppet is drawn a little taller than the hurtbox so the art reads at full
				// size while the box a player has to hit stays honest.
				rig.Normalise(data.BodySize.Y * 1.18f, data.BodySize.Y * 0.5f, data.VisualScale);
				rig.DramaScale = data.AnimationDrama;
				rig.Robotic = data.Robotic;
			}
			else
			{
				rig.QueueFree();
				rig = null;
			}
		}
		footWidth = rig != null ? rig.StanceWidth() : bodySize.X * 0.5f;
		ShapeBody();

		// Added after the rig, so it draws over it.
		overlay = new Node2D();
		AddChild(overlay);
		overlay.Draw += DrawOverlay;

		// A held smash hums, rising in pitch as it charges. It is the fighter's own voice rather
		// than one from SfxPlayer's pool so it pauses with the match and goes when he does.
		chargeVoice = new AudioStreamPlayer2D
		{
			Bus = SfxPlayer.SfxBus,
			Stream = SfxPlayer.Instance?.Stream("smash_charge"),
			MaxDistance = 100000.0f,
			PanningStrength = 0.5f,
		};
		AddChild(chargeVoice);
	}

	// --- Sound -----------------------------------------------------------------

	/// <summary>The sound the current move makes, and on which of its frames.</summary>
	SfxCatalog.Cue moveCue;
	AudioStreamPlayer2D chargeVoice;

	/// <summary>The frame a held smash freezes on while it charges.</summary>
	static int ChargeFreezeFrame(MoveData move) => Mathf.Max(1, move.StartupFrames - ChargeHoldBeforeHit);

	void CueMoveSound(MoveData move)
	{
		moveCue = move == Data.Taunt
			? new SfxCatalog.Cue("taunt", 1)
			: SfxCatalog.ForMove(move, ChargeFreezeFrame(move));
	}

	void UpdateChargeVoice()
	{
		if (chargeVoice == null || chargeVoice.Stream == null) return;
		if (IsCharging && chargeFrames > 0)
		{
			if (!chargeVoice.Playing) chargeVoice.Play();
			chargeVoice.PitchScale = 1.0f + 0.6f * chargeFrames / MaxChargeFrames;
		}
		else if (chargeVoice.Playing)
		{
			chargeVoice.Stop();
		}
	}

	void Redraw()
	{
		QueueRedraw();
		overlay?.QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		UpdateChargeVoice();
		fxFrames++;

		// Hitlag freezes this fighter completely - no movement, no state advance, no timers.
		// Everything else in the world keeps running. This is the single biggest contributor
		// to a hit feeling like it has weight; see .ai/fighting-design.md.
		if (hitlagFrames > 0)
		{
			hitlagFrames--;
			UpdateRigTint();
			Redraw();
			return;
		}

		if (State == FighterState.Eliminated)
		{
			// Out of the match, but still a player: Start still pauses, or moves on at the end.
			if (Controller?.Poll().StartPressed == true) Match?.OnStartPressed();
			return;
		}

		TickBurn();

		// Caught on someone's tether: no control and no physics - the holder puts them where
		// they go, until the throw.
		if (heldBy != null)
		{
			TickHeld();
			UpdateRig();
			UpdateRigTint();
			Redraw();
			return;
		}

		InputState input = Controller?.Poll() ?? InputState.None;
		if (input.StartPressed && Match != null)
		{
			Match.OnStartPressed();
			return;
		}

		// During the 3-2-1 the controller is still read, so button edges stay honest, but
		// nothing it says is acted on.
		if (Match != null && Match.InputLocked) input = InputState.None;

		// Overheated: steam pouring off him, and nothing the controller says does anything.
		if (overheatFrames > 0) input = InputState.None;
		lastStick = input.Move;

		TickTimers(input);
		TickHeat();

		switch (State)
		{
			case FighterState.Grounded: TickGrounded(input, dt); break;
			case FighterState.Airborne: TickAirborne(input, dt); break;
			case FighterState.Attacking: TickAttacking(input, dt); break;
			case FighterState.Dodging: TickDodging(dt); break;
			case FighterState.LedgeHang: TickLedgeHang(input); break;
			case FighterState.Hitstun: TickHitstun(dt); break;
			case FighterState.Tumbling: TickTumbling(input, dt); break;
			case FighterState.Respawning: TickRespawning(); break;
		}

		if (State != FighterState.Respawning && State != FighterState.LedgeHang)
		{
			ApplyGravity(dt);
			StopAtLedge(dt);
			fallSpeedBeforeMove = Velocity.Y;
			bool wasOnFloor = IsOnFloor();
			MoveAndSlide();
			GroundContact(wasOnFloor);
			Bounce();
			StayInsideBlastZone();
		}

		if (landFrames > 0) landFrames--;
		if (jumpStretchFrames > 0) jumpStretchFrames--;

		if (dropThroughFrames > 0 && --dropThroughFrames == 0)
		{
			CollisionMask = GroundMask;
		}

		UpdateGroundInfo();
		UpdateRig();
		UpdateRigTint();
		Redraw();
	}

	// --- Ground ---------------------------------------------------------------

	/// <summary>How fast he was falling going into this frame's move, for the size of a landing.</summary>
	float fallSpeedBeforeMove;
	int stepFrames;
	int lastStompStep = -1;
	int lastRunDirection;

	/// <summary>
	/// The floor answering back. A landing kicks up dust both ways, more the harder he came down;
	/// running kicks a little back off each footfall; turning hard at speed skids a cloud out in
	/// front. Small things, but they are what make a fighter look like he is standing on the
	/// stage rather than in front of it.
	/// </summary>
	void GroundContact(bool wasOnFloor)
	{
		if (Match == null || State == FighterState.Respawning) return;
		bool onFloor = IsOnFloor();
		Vector2 feet = GlobalPosition + new Vector2(0.0f, bodySize.Y * 0.5f);
		float size = bodySize.Y / 128.0f;

		if (onFloor && !wasOnFloor && fallSpeedBeforeMove > 400.0f)
		{
			Match.Dust(feet, size * Mathf.Clamp(fallSpeedBeforeMove / 900.0f, 0.6f, 2.2f), 0.0f);
			stepFrames = 0;
			return;
		}
		if (!onFloor) return;

		float speed = Mathf.Abs(Velocity.X);
		int direction = Velocity.X > 0.0f ? 1 : -1;
		if (speed > RunSpeed * 0.55f && State == FighterState.Grounded)
		{
			// A skid: running one way, and now the stick has turned him round.
			if (lastRunDirection != 0 && Facing != direction && lastRunDirection == direction)
			{
				Match.Dust(feet + new Vector2(direction * bodySize.X * 0.4f, 0.0f), size * 1.3f, direction);
				stepFrames = 0;
			}
			// A robot's footfalls land with the walk: a stomp each time a foot comes down, a
			// puff of dust both ways and a sound. Eric's call, 2026-10-04.
			else if (Data.Robotic && rig != null && rig.CurrentClip != null)
			{
				float half = rig.CurrentClip.LengthFrames * 0.5f;
				int step = Mathf.FloorToInt(rig.ShownClipFrame / half);
				if (step != lastStompStep)
				{
					lastStompStep = step;
					// No camera jolt: on every step, it shook the whole screen (Eric, 2026-10-09).
					Match.Dust(feet, size * 0.9f, 0.0f);
					SfxPlayer.At("stomp", feet, 0.06f);
				}
			}
			// A footfall every half run cycle, kicked back the way he came.
			else if (++stepFrames >= 12)
			{
				stepFrames = 0;
				var sole = rig?.Sole(RigBone.LegFrontLower);
				Vector2 at = sole.HasValue ? new Vector2(sole.Value.sole.X, feet.Y) : feet;
				Match.Dust(at, size * 0.6f, -direction);
			}
			lastRunDirection = direction;
		}
		else
		{
			lastRunDirection = 0;
		}
	}

	/// <summary>
	/// Works out whether the floor underfoot is a soft platform. Read from the slide
	/// collisions rather than a raycast, because those are already computed by MoveAndSlide
	/// and they describe the surface actually being stood on.
	/// </summary>
	void UpdateGroundInfo()
	{
		standingOnOneWay = false;
		standingOnBuilt = false;

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D collision = GetSlideCollision(i);

			// Floor normals point up, which is negative Y on screen.
			if (collision.GetNormal().Y > -0.7f) continue;

			if (collision.GetCollider() is BuiltPlatform) standingOnBuilt = true;

			if (collision.GetCollider() is CollisionObject2D body && (body.CollisionLayer & 2) != 0)
			{
				standingOnOneWay = true;
				return;
			}
		}
	}

	/// <summary>
	/// Holding down and pressing jump falls through a soft platform. Only soft platforms are
	/// masked off, so the same input on solid ground does nothing rather than dropping the
	/// fighter out of the stage.
	/// </summary>
	bool TryDropThrough(InputState input)
	{
		if (jumpBufferFrames <= 0 || input.Move.Y < 0.5f || !standingOnOneWay) return false;

		jumpBufferFrames = 0;
		dropThroughFrames = 12;
		CollisionMask = SolidOnlyMask;

		// A small nudge downward, so the fighter is clear of the platform on the same frame
		// rather than resting on it until gravity builds up.
		GlobalPosition += new Vector2(0.0f, 4.0f);
		Velocity = new Vector2(Velocity.X, Mathf.Max(Velocity.Y, 240.0f));
		State = FighterState.Airborne;
		return true;
	}

	// --- Rig -----------------------------------------------------------------

	/// <summary>
	/// Picks an animation from the shared library based on what the fighter is doing. The rig
	/// is told what to play; it is never told anything about fighting.
	/// </summary>
	void UpdateRig()
	{
		if (rig == null || !rig.Loaded) return;

		rig.SetFacing(Facing);

		// On the ground, the lowest foot is always on the floor, whatever the pose is doing.
		rig.SetPlanted(IsOnFloor() && State != FighterState.Dodging
			&& State != FighterState.Respawning && State != FighterState.LedgeHang && !IsDiving);

		// Squash and stretch, eased so it never pops: long and thin leaving the ground, short
		// and wide landing, coiled before a hit and stretched through it.
		// A wilder fighter squashes and stretches further.
		float squashTarget = 1.0f + (TargetSquash() - 1.0f) * Data.AnimationDrama;
		squashAmount = Mathf.Lerp(squashAmount, squashTarget, 0.45f);
		rig.SetSquash(squashAmount);

		// Lug's hard hat glints while he blocks - or for any move that names it, like a head-first
		// barge. (He wears the hat all the time; "hardhat" is it lit up.)
		string moveExtra = State == FighterState.Attacking ? currentMove?.ShowExtra ?? "" : "";
		// Armour is the hard hat too: lit for as long as a small hit would bounce off him.
		rig.SetExtraVisible("hardhat", IsBlocking || moveExtra == "hardhat" || IsArmoredMove);

		// Knocked over, or curled into a bomb: the limbs are gone and he is drawn as one piece in
		// _Draw. Elim asked for exactly that - "get rid of his arms and legs until he gets back up".
		rig.Visible = HeldPoseName() == null && !IsDrawnAsBall && !IsBombArmed;
		if (!rig.Visible) return;

		// Spinning, the body and arms give way to a turning frame; with the arm stretched out
		// on a tether, the drawn arm gives way to the stretched one.
		bool spinning = IsSpinning;
		bool armOut = IsArmStretched;
		rig.SetPartVisible(RigBone.Torso, !spinning);
		bool grabbing = IsCommandGrabbing;
		rig.SetPartVisible(RigBone.ArmBackUpper, !spinning && !grabbing);
		rig.SetPartVisible(RigBone.ArmBackLower, !spinning && !grabbing);
		rig.SetPartVisible(RigBone.ArmFrontUpper, !spinning && !armOut);
		rig.SetPartVisible(RigBone.ArmFrontLower, !spinning && !armOut);
		// A move can put a different weapon in his hand, or empty it to summon one instead.
		string prop = State == FighterState.Attacking ? currentMove?.PropArt ?? "" : "";
		rig.ShowProp(prop);
		// Hanging from a ledge, both hands are on it - nothing in them.
		rig.SetPartVisible(RigBone.PropFront, !spinning && !armOut && prop != "-" && State != FighterState.LedgeHang);
		ShowTurnFrame(spinning);

		rig.SetRoll(DodgeRoll() + VictimSpinRoll() + AirDashLean() + DiveRoll());
		rig.SetSpinWidth(DiveSpinWidth());
		rig.SetFrontLegReach(LegReachNow());
		// Standing still on flat feet (FighterRig.StandOnFeet) - turned on below for each pose
		// that is just standing.
		rig.SetStanding(false);
		// A hammer carried on the shoulder stays there for anything done on the ground that is not
		// a swing - and through a move that keeps it there, like a barge.
		rig.CarryOnShoulder = Data.ShouldersProp && (State == FighterState.Grounded
			|| State == FighterState.Attacking && currentMove != null && currentMove.CarryOnShoulder);
		// A hand on his hip only while he is just standing; turned on below.
		rig.HandOnHip = false;

		switch (State)
		{
			case FighterState.Dodging:
				// Curled up tight for every dodge; rolls and directional air dodges also turn a
				// full somersault the way they travel.
				rig.ApplyDirect(FighterAnimations.Tuck, 0.5f);
				break;

			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Choice:
				// Holding the swords out to choose from: standing, not swinging.
				rig.SetStanding(IsOnFloor());
				rig.Play(FighterAnimations.Idle);
				rig.Advance();
				break;

			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Resize:
				// Stretching: arms out, legs straight, while the arrows say which way to push.
				rig.SetStanding(IsOnFloor());
				rig.ApplyDirect(FighterAnimations.TPose, 0.35f);
				break;

			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Bomb:
				// Standing still, about to curl up. Not a swing.
				rig.SetStanding(IsOnFloor());
				rig.Play(FighterAnimations.Idle);
				rig.Advance();
				break;

			case FighterState.Attacking when currentMove != null:
				// Sampled against the move's own frame counts, so the strike pose arrives on
				// the exact frame the hitbox does.
				// A robot's attacks click from pose to pose too - rounded up, so the strike still
				// shows by the frame the hitbox does.
				if (Data.Robotic)
				{
					// A robot snaps between the key poses of the move and holds each one.
					(float keyFrame, int key) = RobotAttackKey(moveFrame);
					FighterAnimations.SampleAttack(currentMove, keyFrame, attackPose, drama: Data.AnimationDrama);
					rig.RobotApply(attackPose, currentMove, key, RobotAttackSnap);
					break;
				}
				FighterAnimations.SampleAttack(currentMove, moveFrame, attackPose, drama: Data.AnimationDrama);
				if (currentMove.RunningLegs && Mathf.Abs(Velocity.X) > 45.0f) RunTheLegs(attackPose);
				rig.ApplyDirect(attackPose, FighterAnimations.AttackBlend(currentMove, moveFrame));
				break;

			case FighterState.Hitstun:
				rig.Play(FighterAnimations.Hurt);
				rig.Advance();
				break;

			case FighterState.Grounded when IsBlocking:
				rig.SetStanding(true);
				rig.Play(FighterAnimations.Block);
				rig.Advance();
				break;

			case FighterState.Grounded when IsCrouching:
				rig.Play(FighterAnimations.Crouch);
				rig.Advance();
				break;

			case FighterState.Grounded when landFrames > 0:
				rig.Play(FighterAnimations.Land);
				rig.Advance();
				break;

			case FighterState.Grounded when Mathf.Abs(Velocity.X) > 45.0f:
				rig.Play(Data.Robotic ? FighterAnimations.StompWalk : FighterAnimations.Run);
				// Playback follows actual ground speed, so the feet do not skate - and leg length,
				// so a fighter stretched tall takes long, slow strides.
				rig.Advance(Mathf.Clamp(Mathf.Abs(Velocity.X) / RunSpeed, 0.35f, 1.8f) * 1.25f
					* SizeLevels.StrideRate(sizeLevel));
				break;

			case FighterState.Grounded:
				rig.SetStanding(true);
				rig.HandOnHip = Data.HandOnHip;
				rig.Play(FighterAnimations.Idle);
				rig.Advance();
				break;

			case FighterState.Airborne:
				rig.Play(Velocity.Y < 0.0f ? FighterAnimations.Jump : FighterAnimations.Fall);
				rig.Advance();
				break;

			case FighterState.Respawning:
				rig.Play(FighterAnimations.Fall);
				rig.Advance();
				break;

			case FighterState.LedgeHang when IsClimbingLedge:
				// Pulling up over the lip, hands on it for the first part of the climb.
				rig.ApplyDirect(FighterAnimations.LedgeClimb, 0.5f);
				float handsOn = 1.0f - Mathf.Clamp((ledgeActionTotal - ledgeActionFrames) / (float)Mathf.Max(1, ledgeActionTotal) * 1.8f, 0.0f, 1.0f);
				HoldLedge(handsOn);
				break;

			case FighterState.LedgeHang:
				rig.Play(FighterAnimations.LedgeHang);
				rig.Advance();
				HoldLedge(1.0f);
				break;
		}

		// A stretched kick: the leg pointed at the hit, as well as long enough to reach it.
		float kick = LegStretchAmount();
		if (kick > 0.0f) rig.AimFrontLeg(CurrentHitboxCentre(), kick);
	}

	void UpdateRigTint()
	{
		if (rig == null) return;

		Color tint = Colors.White;

		// The hitlag flash is what turns a freeze into an impact rather than a stutter.
		if (hitlagFrames > 0) tint = new Color(2.2f, 2.2f, 2.2f);
		else if (State == FighterState.Hitstun) tint = new Color(1.0f, 0.62f, 0.62f);
		else if (IsBlinkWindup)
		{
			// Brightening to a flash as the blink gets close, so it is seen coming.
			float t = moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames);
			float flicker = 0.7f + 0.3f * Mathf.Sin(moveFrame * 1.7f);
			tint = Colors.White.Lerp(new Color(1.7f, 1.6f, 1.25f), t * t * flicker);
		}
		else if (IsCharging)
		{
			// A charging smash glows warmer and pulses faster the longer it is held, so the other
			// player can see a big hit coming and how big.
			float t = chargeFrames / (float)MaxChargeFrames;
			float pulse = 0.5f + 0.5f * Mathf.Sin(chargeFrames * (0.3f + 0.5f * t));
			tint = Colors.White.Lerp(new Color(1.3f, 1.1f, 0.55f), (0.3f + 0.7f * t) * pulse);
		}

		else if (helpless && State == FighterState.Airborne)
		{
			// Spent: a little greyed, so it is clear he can do nothing until he lands.
			tint = new Color(0.72f, 0.72f, 0.78f);
		}
		else if (overheatFrames > 0)
		{
			// Stalled in his own steam: washed out and flickering.
			tint = new Color(1.25f, 1.2f, 1.15f).Lerp(Colors.White, 0.5f + 0.5f * Mathf.Sin(fxFrames * 0.9f));
		}
		else if (whiffExtraFrames > 0 && State == FighterState.Attacking
			&& moveFrame > currentMove.TotalFrames - currentMove.EndlagFrames)
		{
			// Seized up after a miss: a dull, rusty cast while he is stuck.
			tint = new Color(0.86f, 0.72f, 0.62f);
		}
		else if (burnFrames > 0)
		{
			tint = Colors.White.Lerp(new Color(1.35f, 0.85f, 0.55f), 0.5f + 0.25f * Mathf.Sin(fxFrames * 0.7f));
		}
		else if (Data.HasHeat && heat > 0.0f)
		{
			tint = HeatTint();
		}

		if (invulnFrames > 0 && (invulnFrames / 4) % 2 == 0) tint.A = 0.4f;

		rig.Modulate = tint;
	}

	// --- Timers --------------------------------------------------------------

	void TickTimers(InputState input)
	{
		if (invulnFrames > 0) invulnFrames--;
		if (blockReleaseLagFrames > 0) blockReleaseLagFrames--;
		if (coyoteFrames > 0) coyoteFrames--;
		if (ledgeCooldownFrames > 0) ledgeCooldownFrames--;
		if (jumpBufferFrames > 0) jumpBufferFrames--;
		if (attackBufferFrames > 0) attackBufferFrames--;
		if (specialBufferFrames > 0) specialBufferFrames--;
		if (airDashLinger > 0) airDashLinger--;
		if (ballSquashFrames > 0) ballSquashFrames--;
		if (spinVictimFrames > 0) spinVictimFrames--;
		if (slipFrames > 0) slipFrames--;
		if (trampolineFrames > 0) trampolineFrames--;
		// Anything that takes him out of the air - a hit, a ledge, a dodge - ends a dash.
		if (State != FighterState.Airborne) airDashFrames = 0;

		// Buffering is what makes the controls feel forgiving rather than strict. An input
		// pressed slightly too early still comes out when it legally can.
		// Frames since each stick axis snapped from the centre to the edge.
		// A flick: the stick reaching the edge within a few frames of leaving the centre.
		centreFramesX = Mathf.Abs(input.Move.X) < 0.3f ? 0 : centreFramesX + 1;
		centreFramesY = Mathf.Abs(input.Move.Y) < 0.3f ? 0 : centreFramesY + 1;
		bool flickedX = Mathf.Abs(input.Move.X) > 0.8f && Mathf.Abs(previousStick.X) <= 0.8f && centreFramesX <= FlickTravelFrames;
		bool flickedY = Mathf.Abs(input.Move.Y) > 0.8f && Mathf.Abs(previousStick.Y) <= 0.8f && centreFramesY <= FlickTravelFrames;
		flickFramesX = flickedX ? 0 : flickFramesX + 1;
		flickFramesY = flickedY ? 0 : flickFramesY + 1;
		previousStick = input.Move;

		if (input.JumpPressed) jumpBufferFrames = Tuning.JumpBufferFrames;
		if (input.AttackPressed) attackBufferFrames = Tuning.AttackBufferFrames;
		if (input.SpecialPressed) specialBufferFrames = Tuning.AttackBufferFrames;

		bool wantsBlock = input.BlockHeld
			&& State == FighterState.Grounded
			&& dodgeFrames == 0
			&& blockReleaseLagFrames == 0;

		if (IsBlocking && !wantsBlock)
		{
			// Releasing block costs a few frames, so block-hit-block is not free.
			blockReleaseLagFrames = Tuning.BlockReleaseLagFrames;
		}
		IsBlocking = wantsBlock;
	}

	// --- States --------------------------------------------------------------

	void TickGrounded(InputState input, float dt)
	{
		if (!IsOnFloor())
		{
			IsCrouching = false;
			coyoteFrames = Tuning.CoyoteFrames;
			State = FighterState.Airborne;
			return;
		}

		helpless = false;
		if (!standingOnBuilt)
		{
			airJumpsUsed = 0;
			upSpecialUsed = false;
			buildUsed = false;
			usedThisAirtime.Clear();
		}
		IsCrouching = false;

		if (IsBlocking)
		{
			// Attack out of a block is a grab - the answer to someone who just holds block.
			if (attackBufferFrames > 0 && Data.Grab != null && Data.Throws.Length == 4)
			{
				attackBufferFrames = 0;
				IsBlocking = false;
				BeginMove(Data.Grab, MoveSlot.Jab);
				Velocity = new Vector2(0.0f, Velocity.Y);
				return;
			}
			// A direction or a fresh press turns a held block into a dodge; otherwise blocking
			// costs chip damage and being slid toward the ledge by reduced knockback.
			if (Mathf.Abs(input.Move.X) > 0.5f && TryStartDodge(input)) return;
			ApplyFriction(dt, GroundFriction);
			return;
		}

		if (TryStartAttack(input)) return;

		// Taunt: standing on the ground, and nothing else going on.
		if (input.TauntPressed && Data.Taunt != null)
		{
			attackBufferFrames = specialBufferFrames = 0;
			BeginMove(Data.Taunt, MoveSlot.Jab);
			Velocity = new Vector2(0.0f, Velocity.Y);
			return;
		}

		// Checked before the jump, because down plus jump is a drop-through and not a jump
		// that happens to be pressed while crouching.
		if (TryDropThrough(input)) return;

		if (jumpBufferFrames > 0)
		{
			jumpBufferFrames = 0;
			Velocity = new Vector2(Velocity.X, -Data.JumpForce * Tuning.JumpScale);
			jumpStretchFrames = JumpStretchFrames;
			State = FighterState.Airborne;
			return;
		}

		// Holding down crouches: no walking, but the stick still turns you round.
		if (input.Move.Y > 0.5f)
		{
			IsCrouching = true;
			if (Mathf.Abs(input.Move.X) > 0.4f) Facing = input.Move.X > 0.0f ? 1 : -1;
			ApplyFriction(dt, GroundFriction);
			return;
		}

		// The stick is a speed dial, not a switch: target speed is the deflection times top
		// speed, so a light push walks and a full push runs, with everything in between.
		float target = input.Move.X * RunSpeed;
		if (Mathf.Abs(input.Move.X) > MoveDeadzone)
		{
			Velocity = new Vector2(
				Mathf.MoveToward(Velocity.X, target, GroundAcceleration * dt),
				Velocity.Y);
			Facing = input.Move.X > 0.0f ? 1 : -1;
		}
		else
		{
			ApplyFriction(dt, GroundFriction);
		}
	}

	/// <summary>How much of his own gravity again holding down adds on the way down.</summary>
	const float FastFallPull = 0.6f;

	void TickAirborne(InputState input, float dt)
	{
		if (IsOnFloor() && Velocity.Y >= 0.0f)
		{
			airJumpsUsed = 0;
			airDashFrames = 0;
			landFrames = 9;
			State = FighterState.Grounded;
			return;
		}

		// Airborne drop-through: holding down and pressing jump while already falling passes
		// through the next soft platform rather than spending an air jump on it.
		if (jumpBufferFrames > 0 && input.Move.Y > 0.5f && Velocity.Y > 0.0f && dropThroughFrames == 0)
		{
			jumpBufferFrames = 0;
			dropThroughFrames = 12;
			CollisionMask = SolidOnlyMask;
		}

		// Checked before anything else: catching a ledge beats every other airborne option,
		// because missing one costs a stock.
		if (TryGrabLedge()) return;

		// Spent an up special: nothing now but drifting down, left or right.
		if (helpless)
		{
			attackBufferFrames = specialBufferFrames = jumpBufferFrames = 0;
			if (Mathf.Abs(input.Move.X) > MoveDeadzone)
			{
				float drift = input.Move.X * Data.AirSpeed * SizeLevels.Speed(sizeLevel);
				Velocity = new Vector2(Mathf.MoveToward(Velocity.X, drift, Data.AirAcceleration * dt), Velocity.Y);
			}
			else
			{
				ApplyFriction(dt, AirDrag);
			}
			return;
		}

		if (input.BlockHeld && TryStartDodge(input)) return;
		if (TryStartAttack(input)) return;

		// Mid air dash: straight along the line, no drift and no gravity, until it runs out - then
		// some of the speed carries on, so it ends in a glide rather than a stop.
		if (airDashFrames > 0)
		{
			if (--airDashFrames > 0)
			{
				Velocity = airDashVelocity;
			}
			else
			{
				Velocity = airDashVelocity * 0.35f;
				airDashLinger = 8;
			}
			return;
		}

		if (jumpBufferFrames > 0)
		{
			// Coyote time: briefly after walking off a ledge you still get your full ground
			// jump, because "I pressed jump and nothing happened" is the worst feeling there is.
			if (coyoteFrames > 0)
			{
				jumpBufferFrames = 0;
				coyoteFrames = 0;
				Velocity = new Vector2(Velocity.X, -Data.JumpForce * Tuning.JumpScale);
			jumpStretchFrames = JumpStretchFrames;
			}
			else if (airJumpsUsed < Data.AirJumps)
			{
				jumpBufferFrames = 0;
				airJumpsUsed++;
				// The second jump is the one way to turn round in the air: it goes the way the
				// stick is held. Drifting never turns him, so back is always a back air.
				if (Mathf.Abs(input.Move.X) > 0.4f) Facing = input.Move.X > 0.0f ? 1 : -1;
				// A fighter with an air dash spends the air jump on that instead.
				if (Data.AirDashSpeed > 0.0f)
				{
					StartAirDash(input);
					return;
				}
				Velocity = new Vector2(Velocity.X, -Data.AirJumpForce * Tuning.JumpScale);
				jumpStretchFrames = JumpStretchFrames;
			}
		}

		if (Mathf.Abs(input.Move.X) > MoveDeadzone)
		{
			float target = input.Move.X * Data.AirSpeed * SizeLevels.Speed(sizeLevel);
			Velocity = new Vector2(
				Mathf.MoveToward(Velocity.X, target, Data.AirAcceleration * dt),
				Velocity.Y);
		}
		else
		{
			// Mild drag, so a fighter who exits hitstun still carries their launch momentum
			// off the stage but does not drift sideways forever with no input.
			ApplyFriction(dt, AirDrag);
		}

		// Fast-fall: holding down on the way down pulls him down harder, up to the fast-fall
		// speed. It used to snap straight to that speed, which flung him at the floor; now it adds
		// a little over half his gravity again. Eric's call, 2026-10-04.
		if (input.Move.Y > 0.5f && Velocity.Y > 0.0f && Velocity.Y < Data.FastFallSpeed)
		{
			float extra = Data.Gravity * Tuning.GravityScale * FastFallPull * dt;
			Velocity = new Vector2(Velocity.X, Mathf.Min(Data.FastFallSpeed, Velocity.Y + extra));
		}
	}

	void TickAttacking(InputState input, float dt)
	{
		// Holding attack freezes a smash at the top of its windup, building charge.
		bool holding = currentMove.ChargeWithSpecial ? input.SpecialHeld : input.AttackHeld;
		if (IsCharging && holding && chargeFrames < MaxChargeFrames)
		{
			chargeFrames++;
			// Curled into a ball and charging: he spins faster and faster on the spot.
			if (currentMove.BallForm) rollAngle += Facing * (0.2f + 0.4f * chargeFrames / MaxChargeFrames);
			if (currentMove.Spin) AdvanceSpin();
			ApplyFriction(dt, GroundFriction);
			return;
		}

		moveFrame++;
		if (currentMove.Spin) AdvanceSpin();
		if (!moveCue.IsNone && moveFrame == moveCue.Frame) SfxPlayer.At(moveCue.Name, GlobalPosition);

		// A ball-form move spins the whole time, fastest while the hitbox is out.
		if (currentMove.BallForm)
		{
			bool hot = moveFrame > currentMove.StartupFrames
				&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;
			rollAngle += Facing * (hot ? 0.45f : 0.2f);
		}

		// Out of a cancellable recovery once it is going: an attack or a special comes straight
		// out of it, and down just stops it. Its own up special stays spent until he lands.
		if (currentMove.CancelIntoAttacks && moveFrame > currentMove.StartupFrames)
		{
			bool attack = attackBufferFrames > 0;
			bool special = specialBufferFrames > 0 && ChooseSpecialSlot(input) != MoveSlot.UpSpecial;
			if (attack || special || input.Move.Y > 0.5f)
			{
				if (!special) specialBufferFrames = 0;
				currentMove = null;
				State = IsOnFloor() ? FighterState.Grounded : FighterState.Airborne;
				if (attack || special) TryStartAttack(input);
				return;
			}
		}

		// Pressing attack again during a combo move queues the next hit.
		if (input.AttackPressed && currentMove.ComboNext != null)
		{
			comboQueued = true;
			attackBufferFrames = 0;
		}

		int activeStart = currentMove.StartupFrames;
		int activeEnd = currentMove.StartupFrames + currentMove.ActiveFrames;

		bool active = moveFrame > activeStart && moveFrame <= activeEnd;

		// A charged dash goes on its first active frame, as fast as it was charged; a dash that
		// waits goes then too, at its own speed.
		if (currentMove.Special == SpecialKind.Dash && (currentMove.Chargeable || currentMove.DashAfterStartup)
			&& moveFrame == activeStart + 1)
		{
			float charge = currentMove.Chargeable ? chargeFrames / (float)MaxChargeFrames : 0.0f;
			Velocity = new Vector2(Facing * currentMove.SpecialSpeed * (1.0f + (currentMove.ChargeSize - 1.0f) * charge),
				Velocity.Y * 0.2f);
		}

		// Reeled in and about to be kicked: held at his hand until the kick lands.
		if (grabbed != null && currentMove == grabKick) grabbed.GlobalPosition = ThrowHold();

		// A swing that met nobody leaves him stuck at the end of it a while (rusty joints).
		if (moveFrame == activeEnd + 1 && Data.WhiffLagFrames > 0 && !hitSomethingThisMove && WhiffCounts(currentMove))
		{
			whiffExtraFrames = Data.WhiffLagFrames;
			SfxPlayer.At("creak", GlobalPosition, 0.08f);
		}

		// A multi-hit lets the same fighter be hit again every few frames.
		if (active && currentMove.RehitFrames > 0 && moveFrame > activeStart + 1
			&& (moveFrame - activeStart - 1) % currentMove.RehitFrames == 0)
		{
			alreadyHitThisMove.Clear();
		}

		switch (currentMove.Special)
		{
			case SpecialKind.Choice:
				// Picking a sword swaps this move for the one picked, which starts from its frame 0.
				if (TickChoice(input, activeStart, activeEnd)) return;
				break;

			case SpecialKind.Dash when currentMove.Blink:
				TickBlink(activeStart, activeEnd);
				if (active) QueryHits();
				break;

			case SpecialKind.Resize:
				if (active) TickResize(input, activeEnd);
				break;

			case SpecialKind.Bomb:
				TickBomb(activeEnd);
				break;

			case SpecialKind.CommandGrab:
				if (TickCommandGrab(activeStart, activeEnd)) return;
				break;

			case SpecialKind.Grab:
				if (TickGrab(input, activeStart, activeEnd)) return;
				break;

			case SpecialKind.Vent:
				// Planted while the furnace opens; in the air he hangs for it.
				Velocity = IsOnFloor() ? new Vector2(0.0f, Velocity.Y) : new Vector2(Velocity.X * 0.8f, Mathf.Min(Velocity.Y, 200.0f));
				if (active && !hazardSpawned) Vent();
				break;

			case SpecialKind.Recovery when currentMove.Flight:
				TickFlight(input, active, dt);
				if (active) QueryHits();
				break;

			case SpecialKind.Recovery when currentMove.DelayedLaunch:
				TickGrapple(input, activeStart);
				if (active) QueryHits();
				break;

			case SpecialKind.Recovery when currentMove.Bounce:
				TickBounce(input, activeStart, dt);
				if (active) QueryHits();
				break;

			default:
				if (active)
				{
					// A command grab's follow-up, or a throw: the one being held takes it first,
					// unblockable - held through any link hits, let go on the last.
					if (grabbed != null && currentMove == grabKick)
					{
						if (currentMove.LinkHit != null && currentMove.RehitFrames > 0 && moveFrame <= activeEnd - currentMove.RehitFrames)
						{
							if ((moveFrame - activeStart - 1) % currentMove.RehitFrames == 0) PinHit(currentMove.LinkHit);
						}
						else
						{
							ThrowGrabbed(currentMove);
						}
					}
					SpawnSpecialHazard(currentMove);
					QueryHits();
				}
				break;
		}

		// Attacks keep their momentum but shed it - you commit to a move, you do not steer it.
		// A dash attack sheds far less, so it slides the whole way through; stopping dead on
		// startup would make it a slow jab rather than a dash attack.
		float shed = currentMove.CarriesMomentum ? 0.12f : 0.5f;
		ApplyFriction(dt, IsOnFloor() ? GroundFriction * shed : Data.AirAcceleration * 0.15f);

		// The next combo hit comes out as soon as this one's hitbox is done - not after its
		// endlag, which is what makes tapping attack a fast string rather than separate jabs.
		if (comboQueued && moveFrame > activeEnd && currentMove.ComboNext != null)
		{
			currentMove = currentMove.ComboNext;
			CueMoveSound(currentMove);
			hitSomethingThisMove = false;
			whiffExtraFrames = 0;
			moveFrame = 0;
			chargeFrames = 0;
			comboQueued = false;
			alreadyHitThisMove.Clear();
			return;
		}

		if (moveFrame >= currentMove.TotalFrames + whiffExtraFrames)
		{
			ReleaseGrabbed();
			// Still sliding from a move made on the ground: the slide keeps stopping at the edge
			// until it is spent or he steers.
			slideStopsAtEdge = moveStartedGrounded && Mathf.Abs(Velocity.X) > 20.0f;
			currentMove = null;
			alreadyHitThisMove.Clear();
			State = IsOnFloor() ? FighterState.Grounded : FighterState.Airborne;
		}
	}

	/// <summary>
	/// Sliding on after a move made on the ground ended - Triguy's long skid out of a Nose
	/// Charge. The edge still stops it, as it stopped the move (see StopAtLedge). Steering, or
	/// coming to a stop, ends it.
	/// </summary>
	bool slideStopsAtEdge;

	void TickHitstun(float dt)
	{
		hitstunFrames--;

		// Launch velocity bleeds off at a fixed rate rather than exponentially, so a launched
		// fighter travels a distance that is proportional to the knockback that sent them.
		Velocity = Velocity.MoveToward(
			new Vector2(0.0f, Velocity.Y),
			Tuning.LaunchDecay * Tuning.GravityScale * dt);

		if (drawnAsBall) rollAngle += Velocity.X * dt / BallRadius();

		if (hitstunFrames <= 0)
		{
			if (pendingTumbleFrames > 0)
			{
				tumbleFrames = pendingTumbleFrames;
				pendingTumbleFrames = 0;
				State = FighterState.Tumbling;

				// Knocked over on the ground, he keeps rolling the way the hit sent him, which is
				// how he ends up rolling off the edge. That is the weakness Elim asked for.
				if (IsOnFloor() && Mathf.Abs(Velocity.X) > 1.0f)
				{
					Velocity = new Vector2(
						Mathf.Sign(Velocity.X) * Mathf.Max(Mathf.Abs(Velocity.X), 380.0f), Velocity.Y);
				}
				return;
			}

			drawnAsBall = false;
			State = IsOnFloor() ? FighterState.Grounded : FighterState.Airborne;
		}
	}

	/// <summary>
	/// Rolling as a ball, with no control, until he gets back up. Still hittable - being knocked
	/// over is supposed to be dangerous - and air control and both jumps come back the moment it
	/// ends, even if he has rolled off the stage, so he can always try to get home.
	/// </summary>
	void TickTumbling(InputState input, float dt)
	{
		tumbleFrames--;
		// The stick leans on the roll - with it to roll further, against it to slow it a little -
		// but never stops it dead: knocked over is still knocked over.
		float lean = Mathf.Abs(Velocity.X) > 1.0f ? input.Move.X * Mathf.Sign(Velocity.X) : 0.0f;
		ApplyFriction(dt, IsOnFloor() ? TumbleFriction * (1.0f - 0.5f * lean) : AirDrag);
		rollAngle += Velocity.X * dt / BallRadius();

		if (tumbleFrames > 0) return;

		drawnAsBall = false;
		if (IsOnFloor())
		{
			landFrames = 9;
			State = FighterState.Grounded;
		}
		else
		{
			State = FighterState.Airborne;
		}
	}

	/// <summary>Knocked out and not back yet: invisible, and not framed by the camera.</summary>
	public bool IsWaitingToRespawn => State == FighterState.Respawning && respawnDelayFrames > 0;
	int respawnDelayFrames;

	void TickRespawning()
	{
		GlobalPosition = respawnPoint;
		Velocity = Vector2.Zero;

		if (respawnDelayFrames > 0)
		{
			Visible = --respawnDelayFrames == 0;
			if (Visible) SfxPlayer.At("respawn", respawnPoint, 0.0f);
			return;
		}

		if (--respawnFreezeFrames <= 0)
		{
			invulnFrames = Tuning.RespawnInvulnFrames;
			State = FighterState.Airborne;
		}
	}

	// --- Attacking -----------------------------------------------------------

	/// <summary>
	/// Picks which of the seventeen moves this press means, from the state the fighter is in
	/// and the direction being held. This is the whole directional-attack system: one button
	/// and a stick, resolved here.
	/// </summary>
	bool TryStartAttack(InputState input)
	{
		bool wantsAttack = attackBufferFrames > 0;
		bool wantsSpecial = specialBufferFrames > 0;
		if (!wantsAttack && !wantsSpecial) return false;

		MoveSlot slot = wantsSpecial ? ChooseSpecialSlot(input) : ChooseAttackSlot(input);
		MoveData chosen = Data.Move(slot);
		if (chosen == null) return false;

		// Already used this trip into the air: the press does nothing until the ground (or a
		// ledge) gives it back.
		// The press is thrown away rather than left buffered, or it comes out a frame later as a
		// neutral special once the stick leaves up.
		if (slot == MoveSlot.UpSpecial && upSpecialUsed)
		{
			specialBufferFrames = 0;
			return false;
		}
		if (chosen.Special == SpecialKind.BuildPlatform && buildUsed)
		{
			specialBufferFrames = 0;
			return false;
		}
		if (chosen.OncePerAirtime && usedThisAirtime.Contains(chosen))
		{
			specialBufferFrames = 0;
			return false;
		}
		if (chosen.Special == SpecialKind.BuildPlatform) buildUsed = true;
		if (slot == MoveSlot.UpSpecial)
		{
			upSpecialUsed = true;
			helpless = !chosen.CancelIntoAttacks;
		}
		if (chosen.OncePerAirtime) usedThisAirtime.Add(chosen);

		attackBufferFrames = 0;
		specialBufferFrames = 0;

		// A special points where the stick points when it is pressed, so a recovery aimed back
		// at the stage goes toward the stage even if he was facing away from it.
		if (wantsSpecial && Mathf.Abs(input.Move.X) > 0.4f) Facing = input.Move.X > 0.0f ? 1 : -1;

		BeginMove(chosen, slot);
		return true;
	}

	/// <summary>Whether the move in progress started on the ground.</summary>
	bool moveStartedGrounded;

	/// <summary>
	/// A move started on the ground stops at the edge of it instead of carrying off: a dash
	/// attack or a charge that slides you off the stage is the game killing you, not the other
	/// player. Only a blink crosses a gap - that is what it is for.
	/// </summary>
	void StopAtLedge(float dt)
	{
		bool sliding = State == FighterState.Grounded && slideStopsAtEdge;
		if (sliding && (Mathf.Abs(Velocity.X) < 20.0f || Mathf.Abs(lastStick.X) > 0.3f)) slideStopsAtEdge = sliding = false;
		bool moving = State == FighterState.Attacking && currentMove != null && moveStartedGrounded;
		if (!moving && !sliding) return;
		// A blink started on the ground stops at the edge too - unless it is going to one of his
		// planted blades, which is the one way across a gap (Eric's call, 2026-10-04: no side
		// special or dash attack carries anyone off the platform they are standing on).
		if (moving && currentMove.Blink && blinkToTrap) return;
		if (!IsOnFloor() || Mathf.Abs(Velocity.X) < 1.0f) return;

		float dir = Mathf.Sign(Velocity.X);
		var probe = new Vector2(
			GlobalPosition.X + dir * (Mathf.Min(footWidth * 0.5f, bodySize.X * 0.35f) + Mathf.Abs(Velocity.X) * dt),
			GlobalPosition.Y + bodySize.Y * 0.5f + 8.0f);
		var query = new PhysicsPointQueryParameters2D { Position = probe, CollisionMask = CollisionMask };
		if (GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count == 0)
		{
			Velocity = new Vector2(0.0f, Velocity.Y);
		}
	}

	/// <summary>Starts a move from its first frame, with nothing carried over from the last one.</summary>
	void BeginMove(MoveData move, MoveSlot slot)
	{
		ReleaseGrabbed();
		// Out of an air dash, an attack keeps half the speed rather than all of it.
		if (airDashFrames > 0)
		{
			airDashFrames = 0;
			airDashLinger = 8;
			Velocity = airDashVelocity * 0.5f;
		}
		move = SizedNormal(move);
		currentMove = move;
		chosenPose = move.PoseArts.Length > 0 ? move.PoseArts[poseRng.Next(move.PoseArts.Length)] : null;
		currentSlot = slot;
		moveStartedGrounded = IsOnFloor();
		moveFrame = 0;
		chargeFrames = 0;
		comboQueued = false;
		alreadyHitThisMove.Clear();
		hazardSpawned = false;
		burstFired = 0;
		blinkStarted = false;
		swingReleased = false;
		spinAngle = 0.0f;
		spinDone = false;
		hitSomethingThisMove = false;
		whiffExtraFrames = 0;
		grabKick = null;
		State = FighterState.Attacking;
		CueMoveSound(move);
		if (move != Data.Taunt && move.Special != SpecialKind.Vent) AddHeat(Heat.PerAttack);

		StartSpecialMotion(move);
	}

	MoveSlot ChooseAttackSlot(InputState input)
	{
		bool airborne = !IsOnFloor();
		float x = input.Move.X;
		float y = input.Move.Y;

		if (airborne)
		{
			if (y < -0.5f) return MoveSlot.UpAir;
			if (y > 0.5f) return MoveSlot.DownAir;
			if (Mathf.Abs(x) > 0.4f)
			{
				// Forward and back are relative to facing, not to the screen. That is what
				// makes a back air a deliberate choice rather than an accident of which way
				// you happen to be pointing.
				return Mathf.Sign(x) == Facing ? MoveSlot.ForwardAir : MoveSlot.BackAir;
			}
			return MoveSlot.NeutralAir;
		}

		// Attacking out of a run gives the dash attack, decided by actual speed rather than by
		// a held direction, so a fighter still sliding from a turnaround does not get one.
		if (AttackWouldDash) return MoveSlot.DashAttack;

		// A flick is a smash; a direction already being held is a tilt. A d-pad direction is
		// always a smash - see InputState.MoveFromDpad.
		bool flickY = flickFramesY <= SmashFlickFrames || input.MoveFromDpad;
		bool flickX = flickFramesX <= SmashFlickFrames || input.MoveFromDpad;
		if (y < -0.5f && flickY) return MoveSlot.UpSmash;
		if (y > 0.5f && flickY) return MoveSlot.DownSmash;
		if (Mathf.Abs(x) > 0.4f && flickX)
		{
			Facing = x > 0.0f ? 1 : -1;
			return MoveSlot.ForwardSmash;
		}

		if (y < -0.5f) return MoveSlot.UpTilt;
		if (y > 0.5f) return MoveSlot.DownTilt;
		if (Mathf.Abs(x) > 0.4f) return MoveSlot.ForwardTilt;
		return MoveSlot.Jab;
	}

	static MoveSlot ChooseSpecialSlot(InputState input)
	{
		if (input.Move.Y < -0.5f) return MoveSlot.UpSpecial;
		if (input.Move.Y > 0.5f) return MoveSlot.DownSpecial;
		if (Mathf.Abs(input.Move.X) > 0.4f) return MoveSlot.SideSpecial;
		return MoveSlot.NeutralSpecial;
	}

	/// <summary>
	/// The movement half of a special, applied the instant it starts. The hazard half waits
	/// for the active frames, in <see cref="TickAttacking"/>.
	/// </summary>
	void StartSpecialMotion(MoveData move)
	{
		switch (move.Special)
		{
			case SpecialKind.Dash when move.Blink:
				// A blink stands still to glint first; the distance is crossed later, all at once.
				Velocity = new Vector2(Velocity.X * 0.3f, Velocity.Y * 0.3f);
				break;

			case SpecialKind.Dash when move.Chargeable || move.DashAfterStartup:
				// Stops to wind up; the dash itself waits for the release, or for the end of the
				// startup (see TickAttacking).
				Velocity = new Vector2(Velocity.X * 0.3f, Velocity.Y);
				break;

			case SpecialKind.Dash:
				Velocity = new Vector2(Facing * move.SpecialSpeed, Velocity.Y * 0.2f);
				break;

			case SpecialKind.Recovery when move.DelayedLaunch:
				// The launch waits for the hook; startup only brakes the fall.
				hookFired = false;
				airJumpsUsed = 0;
				tetherAim = new Vector2(Facing * move.SpecialSpeed, -move.SpecialRise).Normalized();
				break;

			case SpecialKind.Recovery when move.Bounce:
				{
					// In the air the trampoline appears under his feet. On the floor it stands on the
					// floor and he hops up onto its mat, as Lug is lifted onto his girder. The bounce
					// waits for the active frames.
					float lift = IsOnFloor() ? TrampolineLegs() : 0.0f;
					trampolineAt = GlobalPosition + new Vector2(0.0f, bodySize.Y * 0.5f - lift);
					if (lift > 0.0f)
					{
						GlobalPosition -= new Vector2(0.0f, lift);
						Velocity = new Vector2(Velocity.X, 0.0f);
					}
				}
				trampolineShown = move.StartupFrames + move.ActiveFrames + TrampolineLinger;
				trampolineFrames = trampolineShown;
				airJumpsUsed = 0;
				break;

			case SpecialKind.Recovery when move.Flight:
				// Flight takes off from wherever he is: the fall stops, then the wings lift him.
				Velocity = new Vector2(Velocity.X * 0.5f, Mathf.Min(Velocity.Y, 0.0f) * 0.3f);
				airJumpsUsed = 0;
				break;

			case SpecialKind.Recovery:
				// An up-special always gives real height, and always refreshes the air jump,
				// because the whole job of this slot is getting home.
				Velocity = new Vector2(Velocity.X * 0.4f + Facing * move.SpecialSpeed, -move.SpecialRise);
				airJumpsUsed = 0;
				break;

			case SpecialKind.Resize:
				resizeStickDir = 0;
				break;

			case SpecialKind.Bomb:
				bombDetonated = false;
				Velocity = new Vector2(0.0f, Velocity.Y);
				break;
		}
	}

	/// <summary>Spawns whatever the special leaves behind, once, on its first active frame.</summary>
	void SpawnSpecialHazard(MoveData move)
	{
		if (hazardSpawned || Match == null) return;

		Vector2 origin = GlobalPosition + new Vector2(move.HitboxOffset.X * Facing, move.HitboxOffset.Y);

		switch (move.Special)
		{
			case SpecialKind.Projectile when move.ShotAngles.Length > 0 || move.Mirrored || move.FromGround:
				hazardSpawned = true;
				SpawnVolley(move);
				break;

			case SpecialKind.Projectile when move.BurstCount > 1:
				// A burst fires a shot every few frames until it has fired them all, each
				// starting a little higher or lower so they do not overlap.
				if (burstFired >= move.BurstCount) break;
				if ((moveFrame - move.StartupFrames - 1) % Mathf.Max(1, move.BurstInterval) != 0) break;
				float spread = (burstFired % 2 == 0 ? -1.0f : 1.0f) * 6.0f * (burstFired / 2 + 1) * 0.5f;
				Match.SpawnHazard(this, SizedFor(move), origin + new Vector2(0.0f, spread),
					new Vector2(Facing * move.SpecialSpeed, 0.0f));
				burstFired++;
				if (burstFired > 1) SfxPlayer.At("special_shot", GlobalPosition);
				break;

			case SpecialKind.Projectile:
				hazardSpawned = true;
				// Only an arcing projectile gets the little upward toss - or a big one, for a
				// throw that lobs high; a beam starts at the hands and grows straight out.
				float toss = move.LaunchLift > 0.0f ? -move.LaunchLift : move.SpecialGravity > 0.0f ? -120.0f : 0.0f;
				if (move.Beam) activeBeamFrom = move.BeamFrom;
				// A shot can come out of a named spot on the drawing, as a beam can - DoomBot's
				// missile out of the socket in his chest.
				Vector2? spot = !move.Beam && !string.IsNullOrEmpty(move.BeamFrom) && rig != null && rig.Loaded
					? rig.PointGlobal(move.BeamFrom) : null;
				Vector2 start = move.Beam ? BeamOrigin() : spot ?? origin;
				MoveData shot = move.ChargeWithSpecial ? ChargedFor(move) : SizedFor(move);
				Match.SpawnHazard(this, shot, start, new Vector2(Facing * move.SpecialSpeed, toss));
				break;

			case SpecialKind.Shockwave:
				{
					// One quake, from where the hammer hits the floor. It carries the charge, a
					// little weaker than the slam itself, and the camera shakes with it.
					hazardSpawned = true;
					var impact = new Vector2(GlobalPosition.X + move.HitboxOffset.X * Facing,
						GlobalPosition.Y + bodySize.Y * 0.5f);
					float power = ChargeScale * move.ShockwavePower;
					Hazard quake = Match.SpawnHazard(this, move, impact, new Vector2(move.SpecialSpeed, 0.0f), power);
					quake.ShareHits(alreadyHitThisMove);
					Match.Shake(35.0f * ChargeScale);
					break;
				}

			case SpecialKind.BuildPlatform:
				{
					// The girder goes in under his feet, and he is put standing on it. On the
					// ground that lifts him up onto it; in the air it catches him where he is.
					hazardSpawned = true;
					const float Thickness = 28.0f;
					float feet = GlobalPosition.Y + bodySize.Y * 0.5f;
					float top = IsOnFloor() ? feet - Thickness : feet;
					if (builtPlatform != null && IsInstanceValid(builtPlatform)) builtPlatform.QueueFree();
					builtPlatform = Match.SpawnPlatform(this, move,
						new Rect2(GlobalPosition.X - move.PlatformWidth * 0.5f, top, move.PlatformWidth, Thickness));
					GlobalPosition = new Vector2(GlobalPosition.X, top - bodySize.Y * 0.5f - 1.0f);
					Velocity = new Vector2(Velocity.X * 0.3f, 0.0f);
					break;
				}

			case SpecialKind.Drop:
				hazardSpawned = true;
				Match.SpawnHazard(this, move, origin, new Vector2(Velocity.X * 0.3f, move.SpecialSpeed));
				break;

			case SpecialKind.Trap:
				{
					hazardSpawned = true;
					// A kicked trap - a traffic cone - goes down on the floor and skids out along it,
					// and counts as the same hit as the kick that sent it.
					bool slides = move.SlideFriction > 0.0f;
					if (slides) origin.Y = GlobalPosition.Y + bodySize.Y * 0.5f - move.FxArtSize * 0.5f;
					Hazard trap = Match.SpawnHazard(this, move, origin,
						slides ? new Vector2(Facing * move.SpecialSpeed, 0.0f) : Vector2.Zero);
					if (slides) trap.ShareHits(alreadyHitThisMove);
					Track(move, trap);
					break;
				}

			case SpecialKind.Puddle:
				{
					// Down under his feet: the puddle finds the floor from there.
					hazardSpawned = true;
					var feet = new Vector2(GlobalPosition.X, GlobalPosition.Y + bodySize.Y * 0.5f);
					Track(move, Match.SpawnHazard(this, move, feet, Vector2.Zero));
					break;
				}

			case SpecialKind.Walker:
				{
					// Set down on the floor in front of him, already walking.
					hazardSpawned = true;
					var at = new Vector2(origin.X, GlobalPosition.Y + bodySize.Y * 0.5f - move.FxArtSize * 0.5f);
					Track(move, Match.SpawnHazard(this, move, at, new Vector2(Facing * move.SpecialSpeed, 0.0f)));
					break;
				}

			case SpecialKind.Cloud:
				// Formed where the move says - high over his head - and left there to rain.
				hazardSpawned = true;
				Track(move, Match.SpawnHazard(this, move, origin, Vector2.Zero));
				break;
		}
	}

	/// <summary>
	/// Several shots at once: one per angle, on one side or both, from the hands or up out of the
	/// floor. They share the move's hit list, so a volley is one hit on each fighter it catches,
	/// not eight - and a charged smash that summons them charges them too.
	/// </summary>
	void SpawnVolley(MoveData move)
	{
		float[] angles = move.ShotAngles.Length > 0 ? move.ShotAngles : new[] { move.FromGround ? 90.0f : 0.0f };
		float feet = GlobalPosition.Y + bodySize.Y * 0.5f;

		for (int side = 1; side >= -1; side -= 2)
		{
			if (side < 0 && !move.Mirrored) break;
			int dir = Facing * side;

			foreach (float degrees in angles)
			{
				float a = Mathf.DegToRad(degrees);
				var velocity = new Vector2(Mathf.Cos(a) * dir, -Mathf.Sin(a)) * move.SpecialSpeed;
				var start = GlobalPosition + new Vector2(move.HitboxOffset.X * dir, move.HitboxOffset.Y);

				Hazard shot = Match.SpawnHazard(this, move, start, velocity, ChargeScale);
				shot.ShareHits(alreadyHitThisMove);
				shot.FlipArt = dir < 0;
				if (move.FromGround) shot.RiseFrom(feet);
			}
		}
	}

	/// <summary>
	/// Keeps a move with a MaxOut to that many hazards: the oldest goes when a new one arrives.
	/// Planted blades would otherwise carpet the stage.
	/// </summary>
	void Track(MoveData move, Hazard hazard)
	{
		if (move.MaxOut <= 0 || hazard == null) return;
		if (!outHazards.TryGetValue(move, out List<Hazard> list))
		{
			list = new List<Hazard>();
			outHazards[move] = list;
		}
		list.RemoveAll(h => !IsInstanceValid(h) || h.IsExpiring);
		while (list.Count >= move.MaxOut)
		{
			list[0].Remove();
			list.RemoveAt(0);
		}
		list.Add(hazard);
	}

	// --- Dodging ---------------------------------------------------------------

	/// <summary>
	/// Block plus a direction rolls, block on its own spot-dodges, and block in the air is an
	/// air dodge. All three are invulnerable in the middle and vulnerable at the edges, which
	/// is what makes dodging a read rather than a panic button.
	/// </summary>
	/// <summary>
	/// How far round a dodge has rolled the fighter, in radians: one full turn over the dodge,
	/// fast in the middle, in the direction it is moving. A spot dodge or a still air dodge does
	/// not roll.
	/// </summary>
	bool IsDiving => State == FighterState.Attacking && currentMove != null && currentMove.Corkscrew;

	readonly Pose runLegs = new Pose();
	float runLegFrame;

	/// <summary>Puts the run cycle on the legs of an attack pose, at the speed he is going.</summary>
	void RunTheLegs(Pose pose)
	{
		runLegFrame += Mathf.Clamp(Mathf.Abs(Velocity.X) / RunSpeed, 0.35f, 1.8f) * 1.25f;
		FighterAnimations.Run.Sample(runLegFrame, runLegs);
		foreach (RigBone leg in new[] { RigBone.LegBackUpper, RigBone.LegBackLower, RigBone.LegFrontUpper, RigBone.LegFrontLower })
		{
			pose.Set(leg, runLegs[leg]);
		}
	}

	/// <summary>
	/// Which key pose of the move a robot is on at this frame, and the move frame that shows it:
	/// the coil, the strike, the recovery. He snaps between them and holds each - see
	/// FighterRig.Robotic.
	/// </summary>
	(float frame, int key) RobotAttackKey(int frame)
	{
		int startup = currentMove.StartupFrames;
		int activeEnd = startup + currentMove.ActiveFrames;
		// The coil: snapped to at once and held.
		if (frame <= startup - RobotAttackSnap) return (startup, 0);
		// A move that turns through its active frames (a windmill) clicks round in steps.
		bool turning = currentMove.Anim == AttackAnim.Windmill || currentMove.Anim == AttackAnim.WideArc;
		if (turning && frame > startup && frame <= activeEnd)
		{
			int step = (frame - startup - 1) / 3;
			return (Mathf.Min(activeEnd, startup + 1 + (step + 1) * 3), 2 + step);
		}
		// The strike, fully out by the first active frame, held through the follow-through.
		int held = activeEnd + Mathf.CeilToInt(currentMove.EndlagFrames * 0.3f);
		if (frame <= held) return (startup + 1, 1);
		// And back, in one snap.
		return (currentMove.TotalFrames, 1000);
	}

	/// <summary>Frames a robot's attack takes to snap from one key pose to the next.</summary>
	const int RobotAttackSnap = 2;

	/// <summary>
	/// A corkscrew dive tips him flat, head first, through the startup, stays flat while it is
	/// live and comes back up through the endlag.
	/// </summary>
	float DiveRoll()
	{
		if (!IsDiving) return 0.0f;
		int start = currentMove.StartupFrames;
		int end = start + currentMove.ActiveFrames;
		float t = moveFrame <= start ? moveFrame / (float)Mathf.Max(1, start)
			: moveFrame <= end ? 1.0f
			: 1.0f - (moveFrame - end) / (float)Mathf.Max(1, currentMove.EndlagFrames / 2);
		t = Mathf.Clamp(t, 0.0f, 1.0f);
		return Facing * Mathf.Pi * 0.5f * t * t * (3.0f - 2.0f * t);
	}

	/// <summary>Spinning along his length while the dive is live: two and a half turns.</summary>
	float DiveSpinWidth()
	{
		if (!IsDiving || moveFrame <= currentMove.StartupFrames) return 1.0f;
		int since = moveFrame - currentMove.StartupFrames;
		if (since > currentMove.ActiveFrames) return 1.0f;
		float turns = 2.5f * since / (float)Mathf.Max(1, currentMove.ActiveFrames);
		return Mathf.Cos(Mathf.Tau * turns);
	}

	float DodgeRoll()
	{
		if (State != FighterState.Dodging || dodgeStartFrames <= 0 || Mathf.Abs(dodgeVelocity.X) < 1.0f) return 0.0f;
		float t = 1.0f - dodgeFrames / (float)dodgeStartFrames;
		float eased = t * t * (3.0f - 2.0f * t);
		return Mathf.Sign(dodgeVelocity.X) * Mathf.Tau * eased;
	}

	bool TryStartDodge(InputState input)
	{
		if (!input.BlockHeld || blockReleaseLagFrames > 0) return false;

		bool airborne = !IsOnFloor();
		bool directional = Mathf.Abs(input.Move.X) > 0.5f;
		// A fighter who cannot roll just keeps blocking.
		if (!airborne && directional && !Data.CanRoll) return false;

		if (airborne)
		{
			dodgeFrames = AirDodgeFrames;
			dodgeVelocity = directional
				? new Vector2(Mathf.Sign(input.Move.X) * 720.0f, input.Move.Y * 520.0f)
				: Vector2.Zero;
		}
		else if (directional)
		{
			dodgeFrames = RollFrames;
			dodgeVelocity = new Vector2(Mathf.Sign(input.Move.X) * Data.RunSpeed * 1.15f, 0.0f);
		}
		else
		{
			dodgeFrames = SpotDodgeFrames;
			dodgeVelocity = Vector2.Zero;
		}

		dodgeStartFrames = dodgeFrames;
		IsBlocking = false;
		Velocity = dodgeVelocity;
		State = FighterState.Dodging;
		return true;
	}

	void TickDodging(float dt)
	{
		int elapsed = (State == FighterState.Dodging) ? dodgeStartFrames - dodgeFrames : 0;
		dodgeFrames--;

		if (elapsed >= DodgeInvulnStart && elapsed <= DodgeInvulnEnd) invulnFrames = 2;

		Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0.0f, 2600.0f * dt), Velocity.Y);

		if (dodgeFrames <= 0)
		{
			blockReleaseLagFrames = Tuning.BlockReleaseLagFrames;
			State = IsOnFloor() ? FighterState.Grounded : FighterState.Airborne;
		}
	}

	// --- Ledges ----------------------------------------------------------------

	/// <summary>
	/// Catches a stage corner when falling past it. Recovering from off-stage should feel
	/// possible rather than punishing, so the snap radius is generous and grabbing grants a
	/// moment of invulnerability.
	/// </summary>
	bool TryGrabLedge()
	{
		if (Match == null || ledgeCooldownFrames > 0) return false;
		if (Velocity.Y < -140.0f) return false;

		foreach (Vector2 ledge in Match.Ledges)
		{
			// Only from outside the platform, and only from at or below the lip - otherwise a
			// fighter standing on the edge would grab the floor they are already on.
			bool outside = Mathf.Sign(GlobalPosition.X - ledge.X) != 0;
			if (!outside) continue;
			if (GlobalPosition.Y < ledge.Y - 30.0f) continue;
			// What reaches the ledge is his hands, up at the front of his shoulders - not the
			// middle of his body, which on a giant like DoomBot never came near enough to catch
			// anything (Eric, 2026-10-04). The reach grows with the body.
			float toward = Mathf.Sign(ledge.X - GlobalPosition.X);
			Vector2 hands = GlobalPosition + new Vector2(toward * bodySize.X * 0.45f, -bodySize.Y * 0.42f);
			float snap = Mathf.Max(LedgeSnapRadius, bodySize.Y * 0.32f);
			if (hands.DistanceSquaredTo(ledge) > snap * snap) continue;

			heldLedge = ledge;
			ledgeAction = LedgeAction.None;
			ledgeHangFrames = 0;
			Facing = GlobalPosition.X < ledge.X ? 1 : -1;
			GlobalPosition = ledge + new Vector2(-Facing * bodySize.X * 0.45f, bodySize.Y * 0.42f);
			Velocity = Vector2.Zero;
			airJumpsUsed = 0;
			upSpecialUsed = false;
			helpless = false;
			buildUsed = false;
			usedThisAirtime.Clear();
			invulnFrames = Mathf.Max(invulnFrames, LedgeGrabInvulnFrames);
			State = FighterState.LedgeHang;
			return true;
		}

		return false;
	}

	// --- Getting up from a ledge ----------------------------------------------------------

	enum LedgeAction { None, Climb, Roll, Attack }

	LedgeAction ledgeAction;
	int ledgeActionFrames;
	int ledgeActionTotal;
	Vector2 ledgeFrom;
	Vector2 ledgeTo;

	/// <summary>Pulling up onto the stage at an ordinary pace, and quickly before a roll or an attack.</summary>
	const int LedgeClimbFrames = 18;

	/// <summary>
	/// Frames after catching a ledge before a way up can be picked - so the stick still held
	/// toward the stage from drifting back does not climb him up the instant he catches it, before
	/// he could choose to roll or attack instead. Letting go is never held up.
	/// </summary>
	const int LedgeSettleFrames = 10;
	int ledgeHangFrames;
	const int LedgeQuickClimbFrames = 10;

	/// <summary>Climbing up, rolling or attacking from a ledge: he is pulling himself up (see FighterAnimations).</summary>
	public bool IsClimbingLedge => State == FighterState.LedgeHang && ledgeAction != LedgeAction.None;

	/// <summary>
	/// Both hands on the ledge: the back one on the corner, the front one a little further on
	/// along the top. Blended in by <paramref name="amount"/> over the pose.
	/// </summary>
	void HoldLedge(float amount)
	{
		if (amount <= 0.0f) return;
		rig.ReachArm(RigBone.ArmBackUpper, RigBone.ArmBackLower, heldLedge + new Vector2(Facing * 4.0f, 2.0f), amount);
		rig.ReachArm(RigBone.ArmFrontUpper, RigBone.ArmFrontLower, heldLedge + new Vector2(Facing * bodySize.X * 0.22f, 1.0f), amount);
	}

	/// <summary>
	/// Hanging from a ledge, every way back is there (Eric's call, 2026-10-04): jump up off it;
	/// up or toward the stage to climb up onto it; block to climb and roll on past the edge;
	/// attack to climb and swing at whoever is waiting there; down or away to let go. Climbing
	/// cannot be hit, so the stage edge is not a trap - but each way up is a fixed, readable
	/// motion someone standing there can see coming.
	/// </summary>
	void TickLedgeHang(InputState input)
	{
		Velocity = Vector2.Zero;
		if (ledgeAction != LedgeAction.None)
		{
			TickLedgeAction();
			return;
		}

		// Away from the stage, or down: let go.
		bool awayFromStage = Mathf.Abs(input.Move.X) > 0.5f && Mathf.Sign(input.Move.X) != Facing;
		if (awayFromStage || input.Move.Y > 0.6f)
		{
			ReleaseLedge();
			return;
		}

		if (++ledgeHangFrames < LedgeSettleFrames) return;

		// Jump: straight up off the ledge, a little in toward the stage.
		if (jumpBufferFrames > 0)
		{
			jumpBufferFrames = 0;
			ReleaseLedge();
			GlobalPosition = heldLedge + new Vector2(-Facing * bodySize.X * 0.2f, -bodySize.Y * 0.35f);
			Velocity = new Vector2(Facing * 160.0f, -Data.JumpForce * Tuning.JumpScale);
			jumpStretchFrames = JumpStretchFrames;
			return;
		}

		// Block rolls on past the edge - or, for a fighter who cannot roll, just climbs up.
		if (input.BlockHeld && blockReleaseLagFrames == 0)
		{
			StartLedgeAction(Data.CanRoll ? LedgeAction.Roll : LedgeAction.Climb, Data.CanRoll ? LedgeQuickClimbFrames : LedgeClimbFrames);
		}
		else if (attackBufferFrames > 0 || specialBufferFrames > 0)
		{
			attackBufferFrames = specialBufferFrames = 0;
			StartLedgeAction(LedgeAction.Attack, LedgeQuickClimbFrames);
		}
		else if (input.Move.Y < -0.5f || (Mathf.Abs(input.Move.X) > 0.5f && Mathf.Sign(input.Move.X) == Facing))
		{
			StartLedgeAction(LedgeAction.Climb, LedgeClimbFrames);
		}
	}

	void StartLedgeAction(LedgeAction action, int frames)
	{
		ledgeAction = action;
		ledgeActionFrames = ledgeActionTotal = frames;
		ledgeFrom = GlobalPosition;
		// Standing on the stage, his feet just in from the edge.
		ledgeTo = heldLedge + new Vector2(Facing * (footWidth * 0.5f + 14.0f), -bodySize.Y * 0.5f - 1.0f);
		invulnFrames = Mathf.Max(invulnFrames, frames + 4);
	}

	/// <summary>Up first, then over: the body rises up past the lip before it moves in over it.</summary>
	void TickLedgeAction()
	{
		float t = 1.0f - --ledgeActionFrames / (float)Mathf.Max(1, ledgeActionTotal);
		float up = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Min(1.0f, t * 1.6f));
		float over = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Max(0.0f, (t - 0.35f) / 0.65f));
		GlobalPosition = new Vector2(Mathf.Lerp(ledgeFrom.X, ledgeTo.X, over), Mathf.Lerp(ledgeFrom.Y, ledgeTo.Y, up));
		if (ledgeActionFrames > 0) return;

		LedgeAction action = ledgeAction;
		ledgeAction = LedgeAction.None;
		GlobalPosition = ledgeTo;
		ledgeCooldownFrames = LedgeRegrabCooldown;
		Velocity = new Vector2(0.0f, 60.0f);
		State = FighterState.Grounded;

		if (action == LedgeAction.Roll)
		{
			dodgeFrames = dodgeStartFrames = RollFrames;
			dodgeVelocity = new Vector2(Facing * Data.RunSpeed * 1.15f, 0.0f);
			Velocity = dodgeVelocity;
			State = FighterState.Dodging;
		}
		else if (action == LedgeAction.Attack)
		{
			BeginMove(LedgeAttack, MoveSlot.Jab);
		}
	}

	/// <summary>
	/// The swing that comes with climbing up: a low, sweeping kick just in from the edge, that
	/// knocks whoever was waiting there away. The same for everyone, and weak: it is for getting
	/// back on, not for winning.
	/// </summary>
	static readonly MoveData LedgeAttack = new MoveData
	{
		MoveName = "Ledge Attack",
		Anim = AttackAnim.LowSweep,
		PropArt = "-",
		StartupFrames = 6, ActiveFrames = 4, EndlagFrames = 18,
		Damage = 7.0f, BaseKnockback = 40.0f, KnockbackGrowth = 0.4f,
		LaunchAngleDegrees = 30.0f,
		HitboxOffset = new Vector2(64.0f, 30.0f), HitboxRadius = 48.0f,
	};

	void ReleaseLedge()
	{
		ledgeCooldownFrames = LedgeRegrabCooldown;
		ledgeAction = LedgeAction.None;
		State = FighterState.Airborne;
	}

	/// <summary>
	/// Where a hit reaches from: a point inside his own body at the height of the hit (or at the
	/// edge of the body nearest it), on the side it is aimed. The hitbox is the whole capsule from
	/// here out to the hitbox centre, so a hammer hits someone standing right against him as well
	/// as at the end of its head. Eric's call, 2026-10-04: hits that looked like they should
	/// connect up close went through, because only the far end of the weapon could hit. A swing
	/// round an arc or on a rope keeps its single circle.
	/// </summary>
	public Vector2 HitboxRoot()
	{
		if (currentMove == null || !string.IsNullOrEmpty(currentMove.SwingArt) || currentMove.SweepDegrees != 0.0f)
		{
			return CurrentHitboxCentre();
		}
		Vector2 offset = currentMove.HitboxOffset;
		float side = Mathf.Abs(offset.X) < 1.0f ? 0.0f : Mathf.Sign(offset.X) * Facing;
		return GlobalPosition + new Vector2(side * bodySize.X * 0.15f,
			Mathf.Clamp(offset.Y, -bodySize.Y * 0.35f, bodySize.Y * 0.35f));
	}

	/// <summary>Whether a capsule from <paramref name="root"/> to <paramref name="tip"/> touches a box, and where.</summary>
	static bool CapsuleTouches(Vector2 root, Vector2 tip, float radius, Rect2 body, out Vector2 nearest)
	{
		float length = root.DistanceTo(tip);
		int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(6.0f, radius * 0.6f)));
		// From the tip inward, so the point reported is the furthest one along the weapon that hits.
		for (int i = 0; i <= steps; i++)
		{
			Vector2 point = tip.Lerp(root, i / (float)steps);
			nearest = new Vector2(Mathf.Clamp(point.X, body.Position.X, body.End.X), Mathf.Clamp(point.Y, body.Position.Y, body.End.Y));
			if (nearest.DistanceSquaredTo(point) <= radius * radius) return true;
		}
		nearest = tip;
		return false;
	}

	/// <summary>
	/// Hit detection is done inline here rather than with Area2D signals on purpose. Godot's
	/// area overlap callbacks fire after the physics flush and are routinely a frame late,
	/// which is fatal in a game where every window is counted in frames. A capsule-vs-rect test
	/// costs nothing at this scale and resolves on the exact frame the hitbox is live.
	/// </summary>
	void QueryHits()
	{
		// A move with no hitbox of its own - one that only summons or throws - hits nothing here.
		if (currentMove.HitboxRadius <= 0.0f) return;

		Vector2 centre = CurrentHitboxCentre();
		Vector2 root = HitboxRoot();

		foreach (Fighter other in Match.Fighters)
		{
			if (other == this || alreadyHitThisMove.Contains(other)) continue;
			if (!other.CanBeHit) continue;

			if (!CapsuleTouches(root, centre, currentMove.HitboxRadius, other.BodyRect(), out Vector2 nearest)) continue;

			// A multi-hit's early hits are its weak link hit; only the last window launches.
			int activeEnd = currentMove.StartupFrames + currentMove.ActiveFrames;
			MoveData hit = currentMove.LinkHit != null && moveFrame <= activeEnd - currentMove.RehitFrames
				? currentMove.LinkHit
				: currentMove;

			alreadyHitThisMove.Add(other);
			hitSomethingThisMove = true;
			other.ReceiveHit(this, hit, nearest, ChargeScale);

			// "He will also take some damage." Percent only - never knockback.
			Percent += currentMove.SelfDamage;

			// The attacker shares the victim's hitlag, so both sides feel the impact.
			hitlagFrames = Knockback.HitlagFrames(hit.Damage * ChargeScale);
		}
	}

	public Vector2 CurrentHitboxCentre()
	{
		if (currentMove == null) return GlobalPosition;
		if (!string.IsNullOrEmpty(currentMove.SwingArt))
		{
			return HookOrigin() + SwingDirection() * currentMove.SwingLength;
		}
		if (currentMove.SweepDegrees != 0.0f)
		{
			// Round the arc: the same distance from the pivot all the way, the angle moving on.
			Vector2 pivot = FighterAnimations.SweepPivot;
			float reach = (currentMove.HitboxOffset - pivot).Length();
			float a = Mathf.DegToRad(FighterAnimations.SweepAngle(currentMove, pivot,
				FighterAnimations.SweepT(currentMove, moveFrame)));
			return GlobalPosition + new Vector2(pivot.X * Facing + Mathf.Cos(a) * reach * Facing, pivot.Y + Mathf.Sin(a) * reach);
		}
		return GlobalPosition + new Vector2(
			currentMove.HitboxOffset.X * Facing,
			currentMove.HitboxOffset.Y);
	}

	public bool IsHitboxLive =>
		State == FighterState.Attacking
		&& currentMove != null
		&& moveFrame > currentMove.StartupFrames
		&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;

	public float CurrentHitboxRadius => currentMove?.HitboxRadius ?? 0.0f;

	public bool CanBeHitByHazard => CanBeHit;

	bool CanBeHit =>
		invulnFrames <= 0
		&& !IsInvincibleMove
		&& State != FighterState.Eliminated
		&& State != FighterState.Respawning;

	/// <summary>Inside a move's invincible frames (MoveData.InvincibleFrames): nothing can hit or catch him.</summary>
	bool IsInvincibleMove =>
		State == FighterState.Attacking && currentMove != null && currentMove.InvincibleFrames > 0
		&& moveFrame > currentMove.StartupFrames
		&& moveFrame <= currentMove.StartupFrames + currentMove.InvincibleFrames;

	// --- Taking a hit --------------------------------------------------------

	/// <param name="damageScale">More than 1 for a charged smash. Knockback follows damage.</param>
	public void ReceiveHit(Fighter attacker, MoveData move, Vector2 contactPoint, float damageScale = 1.0f)
	{
		ReceiveHitFrom(attacker, attacker.Facing, move, contactPoint, damageScale);
	}

	/// <summary>
	/// A hit from the stage itself - a passing car - which has no fighter behind it, only a
	/// direction it was going. <paramref name="direction"/> is +1 to launch right, -1 left.
	/// </summary>
	public void ReceiveStageHit(MoveData move, int direction, Vector2 contactPoint)
	{
		ReceiveHitFrom(null, direction, move, contactPoint, 1.0f);
	}

	void ReceiveHitFrom(Fighter attacker, int attackerFacing, MoveData move, Vector2 contactPoint, float damageScale)
	{
		// Hitting a bomb sets it off. The hit itself does nothing; the explosion is the answer.
		if (IsBombArmed)
		{
			DetonateBomb();
			return;
		}

		// Hit while holding someone: let go. Hit while held: the hold is over either way - the
		// throw itself arrives as a hit.
		ReleaseGrabbed();
		heldBy = null;

		bool blocked = IsBlocking && !move.Unblockable;

		// A blast in every direction sends people away from it, whichever side they are on.
		if (move.LaunchAway && attacker != null)
		{
			attackerFacing = GlobalPosition.X >= attacker.GlobalPosition.X ? 1 : -1;
		}

		// Blocking reduces; it never negates. Chip damage still raises percent, which is the
		// entire cost of blocking - there is no shield health here by design.
		// A weak block (FighterData.BlockLeak) lets more through - never more than the whole hit.
		float leak = Mathf.Min(1.0f, Tuning.BlockDamageMultiplier * Data.BlockLeak);
		float damage = move.Damage * damageScale * (blocked ? leak : 1.0f);
		Percent += damage;

		if (!blocked && move.BurnFrames > 0) Ignite(move.BurnFrames, move.BurnDamage);
		// Glowing-hot steel: past his heat line, every normal he lands burns a little too.
		else if (!blocked && attacker != null && attacker.HitsHot(move)) Ignite(Heat.HotBurnFrames, Heat.HotBurnDamage);

		// Heat: the one who dealt it warms up a lot, the one who took it a little. Venting is
		// letting heat out, so its own blast never puts any back.
		if (move.Special != SpecialKind.Vent) attacker?.AddHeat(damage * Heat.PerDamageDealt);
		AddHeat(damage * Heat.PerDamageTaken);

		// Hard hat on: a small hit bounces off. The damage still counts - only the flinch is gone.
		if (!blocked && IsArmoredAgainst(damage))
		{
			hitlagFrames = Knockback.HitlagFrames(damage);
			SfxPlayer.At("clang", contactPoint, 0.04f);
			Match.OnHitLanded(attacker, this, contactPoint, 0.0f, damage, true);
			return;
		}

		float knockback = Knockback.Compute(
			Percent, damage, Data.BodyWeight, move.BaseKnockback, move.KnockbackGrowth);

		if (blocked) knockback *= Mathf.Min(1.0f, Tuning.BlockKnockbackMultiplier * Data.BlockLeak);

		Velocity = Knockback.LaunchVelocity(knockback, move.LaunchAngleDegrees, attackerFacing, lastStick);
		hitstunFrames = Knockback.HitstunFrames(knockback);
		hitlagFrames = Knockback.HitlagFrames(damage);
		State = FighterState.Hitstun;
		IsBlocking = false;
		blockedStun = blocked;

		// Flipped like a crepe, or tripped over a cone: one somersault through the hitstun.
		if (!blocked && move.SpinVictim)
		{
			spinVictimTotal = spinVictimFrames = Mathf.Clamp(hitstunFrames, 16, 34);
			spinVictimDir = attackerFacing;
		}
		else
		{
			spinVictimFrames = 0;
		}

		// A fighter who tumbles is knocked over by any real hit, and rolls from the moment it lands.
		if (Data.TumblesWhenHit && !blocked && knockback >= TumbleKnockback)
		{
			drawnAsBall = true;
			pendingTumbleFrames = Mathf.Clamp(Mathf.RoundToInt(knockback * 0.2f), 12, 36);
		}

		// Being launched gives the air jump back, and the recovery: anyone knocked off the stage
		// always has both to come home with, even if they spent them before the hit. Eric's call,
		// 2026-10-04.
		if (!blocked)
		{
			airJumpsUsed = 0;
			helpless = false;
			upSpecialUsed = false;
			usedThisAirtime.Clear();
		}

		if (blocked) shieldFlashFrames = 8;

		if (blocked && attacker != null)
		{
			// Blockstun pushback separates both fighters, so a blocked attack at point blank
			// does not stalemate into a shoving match.
			attacker.Velocity = new Vector2(-attacker.Facing * 260.0f, attacker.Velocity.Y);
		}

		Match.OnHitLanded(attacker, this, contactPoint, knockback, damage, blocked);
	}

	// --- Stocks --------------------------------------------------------------

	public void LoseStock(Vector2 spawnPoint)
	{
		Stocks--;
		Percent = 0.0f;
		Velocity = Vector2.Zero;
		currentMove = null;
		alreadyHitThisMove.Clear();
		hitstunFrames = 0;
		hitlagFrames = 0;
		airJumpsUsed = 0;
		dropThroughFrames = 0;
		dodgeFrames = 0;
		ledgeCooldownFrames = 0;
		tumbleFrames = 0;
		pendingTumbleFrames = 0;
		upSpecialUsed = false;
		helpless = false;
		buildUsed = false;
		usedThisAirtime.Clear();
		ReleaseGrabbed();
		heldBy = null;
		drawnAsBall = false;
		heat = 0.0f;
		framesAtMaxHeat = 0;
		overheatFrames = 0;
		burnFrames = 0;
		whiffExtraFrames = 0;
		airDashFrames = 0;
		spinVictimFrames = 0;
		SetSizeLevel(SizeLevels.Normal);
		CollisionMask = GroundMask;

		if (Stocks <= 0)
		{
			State = FighterState.Eliminated;
			Visible = false;
			return;
		}

		respawnPoint = spawnPoint;
		respawnFreezeFrames = Tuning.RespawnFreezeFrames;
		respawnDelayFrames = Tuning.RespawnDelayFrames;
		Visible = false;
		State = FighterState.Respawning;
		GlobalPosition = spawnPoint;
	}

	// --- Blast zones -----------------------------------------------------------

	/// <summary>
	/// Still flying helplessly from a hit - the only time the side or top blast zone KOs.
	/// Tumbling counts: a knocked-over fighter has no more control than one in hitstun.
	/// </summary>
	public bool IsLaunched => State == FighterState.Hitstun || State == FighterState.Tumbling;

	/// <summary>
	/// Anyone in control of themselves stops at the side and top blast zones, as if at a wall
	/// or a ceiling, so a high jump or a long recovery never KOs and a walk-off floor cannot be
	/// walked off. Done here, straight after the move, rather than by the match a frame later,
	/// so a fighter pressing into the edge does not jitter across it.
	/// </summary>
	void StayInsideBlastZone()
	{
		if (Match == null || IsLaunched) return;
		Rect2 zone = Match.StageBounds;
		Vector2 at = GlobalPosition;
		Vector2 v = Velocity;
		if (at.X < zone.Position.X) { at.X = zone.Position.X; v.X = Mathf.Max(v.X, 0.0f); }
		if (at.X > zone.End.X) { at.X = zone.End.X; v.X = Mathf.Min(v.X, 0.0f); }
		if (at.Y < zone.Position.Y) { at.Y = zone.Position.Y; v.Y = Mathf.Max(v.Y, 0.0f); }
		if (at == GlobalPosition) return;
		GlobalPosition = at;
		Velocity = v;
	}

	/// <summary>Turns to face a way without moving - how a spawn faces into the stage.</summary>
	public void Face(int direction)
	{
		Facing = direction < 0 ? -1 : 1;
		rig?.SetFacing(Facing);
	}

	// --- Helpers -------------------------------------------------------------

	/// <summary>Frames since this fighter started coming down, for the gentle start to a fall.</summary>
	int fallFrames;

	void ApplyGravity(float dt)
	{
		if (airDashFrames > 0 && State == FighterState.Airborne) return;
		if (IsOnFloor() && State != FighterState.Hitstun)
		{
			fallFrames = 0;
			return;
		}

		// Going up is full gravity, so jumps keep their height. Coming down starts soft and
		// builds, and tops out slower - see Tuning.FallSpeedScale.
		float gravity = Data.Gravity * Tuning.GravityScale;
		if (Velocity.Y > 0.0f)
		{
			fallFrames++;
			float ramp = Mathf.Min(1.0f, fallFrames / (float)Tuning.FallRampFrames);
			gravity *= Mathf.Lerp(Tuning.FallStartGravity, 1.0f, ramp);
		}
		else
		{
			fallFrames = 0;
		}

		float maxFall = Data.MaxFallSpeed * Tuning.FallSpeedScale;
		if (Velocity.Y < maxFall)
		{
			Velocity = new Vector2(Velocity.X, Mathf.Min(maxFall, Velocity.Y + gravity * dt));
		}
	}

	void ApplyFriction(float dt, float rate)
	{
		Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0.0f, rate * dt), Velocity.Y);
	}

	public Rect2 BodyRect()
	{
		// Rolled up or curled into a bomb, the thing to hit is the ball on the floor - not the
		// empty space where his legs were. What you can see is what you can hit.
		if (IsDrawnAsBall || IsBombArmed)
		{
			float side = BallRadius() * 2.0f * (IsBombArmed ? 0.62f : 1.0f);
			Vector2 feet = GlobalPosition + new Vector2(0.0f, bodySize.Y * 0.5f);
			return new Rect2(feet - new Vector2(side * 0.5f, side), new Vector2(side, side));
		}

		// Crouched, only the lower part of the body can be hit: high attacks pass over.
		if (State == FighterState.Grounded && IsCrouching)
		{
			float height = bodySize.Y * CrouchHeight;
			return new Rect2(GlobalPosition.X - bodySize.X * 0.5f, GlobalPosition.Y + bodySize.Y * 0.5f - height,
				bodySize.X, height);
		}

		// What can be hit is what is drawn: the box reaches up to the top of the drawing, which
		// stands taller than the body box (see Configure). A hit that visibly lands on someone's
		// head should land. Eric's call, 2026-10-04.
		float drawn = rig != null && rig.Loaded ? Data.BodySize.Y * (1.18f * Data.VisualScale - 1.0f) : 0.0f;
		return new Rect2(GlobalPosition.X - bodySize.X * 0.5f, GlobalPosition.Y - bodySize.Y * 0.5f - drawn,
			bodySize.X, bodySize.Y + drawn);
	}

	public bool IsInvulnerable => invulnFrames > 0;

	/// <summary>Still on the stage and fighting - not respawning and not out of the match.</summary>
	public bool IsInPlay => State != FighterState.Eliminated && State != FighterState.Respawning;

	/// <summary>Whether the air jump is still available. Read by the CPU to plan a recovery.</summary>
	public bool HasAirJump => airJumpsUsed < Data.AirJumps;

	/// <summary>The move in progress, or null. Read by the CPU to see an attack coming.</summary>
	public MoveData CurrentMove => State == FighterState.Attacking ? currentMove : null;

	/// <summary>
	/// Running fast enough on the ground that pressing attack now would give the dash attack. Read
	/// by the CPU, so it knows which move a press will actually throw.
	/// </summary>
	public bool AttackWouldDash => IsOnFloor() && Mathf.Abs(Velocity.X) > RunSpeed * DashThreshold;

	/// <summary>Standing on a soft platform, so down and jump drops through it. Read by the CPU.</summary>
	public bool OnSoftPlatform => IsOnFloor() && standingOnOneWay;

	/// <summary>
	/// Where a beam comes out: his hands, held out in front of the body. It is asked every frame,
	/// so a beam fired in the air follows him down.
	/// </summary>
	public Vector2 BeamOrigin()
	{
		// Charging, the move in hand says where; once fired, the beam keeps the spot it left from -
		// and follows it, so a laser from the eye moves with his head.
		string from = State == FighterState.Attacking && currentMove != null && currentMove.Beam
			? currentMove.BeamFrom
			: activeBeamFrom;
		Vector2? spot = rig != null && rig.Loaded ? rig.PointGlobal(from) : null;
		if (spot.HasValue) return spot.Value;
		return GlobalPosition + new Vector2(Facing * bodySize.X * 0.45f, -bodySize.Y * 0.05f);
	}

	/// <summary>
	/// The charge before a beam: a glow at his hands that grows until it fires, so a player can
	/// see it coming and get out of the way. That warning is what makes a long beam fair.
	/// </summary>
	void DrawBeamCharge()
	{
		if (State != FighterState.Attacking || currentMove == null) return;
		if (!currentMove.Beam && !currentMove.ChargeWithSpecial) return;
		if (moveFrame > currentMove.StartupFrames) return;
		// Curled into a ball, there are no hands to glow - the fireball round him is the charge.
		if (currentMove.BallForm) return;

		// A charged shot grows with the charge, up to the size it will fly at.
		float t = moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames);
		if (currentMove.ChargeWithSpecial)
		{
			float charge = chargeFrames / (float)MaxChargeFrames;
			t = (0.5f + 0.5f * t) * (1.0f + (currentMove.ChargeSize - 1.0f) * charge) * 0.8f;
		}
		Vector2 at = BeamOrigin() - GlobalPosition;
		float radius = currentMove.FxRadius * SizeLevels.ProjectileSize(sizeLevel) * (0.3f + 0.9f * t);
		float pulse = 1.0f + 0.12f * Mathf.Sin(moveFrame * 1.3f);

		Color glow = currentMove.FxColor;
		glow.A = 0.35f;
		DrawCircle(at, radius * 1.6f * pulse, glow);
		glow.A = 0.9f;
		DrawCircle(at, radius * pulse, glow);
		DrawCircle(at, radius * 0.45f * pulse, new Color(1.0f, 0.95f, 0.85f, 0.95f));
	}
	public bool IsInHitlag => hitlagFrames > 0;

	// --- Size ------------------------------------------------------------------

	/// <summary>
	/// Holding special keeps the stance open; each fresh push of the stick changes size by one
	/// step, up to grow and down to shrink. Letting go of special ends it straight away.
	/// </summary>
	void TickResize(InputState input, int activeEnd)
	{
		if (!input.SpecialHeld)
		{
			moveFrame = activeEnd;
			return;
		}

		int dir = input.Move.Y < -0.5f ? 1 : input.Move.Y > 0.5f ? -1 : 0;
		if (dir != 0 && dir != resizeStickDir)
		{
			SetSizeLevel(Mathf.Clamp(sizeLevel + dir, SizeLevels.Short, SizeLevels.Tall));
		}
		resizeStickDir = dir;
	}

	/// <summary>
	/// Changes size by stretching the legs. The hurtbox grows with the drawing - a tall fighter
	/// really is easier to hit - and the body moves so the feet stay exactly where they were.
	/// </summary>
	void SetSizeLevel(int level)
	{
		if (bodyShape == null) return;
		sizeLevel = level;

		float stretch = SizeLevels.LegStretch(level);
		float growth = rig != null && rig.Loaded
			? rig.LegGrowth(stretch)
			: Data.BodySize.Y * 0.4f * (stretch - 1.0f);

		float oldHeight = bodySize.Y;
		bodySize = new Vector2(Data.BodySize.X, Data.BodySize.Y + growth * 0.9f);
		ShapeBody();
		GlobalPosition -= new Vector2(0.0f, (bodySize.Y - oldHeight) * 0.5f);

		rig?.SetLegStretch(stretch, bodySize.Y * 0.5f);
	}

	public int SizeLevel => sizeLevel;

	/// <summary>
	/// What he collides with the stage as: the body box, narrowed at the bottom to the width his
	/// feet stand at. He stands on his feet, so at a ledge he stays up only while his feet are
	/// over it - with a plain box, Lug stood on the stage with both feet drawn off the end of it
	/// (Eric, 2026-10-04). The narrowing is steeper than anything walkable, so past the edge he
	/// slides off it rather than resting on it. Hits still use the plain box (BodyRect).
	/// </summary>
	void ShapeBody()
	{
		float half = bodySize.X * 0.5f;
		float foot = Mathf.Clamp(footWidth, bodySize.X * 0.3f, bodySize.X) * 0.5f;
		float top = -bodySize.Y * 0.5f;
		float bottom = bodySize.Y * 0.5f;
		// Steep enough to be a wall, not a floor (more than 45 degrees), and never above the waist.
		float taper = Mathf.Min((half - foot) * 1.6f, bodySize.Y * 0.4f);
		bodyShape.Points = new[]
		{
			new Vector2(-half, top), new Vector2(half, top),
			new Vector2(half, bottom - taper), new Vector2(foot, bottom),
			new Vector2(-foot, bottom), new Vector2(-half, bottom - taper),
		};
	}

	/// <summary>
	/// While he holds his stretch, an arrow over his head and one under his feet say "push up to
	/// grow, down to shrink" - the stance is otherwise a fighter standing still with no clue what
	/// it wants. An arrow he cannot go any further in is faded. They nudge the way they point, so
	/// they read as "push this way" rather than as part of the stage, and they carry the HUD's
	/// paper outline rather than an ink one: an outline in this game means something real.
	/// </summary>
	void DrawResizeHint()
	{
		if (State != FighterState.Attacking || currentMove == null || currentMove.Special != SpecialKind.Resize) return;
		if (moveFrame < currentMove.StartupFrames) return;

		float half = bodySize.Y * 0.5f;
		// The drawing stands taller than the body box (see Configure), and grows with the legs.
		float growth = (bodySize.Y - Data.BodySize.Y) / 0.9f;
		float top = half - Data.BodySize.Y * 1.18f * Data.VisualScale - growth;
		float nudge = 5.0f * Mathf.Sin(moveFrame * 0.22f);
		Color fill = currentMove.FxColor;
		DrawResizeArrow(new Vector2(0.0f, top - 36.0f - nudge), -1, fill, sizeLevel < SizeLevels.Tall);
		DrawResizeArrow(new Vector2(0.0f, half + 40.0f + nudge), 1, fill, sizeLevel > SizeLevels.Short);
	}

	/// <summary>A chunky arrow, pointing up (-1) or down (1), centred on <paramref name="at"/>.</summary>
	void DrawResizeArrow(Vector2 at, int down, Color fill, bool live)
	{
		const float Size = 26.0f;
		Vector2[] shape =
		{
			new Vector2(0.0f, -1.0f), new Vector2(0.9f, -0.05f), new Vector2(0.38f, -0.05f),
			new Vector2(0.38f, 0.85f), new Vector2(-0.38f, 0.85f), new Vector2(-0.38f, -0.05f),
			new Vector2(-0.9f, -0.05f),
		};
		float alpha = live ? 1.0f : 0.35f;
		var outline = new Vector2[shape.Length];
		var arrow = new Vector2[shape.Length];
		for (int i = 0; i < shape.Length; i++)
		{
			Vector2 p = new Vector2(shape[i].X, shape[i].Y * -down);
			outline[i] = at + p * (Size + 8.0f);
			arrow[i] = at + p * Size;
		}
		DrawColoredPolygon(outline, new Color(0.99f, 0.98f, 0.95f, alpha));
		DrawColoredPolygon(arrow, new Color(fill.R, fill.G, fill.B, alpha));
	}

	/// <summary>
	/// A projectile at this fighter's current size. One cached copy per move per size, so firing
	/// never allocates - a Resource per shot is the kind of leak FighterCatalog already hit once.
	/// </summary>
	MoveData SizedFor(MoveData move)
	{
		if (sizeLevel == SizeLevels.Normal) return move;

		float size = SizeLevels.ProjectileSize(sizeLevel);
		if (!sizedMoves.TryGetValue(move, out MoveData sized)
			|| !Mathf.IsEqualApprox(sized.FxRadius, move.FxRadius * size))
		{
			sized = move.Sized(size, SizeLevels.ProjectileDamage(sizeLevel));
			sizedMoves[move] = sized;
		}
		return sized;
	}

	/// <summary>Charged copies of a projectile, one per charge step, made once and kept.</summary>
	readonly Dictionary<(MoveData, int), MoveData> chargedMoves = new Dictionary<(MoveData, int), MoveData>();
	const int ChargeSteps = 6;

	/// <summary>
	/// A projectile at this charge: bigger and harder-hitting the longer special was held. The
	/// charge is rounded to a few steps, so each size is built once and reused rather than a new
	/// Resource per shot.
	/// </summary>
	MoveData ChargedFor(MoveData move)
	{
		int step = Mathf.RoundToInt(ChargeSteps * chargeFrames / (float)MaxChargeFrames);
		if (step == 0) return move;
		if (!chargedMoves.TryGetValue((move, step), out MoveData charged))
		{
			float t = step / (float)ChargeSteps;
			charged = move.Sized(1.0f + (move.ChargeSize - 1.0f) * t, 1.0f + (move.ChargeDamage - 1.0f) * t);
			chargedMoves[(move, step)] = charged;
		}
		return charged;
	}

	/// <summary>
	/// Flying: after a short take-off, rise steadily for the whole active window, steering left
	/// and right with the stick. Slower than a launch, but it goes on for a long time and it can
	/// be aimed back at the stage.
	/// </summary>
	void TickFlight(InputState input, bool active, float dt)
	{
		if (!active)
		{
			if (moveFrame <= currentMove.StartupFrames)
			{
				Velocity = new Vector2(Velocity.X * 0.85f, Mathf.Min(Velocity.Y * 0.7f, 60.0f));
			}
			return;
		}

		float steer = input.Move.X * Data.AirSpeed * 0.8f;
		if (Mathf.Abs(input.Move.X) > 0.3f) Facing = input.Move.X > 0.0f ? 1 : -1;
		Velocity = new Vector2(
			Mathf.MoveToward(Velocity.X, steer, Data.AirAcceleration * dt),
			-currentMove.SpecialRise);
	}

	// --- Grappling hook ----------------------------------------------------------

	/// <summary>
	/// Hangs through startup while the hook flies out, then yanks him along it on the first
	/// active frame. The hook never has to catch on anything - a recovery that can miss loses
	/// stocks for reasons a player cannot see.
	/// </summary>
	/// <summary>Which way a stick-aimed tether is going (see MoveData.StickAimed).</summary>
	Vector2 tetherAim = Vector2.Up;

	/// <summary>
	/// Through the startup a stick-aimed tether follows the stick, anywhere from about 30 degrees
	/// above level on one side, over the top, to the same on the other - so it stays a way up,
	/// never a flat dash across the stage or a throw into the floor - and turns him to face the
	/// way it goes. Left alone, it keeps the way it was aimed.
	/// </summary>
	void AimTether(InputState input)
	{
		Vector2 stick = input.Move;
		if (stick.Length() < 0.35f) return;
		float angle = Mathf.Atan2(Mathf.Min(stick.Y, -0.6f * stick.Length()), stick.X);
		tetherAim = Vector2.Right.Rotated(angle);
		if (Mathf.Abs(tetherAim.X) > 0.1f) Facing = tetherAim.X > 0.0f ? 1 : -1;
	}

	void TickGrapple(InputState input, int activeStart)
	{
		if (currentMove.StickAimed && !hookFired && moveFrame <= activeStart) AimTether(input);

		// A swung drawing he lets go of drops away at the end of the pull.
		int activeEnd = activeStart + currentMove.ActiveFrames;
		if (currentMove.ReleaseDrop != null && !swingReleased && moveFrame >= activeEnd && Match != null)
		{
			swingReleased = true;
			Vector2 ball = HookOrigin() + SwingDirection() * currentMove.SwingLength;
			Match.SpawnHazard(this, currentMove.ReleaseDrop, ball,
				new Vector2(Velocity.X * 0.3f, currentMove.ReleaseDrop.SpecialSpeed));
		}

		if (moveFrame <= activeStart)
		{
			Velocity = new Vector2(Velocity.X * 0.8f, Mathf.Min(Velocity.Y * 0.8f, 120.0f));
			if (currentMove.GrabThrow != null) TickGrab(activeStart);
			return;
		}

		if (hookFired) return;
		hookFired = true;

		// The throw goes on the same frame as the launch: one yank flings them down and him up.
		if (grabbed != null) ThrowGrabbed(currentMove.GrabThrow);

		Vector2 pull = new Vector2(Facing * currentMove.SpecialSpeed, -currentMove.SpecialRise);
		if (currentMove.StickAimed) pull = tetherAim * pull.Length();
		hookPoint = HookOrigin() + pull.Normalized() * currentMove.TetherLength;
		Velocity = pull;
	}

	Vector2 HookOrigin() => currentMove != null && currentMove.HangFromArt
		? GlobalPosition + new Vector2(Facing * bodySize.X * 0.1f, -bodySize.Y * 0.75f)
		: GlobalPosition + new Vector2(Facing * bodySize.X * 0.3f, -bodySize.Y * 0.15f);

	Vector2 TetherDirection() => currentMove.StickAimed
		? tetherAim
		: new Vector2(Facing * currentMove.SpecialSpeed, -currentMove.SpecialRise).Normalized();

	/// <summary>How far out the tether is through the startup, 0 to 1.</summary>
	float TetherReach() => Mathf.Clamp(moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames), 0.0f, 1.0f);

	/// <summary>
	/// The tether's tip catches the first fighter it touches, and reels them in to his hand over
	/// the rest of the startup. This is the roster's one command grab (see
	/// .ai/character-design.md): it goes through blocking, and there is only ever one.
	/// </summary>
	void TickGrab(int activeStart)
	{
		if (grabbed == null)
		{
			CatchAt(HookOrigin() + TetherDirection() * currentMove.TetherLength * TetherReach());
			return;
		}

		float t = (moveFrame - grabFrame) / (float)Mathf.Max(1, activeStart - grabFrame);
		grabbed.GlobalPosition = grabPoint.Lerp(GrabHand(), t * t);
	}

	/// <summary>Where a reeled-in fighter ends up: just in front of the hand.</summary>
	Vector2 GrabHand() => GrabShoulder(RigBone.ArmFrontUpper) + new Vector2(Facing * bodySize.X * 0.7f, 0.0f);

	/// <summary>
	/// A command grab's arms come out of the drawing's own shoulders when it has them - DoomBot's
	/// sit high on his boxy body - and from the usual hook point otherwise. The tether recovery
	/// keeps <see cref="HookOrigin"/>, which its timing was tuned against.
	/// </summary>
	Vector2 GrabShoulder(RigBone upperArm)
	{
		if (currentMove == null || currentMove.Special != SpecialKind.CommandGrab && currentMove.Special != SpecialKind.Grab
			&& currentMove != grabKick) return HookOrigin();
		return rig?.JointGlobal(upperArm) ?? HookOrigin();
	}

	/// <summary>Seizes the first fighter touching <paramref name="tip"/>, if any. Goes through blocking.</summary>
	void CatchAt(Vector2 tip, float Reach = 36.0f)
	{
		foreach (Fighter other in Match.Fighters)
		{
			if (other == this || !other.CanBeHit || other.heldBy != null) continue;
			// A bomb is not something you pick up.
			if (other.CurrentMove != null && other.CurrentMove.Special == SpecialKind.Bomb) continue;

			Rect2 body = other.BodyRect();
			Vector2 nearest = new Vector2(
				Mathf.Clamp(tip.X, body.Position.X, body.End.X),
				Mathf.Clamp(tip.Y, body.Position.Y, body.End.Y));
			if (nearest.DistanceSquaredTo(tip) > Reach * Reach) continue;

			grabbed = other;
			grabPoint = other.GlobalPosition;
			grabFrame = moveFrame;
			hitSomethingThisMove = true;
			other.BeginHeld(this);
			return;
		}
	}

	// --- Command grab ----------------------------------------------------------------

	/// <summary>
	/// The arms shoot straight out through the startup and catch the first fighter they touch;
	/// through the active frames a catch is reeled in to his hands, and an empty grab pulls the
	/// arms back. Reeled all the way in, the move hands over to its follow-up - the kick - which
	/// plays as a move of its own with the victim held in front of him until it lands. Returns
	/// true when it has handed over.
	/// </summary>
	bool TickCommandGrab(int activeStart, int activeEnd)
	{
		// He stays where he is; the arms do the travelling.
		Velocity = IsOnFloor()
			? new Vector2(0.0f, Velocity.Y)
			: new Vector2(Velocity.X * 0.85f, Mathf.Min(Velocity.Y * 0.8f, 120.0f));

		if (moveFrame <= activeStart)
		{
			if (grabbed == null) CatchAt(GrabTip());
			return false;
		}

		if (grabbed == null) return false;

		float t = (moveFrame - activeStart) / (float)Mathf.Max(1, currentMove.ActiveFrames);
		grabbed.GlobalPosition = grabPoint.Lerp(GrabHand(), t * t);

		if (moveFrame < activeEnd || currentMove.GrabThrow == null) return false;

		grabKick = currentMove.GrabThrow;
		currentMove = grabKick;
		moveFrame = 0;
		chargeFrames = 0;
		whiffExtraFrames = 0;
		CueMoveSound(currentMove);
		return true;
	}

	/// <summary>
	/// Where a command grab's hands are: out along the ground in front through the startup, on
	/// whoever they caught, or coming back empty through the active frames.
	/// </summary>
	Vector2 GrabTip()
	{
		if (grabbed != null) return grabbed.GlobalPosition;
		int activeStart = currentMove.StartupFrames;
		float reach = moveFrame <= activeStart
			? moveFrame / (float)Mathf.Max(1, activeStart)
			: 1.0f - (moveFrame - activeStart) / (float)Mathf.Max(1, currentMove.ActiveFrames);
		// Out and down from the shoulder to the middle of an ordinary fighter: a giant's arms
		// held level would pass clean over everyone's head.
		Vector2 shoulder = GrabShoulder(RigBone.ArmFrontUpper);
		var end = new Vector2(shoulder.X + Facing * currentMove.TetherLength,
			Mathf.Max(shoulder.Y, GlobalPosition.Y + bodySize.Y * 0.5f - GrabHeight));
		return shoulder.Lerp(end, Mathf.Clamp(reach, 0.0f, 1.0f));
	}

	/// <summary>How far off the floor a command grab's hands go out to: about the middle of everyone.</summary>
	const float GrabHeight = 70.0f;

	bool IsCommandGrabbing =>
		State == FighterState.Attacking && currentMove != null && currentMove.Special == SpecialKind.CommandGrab
		&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;

	/// <summary>Both arms stretched from the shoulders out to the hands - the back one a little behind.</summary>
	void DrawGrabArms()
	{
		if (!IsCommandGrabbing) return;
		Vector2 tip = GrabTip() - GlobalPosition;
		Vector2 front = GrabShoulder(RigBone.ArmFrontUpper) - GlobalPosition;
		Vector2 back = GrabShoulder(RigBone.ArmBackUpper) - GlobalPosition;
		// The two claws close on the catch from just above and just below, like a pincer.
		DrawStretchedArm(back, tip + new Vector2(0.0f, -bodySize.Y * 0.08f), RigBone.ArmBackLower);
		DrawStretchedArm(front, tip + new Vector2(0.0f, bodySize.Y * 0.04f), RigBone.ArmFrontLower);
	}

	void ThrowGrabbed(MoveData throwMove)
	{
		Fighter victim = grabbed;
		grabbed = null;
		victim.heldBy = null;

		// The launch that follows must not hit them a second time.
		alreadyHitThisMove.Add(victim);
		hitSomethingThisMove = true;
		victim.ReceiveHit(this, throwMove, victim.GlobalPosition);
		hitlagFrames = Knockback.HitlagFrames(throwMove.Damage);

		// Something fired after them as they go - EdgeLord's sword up after an up throw.
		if (throwMove.FollowShot != null && Match != null)
		{
			float a = Mathf.DegToRad(throwMove.LaunchAngleDegrees);
			var along = new Vector2(Mathf.Cos(a) * Facing, -Mathf.Sin(a));
			Match.SpawnHazard(this, throwMove.FollowShot, GrabHand(), along * throwMove.FollowShot.SpecialSpeed);
		}
	}

	/// <summary>
	/// A throw's link hit on someone it is still holding: it lands, and they are held again where
	/// they were - pinned to the floor under DoomBot's jets, or under a sledgehammer.
	/// </summary>
	void PinHit(MoveData link)
	{
		Fighter victim = grabbed;
		Vector2 at = victim.GlobalPosition;
		alreadyHitThisMove.Add(victim);
		hitSomethingThisMove = true;
		victim.ReceiveHit(this, link, at);
		grabbed = victim;
		victim.BeginHeld(this);
		victim.GlobalPosition = at;
		hitlagFrames = Knockback.HitlagFrames(link.Damage);
	}

	// --- Grabs --------------------------------------------------------------------------

	/// <summary>
	/// Where someone being thrown is held until the throw lets go: in his hand - or, for a down
	/// throw, down on the floor in front of his feet, pinned there (DoomBot's jets burn them
	/// where they lie).
	/// </summary>
	Vector2 ThrowHold()
	{
		if (Data.Throws.Length == 4 && currentMove == Data.Throws[3] && grabbed != null)
		{
			Vector2 victim = grabbed.bodySize;
			return new Vector2(GlobalPosition.X + Facing * (bodySize.X * 0.3f + victim.X * 0.5f),
				GlobalPosition.Y + bodySize.Y * 0.5f - victim.Y * 0.5f);
		}
		return GrabHand();
	}

	/// <summary>How long he holds someone waiting for a throw to be picked, before throwing forward.</summary>
	const int GrabHoldFrames = 40;
	int grabHoldFrames;

	/// <summary>
	/// Where a grab catches: just past the front of his body, at about the middle of an ordinary
	/// fighter - so a giant's grab still finds someone his chest is above.
	/// </summary>
	Vector2 GrabReach() => GlobalPosition + new Vector2(Facing * (bodySize.X * 0.5f + 20.0f),
		bodySize.Y * 0.5f - Mathf.Min(bodySize.Y * 0.55f, 72.0f));

	/// <summary>
	/// A grab: reaching out through the active frames for anyone right in front. Caught, he holds
	/// them in front of him (the pose held at the reach) until the stick picks a throw, or throws
	/// forward when the hold runs out; the throw then plays as a move of its own with them in his
	/// hand until it lands. Back turns him round to throw the other way. True on the hand-over.
	/// </summary>
	bool TickGrab(InputState input, int activeStart, int activeEnd)
	{
		if (IsOnFloor()) Velocity = new Vector2(0.0f, Velocity.Y);
		if (grabbed == null)
		{
			if (moveFrame > activeStart && moveFrame <= activeEnd)
			{
				CatchAt(GrabReach(), currentMove.HitboxRadius);
				if (grabbed != null) grabHoldFrames = GrabHoldFrames;
			}
			return false;
		}

		moveFrame = activeStart + 1;
		grabbed.GlobalPosition = GrabHand();

		int pick = -1;
		if (input.Move.Y < -0.5f) pick = 2;
		else if (input.Move.Y > 0.5f) pick = 3;
		else if (input.Move.X * Facing > 0.5f) pick = 0;
		else if (input.Move.X * Facing < -0.5f) pick = 1;
		else if (attackBufferFrames > 0) pick = 0;
		if (pick < 0 && --grabHoldFrames > 0) return false;
		attackBufferFrames = 0;
		if (pick < 0) pick = 0;

		if (pick == 1) Facing = -Facing;
		grabKick = Data.Throws[pick];
		currentMove = grabKick;
		moveFrame = 0;
		chargeFrames = 0;
		whiffExtraFrames = 0;
		alreadyHitThisMove.Clear();
		CueMoveSound(currentMove);
		return true;
	}

	/// <summary>Lets go of whoever this fighter is holding, without throwing them.</summary>
	void ReleaseGrabbed()
	{
		if (grabbed == null) return;
		Fighter victim = grabbed;
		grabbed = null;
		if (IsInstanceValid(victim) && victim.heldBy == this) victim.EndHeld();
	}

	/// <summary>Caught on a tether. Whatever they were doing stops.</summary>
	void BeginHeld(Fighter by)
	{
		ReleaseGrabbed();
		heldBy = by;
		heldFrames = MaxHeldFrames;
		currentMove = null;
		alreadyHitThisMove.Clear();
		IsBlocking = false;
		IsCrouching = false;
		Velocity = Vector2.Zero;
		hitstunFrames = 1;
		State = FighterState.Hitstun;
	}

	/// <summary>Let go without a throw: a moment of hitstun, then control back.</summary>
	void EndHeld()
	{
		heldBy = null;
		hitstunFrames = Mathf.Max(hitstunFrames, 10);
		State = FighterState.Hitstun;
	}

	void TickHeld()
	{
		Velocity = Vector2.Zero;
		if (--heldFrames <= 0 || !IsInstanceValid(heldBy) || heldBy.grabbed != this
			|| heldBy.State != FighterState.Attacking)
		{
			EndHeld();
		}
	}

	/// <summary>
	/// A crayon arc through where a normal attack hits, for its active frames and a few after,
	/// fading. It is what makes an up tilt read as "up" and a back air as "behind" at a glance:
	/// the pose says what the body did, the trail says where the hit went.
	/// </summary>
	void DrawSwingTrail()
	{
		if (State != FighterState.Attacking || currentMove == null) return;
		if (currentMove.Special != SpecialKind.None || currentMove.HitboxRadius <= 0.0f || currentMove.Spin) return;

		int activeStart = currentMove.StartupFrames;
		int activeEnd = activeStart + currentMove.ActiveFrames;
		const int Linger = 5;
		if (moveFrame <= activeStart || moveFrame > activeEnd + Linger) return;

		float fade = moveFrame <= activeEnd ? 1.0f : 1.0f - (moveFrame - activeEnd) / (float)(Linger + 1);
		var pivot = new Vector2(0.0f, -bodySize.Y * 0.12f);
		Vector2 hit = new Vector2(currentMove.HitboxOffset.X * Facing, currentMove.HitboxOffset.Y);
		Vector2 toHit = hit - pivot;
		float reach = Mathf.Max(toHit.Length(), currentMove.HitboxRadius);
		float angle = toHit.LengthSquared() > 1.0f ? toHit.Angle() : -Mathf.Pi * 0.5f;

		// A little over a quarter turn of arc, centred on the hit, in the fighter's own colour. It
		// is a smear, not a shape: strongest along the middle of the swing and fading to nothing
		// at both ends of the arc and at its inner and outer edges, so it shows the direction of
		// the hit without a hard-edged band covering the fighter or the one being hit.
		float Sweep = 0.9f;

		// A sweeping hitbox smears along everything it has swept so far, from where the swing
		// started to where the hitbox is now, round its own pivot.
		if (currentMove.SweepDegrees != 0.0f)
		{
			Vector2 sweepPivot = new Vector2(FighterAnimations.SweepPivot.X * Facing, FighterAnimations.SweepPivot.Y);
			pivot = sweepPivot;
			reach = (currentMove.HitboxOffset - FighterAnimations.SweepPivot).Length();
			float t = FighterAnimations.SweepT(currentMove, moveFrame);
			float from = FighterAnimations.SweepAngle(currentMove, FighterAnimations.SweepPivot, 0.0f);
			float to = FighterAnimations.SweepAngle(currentMove, FighterAnimations.SweepPivot, t);
			// Mirrored when facing left: a screen angle a becomes 180 - a.
			if (Facing < 0)
			{
				from = 180.0f - from;
				to = 180.0f - to;
			}
			angle = Mathf.DegToRad((from + to) * 0.5f);
			Sweep = Mathf.Max(0.15f, Mathf.Abs(Mathf.DegToRad(to - from)) * 0.5f);
		}
		const int Along = 14;
		const int Across = 4;
		float width = currentMove.HitboxRadius * 0.6f;
		float band = width / Across;
		Color colour = Data.TrailColor.A > 0.0f ? Data.TrailColor : Data.PlaceholderColor;

		for (int i = 0; i < Along; i++)
		{
			float a0 = angle - Sweep + 2.0f * Sweep * i / Along;
			float a1 = angle - Sweep + 2.0f * Sweep * (i + 1) / Along;
			float lengthwise = Mathf.Sin(Mathf.Pi * (i + 0.5f) / Along);

			for (int j = 0; j < Across; j++)
			{
				float across = Mathf.Sin(Mathf.Pi * (j + 0.5f) / Across);
				colour.A = 0.34f * fade * lengthwise * lengthwise * across;
				float r = reach - width * 0.5f + band * (j + 0.5f);
				DrawArc(pivot, r, a0, a1, 3, colour, band + 0.5f);
			}
		}
	}

	/// <summary>
	/// A drawing held out in front for the whole move - the wheelbarrow.
	/// </summary>
	void DrawHeldArt()
	{
		if (State != FighterState.Attacking || currentMove == null || string.IsNullOrEmpty(currentMove.HeldArt)) return;
		// A spin holds its blades out and turns them itself - see DrawSpinBlades.
		if (currentMove.Spin) return;
		RigArt art = rig?.PoseArtFor(currentMove.HeldArt);
		if (art == null) return;

		Vector2 size = art.Texture.GetSize();
		float s = currentMove.HeldArtSize / Mathf.Max(size.X, size.Y);
		Vector2 at = new Vector2(currentMove.HeldArtOffset.X * Facing, currentMove.HeldArtOffset.Y);
		var scale = new Vector2(s * Facing, s);
		if (currentMove.Flight)
		{
			// Two wings, each its own half of the drawing, hinged at his back: they swing open
			// through the take-off, then beat - down together, and back up - the whole flight.
			float open = Mathf.Min(1.0f, moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames));
			float beat = Mathf.Sin(moveFrame * 0.42f);
			float swing = Mathf.DegToRad(-50.0f * (1.0f - open) + 22.0f * beat);
			Vector2 anchor = art.Anchor;
			for (int side = -1; side <= 1; side += 2)
			{
				// Left half of the drawing (side -1) turns one way, right half the other.
				var region = side < 0
					? new Rect2(0.0f, 0.0f, anchor.X, size.Y)
					: new Rect2(anchor.X, 0.0f, size.X - anchor.X, size.Y);
				float turn = -side * swing;
				DrawSetTransformMatrix(new Transform2D(turn, new Vector2(s * Facing, s * (0.6f + 0.4f * open)), 0.0f, at));
				DrawTextureRectRegion(art.Texture, new Rect2(region.Position - anchor, region.Size), region, rig.Modulate);
			}
			DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
			return;
		}
		DrawArtTransform(at, 0.0f, scale);
		DrawTexture(art.Texture, -art.Anchor, rig.Modulate);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>
	/// A ball on a chain from the hand. Through the startup it swings from low behind him, up and
	/// over; once the move is live it leads the way, up and forward, which is the direction the
	/// launch then pulls him. The hitbox rides on the ball.
	/// </summary>
	void DrawSwing()
	{
		if (State != FighterState.Attacking || currentMove == null || string.IsNullOrEmpty(currentMove.SwingArt)) return;
		// Let go of: it is falling on its own now, as a hazard.
		if (swingReleased) return;
		RigArt art = rig?.PoseArtFor(currentMove.SwingArt);
		if (art == null) return;

		Vector2 hand = HookOrigin() - GlobalPosition;
		Vector2 ball = hand + SwingDirection() * currentMove.SwingLength;
		// A steel cable, in yellow-and-grey like everything else on the site.
		CrayonBrush.InkLine(this, hand, ball, new Color(0.30f, 0.30f, 0.34f), 7.0f, 61, 1.0f);
		CrayonBrush.InkLine(this, hand, ball, new Color(0.98f, 0.78f, 0.20f), 3.0f, 62, 1.0f);

		Vector2 size = art.Texture.GetSize();
		float s = currentMove.SwingArtSize / Mathf.Max(size.X, size.Y);
		DrawArtTransform(ball, 0.0f, new Vector2(s * Facing, s));
		DrawTexture(art.Texture, -art.Anchor, rig.Modulate);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>Where the swung ball is, as a direction from the hand.</summary>
	Vector2 SwingDirection()
	{
		// Angles in degrees, measured so 0 points forward and -90 points straight up.
		const float Start = 150.0f;  // low and behind
		// Up and forward - or, for one he hangs from, straight up over his head.
		float Lead = currentMove.HangFromArt ? -90.0f : -70.0f;
		float t = Mathf.Clamp(moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames), 0.0f, 1.0f);
		float angle = Mathf.DegToRad(Mathf.Lerp(Start, Lead, t * t));
		return new Vector2(Mathf.Cos(angle) * Facing, Mathf.Sin(angle));
	}

	void DrawTether()
	{
		if (State != FighterState.Attacking || currentMove == null || !currentMove.DelayedLaunch) return;
		if (!string.IsNullOrEmpty(currentMove.SwingArt)) return;
		if (moveFrame > currentMove.StartupFrames + currentMove.ActiveFrames) return;

		Vector2 dir = TetherDirection();
		float reach = Mathf.Clamp(moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames), 0.0f, 1.0f);
		Vector2 tip = hookFired ? hookPoint : HookOrigin() + dir * currentMove.TetherLength * reach;
		if (grabbed != null) tip = grabbed.GlobalPosition;

		Vector2 from = HookOrigin() - GlobalPosition;
		Vector2 to = tip - GlobalPosition;

		if (currentMove.StretchArm && DrawStretchedArm(from, to, RigBone.ArmFrontLower)) return;
		if (!string.IsNullOrEmpty(currentMove.TetherArt) && DrawChainTether(from, to, currentMove.TetherArt)) return;

		// Elim drew the rope, the gun and the hook. When they are there, those are what fly.
		RigArt ropeArt = rig?.PoseArtFor("rope");
		RigArt gunArt = rig?.PoseArtFor("gun");
		RigArt hookArt = rig?.PoseArtFor("hook");
		if (ropeArt != null && gunArt != null && hookArt != null)
		{
			float angle = (to - from).Angle();
			Vector2 ropeSize = ropeArt.Texture.GetSize();
			DrawArtTransform(from, angle, new Vector2(from.DistanceTo(to) / ropeSize.X, 14.0f / ropeSize.Y));
			DrawTexture(ropeArt.Texture, new Vector2(0.0f, -ropeSize.Y * 0.5f));
			DrawArtAt(gunArt, from, angle, 46.0f);
			DrawArtAt(hookArt, to, angle, 46.0f);
			DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
			return;
		}

		var rope = new Color(0.24f, 0.26f, 0.32f);
		CrayonBrush.InkLine(this, from, to, rope, 4.0f, 17, 1.2f);

		// The hook itself: a small barbed V at the tip, pointing the way it flew.
		Vector2 back = -dir * 18.0f;
		CrayonBrush.InkLine(this, to, to + back.Rotated(0.6f), rope, 5.0f, 23, 0.8f);
		CrayonBrush.InkLine(this, to, to + back.Rotated(-0.6f), rope, 5.0f, 29, 0.8f);
	}

	/// <summary>
	/// A drawing fitted to <paramref name="size"/> pixels and turned to <paramref name="angle"/>.
	/// Flipped top-to-bottom when facing left, so a gun drawn grip-down stays grip-down.
	/// </summary>
	void DrawArtAt(RigArt art, Vector2 at, float angle, float size)
	{
		Vector2 texSize = art.Texture.GetSize();
		float s = size / Mathf.Max(texSize.X, texSize.Y);
		DrawArtTransform(at, angle, new Vector2(s, s * Facing));
		DrawTexture(art.Texture, -art.Anchor);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>
	/// Scale in the drawing's own axes, THEN turn it. DrawSetTransform scales after rotating, so
	/// a rope stretched along its length came out skewed off its angle and too short, and a
	/// mirrored hook turned the wrong way.
	/// </summary>
	void DrawArtTransform(Vector2 at, float angle, Vector2 scale)
	{
		DrawSetTransformMatrix(new Transform2D(angle, scale, 0.0f, at));
	}

	// --- EdgeLord: choosing a sword, blinking, spinning ----------------------------

	/// <summary>
	/// Four swords laid out round him; the first push of the stick picks one. Every pick flies
	/// the way he was facing - back only chooses the sword behind him, it does not turn him. A
	/// tap with no push throws the forward one when the window closes. Returns true once it has
	/// swapped moves.
	/// </summary>
	bool TickChoice(InputState input, int activeStart, int activeEnd)
	{
		if (moveFrame <= activeStart || moveFrame > activeEnd || currentMove.Choices.Length < 4) return false;

		int pick = -1;
		Vector2 stick = input.Move;
		if (stick.Length() > 0.6f)
		{
			if (Mathf.Abs(stick.Y) >= Mathf.Abs(stick.X)) pick = stick.Y < 0.0f ? 0 : 3;
			else pick = Mathf.Sign(stick.X) == Facing ? 1 : 2;
		}
		else if (moveFrame >= activeEnd)
		{
			pick = 1;
		}
		if (pick < 0) return false;

		// A null choice is "cancel": the stance is put away and its short endlag plays out.
		if (currentMove.Choices[pick] == null)
		{
			moveFrame = activeEnd;
			return false;
		}

		BeginMove(currentMove.Choices[pick], currentSlot);
		return true;
	}

	/// <summary>
	/// Glint through the startup, standing still (hanging, in the air). Then cross the whole
	/// distance in the active frames and stop dead - a teleport you can see coming.
	/// </summary>
	void TickBlink(int activeStart, int activeEnd)
	{
		if (moveFrame <= activeStart)
		{
			Velocity = new Vector2(Velocity.X * 0.7f, Mathf.Min(Velocity.Y * 0.8f, 120.0f));
			return;
		}

		if (moveFrame <= activeEnd)
		{
			if (!blinkStarted)
			{
				blinkStarted = true;
				blinkFrom = GlobalPosition;
				blinkVelocity = new Vector2(Facing * currentMove.SpecialSpeed, 0.0f);
				blinkToTrap = false;
				// One of his own blades planted up ahead: he goes exactly to it and pulls it out.
				Hazard anchor = currentMove.BlinkToTrap ? TrapAhead() : null;
				if (anchor != null)
				{
					Vector2 to = anchor.GlobalPosition + new Vector2(0.0f, BladeStandOffset);
					blinkVelocity = (to - GlobalPosition) * (60.0f / Mathf.Max(1, currentMove.ActiveFrames));
					blinkToTrap = true;
					anchor.Remove();
				}
				SfxPlayer.At("special_blink", GlobalPosition, 0.0f);
			}
			Velocity = blinkVelocity;
			return;
		}

		// Arriving at a blade he stops on it; otherwise he slides out of the cut.
		if (moveFrame == activeEnd + 1) Velocity = blinkToTrap ? Vector2.Zero : new Vector2(Facing * 240.0f, 0.0f);
	}

	bool IsBlinkWindup =>
		State == FighterState.Attacking && currentMove != null && currentMove.Blink
		&& moveFrame <= currentMove.StartupFrames;

	/// <summary>
	/// Slow through the windup and the charge, a blur while the blades are live, then round to
	/// the front again and stop - so the body never snaps from his back to his face.
	/// </summary>
	void AdvanceSpin()
	{
		if (spinDone) return;
		int activeStart = currentMove.StartupFrames;
		int activeEnd = activeStart + currentMove.ActiveFrames;
		float speed = moveFrame <= activeStart ? 16.0f : moveFrame <= activeEnd ? 45.0f : 24.0f;

		float before = spinAngle;
		spinAngle += speed;
		if (moveFrame > activeEnd && Mathf.Floor(spinAngle / 360.0f) > Mathf.Floor(before / 360.0f))
		{
			spinAngle = Mathf.Floor(spinAngle / 360.0f) * 360.0f;
			spinDone = true;
		}
	}

	bool IsSpinning =>
		State == FighterState.Attacking && currentMove != null && currentMove.Spin && !spinDone
		&& rig != null && rig.HasExtra(TurnNames[0]);

	/// <summary>
	/// The drawn front arm gives way to a stretched one: all through a tether recovery, only while
	/// the hit is live for a piston punch, and for the whole reach of a command grab.
	/// </summary>
	bool IsArmStretched =>
		(State == FighterState.Attacking && currentMove != null && currentMove.StretchArm
		&& (currentMove.DelayedLaunch ? moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames : IsActiveFrame()))
		|| IsCommandGrabbing;

	/// <summary>Shows the turning frame nearest the spin's angle, mirrored for the far half of the turn.</summary>
	void ShowTurnFrame(bool spinning)
	{
		int shown = -1;
		bool mirrored = false;
		if (spinning)
		{
			float a = Mathf.PosMod(spinAngle, 360.0f);
			if (a > 180.0f)
			{
				a = 360.0f - a;
				mirrored = true;
			}
			float best = float.MaxValue;
			for (int i = 0; i < TurnAngles.Length; i++)
			{
				float d = Mathf.Abs(TurnAngles[i] - a);
				if (d < best)
				{
					best = d;
					shown = i;
				}
			}
		}

		for (int i = 0; i < TurnNames.Length; i++)
		{
			rig.SetExtraVisible(TurnNames[i], i == shown);
			if (i == shown) rig.SetExtraMirrored(TurnNames[i], mirrored);
		}
	}

	/// <summary>
	/// A chain from his hand to a drawing on its end - a dagger thrown on a chain - the drawing
	/// pointing the way the chain runs. Steel links, alternately face-on and edge-on.
	/// </summary>
	bool DrawChainTether(Vector2 from, Vector2 to, string artName)
	{
		RigArt art = rig?.PoseArtFor(artName);
		if (art == null) return false;
		Vector2 span = to - from;
		float length = span.Length();
		if (length > 1.0f)
		{
			Vector2 dir = span / length;
			var steel = new Color(0.58f, 0.60f, 0.66f);
			var edge = new Color(0.40f, 0.41f, 0.46f);
			const float Link = 14.0f;
			int i = 0;
			for (float d = Link * 0.5f; d < length; d += Link, i++)
			{
				Vector2 at = from + dir * d;
				if (i % 2 == 0)
				{
					DrawArc(at, 6.0f, 0.0f, Mathf.Tau, 12, edge, 4.0f);
					DrawArc(at, 6.0f, 0.0f, Mathf.Tau, 12, steel, 2.0f);
				}
				else
				{
					DrawLine(at - dir * 7.0f, at + dir * 7.0f, edge, 5.0f);
					DrawLine(at - dir * 7.0f, at + dir * 7.0f, steel, 2.5f);
				}
			}
		}
		Vector2 size = art.Texture.GetSize();
		float s = 110.0f / Mathf.Max(size.X, size.Y);
		// Blades are drawn tip-up; turned a quarter past the chain's angle they point along it.
		float angle = (length > 1.0f ? span.Angle() : 0.0f) + Mathf.Pi * 0.5f;
		DrawArtOn(this, art.Texture, art.Anchor, to, angle, new Vector2(s, s), rig.Modulate);
		return true;
	}

	/// <summary>
	/// A robot's arm reaching out: telescoping steel rods from the shoulder, and the forearm and
	/// hand - his drawing, at its own size - riding out on the end of them. Nothing of his is
	/// stretched; the rods are machinery added round it.
	/// </summary>
	bool DrawTelescopingArm(Vector2 from, Vector2 to, RigBone bone)
	{
		Texture2D arm = rig?.PartTexture(bone);
		if (arm == null) return false;
		Vector2 span = to - from;
		float length = span.Length();
		if (length < 1.0f) return true;
		Vector2 dir = span / length;
		Vector2 side = dir.Orthogonal();

		Vector2 pivot = rig.PartPivot(bone);
		Vector2 size = arm.GetSize();
		float scale = rig.PuppetScale;
		float forearm = (size.Y - pivot.Y) * scale;
		float rods = Mathf.Max(0.0f, length - forearm);
		Vector2 elbow = from + dir * rods;

		if (rods > 1.0f)
		{
			float thick = size.X * scale * 0.2f;
			var steel = new Color(0.64f, 0.66f, 0.72f);
			var edge = new Color(0.34f, 0.35f, 0.40f);
			var shine = new Color(0.88f, 0.89f, 0.93f);
			const int Sections = 3;
			for (int k = 0; k < Sections; k++)
			{
				// Each section a little thinner than the one it slides out of, overlapping it.
				float a = rods * k / Sections - (k > 0 ? 6.0f : 0.0f);
				float b = rods * (k + 1) / Sections + 4.0f;
				float w = thick * (1.0f - 0.2f * k);
				Vector2 p0 = from + dir * Mathf.Max(0.0f, a), p1 = from + dir * Mathf.Min(rods + 4.0f, b);
				DrawColoredPolygon(new[] { p0 + side * (w + 3.0f), p1 + side * (w + 3.0f), p1 - side * (w + 3.0f), p0 - side * (w + 3.0f) }, edge);
				DrawColoredPolygon(new[] { p0 + side * w, p1 + side * w, p1 - side * w, p0 - side * w }, steel);
				DrawLine(p0 + side * w * 0.45f, p1 + side * w * 0.45f, shine, 2.0f);
			}
		}

		DrawArtOn(this, arm, pivot, elbow, span.Angle() - Mathf.Pi * 0.5f, new Vector2(scale * Facing, scale), rig.Modulate);
		return true;
	}

	/// <summary>His own forearm, hand and all, stretched from the shoulder out to the tether's tip.</summary>
	bool DrawStretchedArm(Vector2 from, Vector2 to, RigBone bone)
	{
		if (Data.TelescopingArms) return DrawTelescopingArm(from, to, bone);
		Texture2D arm = rig?.PartTexture(bone);
		if (arm == null) return false;

		Vector2 span = to - from;
		float length = span.Length();
		if (length < 1.0f) return true;

		Vector2 pivot = rig.PartPivot(bone);
		float angle = span.Angle() - Mathf.Pi * 0.5f;
		Vector2 size = arm.GetSize();

		// A drawing that marks where its hand starts keeps the hand its own size: only the arm
		// above it stretches, and the hand - DoomBot's claw - is drawn as drawn, on the end.
		float handRow = rig.HandRow(bone);
		if (handRow > pivot.Y && handRow < size.Y)
		{
			float scale = rig.PuppetScale;
			float hand = (size.Y - handRow) * scale;
			float reach = Mathf.Max(0.0f, length - hand);
			float stretch = reach / Mathf.Max(1.0f, handRow - pivot.Y);
			var armTransform = new Transform2D(angle, new Vector2(scale * Facing, Mathf.Max(0.01f, stretch)), 0.0f, from);
			DrawSetTransformMatrix(armTransform);
			DrawTextureRectRegion(arm, new Rect2(-pivot, new Vector2(size.X, handRow)),
				new Rect2(Vector2.Zero, new Vector2(size.X, handRow)), rig.Modulate);
			Vector2 wrist = from + span / length * reach;
			DrawSetTransformMatrix(new Transform2D(angle, new Vector2(scale * Facing, scale), 0.0f, wrist));
			DrawTextureRectRegion(arm, new Rect2(-pivot.X, 0.0f, size.X, size.Y - handRow),
				new Rect2(0.0f, handRow, size.X, size.Y - handRow), rig.Modulate);
			DrawSetTransformMatrix(Transform2D.Identity);
			return true;
		}

		float drawn = Mathf.Max(1.0f, size.Y - pivot.Y);
		// A touch thicker than the arm at rest, so a long thin stretch still reads.
		float across = rig.PuppetScale * 1.3f * Facing;
		DrawArtOn(this, arm, pivot, from, angle, new Vector2(across, length / drawn), rig.Modulate);
		return true;
	}

	/// <summary>Frames left of a blocked hit's flash across the shield.</summary>
	int shieldFlashFrames;

	/// <summary>The stun he is in is from a hit he blocked, so the shield stays up through it.</summary>
	bool blockedStun;

	/// <summary>
	/// Blocking puts up a shield: a bubble round him in his player's colour, which flashes when a
	/// hit lands on it. Eric's call, 2026-10-04, in place of blocking with crossed arms. It is only
	/// how a block looks - blocking still lets some damage and knockback through, and the bubble
	/// never shrinks or breaks (see the blocking rules in .ai/fighting-design.md). No hard outline:
	/// in this game an outline means something you can stand on or be hit by.
	/// </summary>
	void DrawShield(CanvasItem canvas)
	{
		if (shieldFlashFrames > 0) shieldFlashFrames--;
		if (!IsBlocking && !(State == FighterState.Hitstun && blockedStun)) return;
		Color tint = PlayerIndex == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent;
		float radius = Mathf.Max(bodySize.X, bodySize.Y) * 0.62f;
		// Breathing a little, hashed from the frame count like everything else that moves.
		radius *= 1.0f + 0.025f * Mathf.Sin(fxFrames * 0.18f);
		float flash = shieldFlashFrames / 8.0f;
		Vector2 centre = new Vector2(0.0f, -bodySize.Y * 0.04f);
		canvas.DrawCircle(centre, radius, new Color(tint.R, tint.G, tint.B, 0.16f + 0.25f * flash));
		canvas.DrawArc(centre, radius, 0.0f, Mathf.Tau, 48, new Color(tint.R, tint.G, tint.B, 0.55f + 0.4f * flash), 7.0f, true);
		canvas.DrawArc(centre, radius * 0.93f, -2.3f, -1.2f, 12, new Color(1.0f, 1.0f, 1.0f, 0.55f), 5.0f, true);
	}

	void DrawOverlay()
	{
		if (State == FighterState.Eliminated || rig == null || !rig.Loaded) return;
		DrawShield(overlay);
		DrawTauntBubble(overlay);
		DrawChoiceMenu(overlay);
		DrawSpinBlades(overlay, true);
		DrawBlinkGlint(overlay);
		DrawActiveFx(overlay);
		DrawHeatSmoke(overlay);
		DrawBurnFlames(overlay);
		DrawHotClaws(overlay);
	}

	/// <summary>
	/// A speech bubble over the fighter's head while it taunts: paper, an ink edge and a tail
	/// pointing down at them, popping up and fading as the taunt ends.
	/// </summary>
	void DrawTauntBubble(CanvasItem canvas)
	{
		if (State != FighterState.Attacking || currentMove == null || currentMove != Data.Taunt) return;
		if (string.IsNullOrEmpty(Data.TauntLine) || moveFrame < currentMove.StartupFrames) return;

		int shown = moveFrame - currentMove.StartupFrames;
		int left = currentMove.TotalFrames - moveFrame;
		float pop = Mathf.Min(1.0f, shown / 6.0f);
		float fade = Mathf.Min(1.0f, left / 10.0f);

		Font font = ThemeDB.FallbackFont;
		const int FontSize = 30;
		Vector2 textSize = font.GetStringSize(Data.TauntLine, HorizontalAlignment.Left, -1, FontSize);
		Vector2 size = (textSize + new Vector2(36.0f, 20.0f)) * (0.7f + 0.3f * pop);
		var centre = new Vector2(Facing * 30.0f, -bodySize.Y * 1.05f - size.Y * 0.5f);
		var box = new Rect2(centre - size * 0.5f, size);

		var paper = new Color(0.99f, 0.98f, 0.95f, fade);
		var ink = new Color(0.20f, 0.20f, 0.26f, fade);
		Vector2 tailTop = new Vector2(centre.X - Facing * size.X * 0.2f, box.End.Y - 2.0f);
		canvas.DrawColoredPolygon(new[] { tailTop + new Vector2(-10.0f, 0.0f), tailTop + new Vector2(10.0f, 0.0f), new Vector2(Facing * 6.0f, -bodySize.Y * 0.62f) }, paper);
		canvas.DrawRect(box, paper);
		canvas.DrawRect(box, ink, false, 3.0f);
		if (pop >= 1.0f)
		{
			canvas.DrawString(font, new Vector2(box.Position.X, centre.Y + textSize.Y * 0.32f), Data.TauntLine,
				HorizontalAlignment.Center, box.Size.X, FontSize, ink);
		}
	}

	/// <summary>The four swords on offer, each pointing the way it sits, popping open around him.</summary>
	void DrawChoiceMenu(CanvasItem canvas)
	{
		if (State != FighterState.Attacking || currentMove == null || currentMove.Special != SpecialKind.Choice) return;
		if (currentMove.Choices.Length < 4) return;

		float open = Mathf.Min(1.0f, moveFrame / 6.0f);
		// Clear of the drawing, which stands taller than the hurtbox.
		Vector2 centre = new Vector2(0.0f, -bodySize.Y * 0.15f);
		float radius = bodySize.Y * 1.05f * open;
		Vector2[] dirs = { Vector2.Up, new Vector2(Facing, 0.0f), new Vector2(-Facing, 0.0f), Vector2.Down };

		for (int i = 0; i < 4; i++)
		{
			Vector2 at = centre + dirs[i] * radius;
			float disc = 56.0f * open;
			// Paper discs with a mid-grey rim: the swords themselves carry the dark ink.
			canvas.DrawCircle(at, disc, new Color(0.97f, 0.95f, 0.89f, 0.85f));
			canvas.DrawArc(at, disc, 0.0f, Mathf.Tau, 32, new Color(0.42f, 0.40f, 0.46f, 0.9f), 3.0f);

			// The cancel slot: a crayon cross instead of a sword.
			if (currentMove.Choices[i] == null)
			{
				float x = 22.0f * open;
				var cross = new Color(0.86f, 0.30f, 0.28f, 0.95f);
				CrayonBrush.InkLine(canvas, at + new Vector2(-x, -x), at + new Vector2(x, x), cross, 7.0f, 51, 1.2f);
				CrayonBrush.InkLine(canvas, at + new Vector2(-x, x), at + new Vector2(x, -x), cross, 7.0f, 53, 1.2f);
				continue;
			}

			Texture2D art = currentMove.Choices[i].FxTexture;
			if (art == null) continue;
			Vector2 size = art.GetSize();
			float s = 98.0f * open / Mathf.Max(size.X, size.Y);
			DrawArtOn(canvas, art, size * 0.5f, at, dirs[i].Angle() + Mathf.Pi * 0.5f, new Vector2(s, s), Colors.White);
		}
	}

	/// <summary>
	/// Two blades held out either side, going round with him. The one swinging past in front is
	/// drawn over him and the one behind under him; each is foreshortened as it turns toward the
	/// viewer, and while the hit is live it smears.
	/// </summary>
	void DrawSpinBlades(CanvasItem canvas, bool front)
	{
		if (!IsSpinning) return;
		RigArt art = rig.PoseArtFor(currentMove.HeldArt);
		if (art == null) return;

		Vector2 texSize = art.Texture.GetSize();
		float s = currentMove.HeldArtSize / texSize.Y;
		bool live = moveFrame > currentMove.StartupFrames
			&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;
		int smears = live ? 3 : 1;

		for (int blade = 0; blade < 2; blade++)
		{
			for (int g = smears - 1; g >= 0; g--)
			{
				float b = Mathf.DegToRad(spinAngle - g * 16.0f + 90.0f + 180.0f * blade);
				float x = Mathf.Sin(b);
				if ((Mathf.Cos(b) >= 0.0f) != front) continue;

				Vector2 grip = new Vector2(currentMove.HeldArtOffset.X * x, currentMove.HeldArtOffset.Y);
				float pointing = x >= 0.0f ? 0.0f : Mathf.Pi;
				var scale = new Vector2(s, s * Mathf.Max(0.12f, Mathf.Abs(x)));
				Color tint = rig.Modulate;
				tint.A *= g == 0 ? 1.0f : 0.35f / g;
				DrawArtOn(canvas, art.Texture, art.Anchor, grip, pointing + Mathf.Pi * 0.5f, scale, tint);
			}
		}
	}

	/// <summary>
	/// The warning before a blink: a star of light on his blade that grows and turns, and flares
	/// just before he goes. Its sound, special_glint, is a ring that rises with it and peaks on
	/// the same frame - see SfxCatalog.ForMove and .ai/fighting-design.md.
	/// </summary>
	void DrawBlinkGlint(CanvasItem canvas)
	{
		if (!IsBlinkWindup) return;

		float t = moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames);
		Vector2 at = new Vector2(Facing * bodySize.X * 0.75f, -bodySize.Y * 0.18f);
		float size = 16.0f + 60.0f * t * t;
		float turn = moveFrame * 0.09f;
		var light = new Color(1.0f, 0.97f, 0.78f, 0.55f + 0.45f * t);

		if (currentMove.StartupFrames - moveFrame < 4) canvas.DrawCircle(at, size * 0.6f, new Color(1.0f, 0.95f, 0.7f, 0.45f));
		for (int i = 0; i < 4; i++)
		{
			float a = turn + i * Mathf.Pi * 0.5f;
			float len = i % 2 == 0 ? size : size * 0.55f;
			Vector2 tip = at + Vector2.Right.Rotated(a) * len;
			Vector2 side = Vector2.Right.Rotated(a + Mathf.Pi * 0.5f) * size * 0.09f;
			canvas.DrawColoredPolygon(new[] { at + side, tip, at - side }, light);
		}
		canvas.DrawCircle(at, size * 0.16f, Colors.White);
	}

	/// <summary>
	/// What a blink leaves behind: streaks across the gap and fading copies of him along it, so
	/// the eye can follow where he went.
	/// </summary>
	void DrawBlinkTrail()
	{
		if (State != FighterState.Attacking || currentMove == null || !currentMove.Blink || !blinkStarted) return;
		int activeEnd = currentMove.StartupFrames + currentMove.ActiveFrames;
		const int Linger = 8;
		if (moveFrame > activeEnd + Linger) return;

		float fade = moveFrame <= activeEnd ? 1.0f : 1.0f - (moveFrame - activeEnd) / (float)(Linger + 1);
		Vector2 from = blinkFrom - GlobalPosition;

		Texture2D body = rig.PartTexture(RigBone.Torso);
		if (body != null)
		{
			var scale = new Vector2(rig.PuppetScale * Facing, rig.PuppetScale);
			for (int i = 0; i < 3; i++)
			{
				float k = i / 3.0f;
				var ghost = new Color(1.0f, 1.0f, 1.0f, (0.12f + 0.12f * i) * fade);
				DrawArtOn(this, body, rig.PartPivot(RigBone.Torso), from * (1.0f - k) + rig.Position, 0.0f, scale, ghost);
			}
		}

		Color streak = Data.PlaceholderColor;
		for (int i = 0; i < 5; i++)
		{
			float y = -bodySize.Y * 0.4f + i * bodySize.Y * 0.2f;
			streak.A = (0.5f - 0.07f * Mathf.Abs(i - 2)) * fade;
			DrawLine(new Vector2(from.X, y), new Vector2(0.0f, y), streak, 5.0f - Mathf.Abs(i - 2));
		}
	}

	/// <summary>
	/// A drawing on any canvas, with its anchor put at <paramref name="at"/>: scaled in its own
	/// axes, then turned (see <see cref="DrawArtTransform"/> for why that order).
	/// </summary>
	static void DrawArtOn(CanvasItem canvas, Texture2D art, Vector2 anchor, Vector2 at, float angle, Vector2 scale, Color tint)
	{
		canvas.DrawSetTransformMatrix(new Transform2D(angle, scale, 0.0f, at));
		canvas.DrawTexture(art, -anchor, tint);
		canvas.DrawSetTransformMatrix(Transform2D.Identity);
	}

	// --- Bomb --------------------------------------------------------------------

	bool IsBombArmed =>
		State == FighterState.Attacking
		&& currentMove != null
		&& currentMove.Special == SpecialKind.Bomb
		&& !bombDetonated
		&& moveFrame > currentMove.StartupFrames
		&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;

	void TickBomb(int activeEnd)
	{
		// Planted: he does not slide, and a bomb in the air drops rather than floating.
		Velocity = new Vector2(0.0f, Velocity.Y);
		if (IsBombArmed && !IsOnFloor())
		{
			Velocity = new Vector2(0.0f, Mathf.Max(Velocity.Y, Data.FastFallSpeed * 0.5f));
		}

		if (!bombDetonated && moveFrame >= activeEnd) DetonateBomb();
	}

	/// <summary>
	/// The explosion is a short-lived hazard carrying the bomb move's own damage and knockback,
	/// so it hits everyone in range except him. He pays the self-damage and takes no knockback.
	/// </summary>
	void DetonateBomb()
	{
		if (bombDetonated || currentMove == null) return;
		bombDetonated = true;
		hazardSpawned = true;

		Match?.SpawnHazard(this, currentMove, GlobalPosition, Vector2.Zero);
		Match?.OnExplosion(GlobalPosition);
		Percent += currentMove.SelfDamage;

		// Straight into endlag: the fuse is over whichever way it ended.
		moveFrame = Mathf.Max(moveFrame, currentMove.StartupFrames + currentMove.ActiveFrames);
	}

	// --- Drawing the fighter as one piece ------------------------------------------

	bool IsDrawnAsBall =>
		(drawnAsBall && (State == FighterState.Hitstun || State == FighterState.Tumbling))
		|| (State == FighterState.Attacking && currentMove != null && currentMove.BallForm);

	/// <summary>World radius of the drawn body as a ball. Decides how fast he appears to roll.</summary>
	float BallRadius()
	{
		Texture2D body = rig?.PartTexture(RigBone.Torso);
		if (body == null) return bodySize.X * 0.5f;
		return Mathf.Max(8.0f, body.GetSize().Y * 0.5f * rig.PuppetScale);
	}

	/// <summary>
	/// Draws his body part on its own, turned about its centre and resting on the floor. The part
	/// is his own drawing, only moved and rotated - exactly what the rig does to it anyway.
	/// </summary>
	void DrawBall(float scale, float angle)
	{
		Texture2D body = rig?.PartTexture(RigBone.Torso);
		if (body == null) return;

		float s = rig.PuppetScale * scale;
		Vector2 size = body.GetSize();
		// Squashed flat for a moment as it bounces off the floor - flattened on the screen, not
		// along however far round it has rolled.
		float q = 0.28f * ballSquashFrames / BallSquashFrames;
		Vector2 centre = new Vector2(0.0f, bodySize.Y * 0.5f - size.Y * 0.5f * s * (1.0f - q));

		DrawSetTransformMatrix(new Transform2D(0.0f, new Vector2(1.0f + q, 1.0f - q), 0.0f, centre)
			* new Transform2D(angle, new Vector2(s * Facing, s), 0.0f, Vector2.Zero));
		DrawTexture(body, -size * 0.5f, rig.Modulate);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>
	/// Curled up inside a ball of fire: it grows as the move charges, and once he is rolling it
	/// streams flame back the way he came. Drawn under the curled-up body, so he is inside it.
	/// </summary>
	void DrawFireball()
	{
		float charge = chargeFrames / (float)MaxChargeFrames;
		float r = BallRadius() * (1.35f + 0.45f * charge);
		Vector2 centre = new Vector2(0.0f, bodySize.Y * 0.5f - BallRadius());
		bool rolling = moveFrame > currentMove.StartupFrames;
		Vector2 back = rolling ? new Vector2(-Facing, -0.25f).Normalized() : Vector2.Up;

		// Rolling, a long tail of fire streams out behind; charging on the spot, only small licks
		// of flame rise off the top - a full tail pointing up read as a second fireball.
		int tail = rolling ? 6 : 3;
		for (int i = tail; i >= 1; i--)
		{
			float k = i / (float)tail;
			float jitter = CrayonBrush.Noise(fxFrames + i * 5, 13) * r * 0.2f;
			Vector2 at = rolling
				? centre + back * r * 0.5f * i + back.Orthogonal() * jitter
				: centre + back * r * (0.75f + 0.25f * i) + back.Orthogonal() * jitter;
			float size = rolling ? r * (1.0f - k * 0.65f) : r * (0.42f - 0.1f * i);
			DrawCircle(at, size, new Color(0.95f, 0.32f + 0.3f * (1.0f - k), 0.16f, 0.7f * (1.0f - k * 0.7f)));
		}
		float flicker = 1.0f + CrayonBrush.Noise(fxFrames, 17) * 0.07f;
		DrawCircle(centre, r * 1.05f * flicker, new Color(0.96f, 0.38f, 0.16f, 0.95f));
		DrawCircle(centre, r * 0.8f * flicker, new Color(0.99f, 0.64f, 0.22f, 0.95f));
		DrawCircle(centre, r * 0.55f, new Color(1.0f, 0.90f, 0.55f, 0.9f));
	}

	/// <summary>Shrunk down with a lit fuse. The spark blinks faster as the fuse runs out.</summary>
	void DrawBomb()
	{
		const float Shrink = 0.62f;
		DrawBall(Shrink, 0.0f);

		Texture2D body = rig?.PartTexture(RigBone.Torso);
		float radius = body != null ? body.GetSize().Y * 0.5f * rig.PuppetScale * Shrink : 30.0f;
		Vector2 top = new Vector2(0.0f, bodySize.Y * 0.5f - radius * 2.0f);
		Vector2 fuseEnd = top + new Vector2(Facing * 14.0f, -24.0f);

		CrayonBrush.InkLine(this, top, fuseEnd, new Color(0.24f, 0.22f, 0.20f), 5.0f, 41, 1.0f);

		int left = currentMove.StartupFrames + currentMove.ActiveFrames - moveFrame;
		int period = left > 60 ? 16 : left > 25 ? 8 : 4;
		if ((moveFrame / period) % 2 == 0)
		{
			DrawCircle(fuseEnd, 9.0f, new Color(0.99f, 0.78f, 0.28f));
			DrawCircle(fuseEnd, 4.5f, new Color(0.96f, 0.36f, 0.20f));
		}
	}

	/// <summary>
	/// The whole drawing a kid made for this moment, if there is one. Circy's are held poses:
	/// looking out at the player and then shrinking before the bomb, the bomb itself, and the
	/// shoulder bash while the hook pulls him in. (He stretches as the puppet, in a T-pose - his
	/// drawings of himself tall and small were dropped for that, Eric's call 2026-10-04.) Null
	/// means the puppet is showing.
	/// </summary>
	string HeldPoseName()
	{
		if (rig == null || !rig.Loaded || State != FighterState.Attacking || currentMove == null) return null;

		// A move that strikes one of several drawn poses holds the one picked for it, start to end.
		if (currentMove.PoseArts.Length > 0) return rig.PoseArtFor(chosenPose) != null ? chosenPose : null;

		string name = null;
		int activeEnd = currentMove.StartupFrames + currentMove.ActiveFrames;

		switch (currentMove.Special)
		{
			case SpecialKind.Bomb when moveFrame <= currentMove.StartupFrames:
				name = moveFrame <= currentMove.StartupFrames * 0.6f ? "lookout" : "shrinking";
				break;

			case SpecialKind.Bomb when IsBombArmed:
				name = "bomb";
				break;

			case SpecialKind.Recovery when currentMove.DelayedLaunch && hookFired && moveFrame <= activeEnd:
				name = "bash";
				break;
		}

		return rig.PoseArtFor(name) != null ? name : null;
	}

	/// <summary>
	/// A held pose, standing on his feet and mirrored with his facing. Moved and scaled only.
	/// The bomb swells in and out as the fuse runs down, so "about to go off" is visible.
	/// </summary>
	void DrawHeldPose(string name)
	{
		RigArt art = rig.PoseArtFor(name);
		float s = rig.PoseScale(name);

		if (name == "bomb")
		{
			int left = currentMove.StartupFrames + currentMove.ActiveFrames - moveFrame;
			s *= 0.7f;
			if (left < 45) s *= 1.0f + 0.07f * Mathf.Sin(moveFrame * (left < 20 ? 1.6f : 0.8f));
		}

		Vector2 feet = new Vector2(0.0f, bodySize.Y * 0.5f);
		DrawSetTransform(feet, 0.0f, new Vector2(s * Facing, s));
		DrawTexture(art.Texture, -art.Anchor, rig.Modulate);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	// --- Heat ------------------------------------------------------------------------

	void AddHeat(float amount)
	{
		if (!Data.HasHeat || amount <= 0.0f || overheatFrames > 0) return;
		heat = Mathf.Min(Heat.Max, heat + amount);
		framesSinceHeat = 0;
	}

	/// <summary>
	/// Cools him once he has gone a while without heating up, and counts how long he has sat at
	/// full heat. Too long there and he overheats: a stall in a cloud of steam that lets some of
	/// it out. It waits until he is free - never in the middle of a swing or a hit.
	/// </summary>
	void TickHeat()
	{
		if (!Data.HasHeat) return;
		if (overheatFrames > 0)
		{
			overheatFrames--;
			return;
		}

		if (++framesSinceHeat > Heat.CoolDelayFrames) heat = Mathf.Max(0.0f, heat - Heat.CoolPerFrame);

		framesAtMaxHeat = heat >= Heat.Max ? framesAtMaxHeat + 1 : 0;
		if (framesAtMaxHeat >= Heat.OverheatAfterFrames
			&& (State == FighterState.Grounded || State == FighterState.Airborne))
		{
			overheatFrames = Heat.OverheatStallFrames;
			heat = Heat.AfterOverheat;
			framesAtMaxHeat = 0;
			IsBlocking = false;
			SfxPlayer.At("overheat", GlobalPosition, 0.0f);
		}
	}

	/// <summary>
	/// Grey steel warming to a dull red, then orange, and a light pulse at full heat - so how hot
	/// he is reads from across the room, and full heat is a warning to everyone.
	/// </summary>
	Color HeatTint()
	{
		float h = HeatFraction;
		Color tint = Colors.White.Lerp(new Color(1.32f, 0.70f, 0.52f), Mathf.Min(1.0f, h * 1.25f) * 0.8f);
		if (h >= 1.0f)
		{
			float pulse = 0.5f + 0.5f * Mathf.Sin(fxFrames * 0.32f);
			tint = tint.Lerp(new Color(1.7f, 1.3f, 0.95f), 0.4f * pulse);
		}
		return tint;
	}

	/// <summary>
	/// Lets all the heat out at once: a burst around him sized by how hot he was (see
	/// <see cref="Heat.VentAt"/>). Each size is built once and kept, like a charged projectile.
	/// </summary>
	void Vent()
	{
		hazardSpawned = true;
		float h = Data.HasHeat ? HeatFraction : 1.0f;
		int step = Mathf.RoundToInt(h * Heat.VentSteps);
		if (!ventMoves.TryGetValue(step, out MoveData vent) || vent == null)
		{
			vent = Heat.VentAt(currentMove, step / (float)Heat.VentSteps);
			ventMoves[step] = vent;
		}
		Match?.SpawnHazard(this, vent, GlobalPosition, Vector2.Zero);

		bool flame = h >= Heat.FlameFrom;
		SfxPlayer.At(flame ? "furnace_blast" : "steam", GlobalPosition, 0.03f);
		if (flame) Match?.Shake(25.0f + 60.0f * h);

		heat = 0.0f;
		framesAtMaxHeat = 0;
	}

	// --- Burning ----------------------------------------------------------------------

	/// <summary>Sets this fighter on fire, or tops the fire back up - burns never stack.</summary>
	void Ignite(int frames, float totalDamage)
	{
		int ticks = Mathf.Max(1, frames / BurnTickFrames);
		float tick = totalDamage / ticks;
		burnTick = burnFrames > 0 ? Mathf.Max(burnTick, tick) : tick;
		burnFrames = Mathf.Max(burnFrames, frames);
		if (burnTickTimer <= 0) burnTickTimer = BurnTickFrames;
	}

	void TickBurn()
	{
		if (burnFrames <= 0) return;
		if (State == FighterState.Respawning)
		{
			burnFrames = 0;
			return;
		}
		burnFrames--;
		if (--burnTickTimer > 0) return;
		burnTickTimer = BurnTickFrames;
		Percent += burnTick;
		SfxPlayer.At("burn", GlobalPosition, 0.1f);
	}

	// --- Rusty joints -----------------------------------------------------------------

	/// <summary>
	/// The moves a miss is counted for: anything swung at someone, and a grab. Projectiles are
	/// not - whether they hit is decided long after the move is over.
	/// </summary>
	static bool WhiffCounts(MoveData move) =>
		(move.Special == SpecialKind.None && move.HitboxRadius > 0.0f) || move.Special == SpecialKind.CommandGrab;

	// --- Effects on a live hit -----------------------------------------------------------

	bool IsActiveFrame(int linger = 0) =>
		State == FighterState.Attacking && currentMove != null
		&& moveFrame > currentMove.StartupFrames
		&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames + linger;

	/// <summary>
	/// What the hit is made of, drawn over the fighter while it is live: sparks off steel,
	/// crackling electricity, rocket jets. Every flicker is hashed from the frame count, so it
	/// moves without being random - see the crayon-wobble rule in CLAUDE.md.
	/// </summary>
	void DrawActiveFx(CanvasItem canvas)
	{
		if (State != FighterState.Attacking || currentMove == null) return;
		Vector2 hit = new Vector2(currentMove.HitboxOffset.X * Facing, currentMove.HitboxOffset.Y);

		switch (currentMove.ActiveFx)
		{
			case ActiveFx.Sparks when IsActiveFrame(6):
				DrawSparks(canvas, hit);
				break;
			case ActiveFx.Electric when IsActiveFrame(3):
				DrawElectric(canvas, hit, currentMove.HitboxRadius);
				break;
			case ActiveFx.Flame when currentMove.Corkscrew && IsActiveFrame(2):
				DrawFireShroud(canvas, CurrentHitboxCentre() - GlobalPosition, currentMove.HitboxRadius);
				break;
			case ActiveFx.Flame when IsActiveFrame(5):
				DrawFlameBurst(canvas, CurrentHitboxCentre() - GlobalPosition, currentMove.HitboxRadius);
				break;
			// The jets light halfway through the startup, small, and roar for the whole flight.
			case ActiveFx.Jets when moveFrame > currentMove.StartupFrames / 2
				&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames:
				DrawJets(canvas);
				break;
		}

		// Glowing hot: whatever he swings bursts into flame where it lands.
		if (HitsHot(currentMove) && IsActiveFrame(5)
			&& currentMove.ActiveFx != ActiveFx.Flame && currentMove.ActiveFx != ActiveFx.Electric)
		{
			DrawFlameBurst(canvas, CurrentHitboxCentre() - GlobalPosition, currentMove.HitboxRadius * 0.8f);
		}
	}

	void DrawSparks(CanvasItem canvas, Vector2 at)
	{
		int since = moveFrame - currentMove.StartupFrames;
		float t = Mathf.Clamp(since / 9.0f, 0.0f, 1.0f);
		const int Count = 9;
		for (int i = 0; i < Count; i++)
		{
			float angle = Mathf.Tau * i / Count + CrayonBrush.Noise(31, i) * 0.5f;
			float reach = (22.0f + 46.0f * Mathf.Abs(CrayonBrush.Noise(37, i))) * (0.3f + t);
			Vector2 dir = Vector2.Right.Rotated(angle);
			Vector2 tip = at + dir * reach;
			Vector2 tail = at + dir * reach * Mathf.Max(0.0f, t - 0.25f);
			var spark = new Color(1.0f, 0.92f, 0.45f, 1.0f - t * 0.8f);
			canvas.DrawLine(tail, tip, spark, 4.0f * (1.0f - t * 0.6f));
			canvas.DrawCircle(tip, 2.5f, new Color(1.0f, 1.0f, 0.9f, 1.0f - t));
		}
		canvas.DrawCircle(at, 16.0f * (1.0f - t), new Color(1.0f, 0.97f, 0.8f, 0.8f * (1.0f - t)));
	}

	/// <summary>
	/// A burst of flame tongues round a hit, flickering outward and fading - the hit is on fire.
	/// Hashed from the frame count like every other flicker.
	/// </summary>
	/// <summary>
	/// Wrapped in fire for as long as a dive is live: tongues of flame all round him, swirling the
	/// way he spins and streaming back from the way he goes, full strength until the last frames.
	/// See-through enough that the drawing still shows through it.
	/// </summary>
	void DrawFireShroud(CanvasItem canvas, Vector2 at, float radius)
	{
		int since = moveFrame - currentMove.StartupFrames;
		int left = currentMove.ActiveFrames + 2 - since;
		float fade = Mathf.Clamp(left / 3.0f, 0.0f, 1.0f);
		const int Tongues = 12;
		for (int i = 0; i < Tongues; i++)
		{
			float a = Mathf.Tau * i / Tongues + since * 0.55f * Facing + CrayonBrush.Noise(fxFrames / 2, i) * 0.2f;
			Vector2 dir = Vector2.Right.Rotated(a);
			// Swept back off him by the speed he is going.
			Vector2 tipDir = (dir + new Vector2(-Facing * 0.9f, -0.2f)).Normalized();
			float reach = radius * (0.75f + 0.35f * Mathf.Abs(CrayonBrush.Noise(fxFrames, i + 23)));
			Vector2 side = dir.Orthogonal() * radius * 0.2f;
			Vector2 root = at + dir * radius * 0.55f;
			canvas.DrawColoredPolygon(new[] { root + side, root + tipDir * reach, root - side },
				new Color(0.96f, 0.40f, 0.16f, 0.62f * fade));
			canvas.DrawColoredPolygon(new[] { root + side * 0.55f, root + tipDir * reach * 0.6f, root - side * 0.55f },
				new Color(0.99f, 0.76f, 0.26f, 0.7f * fade));
		}
	}

	void DrawFlameBurst(CanvasItem canvas, Vector2 at, float radius)
	{
		int since = moveFrame - currentMove.StartupFrames;
		float t = Mathf.Clamp(since / (float)(currentMove.ActiveFrames + 5), 0.0f, 1.0f);
		float fade = 1.0f - t * t;
		const int Tongues = 9;
		for (int i = 0; i < Tongues; i++)
		{
			float a = Mathf.Tau * i / Tongues + CrayonBrush.Noise(fxFrames / 2, i) * 0.25f;
			Vector2 dir = Vector2.Right.Rotated(a);
			// Flames lean upward whichever way they burst.
			Vector2 tipDir = (dir + Vector2.Up * 0.6f).Normalized();
			float reach = radius * (0.7f + 0.35f * Mathf.Abs(CrayonBrush.Noise(fxFrames, i + 11))) * (0.6f + 0.5f * t);
			Vector2 side = dir.Orthogonal() * radius * 0.18f;
			Vector2 root = at + dir * radius * 0.25f;
			canvas.DrawColoredPolygon(new[] { root + side, root + tipDir * reach, root - side },
				new Color(0.96f, 0.40f, 0.16f, 0.85f * fade));
			canvas.DrawColoredPolygon(new[] { root + side * 0.55f, root + tipDir * reach * 0.62f, root - side * 0.55f },
				new Color(0.99f, 0.76f, 0.26f, 0.9f * fade));
		}
		canvas.DrawCircle(at, radius * 0.32f * fade, new Color(1.0f, 0.93f, 0.66f, 0.85f * fade));
	}

	/// <summary>
	/// Above the head, bolts arc up from the antennae into the hit; anywhere else, a crackling
	/// ring round it. The bolts jump to a new shape every two frames, which is what makes it read
	/// as electricity rather than as lines.
	/// </summary>
	void DrawElectric(CanvasItem canvas, Vector2 at, float radius)
	{
		int jolt = fxFrames / 2;
		var glow = new Color(0.45f, 0.82f, 1.0f, 0.55f);
		var core = new Color(1.0f, 0.98f, 0.75f);

		if (at.Y < -bodySize.Y * 0.5f)
		{
			// From the tips of his antennae if his drawing names them, or else the top of his head.
			float top = bodySize.Y * 0.5f - bodySize.Y * 1.18f * Data.VisualScale;
			for (int side = -1; side <= 1; side += 2)
			{
				Vector2? tip = rig?.PointGlobal(side < 0 ? "antenna_back" : "antenna_front");
				Vector2 antenna = tip.HasValue
					? tip.Value - GlobalPosition
					: new Vector2(Facing * side * bodySize.X * 0.2f, top);
				for (int b = 0; b < 2; b++)
				{
					float a = Mathf.Pi * (1.2f + 0.6f * Mathf.Abs(CrayonBrush.Noise(jolt * 3 + b, side + 5)));
					Vector2 end = at + Vector2.Right.Rotated(a) * radius * 0.7f;
					DrawBolt(canvas, antenna, end, jolt * 7 + b * 13 + side, glow, core);
				}
			}
			canvas.DrawCircle(at, radius * 0.45f, new Color(0.55f, 0.85f, 1.0f, 0.25f));
			return;
		}

		const int Arcs = 7;
		for (int i = 0; i < Arcs; i++)
		{
			float a0 = Mathf.Tau * i / Arcs + CrayonBrush.Noise(jolt, i) * 0.4f;
			float a1 = a0 + 0.7f;
			Vector2 p0 = at + Vector2.Right.Rotated(a0) * radius * 0.9f;
			Vector2 p1 = at + Vector2.Right.Rotated(a1) * radius * 0.9f;
			DrawBolt(canvas, p0, p1, jolt * 11 + i, glow, core);
		}
		canvas.DrawCircle(at, radius * 0.95f, new Color(0.55f, 0.85f, 1.0f, 0.12f));
	}

	static void DrawBolt(CanvasItem canvas, Vector2 from, Vector2 to, int seed, Color glow, Color core)
	{
		const int Segments = 6;
		var points = new Vector2[Segments + 1];
		Vector2 side = (to - from).Orthogonal().Normalized();
		float jag = from.DistanceTo(to) * 0.16f;
		for (int i = 0; i <= Segments; i++)
		{
			float t = i / (float)Segments;
			float off = i == 0 || i == Segments ? 0.0f : CrayonBrush.Noise(seed, i) * jag;
			points[i] = from.Lerp(to, t) + side * off;
		}
		canvas.DrawPolyline(points, glow, 9.0f);
		canvas.DrawPolyline(points, core, 3.0f);
	}

	/// <summary>
	/// Rocket flames out of the soles of both boots, along the way each leg points, flickering -
	/// taken from where the drawing's feet actually are in the pose, so they come out of the feet
	/// rather than near them. Sized to the fighter.
	/// </summary>
	void DrawJets(CanvasItem canvas)
	{
		bool live = moveFrame > currentMove.StartupFrames;
		RigBone[] legs = { RigBone.LegBackLower, RigBone.LegFrontLower };
		for (int i = 0; i < legs.Length; i++)
		{
			var sole = rig?.Sole(legs[i]);
			Vector2 nozzle = sole.HasValue ? sole.Value.sole - GlobalPosition : new Vector2((i * 2 - 1) * bodySize.X * 0.3f, bodySize.Y * 0.5f);
			Vector2 down = sole.HasValue ? sole.Value.down : Vector2.Down;
			Vector2 side = down.Orthogonal();
			float flicker = 0.8f + 0.2f * CrayonBrush.Noise(fxFrames, i + 3);
			float length = bodySize.Y * (live ? 0.62f : 0.27f) * flicker;
			float width = bodySize.X * 0.17f;
			canvas.DrawColoredPolygon(new[] { nozzle + side * width, nozzle - side * width, nozzle + down * length },
				new Color(0.96f, 0.42f, 0.18f, 0.9f));
			canvas.DrawColoredPolygon(new[] { nozzle + side * width * 0.6f, nozzle - side * width * 0.6f, nozzle + down * length * 0.68f },
				new Color(0.99f, 0.78f, 0.25f, 0.95f));
			canvas.DrawColoredPolygon(new[] { nozzle + side * width * 0.28f, nozzle - side * width * 0.28f, nozzle + down * length * 0.36f },
				new Color(1.0f, 0.97f, 0.85f));
		}
	}

	/// <summary>
	/// Smoke rising off a hot fighter, thicker the hotter he is; steam pouring off him while he
	/// is overheated. Pale grey and see-through - nothing outside a character is allowed near
	/// black (see .ai/art-direction.md).
	/// </summary>
	void DrawHeatSmoke(CanvasItem canvas)
	{
		if (!Data.HasHeat || State == FighterState.Respawning) return;
		bool steaming = overheatFrames > 0;
		float h = HeatFraction;
		if (!steaming && h < 0.3f) return;

		float strength = steaming ? 1.0f : (h - 0.3f) / 0.7f;
		int puffs = steaming ? 9 : 2 + Mathf.RoundToInt(4.0f * strength);
		int period = steaming ? 36 : 54;
		float top = -bodySize.Y * 0.5f - bodySize.Y * 0.1f;

		for (int i = 0; i < puffs; i++)
		{
			int clock = fxFrames + i * period / puffs;
			int cycle = clock / period;
			float t = (clock % period) / (float)period;
			float x = CrayonBrush.Noise(cycle * 17 + i, 3) * bodySize.X * (steaming ? 0.8f : 0.4f);
			float y = steaming ? Mathf.Lerp(bodySize.Y * 0.3f, top, Mathf.Abs(CrayonBrush.Noise(cycle, i + 9))) : top;
			Vector2 at = new Vector2(x + CrayonBrush.Noise(cycle, i) * 18.0f * t, y - (steaming ? 90.0f : 130.0f) * t);
			float radius = (steaming ? 18.0f : 12.0f) + (steaming ? 30.0f : 24.0f) * t;
			float alpha = (1.0f - t) * (steaming ? 0.6f : 0.25f + 0.25f * strength);
			Color puff = steaming ? new Color(0.97f, 0.97f, 0.98f, alpha) : new Color(0.78f, 0.78f, 0.80f, alpha);
			canvas.DrawCircle(at, radius, puff);
		}
	}

	/// <summary>Flames licking up off a burning fighter, hashed so they dance without being random.</summary>
	void DrawBurnFlames(CanvasItem canvas)
	{
		if (burnFrames <= 0 || State == FighterState.Respawning || !Visible) return;
		float fade = Mathf.Min(1.0f, burnFrames / 20.0f);
		const int Tongues = 6;
		for (int i = 0; i < Tongues; i++)
		{
			int clock = fxFrames + i * 5;
			float t = (clock % 24) / 24.0f;
			float x = (i / (float)(Tongues - 1) - 0.5f) * bodySize.X * 0.9f + CrayonBrush.Noise(clock / 24, i) * 8.0f;
			float baseY = bodySize.Y * (0.25f - 0.5f * Mathf.Abs(CrayonBrush.Noise(i, 41)));
			Vector2 foot = new Vector2(x, baseY - 40.0f * t);
			float height = (34.0f + 16.0f * Mathf.Abs(CrayonBrush.Noise(clock / 6, i))) * (1.0f - t * 0.5f);
			float width = 13.0f * (1.0f - t * 0.6f);
			float alpha = fade * (1.0f - t);
			canvas.DrawColoredPolygon(new[] { foot + new Vector2(-width, 0.0f), foot + new Vector2(width, 0.0f), foot + new Vector2(CrayonBrush.Noise(clock, i) * 6.0f, -height) },
				new Color(0.97f, 0.45f, 0.18f, 0.85f * alpha));
			canvas.DrawColoredPolygon(new[] { foot + new Vector2(-width * 0.5f, 0.0f), foot + new Vector2(width * 0.5f, 0.0f), foot + new Vector2(0.0f, -height * 0.6f) },
				new Color(1.0f, 0.86f, 0.35f, 0.9f * alpha));
		}
	}

	/// <summary>
	/// A normal attack with <see cref="MoveData.StretchArm"/>: the front arm shoots out from the
	/// shoulder to the hit for its active frames - a piston punch - and snaps back after.
	/// </summary>
	void DrawPistonArm()
	{
		if (State != FighterState.Attacking || currentMove == null || !currentMove.StretchArm || currentMove.DelayedLaunch) return;
		if (!IsActiveFrame()) return;
		// From the drawing's own shoulder where it has one, so the arm comes out where it hangs.
		Vector2 from = (rig?.JointGlobal(RigBone.ArmFrontUpper) ?? HookOrigin()) - GlobalPosition;
		Vector2 to = new Vector2(currentMove.HitboxOffset.X * Facing, currentMove.HitboxOffset.Y);
		DrawStretchedArm(from, to, RigBone.ArmFrontLower);
	}

	// --- Bouncing, and the drawings round him ------------------------------------------------

	/// <summary>
	/// A bouncing recovery: settled on the trampoline through the startup, the fall stopped; thrown
	/// up on the first active frame; and from then on the stick steers him in the air - "he can
	/// move while bouncing".
	/// </summary>
	void TickBounce(InputState input, int activeStart, float dt)
	{
		if (moveFrame <= activeStart)
		{
			Velocity = new Vector2(Velocity.X * 0.8f, Mathf.Min(Velocity.Y * 0.5f, 60.0f));
			return;
		}
		if (moveFrame == activeStart + 1) Velocity = new Vector2(Velocity.X * 0.5f, -currentMove.SpecialRise);

		float steer = input.Move.X * Data.AirSpeed;
		Velocity = new Vector2(Mathf.MoveToward(Velocity.X, steer, Data.AirAcceleration * dt), Velocity.Y);
		if (Mathf.Abs(input.Move.X) > 0.3f) Facing = input.Move.X > 0.0f ? 1 : -1;
	}

	/// <summary>
	/// Elim's trampoline, standing where it was put down: its mat sinks as he lands on it and
	/// springs back as it throws him, then it fades. It stays put while he flies off it.
	/// </summary>
	void DrawTrampoline()
	{
		if (trampolineFrames <= 0) return;
		RigArt art = rig.PoseArtFor("trampoline");
		if (art == null) return;
		int age = trampolineShown - trampolineFrames;
		int settle = currentMove != null && currentMove.Bounce ? currentMove.StartupFrames : 7;
		float sink = age <= settle ? age / (float)Mathf.Max(1, settle) : Mathf.Max(0.0f, 1.0f - (age - settle) / 5.0f);
		float s = TrampolineScale(art);
		var tint = new Color(1.0f, 1.0f, 1.0f, Mathf.Min(1.0f, trampolineFrames / 10.0f));
		DrawArtOn(this, art.Texture, art.Anchor, trampolineAt - GlobalPosition, 0.0f,
			new Vector2(s, s * (1.0f - 0.3f * sink)), tint);
	}

	/// <summary>The trampoline is drawn half as wide again as he is.</summary>
	float TrampolineScale(RigArt art) => bodySize.X * 1.5f / art.Texture.GetSize().X;

	/// <summary>How far its mat stands above the floor, at the size it is drawn.</summary>
	float TrampolineLegs()
	{
		RigArt art = rig?.PoseArtFor("trampoline");
		return art == null ? 0.0f : (art.Texture.GetSize().Y - art.Anchor.Y) * TrampolineScale(art);
	}

	/// <summary>While a move makes him invincible, its drawing (MoveData.AuraArt) stands behind him, pulsing.</summary>
	void DrawAura()
	{
		if (!IsInvincibleMove || string.IsNullOrEmpty(currentMove.AuraArt)) return;
		RigArt art = rig.PoseArtFor(currentMove.AuraArt);
		if (art == null) return;
		Vector2 size = art.Texture.GetSize();
		float pulse = 1.0f + 0.06f * Mathf.Sin(fxFrames * 0.5f);
		float s = bodySize.Y * 1.9f / Mathf.Max(size.X, size.Y) * pulse;
		DrawArtOn(this, art.Texture, art.Anchor, new Vector2(0.0f, -bodySize.Y * 0.1f), 0.0f,
			new Vector2(s * Facing, s), Colors.White);
	}

	// --- Glowing claws -----------------------------------------------------------------

	/// <summary>Hot enough that his normals burn (FighterData.HotHitsFrom), and this is one of them.</summary>
	bool HitsHot(MoveData move) =>
		Data.HotHitsFrom > 0.0f && HeatFraction >= Data.HotHitsFrom
		&& move.Special == SpecialKind.None && move != Data.Taunt && move.HitboxRadius > 0.0f;

	static readonly RigBone[] Forearms = { RigBone.ArmBackLower, RigBone.ArmFrontLower };

	/// <summary>Past his heat line his claws glow, pulsing - the warning that his hits burn now.</summary>
	void DrawHotClaws(CanvasItem canvas)
	{
		if (Data.HotHitsFrom <= 0.0f || HeatFraction < Data.HotHitsFrom || !rig.Visible) return;
		if (State == FighterState.Respawning) return;
		float pulse = 0.8f + 0.2f * Mathf.Sin(fxFrames * 0.25f);
		float r = bodySize.X * 0.2f;
		foreach (RigBone arm in Forearms)
		{
			// An arm out on rods is drawn by itself, away from where the rig's hand is.
			if (arm == RigBone.ArmFrontLower && IsArmStretched) continue;
			if (arm == RigBone.ArmBackLower && IsCommandGrabbing) continue;
			var hand = rig.Sole(arm);
			if (!hand.HasValue) continue;
			Vector2 at = hand.Value.sole - GlobalPosition - hand.Value.down * r * 0.6f;
			canvas.DrawCircle(at, r * 1.3f * pulse, new Color(1.0f, 0.55f, 0.2f, 0.22f));
			canvas.DrawCircle(at, r * 0.7f * pulse, new Color(1.0f, 0.82f, 0.42f, 0.32f));
		}
	}

	// --- Armour ----------------------------------------------------------------------

	/// <summary>The move in progress has armour (MoveData.Armor) and is still in its windup or swing.</summary>
	bool IsArmoredMove =>
		State == FighterState.Attacking && currentMove != null && currentMove.Armor > 0.0f
		&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;

	bool IsArmoredAgainst(float damage) => IsArmoredMove && damage <= currentMove.Armor;

	// --- Air dash ----------------------------------------------------------------------

	/// <summary>
	/// The air jump, for a fighter with FighterData.AirDashSpeed: a burst along one of eight
	/// directions - the way the stick points, or straight ahead with it centred.
	/// </summary>
	void StartAirDash(InputState input)
	{
		Vector2 stick = input.Move;
		const float Eighth = Mathf.Pi * 0.25f;
		Vector2 dir = stick.Length() > 0.35f
			? Vector2.Right.Rotated(Mathf.Round(stick.Angle() / Eighth) * Eighth)
			: new Vector2(Facing, 0.0f);
		if (Mathf.Abs(dir.X) > 0.1f) Facing = dir.X > 0.0f ? 1 : -1;
		airDashFrames = AirDashFrames;
		airDashFrom = GlobalPosition;
		airDashVelocity = dir * Data.AirDashSpeed;
		Velocity = airDashVelocity;
		SfxPlayer.At("special_dash", GlobalPosition, 0.05f);
	}

	/// <summary>Leaning into a dash the way it goes.</summary>
	float AirDashLean() =>
		airDashFrames > 0 ? Mathf.Clamp(airDashVelocity.X / Mathf.Max(1.0f, Data.AirDashSpeed), -1.0f, 1.0f) * 0.3f : 0.0f;

	/// <summary>
	/// An air dash's wake: fading copies of him back along the line and streaks along it, the way
	/// a blink leaves them, so the eye can follow where he went.
	/// </summary>
	void DrawAirDashTrail()
	{
		if (airDashFrames <= 0 && airDashLinger <= 0) return;
		float fade = airDashFrames > 0 ? 1.0f : airDashLinger / 9.0f;
		Vector2 from = airDashFrom - GlobalPosition;

		Texture2D body = rig.PartTexture(RigBone.Torso);
		if (body != null)
		{
			var scale = new Vector2(rig.PuppetScale * Facing, rig.PuppetScale);
			for (int i = 0; i < 3; i++)
			{
				float k = (i + 1) / 4.0f;
				var ghost = new Color(1.0f, 1.0f, 1.0f, (0.1f + 0.1f * i) * fade);
				DrawArtOn(this, body, rig.PartPivot(RigBone.Torso), from * (1.0f - k) + rig.Position, 0.0f, scale, ghost);
			}
		}

		Vector2 side = (from.LengthSquared() > 1.0f ? from.Normalized() : Vector2.Left).Orthogonal();
		Color streak = Data.PlaceholderColor;
		for (int i = 0; i < 3; i++)
		{
			Vector2 off = side * (i - 1) * bodySize.X * 0.3f;
			streak.A = (0.45f - 0.12f * Mathf.Abs(i - 1)) * fade;
			DrawLine(from + off, off, streak, 4.0f);
		}
	}

	// --- Blades as anchors ---------------------------------------------------------------

	/// <summary>
	/// The nearest of his own traps in front of him and no further than the blink itself would go,
	/// or null. Searching further than the blink travels made a blade a free extension of it.
	/// </summary>
	Hazard TrapAhead()
	{
		float Reach = currentMove.SpecialSpeed * currentMove.ActiveFrames / 60.0f;
		const float Rise = 320.0f;
		Hazard best = null;
		float bestDistance = float.MaxValue;
		foreach (KeyValuePair<MoveData, List<Hazard>> pair in outHazards)
		{
			if (pair.Key.Special != SpecialKind.Trap) continue;
			foreach (Hazard hazard in pair.Value)
			{
				if (!IsInstanceValid(hazard) || hazard.IsExpiring) continue;
				Vector2 d = hazard.GlobalPosition - GlobalPosition;
				float ahead = d.X * Facing;
				if (ahead < 40.0f || ahead > Reach || Mathf.Abs(d.Y) > Rise) continue;
				if (d.LengthSquared() < bestDistance)
				{
					bestDistance = d.LengthSquared();
					best = hazard;
				}
			}
		}
		return best;
	}

	// --- Spinning hits and bouncing ------------------------------------------------------

	/// <summary>One somersault over a spinning hit's hitstun, fastest in the middle.</summary>
	float VictimSpinRoll()
	{
		if (spinVictimFrames <= 0 || spinVictimTotal <= 0 || State != FighterState.Hitstun) return 0.0f;
		float t = 1.0f - spinVictimFrames / (float)spinVictimTotal;
		return spinVictimDir * Mathf.Tau * t * t * (3.0f - 2.0f * t);
	}

	/// <summary>
	/// Knocked into a ball and dropped hard onto the floor - or spiked straight down into it - he
	/// bounces, nearly half as high as he came down, and squashes as he hits. A ball is a ball.
	/// </summary>
	void Bounce()
	{
		if (!drawnAsBall || (State != FighterState.Hitstun && State != FighterState.Tumbling)) return;
		if (!IsOnFloor() || fallSpeedBeforeMove < BounceFrom) return;
		Velocity = new Vector2(Velocity.X, -fallSpeedBeforeMove * BounceKeep);
		ballSquashFrames = BallSquashFrames;
	}

	// --- Size and stretching ---------------------------------------------------------------

	/// <summary>
	/// A normal attack at this fighter's current size (see SizeLevels): bigger, harder and slower
	/// tall; smaller, weaker and quicker short. The hitbox is moved out from the feet rather than
	/// from the middle, so a low kick stays on the floor at any height. One copy per move per size,
	/// made once and kept. Specials, the taunt and the rolled-up ball moves are left alone.
	/// </summary>
	MoveData SizedNormal(MoveData move)
	{
		if (sizeLevel == SizeLevels.Normal || move.Special != SpecialKind.None || move == Data.Taunt || move.BallForm) return move;
		if (!sizedNormals.TryGetValue((move, sizeLevel), out MoveData sized))
		{
			float frames = SizeLevels.AttackFrames(sizeLevel);
			float reach = SizeLevels.AttackReach(sizeLevel);
			sized = move.Scaled(new MoveScale(frames, frames, SizeLevels.AttackDamage(sizeLevel), 1.0f, 1.0f, reach));
			FromTheFeet(move, sized, reach, Data.BodySize.Y * 0.5f, bodySize.Y * 0.5f);
			sizedNormals[(move, sizeLevel)] = sized;
		}
		return sized;
	}

	/// <summary>Re-places a resized hitbox (and its combo and link hits) measuring from the feet, not the middle.</summary>
	static void FromTheFeet(MoveData original, MoveData sized, float reach, float halfBefore, float halfNow)
	{
		if (original == null || sized == null) return;
		sized.HitboxOffset = new Vector2(original.HitboxOffset.X * reach,
			(original.HitboxOffset.Y - halfBefore) * reach + halfNow);
		FromTheFeet(original.ComboNext, sized.ComboNext, reach, halfBefore, halfNow);
		FromTheFeet(original.LinkHit, sized.LinkHit, reach, halfBefore, halfNow);
	}

	/// <summary>
	/// How far the kicking leg is stretched right now, for a move with StretchLeg: out to the hit
	/// through the windup, held while it is live, and back in after. A multiple of the leg's length
	/// at his current size, worked out from where the hitbox is - so the foot reaches it, tall or
	/// short.
	/// </summary>
	float LegReachNow()
	{
		float t = LegStretchAmount();
		if (t <= 0.0f) return 1.0f;
		Vector2? hip = rig.JointGlobal(RigBone.LegFrontUpper);
		float leg = rig.LegLength * SizeLevels.LegStretch(sizeLevel) * rig.PuppetScale;
		if (!hip.HasValue || leg <= 1.0f) return 1.0f;

		float target = Mathf.Max(1.0f, (CurrentHitboxCentre() - hip.Value).Length() / leg);
		return 1.0f + (target - 1.0f) * t;
	}

	/// <summary>How far into a stretched kick: 0 at rest, rising through the windup, 1 while it is live, easing back after.</summary>
	float LegStretchAmount()
	{
		if (State != FighterState.Attacking || currentMove == null || !currentMove.StretchLeg || rig == null) return 0.0f;
		int s = currentMove.StartupFrames;
		int a = s + currentMove.ActiveFrames;
		return moveFrame <= s ? Mathf.Pow(moveFrame / (float)Mathf.Max(1, s), 2.0f)
			: moveFrame <= a ? 1.0f
			: Mathf.Max(0.0f, 1.0f - (moveFrame - a) / 8.0f);
	}

	// --- Drawing (M1 placeholder) --------------------------------------------

	public override void _Draw()
	{
		if (State == FighterState.Eliminated) return;

		// Once a rig is loaded the puppet IS the fighter; the rectangle below only exists so a
		// missing or broken manifest degrades to something playable instead of invisible.
		if (rig != null && rig.Loaded)
		{
			string pose = HeldPoseName();
			DrawTrampoline();
			DrawAura();
			DrawResizeHint();
			if (IsDrawnAsBall && State == FighterState.Attacking && currentMove != null && currentMove.FxFlame) DrawFireball();
			if (IsDrawnAsBall) DrawBall(1.0f, rollAngle);
			else if (pose != null) DrawHeldPose(pose);
			else if (IsBombArmed) DrawBomb();
			DrawBlinkTrail();
			DrawAirDashTrail();
			DrawSpinBlades(this, false);
			DrawSwingTrail();
			DrawGrabArms();
			DrawPistonArm();
			DrawTether();
			DrawSwing();
			DrawHeldArt();
			DrawBeamCharge();
			return;
		}

		Color body = Data.PlaceholderColor;

		// Flash white during hitlag so the freeze reads as an impact rather than a stutter.
		if (hitlagFrames > 0) body = Colors.White;
		else if (State == FighterState.Hitstun) body = body.Lerp(new Color(1.0f, 0.4f, 0.4f), 0.5f);
		else if (IsBlocking) body = body.Lerp(new Color(0.4f, 0.8f, 1.0f), 0.6f);

		// Blink while respawn-invulnerable.
		if (invulnFrames > 0 && (invulnFrames / 4) % 2 == 0) body.A = 0.45f;

		var rect = new Rect2(-bodySize * 0.5f, bodySize);
		DrawRect(rect, body);
		DrawRect(rect, new Color(0.08f, 0.08f, 0.1f), false, 3.0f);

		// Facing indicator - a stand-in for "which way is this fighter pointing", which the
		// side-view drawing answers on its own from M2 onward.
		var eye = new Rect2(
			new Vector2(Facing * (bodySize.X * 0.5f - 16.0f) - 6.0f, -bodySize.Y * 0.32f),
			new Vector2(12.0f, 12.0f));
		DrawRect(eye, new Color(0.08f, 0.08f, 0.1f));
	}
}
