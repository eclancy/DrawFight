using Godot;

/// <summary>
/// One player's pointer on a select screen, moved with the stick and clicked with A - the way
/// Smash does it. A cursor means nothing on screen has to explain which button goes where:
/// you point at the thing you want and press the button every player already reaches for.
///
/// Speed follows how far the stick is pushed, like movement in a match, so a light push lines
/// up on a small target and a full push crosses the screen in about a second.
/// </summary>
public sealed class MenuCursor
{
	public readonly MenuNav Nav;
	public readonly int Player;
	public readonly Color Tint;
	public Vector2 Position;

	/// <summary>Pixels per frame at full deflection.</summary>
	const float Speed = 24.0f;

	const float Radius = 26.0f;

	public MenuCursor(IInputSource source, int player, Color tint, Vector2 start)
	{
		Nav = new MenuNav(source);
		Player = player;
		Tint = tint;
		Position = start;
	}

	public bool Confirm => Nav.Confirm || Nav.Start;
	public bool Cancel => Nav.Cancel;

	/// <summary>Polls the controller and moves. Call once per physics frame.</summary>
	public void Update(Vector2 viewport)
	{
		Nav.Poll();
		Vector2 stick = Nav.Stick;
		if (stick.LengthSquared() > 1.0f) stick = stick.Normalized();

		Position += stick * Speed;
		Position = new Vector2(
			Mathf.Clamp(Position.X, 4.0f, viewport.X - 4.0f),
			Mathf.Clamp(Position.Y, 4.0f, viewport.Y - 4.0f));
	}

	/// <summary>
	/// A layer above a screen's labels for its cursors to be drawn on, so a cursor is never
	/// hidden under the text it is pointing at. Redraw it every frame the cursors move.
	/// </summary>
	public static Node2D MakeOverlay(Node screen, System.Action<CanvasItem> paint)
	{
		var layer = new CanvasLayer { Layer = 10 };
		screen.AddChild(layer);
		var overlay = new Node2D();
		layer.AddChild(overlay);
		overlay.Draw += () => paint(overlay);
		return overlay;
	}

	/// <summary>A pointer tip that does the picking, trailed by a ring with the player number in it.</summary>
	public void Draw(CanvasItem canvas)
	{
		// The ring hangs below and to the right of the tip, so the tip stays visible on the
		// thing being pointed at and the ring never covers it.
		Vector2 centre = Position + new Vector2(Radius * 1.45f, Radius * 1.45f);

		canvas.DrawColoredPolygon(new[]
		{
			Position,
			Position + new Vector2(Radius * 1.05f, Radius * 0.35f),
			Position + new Vector2(Radius * 0.35f, Radius * 1.05f),
		}, Tint);
		canvas.DrawCircle(centre, Radius, new Color(1.0f, 1.0f, 1.0f, 0.9f));
		canvas.DrawArc(centre, Radius, 0.0f, Mathf.Tau, 28, Tint, 6.0f);
		canvas.DrawString(ThemeDB.FallbackFont, centre + new Vector2(-15.0f, 9.0f), $"P{Player + 1}",
			HorizontalAlignment.Left, -1, 24, MenuTheme.Text);
	}
}
