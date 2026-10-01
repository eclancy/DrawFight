using Godot;

/// <summary>
/// Drawing helpers that make code-drawn geometry look like it was made with a biro and a box
/// of crayons: wobbly ink outlines and hatched fills that overshoot the line.
///
/// Every wobble is derived from a seed by hash, never from a random number generator. A stage
/// redraws whenever the window resizes or the debug view toggles, and a shape that re-rolls its
/// jitter on each redraw shimmers - which reads as a rendering bug, not as hand-drawn.
/// </summary>
public static class CrayonBrush
{
	/// <summary>Deterministic value noise in roughly -1..1. Same inputs, same output, always.</summary>
	public static float Noise(int seed, int index)
	{
		int n = (seed * 73856093) ^ (index * 19349663);
		n = (n << 13) ^ n;
		int m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
		return 1.0f - m / 1073741824.0f;
	}

	/// <summary>
	/// A rectangle outline drawn as four hand-wobbled strokes that overshoot at the corners,
	/// the way a pen does when it does not stop exactly on the join.
	/// </summary>
	public static void InkRect(CanvasItem canvas, Rect2 rect, Color ink, float width, int seed,
		float wobble = 2.2f, bool dashed = false)
	{
		Vector2 tl = rect.Position;
		Vector2 tr = new Vector2(rect.End.X, rect.Position.Y);
		Vector2 br = rect.End;
		Vector2 bl = new Vector2(rect.Position.X, rect.End.Y);

		InkLine(canvas, tl, tr, ink, width, seed + 1, wobble, dashed);
		InkLine(canvas, tr, br, ink, width, seed + 2, wobble, dashed);
		InkLine(canvas, br, bl, ink, width, seed + 3, wobble, dashed);
		InkLine(canvas, bl, tl, ink, width, seed + 4, wobble, dashed);
	}

	/// <summary>A single wobbled stroke. Dashed strokes mark platforms you can fall through.</summary>
	public static void InkLine(CanvasItem canvas, Vector2 from, Vector2 to, Color ink, float width,
		int seed, float wobble = 2.2f, bool dashed = false)
	{
		float length = from.DistanceTo(to);
		if (length < 0.5f) return;

		int segments = Mathf.Max(2, Mathf.RoundToInt(length / 26.0f));
		Vector2 normal = (to - from).Normalized().Orthogonal();

		if (!dashed)
		{
			var points = new Vector2[segments + 1];
			for (int i = 0; i <= segments; i++)
			{
				float t = i / (float)segments;
				float push = Noise(seed, i) * wobble;
				// Ends wobble less, so corners still look like corners.
				float taper = Mathf.Sin(t * Mathf.Pi);
				points[i] = from.Lerp(to, t) + normal * push * (0.35f + taper * 0.65f);
			}
			canvas.DrawPolyline(points, ink, width, true);
			return;
		}

		for (int i = 0; i < segments; i += 2)
		{
			float t0 = i / (float)segments;
			float t1 = Mathf.Min(1.0f, (i + 1) / (float)segments);
			Vector2 a = from.Lerp(to, t0) + normal * Noise(seed, i) * wobble;
			Vector2 b = from.Lerp(to, t1) + normal * Noise(seed, i + 1) * wobble;
			canvas.DrawLine(a, b, ink, width, true);
		}
	}

	/// <summary>
	/// Diagonal crayon hatching across a rectangle, deliberately running past its edges. The
	/// overshoot is the whole point: colouring outside the lines is the single detail that
	/// reads as a child did this rather than a computer.
	/// </summary>
	public static void CrayonFill(CanvasItem canvas, Rect2 rect, Color color, int seed,
		float spacing = 11.0f, float overshoot = 7.0f, float width = 7.0f)
	{
		Rect2 area = rect.Grow(overshoot);
		float span = area.Size.X + area.Size.Y;
		int strokes = Mathf.Max(1, Mathf.RoundToInt(span / spacing));

		for (int i = 0; i < strokes; i++)
		{
			// Sweep a 45-degree line across the box by walking its diagonal extent.
			float offset = i * spacing;
			Vector2 start = new Vector2(area.Position.X + offset, area.Position.Y);
			Vector2 end = new Vector2(area.Position.X + offset - area.Size.Y, area.End.Y);

			// Clip to the padded box so strokes stay near the shape.
			start.X = Mathf.Clamp(start.X, area.Position.X, area.End.X);
			end.X = Mathf.Clamp(end.X, area.Position.X, area.End.X);
			if (Mathf.Abs(start.X - end.X) < 0.5f && start.X <= area.Position.X) continue;

			Color c = color;
			// Uneven pressure, the way a crayon actually lays down.
			c.A *= 0.62f + 0.38f * Mathf.Abs(Noise(seed, i));

			Vector2 jitter = new Vector2(Noise(seed + 7, i) * 2.4f, Noise(seed + 11, i) * 2.0f);
			canvas.DrawLine(start + jitter, end + jitter, c, width, true);
		}
	}

	/// <summary>
	/// Coloured pencil across a rectangle: many thin close strokes at a steep angle, uneven in
	/// pressure, with paper showing between them - and a lighter cross-hatch over the top. Finer
	/// and tighter to the edge than a crayon fill; a pencil goes roughly where it is put.
	/// </summary>
	public static void PencilFill(CanvasItem canvas, Rect2 rect, Color color, int seed,
		float spacing = 5.0f, float width = 2.2f)
	{
		PencilHatch(canvas, rect, color, seed, spacing, width, 2.2f);
		Color light = color;
		light.A *= 0.45f;
		PencilHatch(canvas, rect, light, seed + 5, spacing * 1.8f, width * 0.8f, -0.9f);
	}

	/// <summary>One direction of pencil strokes; <paramref name="slope"/> is x travelled per unit of y.</summary>
	static void PencilHatch(CanvasItem canvas, Rect2 rect, Color color, int seed, float spacing, float width, float slope)
	{
		Rect2 area = rect.Grow(2.0f);
		float run = area.Size.Y * slope;
		float startX = area.Position.X + Mathf.Min(0.0f, run);
		float endX = area.End.X + Mathf.Max(0.0f, run);
		int strokes = Mathf.Max(1, Mathf.RoundToInt((endX - startX) / spacing));

		for (int i = 0; i < strokes; i++)
		{
			float x = startX + i * spacing + Noise(seed, i) * spacing * 0.35f;
			var a = new Vector2(x, area.Position.Y);
			var b = new Vector2(x - run, area.End.Y);

			// Clip the stroke to the rectangle, so the fill stays inside the lines.
			if (!ClipToRect(ref a, ref b, area)) continue;

			Color c = color;
			c.A *= 0.45f + 0.55f * Mathf.Abs(Noise(seed + 3, i));
			canvas.DrawLine(a, b, c, width, true);
		}
	}

	/// <summary>Cuts a segment down to the part inside a rectangle. False if none of it is.</summary>
	static bool ClipToRect(ref Vector2 a, ref Vector2 b, Rect2 r)
	{
		float t0 = 0.0f, t1 = 1.0f;
		Vector2 d = b - a;
		float[] p = { -d.X, d.X, -d.Y, d.Y };
		float[] q = { a.X - r.Position.X, r.End.X - a.X, a.Y - r.Position.Y, r.End.Y - a.Y };
		for (int i = 0; i < 4; i++)
		{
			if (Mathf.IsZeroApprox(p[i]))
			{
				if (q[i] < 0.0f) return false;
				continue;
			}
			float t = q[i] / p[i];
			if (p[i] < 0.0f) t0 = Mathf.Max(t0, t);
			else t1 = Mathf.Min(t1, t);
		}
		if (t0 > t1) return false;
		Vector2 start = a;
		a = start + d * t0;
		b = start + d * t1;
		return true;
	}

	/// <summary>
	/// A pencil outline: two light passes over the same wobbled path, the way a line is gone
	/// over twice with a coloured pencil to make it darker.
	/// </summary>
	public static void PencilRect(CanvasItem canvas, Rect2 rect, Color color, float width, int seed, bool dashed = false)
	{
		Color first = color;
		first.A *= 0.85f;
		InkRect(canvas, rect, first, width, seed, 1.6f, dashed);
		Color second = color;
		second.A *= 0.55f;
		InkRect(canvas, rect.Grow(0.8f), second, width * 0.6f, seed + 17, 2.2f, dashed);
	}

	/// <summary>A loose scribbled blob, for clouds, bushes and smoke.</summary>
	public static void Scribble(CanvasItem canvas, Vector2 centre, Vector2 size, Color color,
		int seed, float width = 8.0f, int loops = 9)
	{
		var points = new Vector2[loops * 4];
		for (int i = 0; i < points.Length; i++)
		{
			float t = i / (float)(points.Length - 1);
			float angle = t * Mathf.Tau * loops * 0.22f;
			float radius = 0.42f + 0.28f * Mathf.Abs(Noise(seed, i));
			points[i] = centre + new Vector2(
				Mathf.Cos(angle) * size.X * radius,
				Mathf.Sin(angle) * size.Y * radius);
		}
		canvas.DrawPolyline(points, color, width, true);
	}

	/// <summary>
	/// A vertical gradient, faked as bands because Godot cannot draw one directly. Used for
	/// every sky in the game.
	/// </summary>
	public static void SkyBands(CanvasItem canvas, Rect2 area, Color top, Color bottom, int bands = 44)
	{
		float step = area.Size.Y / bands;
		for (int i = 0; i < bands; i++)
		{
			float t = i / (float)(bands - 1);
			var band = new Rect2(
				area.Position.X, area.Position.Y + i * step,
				area.Size.X, step + 1.0f);
			canvas.DrawRect(band, top.Lerp(bottom, t));
		}
	}
}
