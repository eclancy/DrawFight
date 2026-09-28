using Godot;

/// <summary>
/// Pick where to fight. Player 1 drives, because two people arguing over a stage cursor is
/// worse than one person choosing.
///
/// Every stage shows a live preview built from its own <see cref="StageData"/>, so a stage
/// added to <see cref="StageCatalog"/> appears here with no work: the preview is the same
/// palette and the same rectangles the match will use.
/// </summary>
public partial class StageSelectScreen : Node2D
{
	MenuNav nav;
	MenuNav secondNav;
	int index;

	Label nameLabel;
	Label blurbLabel;
	readonly StageData[] stages = new StageData[StageCatalog.Count];

	public override void _Ready()
	{
		IInputSource[] sources = ControllerAssignment.ForPlayers(2);
		nav = new MenuNav(sources[0]);
		secondNav = new MenuNav(sources[1]);

		for (int i = 0; i < stages.Length; i++) stages[i] = StageCatalog.Get(i);
		index = GameRoot.Instance.SelectedStage;

		var layer = new CanvasLayer();
		AddChild(layer);
		var root = new Control { AnchorsPreset = (int)Control.LayoutPreset.FullRect };
		layer.AddChild(root);

		root.AddChild(MenuTheme.MakeLabel("Pick a stage", new Vector2(190.0f, 74.0f), 76, MenuTheme.Text));
		root.AddChild(MenuTheme.MakeLabel(
			"left and right to choose        A to fight        B to go back",
			new Vector2(196.0f, 176.0f), 27, MenuTheme.Soft));

		nameLabel = MenuTheme.MakeLabel("", new Vector2(196.0f, 792.0f), 62, MenuTheme.Text);
		blurbLabel = MenuTheme.MakeLabel("", new Vector2(200.0f, 872.0f), 27, MenuTheme.Soft);
		root.AddChild(nameLabel);
		root.AddChild(blurbLabel);

		Refresh();
	}

	void Refresh()
	{
		StageData stage = stages[index];
		nameLabel.Text = stage.DisplayName;

		// The blurb names the thing that changes how the stage plays, not the scenery. What a
		// player needs from this screen is "can I fall off the bottom here".
		blurbLabel.Text = stage.HasBottomBlastZone
			? "Watch the gaps. You can fall off the bottom here."
			: "The ground runs the whole way. You can only be knocked off the sides or the top.";

		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		nav.Poll();
		secondNav.Poll();

		int step = nav.StepX != 0 ? nav.StepX : secondNav.StepX;
		if (step != 0)
		{
			index = ((index + step) % stages.Length + stages.Length) % stages.Length;
			Refresh();
		}

		if (nav.Cancel || secondNav.Cancel)
		{
			GameRoot.Instance.GoCharacterSelect();
			return;
		}

		if (nav.Confirm || secondNav.Confirm)
		{
			GameRoot.Instance.SelectedStage = index;
			GameRoot.Instance.GoMatch();
		}
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);

		const float CardWidth = 470.0f;
		const float Gap = 40.0f;
		float total = stages.Length * CardWidth + (stages.Length - 1) * Gap;
		float left = (size.X - total) * 0.5f;

		for (int i = 0; i < stages.Length; i++)
		{
			bool chosen = i == index;

			// Every card is the same size. Selection is carried by the border alone, so the
			// captions stay on one baseline instead of stepping around as the cursor moves.
			var card = new Rect2(left + i * (CardWidth + Gap), 262.0f, CardWidth, 440.0f);

			ControllerAssignment.DrawStagePreview(this, stages[i], card);

			if (chosen)
			{
				CrayonBrush.InkRect(this, card.Grow(13.0f), MenuTheme.Accent, 7.0f, 41 + i, 3.0f);
				continue;
			}

			// The chosen stage is named in full underneath; only the others need a caption.
			DrawString(ThemeDB.FallbackFont,
				new Vector2(card.Position.X + 6.0f, card.End.Y + 40.0f),
				stages[i].DisplayName, HorizontalAlignment.Left, -1, 26, MenuTheme.Soft);
		}
	}
}
