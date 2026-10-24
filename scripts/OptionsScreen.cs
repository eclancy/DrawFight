using System.Collections.Generic;
using Godot;

/// <summary>
/// The options, from the gear on the main menu (Eric, 2026-10-17): fullscreen or a window, music
/// and sound volume, the pad's button layout, screen shake and how many lives a versus fight
/// starts with - each a button that steps through its choices - and the names people have given
/// themselves on this computer, any of which can be deleted, and new ones added (Eric, 2026-10-24).
/// Saved as they change (GameOptions, PlayerNames). Controls lists every key and button
/// (ControlsScreen); the gallery shows every drawing sent in (GalleryScreen). B, or Back - top left,
/// where every screen has it - returns to the main menu.
/// </summary>
public partial class OptionsScreen : Node2D
{
	/// <summary>The controller that opened the options; its cursor is showing from the start.</summary>
	public string OpenedBy;

	enum Row { Screen, Music, Sound, Buttons, Shake, Stocks }

	static readonly string[] RowNames = { "Screen", "Music", "Sounds", "Pad buttons", "Screen shake", "Lives (versus)" };

	static Rect2 BackBox => MenuTheme.BackBox;

	/// <summary>What changed in each version (ChangelogScreen).</summary>
	static readonly Rect2 NewsBox = new Rect2(560.0f, 852.0f, 380.0f, 72.0f);

	/// <summary>What every key and button does (ControlsScreen).</summary>
	static readonly Rect2 ControlsBox = new Rect2(560.0f, 950.0f, 380.0f, 76.0f);

	/// <summary>Every drawing sent in, and who drew it (GalleryScreen).</summary>
	static readonly Rect2 GalleryBox = new Rect2(190.0f, 950.0f, 330.0f, 76.0f);

	static readonly string[] Keys =
	{
		"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O",
		"P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "SPACE", "DEL", "CLEAR", "OK",
	};
	const int KeyColumns = 6;
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

	/// <summary>A new name being spelled out, on the keys or typed on a keyboard - or null.</summary>
	string adding;

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

	/// <summary>Adding a name: after the names there are, while there is room for one.</summary>
	static Rect2 AddBox => NameBox(System.Math.Min(PlayerNames.All.Count, NameColumns * NameRows - 1));
	static bool CanAdd => PlayerNames.All.Count < NameColumns * NameRows;

	static readonly Rect2 FieldBox = new Rect2(NamesLeft, RowTop + 60.0f, 680.0f, 68.0f);

	static Rect2 KeyBox(int i)
	{
		const float Gap = 8.0f;
		float w = (FieldBox.Size.X - Gap * (KeyColumns - 1)) / KeyColumns;
		return new Rect2(NamesLeft + i % KeyColumns * (w + Gap), RowTop + 150.0f + i / KeyColumns * 72.0f, w, 62.0f);
	}

	bool IsKeyboard(MenuCursor cursor) => touch.KeyOf(cursor)?.StartsWith("keys:") == true;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;
		touch.Tick();
		foreach (MenuCursor cursor in touch.Shown)
		{
			if (leaving) return;
			// Spelling a name, a keyboard types it (_UnhandledInput): its keys drive nothing else.
			if (adding != null && IsKeyboard(cursor)) continue;
			cursor.Update(viewport);
			if (cursor.Cancel)
			{
				if (adding != null)
				{
					adding = null;
					SfxPlayer.Ui("ui_back");
					continue;
				}
				Leave(cursor);
				return;
			}
			if (cursor.Confirm) Press(cursor);
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (adding == null || e is not InputEventKey key || !key.Pressed || key.Echo) return;
		if (key.Keycode == Key.Escape) adding = null;
		else if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter) PressKey("OK");
		else if (key.Keycode == Key.Backspace) PressKey("DEL");
		else
		{
			char c = (char)key.Unicode;
			if (char.IsLetterOrDigit(c) || c == ' ' || c == '-') adding = PlayerNames.Typed(adding, c);
		}
		GetViewport().SetInputAsHandled();
	}

	void PressKey(string key)
	{
		switch (key)
		{
			case "OK":
				string name = adding.Trim();
				if (name.Length > 0) PlayerNames.Use(name);
				adding = null;
				SfxPlayer.Ui("ui_select");
				return;
			case "DEL": if (adding.Length > 0) adding = adding.Substring(0, adding.Length - 1); break;
			case "CLEAR": adding = ""; break;
			case "SPACE": adding = PlayerNames.Typed(adding, ' '); break;
			default: adding = PlayerNames.Typed(adding, key[0]); break;
		}
		SfxPlayer.Ui("ui_move");
	}

	void Press(MenuCursor cursor)
	{
		Vector2 point = cursor.Position;
		if (adding != null)
		{
			for (int i = 0; i < Keys.Length; i++)
			{
				if (KeyBox(i).HasPoint(point)) PressKey(Keys[i]);
			}
			return;
		}
		if (GalleryBox.HasPoint(point))
		{
			leaving = true;
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.GoGallery(touch.KeyOf(cursor));
			return;
		}
		if (CanAdd && AddBox.HasPoint(point))
		{
			adding = "";
			confirmDelete = null;
			SfxPlayer.Ui("ui_select");
			return;
		}
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
		DrawString(font, new Vector2(190.0f, 190.0f), "Options", HorizontalAlignment.Left, -1, 88, MenuTheme.Text);
		MainMenuScreen.DrawGear(this, new Vector2(590.0f, 160.0f), 28.0f, MenuTheme.Ink);

		for (int r = 0; r < RowNames.Length; r++)
		{
			Rect2 box = ValueBox((Row)r);
			DrawString(font, new Vector2(190.0f, box.GetCenter().Y + 14.0f), RowNames[r], HorizontalAlignment.Left, -1, 40, MenuTheme.Text);
			MenuTheme.DrawButton(this, box, ValueOf((Row)r), 300 + r * 7, Hot(box), fontSize: 36);
		}

		// The names saved here: press one to delete it, and again to be sure - or add one.
		DrawString(font, new Vector2(NamesLeft, RowTop + 30.0f), "Names", HorizontalAlignment.Left, -1, 40, MenuTheme.Text);
		if (adding != null)
		{
			DrawAdding(font);
			DrawBottom(font);
			return;
		}
		if (CanAdd) MenuTheme.DrawButton(this, AddBox, "+ Add a name", 398, Hot(AddBox), fontSize: 30);
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

		DrawBottom(font);
	}

	void DrawBottom(Font font)
	{
		MenuTheme.DrawButton(this, BackBox, "Back", 390, Hot(BackBox), fontSize: 32);
		DrawString(font, new Vector2(190.0f, NewsBox.GetCenter().Y + 14.0f), $"Version {UpdateScreen.CurrentVersion}", HorizontalAlignment.Left, -1, 40, MenuTheme.Text);
		MenuTheme.DrawButton(this, NewsBox, "What's new", 392, Hot(NewsBox), fontSize: 36);
		MenuTheme.DrawButton(this, GalleryBox, "Gallery", 396, Hot(GalleryBox), fontSize: 36);
		MenuTheme.DrawButton(this, ControlsBox, "Controls", 394, Hot(ControlsBox), fontSize: 36);
	}

	/// <summary>A new name: what is spelled so far, and the keys to spell it with.</summary>
	void DrawAdding(Font font)
	{
		DrawRect(FieldBox, Colors.White);
		CrayonBrush.InkRect(this, FieldBox, MenuTheme.Ink, 3.0f, 420, 1.6f);
		bool blink = (Engine.GetPhysicsFrames() / 30) % 2 == 0;
		MenuTheme.DrawCentredText(this, FieldBox, adding + (blink ? "_" : " "), 40, MenuTheme.Text);
		var keys = new List<MenuTheme.ButtonSpec>();
		for (int i = 0; i < Keys.Length; i++)
		{
			Rect2 box = KeyBox(i);
			keys.Add(new MenuTheme.ButtonSpec(box, Keys[i], 430 + i, Hot(box), Keys[i] == "OK", Keys[i].Length > 2 ? 20 : 30, null));
		}
		MenuTheme.DrawButtons(this, keys);
		DrawString(font, new Vector2(NamesLeft, KeyBox(Keys.Length - 1).End.Y + 44.0f), "Or type it, then Enter.  B: never mind",
			HorizontalAlignment.Left, -1, 26, MenuTheme.Soft);
	}
}
