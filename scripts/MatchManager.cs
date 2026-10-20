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

	/// <summary>The select-screen seat each fighter sat in (Fighter.Seat); null when they are seats 0, 1, 2...</summary>
	public int[] Seats;

	/// <summary>
	/// A versus team fight (Teams set from the fighter select's red, blue and green): the colours,
	/// tags and the winning banner are the team's. Story mode's two-on-one has Teams too, but not this.
	/// </summary>
	public bool TeamBattle;

	/// <summary>The name each fighter's player gave themselves, by fighter, or null (versus only).</summary>
	public string[] Names;

	/// <summary>
	/// A tag over every head (Fighter.DrawPlayerTag): in versus, once there are three or more in
	/// the fight or two of the same fighter. Two different fighters are told apart at a glance.
	/// </summary>
	public bool ShowPlayerTags { get; private set; }
	CpuInputSource[] cpus;

	/// <summary>
	/// A story chapter, or null for a versus match. A story match opens on its scene instead of
	/// the 3-2-1, and the fight starts in place when the scene says so (StoryDirector).
	/// </summary>
	public StoryRun Story;
	StoryDirector director;

	/// <summary>A story scene is playing, not the fight: everyone is the script's.</summary>
	public bool InStoryScene => director != null && director.InScene;

	/// <summary>
	/// Which side each player is on, or null for everyone on their own - a two-on-one in a story,
	/// two CPUs on one side (Eric's call, 2026-10-15). Teammates cannot hurt or grab each other,
	/// a CPU only goes after the other side, and a side is out when everyone on it is.
	/// </summary>
	public int[] Teams;

	/// <summary>How well each CPU plays, 1 to 9 (CpuInputSource.Level); null for the usual 4.</summary>
	public int[] CpuLevels;

	/// <summary>Which player is a story's boss, whose last stock is a last stand; -1 for none.</summary>
	public int BossPlayer = -1;

	/// <summary>The stage, for a story scene to strip the drawings off (Stage.Bare).</summary>
	public Stage Stage => stage;

	public int TeamOf(Fighter f) => Teams != null && f.PlayerIndex < Teams.Length ? Teams[f.PlayerIndex] : f.PlayerIndex;

	/// <summary>Two different fighters on the same side.</summary>
	public bool Allies(Fighter a, Fighter b) => a != null && b != null && a != b && TeamOf(a) == TeamOf(b);

	Stage stage;
	GameCamera camera;
	HitFx fx;
	MatchHud hud;
	DebugDraw debugDraw;

	string status = "";
	string winner = "";
	bool matchOver;

	/// <summary>Who won, for the camera to go in on once it is won; null for a draw or a match on.</summary>
	Fighter victor;

	// --- Countdown -----------------------------------------------------------
	// 3, 2, 1, FIGHT! Nobody can act until FIGHT! appears, so a match never starts with one
	// player already moving while the other is still finding their controller.

	const int CountdownStepFrames = 50;
	const int FightBannerFrames = 45;
	int countdownFrames;

	/// <summary>True while the numbers are up. Fighters ignore their controllers until it ends.</summary>
	public bool InputLocked => countdownFrames > FightBannerFrames;
	float lastKnockback;

	// --- Items -------------------------------------------------------------------------
	// Every so often an item drops in onto a platform somewhere, as in Smash: how often, and
	// which, are the item settings (GameRoot.Items).

	/// <summary>The item settings in force. Null - no items - when there is no GameRoot (the parade).</summary>
	public ItemSettings Items;

	/// <summary>Each fighter's lives in a story fight, by seat; a versus fight uses GameOptions.Stocks.</summary>
	public int[] StartStocks;

	/// <summary>Only this item drops (--item=ID), for looking at one item at a time.</summary>
	public string OnlyItem = "";

	int itemTimer = -1;
	readonly System.Random itemRng = new System.Random();

	/// <summary>
	/// A random item that may appear; never a pencil case out of a pencil case. A rare one
	/// (ItemData.SpawnWeight) comes up less often than the rest.
	/// </summary>
	public ItemData RandomItem(int salt, bool fromCase)
	{
		bool only = !string.IsNullOrEmpty(OnlyItem) && !fromCase;
		var choices = new List<ItemData>();
		float total = 0.0f;
		for (int i = 0; i < ItemCatalog.Count; i++)
		{
			ItemData data = ItemCatalog.Get(i);
			if (fromCase && data.Use == ItemUse.Open) continue;
			if (only && data.Id != OnlyItem) continue;
			if (!only && Items != null && !Items.Enabled[i]) continue;
			choices.Add(data);
			total += data.SpawnWeight;
		}
		if (choices.Count == 0) return null;
		float pick = (float)itemRng.NextDouble() * total;
		foreach (ItemData data in choices)
		{
			pick -= data.SpawnWeight;
			if (pick <= 0.0f) return data;
		}
		return choices[^1];
	}

	public Item SpawnItem(ItemData data, Vector2 at, Vector2 velocity)
	{
		var item = new Item();
		AddChild(item);
		item.Launch(data, this, at, velocity);
		return item;
	}

	/// <summary>A stripe of wet paint left by a paint roller.</summary>
	public void SpawnStripe(Fighter owner, ItemData data, Vector2 at)
	{
		var item = new Item();
		AddChild(item);
		item.StartStripe(data, this, owner, at);
	}

	void TickItems()
	{
		bool only = !string.IsNullOrEmpty(OnlyItem);
		if (Items == null && !only) return;
		if (!only && (Items.Frequency == ItemFrequency.Off || Items.EnabledCount == 0)) return;
		int interval = only ? 150 : Items.SpawnInterval;
		int most = only ? 2 : Items.MaxOnStage;
		if (itemTimer < 0) itemTimer = interval / 2;
		if (--itemTimer > 0) return;
		itemTimer = interval + itemRng.Next(interval / 3) - interval / 6;

		int lying = 0;
		foreach (Node child in GetChildren())
		{
			if (child is Item item && (item.State == Item.Phase.Falling || item.State == Item.Phase.Resting)) lying++;
		}
		if (lying >= most) return;

		ItemData pick = RandomItem(framesElapsed, false);
		if (pick == null) return;
		if (FindItemDrop(out Vector2 at)) SpawnItem(pick, at, Vector2.Zero);
	}

	/// <summary>
	/// Somewhere over a platform, inside the blast zone and clear of anything solid, for an item
	/// to drop from - onto that platform, or whatever it meets on the way down.
	/// </summary>
	bool FindItemDrop(out Vector2 at)
	{
		at = Vector2.Zero;
		var space = GetWorld2D().DirectSpaceState;
		Rect2 zone = stage.BlastZone.Grow(-120.0f);
		for (int tries = 0; tries < 12; tries++)
		{
			Rect2 platform = stage.Platforms[itemRng.Next(stage.Platforms.Count)];
			if (platform.Size.X < 140.0f) continue;
			float left = Mathf.Max(platform.Position.X + 50.0f, zone.Position.X);
			float right = Mathf.Min(platform.End.X - 50.0f, zone.End.X);
			if (right <= left) continue;
			Vector2 point = new Vector2(left + (float)itemRng.NextDouble() * (right - left), platform.Position.Y - 420.0f);
			if (!zone.HasPoint(point)) point.Y = Mathf.Max(point.Y, zone.Position.Y + 40.0f);
			var query = new PhysicsPointQueryParameters2D { Position = point, CollisionMask = 0b11 };
			if (space.IntersectPoint(query, 1).Count > 0) continue;
			at = point;
			return true;
		}
		return false;
	}

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
		if (stage.Speakers != null) stage.Speakers.Match = this;

		for (int i = 0; i < FighterIndices.Length; i++)
		{
			SpawnFighter(FighterCatalog.Get(FighterIndices[i]), i);
		}
		AssignControllers();
		bool named = false;
		for (int i = 0; i < Fighters.Count; i++)
		{
			if (Story != null)
			{
				// A story's lives come from its difficulty (GameRoot.GoStoryChapter).
				if (StartStocks != null && i < StartStocks.Length && StartStocks[i] > 0) Fighters[i].Stocks = StartStocks[i];
				continue;
			}
			// A versus fight starts with the lives the options say.
			Fighters[i].Stocks = GameOptions.Stocks;
			if (Names != null && i < Names.Length && !string.IsNullOrEmpty(Names[i]))
			{
				Fighters[i].PlayerName = Names[i];
				named = true;
			}
		}
		// Every versus fighter wears their tag over their head, wherever they go (Eric, 2026-10-18) -
		// named or "P1", "CPU". A story's scenes have their own names in their speech bubbles.
		ShowPlayerTags = Story == null;

		fx = new HitFx();
		AddChild(fx);

		debugDraw = new DebugDraw { Match = this };
		AddChild(debugDraw);

		camera = new GameCamera
		{
			Bounds = stage.BlastZone,
			Limits = stage.Backdrop,
			Floor = stage.ArtBottom,
			MinZoom = stage.Data.CameraMinZoom,
		};
		AddChild(camera);
		fx.Camera = camera;

		hud = new MatchHud();
		AddChild(hud);
		hud.Build(Fighters, seats: Story == null);

		countdownFrames = CountdownStepFrames * 3 + FightBannerFrames;

		if (Story?.Chapter != null)
		{
			countdownFrames = 0;
			director = new StoryDirector();
			AddChild(director);
			director.Setup(this, Story, stage, camera);
			if (Story.StartAfter && Story.Chapter.After != "") director.PlayAfter();
			else director.Play(Story.Chapter.Before);
			Story.StartAfter = false;
		}
	}

	/// <summary>The HUD is put away while a story scene plays.</summary>
	public void ShowHud(bool on) => hud.Visible = on;

	/// <summary>
	/// The scene before the fight is over: the fight starts where everyone is standing, on
	/// FIGHT! - no 3-2-1, nobody carrying damage in, the CPU and the player back on their sticks.
	/// </summary>
	public void BeginStoryFight()
	{
		foreach (Fighter fighter in Fighters) fighter.ReadyForFight();
		ShowHud(true);
		cpus = null;
		AssignControllers();
		countdownFrames = FightBannerFrames;
	}

	/// <summary>
	/// The top of the solid ground under x - a roof, not the street under it, and never a platform
	/// that can be dropped through - so a scene stands people on the stage itself.
	/// </summary>
	public float FloorAt(float x)
	{
		float best = SolidTop(x);
		return float.IsPositiveInfinity(best) ? stage.Platforms[0].Position.Y : best;
	}

	float SolidTop(float x)
	{
		float best = float.PositiveInfinity;
		foreach (StagePlatform platform in stage.Data.Platforms)
		{
			if (!platform.OneWay && x >= platform.Rect.Position.X && x <= platform.Rect.End.X) best = Mathf.Min(best, platform.Rect.Position.Y);
		}
		return best;
	}

	/// <summary>
	/// The nearest spot to x with solid ground under it, a little in from any edge - so a scene
	/// written for one stage stands people on the roofs of City Rooftops, not in the gaps.
	/// </summary>
	public float GroundNear(float x)
	{
		const float Margin = 50.0f;
		for (float d = 0.0f; d < 2000.0f; d += 10.0f)
		{
			foreach (float at in new[] { x + d, x - d })
			{
				if (!float.IsPositiveInfinity(SolidTop(at)) && !float.IsPositiveInfinity(SolidTop(at - Margin))
					&& !float.IsPositiveInfinity(SolidTop(at + Margin))) return at;
			}
		}
		return x;
	}

	/// <summary>
	/// How far toward toX someone at fromX can walk in a scene before the ground drops away - the
	/// edge of a roof, not the street below it. A scene walks people up to each other; it never
	/// walks them off a building (Eric, 2026-10-16: on City Rooftops they ended up in the road).
	/// </summary>
	public float WalkableTo(float fromX, float toX)
	{
		const float Margin = 50.0f, Drop = 120.0f;
		float floor = FloorAt(fromX);
		float dir = Mathf.Sign(toX - fromX);
		if (dir == 0.0f) return toX;
		for (float at = fromX; dir * (toX - at) > 0.0f; at += dir * 10.0f)
		{
			float ahead = SolidTop(at + dir * Margin);
			if (float.IsPositiveInfinity(ahead) || ahead > floor + Drop) return at;
		}
		return toX;
	}

	void SpawnFighter(FighterData data, int index)
	{
		var fighter = new Fighter();
		fighter.Configure(data, null, index, this);
		if (Seats != null && index < Seats.Length) fighter.Seat = Seats[index];
		fighter.GlobalPosition = stage.SpawnPoints[index % stage.SpawnPoints.Length];
		FaceInward(fighter);
		AddChild(fighter);
		Fighters.Add(fighter);
	}

	/// <summary>
	/// Someone who appears on the right of the screen faces left, into the stage and at the
	/// other player, instead of everyone starting the match facing the same way. Eric's call,
	/// 2026-10-04. A spawn in the dead centre keeps whichever way they were facing.
	/// </summary>
	void FaceInward(Fighter fighter)
	{
		float side = fighter.GlobalPosition.X - stage.BlastZone.GetCenter().X;
		if (Mathf.Abs(side) > 1.0f) fighter.Face(side > 0.0f ? -1 : 1);
	}

	// --- Controllers ---------------------------------------------------------

	/// <summary>Rebuilds every player's controller. Called on hot-plug by GameRoot.</summary>
	public void ReassignControllers()
	{
		// Mid-scene the script has the fighters; it only needs to hear the new pad.
		if (director != null && director.InScene)
		{
			director.RefreshControllers();
			return;
		}

		// A CPU keeps its slot through hot-plugging: plugging in a pad should not replace the
		// computer player. Each person plays on their seat's controller - the one they joined
		// and picked with on the select screen.
		cpus ??= new CpuInputSource[Fighters.Count];

		for (int i = 0; i < Fighters.Count; i++)
		{
			Fighters[i].IsComputer = IsCpu(i);
			if (IsCpu(i))
			{
				if (cpus[i] == null)
				{
					cpus[i] = new CpuInputSource(this, i) { Level = CpuLevels != null && i < CpuLevels.Length ? CpuLevels[i] : 4 };
					cpus[i].Drive(Fighters[i]);
				}
				Fighters[i].Controller = cpus[i];
			}
			else
			{
				Fighters[i].Controller = GameRoot.Instance != null ? GameRoot.Instance.SourceForSeat(Fighters[i].Seat)
					: ControllerAssignment.ForPlayers(GameRoot.MaxPlayers)[Mathf.Clamp(Fighters[i].Seat, 0, GameRoot.MaxPlayers - 1)];
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

	/// <summary>Kicks up dust from the floor - see <see cref="HitFx.SpawnDust"/>.</summary>
	public void Dust(Vector2 at, float size, float push) => fx?.SpawnDust(at, size, push);

	/// <summary>Shakes the camera - a slam into the ground, say. Bigger numbers shake harder.</summary>
	public void Shake(float amount) => camera?.AddShake(amount);

	public void OnExplosion(Vector2 position)
	{
		fx.SpawnBlastFlash(position);
		camera.AddShake(60.0f);
		SfxPlayer.At("explosion", position, 0.03f);
	}

	// --- The end of a match ---------------------------------------------------------
	// The last KO plays in slow motion, as in Smash, and the winner is still in play through it:
	// jump off the edge and he is KO'd like anyone, the blast and all - but the camera stops at
	// the edge of the stage rather than following him out (Eric, 2026-10-15).

	const float EndSlowScale = 0.25f;
	const int EndSlowFrames = 50;
	const int EndEaseFrames = 24;
	int endFrames;

	void TickEndSlowMotion()
	{
		endFrames++;
		if (endFrames <= EndSlowFrames) Engine.TimeScale = EndSlowScale;
		else if (endFrames <= EndSlowFrames + EndEaseFrames)
		{
			Engine.TimeScale = Mathf.Lerp(EndSlowScale, 1.0f, (endFrames - EndSlowFrames) / (float)EndEaseFrames);
		}
		else if (endFrames == EndSlowFrames + EndEaseFrames + 1) Engine.TimeScale = 1.0f;
	}

	public override void _ExitTree()
	{
		// Leaving mid slow-motion must not leave the menus running slow.
		Engine.TimeScale = 1.0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (countdownFrames > 0)
		{
			CountdownSound();
			countdownFrames--;
		}
		if (director != null && director.InScene) return;
		if (matchOver) TickEndSlowMotion();
		else if (countdownFrames <= FightBannerFrames) TickItems();

		foreach (Fighter fighter in Fighters)
		{
			if (fighter.State == FighterState.Eliminated) continue;
			if (fighter.State == FighterState.Respawning) continue;

			// The bottom always KOs. The side and the top only KO someone still flying helplessly
			// from a hit: jumping or recovering high on purpose, or running along a walk-off floor,
			// just meets the edge of the world (Fighter.StayInsideBlastZone). Eric's call,
			// 2026-10-04.
			if (stage.IsOutOfBounds(fighter.GlobalPosition)
				&& (fighter.IsLaunched || stage.IsBelowBottom(fighter.GlobalPosition)))
			{
				KillOffStage(fighter);
			}
		}

		if (!matchOver) CheckForWinner();
	}

	void KillOffStage(Fighter fighter)
	{
		// The blast goes off at the edge of what is on screen, nearest where they went out, and
		// fires back in across the stage - so it is seen even though the fighter is long gone. It
		// stays at that spot in the world while the camera moves on.
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
		FaceInward(fighter);
	}

	/// <summary>
	/// Rubbed out by the eraser: a KO where he stood, with no blast to fly into - just a shake,
	/// the KO's sound and a rumble, and the stock is gone.
	/// </summary>
	public void ErasedOut(Fighter fighter)
	{
		camera.AddShake(90.0f);
		SfxPlayer.At("ko_blast", fighter.GlobalPosition, 0.0f);
		if (fighter.Controller is IHapticInputSource haptic) haptic.Rumble(1.0f, 0.45f);
		fighter.LoseStock(stage.RespawnPoint);
		FaceInward(fighter);
	}

	void CheckForWinner()
	{
		// Sides still standing - every fighter is a side of one, unless there are teams.
		var sides = new HashSet<int>();
		Fighter last = null;

		foreach (Fighter fighter in Fighters)
		{
			if (fighter.Stocks > 0)
			{
				sides.Add(TeamOf(fighter));
				last = fighter;
			}
		}
		int alive = sides.Count;

		if (alive <= 1)
		{
			matchOver = true;
			MusicPlayer.Stop();
			SfxPlayer.Ui("game_set");
			winner = last == null ? "Draw!"
				: TeamBattle ? $"{MenuTheme.TeamName(TeamOf(last))} Team wins!"
				: $"{last.Data.DisplayName} wins!";
			victor = last;
			// The winners celebrate, once they stand still.
			if (last != null)
			{
				foreach (Fighter fighter in Fighters)
				{
					if (fighter.Stocks > 0 && TeamOf(fighter) == TeamOf(last)) fighter.Celebrating = true;
					// The computer stops when it is won, as a player puts the pad down.
					if (director == null && fighter.Controller is CpuInputSource) fighter.Controller = null;
				}
			}
			director?.OnFightOver(last);
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

		if (director != null && director.DrivesCamera) director.DriveCamera((float)delta);
		else if (director == null && victor != null && endFrames > EndSlowFrames && victor.State != FighterState.Eliminated)
		{
			// Won: in on the winner, celebrating, with the banner over his head - or on the whole
			// winning team, together.
			var winners = Fighters.FindAll(f => f.Celebrating && f.State != FighterState.Eliminated && !f.IsWaitingToRespawn);
			if (winners.Count > 1) camera.FrameFighters(winners, (float)delta);
			else camera.FrameOn(victor.GlobalPosition + new Vector2(0.0f, -victor.Data.BodySize.Y * 0.2f), 1.3f, 2.5f, (float)delta);
		}
		else camera.FrameFighters(Fighters, (float)delta);

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
			// In a story the next scene says who won. In versus the banner goes up top, out of the
			// way of the winner celebrating under it.
			if (director != null) hud.SetBanner("");
			else
			{
				hud.SetBannerHigh(true);
				hud.SetBanner(winner, "Press Start to pick fighters");
			}
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
			// A story's boss fights to his own theme; everyone else to the stage's.
			MusicPlayer.Play(BossPlayer >= 0 ? MusicPlayer.Boss : stage.Data.Music);
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
		SfxPlayer.Hit(contactPoint, knockback, blocked, attacker != null && attacker.Data.SlicesOnHit);
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
				GameOptions.SmashLayout = !ControllerAssignment.Layout.IsStandardLayout;
				GameOptions.Save();
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
			if (director == null) GameRoot.Instance.GoCharacterSelect();
			return;
		}
		if (pauseMenu != null) return;
		if (director != null && director.InScene) return;

		var humans = new List<IInputSource>();
		for (int i = 0; i < Fighters.Count; i++)
		{
			if (!IsCpu(i) && Fighters[i].Controller != null) humans.Add(Fighters[i].Controller);
		}

		pauseMenu = new PauseMenu();
		void Leave(System.Action go)
		{
			GetTree().Paused = false;
			MusicPlayer.SetPaused(false);
			go();
		}
		// A story has no fighters to pick: its map is where it goes back to instead.
		if (director != null)
		{
			pauseMenu.Open(humans, Resume,
				("Story map", () => Leave(GameRoot.Instance.GoStoryMap)),
				("Main menu", () => Leave(GameRoot.Instance.GoMainMenu)));
		}
		else
		{
			pauseMenu.Open(humans, Resume,
				("Pick fighters", () => Leave(GameRoot.Instance.GoCharacterSelect)),
				("Main menu", () => Leave(GameRoot.Instance.GoMainMenu)));
		}
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
		director = null;
		pauseMenu = null;
		GetTree().Paused = false;
		matchOver = false;
		victor = null;
		status = "";
		winner = "";
		lastKnockback = 0.0f;
		Engine.TimeScale = 1.0f;
		endFrames = 0;
		// The battle music comes back in on FIGHT!, like the first time.
		MusicPlayer.Stop();
		MusicPlayer.SetPaused(false);

		BuildMatch();
	}
}
