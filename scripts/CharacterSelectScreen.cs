using System.Collections.Generic;
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
/// fight out. Players are numbered in the order they press a button (Eric, 2026-10-16): whoever
/// presses A first is Player 1, whatever they are holding - a pad, or either half of the keyboard -
/// the next is Player 2, and so on, each into the lowest empty seat; with none left, they take a
/// CPU's. B with nothing picked gets up out of the seat. Each seat offers a CPU button, so one
/// person can play alone or fill a four-way fight with the computer. The mouse does nothing here.
///
/// "Teams" up by the title turns it into a team fight (Eric, 2026-10-16): each seat gets a team
/// button - red, blue, green - and two teams or more start one. A person sets their own team; any
/// person can set a CPU's.
///
/// Anyone sitting down can give themselves a name (Eric, 2026-10-17): the Name button in their
/// seat opens a picker over it - the names saved on this computer (PlayerNames) to pick from, and
/// letters to spell a new one with the cursor, or typed on the keyboard by someone playing on it.
/// The name goes over their head and under their percent in the fight. Names are deleted in the
/// options.
/// </summary>
public partial class CharacterSelectScreen : Node2D
{
	sealed class Slot
	{
		public MenuCursor Cursor;
		public bool Joined;
		public int Index = -1;
		/// <summary>The fighter's name, in their own letters (NameLettering).</summary>
		public NameTag Name;
		public Label Credit;
		/// <summary>"Player 2", or "Player 2 - CPU" while the computer sits there.</summary>
		public Label Heading;
		public int Seat;
		public Label Blurb;
		public Label State;
		public Color Tint;
		/// <summary>The name this seat gave itself, or null.</summary>
		public string PlayerName;
		/// <summary>The name picker is open over this seat, with what has been spelled so far.</summary>
		public bool Naming;
		public string Draft = "";
		/// <summary>Whoever sits here (ControllerAssignment.Devices), or null for nobody, or the computer.</summary>
		public Device Device;
		public bool Locked => Index >= 0;

		/// <summary>The skin picked for his fighter (Skins), "" for none - picked by picking him again.</summary>
		public string Skin = "";
		public bool IsCpu;
		public bool Human => Joined && !IsCpu;
	}

	/// <summary>A controller that can join, with the one cursor it drives, seated or not.</summary>
	sealed class Device
	{
		public string Key;
		public MenuCursor Cursor;
		public Slot Seat;
	}

	/// <summary>Every controller there is (ControllerAssignment.Devices), each polled once a frame.</summary>
	readonly System.Collections.Generic.List<Device> devices = new System.Collections.Generic.List<Device>();

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

	/// <summary>Everyone for themselves, or teams.</summary>
	static readonly Rect2 TeamsBox = new Rect2(1180.0f, 64.0f, 420.0f, 76.0f);

	/// <summary>
	/// How many items: Off, Low, Medium, High, a press at a time - set here as well as on stage select
	/// (which also picks which items), so it is to hand before the fight (Eric, 2026-10-16).
	/// </summary>
	static readonly Rect2 ItemsBox = new Rect2(840.0f, 64.0f, 310.0f, 76.0f);

	/// <summary>
	/// A story's Easy, Medium and Hard, picked here, before the story, rather than on its chapter
	/// map (Eric, 2026-10-22). Remembered for every story (GameOptions.StoryDifficulty). Along the
	/// bottom on the left, under the word "Difficulty", with Start to the right of them (Eric,
	/// 2026-10-24: not up the top with a "How hard?" over them).
	/// </summary>
	static Rect2 DifficultyBox(int d) => new Rect2(DifficultyLeft + d * 194.0f, 992.0f, 182.0f, 66.0f);

	const float DifficultyLeft = RowLeft + 196.0f;

	static readonly string[] Difficulties = { "Easy", "Medium", "Hard" };

	bool TryDifficulty(Vector2 at)
	{
		for (int d = 0; d < Difficulties.Length; d++)
		{
			if (!DifficultyBox(d).HasPoint(at)) continue;
			GameOptions.StoryDifficulty = d;
			GameOptions.Save();
			return true;
		}
		return false;
	}

	bool TeamBattle
	{
		get => GameRoot.Instance.TeamBattle;
		set => GameRoot.Instance.TeamBattle = value;
	}

	int[] Teams => GameRoot.Instance.SeatTeams;

	// The page from the margin rule to the right edge, top to bottom: the cards as wide and tall
	// as fits, the seats under them, and a strip along the bottom for "Press Start to fight!"
	// (Eric, 2026-10-20: the bottom of the page sat empty).
	const float RowLeft = 190.0f;
	const float RowRight = 1880.0f;
	const float CardTop = 196.0f;
	const float CardGap = 26.0f;
	/// <summary>Cards shrink as the roster grows, so the row always fits between the margins.</summary>
	static readonly float CardWidth = Mathf.Min(320.0f, (RowRight - RowLeft - (FighterCatalog.Count - 1) * CardGap) / FighterCatalog.Count);
	/// <summary>
	/// A story has one panel, not four, so the page below the cards is free: the cards stand
	/// taller, and the one panel runs the width of the row under them, so the fighters are bigger
	/// and the description reads on a line or two (Eric, 2026-10-24: both were squished).
	/// </summary>
	float CardHeight => ForStory ? 548.0f : 410.0f;

	float PanelTop => ForStory ? 764.0f : 626.0f;
	/// <summary>A story's one panel is the width of the row; four seats share it.</summary>
	float PanelWidth => ForStory ? RowRight - RowLeft : 410.0f;
	float PanelHeight => ForStory ? 206.0f : 350.0f;
	const float PanelGap = 16.0f;

	/// <summary>"Press Start to fight", shown once everyone has picked.</summary>
	Label startPrompt;
	int promptFrames;

	Node2D cursorLayer;
	Node2D namingLayer;
	readonly QuitPrompt quitPrompt = new QuitPrompt();

	public override void _Ready()
	{
		Vector2 viewport = GetViewportRect().Size;
		SyncDevices();

		var layer = new CanvasLayer();
		AddChild(layer);
		var root = new Control { AnchorsPreset = (int)Control.LayoutPreset.FullRect };
		layer.AddChild(root);

		root.AddChild(MenuTheme.MakeLabel(ForStory ? "Whose story?" : "Pick your fighter", new Vector2(190.0f, 74.0f), 76, MenuTheme.Text));

		float rowWidth = cards.Length * CardWidth + (cards.Length - 1) * CardGap;
		float rowLeft = RowLeft + (RowRight - RowLeft - rowWidth) * 0.5f;

		// Each portrait faces whichever way it likes, picked afresh every time the screen opens -
		// a row all facing the same way looked like a lineup (Eric, 2026-10-04). Never all one way.
		var facing = new int[cards.Length];
		var rng = new System.Random();
		for (int i = 0; i < facing.Length; i++) facing[i] = rng.Next(2) == 0 ? -1 : 1;
		if (System.Array.TrueForAll(facing, f => f == facing[0])) facing[rng.Next(facing.Length)] *= -1;

		for (int i = 0; i < cards.Length; i++)
		{
			var box = new Rect2(rowLeft + Place(i) * (CardWidth + CardGap), CardTop, CardWidth, CardHeight);
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
				rig.BackArmForward = data.IdleBackArmForward;
				rig.SetPlanted(true);
				rig.SetStanding(true);
				FitPortrait(holder, rig, window.Size);
			}
			// Still to be earned: only his shape shows (Unlocks).
			bool locked = Unlocks.IsLocked(data);
			if (locked) holder.Modulate = Silhouette;

			// The name in his own letters - as he will see it over his percent in the fight.
			var name = new NameTag
			{
				FontSize = 40,
				Align = HorizontalAlignment.Center,
				FitWidth = true,
				Position = new Vector2(box.Position.X + 4.0f, box.End.Y - 62.0f),
				Size = new Vector2(CardWidth - 8.0f, 54.0f),
			};
			if (locked) name.SetText(LockedName, NameStyle.Plain(MenuTheme.Soft));
			else name.SetFighter(data);
			root.AddChild(name);

			cards[i] = new Card { Box = box, Rig = rig };
		}

		startPrompt = MenuTheme.MakeLabel(ForStory ? "Press Start to begin!" : "Press Start to fight!", new Vector2(0.0f, 982.0f), 64, MenuTheme.Accent);
		// In a story the difficulty takes the bottom left, so Start sits in the rest of the row.
		float promptLeft = ForStory ? DifficultyBox(Difficulties.Length - 1).End.X + 20.0f : 0.0f;
		startPrompt.Position = new Vector2(promptLeft, startPrompt.Position.Y);
		startPrompt.Size = new Vector2(viewport.X - promptLeft - (ForStory ? viewport.X - RowRight : 0.0f), 90.0f);
		startPrompt.HorizontalAlignment = HorizontalAlignment.Center;
		startPrompt.AddThemeColorOverride("font_outline_color", MenuTheme.Text);
		startPrompt.AddThemeConstantOverride("outline_size", 14);
		startPrompt.PivotOffset = startPrompt.Size * 0.5f;
		startPrompt.Visible = false;
		root.AddChild(startPrompt);

		for (int i = 0; i < slots.Length; i++)
		{
			Rect2 panel = PanelRect(i, viewport);
			Color tint = MenuTheme.PlayerTint(i);

			var slot = new Slot { Tint = tint };

			slot.Seat = i;
			slot.Heading = MenuTheme.MakeLabel($"Player {i + 1}", panel.Position + new Vector2(36.0f, 22.0f), 34, tint);
			// A story is one player's, so the panel is all about the fighter, not whose seat it is.
			root.AddChild(slot.Heading);
			slot.Heading.Visible = !ForStory;

			var column = new VBoxContainer
			{
				Position = panel.Position + new Vector2(36.0f, ForStory ? 18.0f : 68.0f),
				Size = new Vector2(PanelWidth - 72.0f, 0.0f),
			};
			column.AddThemeConstantOverride("separation", ForStory ? 6 : 2);
			root.AddChild(column);

			slot.Name = new NameTag { FontSize = ForStory ? 52 : 44, FitWidth = !ForStory };
			slot.Credit = MenuTheme.MakeLabel("", Vector2.Zero, ForStory ? 28 : 24, MenuTheme.Soft);
			slot.Blurb = MenuTheme.MakeLabel("", Vector2.Zero, ForStory ? 30 : 20, MenuTheme.Soft);
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

			// A story is one player's: already in, on whichever controller touches it (TickStory).
			if (ForStory)
			{
				if (i == 0) Sit(devices[0], slot);
				continue;
			}

			// Coming back, everyone is still sitting where they were with what they picked: the
			// computer, and anyone whose controller is still here.
			int picked = GameRoot.Instance.SelectedFighters[i];
			if (picked >= 0 && Unlocks.IsLocked(picked)) picked = -1;
			if (GameRoot.Instance.CpuPlayers[i] && picked >= 0)
			{
				slot.Joined = slot.IsCpu = true;
				slot.Index = picked;
			}
			else if (picked >= 0 && ControllerAssignment.IsConnected(GameRoot.Instance.SeatDevices[i]))
			{
				Device device = devices.Find(d => d.Key == GameRoot.Instance.SeatDevices[i]);
				if (device != null && device.Seat == null)
				{
					Sit(device, slot);
					slot.Index = picked;
					slot.PlayerName = GameRoot.Instance.SeatNames[i];
				}
			}
		}

		// Whoever chose Versus is already sitting down: Player 1, or the lowest seat free.
		string starter = GameRoot.Instance.VersusStarter;
		GameRoot.Instance.VersusStarter = null;
		Device first = ForStory || starter == null ? null : devices.Find(d => d.Key == starter);
		if (first != null && first.Seat == null)
		{
			Slot seat = System.Array.Find(slots, s => !s.Joined);
			if (seat != null) Sit(first, seat);
		}

		var quitLabel = MenuTheme.MakeLabel("QUIT", QuitBox.Position, 40, MenuTheme.Text);
		quitLabel.Size = QuitBox.Size;
		quitLabel.HorizontalAlignment = HorizontalAlignment.Center;
		quitLabel.VerticalAlignment = VerticalAlignment.Center;
		root.AddChild(quitLabel);

		// The name pickers, over the cards and labels but under every cursor.
		namingLayer = MenuCursor.MakeOverlay(this, DrawPickers);
		cursorLayer = MenuCursor.MakeOverlay(this, canvas =>
		{
			var pointing = new List<Vector2>();
			foreach (Slot slot in slots) if (slot.Human && slot.Cursor != null) pointing.Add(slot.Cursor.Position);
			quitPrompt.Draw(canvas, pointing);
			foreach (Slot slot in slots)
			{
				if (slot.Human && slot.Cursor != null) slot.Cursor.Draw(canvas);
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
		if (ForStory) return new Rect2(RowLeft, PanelTop, PanelWidth, PanelHeight);
		// Four across, inside the page's margin rule.
		const float Margin = 170.0f;
		float left = Margin + (viewport.X - Margin - 20.0f - (PanelWidth * slots.Length + PanelGap * (slots.Length - 1))) * 0.5f;
		return new Rect2(left + player * (PanelWidth + PanelGap), PanelTop, PanelWidth, PanelHeight);
	}

	// --- Who sits where ---------------------------------------------------------------------

	/// <summary>Every controller there is now, each with its cursor - a pad plugged in joins the list.</summary>
	void SyncDevices()
	{
		foreach (string key in ControllerAssignment.Devices())
		{
			if (devices.Exists(d => d.Key == key)) continue;
			devices.Add(new Device
			{
				Key = key,
				Cursor = new MenuCursor(ControllerAssignment.Open(key), -1, MenuTheme.Soft, new Vector2(-200.0f, -200.0f)),
			});
		}
	}

	/// <summary>A controller sits down in a seat: its cursor takes the seat's number and colour.</summary>
	void Sit(Device device, Slot slot)
	{
		slot.Joined = true;
		slot.IsCpu = false;
		slot.Index = -1;
		slot.Device = device;
		slot.Cursor = device.Cursor;
		device.Seat = slot;
		device.Cursor.Player = slot.Seat;
		device.Cursor.Tint = slot.Tint;
		device.Cursor.Position = PanelRect(slot.Seat, GetViewportRect().Size).GetCenter();
	}

	/// <summary>Up out of a seat: it is empty again, and the controller can sit anywhere.</summary>
	void Stand(Slot slot)
	{
		if (slot.Device != null) slot.Device.Seat = null;
		slot.Device = null;
		slot.Cursor = null;
		slot.Joined = false;
		slot.Index = -1;
		slot.PlayerName = null;
		slot.Naming = false;
	}

	/// <summary>
	/// A controller nobody is sitting at pressed A: into the lowest empty seat, or, with none, the
	/// lowest seat the computer has. False if every seat has a person in it.
	/// </summary>
	bool Join(Device device)
	{
		Slot seat = System.Array.Find(slots, s => !s.Joined) ?? System.Array.Find(slots, s => s.IsCpu);
		if (seat == null) return false;
		Sit(device, seat);
		SfxPlayer.Ui("ui_select");
		return true;
	}

	/// <summary>
	/// Whose story it is: one seat, driven by whichever controller was touched last - so it never
	/// matters which one is holding it. True when another controller takes over: that touch does
	/// nothing else.
	/// </summary>
	bool TickStory(Device device)
	{
		Slot seat = slots[0];
		if (seat.Device == device) return false;
		MenuNav nav = device.Cursor.Nav;
		if (!nav.Confirm && !nav.Cancel && !nav.Start && nav.Stick.LengthSquared() < 0.09f) return false;
		Vector2 at = seat.Cursor?.Position ?? device.Cursor.Position;
		if (seat.Device != null) seat.Device.Seat = null;
		int picked = seat.Index;
		Sit(device, seat);
		seat.Index = picked;
		device.Cursor.Position = at;
		return true;
	}

	// --- Names ----------------------------------------------------------------------------

	/// <summary>A person's Name button, where a CPU seat has its CPU button.</summary>
	Rect2 NameButton(int player) => CpuButton(player, 0);

	/// <summary>The seat whose name is being typed on the keyboard, if one is.</summary>
	Slot typingSlot;

	static readonly string[] Keys =
	{
		"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O",
		"P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "SPACE", "DEL", "CLEAR", "OK",
	};

	const int KeyColumns = 6;
	const int SavedShown = 4;

	/// <summary>
	/// The picker, in the seat's own selection box - over it and down to the foot of the page - so
	/// four can name themselves at once and the cards stay in sight (Eric, 2026-10-24: it used to
	/// open up over the cards).
	/// </summary>
	Rect2 PickerRect(int player)
	{
		Vector2 viewport = GetViewportRect().Size;
		Rect2 panel = PanelRect(player, viewport);
		return new Rect2(panel.Position, new Vector2(panel.Size.X, viewport.Y - 12.0f - panel.Position.Y));
	}

	Rect2 FieldRect(int player)
	{
		Rect2 p = PickerRect(player);
		return new Rect2(p.Position.X + 16.0f, p.Position.Y + 14.0f, p.Size.X - 32.0f, 54.0f);
	}

	Rect2 SavedRect(int player, int i)
	{
		Rect2 p = PickerRect(player);
		float w = (p.Size.X - 32.0f - 10.0f) * 0.5f;
		return new Rect2(p.Position.X + 16.0f + i % 2 * (w + 10.0f), p.Position.Y + 80.0f + i / 2 * 46.0f, w, 40.0f);
	}

	Rect2 KeyRect(int player, int i)
	{
		Rect2 p = PickerRect(player);
		const float Gap = 6.0f;
		float w = (p.Size.X - 32.0f - Gap * (KeyColumns - 1)) / KeyColumns;
		const float H = 42.0f;
		return new Rect2(p.Position.X + 16.0f + i % KeyColumns * (w + Gap), p.Position.Y + 180.0f + i / KeyColumns * (H + Gap), w, H);
	}

	/// <summary>The names saved here to offer a seat: the most recent ones nobody else is using right now.</summary>
	List<string> Offered(Slot slot)
	{
		var offered = new List<string>();
		foreach (string name in PlayerNames.All)
		{
			if (offered.Count >= SavedShown) break;
			if (System.Array.Exists(slots, s => s != slot && s.Human && s.PlayerName == name)) continue;
			offered.Add(name);
		}
		return offered;
	}

	void TickNaming(Slot slot, Vector2 viewport)
	{
		MenuCursor cursor = slot.Cursor;
		// On the keyboard, the letters are typed, so its keys do not also drive the cursor.
		if (slot == typingSlot) return;
		cursor.Move(viewport);
		if (cursor.Cancel)
		{
			CloseNaming(slot, false);
			return;
		}
		if (cursor.Nav.Start)
		{
			CloseNaming(slot, true);
			return;
		}
		if (!cursor.Confirm) return;

		List<string> offered = Offered(slot);
		for (int i = 0; i < offered.Count; i++)
		{
			if (!SavedRect(slot.Seat, i).HasPoint(cursor.Position)) continue;
			slot.Draft = offered[i];
			CloseNaming(slot, true);
			return;
		}
		for (int i = 0; i < Keys.Length; i++)
		{
			if (!KeyRect(slot.Seat, i).HasPoint(cursor.Position)) continue;
			PressKey(slot, Keys[i]);
			return;
		}
	}

	void PressKey(Slot slot, string key)
	{
		switch (key)
		{
			case "OK": CloseNaming(slot, true); return;
			case "DEL": if (slot.Draft.Length > 0) slot.Draft = slot.Draft.Substring(0, slot.Draft.Length - 1); break;
			case "CLEAR": slot.Draft = ""; break;
			case "SPACE": Type(slot, ' '); break;
			default: Type(slot, key[0]); break;
		}
		SfxPlayer.Ui("ui_move");
	}

	/// <summary>A letter onto the name: capital at the start of each word, small after.</summary>
	static void Type(Slot slot, char c)
	{
		if (slot.Draft.Length >= PlayerNames.MaxLength) return;
		if (c == ' ' && (slot.Draft.Length == 0 || slot.Draft.EndsWith(" "))) return;
		bool startOfWord = slot.Draft.Length == 0 || slot.Draft.EndsWith(" ");
		slot.Draft += startOfWord ? char.ToUpper(c) : char.ToLower(c);
	}

	void CloseNaming(Slot slot, bool keep)
	{
		slot.Naming = false;
		if (typingSlot == slot) typingSlot = null;
		if (keep)
		{
			string name = slot.Draft.Trim();
			slot.PlayerName = name.Length > 0 ? name : null;
			if (slot.PlayerName != null) PlayerNames.Use(slot.PlayerName);
			SfxPlayer.Ui("ui_ready");
		}
		else SfxPlayer.Ui("ui_back");
	}

	/// <summary>Someone playing on the keyboard types their name: letters, numbers, Backspace, Enter to keep it, Escape to leave it.</summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (typingSlot == null || @event is not InputEventKey key || !key.Pressed) return;
		GetViewport().SetInputAsHandled();
		switch (key.Keycode)
		{
			case Key.Enter:
			case Key.KpEnter:
				CloseNaming(typingSlot, true);
				return;
			case Key.Escape:
				CloseNaming(typingSlot, false);
				return;
			case Key.Backspace:
				if (typingSlot.Draft.Length > 0) typingSlot.Draft = typingSlot.Draft.Substring(0, typingSlot.Draft.Length - 1);
				return;
			case Key.Tab:
			{
				// Tab steps through the names saved here.
				List<string> offered = Offered(typingSlot);
				if (offered.Count == 0) return;
				int at = offered.IndexOf(typingSlot.Draft);
				typingSlot.Draft = offered[(at + 1) % offered.Count];
				return;
			}
		}
		char c = (char)key.Unicode;
		if (char.IsLetterOrDigit(c) || c == ' ' || c == '-') Type(typingSlot, c);
	}

	readonly List<MenuTheme.ButtonSpec> pickerButtons = new List<MenuTheme.ButtonSpec>();

	string lastMainState, lastPickerState;

	/// <summary>Everything the page's own drawing shows, as text: when it changes, it is drawn again.</summary>
	string MainDrawState()
	{
		var state = new System.Text.StringBuilder();
		state.Append(TeamBattle).Append(GameRoot.Instance.Items.Frequency).Append(GameOptions.StoryDifficulty).Append(quitPrompt.Open);
		for (int i = 0; i < slots.Length; i++)
		{
			Slot slot = slots[i];
			state.Append('|').Append(slot.Joined).Append(slot.Human).Append(slot.IsCpu).Append(slot.Index).Append(slot.Naming)
				.Append(slot.PlayerName).Append(Teams[i]).Append(GameRoot.Instance.CpuDifficulty[i]);
			if (slot.Human && slot.Cursor != null) state.Append(':').Append(ButtonUnder(slot.Cursor.Position));
		}
		return state.ToString();
	}

	/// <summary>Which of the page's buttons a point is on, if any - each lights up under a cursor.</summary>
	string ButtonUnder(Vector2 at)
	{
		if (QuitBox.HasPoint(at)) return "quit";
		if (!ForStory && TeamsBox.HasPoint(at)) return "teams";
		if (!ForStory && ItemsBox.HasPoint(at)) return "items";
		for (int d = 0; d < Difficulties.Length; d++) if (ForStory && DifficultyBox(d).HasPoint(at)) return "d" + d;
		for (int i = 0; i < slots.Length && !ForStory; i++)
		{
			for (int k = 0; k < 3; k++) if (CpuButton(i, k).HasPoint(at)) return $"c{i}{k}";
			if (TeamButton(i).HasPoint(at)) return "t" + i;
		}
		return "";
	}

	/// <summary>Everything the name pickers show: the name so far, the blinking caret, and the key under each cursor.</summary>
	string PickerDrawState()
	{
		var state = new System.Text.StringBuilder();
		foreach (Slot slot in slots)
		{
			if (!slot.Naming) continue;
			state.Append('|').Append(slot.Seat).Append(slot.Draft).Append(slot == typingSlot).Append((Engine.GetPhysicsFrames() / 30) % 2);
			if (slot.Cursor == null) continue;
			Vector2 at = slot.Cursor.Position;
			int over = -1;
			for (int i = 0; i < Keys.Length && over < 0; i++) if (KeyRect(slot.Seat, i).HasPoint(at)) over = i;
			for (int i = 0; i < SavedShown && over < 0; i++) if (SavedRect(slot.Seat, i).HasPoint(at)) over = 100 + i;
			state.Append(':').Append(over);
		}
		return state.ToString();
	}

	void DrawPickers(CanvasItem canvas)
	{
		Font font = ThemeDB.FallbackFont;
		foreach (Slot slot in slots)
		{
			if (!slot.Naming) continue;
			int seat = slot.Seat;
			Rect2 box = PickerRect(seat);
			canvas.DrawRect(new Rect2(box.Position + new Vector2(8.0f, 10.0f), box.Size), new Color(0.0f, 0.0f, 0.0f, 0.08f));
			canvas.DrawRect(box, MenuTheme.Paper);
			CrayonBrush.InkRect(canvas, box, slot.Tint, 6.0f, 870 + seat, 2.4f);

			Rect2 field = FieldRect(seat);
			canvas.DrawRect(field, Colors.White);
			CrayonBrush.InkRect(canvas, field, MenuTheme.Ink, 3.0f, 880 + seat, 1.6f);
			bool blink = (Engine.GetPhysicsFrames() / 30) % 2 == 0;
			string shown = slot.Draft.Length > 0 ? slot.Draft + (blink ? "_" : " ") : (blink ? "_" : "");
			MenuTheme.DrawCentredText(canvas, field, shown, 40, MenuTheme.Text);
			if (slot.Draft.Length == 0)
			{
				canvas.DrawString(font, new Vector2(field.Position.X + 12.0f, field.End.Y - 8.0f), "Your name", HorizontalAlignment.Left, -1, 20, MenuTheme.Soft);
			}

			List<string> offered = Offered(slot);
			pickerButtons.Clear();
			for (int i = 0; i < offered.Count; i++)
			{
				Rect2 r = SavedRect(seat, i);
				pickerButtons.Add(new MenuTheme.ButtonSpec(r, offered[i], 890 + seat * 9 + i,
					slot.Cursor != null && r.HasPoint(slot.Cursor.Position), true, 24, slot.Tint));
			}
			if (offered.Count == 0)
			{
				canvas.DrawString(font, new Vector2(box.Position.X + 20.0f, box.Position.Y + 128.0f),
					slot == typingSlot ? "Type it, then Enter." : "Spell it out below.", HorizontalAlignment.Left, box.Size.X - 40.0f, 26, MenuTheme.Soft);
			}

			for (int i = 0; i < Keys.Length; i++)
			{
				Rect2 r = KeyRect(seat, i);
				bool hot = slot != typingSlot && slot.Cursor != null && r.HasPoint(slot.Cursor.Position);
				pickerButtons.Add(new MenuTheme.ButtonSpec(r, Keys[i], 900 + seat * 31 + i, hot, Keys[i] == "OK",
					Keys[i].Length > 2 ? 17 : 26, slot.Tint));
			}
			MenuTheme.DrawButtons(canvas, pickerButtons);
			if (slot == typingSlot)
			{
				canvas.DrawString(font, new Vector2(box.Position.X + 16.0f, box.End.Y - 6.0f), "Typing: Enter keeps it, Tab for saved",
					HorizontalAlignment.Left, box.Size.X - 32.0f, 18, MenuTheme.Soft);
			}
		}
	}

	/// <summary>The CPU buttons live in the bottom corner of a seat's panel.</summary>
	Rect2 CpuButton(int player, int which)
	{
		Rect2 panel = PanelRect(player, GetViewportRect().Size);
		// Three across for a CPU: its difficulty, CHANGE and REMOVE.
		const float W = 116.0f;
		const float H = 48.0f;
		return new Rect2(panel.End.X - 20.0f - W - which * (W + 6.0f), panel.End.Y - 14.0f - H, W, H);
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
				seat.Index = NextUnlocked((int)(GD.Randi() % (uint)FighterCatalog.Count));
				return true;
			}

			if (!seat.IsCpu) continue;

			if (CpuButton(i, 2).HasPoint(point))
			{
				int[] difficulty = GameRoot.Instance.CpuDifficulty;
				difficulty[i] = (difficulty[i] + 1) % 3;
				return true;
			}

			if (CpuButton(i, 1).HasPoint(point))
			{
				seat.Index = NextUnlocked((seat.Index + 1) % FighterCatalog.Count);
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
	/// is who pressed it. True if it was one.
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
	void DrawButton(Rect2 box, string text, int seed, int fontSize = 28)
	{
		Color ink = MenuTheme.Rule;
		foreach (Slot slot in slots)
		{
			if (slot.Human && slot.Cursor != null && box.HasPoint(slot.Cursor.Position)) ink = slot.Tint;
		}

		DrawRect(box, new Color(1.0f, 1.0f, 1.0f, 0.85f));
		CrayonBrush.InkRect(this, box, ink, ink == MenuTheme.Rule ? 3.0f : 6.0f, seed, 2.2f);
		DrawString(ThemeDB.FallbackFont, new Vector2(box.Position.X, box.GetCenter().Y + fontSize * 0.36f), text,
			HorizontalAlignment.Center, box.Size.X, fontSize, MenuTheme.Text);
	}

	/// <summary>A team button: filled with the team's colour, its name on it, outlined by any cursor over it.</summary>
	void DrawTeamButton(Rect2 box, int team, int seed)
	{
		Color ink = MenuTheme.Ink;
		foreach (Slot slot in slots)
		{
			if (slot.Human && slot.Cursor != null && box.HasPoint(slot.Cursor.Position)) ink = slot.Tint;
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

		// Every controller is read once a frame, seated or not. One nobody is sitting at joins on A;
		// that press does nothing else.
		SyncDevices();
		foreach (Device device in devices) device.Cursor.Nav.Poll();
		var justSat = new System.Collections.Generic.HashSet<Slot>();
		foreach (Device device in devices)
		{
			if (ForStory && TickStory(device)) justSat.Add(slots[0]);
			else if (device.Seat == null && device.Cursor.Confirm && Join(device)) justSat.Add(device.Seat);
		}

		foreach (Slot slot in slots)
		{
			if (ForStory && slot != slots[0]) continue;
			if (!slot.Human || slot.Cursor == null || justSat.Contains(slot)) continue;
			MenuCursor cursor = slot.Cursor;
			if (slot.Naming)
			{
				TickNaming(slot, viewport);
				continue;
			}
			cursor.Move(viewport);
			if (quitPrompt.Open)
			{
				if (cursor.Cancel) quitPrompt.Cancel();
				else if (cursor.Confirm) quitPrompt.Press(this, cursor.Position);
				continue;
			}

			// With everyone picked, Start fights. Checked first, because Start also counts as A
			// and would otherwise re-pick whatever card the cursor happens to be resting on.
			if (readyBefore && slot.Human && cursor.Nav.Start)
			{
				startFight = true;
				continue;
			}

			if (cursor.Cancel)
			{
				// B takes a pick back; with nothing picked it gets up out of the seat, and the last
				// person to get up goes back to the main menu.
				SfxPlayer.Ui("ui_back");
				if (slot.Locked)
				{
					slot.Index = -1;
				}
				else
				{
					if (!ForStory) Stand(slot);
					if (ForStory || !System.Array.Exists(slots, s => s.Human))
					{
						GameRoot.Instance.GoMainMenu();
						return;
					}
					continue;
				}
			}

			if (!cursor.Confirm) continue;

			if (QuitBox.HasPoint(cursor.Position))
			{
				quitPrompt.Open = true;
				SfxPlayer.Ui("ui_select");
				continue;
			}

			if (ForStory && TryDifficulty(cursor.Position))
			{
				SfxPlayer.Ui("ui_select");
				continue;
			}

			if (!ForStory && ItemsBox.HasPoint(cursor.Position))
			{
				ItemSettings items = GameRoot.Instance.Items;
				items.Frequency = (ItemFrequency)(((int)items.Frequency + 1) % 4);
				items.Save();
				SfxPlayer.Ui("ui_select");
				continue;
			}

			if (!ForStory && NameButton(slot.Seat).HasPoint(cursor.Position))
			{
				slot.Naming = true;
				slot.Draft = slot.PlayerName ?? "";
				if (slot.Device != null && slot.Device.Key.StartsWith("keys:")) typingSlot = slot;
				SfxPlayer.Ui("ui_select");
				continue;
			}

			if (!ForStory && (TryTeamButtons(cursor.Position, slot) || TryCpuButtons(cursor.Position)))
			{
				SfxPlayer.Ui("ui_select");
				continue;
			}

			int picked = CardUnder(cursor.Position);
			if (picked >= 0 && (Unlocks.IsLocked(picked) || ForStory && !HasStory(picked)))
			{
				SfxPlayer.Ui("ui_back");
				continue;
			}
			if (picked >= 0)
			{
				// Picked again: his next skin he has earned, round to none (versus only).
				if (picked == slot.Index && !ForStory)
				{
					string next = Skins.Next(FighterCatalog.Get(picked), slot.Skin);
					if (next != slot.Skin)
					{
						slot.Skin = next;
						SfxPlayer.Ui("ui_select");
					}
					continue;
				}
				slot.Index = picked;
				slot.Skin = "";
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

		// Drawn again only when what it shows has changed: the page, the cards and a picker are
		// dozens of crayon strokes, and redrawn every frame they took a slow computer down to a few
		// frames a second as soon as a name picker opened (Eric, 2026-10-24). Godot keeps what was
		// last drawn. Twice a second it is drawn regardless, in case something here is missed.
		bool refresh = Engine.GetPhysicsFrames() % 30 == 0;
		string shown = MainDrawState();
		if (refresh || shown != lastMainState)
		{
			lastMainState = shown;
			QueueRedraw();
		}
		string picking = PickerDrawState();
		if (refresh || picking != lastPickerState)
		{
			lastPickerState = picking;
			namingLayer.QueueRedraw();
		}
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
			GameRoot.Instance.SeatDevices[0] = slots[0].Device?.Key;
			GameRoot.Instance.GoStory(StoryScript.IdOf(FighterCatalog.Get(slots[0].Index)));
			return;
		}

		for (int i = 0; i < slots.Length; i++)
		{
			GameRoot.Instance.SelectedFighters[i] = slots[i].Joined ? slots[i].Index : -1;
			GameRoot.Instance.SeatSkins[i] = slots[i].Joined ? slots[i].Skin : "";
			GameRoot.Instance.CpuPlayers[i] = slots[i].Joined && slots[i].IsCpu;
			GameRoot.Instance.SeatDevices[i] = slots[i].Human ? slots[i].Device?.Key : null;
			GameRoot.Instance.SeatNames[i] = slots[i].Human ? slots[i].PlayerName : null;
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

	/// <summary>
	/// Where a fighter's card sits in the row, by catalog index: in catalog order, but DoomBot always
	/// last, at the far right - the boss at the end of the row (Eric, 2026-10-23).
	/// </summary>
	static int Place(int index)
	{
		int last = StoryScript.IndexOf("doombot");
		if (last < 0) return index;
		if (index == last) return FighterCatalog.Count - 1;
		return index > last ? index - 1 : index;
	}

	/// <summary>A locked fighter's portrait: his shape, in ink, and nothing else of him.</summary>
	static readonly Color Silhouette = Colors.Black;

	const string LockedName = "???";

	/// <summary>The first fighter from this one on, round the roster, who is not locked.</summary>
	static int NextUnlocked(int from)
	{
		for (int step = 0; step < FighterCatalog.Count; step++)
		{
			int i = (from + step) % FighterCatalog.Count;
			if (!Unlocks.IsLocked(i)) return i;
		}
		return from;
	}

	/// <summary>Who a panel shows: his name and description, or, still locked, a question mark.</summary>
	void ShowFighter(Slot slot, int index)
	{
		if (index >= 0 && Unlocks.IsLocked(index))
		{
			slot.Name.SetText(LockedName, NameStyle.Plain(MenuTheme.Soft));
			slot.Credit.Text = "";
			slot.Blurb.Text = "Not yet! Beat this fighter in a story to unlock them.";
			return;
		}
		slot.Name.SetFighter(index >= 0 ? FighterCatalog.Get(index) : null);
		slot.Credit.Text = index >= 0 ? FighterCatalog.CreditOf(index) : "";
		slot.Blurb.Text = index >= 0 ? FighterCatalog.BlurbOf(index) : "";
	}

	void RefreshPanels()
	{
		foreach (Slot slot in slots)
		{
			if (slot.Heading != null)
			{
				slot.Heading.Text = slot.Joined && slot.IsCpu ? $"Player {slot.Seat + 1} - CPU"
					: slot.Human && !string.IsNullOrEmpty(slot.PlayerName) ? $"P{slot.Seat + 1}  {slot.PlayerName}"
					: $"Player {slot.Seat + 1}";
			}
			if (ForStory && slot != slots[0])
			{
				slot.State.Text = "";
				continue;
			}

			if (!slot.Joined)
			{
				slot.Name.SetFighter(null);
				slot.Credit.Text = "";
				slot.Blurb.Text = "";
				slot.State.Text = "Press A to join";
				continue;
			}

			if (slot.IsCpu)
			{
				ShowFighter(slot, slot.Index);
				// Said in the heading: down here it would sit on the CPU buttons.
				slot.State.Text = "";
				continue;
			}

			// Show what the cursor is over until something is picked, so a player can read a
			// fighter's description before committing to it.
			int shown = slot.Locked ? slot.Index : slot.Cursor != null ? CardUnder(slot.Cursor.Position) : -1;
			ShowFighter(slot, shown);
			slot.State.Text = slot.Locked ? "READY" : "";
			Skin skin = Skins.Find(slot.Skin);
			if (!ForStory && slot.Locked && skin != null) slot.State.Text = $"READY  -  {skin.Name}";
			else if (!ForStory && slot.Locked && Skins.EarnedFor(FighterCatalog.Get(slot.Index)).Length > 0) slot.State.Text = "READY  -  pick again for a new look";
			if (ForStory && !slot.Locked && shown >= 0 && !Unlocks.IsLocked(shown) && !HasStory(shown))
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
			if (ForStory && !Unlocks.IsLocked(i) && StoryProgress.Finished(StoryScript.IdOf(FighterCatalog.Get(i))))
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
				DrawButton(CpuButton(i, 2), GameRoot.DescribeDifficulty(GameRoot.Instance.CpuDifficulty[i]), 89 + i, 22);
				DrawButton(CpuButton(i, 1), "CHANGE", 91 + i, 22);
				DrawButton(CpuButton(i, 0), "REMOVE", 93 + i, 22);
			}
			else
			{
				DrawButton(CpuButton(i, 0), "CPU", 95 + i);
			}
		}

		if (!ForStory)
		{
			bool teamsHot = false;
			foreach (Slot slot in slots) teamsHot |= slot.Human && slot.Cursor != null && TeamsBox.HasPoint(slot.Cursor.Position);
			MenuTheme.DrawButton(this, TeamsBox, TeamBattle ? "Teams" : "Everyone for themselves", 79, teamsHot, TeamBattle,
				fontSize: TeamBattle ? 40 : 28);

			bool itemsHot = false;
			foreach (Slot slot in slots) itemsHot |= slot.Human && slot.Cursor != null && ItemsBox.HasPoint(slot.Cursor.Position);
			ItemFrequency frequency = GameRoot.Instance.Items.Frequency;
			MenuTheme.DrawButton(this, ItemsBox, "Items: " + ItemSettings.Describe(frequency), 81, itemsHot,
				frequency != ItemFrequency.Off, fontSize: 34);
		}
		else
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(RowLeft, DifficultyBox(0).GetCenter().Y + 12.0f), "Difficulty", HorizontalAlignment.Left, -1, 34, MenuTheme.Soft);
			for (int d = 0; d < Difficulties.Length; d++)
			{
				Slot seat = slots[0];
				bool hot = seat.Human && seat.Cursor != null && DifficultyBox(d).HasPoint(seat.Cursor.Position);
				MenuTheme.DrawButton(this, DifficultyBox(d), Difficulties[d], 83 + d, hot, GameOptions.StoryDifficulty == d, fontSize: 36);
			}
		}

		for (int i = 0; i < slots.Length && !ForStory; i++)
		{
			Slot seat = slots[i];
			if (!seat.Human || seat.Naming) continue;
			bool hot = seat.Cursor != null && NameButton(i).HasPoint(seat.Cursor.Position);
			MenuTheme.DrawButton(this, NameButton(i), string.IsNullOrEmpty(seat.PlayerName) ? "Name" : "Rename", 85 + i, hot,
				fontSize: 26, hotTint: seat.Tint);
		}

		bool quitHovered = false;
		Color quitInk = MenuTheme.Rule;
		foreach (Slot slot in slots)
		{
			if (!slot.Human || slot.Cursor == null || !QuitBox.HasPoint(slot.Cursor.Position)) continue;
			quitInk = slot.Tint;
			quitHovered = true;
		}

		DrawRect(QuitBox, new Color(1.0f, 1.0f, 1.0f, 0.55f));
		CrayonBrush.InkRect(this, QuitBox, quitInk, quitHovered ? 6.0f : 3.0f, 77, 2.6f);
	}
}
