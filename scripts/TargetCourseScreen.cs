using Godot;

/// <summary>
/// Targets, after the fighter is picked: Easy, Medium or Hard. Each card shows the course laid
/// out - its sheets, what you stand on and where the ten targets are - and his best time on it.
/// Pointing and pressing A picks; Back, or B, goes back to picking a fighter.
/// </summary>
public partial class TargetCourseScreen : Node2D
{
	const float CardWidth = 500.0f;
	const float CardHeight = 620.0f;
	const float CardGap = 40.0f;
	const float CardTop = 260.0f;

	/// <summary>The course laid out small, at the top of each card.</summary>
	const float MapHeight = 330.0f;

	TouchCursors touch;
	Node2D cursorLayer;
	FighterData fighter;
	int hovered = -1;
	bool leaving;

	public override void _Ready()
	{
		int picked = GameRoot.Instance.SelectedFighters[0];
		fighter = FighterCatalog.Get(picked >= 0 && picked < FighterCatalog.Count ? picked : 0);

		// Whoever picked the fighter has the cursor from the start.
		touch = new TouchCursors(new Vector2(960.0f, 960.0f), GameRoot.Instance.SeatDevices[0]);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in touch.Shown) cursor.Draw(canvas);
		});

		// His name in his own letters, by the title: whose times these are.
		var name = new NameTag
		{
			FontSize = 64,
			Align = HorizontalAlignment.Right,
			FitWidth = true,
			Position = new Vector2(880.0f, 128.0f),
			Size = new Vector2(1000.0f, 84.0f),
		};
		name.SetFighter(fighter);
		AddChild(name);
	}

	static Rect2 CardRect(int course)
	{
		float left = (1920.0f - (CardWidth * TargetCourses.Count + CardGap * (TargetCourses.Count - 1))) * 0.5f;
		return new Rect2(left + course * (CardWidth + CardGap), CardTop, CardWidth, CardHeight);
	}

	static int CardUnder(Vector2 point)
	{
		for (int i = 0; i < TargetCourses.Count; i++) if (CardRect(i).HasPoint(point)) return i;
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
			if (cursor.Cancel || cursor.Confirm && MenuTheme.BackBox.HasPoint(cursor.Position))
			{
				leaving = true;
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoCharacterSelect();
				return;
			}
			if (!cursor.Confirm) continue;
			int course = CardUnder(cursor.Position);
			if (course < 0) continue;
			leaving = true;
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance.SelectedCourse = course;
			GameRoot.Instance.SeatDevices[0] = touch.KeyOf(cursor);
			GameRoot.Instance.GoTargetRun();
			return;
		}
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	bool Pointed(Rect2 box)
	{
		foreach (MenuCursor cursor in touch.Shown) if (box.HasPoint(cursor.Position)) return true;
		return false;
	}

	public override void _Draw()
	{
		MenuTheme.DrawPage(this, GetViewportRect().Size);
		DrawString(ThemeDB.FallbackFont, new Vector2(190.0f, 190.0f), "Pick a course", HorizontalAlignment.Left, -1, 76, MenuTheme.Text);
		MenuTheme.DrawButton(this, MenuTheme.BackBox, "Back", 801, Pointed(MenuTheme.BackBox), fontSize: 32);
		for (int i = 0; i < TargetCourses.Count; i++) DrawCard(i);
	}

	void DrawCard(int index)
	{
		Rect2 card = CardRect(index);
		TargetCourse course = TargetCourses.Get(index);
		bool hot = hovered == index || Pointed(card);

		DrawRect(new Rect2(card.Position + new Vector2(8.0f, 10.0f), card.Size), new Color(0.0f, 0.0f, 0.0f, 0.06f));
		DrawRect(card, new Color(1.0f, 1.0f, 0.99f));
		CrayonBrush.InkRect(this, card, MenuTheme.Ink, 5.0f, 820 + index * 9, 2.4f);
		if (hot) CrayonBrush.InkRect(this, card.Grow(10.0f), MenuTheme.Accent, 7.0f, 860 + index * 9, 2.8f);

		DrawMap(course, new Rect2(card.Position + new Vector2(20.0f, 20.0f), new Vector2(card.Size.X - 40.0f, MapHeight)));

		DrawString(ThemeDB.FallbackFont, new Vector2(card.Position.X, card.Position.Y + MapHeight + 120.0f), course.Name,
			HorizontalAlignment.Center, card.Size.X, 64, MenuTheme.Text);
		int best = TargetRecords.Best(fighter, course.Id);
		string time = best >= 0 ? $"Best  {TargetRecords.Clock(best)}" : "No time yet";
		DrawString(ThemeDB.FallbackFont, new Vector2(card.Position.X, card.Position.Y + MapHeight + 190.0f), time,
			HorizontalAlignment.Center, card.Size.X, 40, best >= 0 ? MenuTheme.Text : MenuTheme.Soft);
	}

	/// <summary>
	/// The course from far off: its sheets of paper, what you stand on - solid or dashed, as on the
	/// course - and the ten targets.
	/// </summary>
	void DrawMap(TargetCourse course, Rect2 area)
	{
		StageData stage = course.Stage;
		// Framed on where the run goes - its targets and where he starts - not on how far a floor
		// or a sheet runs on past it.
		Rect2 bounds = new Rect2(course.Targets[0], Vector2.Zero);
		foreach (Vector2 target in course.Targets) bounds = bounds.Expand(target);
		foreach (Vector2 spawn in stage.SpawnPoints) bounds = bounds.Expand(spawn);
		bounds = bounds.Grow(220.0f);
		float scale = Mathf.Min(area.Size.X / bounds.Size.X, area.Size.Y / bounds.Size.Y);
		Vector2 offset = area.GetCenter() - bounds.GetCenter() * scale;
		Rect2 Map(Rect2 r) => new Rect2(offset + r.Position * scale, r.Size * scale).Intersection(area);

		DrawRect(area, stage.SkyBottom.Lightened(0.3f));
		foreach (PinnedPage page in stage.Pages) DrawRect(Map(page.Rect), new Color(0.985f, 0.978f, 0.955f));
		foreach (StagePlatform platform in stage.Platforms)
		{
			Rect2 r = Map(platform.Rect);
			if (r.Size.X <= 0.0f) continue;
			r.Size = new Vector2(r.Size.X, Mathf.Max(r.Size.Y, 5.0f));
			DrawRect(r, platform.OneWay ? stage.PlatformCrayon : stage.GroundCrayon);
			CrayonBrush.InkRect(this, r, stage.Ink, 2.0f, 900 + Mathf.RoundToInt(r.Position.X), 0.8f, dashed: platform.OneWay);
		}
		for (int i = 0; i < course.Targets.Length; i++)
		{
			TargetsRun.DrawTarget(this, offset + course.Targets[i] * scale, 11.0f, i * 17 + 5, magnet: false);
		}
	}
}
