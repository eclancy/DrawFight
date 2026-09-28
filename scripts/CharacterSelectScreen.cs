using Godot;

/// <summary>
/// Players join by pressing A, then drag a cursor onto a fighter and press A to pick it - the
/// Smash select screen. Nothing on the page explains buttons: pointing and pressing A is the
/// whole interface, and B takes a pick back.
///
/// Every fighter card is a live rig playing the shared idle animation, not a portrait. It costs
/// nothing extra and it means the moment a real drawing is imported, the select screen shows
/// that drawing breathing - the whole payoff of the project, before the match even starts.
///
/// Each fighter gets a short description of how it plays, never a list of its moves. Finding
/// out what a fighter's specials do is part of playing it.
/// </summary>
public partial class CharacterSelectScreen : Node2D
{
	sealed class Slot
	{
		public MenuCursor Cursor;
		public bool Joined;
		public int Index = -1;
		public Label Name;
		public Label Blurb;
		public Label State;
		public Color Tint;
		public bool Locked => Index >= 0;
	}

	sealed class Card
	{
		public Rect2 Box;
		public FighterRig Rig;
	}

	readonly Slot[] slots = new Slot[2];
	readonly Card[] cards = new Card[FighterCatalog.Count];

	static readonly Rect2 QuitBox = new Rect2(1640.0f, 64.0f, 200.0f, 76.0f);

	const float CardTop = 196.0f;
	const float CardWidth = 300.0f;
	const float CardHeight = 350.0f;
	const float CardGap = 44.0f;

	const float PanelTop = 600.0f;
	const float PanelWidth = 640.0f;
	const float PanelHeight = 330.0f;

	/// <summary>A beat on READY before moving on, so both players see that they both locked in.</summary>
	const int ReadyHoldFrames = 36;
	int readyFrames;

	bool mouseOverQuit;
	Node2D cursorLayer;

	public override void _Ready()
	{
		IInputSource[] sources = ControllerAssignment.ForPlayers(slots.Length);
		Vector2 viewport = GetViewportRect().Size;

		var layer = new CanvasLayer();
		AddChild(layer);
		var root = new Control { AnchorsPreset = (int)Control.LayoutPreset.FullRect };
		layer.AddChild(root);

		root.AddChild(MenuTheme.MakeLabel("Pick your fighter", new Vector2(190.0f, 74.0f), 76, MenuTheme.Text));

		float rowWidth = cards.Length * CardWidth + (cards.Length - 1) * CardGap;
		float rowLeft = (viewport.X - rowWidth) * 0.5f;

		for (int i = 0; i < cards.Length; i++)
		{
			var box = new Rect2(rowLeft + i * (CardWidth + CardGap), CardTop, CardWidth, CardHeight);
			FighterData data = FighterCatalog.Get(i);

			// The rig positions itself inside its parent, so layout lives on a holder above it.
			var holder = new Node2D { Position = new Vector2(box.GetCenter().X, box.Position.Y + 160.0f) };
			AddChild(holder);
			var rig = new FighterRig();
			holder.AddChild(rig);
			if (rig.Load(data.RigPath))
			{
				rig.Normalise(230.0f, 115.0f, data.VisualScale);
				rig.SetFacing(1);
			}

			var name = MenuTheme.MakeLabel(data.DisplayName, new Vector2(box.Position.X, box.End.Y - 62.0f), 40, MenuTheme.Text);
			name.Size = new Vector2(CardWidth, 50.0f);
			name.HorizontalAlignment = HorizontalAlignment.Center;
			root.AddChild(name);

			cards[i] = new Card { Box = box, Rig = rig };
		}

		for (int i = 0; i < slots.Length; i++)
		{
			Rect2 panel = PanelRect(i, viewport);
			Color tint = i == 0 ? MenuTheme.AccentTwo : MenuTheme.Accent;

			var slot = new Slot
			{
				Cursor = new MenuCursor(sources[i], i, tint, panel.GetCenter()),
				Tint = tint,
			};

			root.AddChild(MenuTheme.MakeLabel($"Player {i + 1}", panel.Position + new Vector2(36.0f, 22.0f), 34, tint));

			var column = new VBoxContainer
			{
				Position = panel.Position + new Vector2(36.0f, 76.0f),
				Size = new Vector2(PanelWidth - 72.0f, 0.0f),
			};
			column.AddThemeConstantOverride("separation", 6);
			root.AddChild(column);

			slot.Name = MenuTheme.MakeLabel("", Vector2.Zero, 52, MenuTheme.Text);
			slot.Blurb = MenuTheme.MakeLabel("", Vector2.Zero, 25, MenuTheme.Soft);
			slot.State = MenuTheme.MakeLabel("", Vector2.Zero, 30, tint);
			foreach (Label label in new[] { slot.Name, slot.Blurb, slot.State })
			{
				label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				column.AddChild(label);
			}

			slots[i] = slot;
		}

		var quitLabel = MenuTheme.MakeLabel("QUIT", QuitBox.Position, 40, MenuTheme.Text);
		quitLabel.Size = QuitBox.Size;
		quitLabel.HorizontalAlignment = HorizontalAlignment.Center;
		quitLabel.VerticalAlignment = VerticalAlignment.Center;
		root.AddChild(quitLabel);

		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			foreach (Slot slot in slots)
			{
				if (slot.Joined) slot.Cursor.Draw(canvas);
			}
		});

		RefreshPanels();
	}

	static Rect2 PanelRect(int player, Vector2 viewport)
	{
		const float Gap = 60.0f;
		float left = (viewport.X - (PanelWidth * 2.0f + Gap)) * 0.5f;
		return new Rect2(left + player * (PanelWidth + Gap), PanelTop, PanelWidth, PanelHeight);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			mouseOverQuit = QuitBox.HasPoint(motion.Position);
		}
		else if (@event is InputEventMouseButton click && click.Pressed
			&& click.ButtonIndex == MouseButton.Left && QuitBox.HasPoint(click.Position))
		{
			GetTree().Quit();
		}
	}

	int CardUnder(Vector2 point)
	{
		for (int i = 0; i < cards.Length; i++)
		{
			if (cards[i].Box.HasPoint(point)) return i;
		}
		return -1;
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;

		foreach (Slot slot in slots)
		{
			MenuCursor cursor = slot.Cursor;
			cursor.Update(viewport);

			if (!slot.Joined)
			{
				if (cursor.Confirm) slot.Joined = true;
				continue;
			}

			if (cursor.Cancel)
			{
				// B takes a pick back; with nothing picked it goes back to the title.
				if (slot.Locked)
				{
					slot.Index = -1;
				}
				else
				{
					GameRoot.Instance.GoTitle();
					return;
				}
			}

			if (!cursor.Confirm) continue;

			if (QuitBox.HasPoint(cursor.Position))
			{
				GetTree().Quit();
				return;
			}

			int picked = CardUnder(cursor.Position);
			if (picked >= 0) slot.Index = picked;
		}

		RefreshPanels();

		foreach (Card card in cards)
		{
			if (card.Rig == null || !card.Rig.Loaded) continue;
			card.Rig.Play(FighterAnimations.Idle);
			card.Rig.Advance();
		}

		QueueRedraw();
		cursorLayer.QueueRedraw();

		bool everyoneReady = true;
		foreach (Slot slot in slots) everyoneReady &= slot.Joined && slot.Locked;

		readyFrames = everyoneReady ? readyFrames + 1 : 0;
		if (readyFrames < ReadyHoldFrames) return;

		for (int i = 0; i < slots.Length; i++) GameRoot.Instance.SelectedFighters[i] = slots[i].Index;
		GameRoot.Instance.GoStageSelect();
	}

	void RefreshPanels()
	{
		foreach (Slot slot in slots)
		{
			if (!slot.Joined)
			{
				slot.Name.Text = "";
				slot.Blurb.Text = "";
				slot.State.Text = "Press A to join";
				continue;
			}

			// Show what the cursor is over until something is picked, so a player can read a
			// fighter's description before committing to it.
			int shown = slot.Locked ? slot.Index : CardUnder(slot.Cursor.Position);
			slot.Name.Text = shown >= 0 ? FighterCatalog.NameOf(shown) : "";
			slot.Blurb.Text = shown >= 0 ? FighterCatalog.BlurbOf(shown) : "";
			slot.State.Text = slot.Locked ? "READY" : "";
		}
	}

	public override void _Draw()
	{
		Vector2 viewport = GetViewportRect().Size;
		MenuTheme.DrawPage(this, viewport);

		for (int i = 0; i < cards.Length; i++)
		{
			DrawRect(cards[i].Box, new Color(1.0f, 1.0f, 1.0f, 0.55f));

			// A picked card wears the border of whoever picked it; one picked by both players
			// shows both, one inside the other.
			float grow = 0.0f;
			bool picked = false;
			foreach (Slot slot in slots)
			{
				if (slot.Index != i) continue;
				CrayonBrush.InkRect(this, cards[i].Box.Grow(grow), slot.Tint, 6.0f, 31 + i * 7 + (int)grow, 2.6f);
				grow += 10.0f;
				picked = true;
			}
			if (!picked) CrayonBrush.InkRect(this, cards[i].Box, MenuTheme.Rule, 3.0f, 31 + i * 7, 2.6f);
		}

		for (int i = 0; i < slots.Length; i++)
		{
			Slot slot = slots[i];
			Rect2 panel = PanelRect(i, viewport);
			DrawRect(panel, new Color(1.0f, 1.0f, 1.0f, slot.Joined ? 0.55f : 0.3f));
			CrayonBrush.InkRect(this, panel, slot.Locked ? slot.Tint : MenuTheme.Rule,
				slot.Locked ? 6.0f : 3.0f, 51 + i * 7, 2.6f);
		}

		bool quitHovered = mouseOverQuit;
		Color quitInk = MenuTheme.Rule;
		foreach (Slot slot in slots)
		{
			if (!slot.Joined || !QuitBox.HasPoint(slot.Cursor.Position)) continue;
			quitInk = slot.Tint;
			quitHovered = true;
		}
		if (mouseOverQuit && quitInk == MenuTheme.Rule) quitInk = MenuTheme.Ink;

		DrawRect(QuitBox, new Color(1.0f, 1.0f, 1.0f, 0.55f));
		CrayonBrush.InkRect(this, QuitBox, quitInk, quitHovered ? 6.0f : 3.0f, 77, 2.6f);
	}
}
