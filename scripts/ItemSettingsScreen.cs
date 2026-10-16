using Godot;

/// <summary>
/// The item settings, reached from the stage select screen: how often items turn up (off, low,
/// medium, high), and which of the thirty can. Every item is a card with its own drawing on it;
/// point at one and press A to switch it on or off, and the line along the bottom says what it
/// does. B, or Done, saves and goes back. Cursors, like every other select screen, so nothing has
/// to explain which button goes where.
/// </summary>
public partial class ItemSettingsScreen : Node2D
{
	MenuCursor[] cursors;
	ItemSettings settings;
	Node2D cursorLayer;

	readonly Rect2[] frequencyButtons = new Rect2[4];
	Rect2 allOnButton, allOffButton, doneButton;
	readonly Rect2[] tiles = new Rect2[ItemCatalog.Count];

	int hoveredTile = -1;

	const int Columns = 6;
	static readonly Vector2 TileSize = new Vector2(252.0f, 118.0f);
	const float Gap = 14.0f;
	const float GridTop = 292.0f;

	public override void _Ready()
	{
		settings = GameRoot.Instance.Items;
		Vector2 viewport = GetViewportRect().Size;
		cursors = GameRoot.Instance.HumanCursors(viewport, 200.0f);

		for (int i = 0; i < frequencyButtons.Length; i++)
		{
			frequencyButtons[i] = new Rect2(560.0f + i * 215.0f, 168.0f, 195.0f, 78.0f);
		}
		allOnButton = new Rect2(1440.0f, 168.0f, 190.0f, 78.0f);
		allOffButton = new Rect2(1650.0f, 168.0f, 190.0f, 78.0f);
		doneButton = new Rect2(1640.0f, 975.0f, 200.0f, 78.0f);

		float gridWidth = Columns * TileSize.X + (Columns - 1) * Gap;
		float left = (viewport.X - gridWidth) * 0.5f + 40.0f;
		for (int i = 0; i < tiles.Length; i++)
		{
			int col = i % Columns, row = i / Columns;
			tiles[i] = new Rect2(left + col * (TileSize.X + Gap), GridTop + row * (TileSize.Y + Gap), TileSize.X, TileSize.Y);
		}

		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in cursors) cursor.Draw(canvas);
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;
		foreach (MenuCursor cursor in cursors)
		{
			cursor.Update(viewport);
			if (cursor.Cancel)
			{
				Leave();
				return;
			}

			int over = TileUnder(cursor.Position);
			if (over >= 0 && over != hoveredTile)
			{
				hoveredTile = over;
				SfxPlayer.Ui("ui_move");
			}
			if (!cursor.Confirm) continue;

			for (int i = 0; i < frequencyButtons.Length; i++)
			{
				if (!frequencyButtons[i].HasPoint(cursor.Position)) continue;
				settings.Frequency = (ItemFrequency)i;
				SfxPlayer.Ui("ui_select");
			}
			if (allOnButton.HasPoint(cursor.Position)) SetAll(true);
			if (allOffButton.HasPoint(cursor.Position)) SetAll(false);
			if (doneButton.HasPoint(cursor.Position))
			{
				Leave();
				return;
			}
			if (over >= 0)
			{
				settings.Enabled[over] = !settings.Enabled[over];
				SfxPlayer.Ui(settings.Enabled[over] ? "ui_select" : "ui_back");
			}
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void SetAll(bool on)
	{
		for (int i = 0; i < settings.Enabled.Length; i++) settings.Enabled[i] = on;
		SfxPlayer.Ui("ui_select");
	}

	void Leave()
	{
		settings.Save();
		SfxPlayer.Ui("ui_back");
		GameRoot.Instance.GoStageSelect();
	}

	int TileUnder(Vector2 point)
	{
		for (int i = 0; i < tiles.Length; i++) if (tiles[i].Grow(Gap * 0.5f).HasPoint(point)) return i;
		return -1;
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;

		DrawString(font, new Vector2(190.0f, 132.0f), "Items", HorizontalAlignment.Left, -1, 76, MenuTheme.Text);
		DrawString(font, new Vector2(330.0f, 222.0f), "How often", HorizontalAlignment.Left, -1, 34, MenuTheme.Soft);

		for (int i = 0; i < frequencyButtons.Length; i++)
		{
			bool chosen = (int)settings.Frequency == i;
			DrawButton(frequencyButtons[i], ItemSettings.Describe((ItemFrequency)i), chosen, 300 + i);
		}
		DrawButton(allOnButton, "All on", false, 320);
		DrawButton(allOffButton, "All off", false, 321);
		DrawButton(doneButton, "Done", false, 322);

		bool off = settings.Frequency == ItemFrequency.Off;
		for (int i = 0; i < tiles.Length; i++) DrawTile(i, off);

		// What the one under a cursor does.
		string blurb = hoveredTile >= 0
			? ItemCatalog.Get(hoveredTile).DisplayName + ":  " + ItemCatalog.Get(hoveredTile).Blurb
			: off ? "Items are off. Pick how often they turn up to switch them on."
			: "Point at an item and press A to switch it on or off.";
		DrawString(font, new Vector2(196.0f, 1022.0f), blurb, HorizontalAlignment.Left, 1420.0f, 27, MenuTheme.Text);
	}

	void DrawButton(Rect2 rect, string text, bool chosen, int seed)
	{
		Color fill = chosen ? MenuTheme.AccentTwo.Lightened(0.35f) : MenuTheme.Paper;
		DrawRect(rect, fill);
		CrayonBrush.InkRect(this, rect, chosen ? MenuTheme.AccentTwo : MenuTheme.Ink, chosen ? 6.0f : 4.0f, seed, 2.2f);
		foreach (MenuCursor cursor in cursors)
		{
			if (!rect.HasPoint(cursor.Position)) continue;
			CrayonBrush.InkRect(this, rect.Grow(8.0f), cursor.Tint, 5.0f, seed + 50, 2.6f);
			break;
		}
		Vector2 textSize = ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, 34);
		DrawString(ThemeDB.FallbackFont, rect.GetCenter() + new Vector2(-textSize.X * 0.5f, 12.0f), text,
			HorizontalAlignment.Left, -1, 34, MenuTheme.Text);
	}

	void DrawTile(int index, bool allOff)
	{
		Rect2 rect = tiles[index];
		ItemData data = ItemCatalog.Get(index);
		bool on = settings.Enabled[index] && !allOff;

		DrawRect(rect, on ? new Color(1.0f, 1.0f, 0.99f) : MenuTheme.PaperEdge);
		CrayonBrush.InkRect(this, rect, on ? MenuTheme.Ink : MenuTheme.Rule, on ? 4.0f : 3.0f, 400 + index, 2.0f, !on);

		// The item's own drawing, fitted in on the left.
		ItemArt art = ItemCatalog.Art(data.Id);
		Rect2 well = new Rect2(rect.Position + new Vector2(8.0f, 14.0f), new Vector2(rect.Size.Y - 28.0f, rect.Size.Y - 28.0f));
		if (art != null)
		{
			float s = Mathf.Min(well.Size.X / art.Size.X, well.Size.Y / art.Size.Y);
			Vector2 drawn = art.Size * s;
			Rect2 at = new Rect2(well.GetCenter() - drawn * 0.5f, drawn);
			DrawTextureRect(art.Texture, at, false, new Color(1, 1, 1, on ? 1.0f : 0.35f));
		}

		// Wrapped at spaces and hyphens, and smaller for a name with one long word in it, so no word is ever cut.
		float room = rect.End.X - well.End.X - 18.0f;
		int fontSize = 24;
		foreach (string word in data.DisplayName.Split(' ', '-'))
		{
			while (fontSize > 16 && ThemeDB.FallbackFont.GetStringSize(word, HorizontalAlignment.Left, -1, fontSize).X > room) fontSize--;
		}
		DrawMultilineString(ThemeDB.FallbackFont, new Vector2(well.End.X + 10.0f, rect.Position.Y + 40.0f), data.DisplayName,
			HorizontalAlignment.Left, room, fontSize, 3, on ? MenuTheme.Text : MenuTheme.Soft);

		// Switched off: crossed out in crayon.
		if (!settings.Enabled[index])
		{
			Color cross = new Color(0.86f, 0.36f, 0.36f, 0.8f);
			CrayonBrush.InkLine(this, well.Position + new Vector2(6.0f, 6.0f), well.End - new Vector2(6.0f, 6.0f), cross, 6.0f, 500 + index, 2.0f);
			CrayonBrush.InkLine(this, new Vector2(well.End.X - 6.0f, well.Position.Y + 6.0f), new Vector2(well.Position.X + 6.0f, well.End.Y - 6.0f), cross, 6.0f, 600 + index, 2.0f);
		}

		foreach (MenuCursor cursor in cursors)
		{
			if (TileUnder(cursor.Position) != index) continue;
			CrayonBrush.InkRect(this, rect.Grow(6.0f), cursor.Tint, 5.0f, 700 + index, 2.4f);
			break;
		}
	}
}
