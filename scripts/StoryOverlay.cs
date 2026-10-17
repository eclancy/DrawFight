using System.Collections.Generic;
using Godot;

/// <summary>
/// Everything a story draws over the match: crayon speech bubbles on whoever is talking, typed
/// out a letter at a time in their voice; the narrator's torn paper strip; the bars that come in
/// while a scene plays; and the skip meter. Screen space, so a line stays readable however far
/// the camera has pulled back. See "How a scene is staged" in .ai/story-mode.md.
/// </summary>
public partial class StoryOverlay : CanvasLayer
{
	public MatchManager Match;

	/// <summary>One character or picture in a line, laid out in the bubble.</summary>
	internal struct Glyph
	{
		public string Ch;
		public string Picture;
		public Vector2 Pos;
		public float Width;
		public int Word;
	}

	/// <summary>A line being said: typed in, held to be read, then closed.</summary>
	public class Line
	{
		public Fighter Speaker;
		public string Mood = "";
		public string Text = "";
		internal List<Glyph> Glyphs = new List<Glyph>();
		internal Vector2 Size;
		internal int FontSize;
		internal float LineHeight;
		internal float Shown;
		internal int Age, Hold, Closing = -1;
		internal int Seed;
		internal int Words;

		public bool Typed => Shown >= Glyphs.Count;

		/// <summary>Frames it has stood fully typed.</summary>
		internal int TypedFrames;

		/// <summary>Typed, and read (or hurried on).</summary>
		public bool Finished;
	}

	readonly List<Line> lines = new List<Line>();
	Line narration;
	Node2D canvas;
	int frames;

	/// <summary>How far in the bars are, 0 to 1, and where they are going.</summary>
	float bars, barsTarget;

	/// <summary>How far a held skip has got, 0 to 1.</summary>
	public float SkipHeld;

	/// <summary>Skipping: the scene runs on fast, under a sheet of paper.</summary>
	public bool Skipping;
	float skipCover;

	const float BarHeight = 96.0f;
	const float MaxWidth = 640.0f;

	static readonly Color Paper = new Color(0.985f, 0.975f, 0.94f);
	static readonly Color Bar = new Color(0.27f, 0.29f, 0.36f);
	static readonly Color Pencil = new Color(0.36f, 0.37f, 0.42f);
	static readonly Color Whisper = new Color(0.52f, 0.54f, 0.60f);
	static readonly Color Hot = new Color(0.95f, 0.45f, 0.15f);
	static readonly Color Overheated = new Color(0.90f, 0.22f, 0.20f);

	public override void _Ready()
	{
		Layer = 5;
		canvas = new Node2D();
		AddChild(canvas);
		canvas.Draw += DrawAll;
	}

	public void ShowBars(bool on) => barsTarget = on ? 1.0f : 0.0f;

	public bool BarsGone => bars <= 0.0f && barsTarget <= 0.0f;

	// --- Lines ------------------------------------------------------------------------

	/// <summary>Starts a fighter saying something. Anything they were still saying closes.</summary>
	public Line Say(Fighter speaker, string mood, string text)
	{
		foreach (Line old in lines) if (old.Closing < 0) old.Closing = 8;
		var line = Layout(new Line { Speaker = speaker, Mood = mood, Text = text, Seed = (frames * 31 + text.Length) % 9973 });
		lines.Add(line);
		if (mood == "shout") SfxPlayer.Ui("stomp");
		return line;
	}

	/// <summary>
	/// Starts the narrator's strip. One narrator line at a time. Shouted, it is the grown-up's
	/// voice from the kitchen in Lugnut's story: bigger, red, and the strip shakes.
	/// </summary>
	public Line Narrate(string text, string mood = "")
	{
		narration = Layout(new Line { Mood = mood == "shout" ? "narrator shout" : "narrator", Text = text, Seed = text.Length * 7 });
		if (mood == "shout") SfxPlayer.Ui("stomp");
		return narration;
	}

	/// <summary>The line is done with: it closes and goes.</summary>
	public void Close(Line line)
	{
		if (line == narration) narration = null;
		else if (line != null && line.Closing < 0) line.Closing = 8;
	}

	public void CloseAll()
	{
		foreach (Line line in lines) if (line.Closing < 0) line.Closing = 8;
		narration = null;
	}

	/// <summary>A button pressed: a line still typing finishes typing, a typed one is done.</summary>
	/// <summary>
	/// How long a line stands fully typed before A can send it on: a quarter of a second, so the
	/// press that finished the typing - or a second press just after it - never throws the words
	/// away before they are read (Eric, 2026-10-16).
	/// </summary>
	const int ReadBeforeHurry = 15;

	public void Hurry(Line line)
	{
		if (line == null) return;
		if (!line.Typed)
		{
			line.Shown = line.Glyphs.Count;
			line.TypedFrames = 0;
		}
		else if (line.TypedFrames >= ReadBeforeHurry) line.Finished = true;
	}

	static int FontSizeOf(string mood) => mood switch
	{
		"shout" => 48,
		"slam" => 52,
		"whisper" => 28,
		"narrator" => 38,
		"narrator shout" => 46,
		_ => 36,
	};

	Line Layout(Line line)
	{
		Font font = ThemeDB.FallbackFont;
		int size = line.FontSize = FontSizeOf(line.Mood);
		bool pictures = line.Text.Contains('[');
		float picture = size * 2.7f;
		line.LineHeight = pictures ? picture + 10.0f : size * 1.3f;
		float space = font.GetCharSize(' ', size).X;
		float max = line.Mood.StartsWith("narrator") ? 1300.0f : MaxWidth;

		// Words, and [pictures] as words of their own.
		var words = new List<string>();
		foreach (string raw in line.Text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
		{
			string rest = raw;
			while (rest.Length > 0)
			{
				int open = rest.IndexOf('[');
				if (open < 0) { words.Add(rest); break; }
				if (open > 0) words.Add(rest.Substring(0, open));
				int close = rest.IndexOf(']', open);
				if (close < 0) { words.Add(rest.Substring(open)); break; }
				words.Add(rest.Substring(open, close - open + 1));
				rest = rest.Substring(close + 1);
			}
		}

		float x = 0.0f, y = 0.0f, widest = 0.0f;
		for (int w = 0; w < words.Count; w++)
		{
			string word = words[w];
			bool isPicture = word.StartsWith("[") && word.EndsWith("]");
			float width = 0.0f;
			if (isPicture) width = picture;
			else foreach (char ch in word) width += font.GetCharSize(ch, size).X;

			if (x > 0.0f && x + width > max)
			{
				x = 0.0f;
				y += line.LineHeight;
			}

			if (isPicture)
			{
				line.Glyphs.Add(new Glyph { Picture = word.Substring(1, word.Length - 2).ToLowerInvariant(), Pos = new Vector2(x, y), Width = picture, Word = w });
			}
			else
			{
				float cx = x;
				foreach (char ch in word)
				{
					float cw = font.GetCharSize(ch, size).X;
					line.Glyphs.Add(new Glyph { Ch = ch.ToString(), Pos = new Vector2(cx, y), Width = cw, Word = w });
					cx += cw;
				}
			}
			x += width;
			widest = Mathf.Max(widest, x);
			x += space;
		}
		line.Words = words.Count;
		line.Size = new Vector2(widest, y + line.LineHeight);
		return line;
	}

	/// <summary>Letters typed a frame: a shout comes out fast, a whisper slowly.</summary>
	static float TypeRate(string mood) => mood switch
	{
		"shout" => 1.1f,
		"whisper" => 0.5f,
		"scared" => 0.55f,
		"narrator" => 0.8f,
		"narrator shout" => 1.2f,
		_ => 0.75f,
	};

	/// <summary>Frames between DoomBot's words when each one slams in on its own.</summary>
	const int SlamFrames = 18;

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		bars = Mathf.MoveToward(bars, barsTarget, 1.0f / 14.0f);
		skipCover = Mathf.MoveToward(skipCover, Skipping ? 1.0f : 0.0f, 1.0f / 8.0f);

		for (int i = lines.Count - 1; i >= 0; i--)
		{
			Line line = lines[i];
			if (line.Closing >= 0)
			{
				if (--line.Closing <= 0) lines.RemoveAt(i);
				continue;
			}
			Tick(line);
		}
		if (narration != null) Tick(narration);
		canvas.QueueRedraw();
	}

	void Tick(Line line)
	{
		line.Age++;
		if (Skipping)
		{
			line.Shown = line.Glyphs.Count;
			line.Finished = true;
			return;
		}

		if (!line.Typed)
		{
			int before = (int)line.Shown;
			if (line.Mood == "slam")
			{
				// A word at a time: everything up to the next word appears at once.
				int word = line.Age / SlamFrames;
				int count = 0;
				while (count < line.Glyphs.Count && line.Glyphs[count].Word <= word) count++;
				line.Shown = count;
				if (line.Age % SlamFrames == 1)
				{
					SfxPlayer.Ui("clang");
					Match?.Shake(32.0f);
				}
			}
			else
			{
				line.Shown = Mathf.Min(line.Glyphs.Count, line.Shown + TypeRate(line.Mood));
			}

			// A blip in the speaker's voice every few letters - never on a space.
			int now = (int)line.Shown;
			if (now > before && line.Speaker != null && line.Mood != "slam")
			{
				for (int g = before; g < now; g++)
				{
					if (g % 3 == 0 && line.Glyphs[g].Ch != " ") SfxPlayer.Voice("voice", line.Speaker.Data.VoicePitch);
				}
			}
			if (line.Speaker != null)
			{
				line.Speaker.Talking = !line.Typed;
				line.Speaker.TalkMood = line.Mood;
			}
			if (line.Typed) line.Hold = HoldFor(line);
			return;
		}

		if (line.Speaker != null) line.Speaker.Talking = false;
		line.TypedFrames++;
		if (--line.Hold <= 0) line.Finished = true;
	}

	/// <summary>Long enough to read it twice over, as a kid reads.</summary>
	static int HoldFor(Line line) => Mathf.Min(300, 50 + line.Text.Length * 3 + (line.Mood == "slam" ? 40 : 0));

	// --- Drawing ------------------------------------------------------------------------

	Vector2 ToScreen(Vector2 world) => Match.GetViewport().CanvasTransform * world;

	void DrawAll()
	{
		Vector2 screen = canvas.GetViewportRect().Size;

		if (Match != null) foreach (Fighter f in Match.Fighters) if (!string.IsNullOrEmpty(f.Mark) && f.Visible) DrawMark(f);
		foreach (Line line in lines) DrawBubble(line, screen);
		DrawBars(screen);
		if (narration != null) DrawNarration(narration, screen);
		DrawSkip(screen);
	}

	void DrawBubble(Line line, Vector2 screen)
	{
		if (line.Speaker == null || !IsInstanceValid(line.Speaker)) return;
		float fade = line.Closing >= 0 ? line.Closing / 8.0f : 1.0f;
		// Comes in with a little pop; a shout pops harder.
		float pop = Mathf.Min(1.0f, line.Age / 7.0f);
		float scale = Mathf.Lerp(line.Mood == "shout" || line.Mood == "slam" ? 1.35f : 0.8f, 1.0f, pop);

		Rect2 body = line.Speaker.BodyRect();
		Vector2 head = ToScreen(new Vector2(line.Speaker.GlobalPosition.X, body.Position.Y));
		Vector2 pad = new Vector2(30.0f, 22.0f);
		Vector2 size = line.Size + pad * 2.0f;
		// Below the bars, with room over it for the name.
		float top = Mathf.Clamp(head.Y - 46.0f - size.Y, BarHeight + 48.0f, screen.Y - size.Y - BarHeight - 20.0f);
		float left = Mathf.Clamp(head.X - size.X * 0.5f, 30.0f, screen.X - size.X - 30.0f);
		Vector2 centre = new Vector2(left, top) + size * 0.5f;

		// A shout shakes while it is being shouted.
		if (line.Mood == "shout" && !line.Typed)
		{
			centre += new Vector2(CrayonBrush.Noise(frames, 3), CrayonBrush.Noise(frames, 5)) * 4.0f;
		}

		CurrentTransform = new Transform2D(0.0f, new Vector2(scale, scale), 0.0f, centre);
		canvas.DrawSetTransformMatrix(CurrentTransform);
		Vector2 half = size * 0.5f;
		Vector2 tip = (head - centre) / scale;
		tip.Y = Mathf.Max(tip.Y, half.Y + 18.0f);
		tip.X = Mathf.Clamp(tip.X, -half.X + 30.0f, half.X - 30.0f);

		bool spiky = line.Mood == "shout" || line.Mood == "slam";
		Color ink = new Color(MenuTheme.Ink, fade);
		Color fill = new Color(Paper, fade);
		Vector2[] outline = BubbleShape(half, spiky, line.Seed);

		// Tail first, so the bubble's fill covers where it joins.
		float tx = Mathf.Clamp(tip.X * 0.4f, -half.X + 40.0f, half.X - 70.0f);
		var tail = new[] { new Vector2(tx, half.Y - 6.0f), new Vector2(tx + 36.0f, half.Y - 6.0f), tip };
		canvas.DrawColoredPolygon(new[] { tail[0] + new Vector2(4, 6), tail[1] + new Vector2(4, 6), tip + new Vector2(4, 6) }, new Color(0, 0, 0, 0.06f * fade));
		canvas.DrawColoredPolygon(OffsetAll(outline, new Vector2(6.0f, 8.0f)), new Color(0, 0, 0, 0.06f * fade));
		canvas.DrawColoredPolygon(tail, fill);
		canvas.DrawLine(tail[0], tip, ink, 4.0f, true);
		canvas.DrawLine(tail[1], tip, ink, 4.0f, true);
		canvas.DrawColoredPolygon(outline, fill);
		if (line.Mood == "whisper") DrawDashed(outline, ink, 3.0f);
		else canvas.DrawPolyline(Closed(outline), ink, spiky ? 5.0f : 4.5f, true);
		// Cover the outline where the tail leaves the bubble.
		canvas.DrawLine(tail[0] + new Vector2(4.0f, 0.0f), tail[1] - new Vector2(4.0f, 0.0f), fill, 7.0f);

		// The speaker's name, small, on the corner - in their colour.
		var nameAt = new Vector2(-half.X + 18.0f, -half.Y - 8.0f);
		canvas.DrawStringOutline(ThemeDB.FallbackFont, nameAt, line.Speaker.Data.DisplayName, HorizontalAlignment.Left, -1, 26,
			10, new Color(Paper, fade));
		canvas.DrawString(ThemeDB.FallbackFont, nameAt, line.Speaker.Data.DisplayName, HorizontalAlignment.Left, -1, 26,
			new Color(line.Speaker.Data.PlaceholderColor.Darkened(0.15f), fade));

		DrawText(line, -line.Size * 0.5f, fade, line.Speaker);
		CurrentTransform = Transform2D.Identity;
		canvas.DrawSetTransformMatrix(Transform2D.Identity);
	}

	void DrawText(Line line, Vector2 origin, float fade, Fighter speaker)
	{
		Font font = ThemeDB.FallbackFont;
		Color baseInk = line.Mood switch
		{
			"whisper" => Whisper,
			"narrator" => Pencil,
			"narrator shout" => Overheated,
			_ => MenuTheme.Text,
		};
		// Red-hot when he is: DoomBot's words glow with his heat.
		if (line.Mood == "slam" && speaker != null && speaker.HeatFraction > 0.75f) baseInk = Overheated;

		int shown = (int)line.Shown;
		float ascent = font.GetAscent(line.FontSize);
		for (int i = 0; i < shown && i < line.Glyphs.Count; i++)
		{
			Glyph g = line.Glyphs[i];
			Vector2 at = origin + g.Pos;
			Color ink = baseInk;
			switch (line.Mood)
			{
				case "scared":
					at += new Vector2(CrayonBrush.Noise(frames / 2, i * 3) * 1.6f, Mathf.Sin(frames * 0.7f + i * 1.3f) * 2.4f);
					break;
				case "happy":
					at.Y += Mathf.Sin(frames * 0.22f + i * 0.55f) * 4.0f;
					break;
				case "hot":
					ink = baseInk.Lerp(Hot, 0.55f + 0.45f * Mathf.Sin(frames * 0.5f + i * 0.9f));
					break;
			}
			ink.A *= fade;

			// Each new word of a slam lands big and settles.
			float grow = 1.0f;
			if (line.Mood == "slam")
			{
				int landed = line.Age - g.Word * SlamFrames;
				grow = Mathf.Lerp(1.7f, 1.0f, Mathf.Clamp(landed / 6.0f, 0.0f, 1.0f));
			}

			if (g.Picture != null)
			{
				Vector2 c = at + new Vector2(g.Width * 0.5f, line.LineHeight * 0.5f - 4.0f);
				float appear = Mathf.Clamp((line.Shown - i) * 2.0f, 0.0f, 1.0f);
				StoryPictures.Draw(canvas, g.Picture, c, g.Width * 0.82f * Mathf.Lerp(1.3f, 1.0f, appear), MenuTheme.Ink, fade, line.Seed + i);
				continue;
			}

			Vector2 baseline = at + new Vector2(0.0f, (line.LineHeight - line.FontSize * 1.3f) * 0.5f + ascent);
			if (grow != 1.0f)
			{
				Vector2 pivot = baseline + new Vector2(g.Width * 0.5f, -ascent * 0.4f);
				canvas.DrawSetTransformMatrix(CurrentTransform * new Transform2D(0.0f, new Vector2(grow, grow), 0.0f, pivot) * new Transform2D(0.0f, -pivot));
				canvas.DrawChar(font, baseline, g.Ch, line.FontSize, ink);
				canvas.DrawSetTransformMatrix(CurrentTransform);
			}
			else
			{
				canvas.DrawChar(font, baseline, g.Ch, line.FontSize, ink);
			}
		}
	}

	/// <summary>The transform the bubble being drawn is under, so a slammed word can scale about itself inside it.</summary>
	Transform2D CurrentTransform = Transform2D.Identity;

	static Vector2[] BubbleShape(Vector2 half, bool spiky, int seed)
	{
		var pts = new List<Vector2>();
		if (spiky)
		{
			// A shout's burst: points all round, longer at the corners.
			int n = 26;
			for (int i = 0; i < n; i++)
			{
				float a = Mathf.Tau * i / n;
				Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
				float r = i % 2 == 0 ? 1.18f + CrayonBrush.Noise(seed, i) * 0.06f : 0.98f;
				pts.Add(new Vector2(dir.X * (half.X + 26.0f) * r, dir.Y * (half.Y + 22.0f) * r));
			}
			return pts.ToArray();
		}

		// A rounded box, the corners drawn freehand.
		float radius = Mathf.Min(30.0f, Mathf.Min(half.X, half.Y));
		Vector2[] corners = { new Vector2(half.X - radius, -half.Y + radius), new Vector2(half.X - radius, half.Y - radius),
			new Vector2(-half.X + radius, half.Y - radius), new Vector2(-half.X + radius, -half.Y + radius) };
		float[] starts = { -Mathf.Pi / 2, 0.0f, Mathf.Pi / 2, Mathf.Pi };
		int k = 0;
		for (int c = 0; c < 4; c++)
		{
			for (int i = 0; i <= 6; i++)
			{
				float a = starts[c] + Mathf.Pi / 2 * i / 6.0f;
				Vector2 p = corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
				pts.Add(p + new Vector2(CrayonBrush.Noise(seed, k), CrayonBrush.Noise(seed, k + 50)) * 1.5f);
				k++;
			}
		}
		return pts.ToArray();
	}

	static Vector2[] Closed(Vector2[] pts)
	{
		var closed = new Vector2[pts.Length + 1];
		pts.CopyTo(closed, 0);
		closed[pts.Length] = pts[0];
		return closed;
	}

	static Vector2[] OffsetAll(Vector2[] pts, Vector2 by)
	{
		var moved = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++) moved[i] = pts[i] + by;
		return moved;
	}

	void DrawDashed(Vector2[] pts, Color ink, float width)
	{
		for (int i = 0; i < pts.Length; i += 2)
		{
			canvas.DrawLine(pts[i], pts[(i + 1) % pts.Length], ink, width, true);
		}
	}

	// --- Marks over a head --------------------------------------------------------------------

	static readonly Color MarkRed = new Color(0.92f, 0.28f, 0.28f);
	static readonly Color MarkBlue = new Color(0.42f, 0.68f, 0.98f);
	static readonly Color MarkGold = new Color(1.0f, 0.84f, 0.3f);

	/// <summary>
	/// The cartoon mark a story has put on someone: an anger vein, a sweat drop, sparkles, a heart,
	/// a "!" or "?", steam, tears. Crayon-bright, outlined in the stage's ink so it reads on any
	/// page, and always moving a little - a mark that sits still reads as a sticker.
	/// </summary>
	void DrawMark(Fighter f)
	{
		Vector2 head = ToScreen(f.HeadGlobal());
		float zoom = Match.GetViewport().CanvasTransform.X.X;
		float s = Mathf.Clamp(zoom, 0.6f, 1.4f);
		Color ink = MenuTheme.Ink;
		int t = frames;
		Vector2 side = head + new Vector2(f.Facing * 46.0f * s, -12.0f * s);
		switch (f.Mark)
		{
			case "anger":
			{
				// The four curved prongs of a throbbing vein.
				float pulse = 1.0f + 0.15f * Mathf.Sin(t * 0.4f);
				for (int i = 0; i < 4; i++)
				{
					float a = Mathf.Pi * 0.25f + Mathf.Pi * 0.5f * i;
					Vector2 dir = Vector2.Right.Rotated(a);
					Vector2 from = side + dir * 8.0f * s * pulse;
					Vector2 to = side + dir * 22.0f * s * pulse;
					Vector2 bend = (from + to) * 0.5f + dir.Orthogonal() * 6.0f * s;
					canvas.DrawPolyline(new[] { from, bend, to }, ink, 9.0f * s, true);
					canvas.DrawPolyline(new[] { from, bend, to }, MarkRed, 5.0f * s, true);
				}
				break;
			}
			case "sweat":
			{
				Vector2 at = side + new Vector2(0.0f, 10.0f * s + (t % 40) * 0.6f * s);
				DrawDrop(at, 13.0f * s, MarkBlue, ink);
				break;
			}
			case "tears":
			{
				for (int i = 0; i < 2; i++)
				{
					float k = ((t + i * 17) % 34) / 34.0f;
					Vector2 at = head + new Vector2((i == 0 ? 20.0f : 34.0f) * f.Facing * s, 30.0f * s + 60.0f * s * k);
					DrawDrop(at, 9.0f * s * (1.0f - 0.4f * k), MarkBlue, ink);
				}
				break;
			}
			case "sparkle":
			{
				for (int i = 0; i < 3; i++)
				{
					float tw = Mathf.Abs(Mathf.Sin(t * 0.15f + i * 2.1f));
					Vector2 at = head + new Vector2((i - 1) * 34.0f * s, -26.0f * s - (i == 1 ? 18.0f * s : 0.0f));
					DrawSparkle(at, (8.0f + 10.0f * tw) * s, MarkGold, ink);
				}
				break;
			}
			case "heart":
			{
				float beat = 1.0f + 0.18f * Mathf.Max(0.0f, Mathf.Sin(t * 0.3f));
				Vector2 at = head + new Vector2(0.0f, -40.0f * s - (t % 60) * 0.3f * s);
				StoryPictures.Draw(canvas, "heart", at, 44.0f * s * beat, ink, 1.0f, 77);
				break;
			}
			case "!":
			case "?":
			{
				float hop = Mathf.Abs(Mathf.Sin(t * 0.2f)) * 6.0f * s;
				Vector2 at = head + new Vector2(-12.0f * s, -24.0f * s - hop);
				int size = (int)(64 * s);
				canvas.DrawStringOutline(ThemeDB.FallbackFont, at, f.Mark, HorizontalAlignment.Left, -1, size, (int)(10 * s), MenuTheme.Paper);
				canvas.DrawString(ThemeDB.FallbackFont, at, f.Mark, HorizontalAlignment.Left, -1, size, f.Mark == "!" ? MarkRed : MenuTheme.Ink);
				break;
			}
			case "steam":
			{
				for (int i = 0; i < 5; i++)
				{
					float k = ((t + i * 11) % 50) / 50.0f;
					Vector2 at = head + new Vector2((i - 2) * 16.0f * s + Mathf.Sin(t * 0.1f + i) * 6.0f, -10.0f * s - 70.0f * s * k);
					canvas.DrawCircle(at, (8.0f + 14.0f * k) * s, new Color(0.95f, 0.95f, 0.98f, 0.85f * (1.0f - k)));
				}
				break;
			}
		}
	}

	void DrawDrop(Vector2 at, float r, Color fill, Color ink)
	{
		var pts = new Vector2[13];
		for (int i = 0; i < 12; i++)
		{
			float a = Mathf.Pi * 2.0f * i / 12.0f;
			pts[i] = at + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
		}
		pts[0] = at + new Vector2(0.0f, -r * 2.2f);
		pts[12] = pts[0];
		canvas.DrawColoredPolygon(pts[..12], fill);
		canvas.DrawPolyline(pts, ink, 3.0f, true);
		canvas.DrawCircle(at + new Vector2(-r * 0.35f, -r * 0.2f), r * 0.25f, new Color(1, 1, 1, 0.8f));
	}

	void DrawSparkle(Vector2 at, float r, Color fill, Color ink)
	{
		var pts = new Vector2[9];
		for (int i = 0; i < 8; i++)
		{
			float a = Mathf.Pi * 0.25f * i;
			pts[i] = at + Vector2.Right.Rotated(a) * (i % 2 == 0 ? r : r * 0.28f);
		}
		pts[8] = pts[0];
		canvas.DrawColoredPolygon(pts[..8], fill);
		canvas.DrawPolyline(pts, ink, 2.5f, true);
	}

	void DrawBars(Vector2 screen)
	{
		if (bars <= 0.0f) return;
		float eased = bars * bars * (3.0f - 2.0f * bars);
		float h = BarHeight * eased;
		canvas.DrawRect(new Rect2(0.0f, 0.0f, screen.X, h), Bar);
		canvas.DrawRect(new Rect2(0.0f, screen.Y - h, screen.X, h), Bar);
		Color edge = new Color(0.40f, 0.43f, 0.52f);
		CrayonBrush.InkLine(canvas, new Vector2(0.0f, h), new Vector2(screen.X, h), edge, 4.0f, 7301, 2.0f);
		CrayonBrush.InkLine(canvas, new Vector2(0.0f, screen.Y - h), new Vector2(screen.X, screen.Y - h), edge, 4.0f, 7302, 2.0f);

		if (eased > 0.9f && !Skipping)
		{
			Color hint = new Color(Paper, 0.55f * (eased - 0.9f) * 10.0f);
			canvas.DrawString(ThemeDB.FallbackFont, new Vector2(screen.X - 560.0f, screen.Y - 34.0f),
				"A: next      hold B: skip", HorizontalAlignment.Right, 520.0f, 26, hint);
		}
	}

	void DrawNarration(Line line, Vector2 screen)
	{
		float pop = Mathf.Min(1.0f, line.Age / 8.0f);
		float width = Mathf.Max(700.0f, line.Size.X + 120.0f);
		float height = line.Size.Y + 46.0f;
		Vector2 centre = new Vector2(screen.X * 0.5f, screen.Y - BarHeight - height * 0.5f + 30.0f);
		if (line.Mood == "narrator shout" && !line.Typed) centre += new Vector2(CrayonBrush.Noise(frames, 13), CrayonBrush.Noise(frames, 17)) * 6.0f;
		CurrentTransform = new Transform2D(-0.012f, new Vector2(1.0f, pop), 0.0f, centre);
		canvas.DrawSetTransformMatrix(CurrentTransform);

		// A strip torn off a page: ragged top and bottom, straight-cut ends.
		var pts = new List<Vector2>();
		int teeth = (int)(width / 22.0f);
		for (int i = 0; i <= teeth; i++)
		{
			float x = -width * 0.5f + width * i / teeth;
			pts.Add(new Vector2(x, -height * 0.5f + Mathf.Abs(CrayonBrush.Noise(4401, i)) * 7.0f));
		}
		for (int i = teeth; i >= 0; i--)
		{
			float x = -width * 0.5f + width * i / teeth;
			pts.Add(new Vector2(x, height * 0.5f - Mathf.Abs(CrayonBrush.Noise(4402, i)) * 7.0f));
		}
		Vector2[] strip = pts.ToArray();
		canvas.DrawColoredPolygon(OffsetAll(strip, new Vector2(5.0f, 7.0f)), new Color(0, 0, 0, 0.10f));
		canvas.DrawColoredPolygon(strip, new Color(0.975f, 0.955f, 0.89f));
		// The faint rules of the page it was torn from.
		for (float y = -height * 0.5f + 26.0f; y < height * 0.5f - 6.0f; y += 34.0f)
		{
			canvas.DrawLine(new Vector2(-width * 0.5f, y), new Vector2(width * 0.5f, y), new Color(MenuTheme.Rule, 0.6f), 2.0f);
		}

		DrawText(line, -line.Size * 0.5f, 1.0f, null);
		CurrentTransform = Transform2D.Identity;
		canvas.DrawSetTransformMatrix(Transform2D.Identity);
	}

	void DrawSkip(Vector2 screen)
	{
		if (skipCover > 0.0f)
		{
			canvas.DrawRect(new Rect2(Vector2.Zero, screen), new Color(MenuTheme.Paper, skipCover));
			canvas.DrawString(ThemeDB.FallbackFont, new Vector2(0.0f, screen.Y * 0.5f), "Skipping...",
				HorizontalAlignment.Center, screen.X, 60, new Color(MenuTheme.Soft, skipCover));
		}
		if (SkipHeld > 0.0f && !Skipping)
		{
			Vector2 c = new Vector2(screen.X - 600.0f, screen.Y - 44.0f);
			canvas.DrawArc(c, 18.0f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * SkipHeld, 24, Paper, 6.0f, true);
		}
	}
}
