using System.Collections.Generic;
using Godot;

/// <summary>
/// Break the targets (Eric, 2026-10-29; Smash's Break the Targets is the model): ten targets on a
/// course, one fighter, and a clock running from GO. Anything he hits with breaks one - an attack's
/// hitbox, a shot or anything else he sets loose, a thrown item. All ten and the clock stops, with
/// his time and his best on that course (TargetRecords); off the bottom and the run is over. Then
/// Start goes again and B goes back to the courses.
///
/// MatchManager runs the match round it as usual - the stage, the fighter, the 3-2-1 - and hands
/// this the camera. Everything about targets is here, so the match itself only has to know a run
/// is on.
/// </summary>
public partial class TargetsRun : Node2D
{
	public MatchManager Match;
	public TargetCourse Course;
	public Fighter Player;

	/// <summary>How big a target is, round: about a fighter's head, so it reads as a thing to hit.</summary>
	public const float Radius = 36.0f;

	bool[] broken;
	int left;

	/// <summary>The clock: frames since GO, stopped at the last target.</summary>
	int frames;
	bool complete, fell;
	int overFrames;
	int best = -1;
	bool newBest;

	/// <summary>The player's controller once the run is over: Start goes again, B to the courses.</summary>
	MenuNav nav;

	/// <summary>The run has ended, either way. A fall after the last target is no longer a fall.</summary>
	public bool Over => complete || fell;

	/// <summary>A target just broken: its card in pieces and its magnet dropping away, for a moment.</summary>
	sealed class Burst
	{
		public Vector2 At;
		public int Age;
		public int Seed;
	}

	readonly List<Burst> bursts = new List<Burst>();
	const int BurstFrames = 54;

	/// <summary>Frames after the run ends before Start or B count, so a button mashed at the end does not skip it.</summary>
	const int OverSettleFrames = 40;

	Control hud;

	public override void _Ready()
	{
		broken = new bool[Course.Targets.Length];
		left = broken.Length;
		best = TargetRecords.Best(Player.Data, Course.Id);

		// Its own layer over the match's HUD, so the end of a run sits over everything.
		var layer = new CanvasLayer { Layer = 2 };
		AddChild(layer);
		hud = new Control
		{
			AnchorsPreset = (int)Control.LayoutPreset.FullRect,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		layer.AddChild(hud);
		hud.Draw += DrawHud;
	}

	public override void _PhysicsProcess(double delta)
	{
		for (int i = bursts.Count - 1; i >= 0; i--)
		{
			if (++bursts[i].Age > BurstFrames) bursts.RemoveAt(i);
		}
		QueueRedraw();
		hud.QueueRedraw();

		if (Over)
		{
			TickOver();
			return;
		}
		// The clock starts on GO, as the fighter can move.
		if (Match.InputLocked) return;
		frames++;

		for (int i = 0; i < broken.Length; i++)
		{
			if (!broken[i] && Hit(Course.Targets[i])) Break(i);
		}
		if (left == 0) Complete();
		else if (Player.State == FighterState.Eliminated) Fail();
	}

	// --- Breaking -------------------------------------------------------------------------

	/// <summary>Whether anything of his reaches the target this frame.</summary>
	bool Hit(Vector2 at) => HitByAttack(at) || HitByShot(at) || HitByItem(at);

	/// <summary>
	/// His attack, the way it would reach a fighter: the capsule from inside his body out past the
	/// limb (Fighter.HitboxRoot, HitReach). A grab only grabs, a size change only changes size, and
	/// a bomb hits with the blast it leaves, which is a shot.
	/// </summary>
	bool HitByAttack(Vector2 at)
	{
		MoveData move = Player.CurrentMove;
		if (move == null || !Player.IsHitboxLive || Player.CurrentHitboxRadius <= 0.0f) return false;
		if (move.Special == SpecialKind.Grab || move.Special == SpecialKind.CommandGrab || move.Special == SpecialKind.Resize
			|| move.Special == SpecialKind.Choice || move.Special == SpecialKind.Bomb) return false;
		Vector2 root = Player.HitboxRoot();
		(Vector2 tip, float radius) = Player.HitReach();
		return DistanceToSegment(at, root, tip) <= radius + Radius;
	}

	/// <summary>A fireball, a thrown hammer, a blast - anything he set loose - used up by it as on a fighter.</summary>
	bool HitByShot(Vector2 at)
	{
		var box = new Rect2(at - Vector2.One * Radius, Vector2.One * Radius * 2.0f);
		foreach (Node child in Match.GetChildren())
		{
			if (child is not Hazard shot || shot.OwnerFighter != Player || !shot.Reaches(box)) continue;
			shot.BrokeTarget();
			return true;
		}
		return false;
	}

	bool HitByItem(Vector2 at)
	{
		foreach (Node child in Match.GetChildren())
		{
			if (child is Item item && item.State == Item.Phase.Thrown && item.OwnerFighter == Player
				&& item.GlobalPosition.DistanceTo(at) <= item.Radius + Radius) return true;
		}
		return false;
	}

	static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float t = ab.LengthSquared() < 0.01f ? 0.0f : Mathf.Clamp((point - a).Dot(ab) / ab.LengthSquared(), 0.0f, 1.0f);
		return point.DistanceTo(a + ab * t);
	}

	void Break(int i)
	{
		broken[i] = true;
		left--;
		bursts.Add(new Burst { At = Course.Targets[i], Seed = i * 17 + 5 });
		SfxPlayer.At("target_break", Course.Targets[i], 0.06f);
		Match.Shake(16.0f);
		if (Player.Controller is IHapticInputSource pad) pad.Rumble(0.35f, 0.08f);
	}

	// --- The end of a run ------------------------------------------------------------------

	void Complete()
	{
		complete = true;
		newBest = TargetRecords.Submit(Player.Data, Course.Id, frames);
		if (newBest) best = frames;
		MusicPlayer.Stop();
		SfxPlayer.Ui("game_set");
		Player.Celebrating = true;
		TakeController();
	}

	void Fail()
	{
		fell = true;
		MusicPlayer.Stop();
		TakeController();
	}

	/// <summary>
	/// The controller comes off the fighter, as a player puts the pad down at the end of a match,
	/// and this reads it instead - one reader, so no button is seen twice.
	/// </summary>
	void TakeController()
	{
		if (Player.Controller != null) nav = new MenuNav(Player.Controller);
		Player.Controller = null;
	}

	void TickOver()
	{
		overFrames++;
		if (nav == null || overFrames < OverSettleFrames) return;
		nav.Poll();
		if (nav.Start || nav.Confirm)
		{
			SfxPlayer.Ui("ui_select");
			GameRoot.Instance?.GoTargetRun();
		}
		else if (nav.Cancel)
		{
			SfxPlayer.Ui("ui_back");
			GameRoot.Instance?.GoTargetCourses();
		}
	}

	// --- Camera ------------------------------------------------------------------------------

	/// <summary>The rows of HUD over and under the course: the clock and targets up top, his name and percent below.</summary>
	const float TopBand = 170.0f;

	/// <summary>
	/// Every target still standing and the fighter, all in view: the course is laid out at the
	/// start, and the view closes in as they go. Done, it goes in on him celebrating.
	/// </summary>
	public void Frame(GameCamera camera, float dt)
	{
		if (complete && Player.IsInPlay)
		{
			// In on him, standing just over his name, under the words (DrawHud) rather than behind
			// them - as close as leaves room for him there, arms up, however big he is.
			float close = Mathf.Min(1.0f, 190.0f / Player.Data.BodySize.Y);
			Vector2 feet = Player.GlobalPosition + new Vector2(0.0f, Player.Data.BodySize.Y * 0.5f);
			camera.FrameOn(feet - new Vector2(0.0f, (880.0f - 540.0f) / close), close, 2.5f, dt);
			return;
		}

		bool any = false;
		Vector2 min = Vector2.Zero, max = Vector2.Zero;
		void Include(Vector2 p)
		{
			min = any ? new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y)) : p;
			max = any ? new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y)) : p;
			any = true;
		}
		if (Player.IsInPlay) Include(Player.GlobalPosition);
		for (int i = 0; i < broken.Length; i++) if (!broken[i]) Include(Course.Targets[i]);
		if (!any) return;

		Vector2 viewport = GetViewportRect().Size;
		Vector2 span = max - min + Vector2.One * 360.0f;
		float tall = viewport.Y - TopBand - GameCamera.HudBand;
		float zoom = Mathf.Clamp(Mathf.Min(viewport.X / span.X, tall / span.Y), Course.Stage.CameraMinZoom, 0.9f);
		// The middle of the screen between the bands, not of the whole screen.
		Vector2 centre = (min + max) * 0.5f + new Vector2(0.0f, (GameCamera.HudBand - TopBand) * 0.5f / zoom);
		camera.FrameOn(centre, zoom, 4.0f, dt);
	}

	// --- Drawing -----------------------------------------------------------------------------

	public override void _Draw()
	{
		for (int i = 0; i < broken.Length; i++)
		{
			if (!broken[i]) DrawTarget(this, Course.Targets[i], Radius, i * 17 + 5);
		}
		foreach (Burst burst in bursts) DrawBurst(burst);
	}

	static readonly Color Red = new Color(0.90f, 0.31f, 0.29f);
	static readonly Color Card = new Color(0.99f, 0.97f, 0.93f);
	static readonly Color Ink = new Color(0.19f, 0.31f, 0.61f);

	static readonly Color[] MagnetColours =
	{
		new Color(0.36f, 0.62f, 0.93f), new Color(0.44f, 0.78f, 0.44f),
		new Color(0.98f, 0.80f, 0.30f), new Color(0.72f, 0.52f, 0.90f),
	};

	/// <summary>
	/// A target: a disc of card coloured in red crayon round and round in rings, inked round its
	/// edge - it is a real thing to hit, so it has an outline - and held up by a fridge magnet, so
	/// it is stuck to the door like everything else on it rather than floating. Every wobble is
	/// hashed from the seed.
	/// </summary>
	public static void DrawTarget(CanvasItem canvas, Vector2 at, float radius, int seed, bool magnet = true)
	{
		canvas.DrawCircle(at + new Vector2(radius * 0.08f, radius * 0.12f), radius * 1.02f, new Color(0.46f, 0.50f, 0.56f, 0.22f));
		canvas.DrawCircle(at, radius, Card);
		// The red rings, each gone round twice with the crayon so it is not one clean line.
		foreach ((float r, float width) in new[] { (0.80f, 0.20f), (0.40f, 0.18f) })
		{
			for (int pass = 0; pass < 2; pass++)
			{
				canvas.DrawPolyline(WobblyCircle(at, radius * r, radius * 0.03f, seed * 5 + pass * 3 + (int)(r * 10)), Red,
					radius * width * (pass == 0 ? 1.0f : 0.7f), true);
			}
		}
		canvas.DrawCircle(at, radius * 0.14f, Red);
		canvas.DrawPolyline(WobblyCircle(at, radius * 0.14f, radius * 0.02f, seed + 31), Red, radius * 0.08f, true);
		canvas.DrawPolyline(WobblyCircle(at, radius, radius * 0.035f, seed + 7), Ink, Mathf.Max(2.0f, radius * 0.09f), true);
		if (magnet) DrawMagnet(canvas, at + new Vector2(0.0f, -radius * 0.92f), radius * 0.3f, seed);
	}

	static void DrawMagnet(CanvasItem canvas, Vector2 at, float radius, int seed)
	{
		Color c = MagnetColours[Mathf.PosMod(seed, MagnetColours.Length)];
		canvas.DrawCircle(at + new Vector2(radius * 0.12f, radius * 0.2f), radius, new Color(0.46f, 0.50f, 0.56f, 0.25f));
		canvas.DrawCircle(at, radius, c);
		canvas.DrawCircle(at + new Vector2(-radius * 0.3f, -radius * 0.3f), radius * 0.28f, new Color(1.0f, 1.0f, 1.0f, 0.55f));
		canvas.DrawArc(at, radius, 0.0f, Mathf.Tau, 20, new Color(c.R * 0.6f, c.G * 0.6f, c.B * 0.6f), Mathf.Max(1.5f, radius * 0.16f));
	}

	/// <summary>A circle as a hand goes round it: a little off true all the way, and just past closed.</summary>
	static Vector2[] WobblyCircle(Vector2 at, float radius, float wobble, int seed)
	{
		const int Steps = 28;
		var points = new Vector2[Steps + 2];
		for (int i = 0; i < points.Length; i++)
		{
			float a = i / (float)Steps * Mathf.Tau + CrayonBrush.Noise(seed, 0) * 0.5f;
			float r = radius + CrayonBrush.Noise(seed, i % Steps + 1) * wobble;
			points[i] = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		return points;
	}

	/// <summary>
	/// A target breaking: a pale flash, the card in wedges flung out and falling, and its magnet
	/// dropping off the door. An effect, not a thing - so nothing in it is inked (art-direction.md).
	/// </summary>
	void DrawBurst(Burst burst)
	{
		float t = burst.Age / 60.0f;
		float fade = 1.0f - burst.Age / (float)BurstFrames;
		const float Gravity = 1500.0f;

		float flash = Mathf.Clamp(burst.Age / 8.0f, 0.0f, 1.0f);
		if (burst.Age < 12) DrawArc(burst.At, Radius * (1.0f + flash * 1.4f), 0.0f, Mathf.Tau, 32, new Color(1.0f, 0.95f, 0.70f, 1.0f - flash), 10.0f * (1.0f - flash) + 2.0f, true);

		const int Pieces = 8;
		for (int i = 0; i < Pieces; i++)
		{
			float angle = i * Mathf.Tau / Pieces + CrayonBrush.Noise(burst.Seed, i) * 0.4f;
			float speed = 380.0f + 160.0f * CrayonBrush.Noise(burst.Seed, i + 20);
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 at = burst.At + dir * (Radius * 0.4f + speed * t) + new Vector2(0.0f, 0.5f * Gravity * t * t - 200.0f * t);
			float spin = angle + t * 9.0f * (i % 2 == 0 ? 1.0f : -1.0f);
			float size = Radius * 0.5f;
			Vector2 a = at + new Vector2(Mathf.Cos(spin), Mathf.Sin(spin)) * size;
			Vector2 b = at + new Vector2(Mathf.Cos(spin + 2.3f), Mathf.Sin(spin + 2.3f)) * size * 0.6f;
			Vector2 c = at + new Vector2(Mathf.Cos(spin - 2.3f), Mathf.Sin(spin - 2.3f)) * size * 0.6f;
			Color colour = i % 2 == 0 ? Red : Card.Darkened(0.04f);
			DrawColoredPolygon(new[] { a, b, c }, new Color(colour, fade));
		}

		// The magnet lets go and drops.
		Color m = MagnetColours[Mathf.PosMod(burst.Seed, MagnetColours.Length)];
		Vector2 magnet = burst.At + new Vector2(0.0f, -Radius * 0.92f + 0.5f * Gravity * t * t);
		DrawCircle(magnet, Radius * 0.3f, new Color(m, fade));
	}

	// --- HUD ---------------------------------------------------------------------------------

	static readonly Color Paper = new Color(0.98f, 0.97f, 0.94f);
	static readonly Color Text = new Color(0.14f, 0.14f, 0.18f);
	static readonly Color Good = new Color(0.30f, 0.66f, 0.36f);

	/// <summary>
	/// Up top, the clock and a little target for each of the ten, ticked off as they go; at the end,
	/// the word for how it went, the time, and what the buttons do. Every word carries the thick
	/// paper outline every HUD label has, so it reads on any page.
	/// </summary>
	void DrawHud()
	{
		float width = hud.GetViewportRect().Size.X;
		Font font = ThemeDB.FallbackFont;

		Words(font, new Vector2(0.0f, 92.0f), width, TargetRecords.Clock(frames), 76, complete ? Good : Text);
		const float Gap = 46.0f;
		float row = (width - (broken.Length - 1) * Gap) * 0.5f;
		for (int i = 0; i < broken.Length; i++)
		{
			var at = new Vector2(row + i * Gap, 136.0f);
			if (broken[i]) hud.DrawArc(at, 15.0f, 0.0f, Mathf.Tau, 20, new Color(Ink, 0.35f), 2.0f, true);
			else DrawTarget(hud, at, 17.0f, i * 17 + 5, magnet: false);
		}

		if (!Over || overFrames < 12) return;
		// The word lands big and settles.
		float punch = Mathf.Max(0.0f, 1.0f - (overFrames - 12) / 14.0f);
		int size = Mathf.RoundToInt(130.0f * (1.0f + 0.3f * punch * punch));
		Words(font, new Vector2(0.0f, 320.0f), width, complete ? "Complete!" : "Try again!", size, complete ? Good : Red, 24);
		if (complete)
		{
			Words(font, new Vector2(0.0f, 420.0f), width, TargetRecords.Clock(frames), 76, Text);
			Words(font, new Vector2(0.0f, 482.0f), width, newBest ? "New best!" : $"Best {TargetRecords.Clock(best)}", 44,
				newBest ? MenuTheme.Accent : MenuTheme.Soft);
		}
		else if (best >= 0)
		{
			Words(font, new Vector2(0.0f, 420.0f), width, $"Best {TargetRecords.Clock(best)}", 44, MenuTheme.Soft);
		}
		if (overFrames >= OverSettleFrames)
		{
			Words(font, new Vector2(0.0f, 560.0f), width, "Press Start to go again", 44, Text);
			Words(font, new Vector2(0.0f, CardBottom), width, "B to pick a course", 34, MenuTheme.Soft);
		}
	}

	/// <summary>Where the words at the end of a run stop: the fighter is framed under them.</summary>
	const float CardBottom = 612.0f;

	void Words(Font font, Vector2 baseline, float width, string text, int size, Color colour, int outline = 14)
	{
		hud.DrawStringOutline(font, baseline, text, HorizontalAlignment.Center, width, size, outline, Paper);
		hud.DrawString(font, baseline, text, HorizontalAlignment.Center, width, size, colour);
	}
}
