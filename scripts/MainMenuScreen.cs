using System.Collections.Generic;
using Godot;

/// <summary>
/// After PRESS START: what to do. Fight (versus), play a story, or draw a fighter
/// and send it in. Three big cards and a cursor, like every other menu. The mouse is for the
/// drawing screens only (Eric, 2026-10-16). Nobody is a player number yet: that is decided on
/// fighter select, by who presses a button first.
///
/// The versus card is Elim's two fighters, standing about on their own rigs: the menu shows the
/// game's own drawings rather than anything drawn for the menu.
/// </summary>
public partial class MainMenuScreen : Node2D
{
	enum Choice { Versus, Story, Draw }

	static readonly Rect2 QuitBox = new Rect2(1640.0f, 64.0f, 200.0f, 76.0f);
	const float CardWidth = 470.0f;
	const float CardHeight = 600.0f;
	const float CardGap = 45.0f;
	const float CardTop = 250.0f;

	/// <summary>The options: a small gear beside QUIT (Eric, 2026-10-17).</summary>
	static readonly Rect2 GearBox = new Rect2(1540.0f, 64.0f, 76.0f, 76.0f);

	/// <summary>
	/// Report a bug (SuggestScreen). It was "Ideas" (Eric, 2026-10-18) until Eric made it only bug
	/// reports (2026-10-20): new fighters and stages come in as drawings, through Draw your own.
	/// </summary>
	static readonly Rect2 BugBox = new Rect2(1246.0f, 64.0f, 270.0f, 76.0f);

	/// <summary>A cursor for each controller, shown once it is touched (TouchCursors).</summary>
	TouchCursors touch;
	readonly QuitPrompt quitPrompt = new QuitPrompt();
	List<MenuCursor> cursors => touch.Shown;
	readonly List<FighterRig> rigs = new List<FighterRig>();
	Node2D cursorLayer;
	int hovered = -1;
	int frame;
	bool leaving;

	public override void _Ready()
	{
		// Back from the options, the controller that went there is still pointing.
		touch = new TouchCursors(new Vector2(860.0f, 960.0f), GameRoot.Instance.MenuReturnKey);
		GameRoot.Instance.MenuReturnKey = null;

		// Circy and Triguy: Elim's drawings, facing off on the versus card.
		Rect2 versus = CardRect(Choice.Versus);
		rigs.Add(AddRig(FighterCatalog.Get(2), versus.Position + new Vector2(160.0f, 244.0f), 1));
		rigs.Add(AddRig(FighterCatalog.Get(5), versus.Position + new Vector2(320.0f, 244.0f), -1));

		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			quitPrompt.Draw(canvas, cursors.ConvertAll(c => c.Position));
			foreach (MenuCursor cursor in cursors) cursor.Draw(canvas);
		});
	}

	FighterRig AddRig(FighterData data, Vector2 at, int facing)
	{
		var holder = new Node2D { Position = at };
		AddChild(holder);
		var rig = new FighterRig();
		holder.AddChild(rig);
		if (!rig.Load(data)) return rig;
		rig.ArmBackLimit = data.ArmReachBack;
		rig.Normalise(170.0f, 85.0f, 1.0f);
		rig.SetFacing(facing);
		rig.SetPlanted(true);
		rig.SetStanding(true);
		rig.HandOnHip = data.HandOnHip;
		rig.CarryOnShoulder = data.ShouldersProp;
		rig.Play(FighterAnimations.Idle);
		return rig;
	}

	static Rect2 CardRect(Choice choice)
	{
		float left = (1920.0f - (CardWidth * 3.0f + CardGap * 2.0f)) * 0.5f;
		return new Rect2(left + (int)choice * (CardWidth + CardGap), CardTop, CardWidth, CardHeight);
	}

	static int CardUnder(Vector2 point)
	{
		for (int i = 0; i < 3; i++) if (CardRect((Choice)i).HasPoint(point)) return i;
		return -1;
	}

	public override void _PhysicsProcess(double delta)
	{
		frame++;
		foreach (FighterRig rig in rigs) if (rig.Loaded) rig.Advance();

		Vector2 viewport = GetViewportRect().Size;
		touch.Tick();

		foreach (MenuCursor cursor in cursors)
		{
			if (leaving) break;
			Vector2 was = cursor.Position;
			cursor.Update(viewport);
			if (cursor.Position != was)
			{
				int over = CardUnder(cursor.Position);
				if (over >= 0 && over != hovered) SfxPlayer.Ui("ui_move");
				hovered = over;
			}
			// B steps back through the menus to here, and stops: this is home (Eric, 2026-10-18).
			// Leaving the game is QUIT - which asks first.
			if (quitPrompt.Open)
			{
				if (cursor.Cancel) quitPrompt.Cancel();
				else if (cursor.Confirm) quitPrompt.Press(this, cursor.Position);
				continue;
			}
			if (cursor.Confirm)
			{
				// A pad picked it: drawing a fighter starts with the pad as the pointer.
				OnePointer.StartWith(cursor.Nav.Source is GamepadInputSource);
				Press(cursor.Position, touch.KeyOf(cursor));
			}
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Press(Vector2 point, string key)
	{
		if (leaving) return;
		if (QuitBox.HasPoint(point))
		{
			quitPrompt.Open = true;
			SfxPlayer.Ui("ui_select");
			return;
		}
		if (BugBox.HasPoint(point))
		{
			leaving = true;
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.GoSuggest();
			return;
		}
		if (GearBox.HasPoint(point))
		{
			leaving = true;
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.GoOptions(key);
			return;
		}
		switch ((Choice)CardUnder(point))
		{
			case Choice.Versus:
				leaving = true;
				SfxPlayer.Ui("ui_select");
				// Whoever picked Versus is in already, as Player 1.
				GameRoot.Instance.VersusStarter = key;
				GameRoot.Instance.GoCharacterSelect();
				break;
			case Choice.Draw:
				leaving = true;
				SfxPlayer.Ui("ui_select");
				GameRoot.Instance.GoDrawings();
				break;
			case Choice.Story:
				leaving = true;
				SfxPlayer.Ui("ui_select");
				GameRoot.Instance.GoStorySelect();
				break;
		}
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(190.0f, 170.0f), "DrawFight", HorizontalAlignment.Left, -1, 96, MenuTheme.Text);

		DrawCard(Choice.Versus, "Versus", "Fight a friend, or the computer.");
		DrawCard(Choice.Story, "Story", "Fight for a place on the fridge.");
		DrawCard(Choice.Draw, "Draw your own", "A fighter or a stage, sent in.");

		bool quitHot = false;
		foreach (MenuCursor cursor in cursors) quitHot |= QuitBox.HasPoint(cursor.Position);
		MenuTheme.DrawButton(this, QuitBox, "QUIT", 77, quitHot, fontSize: 38);

		bool bugHot = false;
		foreach (MenuCursor cursor in cursors) bugHot |= BugBox.HasPoint(cursor.Position);
		MenuTheme.DrawButton(this, BugBox, "Report a bug", 79, bugHot, fontSize: 32);

		bool gearHot = false;
		foreach (MenuCursor cursor in cursors) gearHot |= GearBox.HasPoint(cursor.Position);
		MenuTheme.DrawButton(this, GearBox, "", 78, gearHot);
		DrawGear(this, GearBox.GetCenter(), 25.0f, MenuTheme.Ink);

		DrawString(font, new Vector2(190.0f, 1050.0f), $"v{UpdateScreen.CurrentVersion}", HorizontalAlignment.Left, -1, 26, MenuTheme.Soft);
	}

	/// <summary>A gear: eight square teeth round a wheel with a hole in it.</summary>
	public static void DrawGear(CanvasItem canvas, Vector2 at, float radius, Color ink)
	{
		const int Teeth = 8;
		var points = new List<Vector2>();
		for (int i = 0; i < Teeth * 4; i++)
		{
			float a = Mathf.Tau * (i - 0.5f) / (Teeth * 4);
			float r = (i % 4) < 2 ? radius : radius * 0.74f;
			points.Add(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
		}
		canvas.DrawColoredPolygon(points.ToArray(), ink);
		canvas.DrawCircle(at, radius * 0.32f, MenuTheme.Paper);
	}

	void DrawCard(Choice choice, string title, string blurb)
	{
		Rect2 card = CardRect(choice);
		bool hot = hovered == (int)choice;
		foreach (MenuCursor cursor in cursors) hot |= card.HasPoint(cursor.Position);

		DrawRect(new Rect2(card.Position + new Vector2(8.0f, 10.0f), card.Size), new Color(0.0f, 0.0f, 0.0f, 0.06f));
		DrawRect(card, new Color(1.0f, 1.0f, 0.99f));
		CrayonBrush.InkRect(this, card, MenuTheme.Ink, 5.0f, 600 + (int)choice * 9, 2.4f);
		if (hot) CrayonBrush.InkRect(this, card.Grow(10.0f), MenuTheme.Accent, 7.0f, 640 + (int)choice * 9, 2.8f);

		var art = new Rect2(card.Position + new Vector2(20.0f, 20.0f), new Vector2(card.Size.X - 40.0f, 340.0f));
		switch (choice)
		{
			case Choice.Versus:
				CrayonBrush.PencilFill(this, new Rect2(art.Position.X, art.End.Y - 40.0f, art.Size.X, 18.0f), new Color(0.45f, 0.75f, 0.42f), 611);
				break;
			case Choice.Story:
				DrawItem("popup_book_open", art.GetCenter() + new Vector2(0.0f, 10.0f), 300.0f, 0.0f, 1.0f);
				break;
			case Choice.Draw:
				DrawItem("canvas", art.GetCenter() + new Vector2(-50.0f, 0.0f), 300.0f, 0.0f, 1.0f);
				DrawItem("pencil", art.GetCenter() + new Vector2(120.0f, 40.0f), 250.0f, 0.35f, 1.0f);
				DrawItem("paint_bucket", art.GetCenter() + new Vector2(110.0f, 110.0f), 110.0f, 0.0f, 1.0f);
				break;
		}

		Color text = MenuTheme.Text;
		DrawString(ThemeDB.FallbackFont, new Vector2(card.Position.X, card.Position.Y + 440.0f), title,
			HorizontalAlignment.Center, card.Size.X, 60, text);
		if (blurb != "")
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(card.Position.X, card.Position.Y + 500.0f), blurb,
				HorizontalAlignment.Center, card.Size.X, 28, MenuTheme.Soft);
		}
	}

	/// <summary>One of the game's own item drawings, fitted to a size and turned.</summary>
	void DrawItem(string id, Vector2 centre, float longest, float angle, float alpha)
	{
		ItemArt art = ItemCatalog.Art(id);
		if (art == null) return;
		float s = longest / Mathf.Max(art.Size.X, art.Size.Y);
		DrawSetTransformMatrix(new Transform2D(angle, new Vector2(s, s), 0.0f, centre));
		DrawTexture(art.Texture, -art.Size * 0.5f, new Color(1.0f, 1.0f, 1.0f, alpha));
		DrawSetTransformMatrix(Transform2D.Identity);
	}
}
