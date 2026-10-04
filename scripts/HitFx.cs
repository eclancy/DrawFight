using System.Collections.Generic;
using Godot;

/// <summary>
/// Hit sparks and blast-zone flashes. Drawn immediate-mode rather than as particle scenes so
/// that M1 needs no imported assets at all.
/// </summary>
public partial class HitFx : Node2D
{
	struct Spark
	{
		public Vector2 Position;
		public float Age;
		public float Life;
		public float Radius;
		public Color Tint;
	}

	readonly List<Spark> sparks = new List<Spark>();

	/// <summary>
	/// A puff of dust off the floor - a landing, a footstep, a skid. Pale and warm, soft-edged,
	/// drifting the way it was kicked and fading as it spreads. Light enough to stay off the dark
	/// end of the value ladder (see .ai/art-direction.md).
	/// </summary>
	struct Dust
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Age;
		public float Life;
		public float Radius;
	}

	readonly List<Dust> dust = new List<Dust>();
	int dustCount;

	/// <summary>
	/// Kicks up dust at a point on the floor. <paramref name="size"/> 1 is a footstep, 3 a heavy
	/// landing; <paramref name="push"/> is which way it is kicked along the floor (-1 to 1), 0 for
	/// both ways at once.
	/// </summary>
	public void SpawnDust(Vector2 at, float size, float push)
	{
		int puffs = push == 0.0f ? 6 : 3;
		for (int i = 0; i < puffs; i++)
		{
			int k = dustCount++;
			float side = push != 0.0f ? push : (i % 2 == 0 ? -1.0f : 1.0f);
			float spread = 0.6f + 0.4f * Mathf.Abs(CrayonBrush.Noise(k, 61));
			dust.Add(new Dust
			{
				Position = at + new Vector2(CrayonBrush.Noise(k, 63) * 10.0f * size, -4.0f),
				Velocity = new Vector2(side * (60.0f + 70.0f * spread) * size, -(30.0f + 40.0f * spread) * Mathf.Sqrt(size)),
				Life = 0.30f + 0.12f * size,
				Radius = (9.0f + 4.0f * spread) * Mathf.Sqrt(size),
			});
		}
	}

	/// <summary>
	/// A KO: a huge burst at the edge of the screen where the fighter went out, in their own
	/// colour, with rays blasting back in across the stage. It has to be unmistakable - the
	/// most important thing that happens in a match should be the biggest thing on screen.
	/// </summary>
	struct KoBlast
	{
		/// <summary>
		/// Where in the WORLD it goes off: the spot the fighter left the screen. It stays there
		/// while the camera swings back to the fighters still playing, so it reads as an explosion
		/// at the place they fell out, not a sticker on the screen.
		/// </summary>
		public Vector2 Position;
		/// <summary>Its size, fixed from the camera zoom at the moment of the KO, so a zoom
		/// afterwards does not make it swell or shrink.</summary>
		public float Scale;
		public Vector2 Inward;
		public float Age;
		public Color Tint;
		public int Seed;
	}

	readonly List<KoBlast> blasts = new List<KoBlast>();
	const float KoLife = 1.1f;
	int koCount;

	/// <summary>The match camera, which sets how big a KO blast is drawn.</summary>
	public GameCamera Camera;

	public void SpawnKoBlast(Vector2 position, Vector2 inward, Color tint)
	{
		blasts.Add(new KoBlast
		{
			Position = position,
			Scale = Camera != null ? Camera.VisibleRect().Size.X / 1920.0f : 1.0f,
			Inward = inward.Normalized(),
			Tint = tint,
			Seed = 97 + koCount++ * 31,
		});
	}

	public void SpawnHitSpark(Vector2 position, float damage, bool blocked)
	{
		sparks.Add(new Spark
		{
			Position = position,
			Age = 0.0f,
			Life = blocked ? 0.16f : 0.26f,
			Radius = 16.0f + damage * (blocked ? 1.1f : 2.6f),
			Tint = blocked ? new Color(0.45f, 0.82f, 1.0f) : new Color(1.0f, 0.93f, 0.55f),
		});
	}

	public void SpawnBlastFlash(Vector2 position)
	{
		sparks.Add(new Spark
		{
			Position = position,
			Age = 0.0f,
			Life = 0.55f,
			Radius = 150.0f,
			Tint = new Color(1.0f, 0.55f, 0.35f),
		});
	}

	public override void _Process(double delta)
	{
		if (sparks.Count == 0 && blasts.Count == 0 && dust.Count == 0) return;

		float dt = (float)delta;
		for (int i = dust.Count - 1; i >= 0; i--)
		{
			Dust d = dust[i];
			d.Age += dt;
			d.Position += d.Velocity * dt;
			d.Velocity *= 1.0f - 4.0f * dt;
			if (d.Age >= d.Life) dust.RemoveAt(i);
			else dust[i] = d;
		}

		for (int i = blasts.Count - 1; i >= 0; i--)
		{
			KoBlast b = blasts[i];
			b.Age += dt;
			if (b.Age >= KoLife) blasts.RemoveAt(i);
			else blasts[i] = b;
		}

		for (int i = sparks.Count - 1; i >= 0; i--)
		{
			Spark s = sparks[i];
			s.Age += dt;
			if (s.Age >= s.Life) sparks.RemoveAt(i);
			else sparks[i] = s;
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (Dust d in dust)
		{
			float t = d.Age / d.Life;
			DrawCircle(d.Position, d.Radius * (0.7f + 0.9f * t), new Color(0.78f, 0.74f, 0.68f, 0.6f * (1.0f - t)));
		}

		foreach (KoBlast b in blasts) DrawKoBlast(b);

		foreach (Spark s in sparks)
		{
			float t = s.Age / s.Life;
			Color c = s.Tint;
			c.A = 1.0f - t;
			DrawCircle(s.Position, s.Radius * (0.45f + t * 1.15f), c);
		}
	}

	void DrawKoBlast(KoBlast b) => DrawKoBlastAt(b, b.Position, b.Scale);

	void DrawKoBlastAt(KoBlast b, Vector2 position, float k)
	{
		float t = b.Age / KoLife;
		float grow = 1.0f - (1.0f - Mathf.Min(1.0f, t * 3.0f)) * (1.0f - Mathf.Min(1.0f, t * 3.0f));
		float fade = t < 0.55f ? 1.0f : 1.0f - (t - 0.55f) / 0.45f;

		// A fan of long rays blasting back in across the stage from where they went out.
		float baseAngle = b.Inward.Angle();
		const int Rays = 11;
		for (int i = 0; i < Rays; i++)
		{
			float spread = (i / (float)(Rays - 1) - 0.5f) * 1.9f;
			float length = (520.0f + 420.0f * Mathf.Abs(CrayonBrush.Noise(b.Seed, i))) * grow * k;
			float width = (34.0f + 26.0f * Mathf.Abs(CrayonBrush.Noise(b.Seed + 1, i))) * k;
			Vector2 dir = Vector2.Right.Rotated(baseAngle + spread);
			Vector2 side = dir.Orthogonal() * width * 0.5f;
			Color ray = i % 2 == 0 ? b.Tint : new Color(1.0f, 0.92f, 0.52f);
			ray.A = 0.85f * fade;
			DrawColoredPolygon(new[] { position + side, position + dir * length, position - side }, ray);
		}

		// A shock ring racing outward, then the burst itself: fighter colour round a white-hot core.
		var ring = new Color(1.0f, 1.0f, 1.0f, 0.7f * fade);
		DrawArc(position, (60.0f + 700.0f * t) * k, 0.0f, Mathf.Tau, 48, ring, (14.0f * (1.0f - t) + 2.0f) * k);

		float burst = 260.0f * grow * (1.0f - 0.3f * t) * k;
		Color outer = b.Tint;
		outer.A = 0.9f * fade;
		DrawCircle(position, burst, outer);
		DrawCircle(position, burst * 0.68f, new Color(1.0f, 0.86f, 0.40f, 0.95f * fade));
		DrawCircle(position, burst * 0.38f, new Color(1.0f, 1.0f, 0.96f, fade));
	}
}
