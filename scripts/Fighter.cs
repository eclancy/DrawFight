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

	// --- Active move ---------------------------------------------------------

	MoveData currentMove;
	MoveSlot currentSlot;
	int moveFrame;
	int specialBufferFrames;
	int dodgeStartFrames;
	bool hazardSpawned;
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

		if (string.IsNullOrEmpty(data.RigPath)) return;

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
			QueueRedraw();
			return;
		}

		if (State == FighterState.Eliminated)
		{
			return;
		}

		InputState input = Controller?.Poll() ?? InputState.None;
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

		if (dropThroughFrames > 0 && --dropThroughFrames == 0)
		{
			CollisionMask = GroundMask;
		}

		UpdateGroundInfo();
		UpdateRig();
		UpdateRigTint();
		QueueRedraw();
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

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision2D collision = GetSlideCollision(i);

			// Floor normals point up, which is negative Y on screen.
			if (collision.GetNormal().Y > -0.7f) continue;

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

		// Knocked over, or curled into a bomb: the limbs are gone and he is drawn as one piece in
		// _Draw. Elim asked for exactly that - "get rid of his arms and legs until he gets back up".
		rig.Visible = HeldPoseName() == null && !IsDrawnAsBall && !IsBombArmed;
		if (!rig.Visible) return;

		switch (State)
		{
			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Resize:
			case FighterState.Attacking when currentMove != null && currentMove.Special == SpecialKind.Bomb:
				// Standing still: stretching, or about to curl up. Neither is a swing.
				rig.Play(FighterAnimations.Idle);
				rig.Advance();
				break;

			case FighterState.Attacking when currentMove != null:
				// Sampled against the move's own frame counts, so the strike pose arrives on
				// the exact frame the hitbox does.
				FighterAnimations.SampleAttack(currentMove, moveFrame, attackPose, currentMove.CarriesMomentum);
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
			coyoteFrames = Tuning.CoyoteFrames;
			State = FighterState.Airborne;
			return;
		}

		airJumpsUsed = 0;

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
			State = FighterState.Airborne;
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
			}
			else if (airJumpsUsed < Data.AirJumps)
			{
				jumpBufferFrames = 0;
				airJumpsUsed++;
				Velocity = new Vector2(Velocity.X, -Data.AirJumpForce);
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
		moveFrame++;

		int activeStart = currentMove.StartupFrames;
		int activeEnd = currentMove.StartupFrames + currentMove.ActiveFrames;

		bool active = moveFrame > activeStart && moveFrame <= activeEnd;

		switch (currentMove.Special)
		{
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

		if (moveFrame >= currentMove.TotalFrames)
		{
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
	/// Picks which of the fourteen moves this press means, from the state the fighter is in
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

		attackBufferFrames = 0;
		specialBufferFrames = 0;

		// A special points where the stick points when it is pressed, so a recovery aimed back
		// at the stage goes toward the stage even if he was facing away from it.
		if (wantsSpecial && Mathf.Abs(input.Move.X) > 0.4f) Facing = input.Move.X > 0.0f ? 1 : -1;

		currentMove = chosen;
		currentSlot = slot;
		moveFrame = 0;
		alreadyHitThisMove.Clear();
		hazardSpawned = false;
		State = FighterState.Attacking;

		StartSpecialMotion(chosen);
		return true;
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
			case SpecialKind.Projectile:
				hazardSpawned = true;
				// Only an arcing projectile gets the little upward toss; a beam starts at the hands
				// and grows straight out.
				float toss = move.SpecialGravity > 0.0f ? -120.0f : 0.0f;
				Vector2 start = move.Beam ? BeamOrigin() : origin;
				Match.SpawnHazard(this, SizedFor(move), start, new Vector2(Facing * move.SpecialSpeed, toss));
				break;

			case SpecialKind.Drop:
				hazardSpawned = true;
				Match.SpawnHazard(this, move, origin, new Vector2(Velocity.X * 0.3f, move.SpecialSpeed));
				break;

			case SpecialKind.Trap:
				hazardSpawned = true;
				Match.SpawnHazard(this, move, origin, Vector2.Zero);
				break;
		}
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

			alreadyHitThisMove.Add(other);
			other.ReceiveHit(this, currentMove, nearest);

			// "He will also take some damage." Percent only - never knockback.
			Percent += currentMove.SelfDamage;

			// The attacker shares the victim's hitlag, so both sides feel the impact.
			hitlagFrames = Knockback.HitlagFrames(currentMove.Damage);
		}
	}

	public Vector2 CurrentHitboxCentre()
	{
		if (currentMove == null) return GlobalPosition;
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

	public void ReceiveHit(Fighter attacker, MoveData move, Vector2 contactPoint)
	{
		// Hitting a bomb sets it off. The hit itself does nothing; the explosion is the answer.
		if (IsBombArmed)
		{
			DetonateBomb();
			return;
		}

		bool blocked = IsBlocking && !move.Unblockable;

		// Blocking reduces; it never negates. Chip damage still raises percent, which is the
		// entire cost of blocking - there is no shield health here by design.
		float damage = blocked ? move.Damage * Tuning.BlockDamageMultiplier : move.Damage;
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

		return new Rect2(GlobalPosition - bodySize * 0.5f, bodySize);
	}

	public bool IsInvulnerable => invulnFrames > 0;

	/// <summary>Still on the stage and fighting - not respawning and not out of the match.</summary>
	public bool IsInPlay => State != FighterState.Eliminated && State != FighterState.Respawning;

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
			return;
		}

		if (hookFired) return;
		hookFired = true;

		Vector2 pull = new Vector2(Facing * currentMove.SpecialSpeed, -currentMove.SpecialRise);
		hookPoint = HookOrigin() + pull.Normalized() * currentMove.TetherLength;
		Velocity = pull;
	}

	Vector2 HookOrigin() => GlobalPosition + new Vector2(Facing * bodySize.X * 0.3f, -bodySize.Y * 0.15f);

	void DrawTether()
	{
		if (State != FighterState.Attacking || currentMove == null || !currentMove.DelayedLaunch) return;
		if (moveFrame > currentMove.StartupFrames + currentMove.ActiveFrames) return;

		Vector2 dir = new Vector2(Facing * currentMove.SpecialSpeed, -currentMove.SpecialRise).Normalized();
		float reach = Mathf.Clamp(moveFrame / (float)Mathf.Max(1, currentMove.StartupFrames), 0.0f, 1.0f);
		Vector2 tip = hookFired ? hookPoint : HookOrigin() + dir * currentMove.TetherLength * reach;

		Vector2 from = HookOrigin() - GlobalPosition;
		Vector2 to = tip - GlobalPosition;

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

	bool IsDrawnAsBall => drawnAsBall && (State == FighterState.Hitstun || State == FighterState.Tumbling);

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
			DrawTether();
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
