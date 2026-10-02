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
	public bool ShowHitboxes { get; private set; } = false;

	/// <summary>
	/// Which pad layout every gamepad uses. One knob for now; M5 makes it per-player on the
	/// controls screen.
	/// </summary>
	/// <summary>Set by GameRoot before this node enters the tree.</summary>
	public int StageIndex;
	public int[] FighterIndices = { 0, 1 };

	/// <summary>Which players the computer plays. Set by GameRoot, like the fighters.</summary>
	public bool[] CpuPlayers = { false, false };
	CpuInputSource[] cpus;

	Stage stage;
	GameCamera camera;
	HitFx fx;
	MatchHud hud;
	DebugDraw debugDraw;

	string status = "";
	string winner = "";
	bool matchOver;

	// --- Countdown -----------------------------------------------------------
	// 3, 2, 1, FIGHT! Nobody can act until FIGHT! appears, so a match never starts with one
	// player already moving while the other is still finding their controller.

	const int CountdownStepFrames = 50;
	const int FightBannerFrames = 45;
	int countdownFrames;

	/// <summary>True while the numbers are up. Fighters ignore their controllers until it ends.</summary>
	public bool InputLocked => countdownFrames > FightBannerFrames;
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
		if (stage.Traffic != null) stage.Traffic.Match = this;

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
		fx.Camera = camera;

		hud = new MatchHud();
		AddChild(hud);
		hud.Build(Fighters);

		countdownFrames = CountdownStepFrames * 3 + FightBannerFrames;
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
		// A CPU keeps its slot through hot-plugging: plugging in a pad should not replace the
		// computer player, and the humans still get pads in connection order.
		cpus ??= new CpuInputSource[Fighters.Count];
		int humans = 0;
		for (int i = 0; i < Fighters.Count; i++) if (!IsCpu(i)) humans++;
		IInputSource[] sources = ControllerAssignment.ForPlayers(Mathf.Max(1, humans));

		int next = 0;
		for (int i = 0; i < Fighters.Count; i++)
		{
			if (IsCpu(i))
			{
				if (cpus[i] == null)
				{
					cpus[i] = new CpuInputSource(this, i);
					cpus[i].Drive(Fighters[i]);
				}
				Fighters[i].Controller = cpus[i];
			}
			else
			{
				Fighters[i].Controller = sources[next++];
			}
		}
	}

	void AssignControllers() => ReassignControllers();

	bool IsCpu(int player) => player < CpuPlayers.Length && CpuPlayers[player];

	public bool AnyPadConnected => Input.GetConnectedJoypads().Count > 0;

	// --- Match flow ----------------------------------------------------------

	/// <summary>The blast zone, for anything that needs to know when it has left the stage.</summary>
	public Rect2 StageBounds => stage.BlastZone;

	public System.Collections.Generic.List<Vector2> Ledges => stage.Ledges;

	/// <summary>Spawns a fireball, a thrown hammer, a falling anvil or a patch of fire.</summary>
	/// <param name="damageScale">For a hazard that hits harder or softer than its move - a charged
	/// smash's shockwave. Knockback follows damage.</param>
	public Hazard SpawnHazard(Fighter owner, MoveData move, Vector2 position, Vector2 velocity,
		float damageScale = 1.0f)
	{
		var hazard = new Hazard();
		AddChild(hazard);
		hazard.Launch(owner, move, this, position, velocity, move.SpecialGravity, move.SpecialLifetime);
		hazard.DamageScale = damageScale;
		return hazard;
	}

	/// <summary>
	/// A fighter blew up - Circy's bomb. The flash and the shake happen whether or not anyone
	/// is caught in it, so a bomb that goes off in empty air still reads as a bomb.
	/// </summary>
	/// <summary>A platform built by a fighter - Lug's girder.</summary>
	public BuiltPlatform SpawnPlatform(Fighter owner, MoveData move, Rect2 rect)
	{
		var platform = new BuiltPlatform();
		AddChild(platform);
		platform.Build(owner, move, this, rect);
		return platform;
	}

	/// <summary>Shakes the camera - a slam into the ground, say. Bigger numbers shake harder.</summary>
	public void Shake(float amount) => camera?.AddShake(amount);

	public void OnExplosion(Vector2 position)
	{
		fx.SpawnBlastFlash(position);
		camera.AddShake(60.0f);
		SfxPlayer.At("explosion", position, 0.03f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (countdownFrames > 0)
		{
			CountdownSound();
			countdownFrames--;
		}
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
		// The blast goes off at the edge of what is on screen, nearest where they went out, and
		// fires back in across the stage - so it is seen even though the fighter is long gone.
		Vector2 at = fighter.GlobalPosition;
		Rect2 view = camera.VisibleRect().Grow(-camera.VisibleRect().Size.X * 0.04f);
		Vector2 edge = new Vector2(Mathf.Clamp(at.X, view.Position.X, view.End.X), Mathf.Clamp(at.Y, view.Position.Y, view.End.Y));
		Vector2 inward = view.GetCenter() - edge;
		if (inward.LengthSquared() < 1.0f) inward = Vector2.Up;
		fx.SpawnKoBlast(edge, inward, fighter.Data.PlaceholderColor);
		camera.AddShake(140.0f);
		SfxPlayer.At("ko_blast", edge, 0.0f);

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
			MusicPlayer.Stop();
			SfxPlayer.Ui("game_set");
			winner = last != null ? $"{last.Data.DisplayName} wins!" : "Draw!";
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

		status = ShowHitboxes ? $"last KB {lastKnockback:0}" : "";
		hud.Refresh(Fighters, status);
		UpdateBanner();
	}

	void UpdateBanner()
	{
		if (matchOver)
		{
			hud.SetCountdown("", Colors.White, 0.0f, 0.0f);
			hud.SetBanner(winner, "Press Start to pick fighters");
			return;
		}

		hud.SetBanner("");

		if (countdownFrames <= 0)
		{
			hud.SetCountdown("", Colors.White, 0.0f, 0.0f);
			return;
		}

		if (countdownFrames <= FightBannerFrames)
		{
			float left = countdownFrames / (float)FightBannerFrames;
			// Holds solid, then fades over its last third as the players take over.
			hud.SetCountdown("FIGHT!", CountdownColours[0], left, 0.0f, Mathf.Min(1.0f, left * 3.0f));
			return;
		}

		// Which number is showing, and how far through its time on screen.
		int intoNumbers = countdownFrames - FightBannerFrames;
		int number = Mathf.CeilToInt(intoNumbers / (float)CountdownStepFrames);
		float punch = (intoNumbers - (number - 1) * CountdownStepFrames) / (float)CountdownStepFrames;
		hud.SetCountdown(number.ToString(), CountdownColours[number], punch, CountdownTilts[number]);
	}

	/// <summary>
	/// A tick as each number lands and a chord on FIGHT! - which is also where the battle music
	/// starts, so the fight and its music begin on the same frame. The ticks are the A the
	/// FIGHT! chord resolves, so the count sounds like it is going somewhere.
	/// </summary>
	void CountdownSound()
	{
		int intoNumbers = countdownFrames - FightBannerFrames;
		if (intoNumbers > 0 && intoNumbers % CountdownStepFrames == 0)
		{
			SfxPlayer.Ui("count_tick");
		}
		else if (intoNumbers == 0)
		{
			SfxPlayer.Ui("count_go");
			MusicPlayer.Play(MusicPlayer.Battle);
		}
	}

	/// <summary>
	/// FIGHT!, 1, 2, 3 - a traffic light run backwards: red, orange, yellow, then green for go.
	/// Crayon-bright, like every accent in the game (see .ai/art-direction.md).
	/// </summary>
	static readonly Color[] CountdownColours =
	{
		new Color(0.38f, 0.80f, 0.42f),
		new Color(0.99f, 0.83f, 0.24f),
		new Color(0.98f, 0.58f, 0.22f),
		new Color(0.94f, 0.32f, 0.32f),
	};

	/// <summary>Each number lands at a slight, different tilt, as if slapped down by hand.</summary>
	static readonly float[] CountdownTilts = { 0.0f, -0.06f, 0.07f, -0.08f };

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
		SfxPlayer.Hit(contactPoint, knockback, blocked);
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
		if (attacker?.Controller is IHapticInputSource attackerPad)
		{
			attackerPad.Rumble(intensity * 0.35f, duration * 0.7f);
		}
	}

	// --- Debug keys ----------------------------------------------------------

	public override void _UnhandledInput(InputEvent @event)
	{
		// Start is read from each fighter's own controller (see OnStartPressed), not from here.
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

			case Key.R:
				Restart();
				break;
		}
	}

	// --- Pause ---------------------------------------------------------------

	PauseMenu pauseMenu;

	/// <summary>
	/// A player pressed Start. Mid-match that pauses; once the match is over it goes back to
	/// fighter select - the next thing anyone wants after a match is to pick again.
	/// </summary>
	public void OnStartPressed()
	{
		if (matchOver)
		{
			GameRoot.Instance.GoCharacterSelect();
			return;
		}
		if (pauseMenu != null) return;

		var humans = new List<IInputSource>();
		for (int i = 0; i < Fighters.Count; i++)
		{
			if (!IsCpu(i) && Fighters[i].Controller != null) humans.Add(Fighters[i].Controller);
		}

		pauseMenu = new PauseMenu();
		pauseMenu.Open(humans, Resume, () =>
		{
			GetTree().Paused = false;
			MusicPlayer.SetPaused(false);
			GameRoot.Instance.GoCharacterSelect();
		});
		AddChild(pauseMenu);
		GetTree().Paused = true;
		SfxPlayer.Ui("pause");
		MusicPlayer.SetPaused(true);
	}

	void Resume()
	{
		GetTree().Paused = false;
		MusicPlayer.SetPaused(false);
		pauseMenu?.QueueFree();
		pauseMenu = null;
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

		// The CPUs drove the fighters that were just freed; new fighters need new CPUs.
		cpus = null;
		pauseMenu = null;
		GetTree().Paused = false;
		matchOver = false;
		status = "";
		winner = "";
		lastKnockback = 0.0f;
		Engine.TimeScale = 1.0f;
		// The battle music comes back in on FIGHT!, like the first time.
		MusicPlayer.Stop();
		MusicPlayer.SetPaused(false);

		BuildMatch();
	}
}
