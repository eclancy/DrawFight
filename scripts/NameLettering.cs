using System.Collections.Generic;
using System.Globalization;
using Godot;

/// <summary>
/// Draws a fighter's name in their own letters (<see cref="NameStyle"/>) onto any CanvasItem: the
/// one drawer for every name on every screen, so a fighter is lettered the same on the select
/// screen, over his percent and on a speech bubble.
///
/// Letters are drawn one at a time, each with its own transform - that is what lets them lean,
/// stretch, bob on their own beats, sit crooked or judder - and in passes, back to front, so one
/// letter's outline never covers the next one's colour: decorations behind, the paper halo every
/// name has (it is what keeps a name readable on any stage, .ai/art-direction.md), anything laid
/// under the word, the shadow, the coloured edge, the shading, the colour, and decorations in
/// front. There is only the one font; every style is made from it by FontVariation (embolden),
/// transforms and drawing.
/// </summary>
public static class NameLettering
{
	static readonly Color Paper = new Color(0.98f, 0.97f, 0.94f);
	/// <summary>What a hot DoomBot's letters glow toward, and the glow round them.</summary>
	static readonly Color Hot = new Color(1.0f, 0.40f, 0.18f);
	static readonly Color HotGlow = new Color(1.0f, 0.60f, 0.24f);

	// Fractions of the font size the fallback font's letters stand to.
	const float CapHeight = 0.72f;
	const float XHeight = 0.54f;

	static readonly Dictionary<float, Font> fonts = new Dictionary<float, Font>();

	static Font FontFor(NameStyle style)
	{
		Font plain = ThemeDB.FallbackFont;
		if (style.Embolden <= 0.0f) return plain;
		if (!fonts.TryGetValue(style.Embolden, out Font font))
		{
			font = new FontVariation { BaseFont = plain, VariationEmbolden = style.Embolden };
			fonts[style.Embolden] = font;
		}
		return font;
	}

	struct Letter
	{
		public string Text;
		/// <summary>Left of its cell, from where the letters start.</summary>
		public float X;
		/// <summary>Its cell's width, stretched.</summary>
		public float Width;
		/// <summary>The glyph's own advance, before stretching.</summary>
		public float Advance;
		/// <summary>How tall it stands - a capital or an ascender, or only x-height.</summary>
		public float Top;
	}

	/// <summary>Everything a name's layout needs, worked out once per draw.</summary>
	sealed class Laid
	{
		public Font Font;
		public int Px;
		public List<Letter> Letters;
		/// <summary>The letters, end to end.</summary>
		public float TextWidth;
		/// <summary>Room before the first letter for what trails behind (Triguy's speed lines, EdgeLord's hilt).</summary>
		public float Lead;
		/// <summary>Everything it draws, side to side.</summary>
		public float Extent;
	}

	static Laid Lay(string text, NameStyle style, int fontSize)
	{
		var laid = new Laid { Font = FontFor(style), Px = Mathf.Max(6, Mathf.RoundToInt(fontSize * style.SizeScale)) };
		if (style.Caps) text = text.ToUpperInvariant();
		Font font = laid.Font;
		int px = laid.Px;
		float cell = font.GetStringSize("O", HorizontalAlignment.Left, -1, px).X * style.Stretch;
		float gap = style.Spacing * px;
		float x = 0.0f;
		laid.Letters = new List<Letter>();
		TextElementEnumerator e = StringInfo.GetTextElementEnumerator(text ?? "");
		while (e.MoveNext())
		{
			string t = e.GetTextElement();
			float advance = font.GetStringSize(t, HorizontalAlignment.Left, -1, px).X;
			// A terminal's cells, but a thin letter or a dot in half of one, or Mk. II's "II" falls apart.
			float width = style.Mono ? (" .,'I1".IndexOf(t[0]) >= 0 ? cell * 0.55f : cell) : advance * style.Stretch;
			char c = t[0];
			bool tall = char.IsUpper(c) || char.IsDigit(c) || "bdfhklt".IndexOf(c) >= 0 || c > 127;
			laid.Letters.Add(new Letter { Text = t, X = x, Width = width, Advance = advance, Top = (tall ? CapHeight : XHeight) * px });
			x += width + gap;
		}
		laid.TextWidth = laid.Letters.Count > 0 ? x - gap : 0.0f;

		float after = style.Slant * CapHeight * px;
		switch (style.Deco)
		{
			case NameDeco.SpeedLines: laid.Lead = 0.7f * px; break;
			case NameDeco.Blade: laid.Lead = 0.34f * px; after = Mathf.Max(after, 0.42f * px); break;
			case NameDeco.HazardBar:
			case NameDeco.Rivets: laid.Lead = 0.1f * px; after = Mathf.Max(after, 0.1f * px); break;
		}
		laid.Extent = laid.Lead + laid.TextWidth + after;
		return laid;
	}

	/// <summary>
	/// Whether something is laid under the word (a hazard bar, a sword, a riveted strip): a name
	/// sitting on a line of its own has to be lifted a little to keep it clear of what is under it.
	/// </summary>
	public static bool LaysUnder(NameStyle style) =>
		style.Deco == NameDeco.HazardBar || style.Deco == NameDeco.Blade || style.Deco == NameDeco.Rivets;

	/// <summary>The name in this style at this size: how wide everything it draws is, and how tall a line of it is.</summary>
	public static Vector2 Measure(string text, NameStyle style, int fontSize)
	{
		Laid laid = Lay(text, style, fontSize);
		return new Vector2(laid.Extent, laid.Font.GetHeight(laid.Px));
	}

	/// <summary>
	/// The name in a box, on the line a Label of <paramref name="fontSize"/> would put it, aligned
	/// across it - shrunk to fit if it is wider than the box.
	/// </summary>
	public static void DrawIn(CanvasItem canvas, Rect2 box, string text, NameStyle style, int fontSize,
		HorizontalAlignment align = HorizontalAlignment.Left, float alpha = 1.0f, float heat = 0.0f)
	{
		Font font = ThemeDB.FallbackFont;
		float baseline = box.GetCenter().Y + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) * 0.5f;
		float width = Measure(text, style, fontSize).X;
		float fit = box.Size.X > 0.0f && width > box.Size.X ? box.Size.X / width : 1.0f;
		float left = align switch
		{
			HorizontalAlignment.Center => box.Position.X + (box.Size.X - width * fit) * 0.5f,
			HorizontalAlignment.Right => box.End.X - width * fit,
			_ => box.Position.X,
		};
		Draw(canvas, new Vector2(left, baseline), text, style, fontSize, alpha, heat, box.Size.X);
	}

	/// <summary>
	/// The name with the left of everything it draws at <paramref name="origin"/>, on that baseline.
	/// <paramref name="heat"/> (0 to 1) is a fighter's heat, for a style that glows with it;
	/// <paramref name="maxWidth"/> shrinks it to fit; <paramref name="under"/> is whatever transform
	/// the canvas is already drawing with (a popping speech bubble's), put back afterwards.
	/// </summary>
	public static void Draw(CanvasItem canvas, Vector2 origin, string text, NameStyle style, int fontSize,
		float alpha = 1.0f, float heat = 0.0f, float maxWidth = 0.0f, Transform2D? under = null)
	{
		if (string.IsNullOrEmpty(text) || style == null || alpha <= 0.01f) return;
		Laid laid = Lay(text, style, fontSize);
		Font font = laid.Font;
		int px = laid.Px;
		List<Letter> letters = laid.Letters;
		int n = letters.Count;
		int frame = (int)Engine.GetProcessFrames();
		heat = style.Glows ? Mathf.Clamp(heat, 0.0f, 1.0f) : 0.0f;

		float fit = maxWidth > 0.0f && laid.Extent > maxWidth ? maxWidth / laid.Extent : 1.0f;
		Transform2D baseTransform = under ?? Transform2D.Identity;
		Transform2D word = baseTransform * new Transform2D(0.0f, new Vector2(fit, fit), 0.0f, origin);

		float edge = style.Edge.A > 0.0f ? style.EdgeWidth * px : 0.0f;
		float halo = edge + 8.0f + 0.16f * px;

		// Where each letter sits this frame: its own transform, and its colour.
		var placed = new Transform2D[n];
		var colours = new Color[n];
		float glint = style.Deco == NameDeco.Blade ? GlintAt(laid, frame) : float.NaN;
		var shear = new Transform2D(new Vector2(style.Stretch, 0.0f), new Vector2(-style.Slant, 1.0f), Vector2.Zero);
		for (int i = 0; i < n; i++)
		{
			Letter letter = letters[i];
			Vector2 at = new Vector2(laid.Lead + letter.X + letter.Width * 0.5f, 0.0f);
			float turn = 0.0f;
			if (style.Bob > 0.0f)
			{
				// Each on its own beat: a slightly different length of bob, and out of step.
				float period = style.BobFrames * (1.0f + 0.2f * CrayonBrush.Noise(17, i));
				float angle = Mathf.Tau * (frame / period + 0.5f * CrayonBrush.Noise(23, i));
				at.Y -= style.Bob * px * Mathf.Sin(angle);
				turn += Mathf.DegToRad(style.Rock) * Mathf.Sin(angle + 1.3f);
			}
			if (style.Askew > 0.0f) turn += Mathf.DegToRad(style.Askew) * CrayonBrush.Noise(41, i);
			if (style.Drop > 0.0f) at.Y += style.Drop * px * CrayonBrush.Noise(43, i);
			if (style.Judder > 0.0f)
			{
				// A servo catching: now and then one letter twitches, more of them and further as he heats up.
				int step = frame / Mathf.Max(1, style.JudderFrames);
				if (CrayonBrush.Noise(step, i * 7 + 3) > 0.8f - 0.7f * heat)
				{
					at += new Vector2(CrayonBrush.Noise(step, i * 7 + 4), CrayonBrush.Noise(step, i * 7 + 5)) * style.Judder * px * (1.0f + 2.0f * heat);
				}
			}
			placed[i] = new Transform2D(turn, at) * shear;

			Color fill = style.Patches != null && style.Patches.Length > 0 ? style.Patches[i % style.Patches.Length]
				: style.FillTo.A > 0.0f && n > 1 ? style.Fill.Lerp(style.FillTo, i / (n - 1.0f))
				: style.Fill;
			if (style.Flicker > 0.0f)
			{
				float b = 1.0f + style.Flicker * CrayonBrush.Noise(frame / 3, i * 13 + 5);
				fill = new Color(Mathf.Min(1.0f, fill.R * b), Mathf.Min(1.0f, fill.G * b), Mathf.Min(1.0f, fill.B * b), fill.A);
			}
			if (heat > 0.0f)
			{
				float h = Mathf.Pow(heat, 1.3f) * 0.85f;
				// At full heat it throbs, as the gauge flashes.
				if (heat >= 1.0f) h *= 0.9f + 0.1f * Mathf.Sin(frame * 0.4f);
				fill = fill.Lerp(Hot, h);
			}
			if (!float.IsNaN(glint))
			{
				float near = 1.0f - Mathf.Abs(at.X - glint) / (0.7f * px);
				if (near > 0.0f) fill = fill.Lerp(Colors.White, 0.6f * near);
			}
			colours[i] = new Color(fill, fill.A * alpha);
		}

		// Behind everything.
		canvas.DrawSetTransformMatrix(word);
		if (style.Deco == NameDeco.SpeedLines) DrawSpeedLines(canvas, laid, style, frame, alpha);

		// The paper halo, and a hot glow inside it.
		Color paper = new Color(Paper, alpha);
		for (int i = 0; i < n; i++) Glyph(canvas, word, placed[i], letters[i], font, px, paper, halo);
		if (heat > 0.05f)
		{
			Color glow = new Color(HotGlow, 0.85f * heat * alpha);
			for (int i = 0; i < n; i++) Glyph(canvas, word, placed[i], letters[i], font, px, glow, edge + (halo - edge) * 0.6f);
		}

		// Laid under the word, over the halo.
		canvas.DrawSetTransformMatrix(word);
		// Over the halo, so the flames show whole - rising from behind each letter, not cut to spikes by its outline.
		if (style.Deco == NameDeco.Flames) DrawFlames(canvas, laid, style, frame, alpha);
		if (style.Deco == NameDeco.HazardBar) DrawHazardBar(canvas, laid, style, alpha);
		if (style.Deco == NameDeco.Blade) DrawBlade(canvas, laid, style, glint, alpha);
		if (style.Deco == NameDeco.Rivets) DrawRivetStrip(canvas, laid, style, heat, alpha);

		if (style.Shadow.A > 0.0f)
		{
			Color shadow = new Color(style.Shadow, style.Shadow.A * alpha);
			Transform2D offset = new Transform2D(0.0f, style.ShadowOffset * px);
			for (int i = 0; i < n; i++)
			{
				Glyph(canvas, word, offset * placed[i], letters[i], font, px, shadow, edge);
				Glyph(canvas, word, offset * placed[i], letters[i], font, px, shadow);
			}
		}

		if (edge > 0.0f)
		{
			Color ink = new Color(style.Edge, style.Edge.A * alpha);
			for (int i = 0; i < n; i++)
			{
				if (style.Scrawl > 0.0f)
				{
					// Gone over twice, a little off each time, as a marker does - fixed per letter.
					for (int pass = 0; pass < 2; pass++)
					{
						var off = new Vector2(CrayonBrush.Noise(61 + pass, i), CrayonBrush.Noise(71 + pass, i)) * style.Scrawl * px;
						Glyph(canvas, word, new Transform2D(0.0f, off) * placed[i], letters[i], font, px, ink, edge * 0.8f);
					}
				}
				else Glyph(canvas, word, placed[i], letters[i], font, px, ink, edge);
			}
		}

		if (style.Bevel > 0.0f)
		{
			float b = style.Bevel * px;
			for (int i = 0; i < n; i++)
			{
				if (style.Shade.A > 0.0f) Glyph(canvas, word, new Transform2D(0.0f, new Vector2(b, b)) * placed[i], letters[i], font, px, new Color(style.Shade, style.Shade.A * alpha));
				if (style.Highlight.A > 0.0f) Glyph(canvas, word, new Transform2D(0.0f, new Vector2(-b, -b)) * placed[i], letters[i], font, px, new Color(style.Highlight, style.Highlight.A * alpha));
			}
		}

		for (int i = 0; i < n; i++) Glyph(canvas, word, placed[i], letters[i], font, px, colours[i]);

		// In front.
		if (style.Deco == NameDeco.SpeedLines && n > 0) DrawTopHat(canvas, word * placed[0], letters[0], style, px, alpha);
		if (style.Deco == NameDeco.Blade && !float.IsNaN(glint))
		{
			canvas.DrawSetTransformMatrix(word);
			DrawGlint(canvas, new Vector2(glint, BladeY * px), 0.26f * px, alpha);
		}

		canvas.DrawSetTransformMatrix(baseTransform);
	}

	/// <summary>One letter, filled or (with an outline size) outlined, centred on its place.</summary>
	static void Glyph(CanvasItem canvas, Transform2D word, Transform2D placed, Letter letter, Font font, int px, Color colour, float outline = 0.0f)
	{
		if (colour.A <= 0.0f || letter.Text == " ") return;
		canvas.DrawSetTransformMatrix(word * placed);
		var at = new Vector2(-letter.Advance * 0.5f, 0.0f);
		if (outline > 0.0f) canvas.DrawStringOutline(font, at, letter.Text, HorizontalAlignment.Left, -1, px, Mathf.RoundToInt(outline), colour);
		else canvas.DrawString(font, at, letter.Text, HorizontalAlignment.Left, -1, px, colour);
	}

	// --- Decorations ---------------------------------------------------------------
	// Each in the word's own space: x from the left of everything drawn, y down from the baseline.

	/// <summary>Flambé: a tongue of flame off the top of each letter, rising whichever way the letters lean.</summary>
	static void DrawFlames(CanvasItem canvas, Laid laid, NameStyle style, int frame, float alpha)
	{
		int px = laid.Px;
		for (int i = 0; i < laid.Letters.Count; i++)
		{
			Letter letter = laid.Letters[i];
			if (letter.Text == " ") continue;
			float top = letter.Top;
			bool tall = top > XHeight * px + 1.0f;
			var root = new Vector2(laid.Lead + letter.X + letter.Width * 0.5f + style.Slant * top * 0.85f, -top * 0.86f);
			float length = px * (0.46f + 0.1f * CrayonBrush.Noise(5, i)) * (tall ? 1.0f : 0.75f);
			FlameFx.Tongue(canvas, root, Vector2.Up, length, px * (tall ? 0.30f : 0.24f), 0.35f * CrayonBrush.Noise(9, i), frame, i * 5 + 1, alpha);
		}
	}

	/// <summary>Triguy: three lines streaming out behind him, flickering back as he goes.</summary>
	static void DrawSpeedLines(CanvasItem canvas, Laid laid, NameStyle style, int frame, float alpha)
	{
		int px = laid.Px;
		float end = laid.Lead - 0.08f * px;
		for (int k = 0; k < 3; k++)
		{
			float y = -CapHeight * px * (0.2f + 0.26f * k);
			// Each streams back and comes again, out of step with the others.
			float t = ((frame + k * 9) % 27) / 27.0f;
			float length = laid.Lead * (0.55f + 0.3f * CrayonBrush.Noise(3, k)) * (1.0f - 0.35f * t);
			float slantShift = style.Slant * -y;
			var from = new Vector2(end - length + slantShift - 0.1f * px * t, y);
			var to = new Vector2(end + slantShift, y);
			canvas.DrawLine(from, to, new Color(Paper, alpha), 0.11f * px, true);
			canvas.DrawLine(from, to, new Color(style.DecoColor, alpha * (0.95f - 0.4f * t)), 0.055f * px, true);
		}
	}

	/// <summary>Triguy's top hat, tipped forward on his first letter.</summary>
	static void DrawTopHat(CanvasItem canvas, Transform2D letterSpace, Letter letter, NameStyle style, int px, float alpha)
	{
		canvas.DrawSetTransformMatrix(letterSpace * new Transform2D(-0.14f, new Vector2(0.0f, -letter.Top - 0.02f * px)));
		float brim = 0.30f * px, brimH = 0.06f * px, crownW = 0.19f * px, crownH = 0.32f * px, band = 0.07f * px;
		float border = 0.045f * px;
		var brimRect = new Rect2(-brim, -brimH, brim * 2.0f, brimH);
		var crownRect = new Rect2(-crownW, -brimH - crownH, crownW * 2.0f, crownH);
		Color paper = new Color(Paper, alpha);
		canvas.DrawRect(brimRect.Grow(border), paper);
		canvas.DrawRect(crownRect.Grow(border), paper);
		Color hat = new Color(style.DecoColorTwo, alpha);
		canvas.DrawRect(brimRect, hat);
		canvas.DrawRect(crownRect, hat);
		canvas.DrawRect(new Rect2(-crownW, -brimH - band, crownW * 2.0f, band), new Color(style.Edge, alpha));
		// A sheen on it, as Elim's hat has.
		canvas.DrawRect(new Rect2(-crownW + 0.05f * px, -brimH - crownH + 0.05f * px, 0.04f * px, crownH - band - 0.09f * px), new Color(1.0f, 1.0f, 1.0f, 0.45f * alpha));
	}

	/// <summary>Lugnut: a bar of yellow and pale grey hazard stripes under the word, a bolt at each end.</summary>
	static void DrawHazardBar(CanvasItem canvas, Laid laid, NameStyle style, float alpha)
	{
		int px = laid.Px;
		var bar = new Rect2(laid.Lead - 0.08f * px, 0.11f * px, laid.TextWidth + 0.16f * px, 0.17f * px);
		canvas.DrawRect(bar.Grow(0.05f * px), new Color(Paper, alpha));
		canvas.DrawRect(bar, new Color(style.DecoColor, alpha));
		float stripe = 0.22f * px;
		Vector2[] box = { bar.Position, new Vector2(bar.End.X, bar.Position.Y), bar.End, new Vector2(bar.Position.X, bar.End.Y) };
		for (float x = bar.Position.X - bar.Size.Y; x < bar.End.X; x += stripe * 2.0f)
		{
			Vector2[] band =
			{
				new Vector2(x + bar.Size.Y, bar.Position.Y), new Vector2(x + bar.Size.Y + stripe, bar.Position.Y),
				new Vector2(x + stripe, bar.End.Y), new Vector2(x, bar.End.Y),
			};
			foreach (Vector2[] piece in Geometry2D.IntersectPolygons(band, box))
			{
				canvas.DrawColoredPolygon(piece, new Color(style.DecoColorTwo, alpha));
			}
		}
		Color ink = new Color(style.Edge, alpha);
		canvas.DrawRect(bar, ink, false, Mathf.Max(1.5f, 0.035f * px));
		foreach (float x in new[] { bar.Position.X, bar.End.X })
		{
			var at = new Vector2(x, bar.GetCenter().Y);
			canvas.DrawCircle(at, 0.075f * px, new Color(Paper, alpha));
			canvas.DrawCircle(at, 0.055f * px, new Color(style.DecoColorTwo, alpha));
			canvas.DrawArc(at, 0.055f * px, 0.0f, Mathf.Tau, 16, ink, Mathf.Max(1.2f, 0.025f * px), true);
		}
	}

	/// <summary>How far under the baseline EdgeLord's blade runs, as a fraction of the font size.</summary>
	const float BladeY = 0.17f;

	/// <summary>
	/// Where the glint is along EdgeLord's blade this frame, in the word's space - or NaN between
	/// glints. One runs hilt to point every few seconds.
	/// </summary>
	static float GlintAt(Laid laid, int frame)
	{
		const int Every = 170, Run = 30;
		int t = frame % Every;
		if (t >= Run) return float.NaN;
		float from = laid.Lead, to = laid.Lead + laid.TextWidth + 0.38f * laid.Px;
		return Mathf.Lerp(from, to, t / (float)Run);
	}

	/// <summary>EdgeLord: a sword laid under his name, the hilt behind it and the point out past the end.</summary>
	static void DrawBlade(CanvasItem canvas, Laid laid, NameStyle style, float glint, float alpha)
	{
		int px = laid.Px;
		float y = BladeY * px;
		float half = 0.05f * px;
		float start = laid.Lead - 0.04f * px;
		float tip = laid.Lead + laid.TextWidth + 0.40f * px;
		Vector2[] blade =
		{
			new Vector2(start, y - half), new Vector2(tip - 0.32f * px, y - half), new Vector2(tip, y),
			new Vector2(tip - 0.32f * px, y + half), new Vector2(start, y + half),
		};
		Color paper = new Color(Paper, alpha);
		var closed = new Vector2[blade.Length + 1];
		blade.CopyTo(closed, 0);
		closed[blade.Length] = blade[0];
		canvas.DrawPolyline(closed, paper, 0.09f * px, true);
		canvas.DrawColoredPolygon(blade, new Color(style.DecoColor, alpha));
		// The fuller: a pale line down the middle of the blade.
		canvas.DrawLine(new Vector2(start + 0.06f * px, y - half * 0.25f), new Vector2(tip - 0.26f * px, y - half * 0.25f), new Color(1.0f, 1.0f, 1.0f, 0.8f * alpha), Mathf.Max(1.0f, 0.02f * px));
		canvas.DrawPolyline(closed, new Color(style.DecoColorTwo, alpha), Mathf.Max(1.2f, 0.025f * px), true);

		// The hilt: a crossguard, a grip and a pommel, in his dark red.
		Color hilt = new Color(style.Edge, alpha);
		var guard = new Rect2(start - 0.06f * px, y - 0.15f * px, 0.07f * px, 0.30f * px);
		var grip = new Rect2(start - 0.28f * px, y - 0.04f * px, 0.22f * px, 0.08f * px);
		canvas.DrawRect(guard.Grow(0.035f * px), paper);
		canvas.DrawRect(grip.Grow(0.035f * px), paper);
		canvas.DrawCircle(new Vector2(grip.Position.X, y), 0.09f * px, paper);
		canvas.DrawRect(grip, new Color(style.DecoColorTwo, alpha));
		canvas.DrawRect(guard, hilt);
		canvas.DrawCircle(new Vector2(grip.Position.X, y), 0.055f * px, hilt);
	}

	/// <summary>A four-pointed glint: the light catching a blade.</summary>
	static void DrawGlint(CanvasItem canvas, Vector2 at, float size, float alpha)
	{
		float thin = size * 0.16f;
		Color paper = new Color(Paper, 0.7f * alpha);
		Color white = new Color(1.0f, 1.0f, 1.0f, alpha);
		foreach (float grow in new[] { 1.35f, 1.0f })
		{
			float s = size * grow, w = thin * grow;
			canvas.DrawColoredPolygon(new[] { at + new Vector2(0, -s), at + new Vector2(w, 0), at + new Vector2(0, s), at + new Vector2(-w, 0) }, grow > 1.0f ? paper : white);
			canvas.DrawColoredPolygon(new[] { at + new Vector2(-s * 0.7f, 0), at + new Vector2(0, -w), at + new Vector2(s * 0.7f, 0), at + new Vector2(0, w) }, grow > 1.0f ? paper : white);
		}
	}

	/// <summary>DoomBot: a steel strip under the word, riveted along it - each panel of it scrap for Mk. II.</summary>
	static void DrawRivetStrip(CanvasItem canvas, Laid laid, NameStyle style, float heat, float alpha)
	{
		int px = laid.Px;
		var strip = new Rect2(laid.Lead - 0.08f * px, 0.12f * px, laid.TextWidth + 0.16f * px, 0.15f * px);
		canvas.DrawRect(strip.Grow(0.05f * px), new Color(Paper, alpha));
		Color steel = style.DecoColor.Lerp(Hot, 0.45f * heat);
		int rivets = Mathf.Max(2, Mathf.RoundToInt(strip.Size.X / (0.55f * px)) + 1);
		float step = (strip.Size.X - 0.12f * px) / (rivets - 1);
		bool scrap = style.Patches != null && style.Patches.Length > 0;
		canvas.DrawRect(strip, new Color(steel, alpha));
		if (scrap)
		{
			for (int k = 0; k + 1 < rivets; k++)
			{
				var panel = new Rect2(strip.Position.X + 0.06f * px + k * step, strip.Position.Y, step, strip.Size.Y);
				Color patch = style.Patches[(k * 2 + 1) % style.Patches.Length].Lerp(Hot, 0.45f * heat);
				canvas.DrawRect(panel, new Color(patch, alpha));
			}
		}
		Color ink = new Color(style.DecoColorTwo, alpha);
		canvas.DrawRect(strip, ink, false, Mathf.Max(1.2f, 0.03f * px));
		for (int k = 0; k < rivets; k++)
		{
			var at = new Vector2(strip.Position.X + 0.06f * px + k * step, strip.GetCenter().Y);
			canvas.DrawCircle(at, 0.045f * px, ink);
			canvas.DrawCircle(at - new Vector2(0.014f, 0.014f) * px, 0.018f * px, new Color(1.0f, 1.0f, 1.0f, 0.8f * alpha));
		}
	}
}
