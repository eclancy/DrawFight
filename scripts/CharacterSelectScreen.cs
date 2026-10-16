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
///
/// Nothing starts on its own. Once two or more seats are filled and everyone in one has picked, the
/// screen says so and waits for a player to press Start - so someone still reading a description is
/// not rushed into a fight.
///
/// Four seats, for up to four players (Eric, 2026-10-16); a seat nobody sits in simply sits the
/// fight out. Each offers a CPU button, so one person can play alone or fill a four-way fight with
/// the computer. Any joined player can add a CPU, change its fighter or remove it; and whoever owns
/// that seat's controller takes it back just by pressing A.
///
/// "Teams" up by the title turns it into a team fight (Eric, 2026-10-16): each seat gets a team
/// button - red, blue, green - and two teams or more start one. A person sets their own team; any
/// person can set a CPU's.
/// </summary>
public partial class CharacterSelectScreen : Node2D
{
	sealed class Slot
	{
		public MenuCursor Cursor;
		public bool Joined;
		public int Index = -1;
		public Label Name;
		public Label Credit;
		/// <summary>"Player 2", or "Player 2 - CPU" while the computer sits there.</summary>
		public Label Heading;
		public int Seat;
		public Label Blurb;
		public Label State;
		public Color Tint;
		public bool Locked => Index >= 0;
		public bool IsCpu;
		public bool Human => Joined && !IsCpu;
	}

	sealed class Card
	{
		public Rect2 Box;
		public FighterRig Rig;
	}

	/// <summary>
	/// Picking whose story to play (Story, on the main menu): one player, no CPU seat, and Start
	/// begins that fighter's story. A fighter whose story is not written yet cannot be picked.
	/// </summary>
	public bool ForStory;

	readonly Slot[] slots = new Slot[GameRoot.MaxPlayers];
	readonly Card[] cards = new Card[FighterCatalog.Count];

	static readonly Rect2 QuitBox = new Rect2(1640.0f, 64.0f, 200.0f, 76.0f);

	/// <summary>Every one for themselves, or teams.</summary>
	static readonly Rect2 TeamsBox = new Rect2(1180.0f, 64.0f, 420.0f, 76.0f);

	bool TeamBattle
	{
		get => GameRoot.Instance.TeamBattle;
		set => GameRoot.Instance.TeamBattle = value;
	}

	int[] Teams => GameRoot.Instance.SeatTeams;

	Vector2 mouse = new Vector2(-100.0f, -100.0f);

	const float CardTop = 196.0f;
	/// <summary>Cards shrink as the roster grows, so the row always fits between the margins.</summary>
	static readonly float CardWidth = Mathf.Min(300.0f, (1540.0f - (FighterCatalog.Count - 1) * 44.0f) / FighterCatalog.Count);
	const float CardHeight = 350.0f;
	const float CardGap = 44.0f;

	const float PanelTop = 572.0f;
	/// <summary>A story's one panel is wide; four seats share the row.</summary>
	float PanelWidth => ForStory ? 640.0f : 410.0f;
	const float PanelHeight = 358.0f;
	const float PanelGap = 16.0f;

	/// <summary>"Press Start to fight", shown once everyone has picked.</summary>
	Label startPrompt;
	int promptFrames;

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

		root.AddChild(MenuTheme.MakeLabel(ForStory ? "Whose story?" : "Pick your fighter", new Vector2(190.0f, 74.0f), 76, MenuTheme.Text));

		float rowWidth = cards.Length * CardWidth + (cards.Length - 1) * CardGap;
		float rowLeft = (viewport.X - rowWidth) * 0.5f;

		// Each portrait faces whichever way it likes, picked afresh every time the screen opens -
		// a row all facing the same way looked like a lineup (Eric, 2026-10-04). Never all one way.
		var facing = new int[cards.Length];
		var rng = new System.Random();
		for (int i = 0; i < facing.Length; i++) facing[i] = rng.Next(2) == 0 ? -1 : 1;
		if (System.Array.TrueForAll(facing, f => f == facing[0])) facing[rng.Next(facing.Length)] *= -1;

		for (int i = 0; i < cards.Length; i++)
		{
			var box = new Rect2(rowLeft + i * (CardWidth + CardGap), CardTop, CardWidth, CardHeight);
			FighterData data = FighterCatalog.Get(i);

			// The portrait lives in a window the size of the card above its name, and never draws
			// outside it. The rig positions itself inside its parent, so layout lives on a holder.
			var window = new Control
			{
				Position = box.Position + new Vector2(PortraitInset, PortraitInset),
				Size = new Vector2(box.Size.X - PortraitInset * 2.0f, box.Size.Y - NameHeight - PortraitInset),
				ClipContents = true,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			AddChild(window);
			var holder = new Node2D();
			window.AddChild(holder);
			var rig = new FighterRig();
			holder.AddChild(rig);
			if (rig.Load(data))
			{
				rig.Normalise(230.0f, 115.0f, data.VisualScale);
				rig.SetFacing(facing[i]);
				rig.Robotic = data.Robotic;
				rig.Inertia = data.Inertia;
				rig.ArmBackLimit = data.ArmReachBack;
				rig.CarryOnShoulder = data.ShouldersProp;
				rig.HandOnHip = data.HandOnHip;
				rig.SetPlanted(true);
				rig.SetStanding(true);
				FitPortrait(holder, rig, window.Size);
			}

			var name = MenuTheme.MakeLabel(data.DisplayName, new Vector2(box.Position.X, box.End.Y - 62.0f), 40, MenuTheme.Text);
			name.Size = new Vector2(CardWidth, 50.0f);
			name.HorizontalAlignment = HorizontalAlignment.Center;
			root.AddChild(name);

			cards[i] = new Card { Box = box, Rig = rig };
		}

		startPrompt = MenuTheme.MakeLabel(ForStory ? "Press Start to begin!" : "Press Start to fight!", new Vector2(0.0f, 948.0f), 64, MenuTheme.Accent);
		startPrompt.Size = new Vector2(viewport.X, 90.0f);
		startPrompt.HorizontalAlignment = HorizontalAlignment.Center;
		startPrompt.AddThemeColorOverride("font_outline_color", MenuTheme.Text);
		startPrompt.AddThemeConstantOverride("outline_size", 14);
		startPrompt.PivotOffset = new Vector2(viewport.X * 0.5f, 45.0f);
		startPrompt.Visible = false;
		root.AddChild(startPrompt);

		for (int i = 0; i < slots.Length; i++)
		{
			Rect2 panel = PanelRect(i, viewport);
			Color tint = MenuTheme.PlayerTint(i);

			var slot = new Slot
			{
				Cursor = new MenuCursor(sources[i], i, tint, panel.GetCenter()),
				Tint = tint,
			};

			slot.Seat = i;
			slot.Heading = MenuTheme.MakeLabel($"Player {i + 1}", panel.Position + new Vector2(36.0f, 22.0f), 34, tint);
			if (!ForStory || i == 0) root.AddChild(slot.Heading);

			var column = new VBoxContainer
			{
				Position = panel.Position + new Vector2(36.0f, ForStory ? 76.0f : 68.0f),
				Size = new Vector2(PanelWidth - 72.0f, 0.0f),
			};
			column.AddThemeConstantOverride("separation", ForStory ? 6 : 2);
			root.AddChild(column);

			slot.Name = MenuTheme.MakeLabel("", Vector2.Zero, ForStory ? 52 : 44, MenuTheme.Text);
			slot.Credit = MenuTheme.MakeLabel("", Vector2.Zero, ForStory ? 28 : 24, MenuTheme.Soft);
			slot.Blurb = MenuTheme.MakeLabel("", Vector2.Zero, ForStory ? 25 : 20, MenuTheme.Soft);
			slot.State = MenuTheme.MakeLabel("", Vector2.Zero, 30, tint);

			// Who drew it sits on the name's own line, so crediting the artist costs no room the
			// description needs - in the story's wide panel. Four seats across leave no room beside
			// the name, so there it goes under it.
			if (ForStory)
			{
				var heading = new HBoxContainer();
				heading.AddThemeConstantOverride("separation", 18);
				slot.Name.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
				slot.Credit.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
				heading.AddChild(slot.Name);
				heading.AddChild(slot.Credit);
				column.AddChild(heading);
			}
			else
			{
				column.AddChild(slot.Name);
				column.AddChild(slot.Credit);
			}
			foreach (Label label in new[] { slot.Blurb, slot.State })
			{
				label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				column.AddChild(label);
			}

			slots[i] = slot;

			// A story is one player's: already in.
			if (ForStory)
			{
				slot.Joined = i == 0;
				continue;
			}

			// Coming back from stage select, a CPU picked earlier is still sitting there.
			if (GameRoot.Instance.CpuPlayers[i] && GameRoot.Instance.SelectedFighters[i] >= 0)
			{
				slot.Joined = slot.IsCpu = true;
				slot.Index = GameRoot.Instance.SelectedFighters[i];
			}
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
				if (slot.Human) slot.Cursor.Draw(canvas);
			}
		});

		RefreshPanels();
	}

	/// <summary>How far inside the card's border its portrait window starts.</summary>
	const float PortraitInset = 6.0f;
	/// <summary>The strip along the bottom of a card that holds the fighter's name.</summary>
	const float NameHeight = 66.0f;

	/// <summary>
	/// Scales and places a portrait so the fighter fills its card window, standing on the bottom of
	/// it: as big as it goes before the body - not a held weapon - would touch the sides or the
	/// top. A sword or a hammer held out wider than the card is cropped by the window, like a photo
	/// framed close; the fighter is never shrunk to fit his weapon in. Eric's call, 2026-10-04:
	/// closer, and inside the box.
	/// </summary>
	static void FitPortrait(Node2D holder, FighterRig rig, Vector2 window)
	{
		// Settle into the standing pose first, so it is the pose that is measured.
		for (int i = 0; i < 12; i++) rig.PoseAt(FighterAnimations.Idle, 0.0f, 1.0f);
		Rect2 body = rig.BodyBounds(holder);
		if (body.Size.X < 1.0f || body.Size.Y < 1.0f) return;

		float scale = Mathf.Min(window.X * 0.92f / body.Size.X, window.Y * 0.95f / body.Size.Y);
		holder.Scale = Vector2.One * scale;
		holder.Position = new Vector2(window.X * 0.5f - body.GetCenter().X * scale, window.Y - 4.0f - body.End.Y * scale);
	}

	Rect2 PanelRect(int player, Vector2 viewport)
	{
		if (ForStory) return new Rect2((viewport.X - PanelWidth) * 0.5f, PanelTop, PanelWidth, PanelHeight);
		// Four across, inside the page's margin rule.
		const float Margin = 170.0f;
		float left = Margin + (viewport.X - Margin - 20.0f - (PanelWidth * slots.Length + PanelGap * (slots.Length - 1))) * 0.5f;
		return new Rect2(left + player * (PanelWidth + PanelGap), PanelTop, PanelWidth, PanelHeight);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			mouse = motion.Position;
			mouseOverQuit = QuitBox.HasPoint(motion.Position);
		}
		else if (@event is InputEventMouseButton click && click.Pressed && click.ButtonIndex == MouseButton.Left)
		{
			mouse = click.Position;
			if (QuitBox.HasPoint(click.Position)) GetTree().Quit();
			else if (!ForStory && TryTeamButtons(click.Position, null)) SfxPlayer.Ui("ui_select");
		}
	}

	/// <summary>The CPU buttons live in the bottom corner of a seat's panel.</summary>
	Rect2 CpuButton(int player, int which)
	{
		Rect2 panel = PanelRect(player, GetViewportRect().Size);
		const float W = 170.0f;
		const float H = 48.0f;
		return new Rect2(panel.End.X - 24.0f - W - which * (W + 14.0f), panel.End.Y - 14.0f - H, W, H);
	}

	/// <summary>
	/// A joined player pressing A on another seat's CPU buttons. Returns true if it was one, so
	/// the press is not also taken as a fighter pick.
	/// </summary>
	bool TryCpuButtons(Vector2 point)
	{
		for (int i = 0; i < slots.Length; i++)
		{
			Slot seat = slots[i];

			if (!seat.Joined && CpuButton(i, 0).HasPoint(point))
			{
				seat.Joined = seat.IsCpu = true;
				seat.Index = (int)(GD.Randi() % (uint)FighterCatalog.Count);
				return true;
			}

			if (!seat.IsCpu) continue;

			if (CpuButton(i, 1).HasPoint(point))
			{
				seat.Index = (seat.Index + 1) % FighterCatalog.Count;
				return true;
			}

			if (CpuButton(i, 0).HasPoint(point))
			{
				seat.Joined = seat.IsCpu = false;
				seat.Index = -1;
				return true;
			}
		}
		return false;
	}

	/// <summary>A seat's team button, on its name's line, in a team fight.</summary>
	Rect2 TeamButton(int player)
	{
		Rect2 panel = PanelRect(player, GetViewportRect().Size);
		return new Rect2(panel.End.X - 20.0f - 124.0f, panel.Position.Y + 78.0f, 124.0f, 48.0f);
	}

	/// <summary>
	/// The Teams switch, or a seat's team button: a person's own, or any CPU's. <paramref name="by"/>
	/// is who pressed it, or null for the mouse, which can set anyone's. True if it was one.
	/// </summary>
	bool TryTeamButtons(Vector2 point, Slot by)
	{
		if (TeamsBox.HasPoint(point))
		{
			TeamBattle = !TeamBattle;
			return true;
		}
		if (!TeamBattle) return false;
		for (int i = 0; i < slots.Length; i++)
		{
			Slot seat = slots[i];
			if (!seat.Joined || !TeamButton(i).HasPoint(point)) continue;
			if (by != null && seat.Human && seat != by) continue;
			Teams[i] = (Teams[i] + 1) % 3;
			return true;
		}
		return false;
	}

	/// <summary>The teams the seated players are on - two or more makes a team fight.</summary>
	int TeamsSeated()
	{
		var seen = new System.Collections.Generic.HashSet<int>();
		foreach (Slot slot in slots) if (slot.Joined) seen.Add(Teams[slot.Seat]);
		return seen.Count;
	}

	/// <summary>A small crayon button, outlined in the colour of any cursor over it.</summary>
	void DrawButton(Rect2 box, string text, int seed)
	{
		Color ink = MenuTheme.Rule;
		foreach (Slot slot in slots)
		{
			if (slot.Human && box.HasPoint(slot.Cursor.Position)) ink = slot.Tint;
		}

		DrawRect(box, new Color(1.0f, 1.0f, 1.0f, 0.85f));
		CrayonBrush.InkRect(this, box, ink, ink == MenuTheme.Rule ? 3.0f : 6.0f, seed, 2.2f);
		DrawString(ThemeDB.FallbackFont, new Vector2(box.Position.X, box.GetCenter().Y + 10.0f), text,
			HorizontalAlignment.Center, box.Size.X, 28, MenuTheme.Text);
	}

	/// <summary>A team button: filled with the team's colour, its name on it, outlined by any cursor over it.</summary>
	void DrawTeamButton(Rect2 box, int team, int seed)
	{
		Color ink = MenuTheme.Ink;
		foreach (Slot slot in slots)
		{
			if (slot.Human && box.HasPoint(slot.Cursor.Position)) ink = slot.Tint;
		}
		DrawRect(box, MenuTheme.TeamTint(team).Lightened(0.35f));
		CrayonBrush.InkRect(this, box, ink, ink == MenuTheme.Ink ? 3.0f : 6.0f, seed, 2.2f);
		DrawString(ThemeDB.FallbackFont, new Vector2(box.Position.X, box.GetCenter().Y + 10.0f), MenuTheme.TeamName(team).ToUpper(),
			HorizontalAlignment.Center, box.Size.X, 28, MenuTheme.Text);
	}

	int CardUnder(Vector2 point)
	{
		for (int i = 0; i < cards.Length; i++)
		{
			if (cards[i].Box.HasPoint(point)) return i;
		}
		return -1;
	}

	/// <summary>
	/// Two or more seats filled, everyone in one has picked, and at least one of them is a person.
	/// An empty seat sits the fight out.
	/// </summary>
	bool EveryoneReady()
	{
		if (ForStory) return slots[0].Locked;
		int seated = 0;
		bool anyHuman = false;
		foreach (Slot slot in slots)
		{
			if (!slot.Joined) continue;
			if (!slot.Locked) return false;
			seated++;
			anyHuman |= slot.Human;
		}
		return seated >= 2 && anyHuman && (!TeamBattle || TeamsSeated() >= 2);
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 viewport = GetViewportRect().Size;
		bool readyBefore = EveryoneReady();
		bool startFight = false;

		foreach (Slot slot in slots)
		{
			if (ForStory && slot != slots[0]) continue;
			MenuCursor cursor = slot.Cursor;
			cursor.Update(viewport);

			// With everyone picked, Start fights. Checked first, because Start also counts as A
			// and would otherwise re-pick whatever card the cursor happens to be resting on.
			if (readyBefore && slot.Human && cursor.Nav.Start)
			{
				startFight = true;
				continue;
			}

			if (!slot.Joined || slot.IsCpu)
			{
				// Pressing A on this seat's own controller joins it - replacing a CPU if one is
				// sitting there, because a person who wants to play always beats the computer.
				if (cursor.Confirm)
				{
					slot.Joined = true;
					slot.IsCpu = false;
					slot.Index = -1;
					SfxPlayer.Ui("ui_select");
				}
				continue;
			}

			if (cursor.Cancel)
			{
				// B takes a pick back; with nothing picked it goes back to the main menu.
				SfxPlayer.Ui("ui_back");
				if (slot.Locked)
				{
					slot.Index = -1;
				}
				else
				{
					GameRoot.Instance.GoMainMenu();
					return;
				}
			}

			if (!cursor.Confirm) continue;

			if (QuitBox.HasPoint(cursor.Position))
			{
				GetTree().Quit();
				return;
			}

			if (!ForStory && (TryTeamButtons(cursor.Position, slot) || TryCpuButtons(cursor.Position)))
			{
				SfxPlayer.Ui("ui_select");
				continue;
			}

			int picked = CardUnder(cursor.Position);
			if (picked >= 0 && ForStory && !HasStory(picked))
			{
				SfxPlayer.Ui("ui_back");
				continue;
			}
			if (picked >= 0)
			{
				slot.Index = picked;
				SfxPlayer.Ui("ui_ready");
			}
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

		bool everyoneReady = EveryoneReady();

		// The prompt pulses gently so it catches the eye without shouting. Everyone picked, but all
		// on one team, it says what is missing instead.
		promptFrames = everyoneReady ? promptFrames + 1 : 0;
		bool oneTeam = !ForStory && TeamBattle && !everyoneReady && TeamsSeated() < 2 && EveryonePicked();
		startPrompt.Text = oneTeam ? "Everyone's on one team!" : ForStory ? "Press Start to begin!" : "Press Start to fight!";
		startPrompt.Visible = everyoneReady || oneTeam;
		startPrompt.Scale = Vector2.One * (1.0f + 0.05f * Mathf.Sin(promptFrames * 0.12f));

		if (!startFight || !everyoneReady) return;

		SfxPlayer.Ui("ui_select");

		if (ForStory)
		{
			GameRoot.Instance.GoStory(StoryScript.IdOf(FighterCatalog.Get(slots[0].Index)));
			return;
		}

		for (int i = 0; i < slots.Length; i++)
		{
			GameRoot.Instance.SelectedFighters[i] = slots[i].Joined ? slots[i].Index : -1;
			GameRoot.Instance.CpuPlayers[i] = slots[i].Joined && slots[i].IsCpu;
		}
		GameRoot.Instance.GoStageSelect();
	}

	/// <summary>Two or more seated, and every one of them has picked.</summary>
	bool EveryonePicked()
	{
		int seated = 0;
		foreach (Slot slot in slots)
		{
			if (!slot.Joined) continue;
			if (!slot.Locked) return false;
			seated++;
		}
		return seated >= 2;
	}

	/// <summary>Whether a fighter's story can be played yet.</summary>
	static bool HasStory(int index) => StoryRun.For(StoryScript.IdOf(FighterCatalog.Get(index))).Chapters.Count > 0;

	void RefreshPanels()
	{
		foreach (Slot slot in slots)
		{
			if (slot.Heading != null) slot.Heading.Text = slot.Joined && slot.IsCpu ? $"Player {slot.Seat + 1} - CPU" : $"Player {slot.Seat + 1}";
			if (ForStory && slot != slots[0])
			{
				slot.State.Text = "";
				continue;
			}

			if (!slot.Joined)
			{
				slot.Name.Text = "";
				slot.Credit.Text = "";
				slot.Blurb.Text = "";
				// A seat with no controller of its own - no pad for it, and the keyboard's two seats
				// taken - can still be the computer.
				slot.State.Text = slot.Cursor.Nav.Source == null ? "Plug in a pad to join" : "Press A to join";
				continue;
			}

			if (slot.IsCpu)
			{
				slot.Name.Text = FighterCatalog.NameOf(slot.Index);
				slot.Credit.Text = FighterCatalog.CreditOf(slot.Index);
				slot.Blurb.Text = FighterCatalog.BlurbOf(slot.Index);
				// Said in the heading: down here it would sit on the CPU buttons.
				slot.State.Text = "";
				continue;
			}

			// Show what the cursor is over until something is picked, so a player can read a
			// fighter's description before committing to it.
			int shown = slot.Locked ? slot.Index : CardUnder(slot.Cursor.Position);
			slot.Name.Text = shown >= 0 ? FighterCatalog.NameOf(shown) : "";
			slot.Credit.Text = shown >= 0 ? FighterCatalog.CreditOf(shown) : "";
			slot.Blurb.Text = shown >= 0 ? FighterCatalog.BlurbOf(shown) : "";
			slot.State.Text = slot.Locked ? "READY" : "";
			if (ForStory && !slot.Locked && shown >= 0 && !HasStory(shown))
			{
				slot.State.Text = $"{FighterCatalog.NameOf(shown)}'s own story is coming soon.";
			}
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

			// A story played to the end wears a gold star.
			if (ForStory && StoryProgress.Finished(StoryScript.IdOf(FighterCatalog.Get(i))))
			{
				Item.DrawStarOn(this, cards[i].Box.Position + new Vector2(cards[i].Box.Size.X - 26.0f, 26.0f), 30.0f, new Color(1.0f, 0.82f, 0.25f));
			}
		}

		for (int i = 0; i < slots.Length; i++)
		{
			Slot slot = slots[i];
			if (ForStory && i > 0) continue;
			Rect2 panel = PanelRect(i, viewport);
			// In a team fight a seat is washed in its team's colour, and edged in it once picked.
			bool teamed = TeamBattle && !ForStory && slot.Joined;
			Color edge = teamed ? MenuTheme.TeamTint(Teams[i]) : slot.Tint;
			DrawRect(panel, teamed ? new Color(MenuTheme.TeamTint(Teams[i]), 0.16f) : new Color(1.0f, 1.0f, 1.0f, slot.Joined ? 0.55f : 0.3f));
			CrayonBrush.InkRect(this, panel, slot.Locked ? edge : MenuTheme.Rule,
				slot.Locked ? 6.0f : 3.0f, 51 + i * 7, 2.6f);
			if (teamed) DrawTeamButton(TeamButton(i), Teams[i], 97 + i);
		}

		for (int i = 0; i < slots.Length && !ForStory; i++)
		{
			Slot seat = slots[i];
			if (seat.Human) continue;

			if (seat.IsCpu)
			{
				DrawButton(CpuButton(i, 1), "CHANGE", 91 + i);
				DrawButton(CpuButton(i, 0), "REMOVE", 93 + i);
			}
			else
			{
				DrawButton(CpuButton(i, 0), "CPU", 95 + i);
			}
		}

		if (!ForStory)
		{
			bool teamsHot = TeamsBox.HasPoint(mouse);
			foreach (Slot slot in slots) teamsHot |= slot.Human && TeamsBox.HasPoint(slot.Cursor.Position);
			MenuTheme.DrawButton(this, TeamsBox, TeamBattle ? "Teams" : "Every one for themselves", 79, teamsHot, TeamBattle,
				fontSize: TeamBattle ? 40 : 28);
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
