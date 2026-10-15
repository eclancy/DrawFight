using System.Collections.Generic;
using Godot;

/// <summary>One fight in a story, with the scene before it and the scene after.</summary>
public class StoryChapter
{
	public int Stage;

	/// <summary>Story ids of who the player fights, by roster - "doombot".</summary>
	public string[] Opponents = System.Array.Empty<string>();

	/// <summary>The scene before the fight, as "owner/scene" - "doombot/finale". It ends in "fight".</summary>
	public string Before = "";

	/// <summary>The scene once the fight is won, or "" to go straight on.</summary>
	public string After = "";
}

/// <summary>
/// One player's way through a story: whose story it is, the chapters, how far they have got.
/// Kept by GameRoot, so a lost fight is retried with the same chapter and a won one moves on.
/// </summary>
public class StoryRun
{
	public string PlayerId = "";
	public readonly List<StoryChapter> Chapters = new List<StoryChapter>();
	public int At;

	/// <summary>How many times the current chapter has been tried, so a retry can say so.</summary>
	public int Tries;

	/// <summary>Open on the scene after the fight, for looking at it (--story=WHO:N:after).</summary>
	public bool StartAfter;

	public bool Done => At >= Chapters.Count;
	public StoryChapter Chapter => Done ? null : Chapters[At];

	/// <summary>
	/// A fighter's story, as far as it is built: so far, only the finale against DoomBot. The
	/// rest of the chapters come with the scripts (.ai/story-mode.md, "What has to be built").
	/// </summary>
	public static StoryRun For(string playerId)
	{
		var run = new StoryRun { PlayerId = playerId };
		if (playerId != "doombot" && StoryScript.Has("doombot", "finale"))
		{
			run.Chapters.Add(new StoryChapter
			{
				Stage = 0,
				Opponents = new[] { "doombot" },
				Before = "doombot/finale",
				After = StoryScript.Has("doombot", "beaten") ? "doombot/beaten" : "",
			});
		}
		return run;
	}
}

/// <summary>
/// Drives a fighter the way a controller does - a story's hands on the stick. The director
/// sets what is pressed each frame; a press is an edge for one poll, as from any pad.
/// </summary>
public class ScriptedInput : IInputSource
{
	public InputState Next;

	public InputState Poll()
	{
		InputState s = Next;
		Next.JumpPressed = Next.AttackPressed = Next.SpecialPressed = Next.TauntPressed = Next.GrabPressed = false;
		return s;
	}
}

/// <summary>
/// Plays a story scene inside a real match: the real stage, the real fighters on their rigs, the
/// real camera, items and sounds - driven by a script instead of controllers. When the scene
/// reaches "fight", the fighters are already standing there and the fight starts in place; when
/// it is won, the scene after plays in the same place. See .ai/story-mode.md.
///
/// Each line of the script runs until it is done - a line read, a walk arrived - and then the
/// next starts; a line ending in "&amp;" lets the next start at once, alongside it.
/// </summary>
public partial class StoryDirector : Node
{
	public MatchManager Match;
	public StoryRun Run;

	StoryOverlay overlay;
	StoryBoard board;
	GameCamera camera;

	/// <summary>A scene is playing: the script has the fighters and the camera.</summary>
	public bool InScene { get; private set; }

	/// <summary>The camera is the script's while a scene plays.</summary>
	public bool DrivesCamera => InScene;

	List<StoryStep> steps = new List<StoryStep>();
	int next;
	bool afterFight;

	/// <summary>A line of the script that has started and not yet finished.</summary>
	class Running
	{
		public StoryStep Step;
		public Fighter Actor;
		public StoryOverlay.Line Line;
		public object Written;
		public int Frames;
		public float TargetX;
		public bool Started;
		public bool Done;
	}

	Running current;
	readonly List<Running> alongside = new List<Running>();
	readonly Dictionary<Fighter, ScriptedInput> inputs = new Dictionary<Fighter, ScriptedInput>();
	IInputSource[] humans = System.Array.Empty<IInputSource>();
	int skipHeld;
	bool skipping;
	int fightOverFrames = -1;
	Fighter winner;

	// What the camera is doing: a shot, and how fast it gets there.
	string shot = "wide";
	Fighter shotA, shotB;
	Vector2 shotAt;
	float shotZoom = -1.0f;
	float shotSpeed = 4.0f;

	public override void _Ready()
	{
		// Before the fighters, so what the script presses this frame is what they read this frame.
		ProcessPhysicsPriority = -10;
	}

	public override void _ExitTree()
	{
		// Leaving mid-skip must not leave the whole game running fast.
		if (skipping) Engine.TimeScale = 1.0f;
	}

	/// <summary>Straight to the scene after the fight, as if it had been won (--story=WHO:N:after).</summary>
	public void PlayAfter()
	{
		afterFight = true;
		Play(Run.Chapter.After);
	}

	public void Setup(MatchManager match, StoryRun run, Stage stage, GameCamera cam)
	{
		Match = match;
		Run = run;
		camera = cam;

		overlay = new StoryOverlay { Match = match };
		match.AddChild(overlay);

		// On the fridge door: behind the fighters, in front of the stage.
		board = new StoryBoard();
		match.AddChild(board);
		match.MoveChild(board, stage.GetIndex() + 1);
	}

	// --- Scenes ---------------------------------------------------------------------

	/// <summary>Starts a scene by "owner/scene". Every fighter goes over to the script.</summary>
	public void Play(string scene)
	{
		steps = Expand(scene, 0);
		next = 0;
		current = null;
		alongside.Clear();
		InScene = true;
		skipping = false;
		shot = "wide";
		shotZoom = -1.0f;
		shotSpeed = 4.0f;

		humans = new[] { ControllerAssignment.ForPlayers(1)[0], KeyboardInputSource.Player1() };
		inputs.Clear();
		foreach (Fighter fighter in Match.Fighters)
		{
			var input = new ScriptedInput();
			inputs[fighter] = input;
			fighter.Controller = input;
		}
		overlay.ShowBars(true);
		Match.ShowHud(false);
	}

	/// <summary>"owner/scene" read into steps, with every "include" opened up in place.</summary>
	List<StoryStep> Expand(string scene, int depth)
	{
		var list = new List<StoryStep>();
		string[] parts = scene.Split('/');
		StoryScene found = parts.Length == 2 ? StoryScript.Scene(parts[0], parts[1]) : null;
		if (found == null)
		{
			GD.PushError($"StoryDirector: no scene \"{scene}\"");
			return list;
		}
		foreach (StoryStep step in found.Steps)
		{
			if (step.Verb == "include" && depth < 4)
			{
				string who = step.Arg(0) == "player" ? Run.PlayerId : step.Arg(0);
				list.AddRange(Expand($"{who}/{step.Arg(1)}", depth + 1));
			}
			else
			{
				list.Add(step);
			}
		}
		return list;
	}

	/// <summary>Who a script means: "player" is whoever's story this is; anyone else, by name.</summary>
	Fighter ActorOf(string who)
	{
		if (who == "player") return Match.Fighters.Count > 0 ? Match.Fighters[0] : null;
		foreach (Fighter fighter in Match.Fighters)
		{
			if (StoryScript.IdOf(fighter.Data) == who) return fighter;
		}
		return null;
	}

	ScriptedInput InputOf(Fighter fighter) => fighter != null && inputs.TryGetValue(fighter, out ScriptedInput input) ? input : null;

	/// <summary>A pad was plugged in or out mid-scene: listen to the new one.</summary>
	public void RefreshControllers()
	{
		humans = new[] { ControllerAssignment.ForPlayers(1)[0], KeyboardInputSource.Player1() };
	}

	// --- Running ----------------------------------------------------------------------

	public override void _PhysicsProcess(double delta)
	{
		if (fightOverFrames >= 0)
		{
			// The KO lands, a beat, then whatever comes next.
			if (++fightOverFrames == 100) AfterFight();
			return;
		}
		if (!InScene) return;

		ReadHumans();
		foreach (ScriptedInput input in inputs.Values) input.Next.Move = Vector2.Zero;
		foreach (ScriptedInput input in inputs.Values) input.Next.BlockHeld = input.Next.AttackHeld = input.Next.SpecialHeld = input.Next.MoveFromDpad = false;

		for (int i = alongside.Count - 1; i >= 0; i--)
		{
			if (Tick(alongside[i])) alongside.RemoveAt(i);
		}

		// As many lines as finish at once this frame - a cast, a camera, a look - then the one
		// that takes time.
		for (int guard = 0; guard < 64; guard++)
		{
			if (current != null)
			{
				if (!Tick(current)) break;
				current = null;
			}
			if (!InScene || next >= steps.Count)
			{
				if (InScene && next >= steps.Count && alongside.Count == 0) Finish("end");
				break;
			}

			StoryStep step = steps[next];
			if ((step.Verb == "fight" || step.Verb == "end") && alongside.Count > 0) break;
			next++;
			var run = new Running { Step = step, Actor = step.Who != "" ? ActorOf(step.Who) : null };
			if (run.Actor != null) StopActor(run.Actor);
			if (step.Together) alongside.Add(run);
			else current = run;
		}
	}

	/// <summary>A new line for an actor ends whatever they were still doing.</summary>
	void StopActor(Fighter actor)
	{
		for (int i = alongside.Count - 1; i >= 0; i--)
		{
			if (alongside[i].Actor == actor && alongside[i].Step.Verb != "say") alongside.RemoveAt(i);
		}
	}

	void ReadHumans()
	{
		bool hurry = false, holding = false;
		foreach (IInputSource human in humans)
		{
			InputState s = human.Poll();
			hurry |= s.JumpPressed || s.AttackPressed;
			holding |= s.SpecialHeld;
		}

		skipHeld = holding ? skipHeld + 1 : 0;
		overlay.SkipHeld = Mathf.Min(1.0f, skipHeld / (float)SkipHoldFrames);
		if (skipHeld >= SkipHoldFrames && !skipping) StartSkipping();

		if (hurry && current != null)
		{
			if (current.Line != null) overlay.Hurry(current.Line);
			else if (current.Written != null) board.Hurry(current.Written);
		}
	}

	/// <summary>Held this long, the scene skips.</summary>
	const int SkipHoldFrames = 40;

	/// <summary>
	/// Skipping runs the rest of the scene at speed under a sheet of paper, so everyone still
	/// ends up exactly where the scene leaves them for the fight.
	/// </summary>
	void StartSkipping()
	{
		skipping = true;
		overlay.Skipping = board.Skipping = true;
		Engine.TimeScale = 8.0f;
	}

	void StopSkipping()
	{
		if (!skipping) return;
		skipping = false;
		overlay.Skipping = board.Skipping = false;
		Engine.TimeScale = 1.0f;
	}

	/// <summary>Runs one line for a frame. True once it is done.</summary>
	bool Tick(Running run)
	{
		StoryStep step = run.Step;
		bool first = !run.Started;
		run.Started = true;
		run.Frames++;
		Fighter actor = run.Actor;
		ScriptedInput input = InputOf(actor);

		switch (step.Verb)
		{
			case "say":
				if (actor == null) return true;
				if (first) run.Line = overlay.Say(actor, step.Mood, step.Text);
				if (!run.Line.Finished) return false;
				overlay.Close(run.Line);
				actor.Talking = false;
				return true;

			case "narrate":
				if (first) run.Line = overlay.Narrate(step.Text);
				if (!run.Line.Finished) return false;
				overlay.Close(run.Line);
				return true;

			case "write":
				if (actor == null) return true;
				if (first) run.Written = board.Write(actor, step.Text);
				return board.IsFinished(run.Written);

			case "wait":
				return skipping || run.Frames >= step.Number(0, 30);

			case "camera":
				SetShot(step);
				return true;

			case "shake":
				Match.Shake(step.Number(0, 40));
				return true;

			case "music":
				if (step.Arg(0) == "stop") MusicPlayer.Stop();
				else MusicPlayer.Play(step.Arg(0) == "battle" ? MusicPlayer.Battle : MusicPlayer.Menu);
				return true;

			case "sound":
				if (!skipping) SfxPlayer.Ui(step.Arg(0));
				return true;

			case "board":
				if (step.Arg(0) == "clear") board.Clear();
				else board.At(new Vector2(step.Number(1, 0), step.Number(2, 0)));
				return true;

			case "cast":
				Cast(step);
				return true;

			case "fight":
			case "end":
				Finish(step.Verb);
				return true;
		}

		if (actor == null || input == null)
		{
			GD.PushWarning($"StoryDirector: {step.Source}: nobody called \"{step.Who}\" in this match");
			return true;
		}
		return TickActor(run, actor, input, first);
	}

	bool TickActor(Running run, Fighter actor, ScriptedInput input, bool first)
	{
		StoryStep step = run.Step;
		switch (step.Verb)
		{
			case "walk":
			case "run":
			{
				if (first)
				{
					Fighter other = ActorOf(step.Arg(1));
					if (other != null)
					{
						// Up to them, not into them.
						float side = Mathf.Sign(actor.GlobalPosition.X - other.GlobalPosition.X);
						run.TargetX = other.GlobalPosition.X + (side == 0 ? -1 : side) * 170.0f;
					}
					else run.TargetX = PlaceX(step.Arg(1), actor);
				}
				float dx = run.TargetX - actor.GlobalPosition.X;
				if (Mathf.Abs(dx) < (step.Verb == "walk" ? 14.0f : 40.0f) || run.Frames > 420) return true;
				input.Next.Move = new Vector2(Mathf.Sign(dx) * (step.Verb == "walk" ? 0.45f : 1.0f), 0.0f);
				return false;
			}

			case "face":
				if (first) actor.TurnTo(DirectionTo(step.Arg(0), actor));
				return run.Frames >= 8;

			case "turn":
				if (first) actor.TurnTo(-actor.Facing);
				return run.Frames >= 8;

			case "look":
				actor.Expression = step.Arg(0) == "normal" ? "" : step.Arg(0);
				return true;

			case "jump":
				if (first) input.Next.JumpPressed = true;
				return run.Frames > 6 && actor.IsOnFloor() || run.Frames > 150;

			case "attack":
			case "smash":
			case "special":
			case "taunt":
			{
				// Pressed, and pressed again if it did not take (he was still landing, say).
				if (actor.State == FighterState.Attacking) run.Done = true;
				else if (run.Done) return true;
				if (run.Frames > 240) return true;
				if (!run.Done)
				{
					Vector2 dir = step.Arg(0) switch
					{
						"forward" => new Vector2(actor.Facing, 0.0f),
						"up" => Vector2.Up,
						"down" => Vector2.Down,
						_ => Vector2.Zero,
					};
					input.Next.Move = step.Verb == "attack" ? dir * 0.6f : dir;
					input.Next.MoveFromDpad = step.Verb == "smash";
					if (run.Frames % 12 == 1)
					{
						if (step.Verb == "special") input.Next.SpecialPressed = true;
						else if (step.Verb == "taunt") input.Next.TauntPressed = true;
						else input.Next.AttackPressed = true;
					}
				}
				return false;
			}

			case "block":
				input.Next.BlockHeld = true;
				return run.Frames >= step.Number(0, 60);

			case "crouch":
				input.Next.Move = Vector2.Down;
				return run.Frames >= step.Number(0, 60);

			case "drop":
				if (first)
				{
					float x = step.Arg(0) == "at" ? PlaceX(step.Arg(1), actor) : actor.GlobalPosition.X;
					actor.ReviveForScene(new Vector2(x, camera.VisibleRect().Position.Y - 300.0f));
				}
				// Down past the platforms, onto the floor - a scene drops someone onto the stage.
				float feet = actor.GlobalPosition.Y + actor.Data.BodySize.Y * 0.5f;
				if (feet < Match.FloorAt(actor.GlobalPosition.X) - 50.0f) actor.FallThroughPlatforms();
				return run.Frames > 4 && actor.IsOnFloor() || run.Frames > 360;

			case "at":
				Place(actor, PlaceX(step.Arg(0), actor));
				return true;

			case "heat":
				actor.SetHeat(step.Number(0, 0));
				return true;

			case "stocks":
				actor.Stocks = Mathf.Max(1, (int)step.Number(0, 3));
				return true;

			case "twirl":
			case "roll":
				if (first) actor.Twirl((int)(36 * step.Number(0, 1)), step.Number(0, 1), step.Verb == "roll");
				return !actor.Twirling;

			case "hold":
				int item = ItemCatalog.IndexOf(step.Arg(0));
				if (item >= 0) actor.CatchItem(Match.SpawnItem(ItemCatalog.Get(item), actor.GlobalPosition, Vector2.Zero));
				return true;
		}
		return true;
	}

	/// <summary>A place on the stage: a number, or where someone is standing.</summary>
	float PlaceX(string place, Fighter actor)
	{
		if (place.IsValidFloat()) return place.ToFloat();
		Fighter other = ActorOf(place);
		return other != null ? other.GlobalPosition.X : actor.GlobalPosition.X;
	}

	int DirectionTo(string place, Fighter actor)
	{
		if (place == "left") return -1;
		if (place == "right") return 1;
		float dx = PlaceX(place, actor) - actor.GlobalPosition.X;
		return dx < 0.0f ? -1 : 1;
	}

	/// <summary>Stood on the floor at x, at once.</summary>
	void Place(Fighter actor, float x)
	{
		float floor = Match.FloorAt(x);
		actor.ReviveForScene(new Vector2(x, floor - actor.Data.BodySize.Y * 0.5f - 1.0f));
	}

	void Cast(StoryStep step)
	{
		Fighter actor = ActorOf(step.Arg(0));
		if (actor == null) return;
		if (step.Arg(1) == "offstage")
		{
			// Waiting in the wings for a "drop": not seen, not moving.
			actor.Visible = false;
			actor.ProcessMode = ProcessModeEnum.Disabled;
			return;
		}
		float x = step.Number(2, actor.GlobalPosition.X);
		Place(actor, x);
		int facing = step.Arg(3) == "facing" ? (step.Arg(4) == "left" ? -1 : 1) : (x > 0.0f ? -1 : 1);
		actor.Face(facing);
	}

	// --- The camera ----------------------------------------------------------------

	void SetShot(StoryStep step)
	{
		shot = step.Arg(0);
		shotA = shotB = null;
		shotZoom = -1.0f;
		shotSpeed = 4.0f;
		int i = 1;
		switch (shot)
		{
			case "on": shotA = ActorOf(step.Arg(1)); i = 2; break;
			case "two": shotA = ActorOf(step.Arg(1)); shotB = ActorOf(step.Arg(2)); i = 3; break;
			case "at": shotAt = new Vector2(step.Number(1, 0), step.Number(2, 0)); i = 3; break;
		}
		bool cut = false;
		for (; i < step.Args.Length; i++)
		{
			switch (step.Args[i])
			{
				case "zoom": shotZoom = step.Number(++i, 1.0f); break;
				case "slow": shotSpeed = 1.2f; break;
				case "fast": shotSpeed = 9.0f; break;
				case "whip": shotSpeed = 16.0f; if (!skipping) SfxPlayer.Ui("item_gust"); break;
				case "cut": cut = true; break;
			}
		}
		if (cut && ShotTarget(out Vector2 centre, out float zoom)) camera.CutTo(centre, zoom);
	}

	bool ShotTarget(out Vector2 centre, out float zoom)
	{
		centre = Vector2.Zero;
		zoom = 1.0f;
		switch (shot)
		{
			case "on":
				if (shotA == null) return false;
				// A little above his middle: his face, with room over him for what he says.
				centre = shotA.GlobalPosition + new Vector2(0.0f, -shotA.Data.BodySize.Y * 0.55f);
				zoom = shotZoom > 0.0f ? shotZoom : 1.25f;
				return true;
			case "two":
				if (shotA == null || shotB == null) return false;
				centre = (shotA.GlobalPosition + shotB.GlobalPosition) * 0.5f + new Vector2(0.0f, -110.0f);
				float span = Mathf.Abs(shotA.GlobalPosition.X - shotB.GlobalPosition.X) + 900.0f;
				zoom = shotZoom > 0.0f ? shotZoom : Mathf.Clamp(1920.0f / span, 0.5f, 1.15f);
				return true;
			case "at":
				centre = shotAt;
				zoom = shotZoom > 0.0f ? shotZoom : 0.8f;
				return true;
		}
		return false;
	}

	/// <summary>Called by the match each frame instead of its own framing while a scene plays.</summary>
	public void DriveCamera(float dt)
	{
		if (ShotTarget(out Vector2 centre, out float zoom))
		{
			camera.FrameOn(centre, zoom, skipping ? 60.0f : shotSpeed, dt);
			return;
		}
		// Wide: everyone on the stage, as in a fight.
		var seen = new List<Fighter>();
		foreach (Fighter fighter in Match.Fighters) if (fighter.Visible) seen.Add(fighter);
		camera.FrameFighters(seen, dt);
	}

	// --- Into the fight, and out of it ------------------------------------------------

	void Finish(string how)
	{
		InScene = false;
		StopSkipping();
		current = null;
		alongside.Clear();
		overlay.CloseAll();
		overlay.ShowBars(false);
		board.Clear();
		foreach (Fighter fighter in Match.Fighters)
		{
			fighter.Talking = false;
			fighter.Expression = "";
		}

		if (how == "fight" && !afterFight)
		{
			Match.BeginStoryFight();
			return;
		}
		// The scene after a fight - or one with no fight in it - is over: on to the next.
		GameRoot.Instance.StoryNext();
	}

	/// <summary>The fight is over: the match says who is left.</summary>
	public void OnFightOver(Fighter last)
	{
		winner = last;
		fightOverFrames = 0;
	}

	void AfterFight()
	{
		fightOverFrames = -1;
		if (winner == null || winner != Match.Fighters[0])
		{
			GameRoot.Instance.StoryRetry();
			return;
		}
		string after = Run.Chapter?.After ?? "";
		if (after == "")
		{
			GameRoot.Instance.StoryNext();
			return;
		}
		afterFight = true;
		Play(after);
	}
}
