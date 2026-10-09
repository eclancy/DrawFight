using System.Collections.Generic;
using Godot;

/// <summary>
/// Anything a special leaves behind that can hit someone: a fireball, a thrown hammer, a
/// falling anvil, a patch of fire on the ground, a laser beam.
///
/// One class covers all of them because the difference between a projectile and a trap is its
/// starting velocity and how long it lives, not its behaviour. Keeping it that way is what
/// stops the special archetypes from each growing their own node type.
/// </summary>
public partial class Hazard : Node2D
{
	Fighter owner;
	MoveData move;
	MatchManager match;

	Vector2 velocity;
	float gravity;
	int lifeFrames;
	int ageFrames;
	bool expiring;

	HashSet<Fighter> alreadyHit = new HashSet<Fighter>();

	/// <summary>
	/// Shares the move's own hit list, so a hazard and the move that made it count as one hit:
	/// someone caught by Lug's slam is not caught again by its quake.
	/// </summary>
	public void ShareHits(HashSet<Fighter> moveHits) => alreadyHit = moveHits;

	/// <summary>Multiplies the move's damage for this hazard - a charged smash's shockwave.</summary>
	public float DamageScale = 1.0f;

	// An earthquake sits where it started and spreads: how far it has reached each way, and
	// whether each side has run out of ground. Its position is the floor at the impact point.
	float quakeLeft;
	float quakeRight;
	bool quakeLeftDone;
	bool quakeRightDone;

	// A beam does not fly. It stays attached to the hands that fired it and grows outward, so
	// it follows its owner through the air and the far end is the part that hits first.
	float beamLength;
	int beamDir = 1;
	bool beamStopped;

	/// <summary>Frames a beam stays up after it hits something, so the hit is seen.</summary>
	const int BeamHoldFrames = 8;

	/// <summary>
	/// How much of a laser drawing is the burst at its end; the rest is the plain beam, which is
	/// the only part stretched to reach further.
	/// </summary>
	const float BeamBurstFraction = 0.25f;

	// Something summoned up out of the floor: the floor's height, which is where its drawing is
	// cut off, and whether it is one of those at all.
	float riseGround;
	bool rising;

	// Something that topples (MoveData.Topple): standing on the floor and falling over, how far
	// it has turned and how fast, and which way - always away from whoever summoned it.
	bool toppling;
	float toppleAngle;
	float toppleSpin;
	int toppleDir = 1;

	/// <summary>How hard a toppling drawing falls over, in radians a second squared.</summary>
	const float ToppleAcceleration = 26.0f;
	/// <summary>The little push it starts falling with, in radians a second.</summary>
	const float ToppleStart = 1.2f;

	/// <summary>
	/// Starts this hazard buried in the floor at <paramref name="groundY"/>, to rise out of it.
	/// Only the part above the floor is drawn or can hit.
	/// </summary>
	/// <summary>Draws the art mirrored - the far one of a mirrored pair, so both face outward.</summary>
	public bool FlipArt;

	public void RiseFrom(float groundY)
	{
		rising = true;
		riseGround = groundY;
		GlobalPosition = new Vector2(GlobalPosition.X, groundY + ArtHalfHeight());
	}

	/// <summary>Half the height the effect art is drawn at, or the hitbox radius if there is no art.</summary>
	float ArtHalfHeight()
	{
		if (move.FxTexture == null) return move.FxRadius;
		Vector2 size = move.FxTexture.GetSize();
		float fit = move.FxArtSize > 0.0f ? move.FxArtSize : move.FxRadius * 2.3f;
		return fit * size.Y / Mathf.Max(size.X, size.Y) * 0.5f;
	}

	/// <summary>
	/// A walker - the MiniBot - is a small copy of its owner's own puppet, marching along the
	/// floor, and whether it has floor under it this frame.
	/// </summary>
	FighterRig miniRig;
	bool walkerGrounded;

	// A puddle lies on the floor - its position is the floor's surface - spreading out each way
	// until it reaches its full size or the floor ends; then it sends spikes up out of the water.
	float puddleLeft;
	float puddleRight;
	bool puddleLeftDone;
	bool puddleRightDone;
	bool puddleLanded;
	int puddleFrames;
	int spikesSent;

	/// <summary>How fast a puddle spreads along the floor each way, in pixels a second.</summary>
	const float PuddleSpread = 700.0f;

	/// <summary>Hazards do not hit the fighter who made them for this many frames.</summary>
	const int OwnerGraceFrames = 8;

	public void Launch(Fighter owner, MoveData move, MatchManager match,
		Vector2 position, Vector2 velocity, float gravity, int lifeFrames)
	{
		this.owner = owner;
		this.move = move;
		this.match = match;
		this.velocity = velocity;
		this.gravity = gravity;
		this.lifeFrames = lifeFrames;

		GlobalPosition = position;
		if (move.Beam) beamDir = velocity.X < 0.0f ? -1 : 1;
		if (move.Special == SpecialKind.Walker) BuildMiniRig();
	}

	/// <summary>
	/// The walker is drawn as its owner, small: a second copy of the same rig, played at a
	/// fraction of the size. Nothing new is drawn - it is his own drawing, only smaller.
	/// </summary>
	void BuildMiniRig()
	{
		string path = owner?.Data?.RigPath;
		if (string.IsNullOrEmpty(path)) return;
		miniRig = new FighterRig();
		AddChild(miniRig);
		if (!miniRig.Load(path))
		{
			miniRig.QueueFree();
			miniRig = null;
			return;
		}
		miniRig.Normalise(move.FxArtSize, move.FxArtSize * 0.5f, 1.0f);
		miniRig.SetFacing(velocity.X < 0.0f ? -1 : 1);
		miniRig.SetPlanted(true);
	}

	/// <summary>
	/// Walks along whatever it is standing on and falls if there is nothing. At an edge or a wall
	/// it stops dead rather than walking off - and waits there for someone to come to it.
	/// </summary>
	void TickWalker(float dt)
	{
		float half = move.FxArtSize * 0.5f;
		velocity = new Vector2(velocity.X, velocity.Y + gravity * dt);
		Vector2 next = GlobalPosition + velocity * dt;

		walkerGrounded = false;
		if (velocity.Y >= 0.0f)
		{
			var ray = PhysicsRayQueryParameters2D.Create(GlobalPosition + new Vector2(0.0f, half - 6.0f),
				next + new Vector2(0.0f, half + 2.0f), 0b11);
			Godot.Collections.Dictionary hit = GetWorld2D().DirectSpaceState.IntersectRay(ray);
			if (hit.Count > 0)
			{
				next.Y = ((Vector2)hit["position"]).Y - half;
				velocity = new Vector2(velocity.X, 0.0f);
				walkerGrounded = true;
			}
		}

		if (walkerGrounded && velocity.X != 0.0f)
		{
			float dir = Mathf.Sign(velocity.X);
			bool floorAhead = PointSolid(new Vector2(next.X + dir * half * 0.5f, next.Y + half + 6.0f), 0b11);
			bool wallAhead = PointSolid(new Vector2(next.X + dir * half * 0.6f, next.Y), 0b01);
			if (!floorAhead || wallAhead)
			{
				velocity = new Vector2(0.0f, velocity.Y);
				next.X = GlobalPosition.X;
			}
		}
		GlobalPosition = next;

		if (miniRig == null) return;
		bool walking = walkerGrounded && velocity.X != 0.0f;
		miniRig.Play(walking ? FighterAnimations.Run : walkerGrounded ? FighterAnimations.Idle : FighterAnimations.Fall);
		miniRig.Advance();
		// Nearly out of time: flashing hot, faster at the end.
		int left = lifeFrames - ageFrames;
		bool flash = left < 50 && (ageFrames / (left < 20 ? 2 : 4)) % 2 == 0;
		miniRig.Modulate = flash ? new Color(1.7f, 0.8f, 0.6f) : Colors.White;
	}

	/// <summary>
	/// Falls to the floor if it was made in the air, then spreads along it both ways, each side
	/// stopping at its full size or where the floor ends - it never hangs out over a drop.
	/// </summary>
	void TickPuddle(float dt)
	{
		if (!puddleLanded)
		{
			velocity = new Vector2(0.0f, velocity.Y + gravity * dt);
			Vector2 next = GlobalPosition + velocity * dt;
			var ray = PhysicsRayQueryParameters2D.Create(GlobalPosition + new Vector2(0.0f, -4.0f),
				next + new Vector2(0.0f, 2.0f), 0b11);
			Godot.Collections.Dictionary hit = GetWorld2D().DirectSpaceState.IntersectRay(ray);
			if (hit.Count == 0)
			{
				GlobalPosition = next;
				return;
			}
			GlobalPosition = new Vector2(GlobalPosition.X, ((Vector2)hit["position"]).Y);
			velocity = Vector2.Zero;
			puddleLanded = true;
		}

		puddleFrames++;
		float grow = PuddleSpread * dt;
		if (!puddleLeftDone)
		{
			puddleLeft = Mathf.Min(move.FxRadius, puddleLeft + grow);
			puddleLeftDone = puddleLeft >= move.FxRadius || !PointSolid(new Vector2(GlobalPosition.X - puddleLeft, GlobalPosition.Y + 6.0f), 0b11);
		}
		if (!puddleRightDone)
		{
			puddleRight = Mathf.Min(move.FxRadius, puddleRight + grow);
			puddleRightDone = puddleRight >= move.FxRadius || !PointSolid(new Vector2(GlobalPosition.X + puddleRight, GlobalPosition.Y + 6.0f), 0b11);
		}
	}

	/// <summary>
	/// Everyone but its owner standing in the puddle slips; and every RainInterval frames a spike
	/// comes up out of it - every other one right under someone standing in it, the rest anywhere
	/// along it, the spot hashed so it is never random.
	/// </summary>
	void PuddleHits()
	{
		if (!puddleLanded) return;
		Fighter wading = null;
		foreach (Fighter other in match.Fighters)
		{
			if (other == owner || !other.IsInPlay || !other.IsOnFloor()) continue;
			Rect2 body = other.BodyRect();
			if (Mathf.Abs(body.End.Y - GlobalPosition.Y) > 14.0f) continue;
			if (body.End.X < GlobalPosition.X - puddleLeft || body.Position.X > GlobalPosition.X + puddleRight) continue;
			other.Slip();
			wading ??= other;
		}

		int interval = Mathf.Max(1, move.RainInterval);
		if (move.RainDrop == null || puddleFrames % interval != interval / 2 || ageFrames > lifeFrames - 20) return;
		float x = spikesSent % 2 == 1 && wading != null
			? wading.GlobalPosition.X
			: GlobalPosition.X + Mathf.Lerp(-puddleLeft, puddleRight, (CrayonBrush.Noise(spikesSent * 7 + 5, 61) + 1.0f) * 0.5f);
		x = Mathf.Clamp(x, GlobalPosition.X - puddleLeft, GlobalPosition.X + puddleRight);
		spikesSent++;
		Hazard spike = match.SpawnHazard(owner, move.RainDrop, new Vector2(x, GlobalPosition.Y),
			new Vector2(0.0f, -move.RainDrop.SpecialSpeed));
		spike.RiseFrom(GlobalPosition.Y);
	}

	/// <summary>
	/// The puddle: a flat pool of the blue Elim drew his tears in, rippling a little and drying
	/// away at the end. Edged in a deep blue rather than black - only fighters own black (see
	/// .ai/art-direction.md). The ripple is hashed, never random.
	/// </summary>
	void DrawPuddle()
	{
		if (!puddleLanded || puddleLeft + puddleRight < 4.0f) return;
		// Like every trap, a puddle never fades: it is there, then gone (Eric, 2026-10-04).
		const float fade = 1.0f;
		const int Steps = 28;
		const float Depth = 11.0f;
		var outline = new Vector2[Steps * 2 + 1];
		for (int i = 0; i <= Steps; i++)
		{
			float t = i / (float)Steps;
			float x = Mathf.Lerp(-puddleLeft, puddleRight, t);
			float ripple = 1.0f + 0.25f * CrayonBrush.Noise(ageFrames / 5 + i, 71);
			outline[i] = new Vector2(x, -Depth * Mathf.Sin(Mathf.Pi * t) * ripple);
			outline[Steps * 2 - i] = new Vector2(x, Depth * 0.3f * Mathf.Sin(Mathf.Pi * t));
		}
		var fill = new Vector2[Steps * 2];
		System.Array.Copy(outline, fill, Steps * 2);
		DrawColoredPolygon(fill, new Color(0.10f, 0.40f, 1.0f, 0.9f * fade));
		outline[Steps * 2] = outline[0];
		DrawPolyline(outline, new Color(0.05f, 0.20f, 0.55f, fade), 3.0f);
	}

	bool PointSolid(Vector2 at, uint mask)
	{
		var query = new PhysicsPointQueryParameters2D { Position = at, CollisionMask = mask };
		return GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count > 0;
	}

	/// <summary>The walker going off: its Burst left where it stood, and the walker gone.</summary>
	void Detonate()
	{
		if (expiring) return;
		if (move.Burst != null && match != null)
		{
			match.SpawnHazard(owner, move.Burst, GlobalPosition, Vector2.Zero);
			match.Shake(18.0f);
		}
		SfxPlayer.At("minibot_pop", GlobalPosition, 0.04f);
		Expire();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		ageFrames++;

		if (move.Special == SpecialKind.Shockwave)
		{
			float grow = Mathf.Abs(velocity.X) * dt;
			if (!quakeLeftDone)
			{
				quakeLeft += grow;
				quakeLeftDone = !GroundAt(GlobalPosition.X - quakeLeft);
			}
			if (!quakeRightDone)
			{
				quakeRight += grow;
				quakeRightDone = !GroundAt(GlobalPosition.X + quakeRight);
			}
		}
		else if (move.Beam)
		{
			if (owner == null || !IsInstanceValid(owner) || !owner.IsInPlay)
			{
				Expire();
				return;
			}
			GlobalPosition = owner.BeamOrigin();
			if (!beamStopped) beamLength = Mathf.Min(move.Reach, beamLength + Mathf.Abs(velocity.X) * dt);
		}
		else if (move.Special == SpecialKind.Walker)
		{
			TickWalker(dt);
		}
		else if (move.Special == SpecialKind.Puddle)
		{
			TickPuddle(dt);
		}
		else if (rising && move.Topple)
		{
			if (TickTopple(dt)) return;
		}
		else
		{
			velocity = new Vector2(velocity.X, velocity.Y + gravity * dt);
			GlobalPosition += velocity * dt;

			// A kicked trap skids to a stop along the floor - and drops off an edge it slides over.
			if (move.SlideFriction > 0.0f && gravity == 0.0f)
			{
				velocity = new Vector2(Mathf.MoveToward(velocity.X, 0.0f, move.SlideFriction * dt), 0.0f);
				if (!LandsOnGround()) gravity = 3000.0f;
			}
		}

		// Risen and fallen back into the floor: gone.
		if (rising && velocity.Y > 0.0f && GlobalPosition.Y > riseGround + ArtHalfHeight())
		{
			Expire();
			return;
		}

		QueryHits();
		QueueRedraw();

		// Something dropped - a steel beam, an anvil - stops on solid ground instead of falling
		// through the stage. Soft platforms do not stop it; it lands on what you cannot drop through.
		if (move.Special == SpecialKind.Drop && velocity.Y > 0.0f && HitsSolidGround())
		{
			Expire();
			return;
		}

		// A thrown drawing that arcs - a lobbed greatsword - is spent when it comes down into the
		// stage, rather than sinking through it out of sight.
		if (move.Special == SpecialKind.Projectile && move.FxTexture != null && gravity > 0.0f
			&& velocity.Y > 0.0f && HitsSolidGround())
		{
			Expire();
			return;
		}

		// A trap with weight - a planted blade put down in the air - falls until it lands on
		// anything that can be stood on, and stays there.
		if (move.Special == SpecialKind.Trap && gravity > 0.0f && velocity.Y > 0.0f && LandsOnGround())
		{
			velocity = Vector2.Zero;
			gravity = 0.0f;
		}

		// Hazards die of old age or by leaving the stage. Without the second check a fireball
		// fired off the side would live out its full lifetime somewhere nobody can see.
		bool offStage = match?.StageBounds.Grow(400.0f).HasPoint(GlobalPosition) == false;
		if (move.Special == SpecialKind.Walker && ageFrames >= lifeFrames && !offStage)
		{
			Detonate();
			return;
		}
		if (ageFrames >= lifeFrames || offStage) Expire();
	}

	/// <summary>
	/// Up out of the floor, dead stop standing on it, then over: the axe falls outward faster and
	/// faster until it lies on the floor - and there it is gone, in a smash. True once it is gone.
	/// </summary>
	bool TickTopple(float dt)
	{
		float half = ArtHalfHeight();
		if (!toppling)
		{
			GlobalPosition += new Vector2(0.0f, -Mathf.Abs(move.SpecialSpeed) * dt);
			float standing = riseGround - half;
			if (GlobalPosition.Y > standing) return false;

			GlobalPosition = new Vector2(GlobalPosition.X, standing);
			velocity = Vector2.Zero;
			toppling = true;
			toppleSpin = ToppleStart;
			float away = owner != null && IsInstanceValid(owner) ? GlobalPosition.X - owner.GlobalPosition.X : 0.0f;
			toppleDir = Mathf.Abs(away) > 1.0f ? (away > 0.0f ? 1 : -1) : (FlipArt ? -1 : 1);
			return false;
		}

		toppleSpin += ToppleAcceleration * dt;
		toppleAngle += toppleSpin * dt;
		if (toppleAngle < Mathf.Pi * 0.5f) return false;

		// Down: the head hits the floor a full length out from where it stood. A smash of dust
		// and a jolt, and it is simply gone - nothing fades.
		toppleAngle = Mathf.Pi * 0.5f;
		Vector2 landed = new Vector2(GlobalPosition.X + toppleDir * half * 1.6f, riseGround);
		match?.Dust(landed, half * 1.1f, toppleDir * 260.0f);
		match?.Shake(22.0f);
		SfxPlayer.At("special_quake", landed, 0.06f);
		QueryHits();
		Expire();
		return true;
	}

	/// <summary>
	/// The part of a drawing up out of the floor that hits: the top of it - the axe head - and,
	/// once it is toppling, wherever that head has swung to.
	/// </summary>
	Vector2 RisenHead(float radius)
	{
		float half = ArtHalfHeight();
		if (!toppling) return GlobalPosition + new Vector2(0.0f, -half + radius);
		var foot = new Vector2(GlobalPosition.X, riseGround);
		return foot + new Vector2(0.0f, -(half * 2.0f - radius)).Rotated(toppleDir * toppleAngle);
	}

	/// <summary>Whether there is floor just under this hazard's height at <paramref name="x"/>.</summary>
	bool GroundAt(float x)
	{
		var query = new PhysicsPointQueryParameters2D
		{
			Position = new Vector2(x, GlobalPosition.Y + 8.0f),
			CollisionMask = 0b11,
		};
		return GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count > 0;
	}

	bool HitsSolidGround()
	{
		var query = new PhysicsPointQueryParameters2D
		{
			Position = GlobalPosition + new Vector2(0.0f, move.FxRadius * 0.4f),
			CollisionMask = 0b01,
		};
		return GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count > 0;
	}

	/// <summary>Whether the bottom of this hazard's drawing rests on solid ground or a soft platform.</summary>
	bool LandsOnGround()
	{
		float half = move.FxArtSize > 0.0f ? move.FxArtSize * 0.5f : move.FxRadius;
		var query = new PhysicsPointQueryParameters2D
		{
			Position = GlobalPosition + new Vector2(0.0f, half + 3.0f),
			CollisionMask = 0b11,
		};
		return GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count > 0;
	}

	public bool IsExpiring => expiring;

	/// <summary>Takes this hazard away early - the oldest planted blade, when a new one goes in.</summary>
	public void Remove() => Expire();

	/// <summary>
	/// A cloud forms over its first frames, then drops its rain - scattered across its width,
	/// each drop's spot hashed from the count so the pattern never repeats exactly but is never
	/// random either.
	/// </summary>
	const int CloudFormFrames = 12;
	int dropsMade;

	void Rain()
	{
		if (move.RainDrop == null || match == null || ageFrames < CloudFormFrames) return;
		if ((ageFrames - CloudFormFrames) % Mathf.Max(1, move.RainInterval) != 0) return;
		if (ageFrames > lifeFrames - 10) return;
		float x = CrayonBrush.Noise(dropsMade * 7 + 3, 91) * move.FxRadius * 0.85f;
		dropsMade++;
		var at = GlobalPosition + new Vector2(x, move.FxRadius * 0.3f);
		Hazard drop = match.SpawnHazard(owner, move.RainDrop, at, new Vector2(0.0f, move.RainDrop.SpecialSpeed));
		drop.FlipArt = false;
	}

	void QueryHits()
	{
		if (match == null || expiring) return;
		if (move.Special == SpecialKind.Cloud)
		{
			// The cloud itself is only weather; what it drops does the hitting.
			Rain();
			return;
		}

		if (move.Special == SpecialKind.Puddle)
		{
			// The puddle never hits anyone: it trips them up, and what comes out of it hits.
			PuddleHits();
			return;
		}

		if (move.Special == SpecialKind.Walker)
		{
			// The MiniBot never hits anyone: it goes off, and the blast does.
			foreach (Fighter other in match.Fighters)
			{
				if (other == owner || !other.CanBeHitByHazard) continue;
				if (Touches(other.BodyRect(), out _))
				{
					Detonate();
					return;
				}
			}
			return;
		}

		foreach (Fighter other in match.Fighters)
		{
			if (other == owner && ageFrames < OwnerGraceFrames) continue;
			if (other == owner) continue;
			if (alreadyHit.Contains(other)) continue;
			if (!other.CanBeHitByHazard) continue;

			if (!Touches(other.BodyRect(), out Vector2 nearest)) continue;

			alreadyHit.Add(other);
			other.ReceiveHit(owner, move, nearest, DamageScale);

			if (move.Beam)
			{
				// A beam stops where it hits and hangs there a moment rather than vanishing.
				beamStopped = true;
				lifeFrames = Mathf.Min(lifeFrames, ageFrames + BeamHoldFrames);
				return;
			}

			// A travelling hazard is spent on contact; a lingering one keeps burning, which is
			// what makes a trap a zoning tool rather than a slow projectile - unless it is the
			// kind that is used up by the hit it lands.
			if (!Lingers || move.SpentOnHit) Expire();
			return;
		}
	}

	/// <summary>
	/// Whether this hazard reaches a body. A ball tests its centre; a beam tests points all along
	/// its length, a radius apart, so it hits anything the drawn beam passes through.
	/// </summary>
	bool Touches(Rect2 body, out Vector2 nearest)
	{
		if (move.Special == SpecialKind.Shockwave)
		{
			// A flat band along the floor: anyone standing on the shaking stretch is caught.
			var band = new Rect2(GlobalPosition.X - quakeLeft, GlobalPosition.Y - move.FxRadius * 1.6f,
				quakeLeft + quakeRight, move.FxRadius * 1.8f);
			nearest = new Vector2(Mathf.Clamp(body.GetCenter().X, band.Position.X, band.End.X), band.GetCenter().Y);
			return band.Intersects(body);
		}

		float radius = move.Special == SpecialKind.Vent ? VentRadius() : move.FxRadius;
		float length = move.Beam ? beamLength : 0.0f;

		if (rising)
		{
			// What hits is the top of the drawing - the axe head - and only once it is up out
			// of the floor.
			Vector2 top = RisenHead(radius);
			nearest = new Vector2(
				Mathf.Clamp(top.X, body.Position.X, body.End.X),
				Mathf.Clamp(top.Y, body.Position.Y, body.End.Y));
			return top.Y < riseGround && nearest.DistanceSquaredTo(top) <= radius * radius;
		}
		int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(4.0f, radius)));

		for (int i = 0; i <= steps; i++)
		{
			Vector2 point = GlobalPosition + new Vector2(beamDir * length * i / steps, 0.0f);
			nearest = new Vector2(
				Mathf.Clamp(point.X, body.Position.X, body.End.X),
				Mathf.Clamp(point.Y, body.Position.Y, body.End.Y));
			if (nearest.DistanceSquaredTo(point) <= radius * radius) return true;
		}

		nearest = GlobalPosition;
		return false;
	}

	/// <summary>A trap burns and an explosion blasts; both keep hitting until they fade.</summary>
	bool Lingers => move.Special == SpecialKind.Trap || move.Special == SpecialKind.Bomb
		|| move.Special == SpecialKind.Shockwave || move.Special == SpecialKind.Vent || move.FromGround;

	/// <summary>A vent bursts outward from him over its first few frames, rather than appearing whole.</summary>
	const int VentGrowFrames = 6;

	float VentRadius() => move.FxRadius * Mathf.Min(1.0f, 0.35f + 0.65f * ageFrames / (float)VentGrowFrames);

	void Expire()
	{
		if (expiring) return;
		expiring = true;
		QueueFree();
	}

	/// <summary>
	/// A kid's drawing of the effect, fitted around the hitbox. Only moved, scaled and turned -
	/// never redrawn.
	/// </summary>
	void DrawDrawnEffect(Texture2D art, float alpha)
	{
		Vector2 size = art.GetSize();
		var tint = new Color(1.0f, 1.0f, 1.0f, alpha);

		if (move.Beam)
		{
			DrawDrawnBeam(art, tint);
		}
		else
		{
			float fit = move.FxArtSize > 0.0f ? move.FxArtSize : move.FxRadius * 2.3f;
			float s = fit / Mathf.Max(size.X, size.Y);

			// Blades are drawn tip-up: one pointing along its flight is turned a quarter past the
			// flight angle; one tumbling turns a little every frame, the way it is thrown.
			float angle = 0.0f;
			if (move.FxAlongFlight && velocity.LengthSquared() > 1.0f) angle = velocity.Angle() + Mathf.Pi * 0.5f;
			else if (move.FxSpin != 0.0f) angle = Mathf.DegToRad(move.FxSpin * ageFrames) * (velocity.X < 0.0f ? -1.0f : 1.0f);

			Vector2 origin = Vector2.Zero;
			if (toppling)
			{
				// Falling over about its foot, where it stands on the floor.
				angle = toppleDir * toppleAngle;
				var foot = new Vector2(0.0f, ArtHalfHeight());
				origin = foot - foot.Rotated(angle);
			}
			DrawSetTransformMatrix(new Transform2D(angle, new Vector2(FlipArt ? -s : s, s), 0.0f, origin));
			if (rising)
			{
				// Only what has come up through the floor is drawn; the rest is still underground.
				float rows = Mathf.Clamp((riseGround - GlobalPosition.Y) / s + size.Y * 0.5f, 0.0f, size.Y);
				if (rows > 0.5f)
				{
					DrawTextureRectRegion(art, new Rect2(-size.X * 0.5f, -size.Y * 0.5f, size.X, rows),
						new Rect2(0.0f, 0.0f, size.X, rows), tint);
				}
			}
			else
			{
				DrawTexture(art, -size * 0.5f, tint);
			}
		}

		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>
	/// Elim drew the laser with its burst on the left and the beam running right. Mirrored, the
	/// burst sits at the far end, facing away from Circy, and the beam runs back to his hands.
	/// Sized by thickness; when the beam reaches further than the drawing is long, only the plain
	/// beam part is stretched along its length - the burst is never distorted.
	/// </summary>
	void DrawDrawnBeam(Texture2D art, Color tint)
	{
		if (beamLength <= 0.0f) return;

		Vector2 size = art.GetSize();
		float s = move.FxRadius * 2.0f * 2.6f / size.Y;
		float burst = size.X * BeamBurstFraction;
		float length = beamLength / s;

		// Texture x runs from the tip back toward the hands.
		DrawSetTransformMatrix(new Transform2D(0.0f, new Vector2(-beamDir * s, s), 0.0f,
			new Vector2(beamDir * beamLength, 0.0f)));

		if (length <= burst)
		{
			DrawTextureRectRegion(art, new Rect2(0.0f, -size.Y * 0.5f, length, size.Y),
				new Rect2(0.0f, 0.0f, length, size.Y), tint);
		}
		else
		{
			DrawTextureRectRegion(art, new Rect2(0.0f, -size.Y * 0.5f, burst, size.Y),
				new Rect2(0.0f, 0.0f, burst, size.Y), tint);
			DrawTextureRectRegion(art, new Rect2(burst, -size.Y * 0.5f, length - burst, size.Y),
				new Rect2(burst, 0.0f, size.X - burst, size.Y), tint);
		}
	}

	/// <summary>
	/// One flat earthquake along the floor: a band of dust hugging the ground, and a jagged crack
	/// through it that jumps about every frame. It is meant to shake, so unlike stage art the
	/// jitter is allowed to change from frame to frame - it is still hashed, never random.
	/// </summary>
	void DrawQuake(float alpha)
	{
		float left = -quakeLeft;
		float right = quakeRight;
		if (right - left < 2.0f) return;

		float fade = alpha;
		float height = move.FxRadius * 0.9f * Mathf.Lerp(1.0f, 0.4f, ageFrames / (float)Mathf.Max(1, lifeFrames));
		var dust = new Color(move.FxColor.R, move.FxColor.G, move.FxColor.B, 0.55f * fade);
		var crack = new Color(0.30f, 0.22f, 0.16f, 0.9f * fade);

		// The dust: a low, lumpy band sitting on the floor.
		const float Step = 22.0f;
		var outline = new System.Collections.Generic.List<Vector2> { new Vector2(left, 0.0f) };
		for (float x = left; x <= right; x += Step)
		{
			float bump = 0.55f + 0.45f * Mathf.Abs(CrayonBrush.Noise(ageFrames * 7 + Mathf.RoundToInt(x), 5));
			outline.Add(new Vector2(x, -height * bump));
		}
		outline.Add(new Vector2(right, 0.0f));
		DrawColoredPolygon(outline.ToArray(), dust);

		// The crack: a zigzag just above the floor line, shaking.
		var points = new System.Collections.Generic.List<Vector2>();
		int i = 0;
		for (float x = left; x <= right; x += 16.0f, i++)
		{
			float jolt = CrayonBrush.Noise(ageFrames * 13 + i, 9) * height * 0.45f;
			points.Add(new Vector2(x, -height * 0.25f + jolt));
		}
		if (points.Count > 1) DrawPolyline(points.ToArray(), crack, 4.0f);
	}

	/// <summary>
	/// A fireball: a tail of shrinking, reddening puffs streaming back the way it came, then the
	/// ball - orange round a yellow-white core - flickering as it flies.
	/// </summary>
	void DrawFlame(float radius)
	{
		Vector2 back = velocity.LengthSquared() > 1.0f ? -velocity.Normalized() : Vector2.Left;
		const int Puffs = 5;
		for (int i = Puffs; i >= 1; i--)
		{
			float k = i / (float)Puffs;
			float jitter = CrayonBrush.Noise(ageFrames + i * 7, 11) * radius * 0.25f;
			Vector2 at = back * radius * 0.55f * i + back.Orthogonal() * jitter;
			var puff = new Color(0.95f, 0.30f + 0.25f * (1.0f - k), 0.16f, 0.75f * (1.0f - k * 0.8f));
			DrawCircle(at, radius * (1.0f - k * 0.6f), puff);
		}
		float flicker = 1.0f + CrayonBrush.Noise(ageFrames, 3) * 0.08f;
		DrawCircle(Vector2.Zero, radius * 1.1f * flicker, new Color(0.96f, 0.36f, 0.16f));
		DrawCircle(Vector2.Zero, radius * 0.78f * flicker, new Color(0.99f, 0.62f, 0.20f));
		DrawCircle(Vector2.Zero, radius * 0.45f, new Color(1.0f, 0.93f, 0.62f));
	}

	/// <summary>
	/// The furnace let out all at once: a ring of flame tongues bursting outward round a hot
	/// core, or - for a cool vent - a soft cloud of steam. Hashed flicker, never random.
	/// </summary>
	void DrawVent(float alpha)
	{
		float r = VentRadius();
		float t = ageFrames / (float)Mathf.Max(1, lifeFrames);

		if (!move.FxFlame)
		{
			for (int i = 0; i < 8; i++)
			{
				Vector2 at = Vector2.Right.Rotated(Mathf.Tau * i / 8 + CrayonBrush.Noise(i, 5) * 0.3f) * r * 0.55f;
				DrawCircle(at, r * 0.45f, new Color(0.97f, 0.97f, 0.98f, 0.55f * alpha));
			}
			return;
		}

		const int Tongues = 14;
		for (int i = 0; i < Tongues; i++)
		{
			float a = Mathf.Tau * i / Tongues + CrayonBrush.Noise(ageFrames / 2, i) * 0.12f;
			Vector2 dir = Vector2.Right.Rotated(a);
			Vector2 side = dir.Orthogonal() * r * 0.2f;
			float reach = r * (0.95f + 0.25f * Mathf.Abs(CrayonBrush.Noise(ageFrames, i + 20)));
			DrawColoredPolygon(new[] { side, dir * reach, -side }, new Color(0.95f, 0.36f, 0.16f, 0.85f * alpha));
			DrawColoredPolygon(new[] { side * 0.6f, dir * reach * 0.72f, -side * 0.6f }, new Color(0.99f, 0.66f, 0.22f, 0.9f * alpha));
		}
		DrawCircle(Vector2.Zero, r * 0.62f, new Color(0.99f, 0.70f, 0.25f, 0.9f * alpha));
		DrawCircle(Vector2.Zero, r * 0.38f * (1.0f - 0.4f * t), new Color(1.0f, 0.95f, 0.75f, alpha));
	}

	/// <summary>
	/// A fire cloud: lumpy smoke, warm grey on top and glowing orange underneath where the fire is
	/// coming from. It swells into being and fades as it runs out. Pale enough to stay off the
	/// dark end of the value ladder - only fighters own that (see .ai/art-direction.md).
	/// </summary>
	void DrawCloud()
	{
		float grow = Mathf.Min(1.0f, ageFrames / (float)CloudFormFrames);
		float fade = Mathf.Min(1.0f, (lifeFrames - ageFrames) / 20.0f);
		float w = move.FxRadius * (0.5f + 0.5f * grow);
		const int Puffs = 7;
		for (int i = 0; i < Puffs; i++)
		{
			float x = (i / (float)(Puffs - 1) - 0.5f) * w * 1.7f;
			float bob = CrayonBrush.Noise(ageFrames / 6 + i, 29) * 6.0f;
			float r = w * (0.36f + 0.12f * Mathf.Abs(CrayonBrush.Noise(i, 31))) * (1.0f - 0.35f * Mathf.Abs(x) / w);
			DrawCircle(new Vector2(x, bob + r * 0.25f), r * 1.05f, new Color(0.97f, 0.52f, 0.22f, 0.55f * fade));
			DrawCircle(new Vector2(x, bob - r * 0.15f), r, new Color(0.70f, 0.60f, 0.58f, 0.92f * fade));
			DrawCircle(new Vector2(x - r * 0.25f, bob - r * 0.4f), r * 0.55f, new Color(0.80f, 0.72f, 0.70f, 0.85f * fade));
		}
	}

	/// <summary>A little missile pointing the way it flies: a red-tipped grey body, fins, and an exhaust flame.</summary>
	void DrawMissile(float radius)
	{
		Vector2 dir = velocity.LengthSquared() > 1.0f ? velocity.Normalized() : Vector2.Right;
		Vector2 side = dir.Orthogonal();
		float length = radius * 3.4f;
		float half = radius * 0.55f;
		Vector2 nose = dir * length * 0.5f;
		Vector2 tail = -dir * length * 0.5f;

		float flicker = 0.75f + 0.25f * CrayonBrush.Noise(ageFrames, 7);
		DrawColoredPolygon(new[] { tail + side * half * 0.8f, tail - side * half * 0.8f, tail - dir * radius * 2.2f * flicker },
			new Color(0.98f, 0.62f, 0.22f, 0.9f));
		DrawColoredPolygon(new[] { tail + side * half * 0.4f, tail - side * half * 0.4f, tail - dir * radius * 1.2f * flicker },
			new Color(1.0f, 0.93f, 0.62f));

		var steel = new Color(0.62f, 0.64f, 0.70f);
		var edge = new Color(0.36f, 0.36f, 0.42f);
		DrawColoredPolygon(new[] { tail + side * half * 2.0f, tail + side * half, tail + dir * radius * 1.1f + side * half }, edge);
		DrawColoredPolygon(new[] { tail - side * half * 2.0f, tail - side * half, tail + dir * radius * 1.1f - side * half }, edge);
		DrawColoredPolygon(new[] { tail + side * half, nose - dir * radius * 0.6f + side * half,
			nose - dir * radius * 0.6f - side * half, tail - side * half }, steel);
		DrawColoredPolygon(new[] { nose - dir * radius * 0.6f + side * half, nose, nose - dir * radius * 0.6f - side * half },
			new Color(0.92f, 0.28f, 0.26f));
	}

	public override void _Draw()
	{
		if (move == null) return;

		// A walker is its owner's own puppet, which draws itself.
		if (move.Special == SpecialKind.Walker && miniRig != null) return;

		if (move.Special == SpecialKind.Puddle)
		{
			DrawPuddle();
			return;
		}

		float t = ageFrames / (float)Mathf.Max(1, lifeFrames);

		// A lingering effect - a quake, a vent, a patch of fire - fades as it burns out, so "this is
		// about to stop hurting" is visible. A trap or a drawing never does (a planted sword, a
		// traffic cone, a risen axe): it is there and then simply gone. Eric's calls, 2026-10-04.
		Color body = move.FxColor;
		if (Lingers && move.FxTexture == null && move.Special != SpecialKind.Trap) body.A = Mathf.Lerp(1.0f, 0.35f, t);

		float wobble = 1.0f + CrayonBrush.Noise(ageFrames / 4, 3) * 0.10f;
		float radius = move.FxRadius * wobble;

		if (move.FxTexture != null)
		{
			DrawDrawnEffect(move.FxTexture, body.A);
			return;
		}

		if (move.Special == SpecialKind.Shockwave)
		{
			DrawQuake(body.A);
			return;
		}

		if (move.Streak && velocity.LengthSquared() > 1.0f)
		{
			// A nail: a short streak with a head, pointing the way it flies - in the move's own
			// colour, edged in grey so a bright nail still reads on a pale stage.
			Vector2 back = -velocity.Normalized() * radius * 3.2f;
			var edge = new Color(0.36f, 0.34f, 0.38f);
			DrawLine(back, Vector2.Zero, edge, radius * 1.0f);
			DrawCircle(back, radius * 0.75f, edge);
			DrawLine(back, Vector2.Zero, move.FxColor, radius * 0.55f);
			DrawCircle(back, radius * 0.5f, move.FxColor);
			return;
		}

		if (move.Beam)
		{
			// No drawing for this beam: a plain crayon beam from the hands to its far end.
			Vector2 tip = new Vector2(beamDir * beamLength, 0.0f);
			DrawLine(Vector2.Zero, tip, body, radius * 2.0f);
			DrawCircle(tip, radius, body);
			DrawLine(Vector2.Zero, tip, new Color(1.0f, 0.95f, 0.85f, 0.9f), radius * 0.7f);
			return;
		}

		if (move.Special == SpecialKind.Vent)
		{
			DrawVent(body.A);
			return;
		}

		if (move.Special == SpecialKind.Cloud)
		{
			DrawCloud();
			return;
		}

		if (move.FxMissile)
		{
			DrawMissile(radius);
			return;
		}

		if (move.FxFlame)
		{
			DrawFlame(radius);
			return;
		}

		DrawCircle(Vector2.Zero, radius, body);
		CrayonBrush.Scribble(this, Vector2.Zero, new Vector2(radius * 2.1f, radius * 2.1f),
			new Color(0.16f, 0.16f, 0.20f, 0.85f), ageFrames / 3, 5.0f, 6);
	}
}
