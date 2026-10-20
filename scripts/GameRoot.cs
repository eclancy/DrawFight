using System.Collections.Generic;
using Godot;

/// <summary>
/// Owns the screen flow and the choices made along it: title, main menu, then either character
/// select, stage select and a match, or the drawings - and back to the title.
///
/// Screens are swapped as children of this node rather than through Godot scene changes. The
/// whole game is built in code, so there is nothing for a .tscn per screen to hold, and keeping
/// one root means the selections live in an ordinary field instead of in a global that every
/// screen has to remember to write.
/// </summary>
public partial class GameRoot : Node2D
{
	public static GameRoot Instance { get; private set; }

	/// <summary>Seats on the fighter select screen: up to four players in a fight (Eric, 2026-10-16).</summary>
	public const int MaxPlayers = 4;

	/// <summary>The fighter in each seat, or -1 for a seat nobody sat in.</summary>
	public int[] SelectedFighters = { 0, 1, -1, -1 };

	/// <summary>Which seats the computer plays, chosen on the fighter select screen.</summary>
	public bool[] CpuPlayers = { false, false, false, false };

	/// <summary>
	/// The controller in each seat (ControllerAssignment.Devices), or null: whoever pressed a
	/// button to sit there on fighter select. P1 is the first to press, not the first pad.
	/// </summary>
	public string[] SeatDevices = new string[MaxPlayers];

	/// <summary>
	/// The controller that picked Versus on the main menu: fighter select sits it down as Player 1
	/// straight away, so the A that chose Versus is the A that joined (Eric, 2026-10-17). Used once.
	/// </summary>
	public string VersusStarter;

	/// <summary>The controller that went to the options, still pointing when it comes back to the main menu.</summary>
	public string MenuReturnKey;

	/// <summary>The name each seat gave itself on fighter select, or null (PlayerNames).</summary>
	public string[] SeatNames = new string[MaxPlayers];

	/// <summary>
	/// The controller a seat plays on: whoever sat in it - or, booted straight into a match with
	/// nobody having sat anywhere (--match), the pads in order, then the keyboard.
	/// </summary>
	public IInputSource SourceForSeat(int seat)
	{
		if (seat >= 0 && seat < SeatDevices.Length && SeatDevices[seat] != null) return ControllerAssignment.Open(SeatDevices[seat]);
		IInputSource[] order = ControllerAssignment.ForPlayers(MaxPlayers);
		return order[Mathf.Clamp(seat, 0, order.Length - 1)];
	}

	/// <summary>A team fight rather than everyone for themselves, chosen on the fighter select screen.</summary>
	public bool TeamBattle;

	/// <summary>Each seat's team in a team fight: 0 red, 1 blue, 2 green. Two against two to start with.</summary>
	public int[] SeatTeams = { 0, 1, 0, 1 };

	/// <summary>Each CPU seat's difficulty on fighter select: 0 Easy, 1 Medium, 2 Hard.</summary>
	public int[] CpuDifficulty = { 1, 1, 1, 1 };

	/// <summary>The CPU level each difficulty plays at (CpuInputSource.Level).</summary>
	public static int LevelFor(int difficulty) => difficulty switch { 0 => 1, 2 => 8, _ => 4 };

	public static string DescribeDifficulty(int difficulty) => difficulty switch { 0 => "EASY", 2 => "HARD", _ => "MEDIUM" };

	/// <summary>
	/// A cursor for every person playing, in their seat's colour on their seat's controller - the
	/// screens between fighter select and the fight (stage select, item settings). A CPU has none.
	/// </summary>
	public MenuCursor[] HumanCursors(Vector2 viewport, float y)
	{
		var cursors = new System.Collections.Generic.List<MenuCursor>();
		for (int seat = 0; seat < MaxPlayers; seat++)
		{
			if (SelectedFighters[seat] < 0 || CpuPlayers[seat]) continue;
			cursors.Add(new MenuCursor(SourceForSeat(seat), seat, MenuTheme.PlayerTint(seat),
				new Vector2(viewport.X * (0.2f + seat * 0.2f), y)));
		}
		if (cursors.Count == 0) cursors.Add(new MenuCursor(SourceForSeat(0), 0, MenuTheme.PlayerTint(0), new Vector2(viewport.X * 0.5f, y)));
		return cursors.ToArray();
	}
	public int SelectedStage;

	/// <summary>The item settings, saved between games (ItemSettings.Save).</summary>
	public readonly ItemSettings Items = new ItemSettings();

	/// <summary>Only this item drops in a match (--item=ID), for looking at one at a time.</summary>
	public string OnlyItem = "";

	/// <summary>The story being played, or null.</summary>
	public StoryRun Story;
	string storyMapFor = "";

	Node currentScreen;

	public override void _Ready()
	{
		Instance = this;

		// Sound lives here, above the screens, so the menu music carries on unbroken from one
		// menu to the next and a sound that starts on one screen finishes on the next.
		AddChild(new SfxPlayer());
		AddChild(new MusicPlayer());

		RegressionChecks.RunAll();
		Items.Load();
		GameOptions.Load();
		PlayerNames.Load();

		// Pads are reassigned live everywhere, not just in a match: a controller that goes to
		// sleep on the title screen and wakes on character select has to just work.
		Input.Singleton.JoyConnectionChanged += OnJoyConnectionChanged;

		int shotAfter = -1;
		string startAt = "title";
		int paradeOnly = -1;
		int paradeSize = 0;
		string paradeFilm = "";

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--shot=")) shotAfter = Mathf.Max(2, arg.Substring(7).ToInt());
			if (arg.StartsWith("--shots="))
			{
				// "--shots=120,400,900": a screenshot at each, then quit - a story scene is checked
				// by looking at it all the way through, not at one frame.
				foreach (string at in arg.Substring(8).Split(',')) ShotFrames.Add(at.ToInt());
				ShotFrames.Sort();
				shotAfter = ShotFrames[^1] + 2;
			}
			if (arg.StartsWith("--stage=")) SelectedStage = arg.Substring(8).ToInt();
			if (arg.StartsWith("--fighters="))
			{
				// "--fighters=2,0": player one is fighter 2, player two is fighter 0. Up to four.
				string[] picks = arg.Substring(11).Split(',');
				for (int i = 0; i < SelectedFighters.Length; i++)
				{
					SelectedFighters[i] = i < picks.Length ? picks[i].ToInt() : -1;
				}
			}
			// Every seat after the first is the computer.
			if (arg == "--cpu") for (int i = 1; i < CpuPlayers.Length; i++) CpuPlayers[i] = true;
			// A team fight, seats on red, blue, red, blue.
			if (arg == "--teams") TeamBattle = true;
			if (arg == "--parade") startAt = "parade";
			if (arg.StartsWith("--only=")) paradeOnly = arg.Substring(7).ToInt();
			if (arg.StartsWith("--size=")) paradeSize = arg.Substring(7).ToInt();
			if (arg.StartsWith("--film=")) paradeFilm = arg.Substring(7);
			if (arg == "--match") startAt = "match";
			if (arg == "--select") startAt = "select";
			if (arg == "--storyselect") startAt = "storyselect";
			if (arg.StartsWith("--storymap="))
			{
				// "--storymap=lugnut": a story's map, where its saved progress has got to.
				storyMapFor = arg.Substring(11);
				startAt = "storymap";
			}
			if (arg == "--stages") startAt = "stages";
			if (arg == "--itemsettings") startAt = "items";
			if (arg == "--menu") startAt = "menu";
			if (arg == "--options") startAt = "options";
			if (arg == "--changelog") startAt = "changelog";
			if (arg == "--controls") startAt = "controls";
			if (arg == "--icon") startAt = "icon";
			if (arg == "--names") startAt = "names";
			if (arg == "--suggest") startAt = "suggest";
			if (arg == "--newstage") startAt = "newstage";
			if (arg == "--drawings") startAt = "drawings";
			if (arg == "--draw") startAt = "draw";
			if (arg == "--send") startAt = "send";
			if (arg.StartsWith("--story="))
			{
				// "--story=edgelord" plays EdgeLord's story; "--story=edgelord:2" from its third
				// chapter; "--story=edgelord:2:after" from the scene after that chapter's fight.
				string[] parts = arg.Substring(8).Split(':');
				Story = StoryRun.For(parts[0]);
				if (parts.Length > 1) Story.At = parts[1].ToInt();
				Story.StartAfter = parts.Length > 2 && parts[2] == "after";
				StoryDirectStart = true;
				startAt = "story";
			}
			if (arg.StartsWith("--item=")) OnlyItem = arg.Substring(7);
			if (arg.StartsWith("--items="))
			{
				// "--items=high": how often, for this run only - not saved.
				string how = arg.Substring(8).ToLower();
				Items.Frequency = how == "off" ? ItemFrequency.Off : how == "low" ? ItemFrequency.Low
					: how == "high" ? ItemFrequency.High : ItemFrequency.Medium;
			}
		}

		ScreenshotRequestFrames = shotAfter;

		// The game plays fullscreen (Eric's call, 2026-10-04) - set here rather than in
		// project.godot so a screenshot run (--shot) or "--windowed" stays a 1920x1080 window
		// instead of taking over the screen. F11 switches while playing.
		// Fullscreen or a window is an option now (OptionsScreen); the run's own flags still win.
		bool windowed = shotAfter > 0 || System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--windowed") >= 0;
		GameOptions.Apply(!windowed);

		switch (startAt)
		{
			case "parade":
				Show(new RigParade
				{
					Attacks = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--attacks") >= 0,
					Only = paradeOnly,
					Size = paradeSize,
					Film = paradeFilm,
				});
				break;
			case "match": GoMatch(); break;
			case "select": GoCharacterSelect(); break;
			case "storyselect": GoStorySelect(); break;
			case "storymap": GoStory(storyMapFor); break;
			case "stages": GoStageSelect(); break;
			case "items": GoItemSettings(); break;
			case "options": GoOptions(null); break;
			case "changelog": GoChangelog(null); break;
			case "controls": GoControls(null); break;
			// The frame the application icon is cropped from (tools/art/app_icon.py).
			case "icon": Show(new IconFrame()); break;
			// Every fighter's name in their own letters, at every size the game shows it.
			case "names": Show(new NameSheet()); break;
			case "suggest": GoSuggest(); break;
			case "newstage": OpenDrawing(Drawing.New(stage: true)); break;
			case "menu": GoMainMenu(); break;
			case "story": GoStoryChapter(); break;
			case "drawings": GoDrawings(); break;
			// The newest saved drawing, or a new one: for looking at the drawing screens.
			case "draw":
			case "send":
				List<SavedDrawing> saved = Drawing.ListSaved();
				Drawing drawing = saved.Count > 0 ? Drawing.Load(saved[0].Id) : null;
				drawing ??= Drawing.New();
				if (startAt == "draw") OpenDrawing(drawing);
				else GoSendFighter(drawing);
				break;
			// A downloaded copy checks ecec.dev for a newer version before anything else.
			default:
				if (UpdateScreen.ShouldCheck()) Show(new UpdateScreen());
				else GoTitle();
				break;
		}
	}

	public override void _ExitTree()
	{
		Input.Singleton.JoyConnectionChanged -= OnJoyConnectionChanged;
	}

	void OnJoyConnectionChanged(long device, bool connected)
	{
		GD.Print($"GameRoot: pad {device} {(connected ? "connected" : "disconnected")}");

		// Menus build their input sources once, on entry, so the simplest correct response to
		// a pad appearing is to rebuild the screen that is up. A match reassigns in place.
		if (currentScreen is MatchManager match) match.ReassignControllers();
		else if (currentScreen is CharacterSelectScreen select)
		{
			if (select.ForStory) GoStorySelect();
			else GoCharacterSelect();
		}
		else if (currentScreen is TitleScreen) GoTitle();
		else if (currentScreen is MainMenuScreen) GoMainMenu();
	}

	// --- Flow ----------------------------------------------------------------

	public void GoTitle() => Show(new TitleScreen());
	public void GoMainMenu() => Show(new MainMenuScreen());
	public void GoDrawings() => Show(new DrawingsScreen());
	public void OpenDrawing(Drawing drawing) => Show(new DrawScreen { Doc = drawing });
	public void GoSendFighter(Drawing drawing) => Show(new SendFighterScreen { Doc = drawing });
	public void GoCharacterSelect() => Show(new CharacterSelectScreen());
	public void GoStorySelect() => Show(new CharacterSelectScreen { ForStory = true });
	public void GoStageSelect() => Show(new StageSelectScreen());
	public void GoItemSettings() => Show(new ItemSettingsScreen());

	/// <summary>The options, opened by <paramref name="key"/>'s controller (the gear on the main menu).</summary>
	public void GoOptions(string key) => Show(new OptionsScreen { OpenedBy = key });

	/// <summary>An idea for the game, typed and sent in (from the main menu).</summary>
	public void GoSuggest() => Show(new SuggestScreen());

	/// <summary>Every version's changes (from Options).</summary>
	public void GoChangelog(string key) => Show(new ChangelogScreen { OpenedBy = key });

	/// <summary>Every key, click and button, from the options.</summary>
	public void GoControls(string key) => Show(new ControlsScreen { OpenedBy = key });

	// --- Story -----------------------------------------------------------------------

	/// <summary>A fighter's story, at the chapter it had got to: its map first.</summary>
	public void GoStory(string fighterId)
	{
		Story = StoryRun.For(fighterId);
		Story.At = Mathf.Clamp(StoryProgress.ChapterReached(fighterId), 0, Mathf.Max(0, Story.Chapters.Count - 1));
		GoStoryMap();
	}

	/// <summary>The story's chapters on the fridge door (StoryMapScreen).</summary>
	public void GoStoryMap() => Show(new StoryMapScreen { Run = Story });

	/// <summary>
	/// The chapter the story is on: its stage, the player and everyone else in it, its scene
	/// first. In a fight, everyone else is one side against the player - a two-on-one is two CPUs
	/// on a team - at the chapter's level, and the boss, if there is one, gets a last stand.
	/// </summary>
	public void GoStoryChapter()
	{
		StoryChapter chapter = Story?.Chapter;
		int player = Story != null ? StoryScript.IndexOf(Story.PlayerId) : -1;
		if (chapter == null || player < 0)
		{
			if (Story != null) GD.Print($"GameRoot: {Story.PlayerId}'s story is over (or has no chapters yet)");
			Story = null;
			GoMainMenu();
			return;
		}

		// The story's difficulty (on its map): Easy is for someone small, the computer barely
		// fighting back and you with a life to spare; Hard plays two levels up. A two-on-one is
		// two lives short of three each, a level down, so it is a fight and not a wall (Elim,
		// 2026-10-19: Flambe and Lugnut together were too hard). The boss's own lives are his
		// scene's ("stocks").
		int difficulty = GameOptions.StoryDifficulty;
		bool twoOnOne = chapter.Opponents.Length > 1;
		int level = chapter.Level - (twoOnOne ? 1 : 0) + difficulty switch { 0 => -3, 2 => 2, _ => 0 };
		int theirLives = twoOnOne ? difficulty switch { 0 => 1, 2 => 3, _ => 2 } : Tuning.DefaultStocks;
		var fighters = new List<int> { player };
		var cpu = new List<bool> { false };
		var teams = new List<int> { 0 };
		var levels = new List<int> { 0 };
		var lives = new List<int> { difficulty == 0 ? Tuning.DefaultStocks + 1 : Tuning.DefaultStocks };
		int boss = -1;
		foreach (string id in chapter.Opponents)
		{
			if (id == chapter.Boss) boss = fighters.Count;
			fighters.Add(StoryScript.IndexOf(id));
			cpu.Add(true);
			teams.Add(1);
			levels.Add(Mathf.Clamp(level, 1, 9));
			lives.Add(theirLives);
		}
		Show(new MatchManager
		{
			StageIndex = chapter.Stage,
			FighterIndices = fighters.ToArray(),
			CpuPlayers = cpu.ToArray(),
			Teams = teams.ToArray(),
			CpuLevels = levels.ToArray(),
			BossPlayer = boss,
			Story = Story,
			StartStocks = lives.ToArray(),
			Items = StoryItems(),
		});
	}

	/// <summary>
	/// A story fight drops items now and then - always Low, whatever versus is set to, so they
	/// season a chapter without deciding it (Eric, 2026-10-19) - from the items switched on in
	/// the item settings.
	/// </summary>
	ItemSettings StoryItems()
	{
		var items = new ItemSettings { Frequency = ItemFrequency.Low };
		for (int i = 0; i < items.Enabled.Length; i++) items.Enabled[i] = Items.Enabled[i];
		return items;
	}

	/// <summary>
	/// A chapter done: remembered, and back to the map with the next one glowing - or, the
	/// story over, with its gold star. Started straight into one chapter (--story), it plays on.
	/// </summary>
	public void StoryNext()
	{
		if (Story == null) { GoMainMenu(); return; }
		Story.At++;
		Story.Tries = 0;
		if (Story.At > StoryProgress.ChapterReached(Story.PlayerId) || Story.Done)
		{
			StoryProgress.Save(Story.PlayerId, Story.At, Story.Chapters.Count);
		}
		if (StoryDirectStart && !Story.Done)
		{
			GoStoryChapter();
			return;
		}
		if (Story.Done)
		{
			// Finished: it starts again from the top next time, gold star and all.
			Story.At = 0;
		}
		GoStoryMap();
	}

	/// <summary>Booted straight into a story chapter (--story) to look at it: chapter after chapter, no map.</summary>
	public bool StoryDirectStart;

	/// <summary>A fight lost: straight back in, the same chapter.</summary>
	public void StoryRetry()
	{
		if (Story == null) { GoMainMenu(); return; }
		Story.Tries++;
		GoStoryChapter();
	}

	public void GoMatch()
	{
		// The seats somebody sat in, in seat order: P1 and P3 fighting are fighters 0 and 1, and
		// keep seats 0 and 2 - their numbers, colours and controllers.
		var fighters = new System.Collections.Generic.List<int>();
		var cpu = new System.Collections.Generic.List<bool>();
		var seats = new System.Collections.Generic.List<int>();
		var teams = new System.Collections.Generic.List<int>();
		var names = new System.Collections.Generic.List<string>();
		var levels = new System.Collections.Generic.List<int>();
		for (int seat = 0; seat < MaxPlayers; seat++)
		{
			if (SelectedFighters[seat] < 0) continue;
			fighters.Add(SelectedFighters[seat]);
			cpu.Add(CpuPlayers[seat]);
			levels.Add(LevelFor(CpuDifficulty[seat]));
			seats.Add(seat);
			teams.Add(SeatTeams[seat]);
			names.Add(CpuPlayers[seat] ? null : SeatNames[seat]);
		}
		// A team fight needs two teams in it; everyone on one is everyone for themselves.
		bool teamBattle = TeamBattle && new System.Collections.Generic.HashSet<int>(teams).Count >= 2;
		var match = new MatchManager
		{
			StageIndex = SelectedStage,
			FighterIndices = fighters.ToArray(),
			CpuPlayers = cpu.ToArray(),
			Seats = seats.ToArray(),
			Teams = teamBattle ? teams.ToArray() : null,
			TeamBattle = teamBattle,
			Items = Items,
			OnlyItem = OnlyItem,
			Names = names.ToArray(),
			CpuLevels = levels.ToArray(),
		};
		Show(match);
	}

	void Show(Node screen)
	{
		if (currentScreen != null)
		{
			RemoveChild(currentScreen);
			currentScreen.QueueFree();
		}

		currentScreen = screen;
		// The mouse is for the drawing screens only (Eric, 2026-10-16): everywhere else its pointer
		// is hidden, and those screens show it again themselves (OnePointer).
		Input.MouseMode = Input.MouseModeEnum.Hidden;
		AddChild(screen);

		// The click that changed screens is spent: without this it carries on into the new
		// screen too - and on the drawing screen, a click is a dot of ink.
		GetViewport().SetInputAsHandled();

		// A match starts in silence and brings its music in on FIGHT! (see MatchManager); the
		// parade is a tool, not a screen. Every other screen is a menu.
		if (screen is MatchManager || screen is RigParade) MusicPlayer.Stop();
		else MusicPlayer.Play(MusicPlayer.Menu);
	}

	// --- Screenshots ---------------------------------------------------------

	public int ScreenshotRequestFrames = -1;
	int framesElapsed;

	/// <summary>Frames to screenshot along the way (--shots), each written as shot_FRAME.</summary>
	readonly List<int> ShotFrames = new List<int>();

	public override void _Process(double delta)
	{
		framesElapsed++;
		// No mouse pointer anywhere but the drawing screens, whatever turned it on - a window
		// regaining focus, a screen that forgot to put it away (Eric, 2026-10-16).
		if (!(currentScreen is DrawScreen || currentScreen is DrawingsScreen || currentScreen is SendFighterScreen || currentScreen is SuggestScreen)
			&& Input.MouseMode != Input.MouseModeEnum.Hidden)
		{
			Input.MouseMode = Input.MouseModeEnum.Hidden;
		}
		if (ShotFrames.Contains(framesElapsed)) CallDeferred(nameof(Capture), $"shot_{framesElapsed:00000}");
		if (ScreenshotRequestFrames > 0 && framesElapsed >= ScreenshotRequestFrames)
		{
			ScreenshotRequestFrames = -1;
			CallDeferred(nameof(CaptureAndQuit));
		}
	}

	void CaptureAndQuit()
	{
		Capture("latest");
		GetTree().Quit();
	}

	public void Capture(string name)
	{
		string dir = ProjectSettings.GlobalizePath("res://.shots");
		DirAccess.MakeDirRecursiveAbsolute(dir);

		Image image = GetViewport().GetTexture().GetImage();
		string path = dir.PathJoin(name + ".png");
		image.SavePng(path);
		GD.Print($"GameRoot: wrote {path}");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			switch (key.PhysicalKeycode)
			{
				case Key.F12: Capture($"shot_{framesElapsed}"); break;
				// The game starts fullscreen (project.godot); F11 drops to a window and back.
				case Key.F11:
					GameOptions.Fullscreen = DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Fullscreen;
					GameOptions.Save();
					GameOptions.Apply(true);
					break;
				case Key.Escape: GetTree().Quit(); break;
			}
		}
	}
}
