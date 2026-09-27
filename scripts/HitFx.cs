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
		if (sparks.Count == 0) return;

		float dt = (float)delta;
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
		foreach (Spark s in sparks)
		{
			float t = s.Age / s.Life;
			Color c = s.Tint;
			c.A = 1.0f - t;
			DrawCircle(s.Position, s.Radius * (0.45f + t * 1.15f), c);
		}
	}
}
