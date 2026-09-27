using System.Collections.Generic;
using Godot;

/// <summary>
/// Owns a match: builds the stage and the fighters, assigns controllers, enforces blast zones
/// and stocks, and routes hit events to the feel systems. This is the root node of the M1 scene.
///
/// Everything is constructed in code rather than in .tscn files because M1 uses no textures,
/// which means a fresh checkout runs without a Godot import pass.
/// </summary>
public partial class MatchManager : Node2D
{
	public readonly List<Fighter> Fighters = new List<Fighter>();
	public bool ShowHitboxes { get; private set; } = true;

	/// <summary>
	/// Which pad layout every gamepad uses. One knob for now; M5 makes it per-player on the
	/// controls screen.
	/// </summary>
	GamepadLayout padLayout = GamepadLayout.Smash();

	Stage stage;
	GameCamera camera;
	HitFx fx;
	MatchHud hud;
	DebugDraw debugDraw;

	string status = "";
	bool matchOver;
	float lastKnockback;

	// Screenshot capture. Animation and game feel cannot be verified by a build - they have to
	// be looked at - and this is what makes "looked at" possible without a human at the screen.
	int shotAfterFrames = -1;
	int framesElapsed;

	public override void _Ready()
	{
		RegressionChecks.RunAll();

		// Hot-plug: a pad plugged in or yanked out mid-match is reassigned immediately rather
		// than at the next restart, because the actual failure mode here is a kid picking up a
		// controller that went to sleep and concluding the game is broken.
		Input.Singleton.JoyConnectionChanged += OnJoyConnectionChanged;

		bool parade = false;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--shot=")) shotAfterFrames = Mathf.Max(2, arg.Substring(7).ToInt());
			if (arg == "--parade") parade = true;
		}

		if (parade)
		{
			// The parade replaces the match entirely - it is an inspection view, not a mode.
			AddChild(new RigParade());
			SetPhysicsProcess(false);
			return;
		}

		BuildMatch();
	}

	public override void _ExitTree()
	{
		Input.Singleton.JoyConnectionChanged -= OnJoyConnectionChanged;
	}

	void BuildMatch()
	{
		stage = new Stage();
		AddChild(stage);

		SpawnFighter(FighterData.PlaceholderLight(), 0);
		SpawnFighter(FighterData.PlaceholderHeavy(), 1);
		AssignControllers();

		fx = new HitFx();
		AddChild(fx);

		debugDraw = new DebugDraw { Match = this };
		AddChild(debugDraw);

		camera = new GameCamera();
		AddChild(camera);

		hud = new MatchHud();
		AddChild(hud);
		hud.Build(Fighters);
		hud.SetControllerLabels(DescribeControllers(), padLayout.Describe());
	}

	void SpawnFighter(FighterData data, int index)
	{
		var fighter = new Fighter();
		fighter.Configure(data, null, index, this);
		fighter.GlobalPosition = stage.SpawnPoints[index % stage.SpawnPoints.Length];
		AddChild(fighter);
		Fighters.Add(fighter);
	}

	// --- Controllers ---------------------------------------------------------

	/// <summary>
	/// Pads are handed out in connection order, then keyboard schemes fill whoever is left.
	/// Keyboard schemes are handed out in order too, so that the one person on a keyboard in a
	/// pad-plus-keyboard match gets the comfortable WASD half rather than the arrow keys.
	/// </summary>
	void AssignControllers()
	{
		Godot.Collections.Array<int> pads = Input.GetConnectedJoypads();
		int keyboardsUsed = 0;

		for (int i = 0; i < Fighters.Count; i++)
		{
			if (i < pads.Count)
			{
				Fighters[i].Controller = new GamepadInputSource(pads[i], padLayout);
				continue;
			}

			Fighters[i].Controller = keyboardsUsed++ == 0
				? KeyboardInputSource.Player1()
				: KeyboardInputSource.Player2();
		}
	}

	void OnJoyConnectionChanged(long device, bool connected)
	{
		string name = connected ? Input.GetJoyName((int)device) : "(disconnected)";
		GD.Print($"MatchManager: pad {device} {(connected ? "connected" : "disconnected")} {name}");

		AssignControllers();
		hud?.SetControllerLabels(DescribeControllers(), padLayout.Describe());
	}

	List<string> DescribeControllers()
	{
		var labels = new List<string>();
		foreach (Fighter fighter in Fighters)
		{
			labels.Add(fighter.Controller switch
			{
				GamepadInputSource pad => $"pad {pad.Device + 1}",
				KeyboardInputSource => "keyboard",
				_ => "none",
			});
		}
		return labels;
	}

	public bool AnyPadConnected => Input.GetConnectedJoypads().Count > 0;

	// --- Match flow ----------------------------------------------------------

	public override void _PhysicsProcess(double delta)
	{
		if (matchOver) return;

		foreach (Fighter fighter in Fighters)
		{
			if (fighter.State == FighterState.Eliminated) continue;
			if (fighter.State == FighterState.Respawning) continue;

			if (stage.IsOutOfBounds(fighter.GlobalPosition))
			{
				KillOffStage(fighter);
			}
		}

		CheckForWinner();
	}

	void KillOffStage(Fighter fighter)
	{
		fx.SpawnBlastFlash(fighter.GlobalPosition);
		camera.AddShake(90.0f);

		// A KO is the biggest moment in a match; it gets the biggest rumble.
		if (fighter.Controller is IHapticInputSource haptic) haptic.Rumble(1.0f, 0.45f);

		fighter.LoseStock(stage.RespawnPoint);
	}

	void CheckForWinner()
	{
		int alive = 0;
		Fighter last = null;

		foreach (Fighter fighter in Fighters)
		{
			if (fighter.Stocks > 0)
			{
				alive++;
				last = fighter;
			}
		}

		if (alive <= 1)
		{
			matchOver = true;
			status = last != null ? $"{last.Data.DisplayName} wins!   R to restart" : "Draw!   R to restart";
		}
	}

	public override void _Process(double delta)
	{
		framesElapsed++;
		if (camera == null)
		{
			// Parade mode: nothing to frame or refresh, but a screenshot may still be pending.
			if (shotAfterFrames > 0 && framesElapsed >= shotAfterFrames)
			{
				shotAfterFrames = -1;
				CallDeferred(nameof(CaptureAndQuit));
			}
			return;
		}

		camera.FrameFighters(Fighters, (float)delta);

		if (shotAfterFrames > 0 && framesElapsed >= shotAfterFrames)
		{
			shotAfterFrames = -1;
			CallDeferred(nameof(CaptureAndQuit));
		}

		if (!matchOver)
		{
			status = ShowHitboxes ? $"last KB {lastKnockback:0}" : "";
		}

		hud.Refresh(Fighters, status);
	}

	/// <summary>
	/// Called by the attacker the moment a hitbox resolves. Everything that makes a hit feel
	/// like it landed lives here rather than inside Fighter, so the feel systems can be tuned
	/// without touching the state machine.
	/// </summary>
	public void OnHitLanded(
		Fighter attacker, Fighter victim, Vector2 contactPoint,
		float knockback, float damage, bool blocked)
	{
		lastKnockback = knockback;
		fx.SpawnHitSpark(contactPoint, damage, blocked);
		camera.AddShake(blocked ? knockback * 0.25f : knockback);

		// Knockback around 150 is roughly kill range, so that is where rumble maxes out.
		float intensity = Mathf.Clamp(knockback / 150.0f, 0.15f, 1.0f);
		float duration = Mathf.Clamp(0.06f + damage * 0.012f, 0.06f, 0.26f);

		if (blocked)
		{
			intensity *= 0.4f;
			duration *= 0.6f;
		}

		if (victim.Controller is IHapticInputSource victimPad)
		{
			victimPad.Rumble(intensity, duration);
		}

		// The attacker gets a lighter tap - enough to confirm the hit connected without
		// competing with the victim's.
		if (attacker.Controller is IHapticInputSource attackerPad)
		{
			attackerPad.Rumble(intensity * 0.35f, duration * 0.7f);
		}
	}

	// --- Debug keys ----------------------------------------------------------

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventJoypadButton pad && pad.Pressed && !matchOver)
		{
			// Start on any pad restarts, so a match can be reset without reaching for the keyboard.
			if (pad.ButtonIndex == JoyButton.Start) Restart();
			return;
		}

		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

		switch (key.PhysicalKeycode)
		{
			case Key.F1:
				ShowHitboxes = !ShowHitboxes;
				debugDraw.QueueRedraw();
				break;

			case Key.F2:
				// Slow motion is the most useful M1 tuning tool there is - startup and active
				// frames are invisible at full speed and obvious at a quarter of it.
				Engine.TimeScale = Engine.TimeScale > 0.9f ? 0.25f : 1.0f;
				break;

			case Key.F3:
				// Flip every pad between the Smash layout and the platformer one, so the two
				// can be compared back to back rather than argued about.
				padLayout = padLayout.IsSmashLayout
					? GamepadLayout.Platformer()
					: GamepadLayout.Smash();
				AssignControllers();
				hud.SetControllerLabels(DescribeControllers(), padLayout.Describe());
				GD.Print($"MatchManager: pad layout is now {(padLayout.IsSmashLayout ? "Smash" : "Platformer")}");
				break;

			case Key.F12:
				Capture($"shot_{framesElapsed}");
				break;

			case Key.R:
				Restart();
				break;

			case Key.Escape:
				GetTree().Quit();
				break;
		}
	}

	void CaptureAndQuit()
	{
		Capture("latest");
		GetTree().Quit();
	}

	void Capture(string name)
	{
		string dir = ProjectSettings.GlobalizePath("res://.shots");
		DirAccess.MakeDirRecursiveAbsolute(dir);

		Image image = GetViewport().GetTexture().GetImage();
		string path = dir.PathJoin(name + ".png");
		image.SavePng(path);
		GD.Print($"MatchManager: wrote {path}");
	}

	void Restart()
	{
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		Fighters.Clear();
		matchOver = false;
		status = "";
		lastKnockback = 0.0f;
		Engine.TimeScale = 1.0f;

		BuildMatch();
	}
}
