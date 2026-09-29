using System.Collections.Generic;
using Godot;

/// <summary>
/// Builds the collision for a <see cref="StageData"/> and draws it in that stage's style.
///
/// There is one renderer for every stage in the game and there should stay one. A new stage is
/// a palette and a list of rectangles in <see cref="StageCatalog"/>; if adding a stage ever
/// requires new drawing code, something has gone wrong with the data model rather than with the
/// stage. See .ai/art-direction.md.
/// </summary>
public partial class Stage : Node2D
{
	public StageData Data { get; private set; }

	public readonly List<Rect2> Platforms = new List<Rect2>();

	/// <summary>Drawn well past the blast zone so a zoomed-out camera never sees the backdrop end.</summary>
	const float BackdropPad = 900.0f;

	public Vector2[] SpawnPoints => Data.SpawnPoints;
	public Vector2 RespawnPoint => Data.RespawnPoint;
	public Rect2 BlastZone => Data.BlastZone;
	public bool IsOutOfBounds(Vector2 point) => Data.IsOutOfBounds(point);

	/// <summary>
	/// Grabbable corners: the top-left and top-right of every SOLID platform that sits inside
	/// the blast zone.
	///
	/// Soft platforms are excluded because you pass through them, and a corner outside the
	/// blast zone is excluded because it is not somewhere a fighter can be - which is how
	/// Open Plains ends up with no ledges at all without anything having to say so. Its floor
	/// runs past both blast zones, so both its corners fall outside and the stage simply has
	/// nothing to grab.
	/// </summary>
	public readonly List<Vector2> Ledges = new List<Vector2>();

	public void Build(StageData data)
	{
		Data = data;

		foreach (StagePlatform platform in data.Platforms)
		{
			Platforms.Add(platform.Rect);

			// Solid ground is layer 1, soft platforms are layer 2. Keeping them apart is what
			// lets a fighter drop through a soft platform by masking layer 2 off for a few
			// frames without also falling through the floor of the stage.
			var body = new StaticBody2D
			{
				Position = platform.Rect.Position + platform.Rect.Size * 0.5f,
				CollisionLayer = platform.OneWay ? 2u : 1u,
				CollisionMask = 0,
			};

			body.AddChild(new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = platform.Rect.Size },
				OneWayCollision = platform.OneWay,
			});

			AddChild(body);

			if (platform.OneWay) continue;

			foreach (Vector2 corner in new[]
			{
				new Vector2(platform.Rect.Position.X, platform.Rect.Position.Y),
				new Vector2(platform.Rect.End.X, platform.Rect.Position.Y),
			})
			{
				if (data.BlastZone.HasPoint(corner)) Ledges.Add(corner);
			}
		}

		QueueRedraw();
	}

	Rect2 Backdrop => Data.BlastZone.Grow(BackdropPad);

	public override void _Draw()
	{
		if (Data == null) return;

		CrayonBrush.SkyBands(this, Backdrop, Data.SkyTop, Data.SkyBottom);
		DrawPaperSubstrate();

		foreach (StageProp prop in Data.Props) DrawProp(prop);
		foreach (StagePlatform platform in Data.Platforms) DrawPlatform(platform);

		DrawBlastZoneHint();
	}

	/// <summary>
	/// The ruled page the whole world is drawn on. Only the exercise-book stages have it; the
	/// colouring-book ones are on plain paper, which is what makes a nature stage feel like a
	/// different kind of drawing rather than a recoloured one.
	/// </summary>
	void DrawPaperSubstrate()
	{
		if (Data.Style != StageStyle.ExerciseBook) return;

		Rect2 page = Backdrop;
		const float RuleSpacing = 74.0f;

		for (float y = page.Position.Y; y < page.End.Y; y += RuleSpacing)
		{
			CrayonBrush.InkLine(this,
				new Vector2(page.Position.X, y), new Vector2(page.End.X, y),
				Data.RuleLine, 2.0f, Mathf.RoundToInt(y), 1.2f);
		}

		float marginX = Data.BlastZone.Position.X - 210.0f;
		CrayonBrush.InkLine(this,
			new Vector2(marginX, page.Position.Y), new Vector2(marginX, page.End.Y),
			Data.MarginLine, 3.0f, 991, 2.0f);
	}

	// --- Platforms -----------------------------------------------------------

	void DrawPlatform(StagePlatform platform)
	{
		int seed = Mathf.RoundToInt(platform.Rect.Position.X * 7.0f + platform.Rect.Position.Y * 13.0f);

		switch (platform.Look)
		{
			case PlatformLook.Magnet: DrawMagnet(platform, seed); break;
			case PlatformLook.HouseRoof: DrawHouse(platform, seed); break;
			case PlatformLook.Building: DrawBuilding(platform, seed, tower: false); break;
			case PlatformLook.Tower: DrawBuilding(platform, seed, tower: true); break;
			case PlatformLook.TreeTop: DrawTreeTop(platform, seed); break;
			default: DrawLedge(platform, seed); break;
		}
	}

	/// <summary>
	/// Outline weight and dashing carry the rules of the stage: a solid outline means solid
	/// ground, a dashed one means you can drop through it. The mechanic becomes visible rather
	/// than something a player discovers by dying.
	/// </summary>
	void OutlinePlatform(Rect2 rect, bool oneWay, int seed)
	{
		CrayonBrush.InkRect(this, rect, Data.Ink, oneWay ? 3.0f : 4.5f, seed, 2.0f, dashed: oneWay);
	}

	void DrawLedge(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		DrawRect(r, Data.GroundFill);
		CrayonBrush.CrayonFill(this, r, Data.GroundCrayon, seed, 13.0f, 6.0f, 8.0f);
		OutlinePlatform(r, platform.OneWay, seed);
	}

	void DrawMagnet(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		DrawRect(r, Data.GroundFill);
		CrayonBrush.CrayonFill(this, r, Data.PlatformCrayon, seed, 9.0f, 7.0f, 8.0f);
		OutlinePlatform(r, platform.OneWay, seed);

		// A highlight along the top, so a magnet reads as a glossy object rather than a slab.
		CrayonBrush.InkLine(this,
			r.Position + new Vector2(10.0f, 5.0f),
			new Vector2(r.End.X - 10.0f, r.Position.Y + 5.0f),
			new Color(1.0f, 1.0f, 1.0f, 0.55f), 3.0f, seed + 3, 1.0f);
	}

	/// <summary>A house drawn down from its roof slab, which is the part you stand on.</summary>
	void DrawHouse(StagePlatform platform, int seed)
	{
		Rect2 roof = platform.Rect;
		float bodyTop = roof.End.Y;
		float bodyHeight = Mathf.Max(60.0f, Data.GroundLine - bodyTop);
		var body = new Rect2(roof.Position.X + 26.0f, bodyTop, roof.Size.X - 52.0f, bodyHeight);

		DrawRect(body, Data.PropFill);
		CrayonBrush.CrayonFill(this, body, Data.PropCrayon, seed + 1, 13.0f, 5.0f, 8.0f);
		CrayonBrush.InkRect(this, body, Data.Ink, 3.0f, seed + 1, 2.2f);

		if (body.Size.Y > 120.0f)
		{
			var window = new Rect2(body.Position.X + 26.0f, bodyTop + 34.0f, 54.0f, 46.0f);
			DrawRect(window, Data.Accent);
			CrayonBrush.InkRect(this, window, Data.Ink, 2.4f, seed + 3, 1.4f);
		}

		if (body.Size.Y > 90.0f)
		{
			var door = new Rect2(body.Position.X + body.Size.X * 0.44f, body.End.Y - 82.0f, 64.0f, 82.0f);
			DrawRect(door, Data.PropCrayon);
			CrayonBrush.InkRect(this, door, Data.Ink, 2.6f, seed + 2, 1.6f);
		}

		DrawRect(roof, Data.GroundFill);
		CrayonBrush.CrayonFill(this, roof, Data.PlatformCrayon, seed, 9.0f, 6.0f, 8.0f);
		OutlinePlatform(roof, platform.OneWay, seed);
	}

	/// <summary>
	/// A tree drawn down from the top of its leaves. The flat top of the canopy is the part you
	/// stand on, and it still gets the dashed outline, so "you can drop through this" reads the
	/// same as on every other stage.
	/// </summary>
	void DrawTreeTop(StagePlatform platform, int seed)
	{
		Rect2 top = platform.Rect;
		float centreX = top.Position.X + top.Size.X * 0.5f;
		var canopy = new Rect2(top.Position.X - 18.0f, top.Position.Y, top.Size.X + 36.0f, 150.0f);

		// The trunk first, so the leaves sit over it.
		float trunkTop = canopy.Position.Y + canopy.Size.Y * 0.7f;
		float trunkBottom = Mathf.Max(trunkTop + 40.0f, Data.GroundLine);
		var trunk = new Rect2(centreX - 20.0f, trunkTop, 40.0f, trunkBottom - trunkTop);
		DrawRect(trunk, Data.PropFill);
		CrayonBrush.CrayonFill(this, trunk, Data.PropCrayon, seed + 1, 9.0f, 5.0f, 6.0f);
		CrayonBrush.InkRect(this, trunk, Data.Ink, 3.0f, seed + 1, 1.6f);

		// The leaves: overlapping blobs whose tops meet the standing line, so the canopy is a
		// solid shape you could believe you are standing on, not a loop of scribble.
		float w = top.Size.X;
		float big = w * 0.26f;
		float small = w * 0.22f;
		var leaves = new Color(Data.GroundCrayon.R * 0.92f, Data.GroundCrayon.G * 0.95f, Data.GroundCrayon.B * 0.9f);
		DrawCircle(new Vector2(centreX - w * 0.30f, top.Position.Y + big), small, leaves);
		DrawCircle(new Vector2(centreX + w * 0.30f, top.Position.Y + big), small, leaves);
		DrawCircle(new Vector2(centreX, top.Position.Y + big), big, leaves);
		DrawCircle(new Vector2(centreX, top.Position.Y + big * 1.5f), big * 0.9f, leaves);
		DrawRect(new Rect2(top.Position.X, top.Position.Y, w, big * 0.8f), leaves);
		CrayonBrush.CrayonFill(this, new Rect2(top.Position.X + 10.0f, top.Position.Y + 8.0f, w - 20.0f, big * 1.6f),
			new Color(Data.GroundCrayon.R * 0.8f, Data.GroundCrayon.G * 0.88f, Data.GroundCrayon.B * 0.75f, 0.6f),
			seed + 2, 11.0f, 6.0f, 8.0f);
		OutlinePlatform(top, platform.OneWay, seed);
	}

	void DrawBuilding(StagePlatform platform, int seed, bool tower)
	{
		Rect2 r = platform.Rect;

		DrawRect(r, Data.PropFill);
		CrayonBrush.CrayonFill(this, r, Data.PropCrayon, seed, 15.0f, 5.0f, 9.0f);

		// Windows. At night a scattering of them are lit, and that is where all of this stage's
		// warmth comes from - the sky stays pale, and the windows do the work.
		const float Pitch = 78.0f;
		float inset = tower ? 30.0f : 44.0f;
		int columns = Mathf.Max(1, Mathf.FloorToInt((r.Size.X - inset * 2.0f) / Pitch));
		int rows = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(r.Size.Y, 1250.0f) / Pitch));

		for (int cx = 0; cx < columns; cx++)
		{
			for (int cy = 0; cy < rows; cy++)
			{
				var w = new Rect2(
					r.Position.X + inset + cx * Pitch,
					r.Position.Y + 54.0f + cy * Pitch,
					40.0f, 46.0f);
				if (w.End.Y > r.End.Y - 20.0f) continue;

				bool lit = Data.Night && CrayonBrush.Noise(seed + cx * 31, cy) > 0.1f;
				DrawRect(w, lit ? Data.Accent : Data.GroundFill);
				CrayonBrush.InkRect(this, w, Data.Ink, 2.0f, seed + cx * 13 + cy, 1.2f);
			}
		}

		OutlinePlatform(r, platform.OneWay, seed);

		// The roofline gets a heavier stroke than the sides, because that edge is the one
		// players actually land on and it has to read from across the room.
		CrayonBrush.InkLine(this,
			r.Position, new Vector2(r.End.X, r.Position.Y),
			Data.Ink, 6.0f, seed + 77, 2.2f);

		if (!tower) return;

		// An aerial, so the tall one is unmistakable.
		float midX = r.Position.X + r.Size.X * 0.5f;
		CrayonBrush.InkLine(this,
			new Vector2(midX, r.Position.Y), new Vector2(midX, r.Position.Y - 150.0f),
			Data.Ink, 4.0f, seed + 91, 2.4f);
		DrawCircle(new Vector2(midX, r.Position.Y - 158.0f), 13.0f, Data.Accent);
	}

	// --- Props ---------------------------------------------------------------

	void DrawProp(StageProp prop)
	{
		Rect2 r = prop.Rect;
		Vector2 centre = r.Position + r.Size * 0.5f;

		switch (prop.Kind)
		{
			case PropKind.Sun:
				DrawCircle(centre, r.Size.X * 0.5f, Data.Celestial);
				for (int i = 0; i < 12; i++)
				{
					float a = i / 12.0f * Mathf.Tau;
					var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					CrayonBrush.InkLine(this,
						centre + dir * (r.Size.X * 0.58f), centre + dir * (r.Size.X * 0.80f),
						Data.Celestial, 9.0f, prop.Seed + i, 2.0f);
				}
				break;

			case PropKind.Moon:
				DrawCircle(centre, r.Size.X * 0.5f, Data.Celestial);
				// Bitten out with the sky colour, which is how a crescent gets drawn on paper.
				DrawCircle(centre + new Vector2(r.Size.X * 0.30f, -r.Size.X * 0.16f),
					r.Size.X * 0.42f, Data.SkyTop);
				break;

			case PropKind.Star:
				CrayonBrush.InkLine(this,
					centre - new Vector2(0.0f, r.Size.Y), centre + new Vector2(0.0f, r.Size.Y),
					Data.Celestial, 5.0f, prop.Seed, 0.6f);
				CrayonBrush.InkLine(this,
					centre - new Vector2(r.Size.X, 0.0f), centre + new Vector2(r.Size.X, 0.0f),
					Data.Celestial, 5.0f, prop.Seed + 1, 0.6f);
				break;

			case PropKind.Cloud:
				CrayonBrush.Scribble(this, centre, r.Size, new Color(1.0f, 1.0f, 1.0f, 0.75f),
					prop.Seed, 13.0f, 7);
				break;

			case PropKind.Hill:
				// Flat wash with no outline at all: distance is read as "has no lines".
				CrayonBrush.CrayonFill(this, r,
					new Color(Data.GroundCrayon.R, Data.GroundCrayon.G, Data.GroundCrayon.B, 0.42f),
					prop.Seed, 17.0f, 8.0f, 13.0f);
				break;

			case PropKind.Bush:
				CrayonBrush.Scribble(this, centre, r.Size, Data.GroundCrayon, prop.Seed, 10.0f, 8);
				break;

			case PropKind.Tree:
				// A background tree: no outline, washed out, because distance reads as "has no
				// lines" - only the trees you can stand on are drawn with ink.
				var wash = new Color(Data.GroundCrayon.R, Data.GroundCrayon.G, Data.GroundCrayon.B, 0.5f);
				var bark = new Color(Data.PropCrayon.R, Data.PropCrayon.G, Data.PropCrayon.B, 0.5f);
				DrawRect(new Rect2(centre.X - r.Size.X * 0.08f, r.Position.Y + r.Size.Y * 0.45f,
					r.Size.X * 0.16f, r.Size.Y * 0.55f), bark);
				CrayonBrush.Scribble(this, new Vector2(centre.X, r.Position.Y + r.Size.Y * 0.32f),
					new Vector2(r.Size.X, r.Size.Y * 0.64f), wash, prop.Seed, 14.0f, 8);
				break;

			case PropKind.Grass:
				// Tufts along the ground: three short strokes each, leaning a little.
				for (float x = r.Position.X; x < r.End.X; x += 46.0f)
				{
					float jitter = CrayonBrush.Noise(prop.Seed, Mathf.RoundToInt(x)) * 14.0f;
					var root = new Vector2(x + jitter, r.End.Y);
					for (int blade = -1; blade <= 1; blade++)
					{
						CrayonBrush.InkLine(this, root,
							root + new Vector2(blade * 9.0f + 3.0f, -r.Size.Y * (blade == 0 ? 1.0f : 0.7f)),
							Data.GroundCrayon, 4.0f, prop.Seed + Mathf.RoundToInt(x) + blade, 0.8f);
					}
				}
				break;

			case PropKind.Fence:
				for (float x = r.Position.X; x < r.End.X; x += 62.0f)
				{
					CrayonBrush.InkLine(this,
						new Vector2(x, r.End.Y), new Vector2(x, r.Position.Y),
						Data.PropCrayon, 6.0f, prop.Seed + Mathf.RoundToInt(x), 1.6f);
				}
				CrayonBrush.InkLine(this,
					new Vector2(r.Position.X, r.Position.Y + 22.0f),
					new Vector2(r.End.X, r.Position.Y + 22.0f),
					Data.PropCrayon, 6.0f, prop.Seed + 3, 1.8f);
				break;

			case PropKind.TapedDrawing:
				// A sheet stuck to the door. This is where his non-fighter drawings go once
				// there are any; until then it is a blank page with a strip of tape on it.
				DrawRect(r, new Color(1.0f, 1.0f, 1.0f, 0.85f));
				CrayonBrush.InkRect(this, r, Data.RuleLine, 2.4f, prop.Seed, 2.6f);
				for (float y = r.Position.Y + 40.0f; y < r.End.Y - 20.0f; y += 44.0f)
				{
					CrayonBrush.InkLine(this,
						new Vector2(r.Position.X + 26.0f, y), new Vector2(r.End.X - 26.0f, y),
						Data.RuleLine, 2.0f, prop.Seed + Mathf.RoundToInt(y), 1.4f);
				}
				DrawRect(new Rect2(r.Position.X + r.Size.X * 0.5f - 34.0f, r.Position.Y - 14.0f,
					68.0f, 28.0f), new Color(0.96f, 0.93f, 0.72f, 0.80f));
				break;

			case PropKind.FridgeHandle:
				DrawRect(r, Data.PropFill);
				CrayonBrush.InkRect(this, r, Data.Ink, 4.0f, prop.Seed, 2.4f);
				break;
		}
	}

	// --- Debug ---------------------------------------------------------------

	void DrawBlastZoneHint()
	{
		Rect2 b = Data.BlastZone;
		var warn = new Color(0.82f, 0.26f, 0.30f, 0.30f);

		CrayonBrush.InkLine(this, b.Position, new Vector2(b.Position.X, b.End.Y), warn, 4.0f, 1, 3.0f);
		CrayonBrush.InkLine(this, new Vector2(b.End.X, b.Position.Y), b.End, warn, 4.0f, 2, 3.0f);
		CrayonBrush.InkLine(this, b.Position, new Vector2(b.End.X, b.Position.Y), warn, 4.0f, 3, 3.0f);

		// A stage with no bottom blast zone must not draw one: the missing line IS the
		// information that you cannot die downward here.
		if (Data.HasBottomBlastZone)
		{
			CrayonBrush.InkLine(this, new Vector2(b.Position.X, b.End.Y), b.End, warn, 4.0f, 4, 3.0f);
		}
	}
}
