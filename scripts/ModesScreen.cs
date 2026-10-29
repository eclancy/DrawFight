using System.Collections.Generic;
using Godot;

/// <summary>
/// More modes, from the main menu (Eric, 2026-10-29): training, a boss fight, breaking the targets
/// and a fight with items the players draw themselves. Four cards like the main menu's, and Back -
/// or B - to the main menu.
/// </summary>
public partial class ModesScreen : Node2D
{
	/// <summary>The controller that chose More modes: its cursor is showing from the start.</summary>
	public string OpenedBy;

	enum Choice { Training, Boss, Targets, Doodle }
	const int Choices = 4;

	const float CardWidth = 362.0f;
	const float CardHeight = 600.0f;
	const float CardGap = 30.0f;
	const float CardTop = 250.0f;

	static readonly string[] Titles = { "Training", "Boss fight", "Targets", "Doodle brawl" };
	static readonly string[] Blurbs =
	{
		"Practise on a dummy, and see every hit.",
		"Everyone together against giant DoomBot.",
		"Break all ten, as fast as you can.",
		"Draw the items, then fight with them.",
	};

	TouchCursors touch;
	Node2D cursorLayer;
	FighterRig boss;
	int hovered = -1;
	bool leaving;

	public override void _Ready()
	{
		touch = new TouchCursors(new Vector2(860.0f, 960.0f), OpenedBy);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in touch.Shown) cursor.Draw(canvas);
		});

		// DoomBot on the boss card, standing about on his own rig - the game's own drawing.
		int doombot = StoryScript.IndexOf("doombot");
		if (doombot >= 0)
		{
			Rect2 card = CardRect(Choice.Boss);
			// Feet on the floor line drawn at 330 (Normalise puts them that far below the holder).
			var holder = new Node2D { Position = card.Position + new Vector2(card.Size.X * 0.5f, 330.0f - 120.0f) };
			AddChild(holder);
			boss = new FighterRig();
			holder.AddChild(boss);
			FighterData data = FighterCatalog.Get(doombot);
			if (boss.Load(data))
			{
				boss.ArmBackLimit = data.ArmReachBack;
				boss.Normalise(240.0f, 120.0f, 1.0f);
				boss.SetFacing(-1);
				boss.SetPlanted(true);
				boss.SetStanding(true);
				boss.Robotic = data.Robotic;
				boss.BackArmForward = data.IdleBackArmForward;
				boss.Play(FighterAnimations.Idle);
			}
		}
	}

	static Rect2 CardRect(Choice choice)
	{
		float left = (1920.0f - (CardWidth * Choices + CardGap * (Choices - 1))) * 0.5f;
		return new Rect2(left + (int)choice * (CardWidth + CardGap), CardTop, CardWidth, CardHeight);
	}

	static int CardUnder(Vector2 point)
	{
		for (int i = 0; i < Choices; i++) if (CardRect((Choice)i).HasPoint(point)) return i;
		return -1;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (boss != null && boss.Loaded) boss.Advance();
		Vector2 viewport = GetViewportRect().Size;
		touch.Tick();
		foreach (MenuCursor cursor in touch.Shown)
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
			if (cursor.Cancel || cursor.Confirm && MenuTheme.BackBox.HasPoint(cursor.Position))
			{
				leaving = true;
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoMainMenu();
				return;
			}
			if (cursor.Confirm) Press(cursor.Position, touch.KeyOf(cursor));
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Press(Vector2 point, string key)
	{
		int choice = CardUnder(point);
		if (choice < 0) return;
		leaving = true;
		SfxPlayer.Ui("ui_select");
		// Whoever picked the mode is in already, as Player 1 - as for Versus.
		GameRoot.Instance.VersusStarter = key;
		switch ((Choice)choice)
		{
			case Choice.Training: GameRoot.Instance.GoTraining(); break;
			case Choice.Boss: GameRoot.Instance.GoBossFight(); break;
			case Choice.Targets: GameRoot.Instance.GoTargets(); break;
			case Choice.Doodle: GameRoot.Instance.GoDoodleBrawl(); break;
		}
	}

	bool Pointed(Rect2 box)
	{
		foreach (MenuCursor cursor in touch.Shown) if (box.HasPoint(cursor.Position)) return true;
		return false;
	}

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
		DrawString(ThemeDB.FallbackFont, new Vector2(190.0f, 190.0f), "More modes", HorizontalAlignment.Left, -1, 76, MenuTheme.Text);
		MenuTheme.DrawButton(this, MenuTheme.BackBox, "Back", 801, Pointed(MenuTheme.BackBox), fontSize: 32);
		for (int i = 0; i < Choices; i++) DrawCard((Choice)i);
	}

	void DrawCard(Choice choice)
	{
		Rect2 card = CardRect(choice);
		bool hot = hovered == (int)choice || Pointed(card);

		DrawRect(new Rect2(card.Position + new Vector2(8.0f, 10.0f), card.Size), new Color(0.0f, 0.0f, 0.0f, 0.06f));
		DrawRect(card, new Color(1.0f, 1.0f, 0.99f));
		CrayonBrush.InkRect(this, card, MenuTheme.Ink, 5.0f, 700 + (int)choice * 9, 2.4f);
		if (hot) CrayonBrush.InkRect(this, card.Grow(10.0f), MenuTheme.Accent, 7.0f, 740 + (int)choice * 9, 2.8f);

		Vector2 centre = card.Position + new Vector2(card.Size.X * 0.5f, 190.0f);
		switch (choice)
		{
			case Choice.Training:
				DrawItem("metre_stick", centre + new Vector2(-30.0f, 0.0f), 280.0f, -0.5f);
				DrawItem("magnifying_glass", centre + new Vector2(70.0f, 40.0f), 170.0f, 0.3f);
				break;
			case Choice.Boss:
				// DoomBot himself stands on this card (_Ready).
				CrayonBrush.PencilFill(this, new Rect2(card.Position.X + 20.0f, card.Position.Y + 330.0f, card.Size.X - 40.0f, 18.0f), new Color(0.62f, 0.60f, 0.58f), 711);
				break;
			case Choice.Targets:
				DrawTarget(this, centre, 110.0f);
				DrawItem("paper_airplane", centre + new Vector2(-90.0f, 110.0f), 150.0f, -0.4f);
				break;
			case Choice.Doodle:
				DrawItem("canvas", centre + new Vector2(-40.0f, 0.0f), 260.0f, 0.0f);
				DrawItem("crayon_green", centre + new Vector2(90.0f, 80.0f), 170.0f, -0.5f);
				break;
		}

		string title = Titles[(int)choice];
		DrawString(ThemeDB.FallbackFont, new Vector2(card.Position.X, card.Position.Y + 440.0f), title,
			HorizontalAlignment.Center, card.Size.X, MenuTheme.FitFontSize(title, card.Size.X - 24.0f, 54), MenuTheme.Text);
		DrawMultilineString(ThemeDB.FallbackFont, new Vector2(card.Position.X + 20.0f, card.Position.Y + 495.0f), Blurbs[(int)choice],
			HorizontalAlignment.Center, card.Size.X - 40.0f, 28, 2, MenuTheme.Soft);
	}

	/// <summary>A target: red and white rings, inked round the outside.</summary>
	public static void DrawTarget(CanvasItem canvas, Vector2 at, float radius)
	{
		var red = new Color(0.90f, 0.30f, 0.28f);
		var white = new Color(0.99f, 0.97f, 0.93f);
		for (int ring = 0; ring < 4; ring++)
		{
			canvas.DrawCircle(at, radius * (1.0f - ring * 0.24f), ring % 2 == 0 ? red : white);
		}
		canvas.DrawArc(at, radius, 0.0f, Mathf.Tau, 48, MenuTheme.Ink, 5.0f, true);
	}

	void DrawItem(string id, Vector2 centre, float longest, float angle)
	{
		ItemArt art = ItemCatalog.Art(id);
		if (art == null) return;
		float s = longest / Mathf.Max(art.Size.X, art.Size.Y);
		DrawSetTransformMatrix(new Transform2D(angle, new Vector2(s, s), 0.0f, centre));
		DrawTexture(art.Texture, -art.Size * 0.5f);
		DrawSetTransformMatrix(Transform2D.Identity);
	}
}
