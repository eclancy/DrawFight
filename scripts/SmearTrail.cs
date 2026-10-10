using Godot;

/// <summary>
/// The smear a strike leaves - the animator's smear frame. The hand, foot or weapon that hits is
/// traced back through the last few frames of its swing and stretched on out past itself to the
/// far side of what it hits, so a hit can reach further than the drawn limb does and still read
/// as the limb doing it, and a big hit looks big: the stronger the move, the wider, longer and
/// bolder the smear, with speed lines and, for the strongest, a shock of air off its leading edge.
/// Eric, 2026-10-10: the hit zones should go beyond the limited reach of the limbs, with smears
/// more dramatic with the power of the attack to show the range and the power.
///
/// No outline, and nothing near black: an outline means something real that can be stood on or
/// that hits (see .ai/art-direction.md), and the smear is motion, not a thing. Every wobble is
/// hashed from a frame count, never random.
/// </summary>
public sealed class SmearTrail
{
	/// <summary>Front hand, back hand, front foot, back foot, and the head of what he holds.</summary>
	const int Ends = 5;
	const int Kept = 12;

	/// <summary>The last frames of a windup, where the swing comes through to the hit.</summary>
	const int StrikeFrames = 4;

	readonly Vector2[,] path = new Vector2[Ends, Kept];
	readonly bool[,] seen = new bool[Ends, Kept];
	/// <summary>The move frame each slot was recorded on.</summary>
	readonly int[] frameOf = new int[Kept];
	int newest = -1;
	int recorded;
	int striker = -1;
	/// <summary>The striker is settled: picked on the first frame the hit was live.</summary>
	bool locked;

	public void Clear()
	{
		newest = -1;
		recorded = 0;
		striker = -1;
		locked = false;
	}

	/// <summary>
	/// Where each end that could strike is on <paramref name="moveFrame"/>, in
	/// <paramref name="space"/>'s coordinates.
	/// </summary>
	public void Record(FighterRig rig, Node2D space, int moveFrame)
	{
		newest = (newest + 1) % Kept;
		recorded = Mathf.Min(recorded + 1, Kept);
		frameOf[newest] = moveFrame;
		Transform2D to = space.GetGlobalTransform().AffineInverse();
		(Vector2 front, Vector2 back)? hands = rig.HandsGlobal();
		Put(0, hands?.front, to);
		Put(1, hands?.back, to);
		Put(2, rig.Sole(RigBone.LegFrontLower)?.sole, to);
		Put(3, rig.Sole(RigBone.LegBackLower)?.sole, to);
		Put(4, rig.PropHeadGlobal(), to);
	}

	void Put(int end, Vector2? at, Transform2D to)
	{
		seen[end, newest] = at.HasValue;
		if (at.HasValue) path[end, newest] = to * at.Value;
	}

	/// <summary>
	/// How strong a move is, 0 to 1, for how dramatic its smear is and how far it reaches: a jab is
	/// nothing, a tilt about half, a smash all of it. Charged, it grows with the charge.
	/// </summary>
	public static float Power(MoveData move, float charge = 1.0f) =>
		Mathf.Clamp((move.Damage * charge - 2.5f) / 11.5f, 0.0f, 1.0f);

	/// <summary>
	/// Whether a move is smeared and reaches past its hitbox: the normal attacks, swung with the
	/// body. Not a special - its effect is its own - and not a wheel of fire round him.
	/// </summary>
	public static bool Smears(MoveData move) =>
		move != null && move.Special == SpecialKind.None && move.HitboxRadius > 0.0f && !move.Spin
		&& move.ActiveFx != ActiveFx.FireRing;

	/// <summary>
	/// How strongly the smear shows at <paramref name="moveFrame"/>, 0 when it does not: coming in
	/// on the last frames of the windup as the swing comes through, solid on the first frame it
	/// hits, then dying away - longer after a stronger hit. <paramref name="reaching"/> is whether
	/// it stretches out to the hit by now.
	/// </summary>
	public static float Strength(MoveData move, int moveFrame, float power, out bool reaching)
	{
		int start = move.StartupFrames;
		int active = move.ActiveFrames;
		int linger = 3 + Mathf.RoundToInt(4.0f * power);
		reaching = moveFrame > start;
		if (moveFrame < start - 1 || moveFrame > start + active + linger) return 0.0f;
		if (moveFrame <= start) return 0.45f + 0.2f * (moveFrame - start + 1);
		int since = moveFrame - start - 1;
		if (since < active) return 1.0f - 0.35f * since / Mathf.Max(1, active);
		return 0.65f * (1.0f - (since - active + 1) / (float)(linger + 1));
	}

	/// <summary>How far past its hitbox a move reaches along the way it hits: more for a stronger hit.</summary>
	public static float Reach(MoveData move, float charge = 1.0f) =>
		Smears(move) ? move.HitboxRadius * (0.3f + 0.6f * Power(move, charge)) : 0.0f;

	/// <summary>
	/// The hit, carried <paramref name="reach"/> further out: the capsule's far end pushed on along
	/// the way it points (from its root, or from the middle of the body when the hitbox sits on
	/// it), or, for a hitbox centred on him, the circle grown all round.
	/// </summary>
	public static (Vector2 tip, float radius) Extend(Vector2 root, Vector2 centre, float radius, float reach, Vector2 middle)
	{
		if (reach <= 0.0f) return (centre, radius);
		Vector2 along = centre - root;
		if (along.Length() < radius * 0.5f) along = centre - middle;
		if (along.Length() < radius * 0.5f) return (centre, radius + reach * 0.5f);
		return (centre + along.Normalized() * reach, radius);
	}

	/// <summary>
	/// Draws the smear on <paramref name="canvas"/>, in the space it was recorded in.
	/// <paramref name="hit"/> is where the hitbox is, <paramref name="tip"/> where its reach ends,
	/// <paramref name="radius"/> its size; <paramref name="pivot"/> is the middle of the body that
	/// a swing goes round. <paramref name="strength"/> fades it in and out;
	/// <paramref name="reaching"/> is whether the hit is live, which is when the smear stretches out
	/// to the hit rather than only trailing the swing.
	/// </summary>
	public void Draw(CanvasItem canvas, MoveData move, Vector2 hit, Vector2 tip, float radius, Vector2 pivot,
		float power, float strength, bool reaching, Color colour, int clock)
	{
		if (recorded < 2 || strength <= 0.01f) return;
		// Coming through, the striking end is the one moving fastest; once the hit is live, it is
		// the one at the hit, and that is the end that keeps the smear.
		if (!locked)
		{
			striker = reaching ? Nearest(hit) : Fastest();
			locked = reaching;
		}
		if (striker < 0) return;

		// The swing: the striking end from the last frames of the windup - where it comes through
		// to the hit, not the whole wind back - newest last, without the frames it held still.
		int from = move.StartupFrames - StrikeFrames;
		var points = new System.Collections.Generic.List<Vector2>(Kept * 4 + 4);
		for (int k = recorded - 1; k >= 0; k--)
		{
			int slot = (newest - k + Kept) % Kept;
			if (!seen[striker, slot] || frameOf[slot] < from) continue;
			Vector2 p = path[striker, slot];
			if (points.Count == 0) points.Add(p);
			else if (points[points.Count - 1].DistanceTo(p) > 2.0f) Round(points, points[points.Count - 1], p, pivot);
		}
		if (points.Count == 0) return;
		Vector2 end = points[points.Count - 1];
		int swing = points.Count;

		// The reach: on out from the end of the limb, through the hit, to its far side.
		Vector2 outward = tip - hit;
		Vector2 dir = outward.LengthSquared() > 1.0f ? outward.Normalized()
			: (hit - end).LengthSquared() > 1.0f ? (hit - end).Normalized() : Vector2.Right;
		Vector2 far = tip + dir * radius * 0.7f;
		if (reaching)
		{
			for (int s = 1; s <= 4; s++) points.Add(end.Lerp(far, s / 4.0f));
		}
		if (points.Count < 2) return;

		float wide = radius * (0.4f + 0.6f * power);
		float swell = radius * (0.85f + 0.55f * power);
		var widths = new float[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			if (i < swing)
			{
				// Thin at the oldest frame, as wide as the limb's sweep by now.
				float t = swing <= 1 ? 1.0f : i / (float)(swing - 1);
				widths[i] = wide * Mathf.Pow(t, 0.8f);
			}
			else
			{
				// Swelling out round the hit, then closing to a rounded point past it.
				float s = (i - swing + 1) / 4.0f;
				widths[i] = s < 0.6f ? Mathf.Lerp(wide, swell, Mathf.SmoothStep(0.0f, 1.0f, s / 0.6f))
					: swell * Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - Mathf.Pow((s - 0.6f) / 0.4f, 2.0f)));
			}
		}

		var paper = new Color(1.0f, 0.98f, 0.92f);
		Color edge = colour;
		Color core = colour.Lerp(paper, 0.6f);
		float alpha = (0.5f + 0.3f * power) * strength;

		// Speed lines through the hit, along the way it goes: more of them for a harder hit.
		int lines = reaching ? 1 + Mathf.RoundToInt(3.0f * power) : 0;
		Vector2 side = dir.Orthogonal();
		for (int i = 0; i < lines; i++)
		{
			float across = lines > 1 ? (i / (float)(lines - 1) - 0.5f) * 2.0f : 0.0f;
			float jitter = CrayonBrush.Noise(clock / 3, i * 7 + 3);
			Vector2 start = hit - dir * radius * (0.3f + 0.3f * Mathf.Abs(jitter)) + side * across * swell * 0.45f;
			Vector2 stop = far + dir * radius * (0.15f + 0.25f * power) * (0.7f + 0.3f * jitter) + side * across * swell * 0.3f;
			canvas.DrawLine(start, stop, new Color(edge, 0.55f * strength), 2.0f + 2.0f * power, true);
		}

		Strip(canvas, points, widths, 1.0f, new Color(edge, alpha));
		Strip(canvas, points, widths, 0.5f, new Color(core, Mathf.Min(1.0f, alpha * 1.3f)));

		// The strong hits push a shock of air off their leading edge; the strongest, two.
		if (reaching && power > 0.45f)
		{
			float bow = 0.4f + 0.3f * power;
			float a = dir.Angle();
			float r = (far - hit).Length() * 0.95f;
			canvas.DrawArc(hit, r, a - bow, a + bow, 14, new Color(edge, 0.7f * strength), 2.5f + 4.0f * power, true);
			if (power > 0.75f)
			{
				canvas.DrawArc(hit, r * 1.18f, a - bow * 0.7f, a + bow * 0.7f, 10, new Color(edge, 0.45f * strength), 2.0f + 2.0f * power, true);
			}
		}
	}

	/// <summary>
	/// The way from <paramref name="a"/> to <paramref name="b"/> as a swing goes - round
	/// <paramref name="pivot"/>, turning and reaching out or in as it goes - added to
	/// <paramref name="points"/> in steps, not counting <paramref name="a"/>. A hammer brought down
	/// from over his head in two frames sweeps an arc through the air, not a straight line.
	/// </summary>
	static void Round(System.Collections.Generic.List<Vector2> points, Vector2 a, Vector2 b, Vector2 pivot)
	{
		Vector2 fromA = a - pivot, fromB = b - pivot;
		float ra = fromA.Length(), rb = fromB.Length();
		float turn = ra < 8.0f || rb < 8.0f ? 0.0f : Mathf.Wrap(fromB.Angle() - fromA.Angle(), -Mathf.Pi, Mathf.Pi);
		int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(turn) / 0.15f), 1, 12);
		for (int i = 1; i <= steps; i++)
		{
			float t = i / (float)steps;
			points.Add(turn == 0.0f ? a.Lerp(b, t)
				: pivot + Vector2.Right.Rotated(fromA.Angle() + turn * t) * Mathf.Lerp(ra, rb, t));
		}
	}

	/// <summary>The end that moved furthest over the last few frames: the one swinging.</summary>
	int Fastest()
	{
		int best = -1;
		float bestDistance = -1.0f;
		int back = (newest - Mathf.Min(3, recorded - 1) + Kept) % Kept;
		for (int e = 0; e < Ends; e++)
		{
			if (!seen[e, newest] || !seen[e, back]) continue;
			float d = path[e, newest].DistanceSquaredTo(path[e, back]);
			if (d > bestDistance)
			{
				bestDistance = d;
				best = e;
			}
		}
		return best;
	}

	/// <summary>The end nearest <paramref name="to"/> in the newest frame: the one that struck.</summary>
	int Nearest(Vector2 to)
	{
		int best = -1;
		float bestDistance = float.MaxValue;
		for (int e = 0; e < Ends; e++)
		{
			if (!seen[e, newest]) continue;
			float d = path[e, newest].DistanceSquaredTo(to);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = e;
			}
		}
		return best;
	}

	/// <summary>
	/// A ribbon along <paramref name="points"/>, each point <paramref name="scale"/> of its width
	/// across, solid down the middle and fading to its edges, so it reads as a blur of motion.
	/// Drawn as plain triangles, which take any bend a swing makes without having to be a
	/// polygon that does not cross itself.
	/// </summary>
	static void Strip(CanvasItem canvas, System.Collections.Generic.List<Vector2> points, float[] widths, float scale, Color colour)
	{
		int n = points.Count;
		var verts = new Vector2[n * 3];
		var colours = new Color[n * 3];
		var clear = new Color(colour, colour.A * 0.25f);
		for (int i = 0; i < n; i++)
		{
			Vector2 along = (points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)]);
			Vector2 normal = along.LengthSquared() > 0.01f ? along.Normalized().Orthogonal() : Vector2.Up;
			float half = widths[i] * 0.5f * scale;
			verts[i * 3] = points[i] + normal * half;
			verts[i * 3 + 1] = points[i];
			verts[i * 3 + 2] = points[i] - normal * half;
			colours[i * 3] = clear;
			colours[i * 3 + 1] = colour;
			colours[i * 3 + 2] = clear;
		}
		var indices = new int[(n - 1) * 12];
		int k = 0;
		for (int i = 0; i < n - 1; i++)
		{
			int a = i * 3, b = (i + 1) * 3;
			// Two quads a segment: the left edge to the middle, and the middle to the right edge.
			foreach ((int x, int y) in new[] { (0, 1), (1, 2) })
			{
				indices[k++] = a + x; indices[k++] = a + y; indices[k++] = b + x;
				indices[k++] = a + y; indices[k++] = b + y; indices[k++] = b + x;
			}
		}
		RenderingServer.CanvasItemAddTriangleArray(canvas.GetCanvasItem(), indices, verts, colours);
	}
}
