using Godot;

/// <summary>
/// Fire, drawn as tongues of flame: each one a curved, tapering lick - wide and rounded at its
/// root, bending as it rises, flickering to a point - in three layers, red outside, orange, a
/// yellow heart. Every fire effect is built of these, so they all burn alike. Straight triangles
/// round a circle read as an orb with spikes (Eric, 2026-10-10). Every flicker is hashed from a
/// frame count, never random - see the crayon-wobble rule in CLAUDE.md.
/// </summary>
public static class FlameFx
{
	static readonly Color Outer = new Color(0.94f, 0.30f, 0.12f);
	static readonly Color Middle = new Color(0.99f, 0.58f, 0.16f);
	static readonly Color Heart = new Color(1.0f, 0.88f, 0.42f);

	/// <summary>
	/// One tongue of flame from <paramref name="root"/>, rising along <paramref name="dir"/> for
	/// <paramref name="length"/>, <paramref name="width"/> across at its widest, curving to one side
	/// by <paramref name="bend"/> (a fraction of its length; the sign is the side) and swaying with
	/// <paramref name="clock"/>. <paramref name="seed"/> keeps neighbouring tongues out of step.
	/// </summary>
	public static void Tongue(CanvasItem canvas, Vector2 root, Vector2 dir, float length, float width,
		float bend, int clock, int seed, float alpha)
	{
		if (alpha <= 0.01f || length < 2.0f) return;
		dir = dir.Normalized();
		// The tip sways: the bend breathes in and out, out of step with every other tongue.
		float sway = bend + 0.18f * Mathf.Sin(clock * 0.31f + seed * 1.7f);
		float flicker = 1.0f + 0.14f * CrayonBrush.Noise(clock / 2, seed * 3 + 1);
		Layer(canvas, root, dir, length * flicker, width, sway, new Color(Outer, 0.82f * alpha));
		Layer(canvas, root + dir * width * 0.12f, dir, length * flicker * 0.72f, width * 0.68f, sway * 1.1f, new Color(Middle, 0.88f * alpha));
		Layer(canvas, root + dir * width * 0.22f, dir, length * flicker * 0.42f, width * 0.36f, sway * 1.2f, new Color(Heart, 0.92f * alpha));
	}

	/// <summary>
	/// The tongue's outline, round its curved spine: a quadratic curve that leaves the root along
	/// <paramref name="dir"/> and bends off to one side, the width swelling a little past the root
	/// and closing to a point at the tip - and rounded off below the root, so each one is a
	/// flame's teardrop and tongues that overlap run together into one fire. Cut straight across
	/// at the root, they read as separate petals.
	/// </summary>
	static void Layer(CanvasItem canvas, Vector2 root, Vector2 dir, float length, float width, float bend, Color colour)
	{
		const int Steps = 9;
		const int Round = 5;
		Vector2 side = dir.Orthogonal();
		Vector2 control = root + dir * length * 0.55f;
		Vector2 tip = root + dir * length + side * bend * length;
		var points = new Vector2[Steps * 2 + Round];
		Vector2 rootNormal = Vector2.Zero;
		float rootHalf = 0.0f;
		for (int i = 0; i < Steps; i++)
		{
			float t = i / (float)(Steps - 1);
			float u = 1.0f - t;
			Vector2 at = u * u * root + 2.0f * u * t * control + t * t * tip;
			Vector2 tangent = (2.0f * u * (control - root) + 2.0f * t * (tip - control)).Normalized();
			Vector2 normal = tangent.Orthogonal();
			float half = width * 0.5f * Mathf.Pow(u, 0.85f) * (0.75f + 0.55f * Mathf.Sin(Mathf.Pi * t));
			points[i] = at + normal * half;
			points[Steps * 2 - 1 - i] = at - normal * half;
			if (i == 0)
			{
				rootNormal = normal;
				rootHalf = half;
			}
		}
		// Round the bottom: half a circle behind the root, from one side of it to the other.
		for (int k = 1; k <= Round; k++)
		{
			float a = Mathf.Pi * k / (Round + 1);
			points[Steps * 2 - 1 + k] = root - rootNormal.Rotated(a) * rootHalf;
		}
		canvas.DrawColoredPolygon(points, colour);
	}

	/// <summary>
	/// A soft glow of heat: overlapping discs fading outward, for the body of a fire the tongues
	/// rise from - a fireball's heart, the inside of a ring.
	/// </summary>
	public static void Glow(CanvasItem canvas, Vector2 centre, float radius, float alpha)
	{
		if (alpha <= 0.01f) return;
		canvas.DrawCircle(centre, radius, new Color(Outer, 0.22f * alpha));
		canvas.DrawCircle(centre, radius * 0.72f, new Color(Middle, 0.26f * alpha));
		canvas.DrawCircle(centre, radius * 0.42f, new Color(Heart, 0.32f * alpha));
	}

	/// <summary>
	/// A ball of fire with flame streaming off it the way it is not going: a heart, then curved
	/// tongues licking back from all round its trailing side. A fireball, a burning drop.
	/// <paramref name="back"/> is the way the flames trail.
	/// </summary>
	public static void Ball(CanvasItem canvas, Vector2 centre, float radius, Vector2 back, int clock, float alpha = 1.0f)
	{
		back = back.LengthSquared() > 0.01f ? back.Normalized() : Vector2.Up;
		const int Licks = 7;
		for (int i = 0; i < Licks; i++)
		{
			float a = (i / (float)(Licks - 1) - 0.5f) * 2.4f;
			Vector2 dir = back.Rotated(a * 0.55f);
			float length = radius * (1.6f + 0.9f * (1.0f - Mathf.Abs(a) / 1.2f));
			Tongue(canvas, centre + dir * radius * 0.35f, dir, length, radius * 0.95f,
				-a * 0.18f, clock, i + 5, alpha);
		}
		canvas.DrawCircle(centre, radius * 1.02f, new Color(Outer, 0.92f * alpha));
		canvas.DrawCircle(centre, radius * 0.78f, new Color(Middle, 0.95f * alpha));
		canvas.DrawCircle(centre, radius * 0.48f, new Color(Heart, 0.95f * alpha));
	}
}
