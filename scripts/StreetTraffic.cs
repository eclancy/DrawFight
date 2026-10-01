using System.Collections.Generic;
using Godot;

/// <summary>
/// Cars driving along a stage's street. Every few seconds, at random, one comes in from off one
/// side of the stage and drives straight across, and anyone standing in the road when it arrives
/// is knocked flying the way it was going. Jump over it or get out of the street.
///
/// Headlights reach well ahead of each car, so it is seen coming before it is on screen - a
/// hazard you cannot see in time is not a hazard, it is bad luck.
///
/// Timing is random, but the car's look is not: its colour and wobble are hashed from a seed it
/// is given when it appears, so a car never shimmers while it drives.
/// </summary>
public partial class StreetTraffic : Node2D
{
	sealed class Car
	{
		public float X;
		public int Direction;
		public float Speed;
		public int Seed;
		public readonly HashSet<Fighter> Hit = new HashSet<Fighter>();
	}

	public MatchManager Match;

	readonly List<Car> cars = new List<Car>();
	readonly RandomNumberGenerator rng = new RandomNumberGenerator();

	float roadY;
	float leftEdge;
	float rightEdge;
	int nextCarFrames;
	int carCount;

	const float CarLength = 230.0f;
	const float CarHeight = 92.0f;
	const float HeadlightReach = 460.0f;

	/// <summary>Frames between cars, at random within this range: two and a half to five and a half seconds.</summary>
	const int MinGapFrames = 150;
	const int MaxGapFrames = 330;

	/// <summary>
	/// What a car does to you: a smash that sends you up and sideways, off the top of the stage
	/// from about 60% (a heavy lasts to about 80%, a floaty one goes at about 45%). Below that
	/// it just throws you a long way. Mostly up, because the street is at the bottom of shafts
	/// between buildings and a flat launch only slams you into the nearest wall.
	/// </summary>
	static readonly MoveData CarHit = new MoveData
	{
		MoveName = "Car",
		Damage = 20.0f, BaseKnockback = 25.0f, KnockbackGrowth = 2.0f,
		LaunchAngleDegrees = 60.0f,
		Unblockable = true,
	};

	static readonly Color[] Paint =
	{
		new Color(0.93f, 0.36f, 0.34f), new Color(0.34f, 0.60f, 0.93f), new Color(0.98f, 0.80f, 0.28f),
		new Color(0.46f, 0.78f, 0.46f), new Color(0.97f, 0.58f, 0.24f), new Color(0.78f, 0.52f, 0.90f),
	};

	public void Setup(float roadTop, StageData data)
	{
		roadY = roadTop;
		leftEdge = data.BlastZone.Position.X - CarLength;
		rightEdge = data.BlastZone.End.X + CarLength;
		rng.Randomize();
		nextCarFrames = rng.RandiRange(60, 180);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		if (--nextCarFrames <= 0)
		{
			SpawnCar();
			nextCarFrames = rng.RandiRange(MinGapFrames, MaxGapFrames);
		}

		for (int i = cars.Count - 1; i >= 0; i--)
		{
			Car car = cars[i];
			car.X += car.Direction * car.Speed * dt;
			if (car.X < leftEdge - 200.0f || car.X > rightEdge + 200.0f)
			{
				cars.RemoveAt(i);
				continue;
			}
			HitFighters(car);
		}

		QueueRedraw();
	}

	void SpawnCar()
	{
		int direction = rng.Randf() < 0.5f ? 1 : -1;

		// One lane: never send a car into the path of one coming the other way.
		foreach (Car other in cars)
		{
			if (other.Direction != direction) return;
		}

		cars.Add(new Car
		{
			Direction = direction,
			X = direction > 0 ? leftEdge : rightEdge,
			Speed = rng.RandfRange(950.0f, 1350.0f),
			Seed = 1000 + carCount++ * 37,
		});
	}

	Rect2 CarRect(Car car) => new Rect2(car.X - CarLength * 0.5f, roadY - CarHeight, CarLength, CarHeight);

	void HitFighters(Car car)
	{
		if (Match == null) return;
		Rect2 body = CarRect(car);
		foreach (Fighter fighter in Match.Fighters)
		{
			if (car.Hit.Contains(fighter) || !fighter.CanBeHitByHazard) continue;
			if (!body.Intersects(fighter.BodyRect())) continue;
			car.Hit.Add(fighter);
			fighter.ReceiveStageHit(CarHit, car.Direction, new Vector2(fighter.GlobalPosition.X, body.Position.Y + 20.0f));
		}
	}

	public override void _Draw()
	{
		foreach (Car car in cars) DrawCar(car);
	}

	/// <summary>
	/// A crayon car seen side on: a body and a cabin in one bright colour, pale windows, two
	/// wheels, and a wash of headlight on the road in front of it.
	/// </summary>
	void DrawCar(Car car)
	{
		Rect2 r = CarRect(car);
		int d = car.Direction;
		float front = d > 0 ? r.End.X : r.Position.X;
		Color paint = Paint[Mathf.PosMod(car.Seed / 37, Paint.Length)];
		var ink = new Color(0.20f, 0.24f, 0.46f);

		// The headlight beam first, so the car sits on top of it.
		var beam = new[]
		{
			new Vector2(front, r.Position.Y + 34.0f),
			new Vector2(front + d * HeadlightReach, r.Position.Y + 8.0f),
			new Vector2(front + d * HeadlightReach, r.End.Y + 6.0f),
			new Vector2(front, r.Position.Y + 50.0f),
		};
		DrawColoredPolygon(beam, new Color(1.0f, 0.95f, 0.66f, 0.35f));

		var body = new Rect2(r.Position.X, r.Position.Y + 34.0f, r.Size.X, 40.0f);
		float cabinStart = d > 0 ? r.Position.X + 44.0f : r.Position.X + 70.0f;
		var cabin = new Rect2(cabinStart, r.Position.Y, 116.0f, 38.0f);

		DrawRect(cabin, paint);
		DrawRect(body, paint);
		CrayonBrush.CrayonFill(this, body, new Color(paint.R * 0.85f, paint.G * 0.85f, paint.B * 0.85f, 0.6f), car.Seed, 10.0f, 2.0f, 6.0f);

		// Two windows in the cabin.
		var pane = new Color(0.90f, 0.93f, 0.98f);
		DrawRect(new Rect2(cabin.Position.X + 10.0f, cabin.Position.Y + 8.0f, 44.0f, 24.0f), pane);
		DrawRect(new Rect2(cabin.Position.X + 62.0f, cabin.Position.Y + 8.0f, 44.0f, 24.0f), pane);

		CrayonBrush.InkRect(this, cabin, ink, 3.0f, car.Seed + 1, 1.4f);
		CrayonBrush.InkRect(this, body, ink, 3.5f, car.Seed + 2, 1.4f);

		// Headlight and tail light.
		DrawCircle(new Vector2(front - d * 8.0f, body.Position.Y + 12.0f), 7.0f, new Color(1.0f, 0.96f, 0.70f));
		float back = d > 0 ? r.Position.X : r.End.X;
		DrawCircle(new Vector2(back + d * 7.0f, body.Position.Y + 12.0f), 6.0f, new Color(0.95f, 0.40f, 0.36f));

		foreach (float wx in new[] { r.Position.X + 52.0f, r.End.X - 52.0f })
		{
			var hub = new Vector2(wx, r.End.Y - 6.0f);
			DrawCircle(hub, 20.0f, new Color(0.40f, 0.40f, 0.48f));
			DrawCircle(hub, 8.0f, new Color(0.82f, 0.82f, 0.88f));
			DrawArc(hub, 20.0f, 0.0f, Mathf.Tau, 20, ink, 3.0f);
		}
	}
}
