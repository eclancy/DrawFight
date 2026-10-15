using Godot;

/// <summary>
/// A story's chapters laid out on the fridge door, a magnet each, in order: the ones played wear
/// a gold star, the next one glows, the rest wait. A plays the next chapter, B goes back to
/// "Whose story?" - and after every chapter the story comes back here, so you can see how far you
/// have got. A finished story gets its gold star on "Whose story?" and starts again from here.
/// </summary>
public partial class StoryMapScreen : Node2D
{
	public StoryRun Run;

	MenuNav[] navs;
	int frames;

	static readonly Color Steel = new Color(0.84f, 0.87f, 0.90f);
	static readonly Color SteelLow = new Color(0.74f, 0.78f, 0.83f);
	static readonly Color[] MagnetColours =
	{
		new Color(0.92f, 0.32f, 0.30f), new Color(0.98f, 0.80f, 0.25f), new Color(0.40f, 0.72f, 0.42f),
		new Color(0.38f, 0.58f, 0.95f), new Color(0.70f, 0.48f, 0.90f), new Color(0.98f, 0.58f, 0.28f),
	};

	public override void _Ready()
	{
		navs = new[] { new MenuNav(ControllerAssignment.ForPlayers(1)[0]), new MenuNav(KeyboardInputSource.Player1()) };
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left) Play();
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
		}
		QueueRedraw();
	}

	void Play()
	{
		if (Run == null || Run.Done) return;
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
		string name = player >= 0 ? FighterCatalog.Get(player).DisplayName : Run.PlayerId;
		string heading = string.IsNullOrEmpty(Run.Title) ? $"{name}'s story" : $"{name}: {Run.Title}";
		DrawString(font, new Vector2(150.0f, 150.0f), heading, HorizontalAlignment.Left, -1, 72, MenuTheme.Text);
		bool finished = StoryProgress.Finished(Run.PlayerId);
		if (finished)
		{
			Item.DrawStarOn(this, new Vector2(150.0f + font.GetStringSize(heading, HorizontalAlignment.Left, -1, 72).X + 60.0f, 126.0f), 36.0f, new Color(1.0f, 0.82f, 0.25f));
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
			bool played = finished || i < Run.At;
			bool next = i == Run.At;
			float pulse = next ? 1.0f + 0.08f * Mathf.Sin(frames * 0.12f) : 1.0f;
			float r = 58.0f * pulse;
			Color colour = MagnetColours[i % MagnetColours.Length];
			if (!played && !next) colour = colour.Lerp(Steel, 0.55f);

			if (next) DrawCircle(at, r + 22.0f, new Color(1.0f, 0.9f, 0.5f, 0.35f + 0.15f * Mathf.Sin(frames * 0.12f)));
			DrawCircle(at + new Vector2(6.0f, 9.0f), r, new Color(0.2f, 0.22f, 0.3f, 0.18f));
			DrawCircle(at, r, colour);
			DrawArc(at, r, 0.0f, Mathf.Tau, 40, MenuTheme.Ink, 5.0f, true);
			DrawCircle(at + new Vector2(-r * 0.32f, -r * 0.34f), r * 0.22f, new Color(1, 1, 1, 0.55f));

			string number = (i + 1).ToString();
			DrawString(font, at + new Vector2(-r, 16.0f), number, HorizontalAlignment.Center, r * 2.0f, 48, MenuTheme.Text);
			DrawString(font, at + new Vector2(-140.0f, r + 46.0f), chapter.Title, HorizontalAlignment.Center, 280.0f, 30,
				played || next ? MenuTheme.Text : MenuTheme.Soft);
			if (played) Item.DrawStarOn(this, at + new Vector2(r * 0.7f, -r * 0.7f), 26.0f, new Color(1.0f, 0.82f, 0.25f));
		}

		string hint = Run.Done ? "B: back" : $"A: play chapter {Run.At + 1}      B: back";
		DrawString(font, new Vector2(150.0f, size.Y - 70.0f), hint, HorizontalAlignment.Left, -1, 32, MenuTheme.Soft);
	}
}
