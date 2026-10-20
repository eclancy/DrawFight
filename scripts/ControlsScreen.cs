using Godot;

/// <summary>
/// Controls, from Options (Eric, 2026-10-19): what every key, click and button does - the
/// keyboard and mouse, the second keyboard player on the number pad, and a pad in whichever
/// layout Options has it in. Read off the same bindings the input sources use, written out by
/// hand, so a change to InputSource.cs or GamepadLayout changes this list too. B goes back.
/// </summary>
public partial class ControlsScreen : Node2D
{
	public string OpenedBy;

	static readonly Rect2 BackBox = new Rect2(190.0f, 950.0f, 240.0f, 76.0f);

	TouchCursors touch;
	Node2D cursorLayer;
	bool leaving;

	/// <summary>Player 1 on the keyboard and mouse (KeyboardInputSource.Player1).</summary>
	public static readonly (string action, string keys)[] KeyboardControls =
	{
		("Move", "W A S D or arrows"),
		("Jump", "Space"),
		("Attack", "Left click or J"),
		("Special", "Right click or K"),
		("Block", "Shift or L"),
		("Grab", "E or middle click"),
		("Taunt", "Q"),
		("Smash", "tap a way + attack"),
		("Drop down", "S + Space"),
		("Pause", "Enter"),
	};

	/// <summary>Player 2 on the number pad (KeyboardInputSource.Player2).</summary>
	static readonly (string action, string keys)[] NumberPadControls =
	{
		("Move", "8 4 5 6"),
		("Jump", "1"),
		("Attack", "2"),
		("Special", "3"),
		("Block", "0"),
		("Grab", "9"),
		("Taunt", "7"),
		("Pause", "Enter"),
	};

	/// <summary>A pad, in the layout Options has it in (GamepadLayout.Standard or .Smash).</summary>
	static (string action, string keys)[] PadControls() => GameOptions.SmashLayout
		? new[]
		{
			("Move", "left stick"),
			("Jump", "X or Y"),
			("Attack", "A"),
			("Special", "B"),
			("Block", "RB or RT"),
			("Grab", "LB"),
			("Taunt", "Back"),
			("Smash", "flick + A, or right stick"),
			("Drop down", "down + X"),
			("Pause", "Start"),
		}
		: new[]
		{
			("Move", "left stick"),
			("Jump", "A or Y"),
			("Attack", "X"),
			("Special", "B"),
			("Block", "LT or RT"),
			("Grab", "RB"),
			("Taunt", "LB"),
			("Smash", "flick + X, or right stick"),
			("Drop down", "down + A"),
			("Pause", "Start"),
		};

	public override void _Ready()
	{
		touch = new TouchCursors(new Vector2(960.0f, 1000.0f), OpenedBy);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in touch.Shown) cursor.Draw(canvas);
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;
		touch.Tick();
		foreach (MenuCursor cursor in touch.Shown)
		{
			if (leaving) return;
			cursor.Update(viewport);
			if (cursor.Cancel || cursor.Confirm && BackBox.HasPoint(cursor.Position))
			{
				leaving = true;
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoOptions(touch.KeyOf(cursor));
				return;
			}
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void DrawColumn(float left, string heading, (string action, string keys)[] rows)
	{
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(left, 290.0f), heading, HorizontalAlignment.Left, -1, 42, MenuTheme.Text);
		CrayonBrush.InkLine(this, new Vector2(left, 308.0f), new Vector2(left + 500.0f, 308.0f), MenuTheme.Ink, 3.0f, (int)left, 1.2f);
		for (int i = 0; i < rows.Length; i++)
		{
			float y = 370.0f + i * 54.0f;
			DrawString(font, new Vector2(left, y), rows[i].action, HorizontalAlignment.Left, -1, 30, MenuTheme.Soft);
			DrawString(font, new Vector2(left + 190.0f, y), rows[i].keys, HorizontalAlignment.Left, 340.0f, 30, MenuTheme.Text);
		}
	}

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(190.0f, 170.0f), "Controls", HorizontalAlignment.Left, -1, 96, MenuTheme.Text);

		DrawColumn(190.0f, "Keyboard and mouse", KeyboardControls);
		DrawColumn(770.0f, "Controller", PadControls());
		DrawColumn(1350.0f, "2nd keyboard player", NumberPadControls);

		DrawString(font, new Vector2(770.0f, 970.0f), "In menus: Space or A picks, K or B goes back.",
			HorizontalAlignment.Left, -1, 30, MenuTheme.Soft);
		DrawString(font, new Vector2(770.0f, 1012.0f), "Hold block and push a way to roll or dodge.",
			HorizontalAlignment.Left, -1, 30, MenuTheme.Soft);

		bool hot = false;
		foreach (MenuCursor cursor in touch.Shown) hot |= BackBox.HasPoint(cursor.Position);
		MenuTheme.DrawButton(this, BackBox, "Back", 390, hot, fontSize: 38);
	}
}
