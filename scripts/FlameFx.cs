using Godot;

/// <summary>
/// Fire, drawn as tongues of flame: each one a curved, tapering lick - wide and rounded at its
/// root, bending as it rises, flickering to a point - in three layers, red outside, orange, a
/// yellow heart. Every fire effect is built of these, so they all burn alike. Straight triangles
/// round a circle read as an orb with spikes (Eric, 2026-10-10). A fire that stays put rises
/// (<see cref="Tongue"/>); a fire on the move - a fireball, a fireball roll - streams straight
/// back from the way it is going like a comet, and rises as it trails (<see cref="Comet"/>,
/// <see cref="Lick"/>). Every flicker is hashed from a frame count, never random - see the
/// crayon-wobble rule in CLAUDE.md.
/// </summary>
public static class FlameFx
{
	static readonly Color Outer = new Color(0.94f, 0.30f, 0.12f);
	static readonly Color Middle = new Color(0.99f, 0.58f, 0.16f);
	static readonly Color Heart = new Color(1.0f, 0.88f, 0.42f);

	/// <summary>
	/// The furthest a tongue leans off straight up, in radians (50 degrees). Fire rises: a tongue
	/// thrown sideways or down off a wheel or a dive still burns upward (Eric, 2026-10-10). A fire
	/// on the move trails instead - see <see cref="Comet"/>.
	/// </summary>
	const float MostLean = 0.87f;

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
		dir = Rising(dir.Normalized(), seed);
		// The tip sways: the bend breathes in and out, out of step with every other tongue.
		float sway = bend + 0.18f * Mathf.Sin(clock * 0.31f + seed * 1.7f);
		float flicker = 1.0f + 0.14f * CrayonBrush.Noise(clock / 2, seed * 3 + 1);
		Layer(canvas, root, dir, length * flicker, width, sway, new Color(Outer, 0.82f * alpha));
		Layer(canvas, root + dir * width * 0.12f, dir, length * flicker * 0.72f, width * 0.68f, sway * 1.1f, new Color(Middle, 0.88f * alpha));
		Layer(canvas, root + dir * width * 0.22f, dir, length * flicker * 0.42f, width * 0.36f, sway * 1.2f, new Color(Heart, 0.92f * alpha));
	}

	/// <summary>
	/// <paramref name="dir"/>, leaned back toward straight up until it is within
	/// <see cref="MostLean"/> of it, on the side it leans. Straight down picks a side by seed.
	/// </summary>
	static Vector2 Rising(Vector2 dir, int seed)
	{
		float lean = Mathf.Wrap(dir.Angle() + Mathf.Pi * 0.5f, -Mathf.Pi, Mathf.Pi);
		if (Mathf.Abs(lean) > MostLean)
		{
			bool down = Mathf.Pi - Mathf.Abs(lean) < 0.02f;
			lean = (down ? (seed % 2 == 0 ? 1.0f : -1.0f) : Mathf.Sign(lean)) * MostLean;
		}
		return Vector2.Up.Rotated(lean);
	}

	/// <summary>
	/// A lighter's flame - far bigger than any lighter has a right to make - rising from
	/// <paramref name="at"/> <paramref name="height"/> tall, whichever way the lighter is held.
	/// Flambe's. It is drawn here rather than on the lighter, which turns with his hand.
	/// </summary>
	public static void Torch(CanvasItem canvas, Vector2 at, float height, int clock, float alpha = 1.0f)
	{
		Glow(canvas, at + Vector2.Up * height * 0.3f, height * 0.36f, 0.5f * alpha);
		Tongue(canvas, at + Vector2.Left * height * 0.1f, Vector2.Up.Rotated(-0.38f), height * 0.6f, height * 0.4f,
			-0.25f, clock, 3, alpha);
		Tongue(canvas, at + Vector2.Right * height * 0.1f, Vector2.Up.Rotated(0.38f), height * 0.58f, height * 0.4f,
			0.25f, clock, 4, alpha);
		Tongue(canvas, at, Vector2.Up, height, height * 0.55f, 0.12f, clock, 1, alpha);
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

	// --- Fire on the move ----------------------------------------------------

	/// <summary>The very middle of a fire on the move: hotter than yellow, nearly white.</summary>
	static readonly Color WhiteHot = new Color(1.0f, 0.97f, 0.84f);

	/// <summary>
	/// How far a trailing tongue curls up by its tip, as a fraction of its length. A fire on the
	/// move streams straight back from the way it is going - and rises as it trails, which is what
	/// still makes it fire rather than a jet (Eric, 2026-10-20).
	/// </summary>
	const float TrailLift = 0.24f;

	/// <summary>
	/// The furthest a fire on the move trails below level, in radians (30 degrees). Going up, it
	/// streams out behind and to the side; it never pours straight down like a rocket's exhaust.
	/// </summary>
	const float MostDroop = 0.52f;

	/// <summary>
	/// <paramref name="back"/>, the way a fire on the move trails, raised until it droops no
	/// further than <see cref="MostDroop"/> below level. Rising straight up, it burns upward.
	/// </summary>
	static Vector2 Trailing(Vector2 back)
	{
		if (back.LengthSquared() < 0.0001f) return Vector2.Up;
		back = back.Normalized();
		if (back.Y <= Mathf.Sin(MostDroop)) return back;
		if (Mathf.Abs(back.X) < 0.1f) return Vector2.Up;
		return new Vector2(Mathf.Sign(back.X) * Mathf.Cos(MostDroop), Mathf.Sin(MostDroop));
	}

	/// <summary>
	/// The three colours of a fire, outside in, with how much of a tongue's length and width each
	/// one fills.
	/// </summary>
	static readonly (Color colour, float reach, float width, float alpha)[] Layers =
	{
		(Outer, 1.0f, 1.0f, 1.0f),
		(Middle, 0.74f, 0.64f, 1.0f),
		(Heart, 0.46f, 0.34f, 1.0f),
	};

	/// <summary>
	/// A ball of fire on the move, drawn like a comet: a white-hot heart in a yellow and orange
	/// ball with a red edge, and curved tongues streaming straight back off it the way it is not
	/// going - <paramref name="back"/>, at whatever angle it flies - longest dead behind and
	/// shorter round the sides. Each tongue licks: a ripple runs down it from root to tip and it
	/// curls up as it trails. Embers shed off the tail. Ball and tongues are laid down colour by
	/// colour - every red, then every orange, then every yellow - so they run together into one
	/// fire with one hot middle rather than a ball with flames stuck on. Never spun with anything:
	/// the flames always trail the way it has come (Eric, 2026-10-20) - though going up, they
	/// stream out sideways rather than pour down (<see cref="MostDroop"/>). A fireball, a burning
	/// drop, a fireball roll. <paramref name="seed"/> keeps two fires side by side out of step.
	/// </summary>
	public static void Comet(CanvasItem canvas, Vector2 centre, float radius, Vector2 back, int clock,
		float alpha = 1.0f, int seed = 0)
	{
		if (alpha <= 0.01f || radius < 1.0f) return;
		back = Trailing(back);
		// Seven tongues fanned round the trailing half of the ball, drawn outermost first so the
		// long one dead behind lies over its neighbours.
		const int Licks = 7;
		int[] order = { 0, 6, 1, 5, 2, 4, 3 };
		var root = new Vector2[Licks];
		var dir = new Vector2[Licks];
		var length = new float[Licks];
		var width = new float[Licks];
		var bend = new float[Licks];
		var phase = new float[Licks];
		for (int i = 0; i < Licks; i++)
		{
			float a = i / (float)(Licks - 1) * 2.0f - 1.0f;
			float side = Mathf.Abs(a);
			float flicker = 1.0f + 0.12f * Mathf.Sin(clock * 0.43f + seed + i * 2.3f)
				+ 0.06f * Mathf.Sin(clock * 1.1f + i * 4.1f) + 0.06f * CrayonBrush.Noise(clock / 3, seed * 11 + i);
			root[i] = centre + back.Rotated(a * 1.15f) * radius * 0.45f;
			dir[i] = back.Rotated(a * 0.32f + 0.06f * Mathf.Sin(clock * 0.21f + i * 1.3f + seed));
			length[i] = radius * (1.7f + 1.9f * Mathf.Pow(1.0f - side, 1.2f)) * flicker;
			width[i] = radius * (1.3f - 0.45f * side);
			// Each side's tongues curl away from the middle, on their own side.
			bend[i] = -Mathf.Sign(a) * side * 0.16f;
			phase[i] = clock * 0.55f + i * 1.9f + seed * 0.7f;
		}

		// Each colour's disc sits a little further forward than the one round it, so the ball is
		// hottest at its leading edge and cools into the tail.
		foreach (var (colour, reach, across, opacity) in Layers)
		{
			float shrink = across == 1.0f ? 1.0f : 0.25f + 0.75f * across;
			WobblyDisc(canvas, centre - back * radius * (1.0f - shrink) * 0.25f, radius * shrink, clock, seed,
				new Color(colour, opacity * alpha));
			foreach (int i in order)
			{
				LickLayer(canvas, root[i], dir[i], length[i], width[i] * across, bend[i], phase[i], reach,
					new Color(colour, opacity * alpha));
			}
		}
		// White hot, a little forward of the middle: a comet is hottest at its head.
		canvas.DrawCircle(centre - back * radius * 0.14f, radius * 0.3f, new Color(WhiteHot, alpha));

		Embers(canvas, centre, radius, back, clock, alpha, seed);
	}

	/// <summary>
	/// One tongue of a fire on the move, from <paramref name="root"/> streaming along
	/// <paramref name="dir"/> - back from the way it is going, sideways included, unlike
	/// <see cref="Tongue"/>, which a still fire keeps rising - curling to one side by
	/// <paramref name="bend"/>, rippling as it licks and rising as it trails. For anything burning
	/// that is going somewhere fast.
	/// </summary>
	public static void Lick(CanvasItem canvas, Vector2 root, Vector2 dir, float length, float width,
		float bend, int clock, int seed, float alpha = 1.0f)
	{
		if (alpha <= 0.01f || length < 2.0f) return;
		dir = Trailing(dir);
		float phase = clock * 0.55f + seed * 1.9f;
		float flicker = 1.0f + 0.12f * Mathf.Sin(clock * 0.43f + seed * 2.3f) + 0.06f * CrayonBrush.Noise(clock / 3, seed);
		foreach (var (colour, reach, across, opacity) in Layers)
		{
			LickLayer(canvas, root, dir, length * flicker, width * across, bend, phase, reach, new Color(colour, opacity * alpha));
		}
	}

	/// <summary>
	/// A point on a trailing tongue's spine, <paramref name="t"/> of the way from root to tip: on
	/// along its way, curled off to one side, rippled by a wave running down it, and lifted.
	/// </summary>
	static Vector2 Spine(Vector2 root, Vector2 dir, float length, float bend, float phase, float t)
	{
		float curl = bend * t * t + 0.11f * t * Mathf.Sin(phase - t * 4.5f);
		return root + dir * length * t + dir.Orthogonal() * curl * length + Vector2.Up * TrailLift * length * t * t;
	}

	/// <summary>
	/// One colour of a trailing tongue: the outline round the first <paramref name="reach"/> of
	/// its spine, swelling a little past the root and closing to a point, rounded off behind the
	/// root so neighbouring tongues run together.
	/// </summary>
	static void LickLayer(CanvasItem canvas, Vector2 root, Vector2 dir, float length, float width,
		float bend, float phase, float reach, Color colour)
	{
		const int Steps = 14;
		const int Round = 5;
		var points = new Vector2[Steps * 2 + Round];
		Vector2 rootNormal = Vector2.Zero;
		float rootHalf = 0.0f;
		for (int i = 0; i < Steps; i++)
		{
			float s = i / (float)(Steps - 1);
			float t = s * reach;
			Vector2 at = Spine(root, dir, length, bend, phase, t);
			Vector2 tangent = (Spine(root, dir, length, bend, phase, t + 0.01f) - Spine(root, dir, length, bend, phase, t - 0.01f)).Normalized();
			Vector2 normal = tangent.Orthogonal();
			float half = width * 0.5f * Mathf.Pow(1.0f - s, 0.75f) * (0.85f + 0.4f * Mathf.Sin(Mathf.Pi * s));
			points[i] = at + normal * half;
			points[Steps * 2 - 1 - i] = at - normal * half;
			if (i == 0)
			{
				rootNormal = normal;
				rootHalf = half;
			}
		}
		for (int k = 1; k <= Round; k++)
		{
			points[Steps * 2 - 1 + k] = root - rootNormal.Rotated(Mathf.Pi * k / (Round + 1)) * rootHalf;
		}
		canvas.DrawColoredPolygon(points, colour);
	}

	/// <summary>
	/// A disc whose edge boils: two slow ripples running round it out of step, smooth from frame
	/// to frame, so a ball of fire is never a clean circle.
	/// </summary>
	static void WobblyDisc(CanvasItem canvas, Vector2 centre, float radius, int clock, int seed, Color colour)
	{
		const int Points = 28;
		var points = new Vector2[Points];
		for (int i = 0; i < Points; i++)
		{
			float a = Mathf.Tau * i / Points;
			float r = radius * (1.0f + 0.05f * Mathf.Sin(a * 5.0f + clock * 0.45f + seed)
				+ 0.03f * Mathf.Sin(a * 3.0f - clock * 0.3f + seed * 2.0f));
			points[i] = centre + Vector2.Right.Rotated(a) * r;
		}
		canvas.DrawColoredPolygon(points, colour);
	}

	/// <summary>
	/// Sparks shed off a fire on the move: each one thrown off the tail, drifting back and up as
	/// it cools from yellow to red and goes out, then thrown again somewhere new. Hashed from the
	/// clock, never random.
	/// </summary>
	static void Embers(CanvasItem canvas, Vector2 centre, float radius, Vector2 back, int clock, float alpha, int seed)
	{
		const int Count = 6;
		Vector2 across = back.Orthogonal();
		for (int i = 0; i < Count; i++)
		{
			int period = 14 + (i % 3) * 5;
			int local = clock + i * 7 + seed * 3;
			int cycle = local / period;
			float t = (local % period) / (float)period;
			float n1 = CrayonBrush.Noise(cycle, seed * 13 + i + 40);
			float n2 = CrayonBrush.Noise(cycle, seed * 13 + i + 90);
			Vector2 at = centre + back.Rotated(n1 * 0.45f) * radius * (1.3f + 2.8f * t) * (0.9f + 0.2f * n2)
				+ across * Mathf.Sin(t * 6.0f + i) * radius * 0.15f
				+ Vector2.Up * radius * 0.9f * t * t;
			float size = Mathf.Max(1.5f, radius * 0.1f * (1.1f + 0.4f * n2) * (1.0f - 0.6f * t));
			float fade = alpha * (1.0f - t);
			canvas.DrawCircle(at, size, new Color(Heart.Lerp(Outer, t), fade));
			canvas.DrawCircle(at, size * 0.5f, new Color(WhiteHot, 0.8f * fade));
		}
	}
}
