using System.Collections.Generic;
using Godot;

/// <summary>
/// A platform a fighter builds under their own feet - Lug's steel girder. It holds still long
/// enough to stand on and jump off, shakes as a warning, then falls: anyone it lands on is hit
/// (Lug's spikes), and it breaks when it reaches solid ground or leaves the stage.
///
/// It is a soft platform (layer 2), exactly like a stage's drop-through platforms, so standing,
/// jumping up through it and dropping through it all work with no new rules. It is an
/// AnimatableBody2D so a fighter standing on it rides it down as it starts to fall.
/// </summary>
public partial class BuiltPlatform : AnimatableBody2D
{
	Fighter owner;
	MoveData move;
	MatchManager match;
	Vector2 size;
	int ageFrames;
	float fallSpeed;
	readonly HashSet<Fighter> alreadyHit = new HashSet<Fighter>();

	/// <summary>How long before the drop that it shakes, so nobody is surprised by it.</summary>
	const int ShakeFrames = 24;

	const float Gravity = 2600.0f;
	const float MaxFallSpeed = 1900.0f;

	int HoldFrames => move.PlatformHoldFrames;
	bool Falling => ageFrames > HoldFrames;

	public void Build(Fighter owner, MoveData move, MatchManager match, Rect2 rect)
	{
		this.owner = owner;
		this.move = move;
		this.match = match;
		size = rect.Size;

		SyncToPhysics = true;
		CollisionLayer = 0b10;
		CollisionMask = 0;
		AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = size },
			OneWayCollision = true,
		});

		GlobalPosition = rect.GetCenter();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		ageFrames++;

		if (Falling)
		{
			fallSpeed = Mathf.Min(MaxFallSpeed, fallSpeed + Gravity * dt);
			GlobalPosition += new Vector2(0.0f, fallSpeed * dt);
			HitWhateverIsBelow();

			if (HitsSolidGround() || match == null || !match.StageBounds.Grow(200.0f).HasPoint(GlobalPosition))
			{
				QueueFree();
				return;
			}
		}

		QueueRedraw();
	}

	Rect2 Bounds => new Rect2(GlobalPosition - size * 0.5f, size);

	/// <summary>
	/// While falling, it hits anyone it comes down on - but not anyone riding on top, and never
	/// the fighter who built it.
	/// </summary>
	void HitWhateverIsBelow()
	{
		if (match == null) return;
		Rect2 beam = Bounds;

		foreach (Fighter other in match.Fighters)
		{
			if (other == owner || alreadyHit.Contains(other) || !other.CanBeHitByHazard) continue;

			Rect2 body = other.BodyRect();
			if (!beam.Intersects(body)) continue;

			// Standing on it means your feet are at its top edge. Only someone whose body is
			// underneath it gets hit.
			if (body.GetCenter().Y < beam.Position.Y) continue;

			alreadyHit.Add(other);
			other.ReceiveHit(owner, move, new Vector2(body.GetCenter().X, beam.End.Y));
		}
	}

	bool HitsSolidGround()
	{
		var query = new PhysicsPointQueryParameters2D
		{
			Position = GlobalPosition + new Vector2(0.0f, size.Y * 0.5f + 4.0f),
			CollisionMask = 0b01,
		};
		return GetWorld2D().DirectSpaceState.IntersectPoint(query, 1).Count > 0;
	}

	public override void _Draw()
	{
		// Shakes in the last moments before it drops - hashed, never random.
		Vector2 jolt = Vector2.Zero;
		int untilFall = HoldFrames - ageFrames;
		if (!Falling && untilFall < ShakeFrames)
		{
			jolt = new Vector2(CrayonBrush.Noise(ageFrames, 3) * 5.0f, CrayonBrush.Noise(ageFrames, 7) * 2.0f);
		}

		var rect = new Rect2(-size * 0.5f + jolt, size);
		if (move.FxTexture != null)
		{
			DrawTextureRect(move.FxTexture, rect, false);
		}
		else
		{
			DrawRect(rect, move.FxColor);
		}
		CrayonBrush.InkRect(this, rect, new Color(0.16f, 0.16f, 0.20f), 3.0f, 71, 1.4f, dashed: true);
	}
}
