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
		else
		{
			velocity = new Vector2(velocity.X, velocity.Y + gravity * dt);
			GlobalPosition += velocity * dt;
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
		if (ageFrames >= lifeFrames || offStage) Expire();
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

	void QueryHits()
	{
		if (match == null || expiring) return;

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
			// what makes a trap a zoning tool rather than a slow projectile.
			if (!Lingers) Expire();
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

		float radius = move.FxRadius;
		float length = move.Beam ? beamLength : 0.0f;
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
		|| move.Special == SpecialKind.Shockwave;

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

			DrawSetTransformMatrix(new Transform2D(angle, new Vector2(s, s), 0.0f, Vector2.Zero));
			DrawTexture(art, -size * 0.5f, tint);
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

	public override void _Draw()
	{
		if (move == null) return;

		float t = ageFrames / (float)Mathf.Max(1, lifeFrames);

		// A trap fades as it burns out, so "this is about to stop hurting" is visible.
		Color body = move.FxColor;
		if (Lingers) body.A = Mathf.Lerp(1.0f, 0.35f, t);

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
			// A nail: a short dark streak with a head, pointing the way it flies.
			Vector2 back = -velocity.Normalized() * radius * 3.2f;
			DrawLine(back, Vector2.Zero, new Color(0.30f, 0.32f, 0.38f), radius * 0.7f);
			DrawCircle(back, radius * 0.55f, new Color(0.30f, 0.32f, 0.38f));
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

		DrawCircle(Vector2.Zero, radius, body);
		CrayonBrush.Scribble(this, Vector2.Zero, new Vector2(radius * 2.1f, radius * 2.1f),
			new Color(0.16f, 0.16f, 0.20f, 0.85f), ageFrames / 3, 5.0f, 6);
	}
}
