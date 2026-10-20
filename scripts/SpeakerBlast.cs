using System.Collections.Generic;
using Godot;

/// <summary>
/// The Big Show's speakers playing loud (Eric, 2026-10-18): every speaker cone thumps along with
/// the guitar rock, and every so often one stack swells - its cones pushing out further and further
/// for a second, a warning anyone can see - and then blasts a ring of sound that knocks back
/// whoever is near it. It hurts a little; it is mostly a shove. Built by Stage for a stage whose
/// data has <see cref="StageData.Speakers"/>, drawn over the amp stacks it belongs to.
/// </summary>
public partial class SpeakerBlast : Node2D
{
	public MatchManager Match;

	readonly List<Rect2> stacks = new List<Rect2>();
	float ground;
	Color ink;
	readonly RandomNumberGenerator rng = new RandomNumberGenerator();

	/// <summary>
	/// The cones thump on the beat - the Big Show's music is 180 BPM, exactly twenty frames a beat,
	/// so the thump keeps the music's tempo exactly instead of sliding slowly off it, as it did at
	/// the old 184. Change one, change both
	/// (BIG_BPM in tools/audio/music.py).
	/// </summary>
	const int BeatFrames = 20;

	/// <summary>Frames between blasts, at random within this range: nine to fifteen seconds.</summary>
	const int MinGapFrames = 540;
	const int MaxGapFrames = 900;

	/// <summary>The swell before a blast: a second, long enough to get out of the way.</summary>
	const int SwellFrames = 60;

	/// <summary>The ring of sound spreading out, and how far it reaches.</summary>
	const int BlastFrames = 18;
	const float BlastReach = 430.0f;

	/// <summary>
	/// What the blast does: a shove sideways and up, a few percent - at 0% it pushes you a
	/// stage-length away, and only sends you out of the match from well over 100%.
	/// </summary>
	static readonly MoveData BlastHit = new MoveData
	{
		MoveName = "Speaker",
		Damage = 5.0f, BaseKnockback = 34.0f, KnockbackGrowth = 0.7f,
		LaunchAngleDegrees = 32.0f,
	};

	int frames;
	int nextBlast;
	int swelling = -1;
	int blastAge = -1;
	int blasting = -1;
	readonly HashSet<Fighter> hit = new HashSet<Fighter>();

	public void Setup(StageData data)
	{
		ground = data.GroundLine;
		ink = data.Ink;
		foreach (StagePlatform platform in data.Platforms)
		{
			if (platform.Look == PlatformLook.AmpStack) stacks.Add(platform.Rect);
		}
		rng.Randomize();
		nextBlast = rng.RandiRange(MinGapFrames, MaxGapFrames);
	}

	/// <summary>Where a stack's sound comes from: the middle of its two cabinets.</summary>
	Vector2 Mouth(Rect2 stack) => new Vector2(stack.GetCenter().X, (stack.End.Y + ground) * 0.5f);

	public override void _PhysicsProcess(double delta)
	{
		if (stacks.Count == 0) return;
		frames++;
		// Not before FIGHT!, and not while a story scene plays.
		bool scene = Match != null && (Match.InStoryScene || Match.InputLocked);

		if (blastAge >= 0)
		{
			blastAge++;
			HitFighters();
			if (blastAge > BlastFrames) blastAge = -1;
		}
		else if (swelling >= 0)
		{
			if (++swelling >= SwellFrames)
			{
				swelling = -1;
				blastAge = 0;
				hit.Clear();
				SfxPlayer.At("special_quake", Mouth(stacks[blasting]), 0.0f);
				Match?.Shake(24.0f);
			}
		}
		else if (!scene && --nextBlast <= 0)
		{
			blasting = rng.RandiRange(0, stacks.Count - 1);
			swelling = 0;
			nextBlast = rng.RandiRange(MinGapFrames, MaxGapFrames);
		}
		QueueRedraw();
	}

	void HitFighters()
	{
		if (Match == null || Match.InStoryScene) return;
		Vector2 from = Mouth(stacks[blasting]);
		float radius = BlastReach * blastAge / BlastFrames;
		foreach (Fighter fighter in Match.Fighters)
		{
			if (hit.Contains(fighter) || !fighter.CanBeHitByHazard) continue;
			Rect2 body = fighter.BodyRect();
			Vector2 nearest = new Vector2(Mathf.Clamp(from.X, body.Position.X, body.End.X), Mathf.Clamp(from.Y, body.Position.Y, body.End.Y));
			if (nearest.DistanceTo(from) > radius) continue;
			hit.Add(fighter);
			int away = fighter.GlobalPosition.X >= from.X ? 1 : -1;
			fighter.ReceiveStageHit(BlastHit, away, nearest);
		}
	}

	public override void _Draw()
	{
		// Each stack's cones: always buzzing a little, a thump on every beat, and the one about to
		// blow pushing out and out, glowing, until it goes.
		float beat = 1.0f - (frames % BeatFrames) / (float)BeatFrames;
		float thump = beat * beat * 0.08f;
		for (int s = 0; s < stacks.Count; s++)
		{
			float push = thump;
			float glow = 0.0f;
			if (s == blasting && swelling >= 0)
			{
				float t = swelling / (float)SwellFrames;
				push += 0.38f * t + 0.1f * Mathf.Sin(swelling * 0.9f) * t;
				glow = t;
			}
			DrawCones(this, stacks[s], ground, ink, push, glow, frames + s * 7, beat);
		}

		// The blast: rings of sound spreading out from the stack.
		if (blastAge >= 0 && blasting >= 0)
		{
			Vector2 from = Mouth(stacks[blasting]);
			float t = blastAge / (float)BlastFrames;
			for (int k = 0; k < 3; k++)
			{
				float r = BlastReach * Mathf.Clamp(t - k * 0.12f, 0.0f, 1.0f);
				if (r <= 0.0f) continue;
				float a = (1.0f - t) * (1.0f - k * 0.25f);
				DrawArc(from, r, 0.0f, Mathf.Tau, 64, new Color(1.0f, 0.74f, 0.30f, 0.85f * a), 18.0f - k * 4.0f, true);
				DrawArc(from, r, 0.0f, Mathf.Tau, 64, new Color(ink, 0.8f * a), 4.0f, true);
			}
		}
	}

	// --- How a speaker looks ---------------------------------------------------------------
	// Elim took the old grey stacks - two boxes of round windows, knobs on top - for ovens
	// (2026-10-19), with Flambe talking about cooking. So: one tall cabinet in dark cloth, a big
	// woofer under a small tweeter, each in a bright metal rim, metal corners, and an amp head
	// on top with a gold control panel. The cones never sit still. Dark slate, not black - the
	// fighters own black (.ai/art-direction.md).

	static readonly Color CabinetColour = new Color(0.33f, 0.34f, 0.42f);
	static readonly Color ClothColour = new Color(0.40f, 0.41f, 0.49f);
	static readonly Color Metal = new Color(0.80f, 0.81f, 0.85f);
	static readonly Color Rubber = new Color(0.36f, 0.37f, 0.45f);
	static readonly Color ConeColour = new Color(0.55f, 0.56f, 0.64f);
	static readonly Color DustCap = new Color(0.78f, 0.79f, 0.84f);

	/// <summary>The cabinet under an amp top <paramref name="r"/>, down to the ground.</summary>
	static Rect2 Cabinet(Rect2 r, float ground) => new Rect2(r.Position.X + 8.0f, r.End.Y, r.Size.X - 16.0f, ground - r.End.Y);

	/// <summary>Where the woofer and the tweeter sit in a cabinet, and how big they are.</summary>
	static (Vector2 woofer, float wooferR, Vector2 tweeter, float tweeterR) Cones(Rect2 box)
	{
		float wooferR = Mathf.Min(box.Size.X * 0.34f, box.Size.Y * 0.30f);
		var woofer = new Vector2(box.GetCenter().X, box.End.Y - box.Size.Y * 0.37f);
		var tweeter = new Vector2(box.GetCenter().X, box.Position.Y + box.Size.Y * 0.19f);
		return (woofer, wooferR, tweeter, wooferR * 0.28f);
	}

	/// <summary>The cabinet itself: the box, the cloth front with its weave, metal corners.</summary>
	public static void DrawCabinet(CanvasItem canvas, Rect2 r, float ground, Color ink, int seed)
	{
		Rect2 box = Cabinet(r, ground);
		canvas.DrawRect(box, CabinetColour);
		Rect2 cloth = box.Grow(-12.0f);
		canvas.DrawRect(cloth, ClothColour);
		// The weave of the grille cloth: fine crossed lines, so it reads as fabric, not a door.
		var weave = new Color(CabinetColour, 0.35f);
		for (float x = cloth.Position.X + 6.0f; x < cloth.End.X; x += 9.0f)
		{
			canvas.DrawLine(new Vector2(x, cloth.Position.Y), new Vector2(x, cloth.End.Y), weave, 1.0f);
		}
		for (float y = cloth.Position.Y + 6.0f; y < cloth.End.Y; y += 9.0f)
		{
			canvas.DrawLine(new Vector2(cloth.Position.X, y), new Vector2(cloth.End.X, y), weave, 1.0f);
		}
		CrayonBrush.InkRect(canvas, cloth, new Color(ink, 0.6f), 2.0f, seed + 9, 1.0f);
		// Metal corner protectors, as every road case and guitar cab has.
		foreach (Vector2 corner in new[] { box.Position, new Vector2(box.End.X, box.Position.Y), new Vector2(box.Position.X, box.End.Y), box.End })
		{
			var plate = new Rect2(corner.X - (corner.X > box.GetCenter().X ? 18.0f : 0.0f), corner.Y - (corner.Y > box.GetCenter().Y ? 18.0f : 0.0f), 18.0f, 18.0f);
			canvas.DrawRect(plate, Metal);
			canvas.DrawRect(plate, ink, false, 1.5f);
		}
		CrayonBrush.InkRect(canvas, box, ink, 2.5f, seed + 5, 1.2f);
	}

	/// <summary>The amp head on top - the platform: a gold panel of knobs and a light that says it is on.</summary>
	public static void DrawAmpHead(CanvasItem canvas, Rect2 r, Color ink, Color light)
	{
		canvas.DrawRect(r, CabinetColour);
		var panel = new Rect2(r.Position.X + 10.0f, r.Position.Y + 5.0f, r.Size.X - 20.0f, r.Size.Y - 10.0f);
		canvas.DrawRect(panel, new Color(0.88f, 0.76f, 0.44f));
		for (float x = panel.Position.X + 18.0f; x < panel.End.X - 34.0f; x += 22.0f)
		{
			var knob = new Vector2(x, panel.GetCenter().Y);
			canvas.DrawCircle(knob, 5.5f, new Color(0.96f, 0.94f, 0.88f));
			canvas.DrawArc(knob, 5.5f, 0.0f, Mathf.Tau, 12, ink, 1.5f, true);
			canvas.DrawLine(knob, knob + new Vector2(0.0f, -5.0f).Rotated(x * 0.37f), ink, 1.5f);
		}
		canvas.DrawCircle(new Vector2(panel.End.X - 14.0f, panel.GetCenter().Y), 5.0f, light);
	}

	/// <summary>
	/// A stack's two cones - the big woofer and the small tweeter - pushed out by
	/// <paramref name="push"/> (a fraction of their size), buzzing with <paramref name="frame"/>,
	/// and on the beat (<paramref name="beat"/>, 1 on it and falling) sending out little arcs of sound.
	/// </summary>
	public static void DrawCones(CanvasItem canvas, Rect2 r, float ground, Color ink, float push, float glow, int frame, float beat)
	{
		Rect2 box = Cabinet(r, ground);
		(Vector2 woofer, float wooferR, Vector2 tweeter, float tweeterR) = Cones(box);
		// Never quite still: a fast buzz on top of the beat, as a speaker playing loud is.
		float buzz = frame == 0 ? 0.0f : 0.022f * Mathf.Sin(frame * 1.9f) + 0.012f * Mathf.Sin(frame * 3.1f + 1.0f);
		DrawCone(canvas, woofer, wooferR, push + buzz, glow, ink);
		DrawCone(canvas, tweeter, tweeterR, push * 0.6f - buzz, glow, ink);

		// The sound coming out, on the beat: a couple of arcs either side of the woofer.
		if (beat > 0.35f)
		{
			float a = (beat - 0.35f) / 0.65f;
			for (int k = 0; k < 2; k++)
			{
				float rr = wooferR * (1.25f + 0.22f * k + 0.25f * (1.0f - a));
				var colour = new Color(ink, 0.45f * a * (1.0f - 0.35f * k));
				canvas.DrawArc(woofer, rr, -0.55f, 0.55f, 10, colour, 3.0f, true);
				canvas.DrawArc(woofer, rr, Mathf.Pi - 0.55f, Mathf.Pi + 0.55f, 10, colour, 3.0f, true);
			}
		}
	}

	/// <summary>
	/// One cone, from the front: the metal rim, the rubber surround, the cone - lighter toward the
	/// middle - and the dust cap with its shine. The rim stays put; the cone and cap move with
	/// <paramref name="push"/>.
	/// </summary>
	static void DrawCone(CanvasItem canvas, Vector2 at, float radius, float push, float glow, Color ink)
	{
		float rim = radius * 1.12f;
		if (glow > 0.0f) canvas.DrawCircle(at, rim * 1.3f, new Color(1.0f, 0.74f, 0.30f, 0.65f * glow));
		canvas.DrawCircle(at, rim, Metal);
		canvas.DrawArc(at, rim, 0.0f, Mathf.Tau, 32, ink, 2.5f, true);
		canvas.DrawCircle(at, radius, Rubber);
		float cone = radius * 0.86f * (1.0f + push);
		canvas.DrawCircle(at, cone, ConeColour);
		canvas.DrawCircle(at, cone * 0.72f, ConeColour.Lightened(0.08f));
		canvas.DrawArc(at, cone * 0.72f, 0.0f, Mathf.Tau, 28, new Color(ink, 0.25f), 1.5f, true);
		canvas.DrawCircle(at, cone * 0.45f, ConeColour.Lightened(0.16f));
		canvas.DrawArc(at, cone, 0.0f, Mathf.Tau, 28, new Color(ink, 0.55f), 2.0f, true);
		float cap = radius * 0.30f * (1.0f + push * 1.5f);
		canvas.DrawCircle(at, cap, DustCap);
		canvas.DrawArc(at, cap, 0.0f, Mathf.Tau, 20, new Color(ink, 0.6f), 1.5f, true);
		canvas.DrawCircle(at + new Vector2(-cap * 0.35f, -cap * 0.35f), cap * 0.3f, new Color(1, 1, 1, 0.7f));
	}
}
