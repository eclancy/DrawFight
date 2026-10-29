using System.Collections.Generic;
using Godot;

/// <summary>
/// Training (Eric's call, 2026-10-29; Smash's training mode is the model): you, a dummy to hit,
/// and every hit shown. A KO only respawns and nobody wins (MatchManager.Training); this is the
/// rest of it - the dummy's behaviour, the readouts, and the training settings on the pause menu.
///
/// It lives in the match as a child, so a reset (MatchManager.Restart, which rebuilds everything)
/// builds a fresh one. What the dummy does and the speed are kept here, statically, so they last
/// through a reset and back through fighter select for as long as the game is open.
///
/// The readouts are HUD labels like the percents (MatchHud.MakeLabel): no boxes, a thick paper
/// outline. The combo count is lettered like the 3-2-1, crayon-bright with an ink edge, and
/// lands with a punch on every hit.
/// </summary>
public partial class TrainingMode : CanvasLayer
{
	// --- Settings, for the session ----------------------------------------------------

	/// <summary>What the dummy does: Stand to start with, as a punching bag should.</summary>
	public static DummyBehaviour Dummy = DummyBehaviour.Stand;

	/// <summary>Quarter speed, the same as F2: startup and active frames are invisible at full speed.</summary>
	public static bool Slow;
	const float SlowScale = 0.25f;

	public MatchManager Match;

	// --- Combos -------------------------------------------------------------------------
	// A combo is hits in a row while the one taking them is still in hitstun (Fighter.IsLaunched):
	// once he is free to act, even for a frame, the next hit starts a new one. That is the
	// definition that teaches something - a "combo" he could have blocked is not one.

	sealed class Combo
	{
		public int Hits;
		public float Damage;
		public bool Live;
	}

	readonly Dictionary<Fighter, Combo> combos = new Dictionary<Fighter, Combo>();

	/// <summary>The combo on screen: the latest of two hits or more, held a while after it ends.</summary>
	Combo shown;
	int shownEndedFrames;
	int punchFrames;

	/// <summary>How long a finished combo stays up to be read, the last part of it fading.</summary>
	const int ComboHoldFrames = 180;
	const int ComboFadeFrames = 40;
	const int PunchFrames = 14;

	bool anyHit;
	float lastDamage;
	float lastKnockback;
	bool lastBlocked;

	int frames;

	readonly Dictionary<Fighter, DummyInputSource> dummies = new Dictionary<Fighter, DummyInputSource>();

	Label comboLabel;
	Label comboDamageLabel;
	Label lastHitLabel;
	Label knockbackLabel;
	Label statusLabel;

	/// <summary>The combo count's colour, from the countdown's crayons: orange while it is going.</summary>
	static readonly Color ComboLive = new Color(0.98f, 0.58f, 0.22f);
	static readonly Color ComboOver = new Color(0.62f, 0.64f, 0.70f);

	// --- The dummy ------------------------------------------------------------------------

	/// <summary>
	/// The controller for a CPU seat: the dummy, which hands over to <paramref name="cpu"/> when it
	/// is told to fight back. One per fighter, kept, so a pad plugged in mid-session does not
	/// reset where it is in its walk.
	/// </summary>
	public IInputSource DummyFor(Fighter fighter, CpuInputSource cpu)
	{
		if (!dummies.TryGetValue(fighter, out DummyInputSource dummy))
		{
			dummy = new DummyInputSource(fighter, cpu, () => Dummy);
			dummies[fighter] = dummy;
		}
		return dummy;
	}

	/// <summary>
	/// Training always has someone to hit. With nobody on the computer, a dummy sits in the first
	/// empty seat as the next fighter along from the first person's - in the seat, so going back
	/// to fighter select shows it there to be changed.
	/// </summary>
	public static void SeatDummy(int[] fighters, bool[] cpu)
	{
		int player = -1, empty = -1;
		for (int seat = 0; seat < fighters.Length; seat++)
		{
			if (fighters[seat] < 0)
			{
				if (empty < 0) empty = seat;
				continue;
			}
			if (cpu[seat]) return;
			if (player < 0) player = fighters[seat];
		}
		if (empty < 0) return;
		fighters[empty] = NextUnlocked(player);
		cpu[empty] = true;
	}

	static int NextUnlocked(int after)
	{
		int from = Mathf.Max(0, after);
		for (int step = 1; step <= FighterCatalog.Count; step++)
		{
			int index = (from + step) % FighterCatalog.Count;
			if (!Unlocks.IsLocked(index)) return index;
		}
		return from % FighterCatalog.Count;
	}

	// --- The pause menu -------------------------------------------------------------------

	/// <summary>Training's pause menu, under Resume. <paramref name="leave"/> unpauses and goes.</summary>
	public PauseMenu.Option[] PauseOptions(System.Action<System.Action> leave) => new[]
	{
		new PauseMenu.Option(() => $"Dummy: {Dummy}", NextDummy, Stays: true),
		// Deferred: it rebuilds the match, pause menu and all, which is not done from inside the menu.
		new PauseMenu.Option(() => "Reset", () => Callable.From(Match.Restart).CallDeferred()),
		new PauseMenu.Option(() => Match.ShowHitboxes ? "Hitboxes: on" : "Hitboxes: off", Match.ToggleHitboxes, Stays: true),
		new PauseMenu.Option(() => IsSlow ? "Speed: slow" : "Speed: normal", ToggleSlow, Stays: true),
		new PauseMenu.Option(() => "Pick fighters", () => leave(GameRoot.Instance.GoCharacterSelect)),
		new PauseMenu.Option(() => "Main menu", () => leave(GameRoot.Instance.GoMainMenu)),
	};

	static void NextDummy() => Dummy = (DummyBehaviour)(((int)Dummy + 1) % (int)(DummyBehaviour.Fight + 1));

	/// <summary>Read off the engine rather than the setting, so F2 and the menu agree.</summary>
	static bool IsSlow => Engine.TimeScale < 0.9f;

	static void ToggleSlow()
	{
		Slow = !IsSlow;
		Engine.TimeScale = Slow ? SlowScale : 1.0f;
	}

	// --- Flags ------------------------------------------------------------------------------

	static bool flagsRead;

	/// <summary>--trainingmenu: the pause menu opened at this frame and screenshot, to look at.</summary>
	static int menuShotAt = -1;
	int menuShotFrames = -1;

	/// <summary>"--dummy=fight" starts the dummy doing that; "--trainingmenu" shoots the pause menu.</summary>
	static void ReadFlags()
	{
		if (flagsRead) return;
		flagsRead = true;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--dummy=") && System.Enum.TryParse(arg.Substring(8), true, out DummyBehaviour picked)) Dummy = picked;
			if (arg == "--trainingmenu") menuShotAt = 90;
		}
	}

	// --- The match ------------------------------------------------------------------------

	public override void _Ready()
	{
		ReadFlags();
		// A reset puts the engine back to full speed; slow is kept until it is switched off.
		if (Slow) Engine.TimeScale = SlowScale;

		var root = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		AddChild(root);

		// Top left, the combo: lettered like the countdown - crayon colour, ink edge, paper halo.
		// Sized so "12 hit combo!", punched up, still clears the line in the middle.
		comboLabel = MatchHud.MakeLabel(root, 72, ComboLive, 11);
		comboLabel.HorizontalAlignment = HorizontalAlignment.Left;
		comboLabel.AddThemeColorOverride("font_outline_color", MatchHud.Ink);
		comboLabel.AddThemeColorOverride("font_shadow_color", MatchHud.Paper);
		comboLabel.AddThemeConstantOverride("shadow_outline_size", 24);
		comboLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		comboLabel.AddThemeConstantOverride("shadow_offset_y", 0);
		comboLabel.Position = new Vector2(44.0f, 16.0f);
		comboLabel.Size = new Vector2(600.0f, 96.0f);
		comboLabel.PivotOffset = new Vector2(0.0f, 48.0f);

		comboDamageLabel = MatchHud.MakeLabel(root, 40, MatchHud.Ink, 12);
		comboDamageLabel.HorizontalAlignment = HorizontalAlignment.Left;
		comboDamageLabel.Position = new Vector2(48.0f, 106.0f);
		comboDamageLabel.Size = new Vector2(900.0f, 52.0f);

		// Top right, the last hit.
		lastHitLabel = MatchHud.MakeLabel(root, 46, MatchHud.Ink, 14);
		lastHitLabel.HorizontalAlignment = HorizontalAlignment.Right;
		lastHitLabel.Position = new Vector2(1920.0f - 44.0f - 900.0f, 22.0f);
		lastHitLabel.Size = new Vector2(900.0f, 60.0f);

		knockbackLabel = MatchHud.MakeLabel(root, 38, MatchHud.Ink, 12);
		knockbackLabel.HorizontalAlignment = HorizontalAlignment.Right;
		knockbackLabel.Position = new Vector2(1920.0f - 44.0f - 900.0f, 84.0f);
		knockbackLabel.Size = new Vector2(900.0f, 50.0f);

		// Top middle, small: that this is training, what the dummy is doing, and where to change it.
		statusLabel = MatchHud.MakeLabel(root, 28, MenuTheme.Soft, 10);
		statusLabel.Position = new Vector2(0.0f, 24.0f);
		statusLabel.Size = new Vector2(1920.0f, 40.0f);

		Refresh();
	}

	public override void _ExitTree()
	{
		if (menuShotFrames >= 0) GetTree().ProcessFrame -= CountToMenuShot;
	}

	public override void _PhysicsProcess(double delta)
	{
		// The stage's music, straight away: there is no FIGHT! to bring it in on. Asking for the
		// track already playing does nothing, so a reset carries on with it.
		if (++frames == 1) MusicPlayer.Play(Match.Stage.Data.Music);

		foreach (KeyValuePair<Fighter, Combo> pair in combos)
		{
			if (pair.Value.Live && !pair.Key.IsLaunched) pair.Value.Live = false;
		}
		if (shown != null && !shown.Live && ++shownEndedFrames > ComboHoldFrames) shown = null;
		if (punchFrames > 0) punchFrames--;

		if (frames == menuShotAt) OpenMenuForShot();
	}

	/// <summary>Every hit that lands, from MatchManager.OnHitLanded - blocked or not.</summary>
	public void OnHit(Fighter victim, float knockback, float damage, bool blocked)
	{
		anyHit = true;
		lastDamage = damage;
		lastKnockback = knockback;
		lastBlocked = blocked;
		if (blocked) return;

		if (!combos.TryGetValue(victim, out Combo combo) || !combo.Live)
		{
			combo = new Combo();
			combos[victim] = combo;
		}
		combo.Hits++;
		combo.Damage += damage;
		combo.Live = true;
		// One hit is a hit, not a combo; the count comes up from the second.
		if (combo.Hits < 2) return;
		shown = combo;
		shownEndedFrames = 0;
		punchFrames = PunchFrames;
	}

	public override void _Process(double delta) => Refresh();

	void Refresh()
	{
		comboLabel.Visible = comboDamageLabel.Visible = shown != null;
		if (shown != null)
		{
			comboLabel.Text = $"{shown.Hits} hit combo!";
			comboDamageLabel.Text = $"{Mathf.RoundToInt(shown.Damage)}% damage";
			comboLabel.AddThemeColorOverride("font_color", shown.Live ? ComboLive : ComboOver);
			float punch = punchFrames / (float)PunchFrames;
			comboLabel.Scale = Vector2.One * (1.0f + 0.25f * punch * punch);
			// Each hit slaps the number down at a slightly different tilt, like the 3-2-1.
			comboLabel.Rotation = (shown.Hits % 2 == 0 ? -0.04f : 0.04f) * (0.4f + 0.6f * punch);
			int left = ComboHoldFrames - shownEndedFrames;
			float fade = shown.Live ? 1.0f : Mathf.Clamp(left / (float)ComboFadeFrames, 0.0f, 1.0f);
			comboLabel.Modulate = comboDamageLabel.Modulate = new Color(1.0f, 1.0f, 1.0f, fade);
		}

		lastHitLabel.Visible = knockbackLabel.Visible = anyHit;
		lastHitLabel.Text = lastBlocked ? $"Last hit: {lastDamage:0.#}% (blocked)" : $"Last hit: {lastDamage:0.#}%";
		knockbackLabel.Text = $"Knockback: {lastKnockback:0}";

		string speed = IsSlow ? "  -  slow" : "";
		statusLabel.Text = $"Training  -  dummy: {Dummy.ToString().ToLower()}{speed}  -  Start for options";
	}

	// --- Looking at the pause menu -----------------------------------------------------------
	// The pause stops everything, --shots' frame count included, so the menu is shot from here, on
	// the tree's own frame signal, which still comes while paused.

	void OpenMenuForShot()
	{
		Match.OnStartPressed();
		menuShotFrames = 0;
		GetTree().ProcessFrame += CountToMenuShot;
	}

	void CountToMenuShot()
	{
		if (++menuShotFrames < 20) return;
		GetTree().ProcessFrame -= CountToMenuShot;
		menuShotFrames = -1;
		GameRoot.Instance?.Capture("training_menu");
		GetTree().Quit();
	}
}
