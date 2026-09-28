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
	/// <summary>Set by GameRoot before this node enters the tree.</summary>
	public int StageIndex;
	public int[] FighterIndices = { 0, 1 };

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
		BuildMatch();
	}

	void BuildMatch()
	{
		stage = new Stage();
		AddChild(stage);
		stage.Build(StageCatalog.Get(StageIndex));

		for (int i = 0; i < FighterIndices.Length; i++)
		{
			SpawnFighter(FighterCatalog.Get(FighterIndices[i]), i);
		}
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
		hud.SetControllerLabels(DescribeControllers(), ControllerAssignment.Layout.Describe());
		hud.SetStageName(StageCatalog.NameOf(StageIndex));
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

	/// <summary>Rebuilds every player's controller. Called on hot-plug by GameRoot.</summary>
	public void ReassignControllers()
	{
		IInputSource[] sources = ControllerAssignment.ForPlayers(Fighters.Count);
		for (int i = 0; i < Fighters.Count; i++) Fighters[i].Controller = sources[i];
		hud?.SetControllerLabels(DescribeControllers(), ControllerAssignment.Layout.Describe());
	}

	void AssignControllers() => ReassignControllers();

	List<string> DescribeControllers()
	{
		var labels = new List<string>();
		foreach (Fighter fighter in Fighters) labels.Add(ControllerAssignment.Describe(fighter.Controller));
		return labels;
	}

	public bool AnyPadConnected => Input.GetConnectedJoypads().Count > 0;

	// --- Match flow ----------------------------------------------------------

	/// <summary>The blast zone, for anything that needs to know when it has left the stage.</summary>
	public Rect2 StageBounds => stage.BlastZone;

	public System.Collections.Generic.List<Vector2> Ledges => stage.Ledges;

	/// <summary>Spawns a fireball, a thrown hammer, a falling anvil or a patch of fire.</summary>
	public void SpawnHazard(Fighter owner, MoveData move, Vector2 position, Vector2 velocity)
	{
		var hazard = new Hazard();
		AddChild(hazard);
		hazard.Launch(owner, move, this, position, velocity, move.SpecialGravity, move.SpecialLifetime);
	}

	/// <summary>
	/// A fighter blew up - Circy's bomb. The flash and the shake happen whether or not anyone
	/// is caught in it, so a bomb that goes off in empty air still reads as a bomb.
	/// </summary>
	public void OnExplosion(Vector2 position)
	{
		fx.SpawnBlastFlash(position);
		camera.AddShake(60.0f);
	}

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
			status = last != null
				? $"{last.Data.DisplayName} wins!   Start for the title screen, R to rematch"
				: "Draw!   Start for the title screen, R to rematch";
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
		if (@event is InputEventJoypadButton pad && pad.Pressed)
		{
			// Start restarts a live match, and returns to the title once it is over.
			if (pad.ButtonIndex != JoyButton.Start) return;
			if (matchOver) GameRoot.Instance.GoTitle();
			else Restart();
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
				// Slow motion is the most useful tuning tool there is - startup and active
				// frames are invisible at full speed and obvious at a quarter of it.
				Engine.TimeScale = Engine.TimeScale > 0.9f ? 0.25f : 1.0f;
				break;

			case Key.F3:
				ControllerAssignment.Layout = ControllerAssignment.Layout.IsStandardLayout
					? GamepadLayout.Smash()
					: GamepadLayout.Standard();
				ReassignControllers();
				GD.Print($"MatchManager: pad layout is now " +
					$"{(ControllerAssignment.Layout.IsStandardLayout ? "Standard" : "Smash")}");
				break;

			case Key.F4:
				StageIndex = (StageIndex + 1) % StageCatalog.Count;
				GD.Print($"MatchManager: stage is now {StageCatalog.NameOf(StageIndex)}");
				Restart();
				break;

			case Key.Enter:
				if (matchOver) GameRoot.Instance.GoTitle();
				break;

			case Key.R:
				Restart();
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
