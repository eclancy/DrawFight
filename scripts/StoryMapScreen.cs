using Godot;

/// <summary>
/// A story's chapters laid out on the fridge door, a magnet each, in order: the ones played wear
/// a gold star, the next one glows, the rest wait. Left and right pick any chapter already
/// reached - every one, once the story is finished - and A plays it; B goes back to "Whose
/// story?". After every chapter the story comes back here, so you can see how far you have got.
/// Down moves to how hard its fights are - Easy, Medium or Hard (GameOptions.StoryDifficulty),
/// remembered for every story - where left and right change it, and up goes back to the
/// chapters (Elim, 2026-10-21: once a story was beaten, left and right only changed the
/// difficulty and no chapter could be picked).
/// </summary>
public partial class StoryMapScreen : Node2D
{
	public StoryRun Run;

	MenuNav[] navs;
	int frames;

	/// <summary>The chapter A will play.</summary>
	int selected;

	/// <summary>Down at the difficulty strip, rather than on the chapters.</summary>
	bool onDifficulty;

	/// <summary>The furthest chapter that can be picked: everything once the story is finished.</summary>
	int LastOpen => Run == null ? 0 : StoryProgress.Finished(Run.PlayerId)
		? Run.Chapters.Count - 1
		: Mathf.Clamp(Mathf.Max(Run.At, StoryProgress.ChapterReached(Run.PlayerId)), 0, Mathf.Max(0, Run.Chapters.Count - 1));

	static readonly Color Steel = new Color(0.84f, 0.87f, 0.90f);
	static readonly Color SteelLow = new Color(0.74f, 0.78f, 0.83f);
	static readonly Color[] MagnetColours =
	{
		new Color(0.92f, 0.32f, 0.30f), new Color(0.98f, 0.80f, 0.25f), new Color(0.40f, 0.72f, 0.42f),
		new Color(0.38f, 0.58f, 0.95f), new Color(0.70f, 0.48f, 0.90f), new Color(0.98f, 0.58f, 0.28f),
	};

	public override void _Ready()
	{
		// The first pad, and the keyboard - but with no pad plugged in, the first "pad" is the
		// keyboard itself, and reading it twice moved two chapters a press.
		IInputSource first = ControllerAssignment.ForPlayers(1)[0];
		navs = first is KeyboardInputSource
			? new[] { new MenuNav(first) }
			: new[] { new MenuNav(first), new MenuNav(KeyboardInputSource.Player1()) };
		if (Run != null) selected = Mathf.Clamp(Run.At, 0, LastOpen);
	}

	public override void _PhysicsProcess(double delta)
	{
		frames++;
		foreach (MenuNav nav in navs)
		{
			nav.Poll();
			if (nav.Cancel)
			{
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoStorySelect();
				return;
			}
			if (nav.Confirm || nav.Start)
			{
				Play();
				return;
			}
			if (nav.StepY != 0)
			{
				bool down = nav.StepY > 0;
				if (down != onDifficulty)
				{
					onDifficulty = down;
					SfxPlayer.Ui("ui_move");
				}
			}
			if (nav.StepX != 0 && onDifficulty)
			{
				int was = GameOptions.StoryDifficulty;
				GameOptions.StoryDifficulty = Mathf.Clamp(was + nav.StepX, 0, 2);
				if (GameOptions.StoryDifficulty != was)
				{
					SfxPlayer.Ui("ui_move");
					GameOptions.Save();
				}
			}
			else if (nav.StepX != 0 && Run != null)
			{
				int was = selected;
				selected = Mathf.Clamp(selected + nav.StepX, 0, LastOpen);
				if (selected != was) SfxPlayer.Ui("ui_move");
			}
		}
		QueueRedraw();
	}

	void Play()
	{
		if (Run == null || Run.Chapters.Count == 0) return;
		Run.At = Mathf.Clamp(selected, 0, LastOpen);
		Run.Tries = 0;
		SfxPlayer.Ui("ui_ready");
		GameRoot.Instance.GoStoryChapter();
	}

	/// <summary>
	/// Where each chapter's magnet sits: along the top row, then back along the next, snaking, so
	/// the trail from one to the next never crosses the page.
	/// </summary>
	Vector2 SpotOf(int i, Vector2 size)
	{
		const int PerRow = 5;
		int row = i / PerRow, col = i % PerRow;
		if (row % 2 == 1) col = PerRow - 1 - col;
		float gap = 300.0f;
		int across = Mathf.Min(PerRow, Run.Chapters.Count);
		float left = size.X * 0.5f - (across - 1) * gap * 0.5f;
		return new Vector2(left + col * gap, 360.0f + row * 280.0f);
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		// The fridge door: pale brushed steel, as on the Fridge Door stage.
		DrawRect(new Rect2(Vector2.Zero, size), Steel);
		for (float x = 0.0f; x < size.X; x += 14.0f)
		{
			float shade = CrayonBrush.Noise(91, (int)x);
			DrawLine(new Vector2(x, 0.0f), new Vector2(x + 6.0f, size.Y), shade > 0 ? new Color(1, 1, 1, 0.12f * shade) : new Color(SteelLow, -0.25f * shade), 2.0f);
		}
		CrayonBrush.InkLine(this, new Vector2(size.X - 120.0f, 120.0f), new Vector2(size.X - 120.0f, size.Y - 120.0f), new Color(0.56f, 0.58f, 0.63f), 14.0f, 5, 1.4f);

		Font font = ThemeDB.FallbackFont;
		if (Run == null) return;
		int player = StoryScript.IndexOf(Run.PlayerId);
		// Whose story it is, his name in his own letters, and the rest of the heading in ink.
		FighterData data = player >= 0 ? FighterCatalog.Get(player) : null;
		string name = data != null ? data.DisplayName : Run.PlayerId;
		NameStyle look = data != null ? data.NameLook : NameStyle.Plain(MenuTheme.Text);
		string rest = string.IsNullOrEmpty(Run.Title) ? "'s story" : $": {Run.Title}";
		NameLettering.Draw(this, new Vector2(150.0f, 150.0f), name, look, 72);
		float restAt = 150.0f + NameLettering.Measure(name, look, 72).X;
		DrawString(font, new Vector2(restAt, 150.0f), rest, HorizontalAlignment.Left, -1, 72, MenuTheme.Text);
		bool finished = StoryProgress.Finished(Run.PlayerId);
		int reached = StoryProgress.ChapterReached(Run.PlayerId);
		if (finished)
		{
			Item.DrawStarOn(this, new Vector2(restAt + font.GetStringSize(rest, HorizontalAlignment.Left, -1, 72).X + 60.0f, 126.0f), 36.0f, new Color(1.0f, 0.82f, 0.25f));
		}

		// The dotted trail from magnet to magnet.
		for (int i = 0; i + 1 < Run.Chapters.Count; i++)
		{
			CrayonBrush.InkLine(this, SpotOf(i, size), SpotOf(i + 1, size), new Color(MenuTheme.Ink, 0.35f), 4.0f, 30 + i, 1.5f, true);
		}

		for (int i = 0; i < Run.Chapters.Count; i++)
		{
			StoryChapter chapter = Run.Chapters[i];
			Vector2 at = SpotOf(i, size);
			// A finished story starts again from the top, but every chapter keeps its star.
			bool played = finished || i < reached;
			bool open = i <= LastOpen;
			bool picked = i == selected && !onDifficulty;
			float pulse = picked ? 1.0f + 0.08f * Mathf.Sin(frames * 0.12f) : 1.0f;
			float r = 58.0f * pulse;
			Color colour = MagnetColours[i % MagnetColours.Length];
			if (!open) colour = colour.Lerp(Steel, 0.55f);

			if (i == selected) DrawCircle(at, r + 22.0f, new Color(1.0f, 0.9f, 0.5f, (onDifficulty ? 0.25f : 0.35f) + 0.15f * Mathf.Sin(frames * 0.12f)));
			DrawCircle(at + new Vector2(6.0f, 9.0f), r, new Color(0.2f, 0.22f, 0.3f, 0.18f));
			DrawCircle(at, r, colour);
			DrawArc(at, r, 0.0f, Mathf.Tau, 40, MenuTheme.Ink, 5.0f, true);
			DrawCircle(at + new Vector2(-r * 0.32f, -r * 0.34f), r * 0.22f, new Color(1, 1, 1, 0.55f));

			string number = (i + 1).ToString();
			DrawString(font, at + new Vector2(-r, 16.0f), number, HorizontalAlignment.Center, r * 2.0f, 48, MenuTheme.Text);
			DrawTitle(chapter.Title, at + new Vector2(0.0f, r + 46.0f), open);
			if (played) Item.DrawStarOn(this, at + new Vector2(r * 0.7f, -r * 0.7f), 26.0f, new Color(1.0f, 0.82f, 0.25f));
		}

		string hint = onDifficulty
			? $"< > how hard     up: chapters     A: play {selected + 1}     B: back"
			: $"< > pick a chapter     down: how hard     A: play {selected + 1}     B: back";
		DrawString(font, new Vector2(150.0f, size.Y - 70.0f), hint, HorizontalAlignment.Left, 690.0f, 28, MenuTheme.Soft);

		// How hard, along the bottom: the three words, the one picked underlined, arrows to change it.
		int difficulty = GameOptions.StoryDifficulty;
		float baseline = size.Y - 70.0f;
		// Picked out when it is where the stick is.
		if (onDifficulty) CrayonBrush.InkRect(this, new Rect2(845.0f, baseline - 52.0f, 820.0f, 76.0f), MenuTheme.Ink, 3.5f, 77, 1.2f);
		DrawString(font, new Vector2(860.0f, baseline), "How hard:", HorizontalAlignment.Left, -1, 32, MenuTheme.Soft);
		for (int d = 0; d < 3; d++)
		{
			float centre = 1180.0f + d * 170.0f;
			string word = d switch { 0 => "Easy", 2 => "Hard", _ => "Medium" };
			bool on = d == difficulty;
			if (on)
			{
				DrawRect(new Rect2(centre - 80.0f, baseline - 40.0f, 160.0f, 56.0f), new Color(1.0f, 0.9f, 0.5f, 0.6f));
				CrayonBrush.InkLine(this, new Vector2(centre - 70.0f, baseline + 12.0f), new Vector2(centre + 70.0f, baseline + 12.0f), MenuTheme.Ink, 4.0f, 70 + d, 1.4f);
			}
			DrawString(font, new Vector2(centre - 85.0f, baseline), word, HorizontalAlignment.Center, 170.0f, 32, on ? MenuTheme.Text : MenuTheme.Soft);
		}
		DrawString(font, new Vector2(1060.0f, baseline), "<", HorizontalAlignment.Center, 40.0f, 32, difficulty > 0 ? MenuTheme.Text : MenuTheme.Soft);
		DrawString(font, new Vector2(1600.0f, baseline), ">", HorizontalAlignment.Center, 40.0f, 32, difficulty < 2 ? MenuTheme.Text : MenuTheme.Soft);
	}

	/// <summary>
	/// A chapter's title, centred under its magnet: whoever it is against in their own letters
	/// ("EdgeLord", "Lugnut + Flambé"), anything else in ink. Faded until it is reached.
	/// </summary>
	void DrawTitle(string title, Vector2 centre, bool reached)
	{
		const int Size = 30;
		const string Plus = " + ";
		Font font = ThemeDB.FallbackFont;
		Color ink = reached ? MenuTheme.Text : MenuTheme.Soft;
		string[] parts = title.Split(Plus);
		var who = new FighterData[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			who[i] = FighterCatalog.Named(parts[i]);
			if (who[i] == null)
			{
				DrawString(font, centre - new Vector2(140.0f, 0.0f), title, HorizontalAlignment.Center, 280.0f, Size, ink);
				return;
			}
		}
		float plusWidth = font.GetStringSize(Plus, HorizontalAlignment.Left, -1, Size).X;
		float width = plusWidth * (parts.Length - 1);
		foreach (FighterData data in who) width += NameLettering.Measure(data.DisplayName, data.NameLook, Size).X;
		float x = centre.X - width * 0.5f;
		for (int i = 0; i < parts.Length; i++)
		{
			if (i > 0)
			{
				DrawString(font, new Vector2(x, centre.Y), Plus, HorizontalAlignment.Left, -1, Size, ink);
				x += plusWidth;
			}
			NameLettering.Draw(this, new Vector2(x, centre.Y), who[i].DisplayName, who[i].NameLook, Size, reached ? 1.0f : 0.55f);
			x += NameLettering.Measure(who[i].DisplayName, who[i].NameLook, Size).X;
		}
	}
}
