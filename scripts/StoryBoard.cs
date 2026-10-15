using System.Collections.Generic;
using Godot;

/// <summary>
/// Writing on the fridge door itself, a line at a time - DoomBot burning his plan onto it with
/// his eye laser ("doombot writes: 1. REMOVE DRAWINGS."). In the world, behind the fighters, so
/// it is on the door and the camera moves over it like everything else.
/// </summary>
public partial class StoryBoard : Node2D
{
	class Written
	{
		public string Text;
		public float Shown;
		public int Hold;
		public Fighter Writer;
		public bool Finished;
	}

	readonly List<Written> lines = new List<Written>();
	Vector2 anchor;
	float fade = 1.0f;
	bool clearing;
	int frames;

	public bool Skipping;

	const int FontSize = 72;
	const float LineGap = 96.0f;

	static readonly Color Burn = new Color(0.90f, 0.24f, 0.20f);
	static readonly Color Glow = new Color(1.0f, 0.55f, 0.35f);

	public void At(Vector2 world)
	{
		anchor = world;
		lines.Clear();
		fade = 1.0f;
		clearing = false;
	}

	public void Clear() => clearing = true;

	// --- DoomBot's reveal -----------------------------------------------------------------
	// The lights down, a red eye opening up on the freezer door, and the drawings he took down
	// heaped at the foot of the fridge (.ai/story-mode.md, the finale).

	/// <summary>The stage dimmed to a night lavender behind the fighters - never near black: characters own black.</summary>
	public bool Dark;
	float darkness;

	Vector2 eyeAt;
	bool eyeOpen;
	float eyeOpenness;

	float? pileX;
	float pileFloor;

	public void OpenEye(Vector2 at)
	{
		eyeAt = at;
		eyeOpen = true;
	}

	public void CloseEye() => eyeOpen = false;

	Vector2? pageAt;
	int pageFrames;

	/// <summary>
	/// A new drawing pinned up on the door with a magnet - a kid's crayon drawing of DoomBot, for
	/// his ending - or none.
	/// </summary>
	public void Page(Vector2? at)
	{
		pageAt = at;
		pageFrames = 0;
	}

	/// <summary>The heap of taken-down drawings on the floor at x, or none.</summary>
	public void Pile(float? x, float floorY)
	{
		pileX = x;
		pileFloor = floorY;
	}

	/// <summary>Starts the next line; done once it is written and has sat a moment.</summary>
	public object Write(Fighter writer, string text)
	{
		var line = new Written { Text = text, Writer = writer };
		lines.Add(line);
		return line;
	}

	public bool IsFinished(object line) => line is Written w && w.Finished;

	public void Hurry(object line)
	{
		if (line is not Written w) return;
		if (w.Shown < w.Text.Length) w.Shown = w.Text.Length;
		else w.Finished = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		darkness = Mathf.MoveToward(darkness, Dark ? 1.0f : 0.0f, 1.0f / 30.0f);
		eyeOpenness = Mathf.MoveToward(eyeOpenness, eyeOpen ? 1.0f : 0.0f, 1.0f / 14.0f);
		if (clearing)
		{
			fade = Mathf.MoveToward(fade, 0.0f, 1.0f / 20.0f);
			if (fade <= 0.0f) lines.Clear();
		}

		foreach (Written line in lines)
		{
			if (line.Finished) continue;
			if (Skipping)
			{
				line.Shown = line.Text.Length;
				line.Finished = true;
				continue;
			}
			if (line.Shown < line.Text.Length)
			{
				int before = (int)line.Shown;
				line.Shown = Mathf.Min(line.Text.Length, line.Shown + 0.45f);
				if ((int)line.Shown != before && before % 3 == 0) SfxPlayer.At("zap", WorldOf(line, before), 0.08f);
				if (line.Shown >= line.Text.Length) line.Hold = 50;
			}
			else if (--line.Hold <= 0)
			{
				line.Finished = true;
			}
		}
		QueueRedraw();
	}

	/// <summary>
	/// A heap of drawings taken off the fridge: pages lying every which way, each with a scribble
	/// of a drawing on it and the odd magnet - the other fighters' pages, by their colours.
	/// </summary>
	void DrawPile(float x, float floor)
	{
		Color ink = new Color(0.19f, 0.31f, 0.61f);
		Color[] doodles = { new Color(0.97f, 0.55f, 0.2f), new Color(0.98f, 0.85f, 0.2f), new Color(0.5f, 0.78f, 0.45f),
			new Color(0.86f, 0.3f, 0.3f), new Color(0.4f, 0.6f, 0.95f), new Color(0.75f, 0.5f, 0.9f) };
		for (int i = 0; i < 9; i++)
		{
			float w = 150.0f + 40.0f * Mathf.Abs(CrayonBrush.Noise(i, 401));
			float h = w * 0.75f;
			float layer = i / 3;
			Vector2 centre = new Vector2(x + CrayonBrush.Noise(i, 402) * 120.0f, floor - h * 0.3f - layer * 34.0f);
			float angle = CrayonBrush.Noise(i, 403) * 0.5f;
			DrawSetTransform(centre, angle, Vector2.One);
			var page = new Rect2(-w * 0.5f, -h * 0.5f, w, h);
			DrawRect(new Rect2(page.Position + new Vector2(6.0f, 8.0f), page.Size), new Color(0.3f, 0.3f, 0.4f, 0.18f));
			DrawRect(page, new Color(0.98f, 0.97f, 0.93f));
			CrayonBrush.InkRect(this, page, new Color(ink, 0.7f), 2.5f, 410 + i, 1.4f);
			// The drawing on it: a scribbled figure in one of their colours.
			Color c = doodles[i % doodles.Length];
			CrayonBrush.Scribble(this, new Vector2(0.0f, -h * 0.05f), new Vector2(w * 0.45f, h * 0.55f), c, 420 + i);
			if (i % 3 == 0) DrawCircle(new Vector2(-w * 0.35f, -h * 0.4f), 13.0f, new Color(0.9f, 0.3f, 0.3f));
		}
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>The drawing pinned up: dropped onto the door, a magnet clacked on, a crayon DoomBot on it.</summary>
	void DrawPinnedPage(Vector2 at)
	{
		pageFrames++;
		float t = Mathf.Min(1.0f, pageFrames / 10.0f);
		Vector2 centre = at + new Vector2(0.0f, -60.0f * (1.0f - t));
		DrawSetTransform(centre, -0.05f, Vector2.One);
		var page = new Rect2(-130.0f, -170.0f, 260.0f, 340.0f);
		DrawRect(new Rect2(page.Position + new Vector2(8.0f, 10.0f), page.Size), new Color(0.3f, 0.3f, 0.4f, 0.2f));
		DrawRect(page, new Color(0.99f, 0.98f, 0.95f));
		CrayonBrush.InkRect(this, page, new Color(0.19f, 0.31f, 0.61f, 0.7f), 2.5f, 501, 1.4f);
		StoryPictures.Draw(this, "doombot", new Vector2(0.0f, 20.0f), 200.0f, new Color(0.19f, 0.31f, 0.61f), 1.0f, 502);
		CrayonBrush.Scribble(this, new Vector2(-60.0f, 130.0f), new Vector2(60.0f, 16.0f), new Color(0.5f, 0.78f, 0.45f), 503, 6.0f, 6);
		DrawCircle(new Vector2(0.0f, -150.0f), 22.0f, new Color(0.92f, 0.32f, 0.3f));
		DrawCircle(new Vector2(-7.0f, -157.0f), 6.0f, new Color(1, 1, 1, 0.6f));
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	/// <summary>A red eye opening in the dark up on the door - DoomBot, before anyone sees him.</summary>
	void DrawEye()
	{
		float open = eyeOpenness * eyeOpenness * (3.0f - 2.0f * eyeOpenness);
		float pulse = 0.8f + 0.2f * Mathf.Sin(frames * 0.15f);
		DrawCircle(eyeAt, 90.0f * open * pulse, new Color(1.0f, 0.3f, 0.25f, 0.18f * open));
		DrawCircle(eyeAt, 50.0f * open * pulse, new Color(1.0f, 0.35f, 0.3f, 0.3f * open));
		DrawSetTransform(eyeAt, 0.0f, new Vector2(1.0f, Mathf.Max(0.05f, open)));
		DrawCircle(Vector2.Zero, 30.0f, new Color(0.95f, 0.25f, 0.22f));
		DrawCircle(Vector2.Zero, 13.0f, new Color(1.0f, 0.8f, 0.55f));
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	Vector2 WorldOf(Written line, int letter)
	{
		Font font = ThemeDB.FallbackFont;
		int row = lines.IndexOf(line);
		float x = 0.0f;
		for (int i = 0; i < letter && i < line.Text.Length; i++) x += font.GetCharSize(line.Text[i], FontSize).X;
		return anchor + new Vector2(x, row * LineGap);
	}

	public override void _Draw()
	{
		if (darkness > 0.0f) DrawRect(new Rect2(-6000.0f, -6000.0f, 12000.0f, 12000.0f), new Color(0.24f, 0.21f, 0.38f, 0.6f * darkness));
		if (pileX.HasValue) DrawPile(pileX.Value, pileFloor);
		if (eyeOpenness > 0.0f) DrawEye();
		if (pageAt.HasValue) DrawPinnedPage(pageAt.Value);
		Font font = ThemeDB.FallbackFont;
		for (int row = 0; row < lines.Count; row++)
		{
			Written line = lines[row];
			float x = 0.0f;
			int shown = (int)line.Shown;
			for (int i = 0; i < shown; i++)
			{
				string ch = line.Text[i].ToString();
				// Burnt in by hand, so no two letters sit quite level.
				Vector2 at = anchor + new Vector2(x, row * LineGap + CrayonBrush.Noise(row * 31 + 5, i) * 4.0f);
				bool fresh = i >= shown - 2 && shown < line.Text.Length;
				DrawChar(font, at, ch, FontSize, new Color(fresh ? Glow : Burn, fade));
				x += font.GetCharSize(line.Text[i], FontSize).X;
			}

			// The laser, from his eye to the letter going on.
			if (shown < line.Text.Length && line.Writer != null && IsInstanceValid(line.Writer))
			{
				Vector2 eye = line.Writer.BeamOrigin();
				Vector2 tip = anchor + new Vector2(x, row * LineGap - FontSize * 0.3f);
				tip += new Vector2(CrayonBrush.Noise(frames, 1), CrayonBrush.Noise(frames, 2)) * 5.0f;
				DrawLine(eye, tip, new Color(Glow, 0.35f), 16.0f, true);
				DrawLine(eye, tip, new Color(Burn, 0.9f), 5.0f, true);
				DrawCircle(tip, 11.0f + Mathf.Abs(CrayonBrush.Noise(frames, 3)) * 5.0f, new Color(Glow, 0.8f));
			}
		}
	}
}
