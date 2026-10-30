using System.Collections.Generic;
using Godot;

/// <summary>What fighter select and the match are for (GameRoot.Mode).</summary>
public enum GameMode { Versus, Training, Boss, Targets, Doodle }

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

	/// <summary>Each seat's skin (Skins), by id - "" or null for none.</summary>
	public string[] SeatSkins = new string[MaxPlayers];

	AchievementToast toast;
	readonly System.Collections.Generic.List<string> grants = new System.Collections.Generic.List<string>();

	/// <summary>An achievement just earned, on screen for a moment over whatever is showing.</summary>
	public void ToastAchievement(Achievement achievement) => toast?.Add(achievement);

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

	/// <summary>
	/// What fighter select is picking for: a versus fight, or one of the modes behind More modes on
	/// the main menu (ModesScreen). Back to Versus on the main menu.
	/// </summary>
	public GameMode Mode = GameMode.Versus;

	/// <summary>A timed fight's clock in seconds for this run (--rule=time:20), or 0 for the options' minutes.</summary>
	public int ClockSeconds;

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

	/// <summary>The targets course picked (TargetCourses), for Targets mode.</summary>
	public int SelectedCourse;

	/// <summary>The item settings, saved between games (ItemSettings.Save).</summary>
	public readonly ItemSettings Items = new ItemSettings();

	/// <summary>Only this item drops in a match (--item=ID), for looking at one at a time.</summary>
	public string OnlyItem = "";

	/// <summary>The items each seat drew for doodle brawl, kept for the session - never saved or sent.</summary>
	public readonly DoodleSet Doodles = new DoodleSet();

	/// <summary>The story being played, or null.</summary>
	public StoryRun Story;
	string storyMapFor = "";

	Node currentScreen;

	/// <summary>The screen up now: a match, a menu.</summary>
	public Node CurrentScreen => currentScreen;

	public override void _Ready()
	{
		Instance = this;

		// Sound lives here, above the screens, so the menu music carries on unbroken from one
		// menu to the next and a sound that starts on one screen finishes on the next.
		AddChild(new SfxPlayer());
		AddChild(new MusicPlayer());
		toast = new AchievementToast();
		AddChild(toast);

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
			// "--course=2": the targets course a --mode=targets --match plays (0 Easy, 1 Medium, 2 Hard).
			if (arg.StartsWith("--course=")) SelectedCourse = Mathf.Clamp(arg.Substring(9).ToInt(), 0, TargetCourses.Count - 1);
			if (arg == "--courses") startAt = "courses";
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
			// DoomBot and Lugnut locked again for this run, nothing saved: to look at the
			// silhouettes, NEW CHALLENGER and the unlocking without touching the real save.
			if (arg == "--locked") Unlocks.ForceLocked = true;
			if (arg == "--achievements") startAt = "achievements";
			if (arg == "--photos") startAt = "photos";
			// "--grant=meteor": earn one, to look at its toast and the page (with --locked, unsaved).
			if (arg.StartsWith("--grant=")) grants.Add(arg.Substring(8));
			// "--skins=blueflame,,mk2": each seat's skin, in order, for a --match to look at.
			if (arg.StartsWith("--skins="))
			{
				string[] picked = arg.Substring(8).Split(',');
				for (int i = 0; i < SeatSkins.Length && i < picked.Length; i++) SeatSkins[i] = picked[i];
			}
			// "--challenger=lugnut" / "--unlocked=lugnut": either screen on its own, to look at.
			if (arg.StartsWith("--challenger=")) { storyMapFor = arg.Substring(13); startAt = "challenger"; }
			if (arg.StartsWith("--unlocked=")) { storyMapFor = arg.Substring(11); startAt = "unlocked"; }
			if (arg == "--storyselect") startAt = "storyselect";
			if (arg.StartsWith("--storydone="))
			{
				// "--storydone=flambe": the end of a story - STORY: COMPLETED - without playing it.
				storyMapFor = arg.Substring(12);
				startAt = "storydone";
			}
			if (arg.StartsWith("--storymap="))
			{
				// "--storymap=lugnut": a story's map, where its saved progress has got to.
				storyMapFor = arg.Substring(11);
				startAt = "storymap";
			}
			if (arg == "--stages") startAt = "stages";
			if (arg == "--itemsettings") startAt = "items";
			if (arg == "--menu") startAt = "menu";
			if (arg == "--modes") startAt = "modes";
			// "--online=host" or "--online=join:KQZMW": straight into a session, to test it - with
			// DRAWFIGHT_PLAY_URL pointing at a relay run locally (.ai/online.md).
			if (arg.StartsWith("--online=")) { onlineAs = arg.Substring(9); startAt = "online"; }
			// "--rule=time", "--rule=time:20" (a 20-second clock, to watch it run out), "--rule=stamina",
			// "--rule=stamina:100", "--rule=stock": versus rules for this run only - not saved.
			if (arg.StartsWith("--rule="))
			{
				string[] rule = arg.Substring(7).Split(':');
				GameOptions.Rule = rule[0] == "time" ? VersusRule.Time : rule[0] == "stamina" ? VersusRule.Stamina : VersusRule.Stock;
				if (rule.Length > 1 && GameOptions.Rule == VersusRule.Time) ClockSeconds = rule[1].ToInt();
				if (rule.Length > 1 && GameOptions.Rule == VersusRule.Stamina) GameOptions.StaminaHealth = rule[1].ToInt();
			}
			// "--mode=training": fighter select and the match are for that mode (training, boss,
			// targets, doodle) - with --match, straight into it.
			if (arg.StartsWith("--mode="))
			{
				Mode = arg.Substring(7) switch
				{
					"training" => GameMode.Training,
					"boss" => GameMode.Boss,
					"targets" => GameMode.Targets,
					"doodle" => GameMode.Doodle,
					_ => GameMode.Versus,
				};
			}
			// "--doodle": doodle brawl's drawing page ("--doodle=choice": with Throw it / Swing it up).
			// "--doodletest" paints a test item on every seat's page, since a screenshot run cannot
			// draw ("--doodletest=1" turns them round the seats: Player 1 gets the ball to throw);
			// "--doodlehold" hands one to every fighter as the fight starts. Both are tests.
			if (arg == "--doodle" || arg == "--doodle=choice") startAt = arg.Substring(2);
			if (arg == "--doodletest") Doodles.FillForTest();
			if (arg.StartsWith("--doodletest=")) Doodles.FillForTest(arg.Substring(13).ToInt());
			if (arg == "--doodlehold") Doodles.TestHold = true;
			if (arg == "--options") startAt = "options";
			if (arg == "--changelog") startAt = "changelog";
			if (arg == "--gallery") startAt = "gallery";
			if (arg == "--nephew") startAt = "nephew";
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
		foreach (string id in grants) Achievements.Grant(id);

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
			case "match":
				// Training straight in: --fighters=3,1 is you as fighter 3 against a dummy as fighter 1.
				if (Mode == GameMode.Training) for (int i = 1; i < CpuPlayers.Length; i++) CpuPlayers[i] = true;
				GoMatch();
				break;
			case "select": GoCharacterSelect(); break;
			case "storyselect": GoStorySelect(); break;
			case "storymap": GoStory(storyMapFor); break;
			case "challenger":
			case "unlocked":
				int shown = StoryScript.IndexOf(storyMapFor);
				Show(new ChallengerScreen { Fighter = shown >= 0 ? FighterCatalog.Get(shown) : null, Unlocked = startAt == "unlocked", Then = GoMainMenu });
				break;
			case "storydone":
				Story = StoryRun.For(storyMapFor);
				GoStoryComplete();
				break;
			case "stages": GoStageSelect(); break;
			case "items": GoItemSettings(); break;
			case "options": GoOptions(null); break;
			case "changelog": GoChangelog(null); break;
			case "gallery": GoGallery(null); break;
			case "nephew": GoNephew(null); break;
			case "controls": GoControls(null); break;
			// The frame the application icon is cropped from (tools/art/app_icon.py).
			case "icon": Show(new IconFrame()); break;
			// Every fighter's name in their own letters, at every size the game shows it.
			case "names": Show(new NameSheet()); break;
			case "suggest": GoSuggest(); break;
			case "newstage": OpenDrawing(Drawing.New(stage: true)); break;
			case "menu": GoMainMenu(); break;
			case "modes": GoModes(null); break;
			case "online":
				if (onlineAs == "host") NetSession.Host();
				else if (onlineAs.StartsWith("join:")) NetSession.Join(onlineAs.Substring(5));
				GoOnline();
				break;
			case "doodle": GoDoodle(); break;
			case "doodle=choice": Show(new DoodleScreen { OpenOnChoice = true }); break;
			case "courses":
				Mode = GameMode.Targets;
				GoTargetCourses();
				break;
			case "achievements": GoAchievements(); break;
			case "photos": GoSendPhotos(); break;
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

	/// <summary>Whether --flag was passed for this run.</summary>
	public static bool HasFlag(string flag) => System.Array.IndexOf(OS.GetCmdlineUserArgs(), flag) >= 0;

	void OnJoyConnectionChanged(long device, bool connected)
	{
		GD.Print($"GameRoot: pad {device} {(connected ? "connected" : "disconnected")}");
		// Online, a pad plugged in here reaches every copy through the session, on the same frame
		// everywhere (NetSession), and that is when the screens hear of it - not now, on this one.
		if (NetSession.Playing) return;
		ControllersChanged();
	}

	/// <summary>A controller came or went somewhere in an online session - on every copy at once.</summary>
	public void SessionDevicesChanged()
	{
		ControllersChanged();
		if (currentScreen is OnlineHomeScreen) GoOnlineHome();
	}

	void ControllersChanged()
	{
		// Menus build their input sources once, on entry, so the simplest correct response to
		// a pad appearing is to rebuild the screen that is up. A match reassigns in place.
		if (currentScreen is MatchManager match) match.ReassignControllers();
		else if (currentScreen is CharacterSelectScreen select)
		{
			if (select.ForStory && !select.ForTargets) GoStorySelect();
			else GoCharacterSelect();
		}
		else if (currentScreen is TitleScreen) GoTitle();
		else if (currentScreen is MainMenuScreen) GoMainMenu();
	}

	// --- Flow ----------------------------------------------------------------

	public void GoTitle() => Show(new TitleScreen());

	public void GoMainMenu()
	{
		Mode = GameMode.Versus;
		// Online, home is what to play together, and only leaving goes further back.
		if (NetSession.Playing)
		{
			GoOnlineHome();
			return;
		}
		Show(new MainMenuScreen());
	}

	/// <summary>Playing online (from the main menu): hosting, joining, and the lobby - and where a session ends up.</summary>
	public void GoOnline(string message = null) => Show(new OnlineScreen { Message = message });

	/// <summary>Online, in a session: what to play together (OnlineHomeScreen).</summary>
	public void GoOnlineHome()
	{
		Mode = GameMode.Versus;
		Story = null;
		// "--onlinetest=match": every copy straight into the fight this run's flags describe
		// (--fighters, --cpu, --stage, --rule), the CPUs playing it - to watch the copies stay in
		// step without anyone at the controls. Every copy must be given the same flags.
		if (HasFlag("--onlinetest=match") && !onlineTested)
		{
			onlineTested = true;
			ApplyMatchFlags();
			GoMatch();
			return;
		}
		Show(new OnlineHomeScreen());
	}

	string onlineAs = "";
	bool onlineTested;

	/// <summary>The fighters, CPUs, teams and stage this run's flags asked for, again - a session starts by clearing them.</summary>
	void ApplyMatchFlags()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--stage=")) SelectedStage = arg.Substring(8).ToInt();
			if (arg.StartsWith("--fighters="))
			{
				string[] picks = arg.Substring(11).Split(',');
				for (int i = 0; i < SelectedFighters.Length; i++) SelectedFighters[i] = i < picks.Length ? picks[i].ToInt() : -1;
			}
			if (arg == "--cpu") for (int i = 1; i < CpuPlayers.Length; i++) CpuPlayers[i] = true;
			if (arg == "--teams") TeamBattle = true;
		}
	}

	/// <summary>
	/// Every choice back to how the game starts - the seats, teams, stage, story - so that every copy
	/// of the game in an online session starts from the same place, and is left the same after.
	/// </summary>
	public void ResetChoices()
	{
		SelectedFighters = new[] { 0, 1, -1, -1 };
		CpuPlayers = new bool[MaxPlayers];
		SeatDevices = new string[MaxPlayers];
		SeatNames = new string[MaxPlayers];
		SeatSkins = new string[MaxPlayers];
		SeatTeams = new[] { 0, 1, 0, 1 };
		CpuDifficulty = new[] { 1, 1, 1, 1 };
		TeamBattle = false;
		VersusStarter = null;
		MenuReturnKey = null;
		SelectedStage = 0;
		Story = null;
		StoryDirectStart = false;
		Mode = GameMode.Versus;
		ClockSeconds = 0;
		OnlyItem = "";
		Doodles.Clear();
	}

	/// <summary>Training, the boss fight, targets and doodle brawl (from the main menu).</summary>
	public void GoModes(string key) => Show(new ModesScreen { OpenedBy = key });

	// --- More modes ------------------------------------------------------------------

	/// <summary>Training: one fighter, a dummy to hit, every hit shown.</summary>
	public void GoTraining()
	{
		Mode = GameMode.Training;
		GoCharacterSelect();
	}

	/// <summary>Everyone on one side, against a giant DoomBot.</summary>
	public void GoBossFight()
	{
		Mode = GameMode.Boss;
		GoCharacterSelect();
	}

	/// <summary>Break the targets: one fighter, ten targets, the clock running.</summary>
	public void GoTargets()
	{
		Mode = GameMode.Targets;
		GoCharacterSelect();
	}

	/// <summary>Targets: which course, for the fighter just picked.</summary>
	public void GoTargetCourses() => Show(new TargetCourseScreen());

	/// <summary>
	/// A targets run: player one's fighter on the course picked, on their controller - no CPU, no
	/// items. Every try is a fresh one, from the 3-2-1.
	/// </summary>
	public void GoTargetRun()
	{
		int fighter = SelectedFighters[0] >= 0 && SelectedFighters[0] < FighterCatalog.Count ? SelectedFighters[0] : 0;
		Show(new MatchManager
		{
			Course = TargetCourses.Get(SelectedCourse),
			FighterIndices = new[] { fighter },
			CpuPlayers = new[] { false },
			Seats = new[] { 0 },
		});
	}

	/// <summary>A versus fight whose items are drawn by the players first.</summary>
	public void GoDoodleBrawl()
	{
		Mode = GameMode.Doodle;
		GoCharacterSelect();
	}

	/// <summary>Doodle brawl, after stage select: each person playing draws an item, then the fight.</summary>
	public void GoDoodle() => Show(new DoodleScreen());
	public void GoDrawings() => Show(new DrawingsScreen());
	public void OpenDrawing(Drawing drawing) => Show(new DrawScreen { Doc = drawing });
	public void GoSendFighter(Drawing drawing) => Show(new SendFighterScreen { Doc = drawing });

	/// <summary>Photos of a drawing done on paper, and its sheet - nothing drawn in the game.</summary>
	public void GoSendPhotos() => Show(new SendFighterScreen { Doc = Drawing.New(), PhotosOnly = true });
	public void GoCharacterSelect() => Show(Mode == GameMode.Targets
		? new CharacterSelectScreen { ForStory = true, ForTargets = true }
		: new CharacterSelectScreen());
	public void GoStorySelect() => Show(new CharacterSelectScreen { ForStory = true });
	public void GoStageSelect() => Show(new StageSelectScreen());
	public void GoItemSettings() => Show(new ItemSettingsScreen());

	/// <summary>The options, opened by <paramref name="key"/>'s controller (the gear on the main menu).</summary>
	public void GoOptions(string key) => Show(new OptionsScreen { OpenedBy = key });

	/// <summary>An idea for the game, typed and sent in (from the main menu).</summary>
	public void GoSuggest() => Show(new SuggestScreen());

	/// <summary>Every achievement, done or not (from the main menu).</summary>
	public void GoAchievements() => Show(new AchievementsScreen());

	/// <summary>Every version's changes (from Options).</summary>
	public void GoChangelog(string key) => Show(new ChangelogScreen { OpenedBy = key });

	/// <summary>Every drawing sent in, and who drew it, ending on the picture for Eric's nephew (from Options).</summary>
	public void GoGallery(string key) => Show(new GalleryScreen { OpenedBy = key });

	/// <summary>"for my nephew..." - after DoomBot's story, the first time it is finished.</summary>
	public void GoNephew(System.Action then) => Show(new NephewScreen { Then = then });

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

		// Up against someone still locked, for the first time: NEW CHALLENGER, before his scene.
		FighterData challenger = Unlocks.LockedIn(chapter);
		if (challenger != null && !Story.StartAfter && !Unlocks.ChallengerSeen(challenger))
		{
			Unlocks.SeeChallenger(challenger);
			Show(new ChallengerScreen { Fighter = challenger, Then = GoStoryChapter });
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
		bool firstFinish = Story.Done && !StoryProgress.Finished(Story.PlayerId);
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
			StoryAchievements();
			// Finished: it starts again from the top next time, gold star and all - after its
			// moment, and then the main menu (Eric, 2026-10-22).
			Story.At = 0;
			// DoomBot's own story, finished for the first time: Eric's picture for his nephew, and
			// then the story's moment (Eric, 2026-10-24).
			if (firstFinish && Story.PlayerId == "doombot") GoNephew(GoStoryComplete);
			else GoStoryComplete();
			return;
		}
		GoStoryMap();
	}

	public void GoStoryComplete() => Show(new StoryCompleteScreen { Run = Story });

	/// <summary>A story finished: what it earned (Achievements).</summary>
	void StoryAchievements()
	{
		bool hard = GameOptions.StoryDifficulty == 2;
		Achievements.Grant("story_first");
		if (hard) Achievements.Grant("story_hard");
		if (hard && Story.PlayerId == "doombot") Achievements.Grant("story_doombot_hard");
		bool all = true;
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			string id = StoryScript.IdOf(FighterCatalog.Get(i));
			if (StoryRun.For(id).Chapters.Count > 0 && !StoryProgress.Finished(id)) all = false;
		}
		if (all) Achievements.Grant("story_all");
	}

	/// <summary>
	/// A locked fighter beaten: unlocked, his screen, and then the story goes on - his chapter's
	/// scene after the fight, played afresh, or the next chapter if it has none.
	/// </summary>
	public void ShowUnlocked(FighterData fighter)
	{
		Unlocks.Unlock(fighter);
		Story.StartAfter = Story.Chapter != null && Story.Chapter.After != "";
		Show(new ChallengerScreen
		{
			Fighter = fighter,
			Unlocked = true,
			Then = () => { if (Story.StartAfter) GoStoryChapter(); else StoryNext(); },
		});
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
		if (Mode == GameMode.Targets)
		{
			GoTargetRun();
			return;
		}
		bool training = Mode == GameMode.Training;
		if (training) TrainingMode.SeatDummy(SelectedFighters, CpuPlayers);
		// The seats somebody sat in, in seat order: P1 and P3 fighting are fighters 0 and 1, and
		// keep seats 0 and 2 - their numbers, colours and controllers.
		var fighters = new System.Collections.Generic.List<int>();
		var cpu = new System.Collections.Generic.List<bool>();
		var seats = new System.Collections.Generic.List<int>();
		var teams = new System.Collections.Generic.List<int>();
		var names = new System.Collections.Generic.List<string>();
		var levels = new System.Collections.Generic.List<int>();
		var skins = new System.Collections.Generic.List<string>();
		for (int seat = 0; seat < MaxPlayers; seat++)
		{
			if (SelectedFighters[seat] < 0) continue;
			// A skin that is a fighter of its own - DoomBot in Mk. II's armour - is him, swapped in.
			Skin skin = Skins.Find(SeatSkins[seat] ?? "");
			fighters.Add(skin != null && skin.Becomes >= 0 ? skin.Becomes : SelectedFighters[seat]);
			skins.Add(SeatSkins[seat] ?? "");
			cpu.Add(CpuPlayers[seat]);
			levels.Add(LevelFor(CpuDifficulty[seat]));
			seats.Add(seat);
			teams.Add(SeatTeams[seat]);
			names.Add(CpuPlayers[seat] ? null : SeatNames[seat]);
		}
		// A team fight needs two teams in it; everyone on one is everyone for themselves.
		bool teamBattle = TeamBattle && !training && new System.Collections.Generic.HashSet<int>(teams).Count >= 2;
		var match = new MatchManager
		{
			StageIndex = SelectedStage,
			FighterIndices = fighters.ToArray(),
			CpuPlayers = cpu.ToArray(),
			Seats = seats.ToArray(),
			Teams = teamBattle ? teams.ToArray() : null,
			TeamBattle = teamBattle,
			Training = training,
			// Training drops no items: nothing turns up that was not asked for.
			Items = training ? null : Items,
			OnlyItem = OnlyItem,
			Names = names.ToArray(),
			CpuLevels = levels.ToArray(),
			SeatSkins = skins.ToArray(),
			// The options' rules are for versus (and a doodle brawl, which is versus with drawn items).
			Rule = Mode is GameMode.Versus or GameMode.Doodle ? GameOptions.Rule : VersusRule.Stock,
		};
		if (Mode == GameMode.Doodle) Doodles.Arm(match);
		Show(match);
	}

	public void Show(Node screen)
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
		if (!(currentScreen is DrawScreen || currentScreen is DrawingsScreen || currentScreen is SendFighterScreen || currentScreen is SuggestScreen
			|| currentScreen is DoodleScreen)
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
			}
		}
	}
}
