using Godot;

/// <summary>
/// Shared look for every screen outside a match. Menus inherit the same value ladder as the
/// stages - light paper, ink text, black reserved for the fighters themselves. See
/// .ai/art-direction.md.
/// </summary>
public static class MenuTheme
{
	public static readonly Color Paper = new Color(0.965f, 0.960f, 0.940f);

	/// <summary>
	/// Every screen's Back button, in the one place - the top left, above its title - so it is
	/// never mistaken for one of the screen's own choices (Eric, 2026-10-24: on the options it sat
	/// at the bottom, among them).
	/// </summary>
	public static readonly Rect2 BackBox = new Rect2(190.0f, 30.0f, 160.0f, 66.0f);
	public static readonly Color PaperEdge = new Color(0.925f, 0.930f, 0.920f);
	public static readonly Color Rule = new Color(0.78f, 0.855f, 0.90f);
	public static readonly Color Margin = new Color(0.91f, 0.66f, 0.72f);
	public static readonly Color Ink = new Color(0.19f, 0.31f, 0.61f);
	public static readonly Color Text = new Color(0.16f, 0.16f, 0.20f);
	public static readonly Color Soft = new Color(0.44f, 0.46f, 0.52f);
	public static readonly Color Accent = new Color(0.96f, 0.62f, 0.32f);
	public static readonly Color AccentTwo = new Color(0.36f, 0.72f, 0.98f);

	/// <summary>
	/// A team's colour in a team fight - red, blue, green - on the team's panels, tags and
	/// shields. Light, like every player colour.
	/// </summary>
	public static Color TeamTint(int team) => (team % 3) switch
	{
		0 => new Color(0.94f, 0.42f, 0.40f),
		1 => new Color(0.36f, 0.62f, 0.96f),
		_ => new Color(0.36f, 0.74f, 0.36f),
	};

	public static string TeamName(int team) => (team % 3) switch { 0 => "Red", 1 => "Blue", _ => "Green" };

	/// <summary>
	/// Each player's colour, by seat - on their cursor, their HUD tag, their tag over their head in
	/// a match, their shield: blue, orange, green, pink. Light, like everything outside a fighter
	/// (.ai/art-direction.md), and far enough apart to tell four of them apart across a room.
	/// </summary>
	public static Color PlayerTint(int seat) => (seat % 4) switch
	{
		0 => AccentTwo,
		1 => Accent,
		2 => new Color(0.40f, 0.78f, 0.38f),
		_ => new Color(0.88f, 0.48f, 0.82f),
	};

	/// <summary>The ruled page every menu sits on, drawn to fill a viewport of any size.</summary>
	public static void DrawPage(CanvasItem canvas, Vector2 size)
	{
		canvas.DrawRect(new Rect2(Vector2.Zero, size), Paper);

		for (float y = 90.0f; y < size.Y; y += 74.0f)
		{
			CrayonBrush.InkLine(canvas, new Vector2(0.0f, y), new Vector2(size.X, y),
				Rule, 2.0f, Mathf.RoundToInt(y), 1.2f);
		}

		CrayonBrush.InkLine(canvas, new Vector2(150.0f, 0.0f), new Vector2(150.0f, size.Y),
			Margin, 3.0f, 991, 2.0f);
	}

	/// <summary>
	/// A crayon button: paper fill and an ink outline, a thicker coloured outline round it while
	/// something points at it, and blue when it is the one chosen. A button that cannot be used
	/// right now is greyed, but still drawn, so it can say why when it is pressed.
	/// </summary>
	public static void DrawButton(CanvasItem canvas, Rect2 rect, string text, int seed, bool hot = false,
		bool chosen = false, bool enabled = true, int fontSize = 32, Color? hotTint = null)
	{
		Color fill = !enabled ? PaperEdge : chosen ? AccentTwo.Lightened(0.55f) : new Color(1.0f, 1.0f, 1.0f, 0.94f);
		canvas.DrawRect(rect, fill);
		Color ink = !enabled ? Rule : chosen ? AccentTwo.Darkened(0.25f) : Ink;
		CrayonBrush.InkRect(canvas, rect, ink, chosen ? 5.0f : 3.5f, seed, 2.0f);
		if (hot) CrayonBrush.InkRect(canvas, rect.Grow(6.0f), hotTint ?? Accent, 5.0f, seed + 50, 2.4f);
		if (string.IsNullOrEmpty(text)) return;
		DrawCentredText(canvas, rect, text, fontSize, enabled ? Text : Soft);
	}

	/// <summary>One button of a set drawn together (<see cref="DrawButtons"/>).</summary>
	public readonly record struct ButtonSpec(Rect2 Rect, string Text, int Seed, bool Hot, bool Chosen, int FontSize, Color? HotTint);

	/// <summary>
	/// Many buttons at once, looking exactly as <see cref="DrawButton"/> draws each: every fill,
	/// then every outline, then every label. Drawn one button at a time, fill, ink and text took
	/// turns, so none of them could be batched - the name picker's thirty-odd keys took the menu
	/// down to a few frames a second (Eric, 2026-10-24: "seems to lag the screen").
	/// </summary>
	public static void DrawButtons(CanvasItem canvas, System.Collections.Generic.IReadOnlyList<ButtonSpec> buttons)
	{
		foreach (ButtonSpec b in buttons)
			canvas.DrawRect(b.Rect, b.Chosen ? AccentTwo.Lightened(0.55f) : new Color(1.0f, 1.0f, 1.0f, 0.94f));
		foreach (ButtonSpec b in buttons)
		{
			CrayonBrush.InkRect(canvas, b.Rect, b.Chosen ? AccentTwo.Darkened(0.25f) : Ink, b.Chosen ? 5.0f : 3.5f, b.Seed, 2.0f);
			if (b.Hot) CrayonBrush.InkRect(canvas, b.Rect.Grow(6.0f), b.HotTint ?? Accent, 5.0f, b.Seed + 50, 2.4f);
		}
		foreach (ButtonSpec b in buttons)
			if (!string.IsNullOrEmpty(b.Text)) DrawCentredText(canvas, b.Rect, b.Text, b.FontSize, Text);
	}

	/// <summary>Text centred in a box, both ways.</summary>
	public static void DrawCentredText(CanvasItem canvas, Rect2 rect, string text, int fontSize, Color colour)
	{
		Font font = ThemeDB.FallbackFont;
		float baseline = rect.GetCenter().Y + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) * 0.5f;
		canvas.DrawString(font, new Vector2(rect.Position.X, baseline), text, HorizontalAlignment.Center, rect.Size.X, fontSize, colour);
	}

	public static Label MakeLabel(string text, Vector2 position, int size, Color colour)
	{
		var label = new Label { Text = text, Position = position };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		return label;
	}
}

/// <summary>
/// Turns a fighter's <see cref="IInputSource"/> into menu navigation: discrete steps from an
/// analogue stick, plus confirm and cancel edges.
///
/// Menus use the same input seam as the fight so that a pad which works in a match works in the
/// menus without a second mapping to keep in step. Confirm is the jump button and cancel is the
/// special button, which on the pad layout means A confirms and B goes back - the convention
/// every console player already has in their hands.
/// </summary>
public sealed class MenuNav
{
	const int FirstRepeatFrames = 22;
	const int NextRepeatFrames = 7;
	const float Threshold = 0.55f;

	readonly IInputSource source;
	int heldDirection;

	/// <summary>What drives it: a pad, the keyboard.</summary>
	public IInputSource Source => source;
	int repeatCountdown;

	public MenuNav(IInputSource source)
	{
		this.source = source;
	}

	public int StepX { get; private set; }
	public int StepY { get; private set; }
	public bool Confirm { get; private set; }
	public bool Cancel { get; private set; }
	public bool Start { get; private set; }

	/// <summary>Confirm held down, not just pressed: on the drawing screen, A held draws.</summary>
	public bool Held { get; private set; }

	/// <summary>The raw stick this frame, for a cursor that moves with it rather than in steps.</summary>
	public Vector2 Stick { get; private set; }

	/// <summary>True if any button at all was pressed this frame. Used by the title screen.</summary>
	public bool AnyPress => Confirm || Cancel || Start;

	public void Poll()
	{
		InputState input = source?.Poll() ?? InputState.None;

		Confirm = input.JumpPressed;
		Held = input.JumpHeld;
		Cancel = input.SpecialPressed;
		Start = input.StartPressed;
		Stick = input.Move;

		StepX = 0;
		StepY = 0;

		int dirX = Mathf.Abs(input.Move.X) > Threshold ? Mathf.Sign(input.Move.X) : 0;
		int dirY = Mathf.Abs(input.Move.Y) > Threshold ? Mathf.Sign(input.Move.Y) : 0;
		int dir = dirX != 0 ? dirX : dirY * 2;

		if (dir == 0)
		{
			heldDirection = 0;
			repeatCountdown = 0;
			return;
		}

		// A fresh push steps immediately; holding repeats, slowly at first. Without this a
		// stick either steps once and stops, or scrolls the whole list in three frames.
		if (dir != heldDirection)
		{
			heldDirection = dir;
			repeatCountdown = FirstRepeatFrames;
			StepX = dirX;
			StepY = dirY;
			return;
		}

		if (--repeatCountdown > 0) return;

		repeatCountdown = NextRepeatFrames;
		StepX = dirX;
		StepY = dirY;
	}
}
