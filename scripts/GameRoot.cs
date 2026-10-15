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

	public const int PlayerCount = 2;

	public int[] SelectedFighters = { 0, 1 };

	/// <summary>Which players the computer plays, chosen on the fighter select screen.</summary>
	public bool[] CpuPlayers = { false, false };
	public int SelectedStage;

	/// <summary>The item settings, saved between games (ItemSettings.Save).</summary>
	public readonly ItemSettings Items = new ItemSettings();

	/// <summary>Only this item drops in a match (--item=ID), for looking at one at a time.</summary>
	public string OnlyItem = "";

	/// <summary>The story being played, or null.</summary>
	public StoryRun Story;

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
				// "--fighters=2,0": player one is fighter 2, player two is fighter 0.
				string[] picks = arg.Substring(11).Split(',');
				for (int i = 0; i < picks.Length && i < SelectedFighters.Length; i++)
				{
					SelectedFighters[i] = picks[i].ToInt();
				}
			}
			if (arg == "--cpu") CpuPlayers[1] = true;
			if (arg == "--parade") startAt = "parade";
			if (arg.StartsWith("--only=")) paradeOnly = arg.Substring(7).ToInt();
			if (arg.StartsWith("--size=")) paradeSize = arg.Substring(7).ToInt();
			if (arg.StartsWith("--film=")) paradeFilm = arg.Substring(7);
			if (arg == "--match") startAt = "match";
			if (arg == "--select") startAt = "select";
			if (arg == "--storyselect") startAt = "storyselect";
			if (arg == "--stages") startAt = "stages";
			if (arg == "--itemsettings") startAt = "items";
			if (arg == "--menu") startAt = "menu";
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
		bool windowed = shotAfter > 0 || System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--windowed") >= 0;
		if (!windowed && !DisplayServer.GetName().Equals("headless", System.StringComparison.OrdinalIgnoreCase))
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
		}

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
			case "stages": GoStageSelect(); break;
			case "items": GoItemSettings(); break;
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

	// --- Story -----------------------------------------------------------------------

	/// <summary>Starts a fighter's story from its first chapter.</summary>
	public void GoStory(string fighterId)
	{
		Story = StoryRun.For(fighterId);
		GoStoryChapter();
	}

	/// <summary>The chapter the story is on: its stage, the player against whoever it is, its scene first.</summary>
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

		var fighters = new List<int> { player };
		var cpu = new List<bool> { false };
		foreach (string id in chapter.Opponents)
		{
			fighters.Add(StoryScript.IndexOf(id));
			cpu.Add(true);
		}
		Show(new MatchManager
		{
			StageIndex = chapter.Stage,
			FighterIndices = fighters.ToArray(),
			CpuPlayers = cpu.ToArray(),
			Story = Story,
		});
	}

	/// <summary>A chapter won: on to the next.</summary>
	public void StoryNext()
	{
		if (Story == null) { GoMainMenu(); return; }
		Story.At++;
		Story.Tries = 0;
		GoStoryChapter();
	}

	/// <summary>A fight lost: straight back in, the same chapter.</summary>
	public void StoryRetry()
	{
		if (Story == null) { GoMainMenu(); return; }
		Story.Tries++;
		GoStoryChapter();
	}

	public void GoMatch()
	{
		var match = new MatchManager
		{
			StageIndex = SelectedStage,
			FighterIndices = (int[])SelectedFighters.Clone(),
			CpuPlayers = (bool[])CpuPlayers.Clone(),
			Items = Items,
			OnlyItem = OnlyItem,
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
					DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
						? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
					break;
				case Key.Escape: GetTree().Quit(); break;
			}
		}
	}
}
