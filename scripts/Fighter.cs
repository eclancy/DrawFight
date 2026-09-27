using System.Collections.Generic;
using Godot;

public enum FighterState
{
	Grounded,
	Airborne,
	Attacking,
	Hitstun,
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
	int moveFrame;
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
		CollisionMask = 1;

		var shape = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = data.BodySize },
		};
		AddChild(shape);

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
			case FighterState.Hitstun: TickHitstun(dt); break;
			case FighterState.Respawning: TickRespawning(); break;
		}

		if (State != FighterState.Respawning)
		{
			ApplyGravity(dt);
			MoveAndSlide();
		}

		if (landFrames > 0) landFrames--;

		UpdateRig();
		UpdateRigTint();
		QueueRedraw();
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

		switch (State)
		{
			case FighterState.Attacking when currentMove != null:
				// Sampled against the move's own frame counts, so the strike pose arrives on
				// the exact frame the hitbox does.
				FighterAnimations.SampleAttack(currentMove, moveFrame, attackPose);
				rig.ApplyDirect(attackPose);
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
				rig.Advance(Mathf.Clamp(Mathf.Abs(Velocity.X) / Data.RunSpeed, 0.35f, 1.8f) * 1.25f);
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
		if (jumpBufferFrames > 0) jumpBufferFrames--;
		if (attackBufferFrames > 0) attackBufferFrames--;

		// Buffering is what makes the controls feel forgiving rather than strict. An input
		// pressed slightly too early still comes out when it legally can.
		if (input.JumpPressed) jumpBufferFrames = Tuning.JumpBufferFrames;
		if (input.AttackPressed) attackBufferFrames = Tuning.AttackBufferFrames;

		bool wantsBlock = input.BlockHeld
			&& State == FighterState.Grounded
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
			// Cannot move or attack while blocking. The cost of holding it is chip damage
			// raising your percent, plus being slid toward the ledge by reduced knockback.
			ApplyFriction(dt, Data.GroundFriction);
			return;
		}

		if (TryStartAttack()) return;

		if (jumpBufferFrames > 0)
		{
			jumpBufferFrames = 0;
			Velocity = new Vector2(Velocity.X, -Data.JumpForce);
			State = FighterState.Airborne;
			return;
		}

		float target = input.Move.X * Data.RunSpeed;
		if (Mathf.Abs(input.Move.X) > 0.2f)
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

		if (TryStartAttack()) return;

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

		if (Mathf.Abs(input.Move.X) > 0.2f)
		{
			float target = input.Move.X * Data.AirSpeed;
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

		if (moveFrame > activeStart && moveFrame <= activeEnd)
		{
			QueryHits();
		}

		// Attacks keep their momentum but shed it - you commit to a move, you do not steer it.
		ApplyFriction(dt, IsOnFloor() ? Data.GroundFriction * 0.5f : Data.AirAcceleration * 0.15f);

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

		if (hitstunFrames <= 0)
		{
			State = IsOnFloor() ? FighterState.Grounded : FighterState.Airborne;
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

	bool TryStartAttack()
	{
		if (attackBufferFrames <= 0 || Data.Jab == null) return false;

		attackBufferFrames = 0;
		currentMove = Data.Jab;
		moveFrame = 0;
		alreadyHitThisMove.Clear();
		State = FighterState.Attacking;
		return true;
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

	bool CanBeHit =>
		invulnFrames <= 0
		&& State != FighterState.Eliminated
		&& State != FighterState.Respawning;

	// --- Taking a hit --------------------------------------------------------

	public void ReceiveHit(Fighter attacker, MoveData move, Vector2 contactPoint)
	{
		bool blocked = IsBlocking && !move.Unblockable;

		// Blocking reduces; it never negates. Chip damage still raises percent, which is the
		// entire cost of blocking - there is no shield health here by design.
		float damage = blocked ? move.Damage * Tuning.BlockDamageMultiplier : move.Damage;
		Percent += damage;

		float knockback = Knockback.Compute(
			Percent, damage, Data.Weight, move.BaseKnockback, move.KnockbackGrowth);

		if (blocked) knockback *= Tuning.BlockKnockbackMultiplier;

		Velocity = Knockback.LaunchVelocity(knockback, move.LaunchAngleDegrees, attacker.Facing, lastStick);
		hitstunFrames = Knockback.HitstunFrames(knockback);
		hitlagFrames = Knockback.HitlagFrames(damage);
		State = FighterState.Hitstun;
		IsBlocking = false;

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
		return new Rect2(GlobalPosition - Data.BodySize * 0.5f, Data.BodySize);
	}

	public bool IsInvulnerable => invulnFrames > 0;
	public bool IsInHitlag => hitlagFrames > 0;

	// --- Drawing (M1 placeholder) --------------------------------------------

	public override void _Draw()
	{
		if (State == FighterState.Eliminated) return;

		// Once a rig is loaded the puppet IS the fighter; the rectangle below only exists so a
		// missing or broken manifest degrades to something playable instead of invisible.
		if (rig != null && rig.Loaded) return;

		Color body = Data.PlaceholderColor;

		// Flash white during hitlag so the freeze reads as an impact rather than a stutter.
		if (hitlagFrames > 0) body = Colors.White;
		else if (State == FighterState.Hitstun) body = body.Lerp(new Color(1.0f, 0.4f, 0.4f), 0.5f);
		else if (IsBlocking) body = body.Lerp(new Color(0.4f, 0.8f, 1.0f), 0.6f);

		// Blink while respawn-invulnerable.
		if (invulnFrames > 0 && (invulnFrames / 4) % 2 == 0) body.A = 0.45f;

		var rect = new Rect2(-Data.BodySize * 0.5f, Data.BodySize);
		DrawRect(rect, body);
		DrawRect(rect, new Color(0.08f, 0.08f, 0.1f), false, 3.0f);

		// Facing indicator - a stand-in for "which way is this fighter pointing", which the
		// side-view drawing answers on its own from M2 onward.
		var eye = new Rect2(
			new Vector2(Facing * (Data.BodySize.X * 0.5f - 16.0f) - 6.0f, -Data.BodySize.Y * 0.32f),
			new Vector2(12.0f, 12.0f));
		DrawRect(eye, new Color(0.08f, 0.08f, 0.1f));
	}
}
