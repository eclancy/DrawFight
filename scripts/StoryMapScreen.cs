using Godot;

/// <summary>
/// A story's chapters laid out on the fridge door, a magnet each, in order: the ones played wear
/// a gold star, the next one glows, the rest wait. Left and right pick any chapter already
/// reached - every one, once the story is finished - and A plays it; B goes back to "Whose
/// story?". After every chapter the story comes back here, so you can see how far you have got.
/// How hard its fights are is picked on "Whose story?" (Eric, 2026-10-22), not here.
/// </summary>
public partial class StoryMapScreen : Node2D
{
	public StoryRun Run;

	MenuNav[] navs;
	int frames;

	/// <summary>The chapter A will play.</summary>
	int selected;

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
		// Every controller, each read once - reading the keyboard twice moved two chapters a press -
		// and online, the session's own (NetSession).
		navs = System.Array.ConvertAll(ControllerAssignment.Everyone(), source => new MenuNav(source));
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
			if (nav.StepX != 0 && Run != null)
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
			bool picked = i == selected;
			float pulse = picked ? 1.0f + 0.08f * Mathf.Sin(frames * 0.12f) : 1.0f;
			float r = 58.0f * pulse;
			Color colour = MagnetColours[i % MagnetColours.Length];
			if (!open) colour = colour.Lerp(Steel, 0.55f);

			if (i == selected) DrawCircle(at, r + 22.0f, new Color(1.0f, 0.9f, 0.5f, 0.35f + 0.15f * Mathf.Sin(frames * 0.12f)));
			DrawCircle(at + new Vector2(6.0f, 9.0f), r, new Color(0.2f, 0.22f, 0.3f, 0.18f));
			DrawCircle(at, r, colour);
			DrawArc(at, r, 0.0f, Mathf.Tau, 40, MenuTheme.Ink, 5.0f, true);
			DrawCircle(at + new Vector2(-r * 0.32f, -r * 0.34f), r * 0.22f, new Color(1, 1, 1, 0.55f));

			string number = (i + 1).ToString();
			DrawString(font, at + new Vector2(-r, 16.0f), number, HorizontalAlignment.Center, r * 2.0f, 48, MenuTheme.Text);
			DrawTitle(chapter.Title, at + new Vector2(0.0f, r + 46.0f), open);
			if (played) Item.DrawStarOn(this, at + new Vector2(r * 0.7f, -r * 0.7f), 26.0f, new Color(1.0f, 0.82f, 0.25f));
		}

		string hint = $"Left / Right: Pick a chapter     A: Play chapter {selected + 1}     B: Back";
		DrawString(font, new Vector2(0.0f, size.Y - 70.0f), hint, HorizontalAlignment.Center, size.X, 30, MenuTheme.Soft);
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
