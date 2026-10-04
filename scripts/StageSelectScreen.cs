using Godot;

/// <summary>
/// Pick where to fight: either player points at a stage and presses A. B goes back to the
/// fighters. Every person playing has a cursor, because they all just joined on the previous
/// screen and a cursor that vanishes reads as "you are not playing any more". A CPU has none.
///
/// Every card shows the real stage: a <see cref="Stage"/> built from its own
/// <see cref="StageData"/> and drawn live into a small viewport, framed on where the fighting
/// happens. It is the very renderer the match uses, so what you pick is exactly what you get -
/// paper, props, platforms and the traffic going past - and a stage added to
/// <see cref="StageCatalog"/> appears here with no work.
/// </summary>
public partial class StageSelectScreen : Node2D
{
	MenuCursor[] cursors;
	readonly StageData[] stages = new StageData[StageCatalog.Count];
	readonly Rect2[] cards = new Rect2[StageCatalog.Count];
	readonly SubViewport[] previews = new SubViewport[StageCatalog.Count];

	/// <summary>Previews render at twice the card's size, so the crayon lines stay crisp.</summary>
	const int PreviewResolution = 2;

	Label nameLabel;
	Label blurbLabel;
	Node2D cursorLayer;
	int shown = -1;

	const float CardWidth = 470.0f;
	const float CardGap = 40.0f;

	public override void _Ready()
	{
		bool[] cpu = GameRoot.Instance.CpuPlayers;
		int humans = 0;
		foreach (bool isCpu in cpu) if (!isCpu) humans++;
		cursors = new MenuCursor[Mathf.Max(1, humans)];

		IInputSource[] sources = ControllerAssignment.ForPlayers(cursors.Length);
		Vector2 viewport = GetViewportRect().Size;

		for (int i = 0; i < stages.Length; i++) stages[i] = StageCatalog.Get(i);

		float total = stages.Length * CardWidth + (stages.Length - 1) * CardGap;
		float left = (viewport.X - total) * 0.5f;
		for (int i = 0; i < cards.Length; i++)
		{
			cards[i] = new Rect2(left + i * (CardWidth + CardGap), 262.0f, CardWidth, 440.0f);
		}

		for (int i = 0; i < stages.Length; i++) previews[i] = BuildPreview(stages[i], cards[i].Size);

		// Humans keep their player number and colour; the CPU's seat is simply skipped.
		for (int player = 0, next = 0; player < cpu.Length && next < cursors.Length; player++)
		{
			if (cpu[player] && humans > 0) continue;
			Color tint = player == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent;
			cursors[next] = new MenuCursor(sources[next], player, tint,
				new Vector2(viewport.X * (0.4f + player * 0.2f), 900.0f));
			next++;
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

	/// <summary>
	/// The real stage, in its own little world: built, given a camera framed on the play area -
	/// every platform inside the blast zone and every spawn point, with room round them - and
	/// rendered every frame, so anything that moves on it moves here too.
	/// </summary>
	SubViewport BuildPreview(StageData data, Vector2 cardSize)
	{
		var view = new SubViewport
		{
			Size = new Vector2I((int)cardSize.X * PreviewResolution, (int)cardSize.Y * PreviewResolution),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			Disable3D = true,
		};
		AddChild(view);

		var stage = new Stage();
		view.AddChild(stage);
		stage.Build(data);

		Rect2 frame = new Rect2(data.SpawnPoints[0], Vector2.Zero);
		foreach (Vector2 spawn in data.SpawnPoints) frame = frame.Expand(spawn);
		foreach (StagePlatform platform in data.Platforms)
		{
			Rect2 inside = platform.Rect.Intersection(data.BlastZone);
			if (inside.Size.X > 0.0f && inside.Size.Y > 0.0f) frame = frame.Merge(inside);
		}
		frame = frame.Grow(70.0f);

		// Widen or heighten the frame to the card's shape, keeping it centred.
		float aspect = cardSize.X / cardSize.Y;
		if (frame.Size.X / frame.Size.Y < aspect) frame = frame.GrowIndividual((frame.Size.Y * aspect - frame.Size.X) * 0.5f, 0.0f, (frame.Size.Y * aspect - frame.Size.X) * 0.5f, 0.0f);
		else frame = frame.GrowIndividual(0.0f, (frame.Size.X / aspect - frame.Size.Y) * 0.5f, 0.0f, (frame.Size.X / aspect - frame.Size.Y) * 0.5f);

		var camera = new Camera2D
		{
			Position = frame.GetCenter(),
			Zoom = Vector2.One * (view.Size.X / frame.Size.X),
		};
		view.AddChild(camera);
		camera.MakeCurrent();
		return view;
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
				SfxPlayer.Ui("ui_back");
				GameRoot.Instance.GoCharacterSelect();
				return;
			}

			int under = StageUnder(cursor.Position);
			if (under >= 0 && under != shown)
			{
				shown = under;
				SfxPlayer.Ui("ui_move");
			}

			if (cursor.Confirm && under >= 0)
			{
				GameRoot.Instance.SelectedStage = under;
				SfxPlayer.Ui("ui_ready");
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
			DrawTextureRect(previews[i].GetTexture(), cards[i], false);
			CrayonBrush.InkRect(this, cards[i], MenuTheme.Ink, 4.0f, 17, 2.4f);

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
