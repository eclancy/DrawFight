using System.Collections.Generic;
using Godot;

/// <summary>
/// The options, from the gear on the main menu (Eric, 2026-10-17): fullscreen or a window, music
/// and sound volume, the pad's button layout, screen shake and how many lives a versus fight
/// starts with - each a button that steps through its choices - and the names people have given
/// themselves on this computer, any of which can be deleted. Saved as they change (GameOptions,
/// PlayerNames). Controls lists every key and button (ControlsScreen). B, or Back, returns to the
/// main menu.
/// </summary>
public partial class OptionsScreen : Node2D
{
	/// <summary>The controller that opened the options; its cursor is showing from the start.</summary>
	public string OpenedBy;

	enum Row { Screen, Music, Sound, Buttons, Shake, Stocks }

	static readonly string[] RowNames = { "Screen", "Music", "Sounds", "Pad buttons", "Screen shake", "Lives (versus)" };

	static readonly Rect2 BackBox = new Rect2(190.0f, 950.0f, 240.0f, 76.0f);

	/// <summary>What changed in each version (ChangelogScreen).</summary>
	static readonly Rect2 NewsBox = new Rect2(560.0f, 852.0f, 380.0f, 72.0f);

	/// <summary>What every key and button does (ControlsScreen).</summary>
	static readonly Rect2 ControlsBox = new Rect2(560.0f, 950.0f, 380.0f, 76.0f);
	const float RowTop = 250.0f;
	const float RowStep = 96.0f;
	const float NamesLeft = 1060.0f;
	const int NameColumns = 2;
	const int NameRows = 7;

	TouchCursors touch;
	Node2D cursorLayer;
	bool leaving;

	/// <summary>A name pressed once: pressed again, it is deleted.</summary>
	string confirmDelete;

	public override void _Ready()
	{
		touch = new TouchCursors(new Vector2(960.0f, 1000.0f), OpenedBy);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in touch.Shown) cursor.Draw(canvas);
		});
	}

	static Rect2 ValueBox(Row row) => new Rect2(560.0f, RowTop + (int)row * RowStep, 380.0f, 72.0f);

	static Rect2 NameBox(int i) => new Rect2(NamesLeft + i % NameColumns * 350.0f, RowTop + 60.0f + i / NameColumns * 84.0f, 330.0f, 68.0f);

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;
		touch.Tick();
		foreach (MenuCursor cursor in touch.Shown)
		{
			if (leaving) return;
			cursor.Update(viewport);
			if (cursor.Cancel)
			{
				Leave(cursor);
				return;
			}
			if (cursor.Confirm) Press(cursor);
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Press(MenuCursor cursor)
	{
		Vector2 point = cursor.Position;
		if (BackBox.HasPoint(point))
		{
			Leave(cursor);
			return;
		}
		if (ControlsBox.HasPoint(point))
		{
			leaving = true;
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.GoControls(touch.KeyOf(cursor));
			return;
		}
		if (NewsBox.HasPoint(point))
		{
			leaving = true;
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.GoChangelog(touch.KeyOf(cursor));
			return;
		}

		for (int r = 0; r < RowNames.Length; r++)
		{
			if (!ValueBox((Row)r).HasPoint(point)) continue;
			Step((Row)r);
			GameOptions.Save();
			GameOptions.Apply(true);
			SfxPlayer.Ui("ui_select");
			return;
		}

		for (int i = 0; i < PlayerNames.All.Count && i < NameColumns * NameRows; i++)
		{
			if (!NameBox(i).HasPoint(point)) continue;
			string name = PlayerNames.All[i];
			if (confirmDelete == name)
			{
				PlayerNames.Delete(name);
				confirmDelete = null;
				SfxPlayer.Ui("ui_back");
			}
			else
			{
				confirmDelete = name;
				SfxPlayer.Ui("ui_select");
			}
			return;
		}
		confirmDelete = null;
	}

	static void Step(Row row)
	{
		switch (row)
		{
			case Row.Screen: GameOptions.Fullscreen = !GameOptions.Fullscreen; break;
			case Row.Music: GameOptions.MusicVolume = (GameOptions.MusicVolume + 1) % 4; break;
			case Row.Sound: GameOptions.SoundVolume = (GameOptions.SoundVolume + 1) % 4; break;
			case Row.Buttons: GameOptions.SmashLayout = !GameOptions.SmashLayout; break;
			case Row.Shake: GameOptions.Shake = (GameOptions.Shake + 2) % 3; break;
			case Row.Stocks:
				GameOptions.Stocks = GameOptions.Stocks >= GameOptions.MaxStocks ? GameOptions.MinStocks : GameOptions.Stocks + 1;
				break;
		}
	}

	static string ValueOf(Row row) => row switch
	{
		Row.Screen => GameOptions.Fullscreen ? "Fullscreen" : "Window",
		Row.Music => GameOptions.DescribeVolume(GameOptions.MusicVolume),
		Row.Sound => GameOptions.DescribeVolume(GameOptions.SoundVolume),
		Row.Buttons => GameOptions.SmashLayout ? "Smash" : "Standard",
		Row.Shake => GameOptions.DescribeShake(GameOptions.Shake),
		_ => GameOptions.Stocks.ToString(),
	};

	void Leave(MenuCursor cursor)
	{
		leaving = true;
		SfxPlayer.Ui("ui_back");
		GameRoot.Instance.MenuReturnKey = touch.KeyOf(cursor);
		GameRoot.Instance.GoMainMenu();
	}

	bool Hot(Rect2 box)
	{
		foreach (MenuCursor cursor in touch.Shown) if (box.HasPoint(cursor.Position)) return true;
		return false;
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(190.0f, 170.0f), "Options", HorizontalAlignment.Left, -1, 96, MenuTheme.Text);
		MainMenuScreen.DrawGear(this, new Vector2(620.0f, 136.0f), 30.0f, MenuTheme.Ink);

		for (int r = 0; r < RowNames.Length; r++)
		{
			Rect2 box = ValueBox((Row)r);
			DrawString(font, new Vector2(190.0f, box.GetCenter().Y + 14.0f), RowNames[r], HorizontalAlignment.Left, -1, 40, MenuTheme.Text);
			MenuTheme.DrawButton(this, box, ValueOf((Row)r), 300 + r * 7, Hot(box), fontSize: 36);
		}

		// The names saved here: press one to delete it, and again to be sure.
		DrawString(font, new Vector2(NamesLeft, RowTop + 30.0f), "Names", HorizontalAlignment.Left, -1, 40, MenuTheme.Text);
		if (PlayerNames.All.Count == 0)
		{
			DrawMultilineString(font, new Vector2(NamesLeft, RowTop + 110.0f), "No names yet. Give yourself one on Pick your fighter.",
				HorizontalAlignment.Left, 690.0f, 30, -1, MenuTheme.Soft);
		}
		for (int i = 0; i < PlayerNames.All.Count && i < NameColumns * NameRows; i++)
		{
			Rect2 box = NameBox(i);
			string name = PlayerNames.All[i];
			bool asking = confirmDelete == name;
			MenuTheme.DrawButton(this, box, asking ? "Delete?" : name, 400 + i * 5, Hot(box), chosen: asking, fontSize: 32);
			if (!asking)
			{
				// A small cross: pressing a name deletes it.
				Vector2 x = new Vector2(box.End.X - 26.0f, box.GetCenter().Y);
				DrawLine(x + new Vector2(-9, -9), x + new Vector2(9, 9), MenuTheme.Soft, 3.0f, true);
				DrawLine(x + new Vector2(-9, 9), x + new Vector2(9, -9), MenuTheme.Soft, 3.0f, true);
			}
		}
		if (PlayerNames.All.Count > NameColumns * NameRows)
		{
			DrawString(font, new Vector2(NamesLeft, NameBox(NameColumns * NameRows - 1).End.Y + 40.0f),
				$"and {PlayerNames.All.Count - NameColumns * NameRows} more", HorizontalAlignment.Left, -1, 28, MenuTheme.Soft);
		}

		MenuTheme.DrawButton(this, BackBox, "Back", 390, Hot(BackBox), fontSize: 38);
		DrawString(font, new Vector2(190.0f, NewsBox.GetCenter().Y + 14.0f), $"Version {UpdateScreen.CurrentVersion}", HorizontalAlignment.Left, -1, 40, MenuTheme.Text);
		MenuTheme.DrawButton(this, NewsBox, "What's new", 392, Hot(NewsBox), fontSize: 36);
		MenuTheme.DrawButton(this, ControlsBox, "Controls", 394, Hot(ControlsBox), fontSize: 36);
	}
}
