using Godot;

/// <summary>
/// Owns the screen flow and the choices made along it: title, character select, stage select,
/// match, and back to the title.
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

	Node currentScreen;

	public override void _Ready()
	{
		Instance = this;

		// Sound lives here, above the screens, so the menu music carries on unbroken from one
		// menu to the next and a sound that starts on one screen finishes on the next.
		AddChild(new SfxPlayer());
		AddChild(new MusicPlayer());

		RegressionChecks.RunAll();

		// Pads are reassigned live everywhere, not just in a match: a controller that goes to
		// sleep on the title screen and wakes on character select has to just work.
		Input.Singleton.JoyConnectionChanged += OnJoyConnectionChanged;

		int shotAfter = -1;
		string startAt = "title";
		int paradeOnly = -1;
		int paradeSize = 0;

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--shot=")) shotAfter = Mathf.Max(2, arg.Substring(7).ToInt());
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
			if (arg == "--match") startAt = "match";
			if (arg == "--select") startAt = "select";
			if (arg == "--stages") startAt = "stages";
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
				});
				break;
			case "match": GoMatch(); break;
			case "select": GoCharacterSelect(); break;
			case "stages": GoStageSelect(); break;
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
		else if (currentScreen is CharacterSelectScreen) GoCharacterSelect();
		else if (currentScreen is TitleScreen) GoTitle();
	}

	// --- Flow ----------------------------------------------------------------

	public void GoTitle() => Show(new TitleScreen());
	public void GoCharacterSelect() => Show(new CharacterSelectScreen());
	public void GoStageSelect() => Show(new StageSelectScreen());

	public void GoMatch()
	{
		var match = new MatchManager
		{
			StageIndex = SelectedStage,
			FighterIndices = (int[])SelectedFighters.Clone(),
			CpuPlayers = (bool[])CpuPlayers.Clone(),
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

		// A match starts in silence and brings its music in on FIGHT! (see MatchManager); the
		// parade is a tool, not a screen. Every other screen is a menu.
		if (screen is MatchManager || screen is RigParade) MusicPlayer.Stop();
		else MusicPlayer.Play(MusicPlayer.Menu);
	}

	// --- Screenshots ---------------------------------------------------------

	public int ScreenshotRequestFrames = -1;
	int framesElapsed;

	public override void _Process(double delta)
	{
		framesElapsed++;
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
