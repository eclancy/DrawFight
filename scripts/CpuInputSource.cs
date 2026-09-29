using Godot;

/// <summary>
/// A computer player. It is an <see cref="IInputSource"/> like a pad or a keyboard: every frame
/// it looks at the match and reports which buttons it is "pressing", and <see cref="Fighter"/>
/// cannot tell the difference. That is the whole design - the CPU plays by exactly the same
/// rules, frame windows and buffers as a person, so it can never do anything a player cannot.
///
/// It aims for "fun to play against", not "good" (see .ai/roadmap.md, M7). Mostly that means:
///   - it only makes a decision every so often, and holds it in between - a reaction time
///   - sometimes it decides to do nothing at all
///   - it gets home when knocked off, because a CPU that falls off by itself is no test
///
/// A decision is one of: recover, get out of hitstun, block, attack, jump, approach, or wait.
/// </summary>
public sealed class CpuInputSource : IInputSource
{
	readonly MatchManager match;
	readonly RandomNumberGenerator rng = new RandomNumberGenerator();

	Fighter self;
	int frame;
	int nextDecision;
	int blockUntil;
	int jumpCooldown;
	int ledgeFrames;
	bool usedRecovery;

	// What it is doing until the next decision. Buttons are pressed only on the decision frame;
	// the stick is held throughout, because a buffered move reads the stick when it comes out.
	Vector2 heldStick;
	bool pressJump, pressAttack, pressSpecial;

	/// <summary>Frames between decisions: the CPU's reaction time. About a sixth of a second.</summary>
	const int ReactionFrames = 10;
	const int ReactionJitter = 5;

	/// <summary>How often a decision is simply "do nothing" - the main thing keeping it beatable.</summary>
	const float IdleChance = 0.15f;

	const float BlockChance = 0.3f;
	const float CloseRange = 115.0f;

	public CpuInputSource(MatchManager match, int seed)
	{
		this.match = match;
		rng.Seed = (ulong)(seed * 7919 + 17);
	}

	/// <summary>The fighter this CPU drives. Set once the fighter exists.</summary>
	public void Drive(Fighter fighter) => self = fighter;

	public InputState Poll()
	{
		frame++;
		if (self == null || match == null || !self.IsInPlay) return InputState.None;

		if (frame >= nextDecision)
		{
			Decide();
			nextDecision = frame + ReactionFrames + rng.RandiRange(-ReactionJitter, ReactionJitter);
		}
		if (jumpCooldown > 0) jumpCooldown--;

		var state = new InputState
		{
			Move = heldStick,
			JumpPressed = pressJump,
			AttackPressed = pressAttack,
			SpecialPressed = pressSpecial,
			SpecialHeld = pressSpecial,
			BlockHeld = frame < blockUntil,
		};

		pressJump = pressAttack = pressSpecial = false;
		return state;
	}

	void Decide()
	{
		heldStick = Vector2.Zero;

		if (self.IsOnFloor() || self.State == FighterState.LedgeHang) usedRecovery = false;

		switch (self.State)
		{
			case FighterState.Hitstun:
			case FighterState.Tumbling:
				// Lean back toward the middle of the stage, which also angles the launch (DI).
				heldStick = new Vector2(Mathf.Sign(StageCentreX() - self.GlobalPosition.X), -0.6f);
				return;

			case FighterState.LedgeHang:
				// Hang for a moment, then climb - hanging forever is a free hit for the other player.
				if (++ledgeFrames > 2)
				{
					ledgeFrames = 0;
					heldStick = new Vector2(0.0f, -1.0f);
				}
				return;

			case FighterState.Attacking:
			case FighterState.Dodging:
				return;
		}

		ledgeFrames = 0;

		if (!self.IsOnFloor() && !GroundBelow())
		{
			Recover();
			return;
		}

		Fighter target = NearestOpponent();
		if (target == null)
		{
			// Nobody to fight: drift back to the middle and wait.
			float home = StageCentreX() - self.GlobalPosition.X;
			if (Mathf.Abs(home) > 80.0f) heldStick = new Vector2(Mathf.Sign(home) * 0.6f, 0.0f);
			return;
		}

		if (rng.Randf() < IdleChance) return;

		Vector2 toTarget = target.GlobalPosition - self.GlobalPosition;
		float dx = toTarget.X;
		float dy = toTarget.Y;
		float distance = Mathf.Abs(dx);
		int toward = dx >= 0.0f ? 1 : -1;

		// Something is coming: sometimes block it.
		if (self.IsOnFloor() && target.CurrentMove != null && distance < 170.0f && rng.Randf() < BlockChance)
		{
			blockUntil = frame + rng.RandiRange(12, 22);
			return;
		}

		// Close enough to hit.
		if (distance < CloseRange && Mathf.Abs(dy) < 110.0f)
		{
			Attack(toward, dy);
			return;
		}

		// Above us and nearby: go up after them.
		if (self.IsOnFloor() && dy < -140.0f && distance < 260.0f && jumpCooldown == 0)
		{
			pressJump = true;
			jumpCooldown = 30;
			heldStick = new Vector2(toward * 0.5f, 0.0f);
			return;
		}

		// A long way off: sometimes use a side special - a projectile or a dash closes the gap.
		if (distance > 380.0f && rng.Randf() < 0.22f)
		{
			heldStick = new Vector2(toward, 0.0f);
			pressSpecial = true;
			return;
		}

		// Otherwise walk or run at them - but never off the edge after someone already off it.
		if (!(self.IsOnFloor() && NearEdge(toward) && !target.IsOnFloor()))
		{
			heldStick = new Vector2(toward * rng.RandfRange(0.65f, 1.0f), 0.0f);
		}
	}

	void Attack(int toward, float dy)
	{
		if (!self.IsOnFloor())
		{
			// Aerials by where the target is: above, below, or in front.
			heldStick = dy < -60.0f ? new Vector2(0.0f, -1.0f)
				: dy > 60.0f ? new Vector2(0.0f, 1.0f)
				: new Vector2(toward, 0.0f);
			pressAttack = true;
			return;
		}

		// The stick decides tilt or smash. At 0.7 it is a tilt; snapped to full on the same frame as
		// the button it reads as a flick, which is a smash - exactly as it would for a person.
		float push = rng.Randf() < 0.2f ? 1.0f : 0.7f;

		if (dy < -70.0f)
		{
			heldStick = new Vector2(0.0f, -push);
		}
		else if (dy > 40.0f)
		{
			// Lower than us - hanging off a ledge, or crouched: the down attacks are the low hits.
			heldStick = new Vector2(0.0f, push);
		}
		else
		{
			// Mostly forward attacks facing them, sometimes a jab combo.
			heldStick = rng.Randf() < 0.6f ? new Vector2(toward * push, 0.0f) : Vector2.Zero;
		}

		// A down special now and then, close in - Circy's bomb, Swift's fire trap, Lug's anvil.
		if (rng.Randf() < 0.08f)
		{
			heldStick = new Vector2(0.0f, 1.0f);
			pressSpecial = true;
			return;
		}

		pressAttack = true;
	}

	/// <summary>
	/// Off the stage with nothing underneath: steer home, use the air jump once falling, then the
	/// up special. This is what separates a CPU worth practising against from one that falls off.
	/// </summary>
	void Recover()
	{
		Vector2 target = RecoveryTarget();
		float dx = target.X - self.GlobalPosition.X;
		int toward = dx >= 0.0f ? 1 : -1;
		bool falling = self.Velocity.Y > 0.0f;
		bool low = self.GlobalPosition.Y > target.Y - 40.0f;

		heldStick = new Vector2(toward, 0.0f);

		if (falling && self.HasAirJump && jumpCooldown == 0)
		{
			pressJump = true;
			jumpCooldown = 20;
			return;
		}

		if (falling && low && !self.HasAirJump && !usedRecovery)
		{
			usedRecovery = true;
			heldStick = new Vector2(toward * 0.5f, -1.0f);
			pressSpecial = true;
		}
	}

	Fighter NearestOpponent()
	{
		Fighter best = null;
		float bestDistance = float.MaxValue;
		foreach (Fighter other in match.Fighters)
		{
			if (other == self || !other.IsInPlay) continue;
			float d = other.GlobalPosition.DistanceSquaredTo(self.GlobalPosition);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = other;
			}
		}
		return best;
	}

	/// <summary>Anything to land on straight below - solid ground or a soft platform.</summary>
	bool GroundBelow()
	{
		PhysicsDirectSpaceState2D space = self.GetWorld2D().DirectSpaceState;
		var query = PhysicsRayQueryParameters2D.Create(
			self.GlobalPosition, self.GlobalPosition + new Vector2(0.0f, 1600.0f), 0b11);
		return space.IntersectRay(query).Count > 0;
	}

	bool NearEdge(int direction)
	{
		PhysicsDirectSpaceState2D space = self.GetWorld2D().DirectSpaceState;
		Vector2 ahead = self.GlobalPosition + new Vector2(direction * 90.0f, 0.0f);
		var query = PhysicsRayQueryParameters2D.Create(ahead, ahead + new Vector2(0.0f, 400.0f), 0b11);
		return space.IntersectRay(query).Count == 0;
	}

	/// <summary>The nearest ledge, or the middle of the stage if it has none.</summary>
	Vector2 RecoveryTarget()
	{
		Vector2 best = new Vector2(StageCentreX(), self.GlobalPosition.Y);
		float bestDistance = float.MaxValue;
		foreach (Vector2 ledge in match.Ledges)
		{
			float d = ledge.DistanceSquaredTo(self.GlobalPosition);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = ledge;
			}
		}
		return best;
	}

	float StageCentreX() => match.StageBounds.GetCenter().X;
}
