using System;
using System.Collections.Generic;
using Godot;

public enum PaintTool { Marker, Pencil, Bucket, Eraser, Picker, Move }

/// <summary>
/// The drawing screen's brushes, working straight on a layer's pixels.
///
/// A stroke is laid down the way ink and pencil are on paper. Within one stroke a pixel takes the
/// most any dab gave it, never the sum - so a marker line is even all the way along, however
/// slowly it was drawn, instead of darkening wherever the dabs overlap. The coloured pencil keeps
/// that too, except when the pencil goes back over a place it has already left: then it builds up,
/// darker each pass, the way scribbling over with a real pencil does.
///
/// The pencil's grain is fixed to the page, not the stroke - it is the paper's tooth - so going
/// over an area again fills in the same pattern rather than a new one, and the look matches the
/// coloured pencil the game draws its own art in (CrayonBrush.PencilFill: close strokes at a
/// slant, a lighter cross-hatch over them).
///
/// Nothing here smooths or straightens a line. Every dab goes exactly where the pen went: a
/// drawing tool for this game must never tidy up a kid's linework (CLAUDE.md, rule 1).
/// </summary>
public sealed class Painter
{
	const int W = Drawing.Width;
	const int H = Drawing.Height;

	static byte[] grain;

	/// <summary>Built on a worker the moment the drawing screen opens (Prewarm): it takes a second or two.</summary>
	static readonly Lazy<byte[]> colouring = new Lazy<byte[]>(BuildColouring);

	/// <summary>The paper's tooth, 0-255 per pixel: high where a pencil catches first.</summary>
	public static byte[] Grain => grain ??= BuildGrain();

	/// <summary>
	/// How much colour a coloured pencil pressed on hard leaves at each pixel, 0-255: the solid
	/// fill (BuildColouring).
	/// </summary>
	public static byte[] Colouring => colouring.Value;

	/// <summary>Starts building the solid fill's colouring in the background, so the first fill does not wait for it.</summary>
	public static void Prewarm() => System.Threading.Tasks.Task.Run(() => colouring.Value);

	readonly byte[] original = new byte[W * H * 4];
	readonly byte[] under = new byte[W * H * 4];
	readonly byte[] mask = new byte[W * H];
	readonly int[] lastDab = new int[W * H];
	int dabs;
	int firstDab;

	DrawingPage page;
	DrawingLayer layer;
	PaintTool tool;
	float r, g, b;
	float size;
	Vector2 last;
	float lastPressure;
	float travelled;
	Rect2I touched;
	bool painting;

	public bool Painting => painting;

	// --- Strokes --------------------------------------------------------------------------

	public void Begin(DrawingPage page, DrawingLayer layer, PaintTool tool, Color colour, float size, Vector2 at, float pressure)
	{
		this.page = page;
		this.layer = layer;
		this.tool = tool;
		this.size = size;
		r = colour.R;
		g = colour.G;
		b = colour.B;
		Buffer.BlockCopy(layer.Pixels, 0, original, 0, original.Length);
		Buffer.BlockCopy(layer.Pixels, 0, under, 0, under.Length);
		Array.Clear(mask);
		firstDab = dabs + 1;
		touched = new Rect2I();
		painting = true;
		last = at;
		lastPressure = pressure;
		travelled = 0.0f;
		Dab(at, pressure);
	}

	/// <summary>The pen moved: dabs all along the way from where it was, close enough to make a line.</summary>
	public void To(Vector2 at, float pressure)
	{
		if (!painting) return;
		float spacing = Mathf.Max(1.0f, size * 0.12f);
		float length = last.DistanceTo(at);
		if (length <= 0.0f) return;
		float distance = spacing - travelled;
		while (distance <= length)
		{
			float t = distance / length;
			Dab(last.Lerp(at, t), Mathf.Lerp(lastPressure, pressure, t));
			distance += spacing;
		}
		travelled = length - (distance - spacing);
		last = at;
		lastPressure = pressure;
	}

	/// <summary>The pen lifted. Returns the stroke as an undoable edit, or null if it drew nothing.</summary>
	public Edit End()
	{
		if (!painting) return null;
		painting = false;
		if (touched.Size.X <= 0 || touched.Size.Y <= 0) return null;
		return new PixelEdit(page, layer, touched, PixelEdit.Cut(original, touched), PixelEdit.Cut(layer.Pixels, touched));
	}

	void Dab(Vector2 centre, float pressure)
	{
		dabs++;
		float radius = size * 0.5f;
		if (tool == PaintTool.Marker) radius *= 0.6f + 0.4f * pressure;
		int x0 = Mathf.Max(0, (int)(centre.X - radius - 1.0f));
		int y0 = Mathf.Max(0, (int)(centre.Y - radius - 1.0f));
		int x1 = Mathf.Min(W - 1, (int)(centre.X + radius + 1.0f));
		int y1 = Mathf.Min(H - 1, (int)(centre.Y + radius + 1.0f));
		if (x1 < x0 || y1 < y0) return;

		byte[] tooth = Grain;
		byte[] pixels = layer.Pixels;
		// How many dabs the pencil can be away from a pixel and still be on the same pass over it.
		int samePass = Mathf.Max(4, Mathf.RoundToInt(0.9f * size / Mathf.Max(1.0f, size * 0.12f)));
		// Light pressure catches only the top of the paper's tooth; pressing harder fills more of it
		// in. A mouse presses as hard as a pen can, and still leaves paper showing - it is a
		// pencil, and going over it again is what fills it in.
		float threshold = 0.74f - 0.32f * Mathf.Clamp(pressure, 0.05f, 1.0f);

		for (int y = y0; y <= y1; y++)
		{
			float dy = y + 0.5f - centre.Y;
			for (int x = x0; x <= x1; x++)
			{
				float dx = x + 0.5f - centre.X;
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				float cover;
				int i = y * W + x;
				if (tool == PaintTool.Pencil)
				{
					float edge = Mathf.Clamp((radius - d) / (radius * 0.12f + 1.2f), 0.0f, 1.0f);
					if (edge <= 0.0f) continue;
					float tooth01 = tooth[i] / 255.0f;
					cover = Mathf.SmoothStep(threshold - 0.14f, threshold + 0.14f, tooth01) * edge * 0.88f;
				}
				else
				{
					cover = Mathf.Clamp(radius + 0.5f - d, 0.0f, 1.0f);
				}
				if (cover <= 0.004f) continue;

				// Back over a place the pencil had left: what is there now is what this pass
				// colours over, so it gets darker.
				if (tool == PaintTool.Pencil && lastDab[i] >= firstDab && dabs - lastDab[i] > samePass)
				{
					int p = i * 4;
					under[p] = pixels[p];
					under[p + 1] = pixels[p + 1];
					under[p + 2] = pixels[p + 2];
					under[p + 3] = pixels[p + 3];
					mask[i] = 0;
				}
				lastDab[i] = dabs;

				byte m = (byte)(cover * 255.0f);
				if (m <= mask[i]) continue;
				mask[i] = m;
				if (tool == PaintTool.Eraser) Erase(pixels, under, i * 4, m / 255.0f);
				else if (tool == PaintTool.Pencil) Lay(pixels, under, i * 4, r, g, b, m / 255.0f * Waxy(under, i * 4, r, g, b));
				else Lay(pixels, under, i * 4, r, g, b, m / 255.0f);
			}
		}

		var dab = new Rect2I(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
		touched = touched.Size.X <= 0 ? dab : touched.Merge(dab);
		layer.Touch(dab);
	}

	// --- Blending (straight alpha) ------------------------------------------------------------

	/// <summary>Colour laid over what is underneath, at alpha <paramref name="a"/>.</summary>
	static void Lay(byte[] into, byte[] from, int p, float r, float g, float b, float a)
	{
		float ba = from[p + 3] / 255.0f * (1.0f - a);
		float outA = a + ba;
		if (outA <= 0.0f)
		{
			into[p] = into[p + 1] = into[p + 2] = into[p + 3] = 0;
			return;
		}
		into[p] = (byte)((r * 255.0f * a + from[p] * ba) / outA + 0.5f);
		into[p + 1] = (byte)((g * 255.0f * a + from[p + 1] * ba) / outA + 0.5f);
		into[p + 2] = (byte)((b * 255.0f * a + from[p + 2] * ba) / outA + 0.5f);
		into[p + 3] = (byte)(outA * 255.0f + 0.5f);
	}

	/// <summary>
	/// A coloured pencil is wax: a light one hardly shows over something darker, so colouring in
	/// over a marker line leaves the line black. How much of the pencil gets through, 0.2 to 1.
	/// </summary>
	static float Waxy(byte[] under, int p, float r, float g, float b)
	{
		float a = under[p + 3] / 255.0f;
		if (a <= 0.0f) return 1.0f;
		float below = (0.3f * under[p] + 0.59f * under[p + 1] + 0.11f * under[p + 2]) / 255.0f * a + (1.0f - a);
		float lighter = 0.3f * r + 0.59f * g + 0.11f * b - below;
		return Mathf.Clamp(1.0f - lighter * 3.0f, 0.2f, 1.0f);
	}

	/// <summary>Colour slid in underneath what is there - a fill going behind the lines it fills.</summary>
	static void Behind(byte[] pixels, int p, float r, float g, float b, float a)
	{
		float ta = pixels[p + 3] / 255.0f;
		float ua = a * (1.0f - ta);
		float outA = ta + ua;
		if (outA <= 0.0f) return;
		pixels[p] = (byte)((pixels[p] * ta + r * 255.0f * ua) / outA + 0.5f);
		pixels[p + 1] = (byte)((pixels[p + 1] * ta + g * 255.0f * ua) / outA + 0.5f);
		pixels[p + 2] = (byte)((pixels[p + 2] * ta + b * 255.0f * ua) / outA + 0.5f);
		pixels[p + 3] = (byte)(outA * 255.0f + 0.5f);
	}

	static void Erase(byte[] into, byte[] from, int p, float a)
	{
		into[p] = from[p];
		into[p + 1] = from[p + 1];
		into[p + 2] = from[p + 2];
		into[p + 3] = (byte)(from[p + 3] * (1.0f - a) + 0.5f);
	}

	// --- Paint bucket ----------------------------------------------------------------------------

	/// <summary>How different a pixel can look from the one clicked and still be filled, out of 255.</summary>
	const int FillTolerance = 60;

	/// <summary>
	/// Fills the area round <paramref name="at"/>, as bounded by everything visible on the page
	/// (so lines on one layer hold in colour on another), into <paramref name="layer"/>. The fill
	/// reaches a couple of pixels under the lines that bound it, so no pale fringe is left between
	/// the colour and the line, and it goes behind anything already on the layer - so filling
	/// inside a line drawn on the same layer never paints over the line. Click on a line itself,
	/// though, and the line is recoloured. <paramref name="pencil"/> fills it lightly, the paper's
	/// tooth showing through; otherwise it is coloured in solid - but still in coloured pencil,
	/// pressed on, never flat colour (Eric, 2026-10-16).
	/// </summary>
	public static Edit Fill(DrawingPage page, DrawingLayer layer, Vector2I at, Color colour, bool pencil)
	{
		if (at.X < 0 || at.Y < 0 || at.X >= W || at.Y >= H) return null;
		byte[] seen = Drawing.Composite(page);
		int seed = at.Y * W + at.X;
		OnPaper(seen, seed, out int sr, out int sg, out int sb);

		var region = new byte[W * H];
		bool Match(int i)
		{
			if (region[i] != 0) return false;
			OnPaper(seen, i, out int pr, out int pg, out int pb);
			return Math.Abs(pr - sr) <= FillTolerance && Math.Abs(pg - sg) <= FillTolerance && Math.Abs(pb - sb) <= FillTolerance;
		}

		int minX = at.X, maxX = at.X, minY = at.Y, maxY = at.Y;
		var stack = new Stack<int>();
		stack.Push(seed);
		while (stack.Count > 0)
		{
			int i = stack.Pop();
			if (!Match(i)) continue;
			int y = i / W;
			int x = i - y * W;
			int left = x;
			while (left > 0 && Match(y * W + left - 1)) left--;
			int right = x;
			while (right < W - 1 && Match(y * W + right + 1)) right++;
			for (int k = left; k <= right; k++) region[y * W + k] = 1;
			minX = Math.Min(minX, left);
			maxX = Math.Max(maxX, right);
			minY = Math.Min(minY, y);
			maxY = Math.Max(maxY, y);
			for (int ny = y - 1; ny <= y + 1; ny += 2)
			{
				if (ny < 0 || ny >= H) continue;
				bool run = false;
				for (int k = left; k <= right; k++)
				{
					int j = ny * W + k;
					if (Match(j))
					{
						if (!run) stack.Push(j);
						run = true;
					}
					else
					{
						run = false;
					}
				}
			}
		}

		// Two pixels out, under the antialiased edge of the line.
		var rect = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1).Grow(2).Intersection(new Rect2I(0, 0, W, H));
		Grow(region, rect);
		Grow(region, rect);

		byte[] before = PixelEdit.Cut(layer.Pixels, rect);
		bool recolour = layer.Pixels[seed * 4 + 3] >= 128;
		byte[] tooth = pencil ? Grain : Colouring;
		float cr = colour.R, cg = colour.G, cb = colour.B;
		for (int y = rect.Position.Y; y < rect.End.Y; y++)
		{
			for (int x = rect.Position.X; x < rect.End.X; x++)
			{
				int i = y * W + x;
				if (region[i] == 0) continue;
				float a = pencil ? Mathf.SmoothStep(0.06f, 0.5f, tooth[i] / 255.0f) * 0.94f : tooth[i] / 255.0f;
				if (recolour) Lay(layer.Pixels, layer.Pixels, i * 4, cr, cg, cb, a);
				else Behind(layer.Pixels, i * 4, cr, cg, cb, a);
			}
		}
		layer.Touch(rect);
		return new PixelEdit(page, layer, rect, before, PixelEdit.Cut(layer.Pixels, rect));
	}

	/// <summary>A pixel as it looks on white paper.</summary>
	static void OnPaper(byte[] pixels, int i, out int r, out int g, out int b)
	{
		int p = i * 4;
		int a = pixels[p + 3];
		r = (pixels[p] * a + 255 * (255 - a)) / 255;
		g = (pixels[p + 1] * a + 255 * (255 - a)) / 255;
		b = (pixels[p + 2] * a + 255 * (255 - a)) / 255;
	}

	/// <summary>Grows a region by one pixel in every direction, within a rectangle.</summary>
	static void Grow(byte[] region, Rect2I rect)
	{
		var grown = new List<int>();
		for (int y = rect.Position.Y; y < rect.End.Y; y++)
		{
			for (int x = rect.Position.X; x < rect.End.X; x++)
			{
				int i = y * W + x;
				if (region[i] != 0) continue;
				bool near = (x > 0 && region[i - 1] == 1) || (x < W - 1 && region[i + 1] == 1)
					|| (y > 0 && region[i - W] == 1) || (y < H - 1 && region[i + W] == 1);
				if (near) grown.Add(i);
			}
		}
		foreach (int i in grown) region[i] = 2;
		for (int y = rect.Position.Y; y < rect.End.Y; y++)
		{
			for (int x = rect.Position.X; x < rect.End.X; x++)
			{
				int i = y * W + x;
				if (region[i] == 2) region[i] = 1;
			}
		}
	}

	// --- Other tools ------------------------------------------------------------------------------

	/// <summary>The colour that shows at a point of the page, or null where there is only paper.</summary>
	public static Color? Pick(DrawingPage page, Vector2I at)
	{
		if (at.X < 0 || at.Y < 0 || at.X >= W || at.Y >= H) return null;
		byte[] seen = Drawing.Composite(page);
		int p = (at.Y * W + at.X) * 4;
		if (seen[p + 3] < 40) return null;
		return new Color(seen[p] / 255.0f, seen[p + 1] / 255.0f, seen[p + 2] / 255.0f);
	}

	/// <summary>Moves everything on a layer by <paramref name="by"/>. Whatever goes off the page is gone.</summary>
	public static Edit Shift(DrawingPage page, DrawingLayer layer, Vector2I by)
	{
		if (by == Vector2I.Zero) return null;
		var all = new Rect2I(0, 0, W, H);
		byte[] before = (byte[])layer.Pixels.Clone();
		var moved = new byte[before.Length];
		for (int y = 0; y < H; y++)
		{
			int sy = y - by.Y;
			if (sy < 0 || sy >= H) continue;
			int from = Math.Max(0, -by.X), to = Math.Min(W, W - by.X);
			if (to <= from) continue;
			Buffer.BlockCopy(before, (sy * W + from) * 4, moved, (y * W + from + by.X) * 4, (to - from) * 4);
		}
		layer.Pixels = moved;
		layer.TouchAll();
		return new PixelEdit(page, layer, all, before, (byte[])moved.Clone());
	}

	// --- Coloured in solid ------------------------------------------------------------------------

	/// <summary>
	/// A page coloured in solid with a coloured pencil, pressed on: the same recipe the stand-ins
	/// and items are coloured in (pencil_coverage in tools/art/drawn.py), so a kid's solid fill and
	/// the game's own art are the same pencils. Not rows of strokes: pressed harder and softer in
	/// broad swells; coloured a patch at a time, the short strokes in each going one way and the
	/// way wandering from patch to patch; a faint mottle on the paper's grain; and a few specks
	/// where the pencil skipped the tooth. Hashed, so it is the same page every time.
	/// </summary>
	static byte[] BuildColouring()
	{
		var rnd = new Hash(20261016);
		float[] pressure = SoftNoise(ref rnd, 64, 210.0f / 255.0f, 1.0f);
		float[] grainy = SoftNoise(ref rnd, 6, 208.0f / 255.0f, 1.0f);
		float[] skips = SoftNoise(ref rnd, 5, 0.0f, 1.0f);

		// The strokes: each darkens what it crosses to its own pressure, the darkest one winning.
		var marks = new float[W * H];
		Array.Fill(marks, 1.0f);
		float slant = rnd.Range(25.0f, 60.0f);
		const float Spacing = 62.0f;
		for (int row = 0; row < H / Spacing + 2; row++)
		{
			for (int col = 0; col < W / Spacing + 2; col++)
			{
				float cx = (col - 0.5f + rnd.Range(-0.4f, 0.4f)) * Spacing;
				float cy = (row - 0.5f + rnd.Range(-0.4f, 0.4f)) * Spacing;
				float angle = slant + rnd.Range(-40.0f, 40.0f);
				int strokes = 22 + (int)rnd.Range(0.0f, 13.0f);
				for (int s = 0; s < strokes; s++)
				{
					float a = Mathf.DegToRad(angle + rnd.Range(-7.0f, 7.0f));
					var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					var across = new Vector2(-dir.Y, dir.X);
					var mid = new Vector2(cx, cy) + dir * rnd.Range(-Spacing * 0.6f, Spacing * 0.6f)
						+ across * rnd.Range(-Spacing * 0.6f, Spacing * 0.6f);
					float half = rnd.Range(10.0f, 24.0f);
					float width = rnd.Range(0.5f, 1.1f);
					float tone = rnd.Range(210.0f, 238.0f) / 255.0f;
					Stroke(marks, mid - dir * half, mid + dir * half, width, tone);
				}
			}
		}

		var cover = new byte[W * H];
		for (int i = 0; i < cover.Length; i++)
		{
			// Specks of bare paper: a few, small, soft-edged.
			float skip = skips[i];
			float tooth = skip < 18.0f / 255.0f ? Mathf.Lerp(0.647f, 0.894f, Mathf.SmoothStep(0.04f, 0.075f, skip))
				: Mathf.Lerp(0.894f, 1.0f, Mathf.SmoothStep(0.11f, 0.15f, skip));
			float c = pressure[i] * grainy[i] * marks[i] * tooth;
			// Never so light the colour washes out: colouring in, a kid presses on.
			cover[i] = (byte)Mathf.Clamp(50.0f + c * 205.0f, 0.0f, 255.0f);
		}
		return cover;
	}

	/// <summary>One short pencil stroke, soft at its edges, into <paramref name="marks"/>.</summary>
	static void Stroke(float[] marks, Vector2 from, Vector2 to, float halfWidth, float tone)
	{
		float reach = halfWidth + 1.5f;
		int x0 = Math.Max(0, (int)(Mathf.Min(from.X, to.X) - reach));
		int x1 = Math.Min(W - 1, (int)(Mathf.Max(from.X, to.X) + reach));
		int y0 = Math.Max(0, (int)(Mathf.Min(from.Y, to.Y) - reach));
		int y1 = Math.Min(H - 1, (int)(Mathf.Max(from.Y, to.Y) + reach));
		Vector2 along = to - from;
		float lengthSq = Mathf.Max(0.0001f, along.LengthSquared());
		for (int y = y0; y <= y1; y++)
		{
			for (int x = x0; x <= x1; x++)
			{
				var p = new Vector2(x + 0.5f, y + 0.5f);
				float t = Mathf.Clamp((p - from).Dot(along) / lengthSq, 0.0f, 1.0f);
				float d = p.DistanceTo(from + along * t);
				// A pencil stroke is soft-edged and thin: about as blurred as the art scripts' (drawn.py).
				float on = Mathf.Clamp(halfWidth + 0.5f - d, 0.0f, 1.0f) * 0.85f;
				if (on <= 0.0f) continue;
				int i = y * W + x;
				marks[i] = Mathf.Min(marks[i], Mathf.Lerp(1.0f, tone, on));
			}
		}
	}

	/// <summary>Smooth values from lo to hi changing over about <paramref name="cell"/> pixels: a hashed grid, eased between.</summary>
	static float[] SoftNoise(ref Hash rnd, int cell, float lo, float hi)
	{
		int gw = W / cell + 3, gh = H / cell + 3;
		var grid = new float[gw * gh];
		for (int i = 0; i < grid.Length; i++) grid[i] = rnd.Range(lo, hi);
		var values = new float[W * H];
		for (int y = 0; y < H; y++)
		{
			float gy = y / (float)cell;
			int iy = (int)gy;
			float ty = Mathf.SmoothStep(0.0f, 1.0f, gy - iy);
			for (int x = 0; x < W; x++)
			{
				float gx = x / (float)cell;
				int ix = (int)gx;
				float tx = Mathf.SmoothStep(0.0f, 1.0f, gx - ix);
				float top = Mathf.Lerp(grid[iy * gw + ix], grid[iy * gw + ix + 1], tx);
				float bottom = Mathf.Lerp(grid[(iy + 1) * gw + ix], grid[(iy + 1) * gw + ix + 1], tx);
				values[y * W + x] = Mathf.Lerp(top, bottom, ty);
			}
		}
		return values;
	}

	/// <summary>A run of hashed numbers - the same every time, so a page is never coloured two ways.</summary>
	struct Hash
	{
		uint state;

		public Hash(uint seed) => state = seed == 0 ? 1u : seed;

		public float Range(float lo, float hi)
		{
			state ^= state << 13;
			state ^= state >> 17;
			state ^= state << 5;
			return lo + (hi - lo) * (state / 4294967296.0f);
		}
	}

	// --- The paper's tooth -----------------------------------------------------------------------

	/// <summary>
	/// The grain a coloured pencil catches on: close strokes at a slant, uneven in pressure and
	/// broken along their length, with a lighter cross-hatch and a little fine noise - the same
	/// recipe as CrayonBrush.PencilFill, so a kid's colouring and the game's own art look like the
	/// same pencils. Hashed, so it is the same page every time.
	/// </summary>
	static byte[] BuildGrain()
	{
		var tooth = new byte[W * H];
		for (int y = 0; y < H; y++)
		{
			for (int x = 0; x < W; x++)
			{
				float s1 = (x + 2.2f * y) / 5.0f;
				int k1 = Mathf.FloorToInt(s1);
				float f1 = s1 - k1;
				int run1 = Mathf.FloorToInt((y - 0.45f * x) / 38.0f + CrayonBrush.Noise(k1, 5) * 0.8f);
				float stroke1 = (0.45f + 0.55f * Mathf.Abs(CrayonBrush.Noise(k1, run1))) * (1.0f - Mathf.Abs(f1 - 0.5f) * 1.3f);

				float s2 = (x - 0.9f * y) / 9.0f;
				int k2 = Mathf.FloorToInt(s2);
				float f2 = s2 - k2;
				int run2 = Mathf.FloorToInt((y + 1.1f * x) / 52.0f + CrayonBrush.Noise(k2, 9) * 0.8f);
				float stroke2 = (0.4f + 0.6f * Mathf.Abs(CrayonBrush.Noise(k2 + 7919, run2))) * (1.0f - Mathf.Abs(f2 - 0.5f) * 1.6f);

				float fine = 0.5f + 0.5f * CrayonBrush.Noise(x, y);
				float value = stroke1 * 0.62f + Mathf.Max(0.0f, stroke2) * 0.24f + fine * 0.22f;
				tooth[y * W + x] = (byte)(Mathf.Clamp(value, 0.0f, 1.0f) * 255.0f);
			}
		}
		return tooth;
	}
}
