using System.Collections.Generic;
using Godot;

/// <summary>
/// The gallery, from the bottom of the options (Eric, 2026-10-24): every drawing sent in for the
/// game, one at a time, each with its fighter's name and who drew it - Elim's Circy and Triguy,
/// Eric's EdgeLord and DoomBot - ending on Eric's picture for his nephew. The pictures are copies
/// made by tools/art/gallery.py, in the order of assets/gallery/gallery.txt. Left and right turn
/// the page (or the arrows); B, or Back, returns to the options.
/// </summary>
public partial class GalleryScreen : Node2D
{
	public string OpenedBy;

	const string ListPath = "res://assets/gallery/gallery.txt";

	/// <summary>Where the picture sits, on its sheet.</summary>
	static readonly Rect2 Sheet = new Rect2(330.0f, 150.0f, 1260.0f, 760.0f);
	static readonly Rect2 PrevBox = new Rect2(190.0f, 470.0f, 100.0f, 120.0f);
	static readonly Rect2 NextBox = new Rect2(1630.0f, 470.0f, 100.0f, 120.0f);

	readonly List<(Texture2D art, string fighter)> pictures = new List<(Texture2D, string)>();
	int at;
	TouchCursors touch;
	Node2D cursorLayer;
	bool leaving;

	public override void _Ready()
	{
		using (FileAccess file = FileAccess.Open(ListPath, FileAccess.ModeFlags.Read))
		{
			if (file != null)
			{
				foreach (string raw in file.GetAsText().Split('\n'))
				{
					string line = raw.Trim();
					if (line.Length == 0 || line.StartsWith("#")) continue;
					string[] parts = line.Split('|');
					var art = GD.Load<Texture2D>("res://assets/gallery/" + parts[0]);
					if (art != null) pictures.Add((art, parts.Length > 1 ? parts[1] : ""));
				}
			}
		}
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
			if (cursor.Cancel || cursor.Confirm && MenuTheme.BackBox.HasPoint(cursor.Position))
			{
				leaving = true;
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoOptions(touch.KeyOf(cursor));
				return;
			}
			if (cursor.Nav.StepX != 0) Turn(cursor.Nav.StepX);
			else if (cursor.Confirm && PrevBox.HasPoint(cursor.Position)) Turn(-1);
			else if (cursor.Confirm && NextBox.HasPoint(cursor.Position)) Turn(1);
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Turn(int way)
	{
		int next = Mathf.Clamp(at + way, 0, Mathf.Max(0, pictures.Count - 1));
		if (next == at) return;
		at = next;
		SfxPlayer.Ui("ui_move");
	}

	bool Hot(Rect2 box)
	{
		foreach (MenuCursor cursor in touch.Shown) if (box.HasPoint(cursor.Position)) return true;
		return false;
	}

	/// <summary>"Circy - drawn by Elim, age 9", from the catalog, so it says what the select screen says.</summary>
	static string Caption(string fighter)
	{
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			if (StoryScript.IdOf(FighterCatalog.Get(i)) == fighter) return $"{FighterCatalog.NameOf(i)}  -  {FighterCatalog.CreditOf(i)}";
		}
		return "";
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;
		MenuTheme.DrawButton(this, MenuTheme.BackBox, "Back", 801, Hot(MenuTheme.BackBox), fontSize: 32);
		DrawString(font, new Vector2(400.0f, 96.0f), "Gallery", HorizontalAlignment.Left, -1, 64, MenuTheme.Text);
		if (pictures.Count == 0) return;

		(Texture2D art, string fighter) = pictures[at];
		// A sheet of white paper, and the drawing on it as big as it fits.
		DrawRect(new Rect2(Sheet.Position + new Vector2(8.0f, 10.0f), Sheet.Size), new Color(0.0f, 0.0f, 0.0f, 0.08f));
		DrawRect(Sheet, Colors.White);
		CrayonBrush.InkRect(this, Sheet, MenuTheme.Rule, 3.0f, 811, 2.4f);
		Vector2 room = Sheet.Size - new Vector2(40.0f, 40.0f);
		Vector2 drawn = art.GetSize();
		float scale = Mathf.Min(room.X / drawn.X, room.Y / drawn.Y);
		Vector2 shown = drawn * scale;
		DrawTextureRect(art, new Rect2(Sheet.GetCenter() - shown * 0.5f, shown), false);

		string caption = fighter == "" ? "" : Caption(fighter);
		DrawString(font, new Vector2(Sheet.Position.X, Sheet.End.Y + 64.0f), caption, HorizontalAlignment.Left, -1, 40, MenuTheme.Text);
		DrawString(font, new Vector2(Sheet.Position.X, Sheet.End.Y + 64.0f), $"{at + 1} / {pictures.Count}", HorizontalAlignment.Right, Sheet.Size.X, 34, MenuTheme.Soft);

		MenuTheme.DrawButton(this, PrevBox, "<", 813, Hot(PrevBox), enabled: at > 0, fontSize: 56);
		MenuTheme.DrawButton(this, NextBox, ">", 815, Hot(NextBox), enabled: at < pictures.Count - 1, fontSize: 56);
	}
}
