using Godot;

/// <summary>
/// Online, once a session has started: what to play together - versus, the boss fight, a doodle
/// brawl or a story - picked by anyone on any computer, and Leave, which ends it for everyone.
/// Every copy of the game shows this screen and moves its cursors from the same controllers,
/// frame for frame (NetSession), so a card picked on one is picked on all.
/// </summary>
public partial class OnlineHomeScreen : Node2D
{
	enum Choice { Versus, Boss, Doodle, Story }
	const int Choices = 4;

	const float CardWidth = 362.0f;
	const float CardHeight = 560.0f;
	const float CardGap = 30.0f;
	const float CardTop = 290.0f;

	static readonly string[] Titles = { "Versus", "Boss fight", "Doodle brawl", "Story" };
	static readonly string[] Blurbs =
	{
		"Everyone for themselves, or teams.",
		"All of you against giant DoomBot.",
		"Draw the items, then fight with them.",
		"Play a story, with friends on your side.",
	};
	static readonly string[] Art = { "pencil", "glitter_bomb", "canvas", "popup_book_open" };

	TouchCursors touch;
	Node2D cursorLayer;
	int hovered = -1;
	bool leaving;

	public override void _Ready()
	{
		touch = new TouchCursors(new Vector2(860.0f, 960.0f));
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in touch.Shown) cursor.Draw(canvas);
		});
	}

	static Rect2 CardRect(int choice)
	{
		float left = (1920.0f - (CardWidth * Choices + CardGap * (Choices - 1))) * 0.5f;
		return new Rect2(left + choice * (CardWidth + CardGap), CardTop, CardWidth, CardHeight);
	}

	static int CardUnder(Vector2 point)
	{
		for (int i = 0; i < Choices; i++) if (CardRect(i).HasPoint(point)) return i;
		return -1;
	}

	public override void _PhysicsProcess(double delta)
	{
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
			// Only Leave leaves: B here would end the game for everyone by accident.
			if (cursor.Confirm && MenuTheme.BackBox.HasPoint(cursor.Position))
			{
				leaving = true;
				SfxPlayer.Ui("ui_back");
				NetSession.Instance?.Leave();
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
		GameRoot.Instance.VersusStarter = key;
		switch ((Choice)choice)
		{
			case Choice.Versus: GameRoot.Instance.GoCharacterSelect(); break;
			case Choice.Boss: GameRoot.Instance.GoBossFight(); break;
			case Choice.Doodle: GameRoot.Instance.GoDoodleBrawl(); break;
			case Choice.Story: GameRoot.Instance.GoStorySelect(); break;
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
		Font font = ThemeDB.FallbackFont;
		DrawString(font, new Vector2(190.0f, 190.0f), "Playing online", HorizontalAlignment.Left, -1, 76, MenuTheme.Text);
		NetSession session = NetSession.Instance;
		if (session != null)
		{
			DrawString(font, new Vector2(190.0f, 250.0f), $"{session.Members.Count} computers - code {session.Code}", HorizontalAlignment.Left, -1, 32, MenuTheme.Soft);
		}
		MenuTheme.DrawButton(this, MenuTheme.BackBox, "Leave", 801, Pointed(MenuTheme.BackBox), fontSize: 32);

		for (int i = 0; i < Choices; i++)
		{
			Rect2 card = CardRect(i);
			bool hot = hovered == i || Pointed(card);
			DrawRect(new Rect2(card.Position + new Vector2(8.0f, 10.0f), card.Size), new Color(0.0f, 0.0f, 0.0f, 0.06f));
			DrawRect(card, new Color(1.0f, 1.0f, 0.99f));
			CrayonBrush.InkRect(this, card, MenuTheme.Ink, 5.0f, 760 + i * 9, 2.4f);
			if (hot) CrayonBrush.InkRect(this, card.Grow(10.0f), MenuTheme.Accent, 7.0f, 790 + i * 9, 2.8f);

			ItemArt art = ItemCatalog.Art(Art[i]);
			if (art != null)
			{
				float s = 230.0f / Mathf.Max(art.Size.X, art.Size.Y);
				DrawSetTransformMatrix(new Transform2D(i == 0 ? -0.6f : 0.0f, new Vector2(s, s), 0.0f, card.Position + new Vector2(card.Size.X * 0.5f, 180.0f)));
				DrawTexture(art.Texture, -art.Size * 0.5f);
				DrawSetTransformMatrix(Transform2D.Identity);
			}
			DrawString(font, new Vector2(card.Position.X, card.Position.Y + 400.0f), Titles[i],
				HorizontalAlignment.Center, card.Size.X, MenuTheme.FitFontSize(Titles[i], card.Size.X - 24.0f, 54), MenuTheme.Text);
			DrawMultilineString(font, new Vector2(card.Position.X + 20.0f, card.Position.Y + 455.0f), Blurbs[i],
				HorizontalAlignment.Center, card.Size.X - 40.0f, 28, 2, MenuTheme.Soft);
		}
	}
}
