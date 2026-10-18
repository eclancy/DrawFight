using System.Collections.Generic;
using Godot;

/// <summary>
/// The Big Show's speakers playing loud (Eric, 2026-10-18): every speaker cone thumps along with
/// the punk rock, and every so often one stack swells - its cones pushing out further and further
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

	/// <summary>The cones thump on the beat - the Big Show's music is 184 BPM, near enough twenty frames a beat.</summary>
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
		// Each stack's cones: a thump on every beat, and the one about to blow pushing out and
		// out, glowing, until it goes.
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
			DrawCones(stacks[s], push, glow);
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

	/// <summary>The cones of one stack, as Stage.DrawAmpStack lays them out, pushed out by <paramref name="push"/> (a fraction of their size).</summary>
	void DrawCones(Rect2 r, float push, float glow)
	{
		float height = (ground - r.End.Y) * 0.5f;
		for (int i = 0; i < 2; i++)
		{
			var box = new Rect2(r.Position.X + 8.0f, r.End.Y + i * height, r.Size.X - 16.0f, height);
			foreach (float fx in new[] { 0.3f, 0.7f })
			{
				var at = new Vector2(box.Position.X + box.Size.X * fx, box.GetCenter().Y);
				float radius = Mathf.Min(box.Size.X * 0.17f, box.Size.Y * 0.36f) * (1.0f + push);
				if (glow > 0.0f) DrawCircle(at, radius * 1.4f, new Color(1.0f, 0.74f, 0.30f, 0.65f * glow));
				DrawCircle(at, radius, new Color(0.62f, 0.63f, 0.69f));
				DrawArc(at, radius, 0.0f, Mathf.Tau, 24, ink, 2.5f, true);
				DrawCircle(at, radius * 0.32f * (1.0f + push * 1.5f), new Color(0.80f, 0.80f, 0.84f));
			}
		}
	}
}
