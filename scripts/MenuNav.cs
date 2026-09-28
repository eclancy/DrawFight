using Godot;

/// <summary>
/// Shared look for every screen outside a match. Menus inherit the same value ladder as the
/// stages - light paper, ink text, black reserved for the fighters themselves. See
/// .ai/art-direction.md.
/// </summary>
public static class MenuTheme
{
	public static readonly Color Paper = new Color(0.965f, 0.960f, 0.940f);
	public static readonly Color PaperEdge = new Color(0.925f, 0.930f, 0.920f);
	public static readonly Color Rule = new Color(0.78f, 0.855f, 0.90f);
	public static readonly Color Margin = new Color(0.91f, 0.66f, 0.72f);
	public static readonly Color Ink = new Color(0.19f, 0.31f, 0.61f);
	public static readonly Color Text = new Color(0.16f, 0.16f, 0.20f);
	public static readonly Color Soft = new Color(0.44f, 0.46f, 0.52f);
	public static readonly Color Accent = new Color(0.96f, 0.62f, 0.32f);
	public static readonly Color AccentTwo = new Color(0.36f, 0.72f, 0.98f);

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

	/// <summary>True if any button at all was pressed this frame. Used by the title screen.</summary>
	public bool AnyPress => Confirm || Cancel || Start;

	public void Poll()
	{
		InputState input = source?.Poll() ?? InputState.None;

		Confirm = input.JumpPressed;
		Cancel = input.SpecialPressed;
		Start = input.StartPressed;

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
