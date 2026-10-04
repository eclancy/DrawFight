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

	/// <summary>The cars, on a stage with traffic. MatchManager hands it the match so it can hit people.</summary>
	public StreetTraffic Traffic { get; private set; }

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

			if (data.Traffic && platform.Look == PlatformLook.Road && Traffic == null)
			{
				Traffic = new StreetTraffic();
				AddChild(Traffic);
				Traffic.Setup(platform.Rect.Position.Y, data);
			}

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
		if (Data.Style == StageStyle.TapedPage || Data.Style == StageStyle.PinnedPages) DrawBrushedMetal();
		DrawPaperSubstrate();

		foreach (StageProp prop in Data.Props) DrawProp(prop);
		if (Data.Style == StageStyle.TapedPage) DrawTapedPage(Data.PageRect);
		if (Data.Style == StageStyle.PinnedPages) foreach (PinnedPage page in Data.Pages) DrawPinnedPage(page);
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

	/// <summary>
	/// A steel door in coloured pencil: long vertical strokes for the brushed grain, a light
	/// pencil hatch over everything, two broad pale sheens where the light catches it, and the
	/// rubber seal down each edge of the door far out past the fight.
	/// </summary>
	void DrawBrushedMetal()
	{
		Rect2 door = Backdrop;

		for (float x = door.Position.X; x < door.End.X; x += 13.0f)
		{
			int i = Mathf.RoundToInt(x);
			float shade = CrayonBrush.Noise(41, i);
			Color grain = shade > 0.0f
				? new Color(1.0f, 1.0f, 1.0f, 0.10f + 0.12f * shade)
				: new Color(0.58f, 0.62f, 0.68f, 0.06f - 0.10f * shade);
			CrayonBrush.InkLine(this, new Vector2(x, door.Position.Y), new Vector2(x, door.End.Y), grain, 2.0f, i, 1.4f);
		}

		CrayonBrush.PencilFill(this, door, new Color(0.64f, 0.68f, 0.74f, 0.22f), 57, 10.0f, 2.0f);

		foreach (float centre in new[] { -520.0f, 360.0f })
		{
			var sheen = new[]
			{
				new Vector2(centre - 160.0f, door.Position.Y), new Vector2(centre + 60.0f, door.Position.Y),
				new Vector2(centre + 60.0f - 900.0f, door.End.Y), new Vector2(centre - 160.0f - 900.0f, door.End.Y),
			};
			DrawColoredPolygon(sheen, new Color(1.0f, 1.0f, 1.0f, 0.16f));
		}

		var seal = new Color(0.56f, 0.58f, 0.63f);
		foreach (float x in new[] { Data.BlastZone.Position.X - 140.0f, Data.BlastZone.End.X + 140.0f })
		{
			CrayonBrush.InkLine(this, new Vector2(x, door.Position.Y), new Vector2(x, door.End.Y), seal, 7.0f, 301, 1.6f);
		}
	}

	/// <summary>
	/// The sheet the stage is drawn on: ruled paper with a margin, a soft shadow lifting it off
	/// the door, and a strip of tape across each corner.
	/// </summary>
	void DrawTapedPage(Rect2 page)
	{
		DrawRect(new Rect2(page.Position + new Vector2(12.0f, 14.0f), page.Size), new Color(0.46f, 0.50f, 0.56f, 0.22f));
		DrawRect(page, new Color(0.985f, 0.978f, 0.955f));

		for (float y = page.Position.Y + 90.0f; y < page.End.Y - 20.0f; y += 74.0f)
		{
			CrayonBrush.InkLine(this, new Vector2(page.Position.X + 8.0f, y), new Vector2(page.End.X - 8.0f, y),
				Data.RuleLine, 2.0f, Mathf.RoundToInt(y), 1.2f);
		}
		float margin = page.Position.X + 130.0f;
		CrayonBrush.InkLine(this, new Vector2(margin, page.Position.Y + 6.0f), new Vector2(margin, page.End.Y - 6.0f),
			Data.MarginLine, 3.0f, 991, 2.0f);
		CrayonBrush.PencilRect(this, page, new Color(0.72f, 0.74f, 0.78f), 2.0f, 993);

		int seed = 0;
		foreach (Vector2 corner in new[]
		{
			page.Position, new Vector2(page.End.X, page.Position.Y),
			page.End, new Vector2(page.Position.X, page.End.Y),
		})
		{
			// Each strip runs diagonally across its corner, pointing out of the page.
			Vector2 outward = (corner - page.GetCenter()).Normalized();
			float angle = outward.Angle() + Mathf.Pi * 0.5f;
			DrawTape(corner, angle, 170.0f, 54.0f, 700 + seed++);
		}
	}

	/// <summary>
	/// One sheet held up by magnets: a soft shadow lifting it off the door, the paper - ruled with
	/// a margin, squared, or plain - a pencil edge, all turned a degree or two, then its magnets
	/// along the top edge, which sit square on the door.
	/// </summary>
	void DrawPinnedPage(PinnedPage page)
	{
		Vector2 centre = page.Rect.GetCenter();
		Vector2 half = page.Rect.Size * 0.5f;
		float angle = Mathf.DegToRad(page.Angle);
		var local = new Rect2(-half, page.Rect.Size);

		DrawSetTransformMatrix(new Transform2D(angle, centre + new Vector2(10.0f, 12.0f)));
		DrawRect(local, new Color(0.46f, 0.50f, 0.56f, 0.24f));
		DrawSetTransformMatrix(new Transform2D(angle, centre));
		DrawRect(local, new Color(0.985f, 0.978f, 0.955f));

		switch (page.Paper)
		{
			case PaperKind.Ruled:
				for (float y = local.Position.Y + 64.0f; y < local.End.Y - 16.0f; y += 52.0f)
				{
					CrayonBrush.InkLine(this, new Vector2(local.Position.X + 6.0f, y), new Vector2(local.End.X - 6.0f, y),
						Data.RuleLine, 2.0f, page.Seed * 31 + Mathf.RoundToInt(y), 1.2f);
				}
				CrayonBrush.InkLine(this, new Vector2(local.Position.X + 70.0f, local.Position.Y + 4.0f),
					new Vector2(local.Position.X + 70.0f, local.End.Y - 4.0f), Data.MarginLine, 3.0f, page.Seed * 7 + 991, 2.0f);
				break;

			case PaperKind.Grid:
				var grid = new Color(Data.RuleLine.R, Data.RuleLine.G, Data.RuleLine.B, 0.75f);
				for (float x = local.Position.X + 36.0f; x < local.End.X; x += 36.0f)
				{
					CrayonBrush.InkLine(this, new Vector2(x, local.Position.Y + 4.0f), new Vector2(x, local.End.Y - 4.0f),
						grid, 1.5f, page.Seed * 13 + Mathf.RoundToInt(x), 0.8f);
				}
				for (float y = local.Position.Y + 36.0f; y < local.End.Y; y += 36.0f)
				{
					CrayonBrush.InkLine(this, new Vector2(local.Position.X + 4.0f, y), new Vector2(local.End.X - 4.0f, y),
						grid, 1.5f, page.Seed * 17 + Mathf.RoundToInt(y), 0.8f);
				}
				break;
		}
		CrayonBrush.PencilRect(this, local, new Color(0.72f, 0.74f, 0.78f), 2.0f, page.Seed * 5 + 993);
		DrawSetTransformMatrix(Transform2D.Identity);

		// The magnets: on the top edge, where the page actually hangs from.
		var pageToWorld = new Transform2D(angle, centre);
		float[] spots = page.Magnets >= 2 ? new[] { 0.16f, 0.84f } : new[] { 0.5f };
		for (int i = 0; i < spots.Length; i++)
		{
			Vector2 at = pageToWorld * new Vector2(local.Position.X + local.Size.X * spots[i], local.Position.Y + 18.0f);
			DrawFridgeMagnet(at, 26.0f, page.Seed * 3 + i);
		}
	}

	/// <summary>A round fridge magnet in one of four bright colours, picked by its seed.</summary>
	void DrawFridgeMagnet(Vector2 centre, float radius, int seed)
	{
		Color[] colours =
		{
			new Color(0.93f, 0.38f, 0.36f), new Color(0.36f, 0.62f, 0.93f),
			new Color(0.44f, 0.78f, 0.44f), new Color(0.98f, 0.80f, 0.30f),
		};
		Color c = colours[Mathf.PosMod(seed, colours.Length)];
		DrawCircle(centre + new Vector2(3.0f, 5.0f), radius, new Color(0.46f, 0.50f, 0.56f, 0.25f));
		DrawCircle(centre, radius, c);
		DrawCircle(centre + new Vector2(-radius * 0.3f, -radius * 0.3f), radius * 0.28f, new Color(1.0f, 1.0f, 1.0f, 0.55f));
		DrawArc(centre, radius, 0.0f, Mathf.Tau, 28, new Color(c.R * 0.6f, c.G * 0.6f, c.B * 0.6f), 3.0f);
	}

	/// <summary>A strip of sticky tape: pale, see-through, with torn ends.</summary>
	void DrawTape(Vector2 centre, float angle, float length, float width, int seed)
	{
		Vector2 along = Vector2.Right.Rotated(angle) * length * 0.5f;
		Vector2 across = Vector2.Down.Rotated(angle) * width * 0.5f;
		var points = new List<Vector2>();
		const int Teeth = 5;
		for (int i = 0; i <= Teeth; i++)
		{
			float t = i / (float)Teeth * 2.0f - 1.0f;
			points.Add(centre - along + across * t + along.Normalized() * CrayonBrush.Noise(seed, i) * 5.0f);
		}
		for (int i = 0; i <= Teeth; i++)
		{
			float t = 1.0f - i / (float)Teeth * 2.0f;
			points.Add(centre + along + across * t + along.Normalized() * CrayonBrush.Noise(seed + 1, i) * 5.0f);
		}
		DrawColoredPolygon(points.ToArray(), new Color(0.96f, 0.93f, 0.74f, 0.62f));
		var edge = new Color(0.80f, 0.76f, 0.56f, 0.6f);
		CrayonBrush.InkLine(this, centre - along - across, centre + along - across, edge, 2.0f, seed + 2, 1.0f);
		CrayonBrush.InkLine(this, centre - along + across, centre + along + across, edge, 2.0f, seed + 3, 1.0f);
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
			case PlatformLook.PencilLedge: DrawPencilLedge(platform, seed); break;
			case PlatformLook.Road: DrawRoad(platform, seed); break;
			case PlatformLook.CraneJib: DrawCrane(platform, seed); break;
			case PlatformLook.HangingBeam: DrawHangingBeam(platform, seed); break;
			case PlatformLook.Scaffold: DrawScaffold(platform, seed); break;
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

	/// <summary>
	/// A slab coloured in with pencil on the page. Solid ground and soft platforms get different
	/// colours as well as the usual solid or dashed outline, and solid ground a heavier line
	/// along the top - the edge a player lands on.
	/// </summary>
	void DrawPencilLedge(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		Color colour = platform.OneWay ? Data.PlatformCrayon : Data.GroundCrayon;
		CrayonBrush.PencilFill(this, r, colour, seed, 4.5f, 2.4f);
		CrayonBrush.PencilRect(this, r, Data.Ink, platform.OneWay ? 3.0f : 4.0f, seed, dashed: platform.OneWay);
		if (!platform.OneWay)
		{
			CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 5.0f, seed + 77, 1.6f);
		}
	}

	/// <summary>
	/// A street seen from the side. The top of the rectangle is the road surface - what fighters
	/// and cars stand on - with the pavement drawn BEHIND it, as a pale concrete band running
	/// along the foot of the buildings above the road line. Below the surface, asphalt with a
	/// dashed yellow centre line. Mid-grey at the darkest: asphalt drawn in crayon, not black.
	/// </summary>
	void DrawRoad(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		const float Pavement = 46.0f;
		var pavement = new Rect2(r.Position.X, r.Position.Y - Pavement, r.Size.X, Pavement);

		DrawRect(pavement, Data.PropFill);
		CrayonBrush.CrayonFill(this, pavement, Data.PropCrayon, seed, 13.0f, 0.0f, 6.0f);
		// Paving slab joints, and the kerb edge where the pavement meets the road.
		for (float x = r.Position.X; x < r.End.X; x += 90.0f)
		{
			CrayonBrush.InkLine(this, new Vector2(x, pavement.Position.Y + 4.0f), new Vector2(x - 12.0f, pavement.End.Y - 8.0f),
				new Color(Data.PropCrayon.R * 0.9f, Data.PropCrayon.G * 0.9f, Data.PropCrayon.B * 0.9f), 2.0f, seed + Mathf.RoundToInt(x), 0.8f);
		}
		CrayonBrush.InkLine(this, new Vector2(r.Position.X, pavement.End.Y - 8.0f), new Vector2(r.End.X, pavement.End.Y - 8.0f),
			Data.PropCrayon, 5.0f, seed + 5, 1.0f);

		DrawRect(r, new Color(0.62f, 0.63f, 0.68f));
		CrayonBrush.CrayonFill(this, r, new Color(0.54f, 0.55f, 0.61f, 0.7f), seed + 1, 15.0f, 0.0f, 8.0f);
		for (float x = r.Position.X + 40.0f; x < r.End.X; x += 150.0f)
		{
			float y = r.Position.Y + 62.0f;
			CrayonBrush.InkLine(this, new Vector2(x, y), new Vector2(x + 80.0f, y), Data.Accent, 8.0f, seed + Mathf.RoundToInt(x), 1.2f);
		}

		// The road surface is solid ground, so it gets the solid line.
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 4.5f, seed + 9, 1.4f);
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

		// Windows, in a grid centred on the building so both sides match, stopping above the
		// door. At night a scattering of them are lit, and that is where the warmth comes from -
		// the sky stays pale, and the windows do the work.
		const float Pitch = 78.0f;
		const float WindowW = 40.0f;
		const float WindowH = 46.0f;
		float minInset = tower ? 26.0f : 36.0f;
		int columns = Mathf.Max(1, Mathf.FloorToInt((r.Size.X - minInset * 2.0f + (Pitch - WindowW)) / Pitch));
		float gridLeft = r.Position.X + (r.Size.X - (columns * Pitch - (Pitch - WindowW))) * 0.5f;

		// A door at the foot of every building that reaches the ground, sitting on the pavement.
		bool onGround = r.End.Y >= Data.GroundLine - 1.0f;
		const float Pavement = 46.0f;
		float doorH = tower ? 110.0f : 128.0f;
		float lowestWindow = onGround ? r.End.Y - Pavement - doorH - 30.0f : r.End.Y - 20.0f;
		int rows = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(lowestWindow - r.Position.Y, 1250.0f) - 54.0f + (Pitch - WindowH)) / Pitch));

		for (int cx = 0; cx < columns; cx++)
		{
			for (int cy = 0; cy < rows; cy++)
			{
				var w = new Rect2(
					gridLeft + cx * Pitch,
					r.Position.Y + 54.0f + cy * Pitch,
					WindowW, WindowH);
				if (w.End.Y > lowestWindow) continue;

				bool lit = Data.Night && CrayonBrush.Noise(seed + cx * 31, cy) > 0.1f;
				DrawRect(w, lit ? Data.Accent : Data.GroundFill);
				CrayonBrush.InkRect(this, w, Data.Ink, 2.0f, seed + cx * 13 + cy, 1.2f);
			}
		}

		if (onGround) DrawDoor(r, doorH, tower ? 70.0f : 96.0f, Pavement, seed);

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

	/// <summary>A pair of glass doors at the foot of a building, under a coloured awning.</summary>
	void DrawDoor(Rect2 building, float height, float width, float pavement, int seed)
	{
		float bottom = building.End.Y - pavement + 4.0f;
		var door = new Rect2(building.GetCenter().X - width * 0.5f, bottom - height, width, height);
		var frame = new Color(Data.PropCrayon.R * 0.85f, Data.PropCrayon.G * 0.85f, Data.PropCrayon.B * 0.85f);

		DrawRect(door, frame);
		float pane = (width - 18.0f) * 0.5f;
		DrawRect(new Rect2(door.Position.X + 6.0f, door.Position.Y + 8.0f, pane, height - 12.0f), Data.GroundFill);
		DrawRect(new Rect2(door.End.X - 6.0f - pane, door.Position.Y + 8.0f, pane, height - 12.0f), Data.GroundFill);
		DrawCircle(new Vector2(door.GetCenter().X - 7.0f, door.GetCenter().Y + 6.0f), 3.5f, Data.Ink);
		DrawCircle(new Vector2(door.GetCenter().X + 7.0f, door.GetCenter().Y + 6.0f), 3.5f, Data.Ink);
		CrayonBrush.InkRect(this, door, Data.Ink, 3.0f, seed + 41, 1.2f);

		// An awning over it, in the stage's accent colour.
		var awning = new Rect2(door.Position.X - 14.0f, door.Position.Y - 20.0f, width + 28.0f, 20.0f);
		DrawRect(awning, Data.MarginLine);
		CrayonBrush.InkRect(this, awning, Data.Ink, 2.6f, seed + 43, 1.0f);
	}

	static readonly Color CraneYellow = new Color(0.98f, 0.78f, 0.24f);

	/// <summary>
	/// A tower crane. The rectangle is the jib, the arm you stand on, drawn as a yellow lattice
	/// girder; the mast comes down from under its middle to the ground, with the driver's cab at
	/// the top, a concrete counterweight on the back end and a hook on the front.
	/// </summary>
	void DrawCrane(StagePlatform platform, int seed)
	{
		Rect2 jib = platform.Rect;
		float mastX = jib.GetCenter().X;
		const float MastW = 46.0f;
		float ground = Data.GroundLine - 6.0f;

		// The mast: two rails and a zigzag of bracing between them, all the way down.
		var mast = new Rect2(mastX - MastW * 0.5f, jib.End.Y, MastW, ground - jib.End.Y);
		DrawRect(mast, new Color(CraneYellow.R, CraneYellow.G, CraneYellow.B, 0.35f));
		CrayonBrush.InkLine(this, new Vector2(mast.Position.X, mast.Position.Y), new Vector2(mast.Position.X, mast.End.Y), Data.Ink, 4.0f, seed + 1, 1.2f);
		CrayonBrush.InkLine(this, new Vector2(mast.End.X, mast.Position.Y), new Vector2(mast.End.X, mast.End.Y), Data.Ink, 4.0f, seed + 2, 1.2f);
		int i = 0;
		for (float y = mast.Position.Y; y < mast.End.Y - 20.0f; y += MastW, i++)
		{
			float x0 = i % 2 == 0 ? mast.Position.X : mast.End.X;
			float x1 = i % 2 == 0 ? mast.End.X : mast.Position.X;
			CrayonBrush.InkLine(this, new Vector2(x0, y), new Vector2(x1, Mathf.Min(y + MastW, mast.End.Y)), CraneYellow, 5.0f, seed + 10 + i, 0.8f);
		}

		// The peak above the mast, with tie lines out to both ends of the jib.
		var peak = new Vector2(mastX, jib.Position.Y - 110.0f);
		CrayonBrush.InkLine(this, new Vector2(mastX - 18.0f, jib.Position.Y), peak, Data.Ink, 4.0f, seed + 3, 1.0f);
		CrayonBrush.InkLine(this, new Vector2(mastX + 18.0f, jib.Position.Y), peak, Data.Ink, 4.0f, seed + 4, 1.0f);
		CrayonBrush.InkLine(this, peak, new Vector2(jib.Position.X + 14.0f, jib.Position.Y), Data.Ink, 2.0f, seed + 5, 0.6f);
		CrayonBrush.InkLine(this, peak, new Vector2(jib.End.X - 14.0f, jib.Position.Y), Data.Ink, 2.0f, seed + 6, 0.6f);

		// The counterweight on the back end, and the cab under the jib by the mast.
		var weight = new Rect2(jib.Position.X + 12.0f, jib.End.Y, 64.0f, 54.0f);
		DrawRect(weight, Data.PropFill);
		CrayonBrush.CrayonFill(this, weight, Data.PropCrayon, seed + 7, 10.0f, 0.0f, 6.0f);
		CrayonBrush.InkRect(this, weight, Data.Ink, 3.0f, seed + 7, 1.0f);
		var cab = new Rect2(mastX + MastW * 0.5f, jib.End.Y, 54.0f, 46.0f);
		DrawRect(cab, CraneYellow);
		DrawRect(new Rect2(cab.Position.X + 10.0f, cab.Position.Y + 8.0f, 34.0f, 20.0f), Data.GroundFill);
		CrayonBrush.InkRect(this, cab, Data.Ink, 3.0f, seed + 8, 1.0f);

		// The hook, on its trolley near the front end.
		float hookX = jib.End.X - 50.0f;
		CrayonBrush.InkLine(this, new Vector2(hookX, jib.End.Y), new Vector2(hookX, jib.End.Y + 80.0f), Data.Ink, 2.5f, seed + 9, 0.4f);
		DrawArc(new Vector2(hookX + 7.0f, jib.End.Y + 88.0f), 9.0f, 0.0f, Mathf.Pi * 1.3f, 12, Data.Ink, 4.0f);

		// The jib itself: a lattice girder - top and bottom chords and a zigzag between.
		DrawRect(jib, new Color(CraneYellow.R, CraneYellow.G, CraneYellow.B, 0.4f));
		int j = 0;
		for (float x = jib.Position.X; x < jib.End.X - 10.0f; x += jib.Size.Y * 1.2f, j++)
		{
			float y0 = j % 2 == 0 ? jib.Position.Y : jib.End.Y;
			float y1 = j % 2 == 0 ? jib.End.Y : jib.Position.Y;
			CrayonBrush.InkLine(this, new Vector2(x, y0), new Vector2(Mathf.Min(x + jib.Size.Y * 1.2f, jib.End.X), y1), CraneYellow, 5.0f, seed + 30 + j, 0.6f);
		}
		OutlinePlatform(jib, platform.OneWay, seed);
	}

	/// <summary>A red steel beam hanging level on two cables from the crane above it.</summary>
	void DrawHangingBeam(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		var hook = new Vector2(r.GetCenter().X, r.Position.Y - StageData.CraneDrop + 110.0f);
		CrayonBrush.InkLine(this, new Vector2(r.Position.X + 20.0f, r.Position.Y), hook, Data.Ink, 2.5f, seed + 1, 0.5f);
		CrayonBrush.InkLine(this, new Vector2(r.End.X - 20.0f, r.Position.Y), hook, Data.Ink, 2.5f, seed + 2, 0.5f);

		var beam = new Color(0.90f, 0.40f, 0.34f);
		DrawRect(r, beam);
		DrawRect(new Rect2(r.Position.X, r.Position.Y + r.Size.Y * 0.3f, r.Size.X, r.Size.Y * 0.4f), new Color(0.80f, 0.34f, 0.30f));
		for (float x = r.Position.X + 22.0f; x < r.End.X - 10.0f; x += 36.0f)
		{
			DrawCircle(new Vector2(x, r.GetCenter().Y), 3.5f, new Color(0.98f, 0.94f, 0.88f));
		}
		OutlinePlatform(r, platform.OneWay, seed);
	}

	/// <summary>
	/// A scaffolding plank: wooden boards on steel poles, with the poles and cross-braces drawn
	/// down to the ground. Stack several at different heights for a scaffold you can climb.
	/// </summary>
	void DrawScaffold(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		float ground = Data.GroundLine - 6.0f;
		var pole = new Color(0.56f, 0.60f, 0.68f);

		foreach (float x in new[] { r.Position.X + 8.0f, r.End.X - 8.0f })
		{
			CrayonBrush.InkLine(this, new Vector2(x, r.Position.Y - 30.0f), new Vector2(x, ground), pole, 6.0f, seed + Mathf.RoundToInt(x), 0.8f);
		}
		// A cross-brace from this plank down to the next level.
		CrayonBrush.InkLine(this, new Vector2(r.Position.X + 8.0f, r.End.Y), new Vector2(r.End.X - 8.0f, Mathf.Min(r.End.Y + 150.0f, ground)), pole, 4.0f, seed + 3, 0.6f);
		// A guard rail above the boards.
		CrayonBrush.InkLine(this, new Vector2(r.Position.X + 8.0f, r.Position.Y - 26.0f), new Vector2(r.End.X - 8.0f, r.Position.Y - 26.0f), pole, 4.0f, seed + 4, 0.6f);

		var wood = new Color(0.86f, 0.66f, 0.42f);
		DrawRect(r, wood);
		CrayonBrush.CrayonFill(this, r, new Color(0.76f, 0.54f, 0.32f, 0.6f), seed, 9.0f, 0.0f, 5.0f);
		OutlinePlatform(r, platform.OneWay, seed);
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
				// Bitten out with the sky colour at that height, which is how a crescent gets drawn
				// on paper - the sky is a gradient, so the top colour alone shows as a disc.
				float skyT = Mathf.Clamp((centre.Y - Backdrop.Position.Y) / Backdrop.Size.Y, 0.0f, 1.0f);
				DrawCircle(centre + new Vector2(r.Size.X * 0.30f, -r.Size.X * 0.16f),
					r.Size.X * 0.42f, Data.SkyTop.Lerp(Data.SkyBottom, skyT));
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
				// A chrome bar: pale, pencil-shaded down one side, a bright streak down the other.
				DrawRect(r, new Color(0.90f, 0.91f, 0.93f));
				CrayonBrush.PencilFill(this, new Rect2(r.Position.X + r.Size.X * 0.55f, r.Position.Y, r.Size.X * 0.45f, r.Size.Y),
					new Color(0.55f, 0.58f, 0.64f, 0.8f), prop.Seed, 4.0f, 2.0f);
				DrawRect(new Rect2(r.Position.X + r.Size.X * 0.18f, r.Position.Y + 20.0f, r.Size.X * 0.14f, r.Size.Y - 40.0f),
					new Color(1.0f, 1.0f, 1.0f, 0.7f));
				CrayonBrush.PencilRect(this, r, new Color(0.46f, 0.49f, 0.55f), 3.0f, prop.Seed);
				break;

			case PropKind.Skyline:
			{
				// The city far away behind the fight: flat blocks a shade darker than the sky, no
				// outlines (distance reads as "has no lines"), a scattering of lit windows. Heights
				// are hashed from the seed, so the skyline is the same every time.
				// Hazy: the sky's own colours, a little deeper, half see-through - far enough off to
				// sit back behind the fight rather than compete with it.
				Color haze = Data.SkyTop.Lerp(Data.SkyBottom, 0.45f);
				var block = new Color(haze.R * 0.84f, haze.G * 0.84f, haze.B * 0.9f, 0.55f);
				var window = new Color(Data.Accent.R, Data.Accent.G, Data.Accent.B, 0.55f);
				float x = r.Position.X;
				int i = 0;
				while (x < r.End.X)
				{
					float w = 90.0f + 70.0f * Mathf.Abs(CrayonBrush.Noise(prop.Seed, i));
					float h = r.Size.Y * (0.35f + 0.65f * Mathf.Abs(CrayonBrush.Noise(prop.Seed + 1, i)));
					var building = new Rect2(x, r.End.Y - h, w, h);
					DrawRect(building, block);
					for (float wy = building.Position.Y + 30.0f; wy < building.End.Y - 30.0f; wy += 46.0f)
					{
						for (float wx = building.Position.X + 16.0f; wx < building.End.X - 24.0f; wx += 34.0f)
						{
							if (CrayonBrush.Noise(prop.Seed + Mathf.RoundToInt(wx), Mathf.RoundToInt(wy)) > 0.45f)
							{
								DrawRect(new Rect2(wx, wy, 14.0f, 18.0f), window);
							}
						}
					}
					x += w + 8.0f + 30.0f * Mathf.Abs(CrayonBrush.Noise(prop.Seed + 2, i));
					i++;
				}
				break;
			}

			case PropKind.FridgeMagnet:
				DrawFridgeMagnet(centre, r.Size.X * 0.5f, prop.Seed);
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
