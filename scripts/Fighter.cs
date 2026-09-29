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

	/// <summary>How much of the standing height is left to hit while crouched.</summary>
	const float CrouchHeight = 0.62f;

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
	/// ago the stick went from centre to full - this many frames or fewer is a flick.
	/// </summary>
	const int SmashFlickFrames = 4;

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

	float ChargeScale => 1.0f + MaxChargeBonus * chargeFrames / MaxChargeFrames;

	bool IsCharging =>
		State == FighterState.Attacking && currentMove != null && currentMove.Chargeable
		&& moveFrame == Mathf.Max(1, currentMove.StartupFrames - ChargeHoldBeforeHit);

	// --- Size, for a fighter with a Resize special -------------------------------

	/// <summary>The hurtbox as it is right now. Differs from Data.BodySize only while resized.</summary>
	Vector2 bodySize;
	RectangleShape2D bodyShape;
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

	/// <summary>A held fighter is let go after this long whatever happens, so a bug can never pin someone.</summary>
	const int MaxHeldFrames = 40;

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

		// Fighters collide with the stage (layer 1) but never with each other, which is how
		// platform fighters behave - two players standing on the same tile is normal.
		CollisionLayer = 0;
		CollisionMask = GroundMask;

		bodySize = data.BodySize;
		bodyShape = new RectangleShape2D { Size = bodySize };
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
			}
			else
			{
				rig.QueueFree();
				rig = null;
			}
		}

		// Added after the rig, so it draws over it.
		overlay = new Node2D();
		AddChild(overlay);
		overlay.Draw += DrawOverlay;
	}

	void Redraw()
	{
		QueueRedraw();
		overlay?.QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

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
			return;
		}

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

		// During the 3-2-1 the controller is still read, so button edges stay honest, but
		// nothing it says is acted on.
		if (Match != null && Match.InputLocked) input = InputState.None;
		lastStick = input.Move;

		TickTimers(input);

		switch (State)
		{
			case FighterState.Grounded: TickGrounded(input, dt); break;
			case FighterState.Airborne: TickAirborne(input, dt); break;
			case FighterState.Attacking: TickAttacking(input, dt); break;
			case FighterState.Dodging: TickDodging(dt); break;
			case FighterState.LedgeHang: TickLedgeHang(input); break;
			case FighterState.Hitstun: TickHitstun(dt); break;
			case FighterState.Tumbling: TickTumbling(dt); break;
			case FighterState.Respawning: TickRespawning(); break;
		}

		if (State != FighterState.Respawning && State != FighterState.LedgeHang)
		{
			ApplyGravity(dt);
			MoveAndSlide();
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

		// Squash and stretch, eased so it never pops: long and thin leaving the ground, short
		// and wide landing, coiled before a hit and stretched through it.
		squashAmount = Mathf.Lerp(squashAmount, TargetSquash(), 0.45f);
		rig.SetSquash(squashAmount);

		// A fighter with a hard hat puts it on to block, and takes it off after.
		rig.SetExtraVisible("hardhat", IsBlocking);

		// Knocked over, or curled into a bomb: the limbs are gone and he is drawn as one piece in
		// _Draw. Elim asked for exactly that - "get rid of his arms and legs until he gets back up".
		rig.Visible = HeldPoseName() == null && !IsDrawnAsBall && !IsBombArmed;
		if (!rig.Visible) return;

		// Spinning, the body and arms give way to a turning frame; with the arm stretched out
		// on a tether, the drawn arm gives way to the stretched one.
		bool spinning = IsSpinning;
		bool armOut = IsArmStretched;
		rig.SetPartVisible(RigBone.Torso, !spinning);
		rig.SetPartVisible(RigBone.ArmBackUpper, !spinning);
		rig.SetPartVisible(RigBone.ArmBackLower, !spinning);
		rig.SetPartVisible(RigBone.ArmFrontUpper, !spinning && !armOut);
		rig.SetPartVisible(RigBone.ArmFrontLower, !spinning && !armOut);
		rig.SetPartVisible(RigBone.PropFront, !spinning && !armOut);
		ShowTurnFrame(spinning);

		switch (State)
		{
			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Choice:
				// Holding the swords out to choose from: standing, not swinging.
				rig.Play(FighterAnimations.Idle);
				rig.Advance();
				break;

			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Resize:
			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Bomb:
				// Standing still: stretching, or about to curl up. Neither is a swing.
				rig.Play(FighterAnimations.Idle);
				rig.Advance();
				break;

			case FighterState.Attacking when currentMove != null:
				// Sampled against the move's own frame counts, so the strike pose arrives on
				// the exact frame the hitbox does.
				FighterAnimations.SampleAttack(currentMove, moveFrame, attackPose);
				rig.ApplyDirect(attackPose, FighterAnimations.AttackBlend(currentMove, moveFrame));
				break;

			case FighterState.Hitstun:
				rig.Play(FighterAnimations.Hurt);
				rig.Advance();
				break;

			case FighterState.Grounded when IsBlocking:
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
				rig.Play(FighterAnimations.Run);
				// Playback follows actual ground speed, so the feet do not skate.
				rig.Advance(Mathf.Clamp(Mathf.Abs(Velocity.X) / RunSpeed, 0.35f, 1.8f) * 1.25f);
				break;

			case FighterState.Grounded:
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
		}
	}

	void UpdateRigTint()
	{
		if (rig == null) return;

		Color tint = Colors.White;

		// The hitlag flash is what turns a freeze into an impact rather than a stutter.
		if (hitlagFrames > 0) tint = new Color(2.2f, 2.2f, 2.2f);
		else if (State == FighterState.Hitstun) tint = new Color(1.0f, 0.62f, 0.62f);
		else if (IsBlocking) tint = new Color(0.62f, 0.86f, 1.0f);
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

		// Buffering is what makes the controls feel forgiving rather than strict. An input
		// pressed slightly too early still comes out when it legally can.
		// Frames since each stick axis snapped from the centre to the edge.
		flickFramesX = Mathf.Abs(input.Move.X) > 0.8f && Mathf.Abs(previousStick.X) < 0.3f ? 0 : flickFramesX + 1;
		flickFramesY = Mathf.Abs(input.Move.Y) > 0.8f && Mathf.Abs(previousStick.Y) < 0.3f ? 0 : flickFramesY + 1;
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
			// A direction or a fresh press turns a held block into a dodge; otherwise blocking
			// costs chip damage and being slid toward the ledge by reduced knockback.
			if (Mathf.Abs(input.Move.X) > 0.5f && TryStartDodge(input)) return;
			ApplyFriction(dt, Data.GroundFriction);
			return;
		}

		if (TryStartAttack(input)) return;

		// Checked before the jump, because down plus jump is a drop-through and not a jump
		// that happens to be pressed while crouching.
		if (TryDropThrough(input)) return;

		if (jumpBufferFrames > 0)
		{
			jumpBufferFrames = 0;
			Velocity = new Vector2(Velocity.X, -Data.JumpForce);
			jumpStretchFrames = JumpStretchFrames;
			State = FighterState.Airborne;
			return;
		}

		// Holding down crouches: no walking, but the stick still turns you round.
		if (input.Move.Y > 0.5f)
		{
			IsCrouching = true;
			if (Mathf.Abs(input.Move.X) > 0.4f) Facing = input.Move.X > 0.0f ? 1 : -1;
			ApplyFriction(dt, Data.GroundFriction);
			return;
		}

		// The stick is a speed dial, not a switch: target speed is the deflection times top
		// speed, so a light push walks and a full push runs, with everything in between.
		float target = input.Move.X * RunSpeed;
		if (Mathf.Abs(input.Move.X) > MoveDeadzone)
		{
			Velocity = new Vector2(
				Mathf.MoveToward(Velocity.X, target, Data.GroundAcceleration * dt),
				Velocity.Y);
			Facing = input.Move.X > 0.0f ? 1 : -1;
		}
		else
		{
			ApplyFriction(dt, Data.GroundFriction);
		}
	}

	void TickAirborne(InputState input, float dt)
	{
		if (IsOnFloor() && Velocity.Y >= 0.0f)
		{
			airJumpsUsed = 0;
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

		if (input.BlockHeld && TryStartDodge(input)) return;
		if (TryStartAttack(input)) return;

		if (jumpBufferFrames > 0)
		{
			// Coyote time: briefly after walking off a ledge you still get your full ground
			// jump, because "I pressed jump and nothing happened" is the worst feeling there is.
			if (coyoteFrames > 0)
			{
				jumpBufferFrames = 0;
				coyoteFrames = 0;
				Velocity = new Vector2(Velocity.X, -Data.JumpForce);
			jumpStretchFrames = JumpStretchFrames;
			}
			else if (airJumpsUsed < Data.AirJumps)
			{
				jumpBufferFrames = 0;
				airJumpsUsed++;
				Velocity = new Vector2(Velocity.X, -Data.AirJumpForce);
				jumpStretchFrames = JumpStretchFrames;
			}
		}

		if (Mathf.Abs(input.Move.X) > MoveDeadzone)
		{
			float target = input.Move.X * Data.AirSpeed * SizeLevels.Speed(sizeLevel);
			Velocity = new Vector2(
				Mathf.MoveToward(Velocity.X, target, Data.AirAcceleration * dt),
				Velocity.Y);
			Facing = input.Move.X > 0.0f ? 1 : -1;
		}
		else
		{
			// Mild drag, so a fighter who exits hitstun still carries their launch momentum
			// off the stage but does not drift sideways forever with no input.
			ApplyFriction(dt, AirDrag);
		}

		// Fast-fall: free expressiveness, and the main way a player chooses to commit.
		if (input.Move.Y > 0.5f && Velocity.Y > 0.0f)
		{
			Velocity = new Vector2(Velocity.X, Mathf.Max(Velocity.Y, Data.FastFallSpeed));
		}
	}

	void TickAttacking(InputState input, float dt)
	{
		// Holding attack freezes a smash at the top of its windup, building charge.
		if (IsCharging && input.AttackHeld && chargeFrames < MaxChargeFrames)
		{
			chargeFrames++;
			if (currentMove.Spin) AdvanceSpin();
			ApplyFriction(dt, Data.GroundFriction);
			return;
		}

		moveFrame++;
		if (currentMove.Spin) AdvanceSpin();

		// A ball-form move spins the whole time, fastest while the hitbox is out.
		if (currentMove.BallForm)
		{
			bool hot = moveFrame > currentMove.StartupFrames
				&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;
			rollAngle += Facing * (hot ? 0.45f : 0.2f);
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

			case SpecialKind.Recovery when currentMove.DelayedLaunch:
				TickGrapple(activeStart);
				if (active) QueryHits();
				break;

			default:
				if (active)
				{
					SpawnSpecialHazard(currentMove);
					QueryHits();
				}
				break;
		}

		// Attacks keep their momentum but shed it - you commit to a move, you do not steer it.
		// A dash attack sheds far less, so it slides the whole way through; stopping dead on
		// startup would make it a slow jab rather than a dash attack.
		float shed = currentMove.CarriesMomentum ? 0.12f : 0.5f;
		ApplyFriction(dt, IsOnFloor() ? Data.GroundFriction * shed : Data.AirAcceleration * 0.15f);

		// The next combo hit comes out as soon as this one's hitbox is done - not after its
		// endlag, which is what makes tapping attack a fast string rather than separate jabs.
		if (comboQueued && moveFrame > activeEnd && currentMove.ComboNext != null)
		{
			currentMove = currentMove.ComboNext;
			moveFrame = 0;
			chargeFrames = 0;
			comboQueued = false;
			alreadyHitThisMove.Clear();
			return;
		}

		if (moveFrame >= currentMove.TotalFrames)
		{
			ReleaseGrabbed();
			currentMove = null;
			alreadyHitThisMove.Clear();
			State = IsOnFloor() ? FighterState.Grounded : FighterState.Airborne;
		}
	}

	void TickHitstun(float dt)
	{
		hitstunFrames--;

		// Launch velocity bleeds off at a fixed rate rather than exponentially, so a launched
		// fighter travels a distance that is proportional to the knockback that sent them.
		Velocity = Velocity.MoveToward(
			new Vector2(0.0f, Velocity.Y),
			Tuning.LaunchDecay * dt);

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
	void TickTumbling(float dt)
	{
		tumbleFrames--;
		ApplyFriction(dt, IsOnFloor() ? TumbleFriction : AirDrag);
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

	void TickRespawning()
	{
		GlobalPosition = respawnPoint;
		Velocity = Vector2.Zero;

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
		if (slot == MoveSlot.UpSpecial) upSpecialUsed = true;
		if (chosen.OncePerAirtime) usedThisAirtime.Add(chosen);

		attackBufferFrames = 0;
		specialBufferFrames = 0;

		// A special points where the stick points when it is pressed, so a recovery aimed back
		// at the stage goes toward the stage even if he was facing away from it.
		if (wantsSpecial && Mathf.Abs(input.Move.X) > 0.4f) Facing = input.Move.X > 0.0f ? 1 : -1;

		BeginMove(chosen, slot);
		return true;
	}

	/// <summary>Starts a move from its first frame, with nothing carried over from the last one.</summary>
	void BeginMove(MoveData move, MoveSlot slot)
	{
		ReleaseGrabbed();
		currentMove = move;
		currentSlot = slot;
		moveFrame = 0;
		chargeFrames = 0;
		comboQueued = false;
		alreadyHitThisMove.Clear();
		hazardSpawned = false;
		burstFired = 0;
		blinkStarted = false;
		spinAngle = 0.0f;
		spinDone = false;
		State = FighterState.Attacking;

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
		if (Mathf.Abs(Velocity.X) > RunSpeed * DashThreshold) return MoveSlot.DashAttack;

		// A flick is a smash; a direction already being held is a tilt.
		if (y < -0.5f && flickFramesY <= SmashFlickFrames) return MoveSlot.UpSmash;
		if (y > 0.5f && flickFramesY <= SmashFlickFrames) return MoveSlot.DownSmash;
		if (Mathf.Abs(x) > 0.4f && flickFramesX <= SmashFlickFrames)
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

			case SpecialKind.Dash:
				Velocity = new Vector2(Facing * move.SpecialSpeed, Velocity.Y * 0.2f);
				break;

			case SpecialKind.Recovery when move.DelayedLaunch:
				// The launch waits for the hook; startup only brakes the fall.
				hookFired = false;
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
			case SpecialKind.Projectile when move.BurstCount > 1:
				// A burst fires a shot every few frames until it has fired them all, each
				// starting a little higher or lower so they do not overlap.
				if (burstFired >= move.BurstCount) break;
				if ((moveFrame - move.StartupFrames - 1) % Mathf.Max(1, move.BurstInterval) != 0) break;
				float spread = (burstFired % 2 == 0 ? -1.0f : 1.0f) * 6.0f * (burstFired / 2 + 1) * 0.5f;
				Match.SpawnHazard(this, SizedFor(move), origin + new Vector2(0.0f, spread),
					new Vector2(Facing * move.SpecialSpeed, 0.0f));
				burstFired++;
				break;

			case SpecialKind.Projectile:
				hazardSpawned = true;
				// Only an arcing projectile gets the little upward toss - or a big one, for a
				// throw that lobs high; a beam starts at the hands and grows straight out.
				float toss = move.LaunchLift > 0.0f ? -move.LaunchLift : move.SpecialGravity > 0.0f ? -120.0f : 0.0f;
				Vector2 start = move.Beam ? BeamOrigin() : origin;
				Match.SpawnHazard(this, SizedFor(move), start, new Vector2(Facing * move.SpecialSpeed, toss));
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
				hazardSpawned = true;
				Track(move, Match.SpawnHazard(this, move, origin, Vector2.Zero));
				break;
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
	bool TryStartDodge(InputState input)
	{
		if (!input.BlockHeld || blockReleaseLagFrames > 0) return false;

		bool airborne = !IsOnFloor();
		bool directional = Mathf.Abs(input.Move.X) > 0.5f;

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
			if (GlobalPosition.DistanceSquaredTo(ledge) > LedgeSnapRadius * LedgeSnapRadius) continue;

			heldLedge = ledge;
			Facing = GlobalPosition.X < ledge.X ? 1 : -1;
			GlobalPosition = ledge + new Vector2(-Facing * bodySize.X * 0.45f, bodySize.Y * 0.42f);
			Velocity = Vector2.Zero;
			airJumpsUsed = 0;
			upSpecialUsed = false;
			buildUsed = false;
			usedThisAirtime.Clear();
			invulnFrames = Mathf.Max(invulnFrames, LedgeGrabInvulnFrames);
			State = FighterState.LedgeHang;
			return true;
		}

		return false;
	}

	void TickLedgeHang(InputState input)
	{
		Velocity = Vector2.Zero;

		// Away from the stage, or down: let go.
		bool awayFromStage = Mathf.Abs(input.Move.X) > 0.5f && Mathf.Sign(input.Move.X) != Facing;
		if (awayFromStage || input.Move.Y > 0.6f)
		{
			ReleaseLedge();
			return;
		}

		// Up, toward the stage, or jump: climb back on.
		if (jumpBufferFrames > 0 || input.Move.Y < -0.5f
			|| (Mathf.Abs(input.Move.X) > 0.5f && Mathf.Sign(input.Move.X) == Facing))
		{
			jumpBufferFrames = 0;
			ReleaseLedge();
			GlobalPosition = heldLedge + new Vector2(Facing * 30.0f, -bodySize.Y * 0.55f);
			Velocity = new Vector2(Facing * 180.0f, -Data.JumpForce * 0.72f);
			jumpStretchFrames = JumpStretchFrames;
		}
	}

	void ReleaseLedge()
	{
		ledgeCooldownFrames = LedgeRegrabCooldown;
		State = FighterState.Airborne;
	}

	/// <summary>
	/// Hit detection is done inline here rather than with Area2D signals on purpose. Godot's
	/// area overlap callbacks fire after the physics flush and are routinely a frame late,
	/// which is fatal in a game where every window is counted in frames. A circle-vs-rect test
	/// costs nothing at this scale and resolves on the exact frame the hitbox is live.
	/// </summary>
	void QueryHits()
	{
		Vector2 centre = CurrentHitboxCentre();

		foreach (Fighter other in Match.Fighters)
		{
			if (other == this || alreadyHitThisMove.Contains(other)) continue;
			if (!other.CanBeHit) continue;

			Rect2 body = other.BodyRect();
			Vector2 nearest = new Vector2(
				Mathf.Clamp(centre.X, body.Position.X, body.End.X),
				Mathf.Clamp(centre.Y, body.Position.Y, body.End.Y));

			if (nearest.DistanceSquaredTo(centre) > currentMove.HitboxRadius * currentMove.HitboxRadius)
			{
				continue;
			}

			// A multi-hit's early hits are its weak link hit; only the last window launches.
			int activeEnd = currentMove.StartupFrames + currentMove.ActiveFrames;
			MoveData hit = currentMove.LinkHit != null && moveFrame <= activeEnd - currentMove.RehitFrames
				? currentMove.LinkHit
				: currentMove;

			alreadyHitThisMove.Add(other);
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
		&& State != FighterState.Eliminated
		&& State != FighterState.Respawning;

	// --- Taking a hit --------------------------------------------------------

	/// <param name="damageScale">More than 1 for a charged smash. Knockback follows damage.</param>
	public void ReceiveHit(Fighter attacker, MoveData move, Vector2 contactPoint, float damageScale = 1.0f)
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

		// Blocking reduces; it never negates. Chip damage still raises percent, which is the
		// entire cost of blocking - there is no shield health here by design.
		float damage = move.Damage * damageScale * (blocked ? Tuning.BlockDamageMultiplier : 1.0f);
		Percent += damage;

		float knockback = Knockback.Compute(
			Percent, damage, Data.BodyWeight, move.BaseKnockback, move.KnockbackGrowth);

		if (blocked) knockback *= Tuning.BlockKnockbackMultiplier;

		Velocity = Knockback.LaunchVelocity(knockback, move.LaunchAngleDegrees, attacker.Facing, lastStick);
		hitstunFrames = Knockback.HitstunFrames(knockback);
		hitlagFrames = Knockback.HitlagFrames(damage);
		State = FighterState.Hitstun;
		IsBlocking = false;

		// A fighter who tumbles is knocked over by any real hit, and rolls from the moment it lands.
		if (Data.TumblesWhenHit && !blocked && knockback >= TumbleKnockback)
		{
			drawnAsBall = true;
			pendingTumbleFrames = Mathf.Clamp(Mathf.RoundToInt(knockback * 0.2f), 12, 36);
		}

		// Being launched gives the air jump back, so a fighter knocked off the stage always has a
		// jump to come home with once hitstun ends - even one who had spent it before the hit.
		if (!blocked) airJumpsUsed = 0;

		if (blocked)
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
		buildUsed = false;
		usedThisAirtime.Clear();
		ReleaseGrabbed();
		heldBy = null;
		drawnAsBall = false;
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
		State = FighterState.Respawning;
		GlobalPosition = spawnPoint;
	}

	// --- Helpers -------------------------------------------------------------

	void ApplyGravity(float dt)
	{
		if (IsOnFloor() && State != FighterState.Hitstun) return;

		float maxFall = Data.MaxFallSpeed;
		if (Velocity.Y < maxFall)
		{
			Velocity = new Vector2(Velocity.X, Mathf.Min(maxFall, Velocity.Y + Data.Gravity * dt));
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

		return new Rect2(GlobalPosition - bodySize * 0.5f, bodySize);
	}

	public bool IsInvulnerable => invulnFrames > 0;

	/// <summary>Still on the stage and fighting - not respawning and not out of the match.</summary>
	public bool IsInPlay => State != FighterState.Eliminated && State != FighterState.Respawning;

	/// <summary>Whether the air jump is still available. Read by the CPU to plan a recovery.</summary>
	public bool HasAirJump => airJumpsUsed < Data.AirJumps;

	/// <summary>The move in progress, or null. Read by the CPU to see an attack coming.</summary>
	public MoveData CurrentMove => State == FighterState.Attacking ? currentMove : null;

	/// <summary>
	/// Where a beam comes out: his hands, held out in front of the body. It is asked every frame,
	/// so a beam fired in the air follows him down.
	/// </summary>
	public Vector2 BeamOrigin() => GlobalPosition + new Vector2(Facing * bodySize.X * 0.45f, -bodySize.Y * 0.05f);

	/// <summary>
	/// The charge before a beam: a glow at his hands that grows until it fires, so a player can
	/// see it coming and get out of the way. That warning is what makes a long beam fair.
	/// </summary>
	void DrawBeamCharge()
	{
		if (State != FighterState.Attacking || currentMove == null || !currentMove.Beam) return;
		if (moveFrame > currentMove.StartupFrames) return;

		float t = moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames);
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
		bodyShape.Size = bodySize;
		GlobalPosition -= new Vector2(0.0f, (bodySize.Y - oldHeight) * 0.5f);

		rig?.SetLegStretch(stretch, bodySize.Y * 0.5f);
	}

	public int SizeLevel => sizeLevel;

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

	// --- Grappling hook ----------------------------------------------------------

	/// <summary>
	/// Hangs through startup while the hook flies out, then yanks him along it on the first
	/// active frame. The hook never has to catch on anything - a recovery that can miss loses
	/// stocks for reasons a player cannot see.
	/// </summary>
	void TickGrapple(int activeStart)
	{
		if (moveFrame <= activeStart)
		{
			Velocity = new Vector2(Velocity.X * 0.8f, Mathf.Min(Velocity.Y * 0.8f, 120.0f));
			if (currentMove.GrabThrow != null) TickGrab(activeStart);
			return;
		}

		if (hookFired) return;
		hookFired = true;

		// The throw goes on the same frame as the launch: one yank flings them down and him up.
		if (grabbed != null) ThrowGrabbed();

		Vector2 pull = new Vector2(Facing * currentMove.SpecialSpeed, -currentMove.SpecialRise);
		hookPoint = HookOrigin() + pull.Normalized() * currentMove.TetherLength;
		Velocity = pull;
	}

	Vector2 HookOrigin() => GlobalPosition + new Vector2(Facing * bodySize.X * 0.3f, -bodySize.Y * 0.15f);

	Vector2 TetherDirection() =>
		new Vector2(Facing * currentMove.SpecialSpeed, -currentMove.SpecialRise).Normalized();

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
			Vector2 tip = HookOrigin() + TetherDirection() * currentMove.TetherLength * TetherReach();
			const float Reach = 36.0f;
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
				other.BeginHeld(this);
				break;
			}
			return;
		}

		float t = (moveFrame - grabFrame) / (float)Mathf.Max(1, activeStart - grabFrame);
		Vector2 hand = HookOrigin() + new Vector2(Facing * bodySize.X * 0.7f, 0.0f);
		grabbed.GlobalPosition = grabPoint.Lerp(hand, t * t);
	}

	void ThrowGrabbed()
	{
		Fighter victim = grabbed;
		grabbed = null;
		victim.heldBy = null;

		// The launch that follows must not hit them a second time.
		alreadyHitThisMove.Add(victim);
		victim.ReceiveHit(this, currentMove.GrabThrow, victim.GlobalPosition);
		hitlagFrames = Knockback.HitlagFrames(currentMove.GrabThrow.Damage);
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

		// A little over a quarter turn of arc, centred on the hit, drawn in the fighter's own
		// colour with an ink edge so it reads on any stage.
		const float Sweep = 0.9f;
		Color colour = Data.PlaceholderColor;
		colour.A = 0.5f * fade;
		DrawArc(pivot, reach, angle - Sweep, angle + Sweep, 20, colour, currentMove.HitboxRadius * 0.55f);
		DrawArc(pivot, reach + currentMove.HitboxRadius * 0.3f, angle - Sweep, angle + Sweep, 20,
			new Color(0.16f, 0.16f, 0.20f, 0.55f * fade), 3.0f);
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
		DrawArtTransform(at, 0.0f, new Vector2(s * Facing, s));
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
		RigArt art = rig?.PoseArtFor(currentMove.SwingArt);
		if (art == null) return;

		Vector2 hand = HookOrigin() - GlobalPosition;
		Vector2 ball = hand + SwingDirection() * currentMove.SwingLength;
		CrayonBrush.InkLine(this, hand, ball, new Color(0.30f, 0.30f, 0.34f), 5.0f, 61, 1.0f);

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
		const float Lead = -70.0f;   // up and forward
		float t = Mathf.Clamp(moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames), 0.0f, 1.0f);
		float angle = Mathf.DegToRad(Mathf.Lerp(Start, Lead, t * t));
		return new Vector2(Mathf.Cos(angle) * Facing, Mathf.Sin(angle));
	}

	void DrawTether()
	{
		if (State != FighterState.Attacking || currentMove == null || !currentMove.DelayedLaunch) return;
		if (!string.IsNullOrEmpty(currentMove.SwingArt)) return;
		if (moveFrame > currentMove.StartupFrames + currentMove.ActiveFrames) return;

		Vector2 dir = new Vector2(Facing * currentMove.SpecialSpeed, -currentMove.SpecialRise).Normalized();
		float reach = Mathf.Clamp(moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames), 0.0f, 1.0f);
		Vector2 tip = hookFired ? hookPoint : HookOrigin() + dir * currentMove.TetherLength * reach;
		if (grabbed != null) tip = grabbed.GlobalPosition;

		Vector2 from = HookOrigin() - GlobalPosition;
		Vector2 to = tip - GlobalPosition;

		if (currentMove.StretchArm && DrawStretchedArm(from, to)) return;

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
	/// Four swords laid out round him; the first push of the stick picks one. Back turns him
	/// round, so the sword he picked behind him flies the way he now faces. A tap with no push
	/// throws the forward one when the window closes. Returns true once it has swapped moves.
	/// </summary>
	bool TickChoice(InputState input, int activeStart, int activeEnd)
	{
		if (moveFrame <= activeStart || currentMove.Choices.Length < 4) return false;

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

		if (pick == 2) Facing = -Facing;
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
			}
			Velocity = new Vector2(Facing * currentMove.SpecialSpeed, 0.0f);
			return;
		}

		if (moveFrame == activeEnd + 1) Velocity = new Vector2(Facing * 240.0f, 0.0f);
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

	bool IsArmStretched =>
		State == FighterState.Attacking && currentMove != null && currentMove.StretchArm
		&& moveFrame <= currentMove.StartupFrames + currentMove.ActiveFrames;

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

	/// <summary>His own forearm, hand and all, stretched from the shoulder out to the tether's tip.</summary>
	bool DrawStretchedArm(Vector2 from, Vector2 to)
	{
		Texture2D arm = rig?.PartTexture(RigBone.ArmFrontLower);
		if (arm == null) return false;

		Vector2 span = to - from;
		float length = span.Length();
		if (length < 1.0f) return true;

		Vector2 pivot = rig.PartPivot(RigBone.ArmFrontLower);
		float drawn = Mathf.Max(1.0f, arm.GetSize().Y - pivot.Y);
		// A touch thicker than the arm at rest, so a long thin stretch still reads.
		float across = rig.PuppetScale * 1.3f * Facing;
		DrawArtOn(this, arm, pivot, from, span.Angle() - Mathf.Pi * 0.5f, new Vector2(across, length / drawn), rig.Modulate);
		return true;
	}

	void DrawOverlay()
	{
		if (State == FighterState.Eliminated || rig == null || !rig.Loaded) return;
		DrawChoiceMenu(overlay);
		DrawSpinBlades(overlay, true);
		DrawBlinkGlint(overlay);
	}

	/// <summary>The four swords on offer, each pointing the way it sits, popping open around him.</summary>
	void DrawChoiceMenu(CanvasItem canvas)
	{
		if (State != FighterState.Attacking || currentMove == null || currentMove.Special != SpecialKind.Choice) return;
		if (currentMove.Choices.Length < 4) return;

		float open = Mathf.Min(1.0f, moveFrame / 6.0f);
		// Clear of the drawing, which stands taller than the hurtbox.
		Vector2 centre = new Vector2(0.0f, -bodySize.Y * 0.15f);
		float radius = bodySize.Y * 0.95f * open;
		Vector2[] dirs = { Vector2.Up, new Vector2(Facing, 0.0f), new Vector2(-Facing, 0.0f), Vector2.Down };

		for (int i = 0; i < 4; i++)
		{
			Vector2 at = centre + dirs[i] * radius;
			float disc = 46.0f * open;
			// Paper discs with a mid-grey rim: the swords themselves carry the dark ink.
			canvas.DrawCircle(at, disc, new Color(0.97f, 0.95f, 0.89f, 0.85f));
			canvas.DrawArc(at, disc, 0.0f, Mathf.Tau, 32, new Color(0.42f, 0.40f, 0.46f, 0.9f), 3.0f);

			Texture2D art = currentMove.Choices[i].FxTexture;
			if (art == null) continue;
			Vector2 size = art.GetSize();
			float s = 78.0f * open / Mathf.Max(size.X, size.Y);
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
	/// just before he goes. (There is no sound yet. When there is, this wants a rising ring that
	/// peaks on the same frame - see .ai/fighting-design.md.)
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
		Vector2 centre = new Vector2(0.0f, bodySize.Y * 0.5f - size.Y * 0.5f * s);

		DrawArtTransform(centre, angle, new Vector2(s * Facing, s));
		DrawTexture(body, -size * 0.5f, rig.Modulate);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
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
	/// standing tall or small while he stretches, looking out at the player and then shrinking
	/// before the bomb, the bomb itself, and the shoulder bash while the hook pulls him in.
	/// Null means the puppet is showing.
	/// </summary>
	string HeldPoseName()
	{
		if (rig == null || !rig.Loaded || State != FighterState.Attacking || currentMove == null) return null;

		string name = null;
		int activeEnd = currentMove.StartupFrames + currentMove.ActiveFrames;

		switch (currentMove.Special)
		{
			case SpecialKind.Resize:
				name = sizeLevel > 0 ? "tall" : sizeLevel < 0 ? "small" : "stand";
				break;

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

	// --- Drawing (M1 placeholder) --------------------------------------------

	public override void _Draw()
	{
		if (State == FighterState.Eliminated) return;

		// Once a rig is loaded the puppet IS the fighter; the rectangle below only exists so a
		// missing or broken manifest degrades to something playable instead of invisible.
		if (rig != null && rig.Loaded)
		{
			string pose = HeldPoseName();
			if (IsDrawnAsBall) DrawBall(1.0f, rollAngle);
			else if (pose != null) DrawHeldPose(pose);
			else if (IsBombArmed) DrawBomb();
			DrawBlinkTrail();
			DrawSpinBlades(this, false);
			DrawSwingTrail();
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
