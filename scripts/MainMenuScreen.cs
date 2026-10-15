using System.Collections.Generic;
using Godot;

/// <summary>
/// After PRESS START: what to do. Fight (versus), play a story, or draw a fighter
/// and send it in. Three big cards and a cursor, like every other menu - and the mouse works too,
/// because the drawing screen is a mouse-and-tablet place and a kid heading there has one in hand.
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

	readonly List<MenuCursor> cursors = new List<MenuCursor>();
	readonly List<FighterRig> rigs = new List<FighterRig>();
	Node2D cursorLayer;
	Vector2 mouse = new Vector2(-100.0f, -100.0f);
	int hovered = -1;
	int frame;
	bool leaving;

	public override void _Ready()
	{
		int pads = Input.GetConnectedJoypads().Count;
		IInputSource[] sources = ControllerAssignment.ForPlayers(Mathf.Max(1, pads));
		for (int i = 0; i < sources.Length; i++)
		{
			Color tint = i == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent;
			cursors.Add(new MenuCursor(sources[i], i, tint, new Vector2(860.0f + i * 200.0f, 960.0f)));
		}

		// Circy and Triguy: Elim's drawings, facing off on the versus card.
		Rect2 versus = CardRect(Choice.Versus);
		rigs.Add(AddRig(FighterCatalog.Get(2), versus.Position + new Vector2(160.0f, 244.0f), 1));
		rigs.Add(AddRig(FighterCatalog.Get(5), versus.Position + new Vector2(320.0f, 244.0f), -1));

		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in cursors) cursor.Draw(canvas);
		});
	}

	FighterRig AddRig(FighterData data, Vector2 at, int facing)
	{
		var holder = new Node2D { Position = at };
		AddChild(holder);
		var rig = new FighterRig();
		holder.AddChild(rig);
		if (!rig.Load(data.RigPath)) return rig;
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

	public override void _Input(InputEvent @event)
	{
		if (leaving) return;
		if (@event is InputEventMouseMotion motion)
		{
			mouse = motion.Position;
			int over = CardUnder(mouse);
			if (over >= 0 && over != hovered) SfxPlayer.Ui("ui_move");
			hovered = over;
		}
		else if (@event is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left)
		{
			mouse = click.Position;
			Press(click.Position);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		frame++;
		foreach (FighterRig rig in rigs) if (rig.Loaded) rig.Advance();

		Vector2 viewport = GetViewportRect().Size;
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
			if (cursor.Cancel)
			{
				leaving = true;
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoTitle();
				return;
			}
			if (cursor.Confirm) Press(cursor.Position);
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Press(Vector2 point)
	{
		if (leaving) return;
		if (QuitBox.HasPoint(point))
		{
			GetTree().Quit();
			return;
		}
		switch ((Choice)CardUnder(point))
		{
			case Choice.Versus:
				leaving = true;
				SfxPlayer.Ui("ui_select");
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
		DrawCard(Choice.Draw, "Draw a fighter", "Draw your own and send it in.");

		bool quitHot = QuitBox.HasPoint(mouse);
		foreach (MenuCursor cursor in cursors) quitHot |= QuitBox.HasPoint(cursor.Position);
		MenuTheme.DrawButton(this, QuitBox, "QUIT", 77, quitHot, fontSize: 38);

		DrawString(font, new Vector2(190.0f, 1050.0f), $"v{UpdateScreen.CurrentVersion}", HorizontalAlignment.Left, -1, 26, MenuTheme.Soft);
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
