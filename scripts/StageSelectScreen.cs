using Godot;

/// <summary>
/// Pick where to fight: either player points at a stage and presses A. B goes back to the
/// fighters. Both players have a cursor, because they both just joined on the previous screen
/// and a cursor that vanishes for one of them reads as "you are not playing any more".
///
/// Every stage shows a live preview built from its own <see cref="StageData"/>, so a stage
/// added to <see cref="StageCatalog"/> appears here with no work: the preview is the same
/// palette and the same rectangles the match will use.
/// </summary>
public partial class StageSelectScreen : Node2D
{
	readonly MenuCursor[] cursors = new MenuCursor[2];
	readonly StageData[] stages = new StageData[StageCatalog.Count];
	readonly Rect2[] cards = new Rect2[StageCatalog.Count];

	Label nameLabel;
	Label blurbLabel;
	Node2D cursorLayer;
	int shown = -1;

	const float CardWidth = 470.0f;
	const float CardGap = 40.0f;

	public override void _Ready()
	{
		IInputSource[] sources = ControllerAssignment.ForPlayers(cursors.Length);
		Vector2 viewport = GetViewportRect().Size;

		for (int i = 0; i < stages.Length; i++) stages[i] = StageCatalog.Get(i);

		float total = stages.Length * CardWidth + (stages.Length - 1) * CardGap;
		float left = (viewport.X - total) * 0.5f;
		for (int i = 0; i < cards.Length; i++)
		{
			cards[i] = new Rect2(left + i * (CardWidth + CardGap), 262.0f, CardWidth, 440.0f);
		}

		for (int i = 0; i < cursors.Length; i++)
		{
			Color tint = i == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent;
			cursors[i] = new MenuCursor(sources[i], i, tint, new Vector2(viewport.X * (0.4f + i * 0.2f), 900.0f));
		}

		var layer = new CanvasLayer();
		AddChild(layer);
		var root = new Control { AnchorsPreset = (int)Control.LayoutPreset.FullRect };
		layer.AddChild(root);

		root.AddChild(MenuTheme.MakeLabel("Pick a stage", new Vector2(190.0f, 74.0f), 76, MenuTheme.Text));

		for (int i = 0; i < stages.Length; i++)
		{
			root.AddChild(MenuTheme.MakeLabel(stages[i].DisplayName,
				new Vector2(cards[i].Position.X + 6.0f, cards[i].End.Y + 18.0f), 30, MenuTheme.Text));
		}

		nameLabel = MenuTheme.MakeLabel("", new Vector2(196.0f, 812.0f), 56, MenuTheme.Text);
		blurbLabel = MenuTheme.MakeLabel("", new Vector2(200.0f, 886.0f), 27, MenuTheme.Soft);
		root.AddChild(nameLabel);
		root.AddChild(blurbLabel);

		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (MenuCursor cursor in cursors) cursor.Draw(canvas);
		});
	}

	int StageUnder(Vector2 point)
	{
		for (int i = 0; i < cards.Length; i++)
		{
			if (cards[i].Grow(13.0f).HasPoint(point)) return i;
		}
		return -1;
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;

		foreach (MenuCursor cursor in cursors)
		{
			cursor.Update(viewport);

			if (cursor.Cancel)
			{
				GameRoot.Instance.GoCharacterSelect();
				return;
			}

			int under = StageUnder(cursor.Position);
			if (under >= 0) shown = under;

			if (cursor.Confirm && under >= 0)
			{
				GameRoot.Instance.SelectedStage = under;
				GameRoot.Instance.GoMatch();
				return;
			}
		}

		Refresh();
		QueueRedraw();
		cursorLayer.QueueRedraw();
	}

	void Refresh()
	{
		if (shown < 0)
		{
			nameLabel.Text = "";
			blurbLabel.Text = "";
			return;
		}

		StageData stage = stages[shown];
		nameLabel.Text = stage.DisplayName;

		// The blurb names the thing that changes how the stage plays, not the scenery. What a
		// player needs from this screen is "can I fall off the bottom here".
		blurbLabel.Text = stage.HasBottomBlastZone
			? "Watch the gaps. You can fall off the bottom here."
			: "The ground runs the whole way. You can only be knocked off the sides or the top.";
	}

	public override void _Draw()
	{
		Vector2 size = GetViewportRect().Size;
		MenuTheme.DrawPage(this, size);

		for (int i = 0; i < stages.Length; i++)
		{
			ControllerAssignment.DrawStagePreview(this, stages[i], cards[i]);

			// A card under a cursor wears that player's colour. Every card stays the same size,
			// so the captions sit on one baseline however the cursors move.
			foreach (MenuCursor cursor in cursors)
			{
				if (StageUnder(cursor.Position) != i) continue;
				CrayonBrush.InkRect(this, cards[i].Grow(13.0f), cursor.Tint, 7.0f, 41 + i, 3.0f);
				break;
			}
		}
	}
}
