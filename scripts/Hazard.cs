using System.Collections.Generic;
using Godot;

/// <summary>
/// Anything a special leaves behind that can hit someone: a fireball, a thrown hammer, a
/// falling anvil, a patch of fire on the ground.
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

	readonly HashSet<Fighter> alreadyHit = new HashSet<Fighter>();

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
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		ageFrames++;

		velocity = new Vector2(velocity.X, velocity.Y + gravity * dt);
		GlobalPosition += velocity * dt;

		QueryHits();
		QueueRedraw();

		// Hazards die of old age or by leaving the stage. Without the second check a fireball
		// fired off the side would live out its full lifetime somewhere nobody can see.
		bool offStage = match?.StageBounds.Grow(400.0f).HasPoint(GlobalPosition) == false;
		if (ageFrames >= lifeFrames || offStage) Expire();
	}

	void QueryHits()
	{
		if (match == null || expiring) return;

		foreach (Fighter other in match.Fighters)
		{
			if (other == owner && ageFrames < OwnerGraceFrames) continue;
			if (other == owner) continue;
			if (alreadyHit.Contains(other)) continue;
			if (!other.CanBeHitByHazard) continue;

			Rect2 body = other.BodyRect();
			Vector2 nearest = new Vector2(
				Mathf.Clamp(GlobalPosition.X, body.Position.X, body.End.X),
				Mathf.Clamp(GlobalPosition.Y, body.Position.Y, body.End.Y));

			if (nearest.DistanceSquaredTo(GlobalPosition) > move.FxRadius * move.FxRadius) continue;

			alreadyHit.Add(other);
			other.ReceiveHit(owner, move, nearest);

			// A travelling hazard is spent on contact; a lingering one keeps burning, which is
			// what makes a trap a zoning tool rather than a slow projectile.
			if (!Lingers) Expire();
			return;
		}
	}

	/// <summary>A trap burns and an explosion blasts; both keep hitting until they fade.</summary>
	bool Lingers => move.Special == SpecialKind.Trap || move.Special == SpecialKind.Bomb;

	void Expire()
	{
		if (expiring) return;
		expiring = true;
		QueueFree();
	}

	/// <summary>
	/// A kid's drawing of the effect. A beam is sized by its thickness and trails back from the
	/// hitbox at its head; anything else is fitted around the hitbox. Only moved, scaled and
	/// turned - never redrawn.
	/// </summary>
	void DrawDrawnEffect(Texture2D art, float alpha)
	{
		Vector2 size = art.GetSize();
		var tint = new Color(1.0f, 1.0f, 1.0f, alpha);

		if (move.Beam)
		{
			// The drawn band of the beam is roughly the middle third of the picture.
			float s = move.FxRadius * 2.0f * 2.6f / size.Y;
			float angle = velocity.LengthSquared() > 1.0f ? velocity.Angle() : 0.0f;
			DrawSetTransform(Vector2.Zero, angle, new Vector2(s, s));
			DrawTexture(art, new Vector2(-size.X, -size.Y * 0.5f), tint);
		}
		else
		{
			float s = move.FxRadius * 2.3f / Mathf.Max(size.X, size.Y);
			DrawSetTransform(Vector2.Zero, 0.0f, new Vector2(s, s));
			DrawTexture(art, -size * 0.5f, tint);
		}

		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
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

		if (move.Beam && velocity.LengthSquared() > 1.0f)
		{
			// A beam trails back along its path. It still hits as a circle at its head, so what
			// you see behind it is a streak, not extra reach.
			Vector2 tail = -velocity.Normalized() * radius * 5.0f;
			DrawLine(tail, Vector2.Zero, body, radius * 2.0f);
			DrawCircle(tail, radius, body);
			DrawCircle(Vector2.Zero, radius, body);
			DrawLine(tail, Vector2.Zero, new Color(1.0f, 0.95f, 0.85f, 0.9f), radius * 0.7f);
			return;
		}

		DrawCircle(Vector2.Zero, radius, body);
		CrayonBrush.Scribble(this, Vector2.Zero, new Vector2(radius * 2.1f, radius * 2.1f),
			new Color(0.16f, 0.16f, 0.20f, 0.85f), ageFrames / 3, 5.0f, 6);
	}
}
