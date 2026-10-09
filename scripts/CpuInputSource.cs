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
/// A decision is one of: recover, get out of hitstun, block, attack, jump, close in or back off,
/// or wait.
///
/// It swings only with a move that would land, judged the way the game judges a hit - the move's
/// hitbox against the target's body - and otherwise keeps to the distance its own forward tilt
/// reaches. A fighter with a sledgehammer stands back and swings; one with fists closes in. The
/// first version attacked anything within a fixed 115 pixels, so the long-reach fighters walked
/// in on top of people and swung clean past them.
/// </summary>
public sealed class CpuInputSource : IInputSource
{
	readonly MatchManager match;
	/// <summary>
	/// System.Random rather than Godot's RandomNumberGenerator. This is not a node, so it has no
	/// moment to let go of a Godot object, and one still held when the game quits is cleaned up
	/// after the engine's C# side has shut down - which crashed the game on the way out.
	/// </summary>
	readonly System.Random rng;

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
	bool pressJump, pressAttack, pressSpecial, pressSmash;

	/// <summary>Going over an edge on purpose this decision - down to someone below - so the brake leaves it be.</summary>
	bool meansToDrop;

	/// <summary>One way to attack: the stick and buttons that ask for a move, and how often to pick it.</summary>
	readonly struct Option
	{
		public readonly Vector2 Stick;
		public readonly bool Smash;
		public readonly float Weight;

		public Option(Vector2 stick, bool smash, float weight)
		{
			Stick = stick;
			Smash = smash;
			Weight = weight;
		}
	}

	/// <summary>The attacks that would land right now, rebuilt each decision.</summary>
	readonly System.Collections.Generic.List<Option> options = new System.Collections.Generic.List<Option>();

	// These are the difficulty. Tuned down after the first version was too hard to beat - it
	// reacted in a sixth of a second and swung at every chance it got.

	/// <summary>Frames between decisions: the CPU's reaction time. About a third of a second.</summary>
	const int ReactionFrames = 20;
	const int ReactionJitter = 6;

	/// <summary>How often a decision is simply "do nothing" - the main thing keeping it beatable.</summary>
	const float IdleChance = 0.35f;

	const float BlockChance = 0.15f;

	/// <summary>In reach, how often it actually swings rather than hesitating.</summary>
	const float AttackChance = 0.6f;

	/// <summary>A long way off, how often it reaches for a side special to close the gap.</summary>
	const float SideSpecialChance = 0.12f;

	/// <summary>
	/// Within this much of its preferred gap it walks rather than runs, so it arrives able to throw
	/// a tilt - attacking out of a run is always the dash attack.
	/// </summary>
	const float WalkInRange = 220.0f;

	public CpuInputSource(MatchManager match, int seed)
	{
		this.match = match;
		rng = new System.Random(seed * 7919 + 17);
	}

	/// <summary>The fighter this CPU drives. Set once the fighter exists.</summary>
	public void Drive(Fighter fighter) => self = fighter;

	float Randf() => (float)rng.NextDouble();
	int RandiRange(int from, int to) => rng.Next(from, to + 1);
	float RandfRange(float from, float to) => from + (to - from) * (float)rng.NextDouble();

	public InputState Poll()
	{
		frame++;
		if (self == null || match == null || !self.IsInPlay) return InputState.None;

		if (frame >= nextDecision)
		{
			Decide();
			nextDecision = frame + ReactionFrames + RandiRange(-ReactionJitter, ReactionJitter);
		}
		if (jumpCooldown > 0) jumpCooldown--;

		var state = new InputState
		{
			// The brake is a reflex over the top of the decision, not a new one: once the slide is
			// under control, the stick goes back to what was decided.
			Move = Braking() ? new Vector2(self.Velocity.X > 0.0f ? -1.0f : 1.0f, heldStick.Y) : heldStick,
			JumpPressed = pressJump,
			AttackPressed = pressAttack,
			SpecialPressed = pressSpecial,
			SpecialHeld = pressSpecial,
			BlockHeld = frame < blockUntil,
			// A smash is asked for the way a d-pad asks for one: the direction counts as a flick
			// whatever the stick was doing before, so a smash never comes out as a tilt by accident.
			MoveFromDpad = pressSmash,
		};

		pressJump = pressAttack = pressSpecial = pressSmash = false;
		return state;
	}

	void Decide()
	{
		heldStick = Vector2.Zero;
		meansToDrop = false;

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

		if (Randf() < IdleChance) return;

		Vector2 toTarget = target.GlobalPosition - self.GlobalPosition;
		float dx = toTarget.X;
		float dy = toTarget.Y;
		float distance = Mathf.Abs(dx);
		int toward = dx >= 0.0f ? 1 : -1;

		// Something is coming: sometimes block it.
		if (self.IsOnFloor() && target.CurrentMove != null && distance < 170.0f && Randf() < BlockChance)
		{
			blockUntil = frame + RandiRange(12, 22);
			return;
		}

		// Someone sitting in block right in front: grab them - that is what a grab is for. It has
		// to face them, and the grab reaches just past the front of its body.
		if (self.IsOnFloor() && target.IsBlocking && self.Facing == toward && Mathf.Abs(dy) < 80.0f
			&& distance < self.BodyRect().Size.X * 0.5f + target.BodyRect().Size.X * 0.5f + 50.0f
			&& Randf() < AttackChance)
		{
			heldStick = Vector2.Zero;
			blockUntil = frame + 2;
			pressAttack = true;
			return;
		}

		// Something would land: swing (or, now and then, hesitate).
		FindOptions(target, toward);
		if (options.Count > 0)
		{
			if (Randf() < AttackChance) Attack();
			return;
		}

		// Above us and nearby: go up after them - and on up with the air jump at the top of the
		// first one, or a short jumper like Circy just hops about underneath a platform for ever.
		// Only after someone standing up there: chasing a launched fighter with the air jump would
		// spend it over the void.
		bool topOfJump = !self.IsOnFloor() && self.HasAirJump && self.Velocity.Y > -250.0f && target.IsOnFloor();
		if ((self.IsOnFloor() || topOfJump) && dy < -140.0f && distance < 260.0f && jumpCooldown == 0)
		{
			pressJump = true;
			jumpCooldown = 30;
			heldStick = new Vector2(toward * 0.5f, 0.0f);
			// An air dash goes where the stick points: up at them, not along the floor.
			if (topOfJump && self.Data.AirDashSpeed > 0.0f) heldStick = new Vector2(toward * 0.4f, -1.0f).Normalized();
			return;
		}

		// A long way off: sometimes use a side special - a projectile or a dash closes the gap. Not
		// with a wall in the way: getting over it comes first, or a big fighter never does.
		if (distance > 380.0f && !WallAhead(toward) && !NearBlastZone(toward) && Randf() < SideSpecialChance)
		{
			heldStick = new Vector2(toward, 0.0f);
			pressSpecial = true;
			return;
		}

		// Nothing lands from here: get to its own range - in, out, or turned round.
		Space(target, toward, distance);
	}

	/// <summary>
	/// Every attack that would land on the target if thrown now: the move's hitbox, where it will
	/// be, against their body box - the same test a real hit uses (Fighter.QueryHits). A move that
	/// throws something instead - a missile, a summoned blade, a MiniBot - counts if what it throws
	/// would pass through them (see <see cref="ShotLands"/>). Where they will have moved to by the
	/// time it comes out is ignored; this is a reaction, not a prediction.
	/// </summary>
	void FindOptions(Fighter target, int toward)
	{
		options.Clear();
		Rect2 body = target.BodyRect();
		int facing = self.Facing;

		if (!self.IsOnFloor())
		{
			Consider(MoveSlot.NeutralAir, Vector2.Zero, false, 1.0f, facing, body);
			Consider(MoveSlot.ForwardAir, new Vector2(facing, 0.0f), false, 1.0f, facing, body);
			Consider(MoveSlot.BackAir, new Vector2(-facing, 0.0f), false, 1.0f, facing, body);
			Consider(MoveSlot.UpAir, new Vector2(0.0f, -1.0f), false, 1.0f, facing, body);
			Consider(MoveSlot.DownAir, new Vector2(0.0f, 1.0f), false, 1.0f, facing, body);
			return;
		}

		if (self.AttackWouldDash)
		{
			// Running, any press is the dash attack - and it slides on into them while it winds up.
			// Not toward the side of the screen, where the slide carries on out of the match.
			MoveData dash = self.Data.Move(MoveSlot.DashAttack);
			float slide = dash != null ? self.Velocity.X * dash.StartupFrames / 60.0f * 0.7f : 0.0f;
			if (NearBlastZone(facing, Mathf.Abs(slide) + 160.0f)) return;
			Consider(MoveSlot.DashAttack, Vector2.Zero, false, 1.0f, facing, body, slide);
			return;
		}

		// At 0.7 the stick is a tilt; the smashes come with the d-pad flag, as a flick would.
		Consider(MoveSlot.Jab, Vector2.Zero, false, 1.0f, facing, body);
		Consider(MoveSlot.ForwardTilt, new Vector2(facing * 0.7f, 0.0f), false, 1.2f, facing, body);
		Consider(MoveSlot.UpTilt, new Vector2(0.0f, -0.7f), false, 1.0f, facing, body);
		Consider(MoveSlot.DownTilt, new Vector2(0.0f, 0.7f), false, 1.0f, facing, body);
		// Smashes are slow: thrown now and then, and more once a hit could finish them.
		float smash = target.Percent > 90.0f ? 1.2f : 0.4f;
		// A forward smash turns him to face the way it is flicked, so it can go either way.
		Consider(MoveSlot.ForwardSmash, new Vector2(toward, 0.0f), true, smash, toward, body);
		Consider(MoveSlot.UpSmash, new Vector2(0.0f, -1.0f), true, smash, facing, body);
		Consider(MoveSlot.DownSmash, new Vector2(0.0f, 1.0f), true, smash, facing, body);
	}

	void Consider(MoveSlot slot, Vector2 stick, bool smash, float weight, int facing, Rect2 body, float slide = 0.0f)
	{
		MoveData move = self.Data.Move(slot);
		if (move == null) return;
		if (move.HitboxRadius > 0.0f)
		{
			Vector2 centre = self.GlobalPosition + new Vector2(move.HitboxOffset.X * facing + slide, move.HitboxOffset.Y);
			Vector2 nearest = new Vector2(
				Mathf.Clamp(centre.X, body.Position.X, body.End.X),
				Mathf.Clamp(centre.Y, body.Position.Y, body.End.Y));
			if (nearest.DistanceSquaredTo(centre) > move.HitboxRadius * move.HitboxRadius) return;
		}
		else
		{
			if (!ShotLands(move, facing, body)) return;
			// Thrown, so less often than a swing that is sure to land.
			weight *= 0.5f;
		}
		options.Add(new Option(stick, smash, weight));
	}

	/// <summary>
	/// Whether what a move throws would pass through the target: its flight followed a couple of
	/// frames at a time, from where the move puts it, at every angle it fires at, both ways if it
	/// is mirrored, for as long as it lives. Something risen out of the floor is a column where
	/// it comes up; a walker walks the floor ahead. The same numbers the hazards fly by
	/// (Fighter.SpawnSpecialHazard, Hazard).
	/// </summary>
	bool ShotLands(MoveData move, int facing, Rect2 body)
	{
		float feet = self.BodyRect().End.Y;
		float ahead = (body.GetCenter().X - self.GlobalPosition.X) * facing;

		if (move.Special == SpecialKind.Walker)
		{
			float walks = move.SpecialSpeed * move.SpecialLifetime / 60.0f;
			return ahead > 0.0f && ahead < walks && Mathf.Abs(body.End.Y - feet) < 30.0f;
		}
		if (move.Special != SpecialKind.Projectile) return false;

		float[] angles = move.ShotAngles.Length > 0 ? move.ShotAngles : new[] { move.FromGround ? 90.0f : 0.0f };
		for (int side = 1; side >= -1; side -= 2)
		{
			if (side < 0 && !move.Mirrored) break;
			int dir = facing * side;
			var start = self.GlobalPosition + new Vector2(move.HitboxOffset.X * dir, move.HitboxOffset.Y);

			if (move.FromGround)
			{
				// Up out of the floor where it is aimed, as high as its drawing stands.
				float half = body.Size.X * 0.5f + move.FxRadius;
				if (Mathf.Abs(body.GetCenter().X - start.X) <= half && body.End.Y >= feet - 30.0f
					&& body.Position.Y <= feet + 30.0f) return true;
				continue;
			}

			foreach (float degrees in angles)
			{
				float a = Mathf.DegToRad(degrees);
				Vector2 velocity = move.ShotAngles.Length > 0
					? new Vector2(Mathf.Cos(a) * dir, -Mathf.Sin(a)) * move.SpecialSpeed
					: new Vector2(dir * move.SpecialSpeed, move.LaunchLift > 0.0f ? -move.LaunchLift
						: move.SpecialGravity > 0.0f ? -120.0f : 0.0f);
				Vector2 p = start;
				float travelled = 0.0f;
				const float Step = 2.0f / 60.0f;
				for (int f = 0; f < move.SpecialLifetime; f += 2)
				{
					Vector2 next = p + velocity * Step;
					travelled += p.DistanceTo(next);
					p = next;
					velocity.Y += move.SpecialGravity * Step;
					// A beam stops growing at its reach.
					if (move.Beam && travelled > move.Reach) break;
					Vector2 nearest = new Vector2(
						Mathf.Clamp(p.X, body.Position.X, body.End.X),
						Mathf.Clamp(p.Y, body.Position.Y, body.End.Y));
					if (nearest.DistanceSquaredTo(p) <= move.FxRadius * move.FxRadius) return true;
				}
			}
		}
		return false;
	}

	void Attack()
	{
		// A down special now and then, close in - Circy's bomb, Flambe's fire cloud, Lug's girder.
		if (self.IsOnFloor() && Randf() < 0.08f)
		{
			heldStick = new Vector2(0.0f, 1.0f);
			pressSpecial = true;
			return;
		}

		float total = 0.0f;
		foreach (Option option in options) total += option.Weight;
		float roll = Randf() * total;
		Option chosen = options[options.Count - 1];
		foreach (Option option in options)
		{
			roll -= option.Weight;
			if (roll > 0.0f) continue;
			chosen = option;
			break;
		}

		heldStick = chosen.Stick;
		pressSmash = chosen.Smash;
		pressAttack = true;
	}

	/// <summary>
	/// The gap it likes: where its forward tilt's hitbox is, or its jab's if that reaches further -
	/// measured centre to centre, so that is where the target's middle sits when the hit comes out.
	/// </summary>
	float PreferredGap()
	{
		float gap = 0.0f;
		foreach (MoveSlot slot in GapMoves)
		{
			MoveData move = self.Data.Move(slot);
			if (move != null && move.HitboxRadius > 0.0f) gap = Mathf.Max(gap, move.HitboxOffset.X);
		}
		return gap;
	}

	static readonly MoveSlot[] GapMoves = { MoveSlot.Jab, MoveSlot.ForwardTilt };

	/// <summary>
	/// Nothing would land, so move to where something will. Well below it, it goes down to them.
	/// Too close for its own reach - right on top of them with a long weapon - it backs off, unless
	/// the edge is behind it. Too far, it comes in, walking for the last stretch so it arrives able
	/// to tilt, jumping a wall or a gap on the way, and stopping short of a side blast zone. At
	/// about the right gap but facing away, it turns round.
	/// </summary>
	void Space(Fighter target, int toward, float distance)
	{
		bool grounded = self.IsOnFloor();
		float gap = PreferredGap();
		float halfWidth = target.BodyRect().Size.X * 0.5f;
		float below = target.GlobalPosition.Y - self.GlobalPosition.Y;

		// Well below, on another level: go down to them - through a soft platform, or off the side
		// toward them. Spacing left and right from up here only paces about over their head.
		bool beneath = below > 150.0f && target.IsOnFloor();
		meansToDrop = beneath;
		if (grounded && beneath && distance < 400.0f)
		{
			if (self.OnSoftPlatform)
			{
				heldStick = new Vector2(0.0f, 1.0f);
				pressJump = true;
			}
			else
			{
				heldStick = new Vector2(toward * 0.6f, 0.0f);
			}
			return;
		}

		// Backing off is a slow walk, and only with plenty of floor behind: it is held until the
		// next decision, and a fast fighter walking backwards for that long would go off the edge.
		// Only from someone on about our level - one below or above is not in the way.
		if (grounded && Mathf.Abs(below) < 100.0f && distance < gap - halfWidth && !NearEdge(-toward, BackOffClearance)
			&& !NearBlastZone(-toward))
		{
			heldStick = new Vector2(-toward * 0.35f, 0.0f);
			return;
		}

		if (distance > gap + halfWidth)
		{
			// Toward the side of the screen only slowly, and not right up to it: on a walk-off stage
			// the floor runs on past the blast zone, and a fighter chasing someone along it at a run
			// runs out of the match.
			if (NearBlastZone(toward, 120.0f)) return;
			if (NearBlastZone(toward))
			{
				heldStick = new Vector2(toward * 0.35f, 0.0f);
				return;
			}

			// A wall in the way - the side of a taller building: jump it, and use the air jump at
			// the top of that if it is still not over. Walking into it just stands there.
			bool canJump = grounded || (self.HasAirJump && self.Velocity.Y > -250.0f);
			if (WallAhead(toward) && canJump && jumpCooldown == 0)
			{
				pressJump = true;
				jumpCooldown = 20;
				heldStick = new Vector2(toward, 0.0f);
				return;
			}

			// At the edge of a drop with them across it: jump over to them if they are standing on
			// something there, and otherwise wait. Walking off after them is how a CPU falls off
			// the stage by itself - the first version did it chasing someone onto a far platform.
			// (With them standing down below, walking off is the way down.)
			if (grounded && NearEdge(toward, 90.0f + StopDistance()) && !beneath)
			{
				if (target.IsOnFloor() && jumpCooldown == 0)
				{
					pressJump = true;
					jumpCooldown = 30;
					heldStick = new Vector2(toward, 0.0f);
				}
				return;
			}
			bool closing = distance - gap < WalkInRange;
			heldStick = new Vector2(toward * (closing ? 0.45f : RandfRange(0.65f, 1.0f)), 0.0f);
			return;
		}

		// In range but turned the wrong way: a nudge toward them turns him without closing in.
		if (self.Facing != toward) heldStick = new Vector2(toward * 0.15f, 0.0f);
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

		// Underneath the stage - below its ledge and in on the stage side of it - the way home is
		// out past the ledge first, then up. Heading straight for the ledge from there runs into
		// the stage's underside, and an aimed recovery flips back and forth under the corner.
		int outward = target.X >= StageCentreX() ? 1 : -1;
		float below = self.GlobalPosition.Y - target.Y;
		float outside = (self.GlobalPosition.X - target.X) * outward;
		// Clear of it means the whole body clear, with room to spare - fired from right at the
		// corner, a recovery catches on the stage's underside and loses its height.
		bool underneath = below > 40.0f && outside < self.BodyRect().Size.X * 0.5f + 40.0f;
		if (underneath) toward = outward;

		heldStick = new Vector2(toward, 0.0f);

		if (falling && self.HasAirJump && jumpCooldown == 0)
		{
			pressJump = true;
			jumpCooldown = 20;
			// An air dash goes where the stick points, so aim it: out from under the stage; straight
			// up while the ledge is further above than across, since a diagonal from there would
			// carry him in under the floor; otherwise up and in. Sideways alone gains no height.
			if (self.Data.AirDashSpeed > 0.0f)
			{
				heldStick = underneath ? new Vector2(outward, -0.4f).Normalized()
					: below > outside + 40.0f ? new Vector2(0.0f, -1.0f)
					: new Vector2(toward, -1.0f).Normalized();
			}
			return;
		}

		// The up special waits until he is out from under the stage: fired from below the floor it
		// only hits the underside, and then there is nothing left to come home with.
		if (falling && low && !self.HasAirJump && !usedRecovery && !underneath)
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

	/// <summary>
	/// Within reach of a side blast zone that way - Open Plains' floor runs on past both of them.
	/// Far enough out to stop a run or a dash attack in time.
	/// </summary>
	bool NearBlastZone(int direction, float margin = BlastZoneMargin)
	{
		float x = self.GlobalPosition.X + direction * margin;
		return x < match.StageBounds.Position.X || x > match.StageBounds.End.X;
	}

	const float BlastZoneMargin = 320.0f;

	/// <summary>
	/// A reflex rather than a decision, checked every frame: sliding toward a drop, or out of the
	/// match, faster than friction will stop in time - Triguy's slippery feet - it pushes back
	/// against the slide at once, as a person would on seeing the edge. Left to the next decision,
	/// a fast skid covers a whole platform first. Not while it means to jump: a jump across a gap
	/// is the stick held toward it.
	/// </summary>
	bool Braking()
	{
		if (pressJump || Mathf.Abs(self.Velocity.X) < 200.0f) return false;
		int sliding = self.Velocity.X > 0.0f ? 1 : -1;
		if (heldStick.X * sliding < 0.0f) return false;
		float stop = StopDistance();
		// Only when the slide would really carry it over: braking well short of every edge keeps it
		// away from anyone standing near one. An edge it means to go over is no danger, and in the
		// air only the side of the screen matters.
		if (!self.IsOnFloor()) return NearBlastZone(sliding, stop + 120.0f);
		return (!meansToDrop && NearEdge(sliding, stop + 30.0f)) || NearBlastZone(sliding, stop + 80.0f);
	}

	/// <summary>
	/// How far it would slide before stopping if it pushed back now: friction and the stick
	/// together on the ground, the stick alone against the air's drift.
	/// </summary>
	float StopDistance()
	{
		float braking = self.IsOnFloor()
			? self.Data.GroundFriction + self.Data.GroundAcceleration
			: self.Data.AirAcceleration;
		return self.Velocity.X * self.Velocity.X / (2.0f * Mathf.Max(1.0f, braking));
	}

	/// <summary>Solid ground straight ahead at body height - a wall to get over, not a floor to walk on.</summary>
	bool WallAhead(int direction)
	{
		PhysicsDirectSpaceState2D space = self.GetWorld2D().DirectSpaceState;
		Vector2 from = self.GlobalPosition;
		Vector2 to = from + new Vector2(direction * (self.BodyRect().Size.X * 0.5f + 40.0f), 0.0f);
		return space.IntersectRay(PhysicsRayQueryParameters2D.Create(from, to, 0b01)).Count > 0;
	}

	/// <summary>Floor it wants behind it before it will back away - about a whole decision's walk.</summary>
	const float BackOffClearance = 220.0f;

	bool NearEdge(int direction, float reach = 90.0f)
	{
		PhysicsDirectSpaceState2D space = self.GetWorld2D().DirectSpaceState;
		Vector2 ahead = self.GlobalPosition + new Vector2(direction * reach, 0.0f);
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
