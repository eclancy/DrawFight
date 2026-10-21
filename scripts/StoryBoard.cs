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

	TakenDownPile pile;

	public MatchManager Match;

	float? tableX;
	float tableFloor;

	/// <summary>
	/// The stools people sat down on, by who sat on each: where its seat is and how wide. Kept once
	/// they get up, so standing to fight does not take the furniture away with them.
	/// </summary>
	readonly Dictionary<Fighter, (Vector2 seat, float width)> stools = new Dictionary<Fighter, (Vector2, float)>();

	public void OpenEye(Vector2 at)
	{
		eyeAt = at;
		eyeOpen = true;
	}

	public void CloseEye() => eyeOpen = false;

	/// <summary>Where the red eye last opened in the dark - "drop from eye" brings him in there.</summary>
	public Vector2 EyeAt => eyeAt;

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

	/// <summary>
	/// The heap of taken-down drawings on the floor at x, or none: the other fighters' own
	/// drawings, everyone's but DoomBot's and <paramref name="playerId"/>'s (TakenDownPile).
	/// </summary>
	public void Pile(float? x, float floorY, string playerId)
	{
		pile?.Free();
		pile = null;
		if (x == null) return;
		pile = new TakenDownPile();
		pile.Build(this, playerId, x.Value, floorY);
	}

	/// <summary>A table set for lunch on the floor at x, or none - the stools go with it.</summary>
	public void Table(float? x, float floorY)
	{
		tableX = x;
		tableFloor = floorY;
		if (x == null) stools.Clear();
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

	static readonly Color FurnitureInk = new Color(0.19f, 0.31f, 0.61f);
	static readonly Color Wood = new Color(0.86f, 0.66f, 0.42f);

	/// <summary>
	/// Lunch: a little wooden table with a lunch pail, half a sandwich on its paper, a creme
	/// brulee in its dish and two cups - and under everyone sitting at it, a stool, at the height
	/// their hips came down to.
	/// </summary>
	void DrawLunch(float x, float floor)
	{
		if (Match != null)
		{
			foreach (Fighter fighter in Match.Fighters)
			{
				if (!IsInstanceValid(fighter)) continue;
				(Vector2 seat, float width)? sat = fighter.SeatNow();
				if (sat.HasValue) stools[fighter] = sat.Value;
			}
		}
		int n = 0;
		foreach ((Vector2 seat, float width) in stools.Values) DrawStool(seat, width, floor, 700 + 10 * n++);

		const float Height = 74.0f, Width = 210.0f, Top = 14.0f;
		float top = floor - Height;
		// Legs, then the top over them.
		foreach (float side in new[] { -1.0f, 1.0f })
		{
			var leg = new Rect2(x + side * (Width * 0.5f - 22.0f) - 6.0f, top + Top, 12.0f, Height - Top);
			DrawRect(leg, Wood.Darkened(0.12f));
			CrayonBrush.PencilFill(this, leg, Wood.Darkened(0.3f), 731 + (int)side, 4.0f, 1.6f);
			CrayonBrush.InkRect(this, leg, FurnitureInk, 2.2f, 735 + (int)side, 1.2f);
		}
		var board = new Rect2(x - Width * 0.5f, top, Width, Top);
		DrawRect(board, Wood);
		CrayonBrush.PencilFill(this, board, Wood.Darkened(0.25f), 741, 4.0f, 1.6f);
		CrayonBrush.InkRect(this, board, FurnitureInk, 2.5f, 745, 1.4f);

		// The lunch pail: Lugnut's, yellow, with its handle up.
		var pail = new Rect2(x + 22.0f, top - 34.0f, 54.0f, 34.0f);
		DrawRect(pail, new Color(0.98f, 0.80f, 0.30f));
		CrayonBrush.PencilFill(this, pail, new Color(0.92f, 0.62f, 0.18f), 751, 4.0f, 1.6f);
		CrayonBrush.InkRect(this, pail, FurnitureInk, 2.2f, 755, 1.2f);
		DrawArc(new Vector2(pail.GetCenter().X, pail.Position.Y), 16.0f, Mathf.Pi, Mathf.Tau, 12, FurnitureInk, 2.2f, true);

		// Half a sandwich on its paper.
		var paper = new Rect2(x + 76.0f, top - 3.0f, 26.0f, 3.0f);
		DrawRect(paper, new Color(0.97f, 0.96f, 0.92f));
		Vector2[] half = { new Vector2(x + 80.0f, top - 3.0f), new Vector2(x + 100.0f, top - 3.0f), new Vector2(x + 80.0f, top - 23.0f) };
		DrawColoredPolygon(half, new Color(0.96f, 0.86f, 0.62f));
		DrawLine(half[0] + new Vector2(4.0f, -4.0f), half[2] + new Vector2(4.0f, 6.0f), new Color(0.98f, 0.88f, 0.36f), 4.0f, true);
		DrawPolyline(new[] { half[0], half[1], half[2], half[0] }, FurnitureInk, 2.0f, true);

		// The creme brulee: Flambe's, in its white dish, the top torched brown.
		var dish = new Rect2(x - 78.0f, top - 16.0f, 44.0f, 16.0f);
		DrawRect(dish, new Color(0.99f, 0.98f, 0.95f));
		DrawRect(new Rect2(dish.Position.X + 3.0f, dish.Position.Y, dish.Size.X - 6.0f, 5.0f), new Color(0.88f, 0.60f, 0.28f));
		CrayonBrush.InkRect(this, dish, FurnitureInk, 2.0f, 761, 1.0f);

		// Two cups.
		foreach ((float at, Color colour) in new[] { (x - 26.0f, new Color(0.92f, 0.42f, 0.36f)), (x - 104.0f, new Color(0.45f, 0.66f, 0.92f)) })
		{
			var cup = new Rect2(at - 9.0f, top - 22.0f, 18.0f, 22.0f);
			DrawRect(cup, colour);
			CrayonBrush.InkRect(this, cup, FurnitureInk, 2.0f, 770 + (int)at, 1.0f);
		}
	}

	/// <summary>A wooden stool: a seat, two splayed legs and a rung, from the floor up to <paramref name="seat"/>.</summary>
	void DrawStool(Vector2 seat, float width, float floor, int seed)
	{
		var top = new Rect2(seat.X - width * 0.5f, seat.Y, width, 10.0f);
		float legTop = top.End.Y;
		foreach (float side in new[] { -1.0f, 1.0f })
		{
			Vector2 from = new Vector2(seat.X + side * width * 0.28f, legTop);
			Vector2 to = new Vector2(seat.X + side * width * 0.42f, floor);
			DrawLine(from, to, FurnitureInk, 9.0f, true);
			DrawLine(from, to, Wood, 5.0f, true);
		}
		float rung = Mathf.Lerp(legTop, floor, 0.55f);
		CrayonBrush.InkLine(this, new Vector2(seat.X - width * 0.34f, rung), new Vector2(seat.X + width * 0.34f, rung), FurnitureInk, 3.0f, seed, 1.0f);
		DrawRect(top, Wood);
		CrayonBrush.PencilFill(this, top, Wood.Darkened(0.25f), seed + 1, 4.0f, 1.6f);
		CrayonBrush.InkRect(this, top, FurnitureInk, 2.2f, seed + 2, 1.0f);
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
		pile?.Draw(this);
		if (tableX.HasValue) DrawLunch(tableX.Value, tableFloor);
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
