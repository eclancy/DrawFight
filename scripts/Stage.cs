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
	public bool IsBelowBottom(Vector2 point) => Data.HasBottomBlastZone && point.Y > Data.BlastZone.End.Y;

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
			// frames without also falling through the floor of the stage. One that moves is an
			// AnimatableBody2D moved in step with physics, so whoever stands on it is carried.
			PhysicsBody2D body = platform.Moves
				? new AnimatableBody2D { SyncToPhysics = true }
				: new StaticBody2D();
			body.Position = platform.Rect.Position + platform.Rect.Size * 0.5f;
			body.CollisionLayer = platform.OneWay ? 2u : 1u;
			body.CollisionMask = 0;

			body.AddChild(new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = platform.Rect.Size },
				OneWayCollision = platform.OneWay,
			});

			AddChild(body);

			if (platform.Moves)
			{
				// Drawn on its own, over the stage, where it has got to.
				var view = new Node2D();
				AddChild(view);
				StagePlatform drawn = platform;
				view.Draw += () => DrawMoving(view, drawn);
				movers.Add((platform, body, view));
			}

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

	/// <summary>
	/// The other drawings taken off the fridge door, for DoomBot's reveal: "the fridge is nearly
	/// bare". Only the props come off; the pages the fight is on stay.
	/// </summary>
	public bool Bare
	{
		get => bare;
		set
		{
			bare = value;
			QueueRedraw();
		}
	}
	bool bare;

	/// <summary>Everything painted behind the stage. The camera never looks past it (GameCamera.Limits).</summary>
	public Rect2 Backdrop => Data.BlastZone.Grow(BackdropPad);

	/// <summary>
	/// Where the stage's own drawing ends at the bottom: the foot of its lowest solid ground, or of
	/// the page or pages it is drawn on, whichever is lower. The camera never looks below it (Eric,
	/// 2026-10-17): someone falling to a KO goes off the bottom of the screen, and the view stays
	/// on the stage instead of chasing them down into blank backdrop.
	/// </summary>
	public float ArtBottom
	{
		get
		{
			float bottom = float.NegativeInfinity;
			foreach (StagePlatform platform in Data.Platforms)
			{
				if (!platform.OneWay) bottom = Mathf.Max(bottom, platform.Rect.End.Y);
			}
			if (Data.Style == StageStyle.TapedPage) bottom = Mathf.Max(bottom, Data.PageRect.End.Y);
			if (Data.Style == StageStyle.PinnedPages) foreach (PinnedPage page in Data.Pages) bottom = Mathf.Max(bottom, page.Rect.End.Y);
			return float.IsNegativeInfinity(bottom) ? Backdrop.End.Y : Mathf.Min(bottom, Backdrop.End.Y);
		}
	}

	public override void _Draw()
	{
		if (Data == null) return;

		CrayonBrush.SkyBands(this, Backdrop, Data.SkyTop, Data.SkyBottom);
		if (Data.Style == StageStyle.TapedPage || Data.Style == StageStyle.PinnedPages) DrawBrushedMetal();
		DrawPaperSubstrate();

		foreach (StageProp prop in Data.Props)
		{
			if (bare && (prop.Kind == PropKind.TapedDrawing || prop.Kind == PropKind.FridgeMagnet)) continue;
			DrawProp(prop);
		}
		if (Data.Style == StageStyle.TapedPage) DrawTapedPage(Data.PageRect);
		if (Data.Style == StageStyle.PinnedPages) foreach (PinnedPage page in Data.Pages) DrawPinnedPage(page);
		foreach (StageProp prop in Data.OnPage) DrawProp(prop);
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
			case PlatformLook.StageDeck: DrawStageDeck(platform, seed); break;
			case PlatformLook.AmpStack: DrawAmpStack(platform, seed); break;
			case PlatformLook.Girder: DrawGirder(platform, seed); break;
			case PlatformLook.SlideTower: DrawSlideTower(platform, seed); break;
			case PlatformLook.MonkeyBars: DrawMonkeyBars(platform, seed); break;
			case PlatformLook.SwingSet: DrawSwingSet(platform, seed); break;
			case PlatformLook.Sandbox: DrawSandbox(platform, seed); break;
			case PlatformLook.Dirt: DrawDirt(platform, seed); break;
			case PlatformLook.SiteCabin: DrawSiteCabin(platform, seed); break;
			case PlatformLook.Countertop: DrawCountertop(platform, seed); break;
			case PlatformLook.Sink: DrawSink(platform, seed); break;
			case PlatformLook.Toaster: DrawToaster(platform, seed); break;
			case PlatformLook.Books: DrawBooks(platform, seed); break;
			case PlatformLook.WallCabinet: DrawWallCabinet(platform, seed); break;
			case PlatformLook.Shelf: DrawShelf(platform, seed); break;
			default: DrawLedge(platform, seed); break;
		}
	}

	/// <summary>
	/// Outline weight and dashing carry the rules of the stage: a solid outline means solid
	/// ground, a dashed one means you can drop through it. The mechanic becomes visible rather
	/// than something a player discovers by dying.
	/// </summary>
	void OutlinePlatform(Rect2 rect, bool oneWay, int seed) => OutlinePlatform(this, rect, oneWay, seed);

	void OutlinePlatform(CanvasItem canvas, Rect2 rect, bool oneWay, int seed)
	{
		CrayonBrush.InkRect(canvas, rect, Data.Ink, oneWay ? 3.0f : 4.5f, seed, 2.0f, dashed: oneWay);
	}

	// --- Moving platforms ------------------------------------------------------------------

	readonly List<(StagePlatform platform, PhysicsBody2D body, Node2D view)> movers = new List<(StagePlatform, PhysicsBody2D, Node2D)>();
	int moveFrames;

	/// <summary>Where a moving platform has got to: a smooth swing out and back, once every TravelFrames.</summary>
	Vector2 TravelNow(StagePlatform platform) =>
		platform.Travel * Mathf.Sin(Mathf.Tau * (moveFrames % platform.TravelFrames) / platform.TravelFrames);

	public override void _PhysicsProcess(double delta)
	{
		if (movers.Count == 0) return;
		moveFrames++;
		foreach ((StagePlatform platform, PhysicsBody2D body, Node2D view) in movers)
		{
			Vector2 offset = TravelNow(platform);
			body.Position = platform.Rect.Position + platform.Rect.Size * 0.5f + offset;
			view.Position = offset;
			view.QueueRedraw();
		}
	}

	void DrawMoving(CanvasItem canvas, StagePlatform platform)
	{
		int seed = Mathf.RoundToInt(platform.Rect.Position.X * 7.0f + platform.Rect.Position.Y * 13.0f);
		if (platform.Look == PlatformLook.HangingBeam) DrawHangingBeam(canvas, platform, seed, true);
	}

	/// <summary>The beam hanging from a crane's jib, if one does: the one right under it, a cable's drop below.</summary>
	StagePlatform LoadUnder(Rect2 jib)
	{
		foreach (StagePlatform p in Data.Platforms)
		{
			if (p.Look != PlatformLook.HangingBeam) continue;
			float x = p.Rect.GetCenter().X;
			if (Mathf.IsEqualApprox(p.Rect.Position.Y, jib.Position.Y + StageData.CraneDrop) && x > jib.Position.X && x < jib.End.X) return p;
		}
		return null;
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
		// The front of the jib is the end its load hangs from; the counterweight goes on the back.
		StagePlatform load = LoadUnder(jib);
		int front = load == null || load.Rect.GetCenter().X >= mastX ? 1 : -1;
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

		// The counterweight on the back end, and the cab under the jib by the mast, on the front side.
		var weight = new Rect2(front > 0 ? jib.Position.X + 12.0f : jib.End.X - 76.0f, jib.End.Y, 64.0f, 54.0f);
		DrawRect(weight, Data.PropFill);
		CrayonBrush.CrayonFill(this, weight, Data.PropCrayon, seed + 7, 10.0f, 0.0f, 6.0f);
		CrayonBrush.InkRect(this, weight, Data.Ink, 3.0f, seed + 7, 1.0f);
		var cab = new Rect2(front > 0 ? mastX + MastW * 0.5f : mastX - MastW * 0.5f - 54.0f, jib.End.Y, 54.0f, 46.0f);
		DrawRect(cab, CraneYellow);
		DrawRect(new Rect2(cab.Position.X + 10.0f, cab.Position.Y + 8.0f, 34.0f, 20.0f), Data.GroundFill);
		CrayonBrush.InkRect(this, cab, Data.Ink, 3.0f, seed + 8, 1.0f);

		// With nothing on it, the bare hook near the front end; a load draws its own hook and
		// trolley over the beam it carries (DrawHangingBeam), wherever that has run to.
		if (load == null)
		{
			float hookX = front > 0 ? jib.End.X - 50.0f : jib.Position.X + 50.0f;
			CrayonBrush.InkLine(this, new Vector2(hookX, jib.End.Y), new Vector2(hookX, jib.End.Y + 80.0f), Data.Ink, 2.5f, seed + 9, 0.4f);
			DrawArc(new Vector2(hookX + 7.0f, jib.End.Y + 88.0f), 9.0f, 0.0f, Mathf.Pi * 1.3f, 12, Data.Ink, 4.0f);
		}

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

	void DrawHangingBeam(StagePlatform platform, int seed)
	{
		// A moving one is drawn by its own view, where it has run to (DrawMoving).
		if (!platform.Moves) DrawHangingBeam(this, platform, seed, false);
	}

	/// <summary>
	/// A red steel beam hanging level on two cables from the crane above it: the cables meet at the
	/// hook, which hangs on its line from a trolley riding under the jib - straight above the beam,
	/// so it is plainly carried by the crane, and goes wherever the trolley runs.
	/// </summary>
	void DrawHangingBeam(CanvasItem canvas, StagePlatform platform, int seed, bool moving)
	{
		Rect2 r = platform.Rect;
		float x = r.GetCenter().X;
		float jibBottom = r.Position.Y - StageData.CraneDrop + 22.0f;
		var hook = new Vector2(x, r.Position.Y - StageData.CraneDrop + 110.0f);

		// The trolley, its wheels on the jib's bottom chord, and the hook's line down from it.
		var trolley = new Rect2(x - 22.0f, jibBottom, 44.0f, 16.0f);
		canvas.DrawRect(trolley, CraneYellow);
		CrayonBrush.InkRect(canvas, trolley, Data.Ink, 2.6f, seed + 5, 0.6f);
		canvas.DrawCircle(new Vector2(x - 12.0f, jibBottom + 1.0f), 5.0f, Data.Ink);
		canvas.DrawCircle(new Vector2(x + 12.0f, jibBottom + 1.0f), 5.0f, Data.Ink);
		CrayonBrush.InkLine(canvas, new Vector2(x, trolley.End.Y), hook, Data.Ink, 2.5f, seed + 6, 0.3f);
		canvas.DrawArc(hook + new Vector2(5.0f, 6.0f), 7.0f, 0.0f, Mathf.Pi * 1.3f, 10, Data.Ink, 3.5f);

		// The two cables, from the hook down to each end of the beam.
		CrayonBrush.InkLine(canvas, new Vector2(r.Position.X + 20.0f, r.Position.Y), hook, Data.Ink, 2.5f, seed + 1, 0.5f);
		CrayonBrush.InkLine(canvas, new Vector2(r.End.X - 20.0f, r.Position.Y), hook, Data.Ink, 2.5f, seed + 2, 0.5f);

		var beam = new Color(0.90f, 0.40f, 0.34f);
		canvas.DrawRect(r, beam);
		canvas.DrawRect(new Rect2(r.Position.X, r.Position.Y + r.Size.Y * 0.3f, r.Size.X, r.Size.Y * 0.4f), new Color(0.80f, 0.34f, 0.30f));
		for (float bx = r.Position.X + 22.0f; bx < r.End.X - 10.0f; bx += 36.0f)
		{
			canvas.DrawCircle(new Vector2(bx, r.GetCenter().Y), 3.5f, new Color(0.98f, 0.94f, 0.88f));
		}
		OutlinePlatform(canvas, r, platform.OneWay, seed);
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

	static readonly Color Wood = new Color(0.86f, 0.66f, 0.42f);
	static readonly Color Steel = new Color(0.62f, 0.66f, 0.74f);

	/// <summary>
	/// A concert stage floor. The rectangle is the deck, boards running across it; below it hangs
	/// the stage's skirt, pleated, down to where the deck would meet the floor of the hall.
	/// </summary>
	void DrawStageDeck(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		var skirt = new Rect2(r.Position.X + 10.0f, r.End.Y, r.Size.X - 20.0f, 150.0f);
		DrawRect(skirt, new Color(0.90f, 0.58f, 0.58f));
		for (float x = skirt.Position.X + 30.0f; x < skirt.End.X - 10.0f; x += 46.0f)
		{
			CrayonBrush.InkLine(this, new Vector2(x, skirt.Position.Y + 4.0f), new Vector2(x, skirt.End.Y - 4.0f),
				new Color(0.78f, 0.44f, 0.46f), 4.0f, seed + Mathf.RoundToInt(x), 1.0f);
		}
		CrayonBrush.InkRect(this, skirt, Data.Ink, 2.5f, seed + 3, 1.6f);

		DrawRect(r, Wood);
		CrayonBrush.CrayonFill(this, r, new Color(0.76f, 0.54f, 0.32f, 0.5f), seed, 10.0f, 0.0f, 5.0f);
		for (float x = r.Position.X + 80.0f; x < r.End.X - 20.0f; x += 120.0f)
		{
			CrayonBrush.InkLine(this, new Vector2(x, r.Position.Y + 4.0f), new Vector2(x, r.End.Y - 4.0f),
				new Color(0.62f, 0.44f, 0.28f), 2.5f, seed + Mathf.RoundToInt(x), 0.6f);
		}
		OutlinePlatform(r, platform.OneWay, seed);
		if (!platform.OneWay)
		{
			CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 5.0f, seed + 77, 1.6f);
		}
	}

	/// <summary>
	/// An amp on top of speaker cabinets. The rectangle is the amp's top, the part you stand on;
	/// the cabinets stack under it to the ground, each with a pair of speakers. Grey, not black -
	/// the fighters own black.
	/// </summary>
	void DrawAmpStack(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		float ground = Data.GroundLine;
		var cabinet = new Color(0.56f, 0.57f, 0.63f);
		var grille = new Color(0.70f, 0.71f, 0.76f);
		float height = (ground - r.End.Y) * 0.5f;
		for (int i = 0; i < 2; i++)
		{
			var box = new Rect2(r.Position.X + 8.0f, r.End.Y + i * height, r.Size.X - 16.0f, height);
			DrawRect(box, cabinet);
			DrawRect(box.Grow(-10.0f), grille);
			foreach (float fx in new[] { 0.3f, 0.7f })
			{
				var at = new Vector2(box.Position.X + box.Size.X * fx, box.GetCenter().Y);
				float radius = Mathf.Min(box.Size.X * 0.17f, box.Size.Y * 0.36f);
				DrawCircle(at, radius, new Color(0.62f, 0.63f, 0.69f));
				DrawArc(at, radius, 0.0f, Mathf.Tau, 24, Data.Ink, 2.5f, true);
				DrawCircle(at, radius * 0.32f, new Color(0.80f, 0.80f, 0.84f));
			}
			CrayonBrush.InkRect(this, box, Data.Ink, 2.5f, seed + 5 + i, 1.2f);
		}

		// The amp: knobs along its front, and a warm light that says it is on.
		DrawRect(r, new Color(0.66f, 0.62f, 0.58f));
		for (float x = r.Position.X + 26.0f; x < r.End.X - 40.0f; x += 24.0f)
		{
			DrawCircle(new Vector2(x, r.GetCenter().Y), 4.0f, new Color(0.96f, 0.93f, 0.86f));
		}
		DrawCircle(new Vector2(r.End.X - 22.0f, r.GetCenter().Y), 5.0f, Data.Accent);
		OutlinePlatform(r, platform.OneWay, seed);
	}

	/// <summary>
	/// A lighting rig: a red steel girder, riveted, held up at each end on a steel lattice tower
	/// that stands on the ground, with spotlights hung along it. A soft girder gets the dashed
	/// outline, like any platform you can jump up through. The beams of light are pale and
	/// see-through, so they light whoever is under them rather than hiding them - and they are
	/// part of the girder, drawn over the page, where a prop would sit behind it.
	/// </summary>
	void DrawGirder(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		float ground = Data.GroundLine;
		const float TowerW = 44.0f;
		int i = 0;
		foreach (float x in new[] { r.Position.X + 6.0f, r.End.X - 6.0f - TowerW })
		{
			var tower = new Rect2(x, r.End.Y, TowerW, ground - r.End.Y);
			DrawRect(tower, new Color(Steel, 0.35f));
			CrayonBrush.InkLine(this, tower.Position, new Vector2(tower.Position.X, tower.End.Y), Data.Ink, 3.5f, seed + 1 + i, 1.0f);
			CrayonBrush.InkLine(this, new Vector2(tower.End.X, tower.Position.Y), tower.End, Data.Ink, 3.5f, seed + 3 + i, 1.0f);
			int k = 0;
			for (float y = tower.Position.Y; y < tower.End.Y - 10.0f; y += TowerW, k++)
			{
				float x0 = k % 2 == 0 ? tower.Position.X : tower.End.X;
				float x1 = k % 2 == 0 ? tower.End.X : tower.Position.X;
				CrayonBrush.InkLine(this, new Vector2(x0, y), new Vector2(x1, Mathf.Min(y + TowerW, tower.End.Y)), Steel, 4.0f, seed + 20 + k + i * 50, 0.6f);
			}
			i++;
		}

		int lamps = Mathf.Max(1, Mathf.RoundToInt((r.Size.X - TowerW * 2.0f) / 320.0f));
		for (int l = 0; l < lamps; l++)
		{
			float x = r.Position.X + TowerW + (r.Size.X - TowerW * 2.0f) * (l + 0.5f) / lamps;
			var lamp = new Vector2(x, r.End.Y + 30.0f);
			DrawColoredPolygon(new[]
			{
				lamp + new Vector2(-12.0f, 8.0f), lamp + new Vector2(12.0f, 8.0f),
				new Vector2(x + 110.0f, ground), new Vector2(x - 110.0f, ground),
			}, new Color(Data.Celestial, 0.18f));
			CrayonBrush.InkLine(this, new Vector2(x, r.End.Y), lamp, Data.Ink, 2.5f, seed + 60 + l, 0.3f);
			DrawCircle(lamp, 16.0f, Steel);
			DrawArc(lamp, 16.0f, 0.0f, Mathf.Tau, 20, Data.Ink, 2.5f, true);
			DrawCircle(lamp + new Vector2(0.0f, 8.0f), 8.0f, Data.Celestial);
		}

		var beam = new Color(0.90f, 0.40f, 0.34f);
		DrawRect(r, beam);
		DrawRect(new Rect2(r.Position.X, r.Position.Y + r.Size.Y * 0.3f, r.Size.X, r.Size.Y * 0.4f), new Color(0.80f, 0.34f, 0.30f));
		for (float x = r.Position.X + 22.0f; x < r.End.X - 10.0f; x += 36.0f)
		{
			DrawCircle(new Vector2(x, r.GetCenter().Y), 3.5f, new Color(0.98f, 0.94f, 0.88f));
		}
		OutlinePlatform(r, platform.OneWay, seed);
	}


	// --- The playground ------------------------------------------------------------------------

	static readonly Color PlayRed = new Color(0.95f, 0.50f, 0.42f);
	static readonly Color PlayBlue = new Color(0.46f, 0.70f, 0.94f);
	static readonly Color Sand = new Color(0.97f, 0.88f, 0.62f);

	/// <summary>A ladder of two rails and rungs, from <paramref name="top"/> straight down to <paramref name="bottom"/>.</summary>
	void DrawLadder(float x, float top, float bottom, float width, Color colour, int seed)
	{
		CrayonBrush.InkLine(this, new Vector2(x - width * 0.5f, top), new Vector2(x - width * 0.5f, bottom), Data.Ink, 4.0f, seed, 0.8f);
		CrayonBrush.InkLine(this, new Vector2(x + width * 0.5f, top), new Vector2(x + width * 0.5f, bottom), Data.Ink, 4.0f, seed + 1, 0.8f);
		int k = 0;
		for (float y = top + 34.0f; y < bottom - 10.0f; y += 52.0f, k++)
		{
			CrayonBrush.InkLine(this, new Vector2(x - width * 0.5f, y), new Vector2(x + width * 0.5f, y), colour, 6.0f, seed + 10 + k, 0.6f);
		}
	}

	/// <summary>
	/// A slide. The rectangle is the deck at the top, which you stand on; a post at each end goes
	/// down to the ground, the ladder up its back, and the slide itself runs down its front to the
	/// grass - coloured in, no ink round it, because you cannot stand on it.
	/// </summary>
	void DrawSlideTower(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		float ground = Data.GroundLine;
		foreach (float x in new[] { r.Position.X + 14.0f, r.End.X - 14.0f })
		{
			var post = new Rect2(x - 9.0f, r.End.Y, 18.0f, ground - r.End.Y);
			DrawRect(post, Wood);
			CrayonBrush.InkRect(this, post, Data.Ink, 2.5f, seed + Mathf.RoundToInt(x), 1.0f);
		}
		DrawLadder(r.Position.X - 30.0f, r.Position.Y - 10.0f, ground, 54.0f, Wood, seed + 3);

		// The slide: a band from the front of the deck down to the ground, with its raised edge.
		float run = (ground - r.Position.Y) * 1.25f;
		Vector2 topLeft = new Vector2(r.End.X - 6.0f, r.Position.Y + 4.0f);
		Vector2 bottomRight = new Vector2(r.End.X + run, ground - 6.0f);
		Vector2 drop = new Vector2(0.0f, 30.0f);
		DrawColoredPolygon(new[] { topLeft, bottomRight + new Vector2(30.0f, 0.0f), bottomRight + new Vector2(30.0f, 0.0f) + drop * 0.6f, topLeft + drop },
			new Color(PlayRed, 0.85f));
		CrayonBrush.InkLine(this, topLeft - new Vector2(0.0f, 10.0f), bottomRight + new Vector2(30.0f, -10.0f),
			new Color(PlayRed.R * 0.85f, PlayRed.G * 0.85f, PlayRed.B * 0.85f), 7.0f, seed + 5, 0.8f);

		DrawRect(r, Wood);
		CrayonBrush.CrayonFill(this, r, new Color(0.76f, 0.54f, 0.32f, 0.5f), seed, 9.0f, 0.0f, 5.0f);
		OutlinePlatform(r, platform.OneWay, seed);
	}

	/// <summary>
	/// Monkey bars from the side: the bars seen end on, as a row of rounds between two rails, on a
	/// ladder at each end down to the ground. The rails are what you stand on.
	/// </summary>
	void DrawMonkeyBars(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		float ground = Data.GroundLine;
		DrawLadder(r.Position.X + 26.0f, r.End.Y, ground, 50.0f, PlayBlue, seed + 1);
		DrawLadder(r.End.X - 26.0f, r.End.Y, ground, 50.0f, PlayBlue, seed + 2);

		DrawRect(r, new Color(PlayBlue, 0.35f));
		for (float x = r.Position.X + 30.0f; x < r.End.X - 20.0f; x += 46.0f)
		{
			DrawCircle(new Vector2(x, r.GetCenter().Y), r.Size.Y * 0.36f, Data.Accent);
		}
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), PlayBlue, 7.0f, seed + 3, 0.6f);
		CrayonBrush.InkLine(this, new Vector2(r.Position.X, r.End.Y), r.End, PlayBlue, 7.0f, seed + 4, 0.6f);
		OutlinePlatform(r, platform.OneWay, seed);
	}

	/// <summary>
	/// A swing set: the top bar, which you stand on, on a pair of splayed legs at each end, and
	/// swings hanging on chains - drawn thin, and you go through them.
	/// </summary>
	void DrawSwingSet(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		float ground = Data.GroundLine;
		const float Splay = 110.0f;
		int i = 0;
		foreach (float x in new[] { r.Position.X + 12.0f, r.End.X - 12.0f })
		{
			CrayonBrush.InkLine(this, new Vector2(x, r.GetCenter().Y), new Vector2(x - Splay, ground), Data.Ink, 6.0f, seed + i, 1.0f);
			CrayonBrush.InkLine(this, new Vector2(x, r.GetCenter().Y), new Vector2(x + Splay, ground), Data.Ink, 6.0f, seed + 5 + i, 1.0f);
			CrayonBrush.InkLine(this, new Vector2(x, r.GetCenter().Y), new Vector2(x - Splay, ground), PlayBlue, 3.0f, seed + 10 + i, 1.0f);
			CrayonBrush.InkLine(this, new Vector2(x, r.GetCenter().Y), new Vector2(x + Splay, ground), PlayBlue, 3.0f, seed + 15 + i, 1.0f);
			i++;
		}

		int swings = Mathf.Max(1, Mathf.RoundToInt(r.Size.X / 220.0f));
		float hang = Mathf.Min(300.0f, (ground - r.End.Y) * 0.72f);
		for (int s = 0; s < swings; s++)
		{
			float x = r.Position.X + r.Size.X * (s + 0.5f) / swings;
			// Each hangs a little off true, as if someone just got off it.
			float lean = CrayonBrush.Noise(seed, s) * 18.0f;
			var seat = new Rect2(x - 46.0f + lean, r.End.Y + hang, 92.0f, 16.0f);
			foreach (float cx in new[] { seat.Position.X + 8.0f, seat.End.X - 8.0f })
			{
				CrayonBrush.InkLine(this, new Vector2(cx - lean, r.End.Y), new Vector2(cx, seat.Position.Y), new Color(Data.Ink, 0.6f), 2.0f, seed + 30 + s, 0.4f);
			}
			DrawRect(seat, s % 2 == 0 ? PlayRed : Data.Accent);
		}

		DrawRect(r, Steel);
		DrawRect(new Rect2(r.Position.X, r.Position.Y + 4.0f, r.Size.X, r.Size.Y * 0.3f), new Color(1.0f, 1.0f, 1.0f, 0.4f));
		OutlinePlatform(r, platform.OneWay, seed);
	}

	/// <summary>
	/// A sandbox: a low wooden box on the ground, its sand heaped a little, a spade stuck upright
	/// in one corner. The sand is the top you stand on.
	/// </summary>
	void DrawSandbox(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		DrawRect(r, Wood);
		CrayonBrush.CrayonFill(this, r, new Color(0.76f, 0.54f, 0.32f, 0.5f), seed, 9.0f, 0.0f, 5.0f);
		for (float y = r.Position.Y + 24.0f; y < r.End.Y - 6.0f; y += 22.0f)
		{
			CrayonBrush.InkLine(this, new Vector2(r.Position.X + 6.0f, y), new Vector2(r.End.X - 6.0f, y), new Color(0.62f, 0.44f, 0.28f), 2.0f, seed + Mathf.RoundToInt(y), 0.6f);
		}
		// The sand along the top, with a heap of it, and the spade.
		var sand = new Rect2(r.Position.X + 10.0f, r.Position.Y - 6.0f, r.Size.X - 20.0f, 18.0f);
		DrawRect(sand, Sand);
		Vector2 heap = new Vector2(r.Position.X + r.Size.X * 0.62f, r.Position.Y);
		DrawColoredPolygon(new[] { heap + new Vector2(-70.0f, 2.0f), heap + new Vector2(0.0f, -14.0f), heap + new Vector2(70.0f, 2.0f) }, Sand);
		float spadeX = r.Position.X + 40.0f;
		CrayonBrush.InkLine(this, new Vector2(spadeX, r.Position.Y - 4.0f), new Vector2(spadeX + 14.0f, r.Position.Y - 74.0f), PlayRed, 6.0f, seed + 7, 0.4f);
		DrawColoredPolygon(new[] { new Vector2(spadeX - 14.0f, r.Position.Y + 2.0f), new Vector2(spadeX + 14.0f, r.Position.Y + 2.0f),
			new Vector2(spadeX + 10.0f, r.Position.Y - 26.0f), new Vector2(spadeX - 10.0f, r.Position.Y - 26.0f) }, PlayBlue);
		OutlinePlatform(r, platform.OneWay, seed);
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 5.0f, seed + 77, 1.6f);
	}

	// --- The building site ----------------------------------------------------------------------

	/// <summary>
	/// Dug earth seen in cross-section: soil coloured in, a band of it packed harder along the top,
	/// and stones in it. Solid ground, with a heavy line along the top where people land.
	/// </summary>
	void DrawDirt(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		DrawRect(r, Data.GroundFill);
		CrayonBrush.CrayonFill(this, r, Data.GroundCrayon, seed, 14.0f, 0.0f, 8.0f);
		var packed = new Rect2(r.Position.X, r.Position.Y, r.Size.X, 34.0f);
		DrawRect(packed, new Color(Data.GroundCrayon.R * 0.92f, Data.GroundCrayon.G * 0.9f, Data.GroundCrayon.B * 0.88f, 0.7f));
		var stone = new Color(0.80f, 0.77f, 0.72f);
		for (int i = 0; i < r.Size.X * r.Size.Y / 22000.0f; i++)
		{
			float x = r.Position.X + 30.0f + (r.Size.X - 60.0f) * (0.5f + 0.5f * CrayonBrush.Noise(seed, i));
			float y = r.Position.Y + 70.0f + (r.Size.Y - 100.0f) * (0.5f + 0.5f * CrayonBrush.Noise(seed + 1, i));
			float size = 9.0f + 9.0f * Mathf.Abs(CrayonBrush.Noise(seed + 2, i));
			DrawCircle(new Vector2(x, y), size, stone);
			DrawArc(new Vector2(x, y), size, 0.0f, Mathf.Tau, 14, new Color(Data.GroundCrayon.R * 0.8f, Data.GroundCrayon.G * 0.8f, Data.GroundCrayon.B * 0.8f), 2.0f, true);
		}
		OutlinePlatform(r, platform.OneWay, seed);
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 5.0f, seed + 77, 1.6f);
	}

	/// <summary>
	/// A site office, the kind that comes on a lorry: corrugated walls, a window and a door, and a
	/// strip of hazard stripes along the bottom. Its roof is where you stand.
	/// </summary>
	void DrawSiteCabin(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		DrawRect(r, Data.PropFill);
		CrayonBrush.CrayonFill(this, r, Data.PropCrayon, seed, 14.0f, 0.0f, 8.0f);
		for (float x = r.Position.X + 20.0f; x < r.End.X - 10.0f; x += 26.0f)
		{
			CrayonBrush.InkLine(this, new Vector2(x, r.Position.Y + 10.0f), new Vector2(x, r.End.Y - 30.0f),
				new Color(Data.PropCrayon.R * 0.9f, Data.PropCrayon.G * 0.9f, Data.PropCrayon.B * 0.9f), 2.5f, seed + Mathf.RoundToInt(x), 0.5f);
		}
		var window = new Rect2(r.Position.X + 36.0f, r.Position.Y + 40.0f, r.Size.X * 0.36f, r.Size.Y * 0.36f);
		DrawRect(window, Data.SkyBottom);
		CrayonBrush.InkRect(this, window, Data.Ink, 2.5f, seed + 3, 1.2f);
		var door = new Rect2(r.End.X - 30.0f - r.Size.X * 0.22f, r.Position.Y + 34.0f, r.Size.X * 0.22f, r.Size.Y - 64.0f);
		DrawRect(door, Data.PlatformCrayon);
		CrayonBrush.InkRect(this, door, Data.Ink, 2.5f, seed + 4, 1.2f);
		DrawCircle(new Vector2(door.Position.X + 12.0f, door.GetCenter().Y), 4.0f, Data.Ink);
		// Hazard stripes along the bottom: yellow and pale grey, never black.
		var stripes = new Rect2(r.Position.X, r.End.Y - 26.0f, r.Size.X, 26.0f);
		DrawRect(stripes, Data.Accent);
		for (float x = stripes.Position.X; x < stripes.End.X - 20.0f; x += 40.0f)
		{
			DrawColoredPolygon(new[] { new Vector2(x, stripes.End.Y), new Vector2(x + 20.0f, stripes.End.Y),
				new Vector2(x + 40.0f, stripes.Position.Y), new Vector2(x + 20.0f, stripes.Position.Y) }, Data.PropCrayon);
		}
		OutlinePlatform(r, platform.OneWay, seed);
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 6.0f, seed + 77, 2.0f);
	}

	// --- The kitchen ----------------------------------------------------------------------------

	/// <summary>Cupboard doors across a rectangle, each with a knob - pencilled, the way the furniture is.</summary>
	void DrawCupboards(Rect2 area, float doorWidth, Color fill, int seed)
	{
		DrawRect(area, fill);
		CrayonBrush.PencilFill(this, area, new Color(Data.PropCrayon, 0.7f), seed, 6.0f, 2.2f);
		int doors = Mathf.Max(1, Mathf.RoundToInt(area.Size.X / doorWidth));
		float w = area.Size.X / doors;
		for (int d = 0; d < doors; d++)
		{
			var door = new Rect2(area.Position.X + d * w + 10.0f, area.Position.Y + 12.0f, w - 20.0f, area.Size.Y - 24.0f);
			CrayonBrush.PencilRect(this, door, new Color(Data.PropCrayon.R * 0.8f, Data.PropCrayon.G * 0.8f, Data.PropCrayon.B * 0.8f), 2.5f, seed + d);
			float knobX = d % 2 == 0 ? door.End.X - 16.0f : door.Position.X + 16.0f;
			DrawCircle(new Vector2(knobX, door.Position.Y + Mathf.Min(40.0f, door.Size.Y * 0.3f)), 6.0f, Steel);
		}
	}

	/// <summary>
	/// A kitchen unit. The rectangle is the whole of it, standing on the floor: the worktop along
	/// the top - what you stand on, a heavy line along it - then a row of drawers, cupboards below,
	/// and the kick board at the bottom.
	/// </summary>
	void DrawCountertop(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		const float Top = 40.0f;
		const float Drawers = 70.0f;
		const float Kick = 34.0f;
		var drawers = new Rect2(r.Position.X + 6.0f, r.Position.Y + Top, r.Size.X - 12.0f, Drawers);
		var cupboards = new Rect2(r.Position.X + 6.0f, drawers.End.Y, r.Size.X - 12.0f, r.Size.Y - Top - Drawers - Kick);
		DrawCupboards(cupboards, 200.0f, Data.PropFill, seed + 1);
		DrawRect(drawers, Data.PropFill);
		CrayonBrush.PencilFill(this, drawers, new Color(Data.PropCrayon, 0.7f), seed + 2, 6.0f, 2.2f);
		int count = Mathf.Max(1, Mathf.RoundToInt(drawers.Size.X / 200.0f));
		for (int d = 0; d < count; d++)
		{
			var front = new Rect2(drawers.Position.X + d * drawers.Size.X / count + 10.0f, drawers.Position.Y + 10.0f, drawers.Size.X / count - 20.0f, Drawers - 20.0f);
			CrayonBrush.PencilRect(this, front, new Color(Data.PropCrayon.R * 0.8f, Data.PropCrayon.G * 0.8f, Data.PropCrayon.B * 0.8f), 2.5f, seed + 20 + d);
			CrayonBrush.InkLine(this, new Vector2(front.GetCenter().X - 24.0f, front.GetCenter().Y), new Vector2(front.GetCenter().X + 24.0f, front.GetCenter().Y), Steel, 6.0f, seed + 40 + d, 0.3f);
		}
		var kick = new Rect2(r.Position.X + 16.0f, r.End.Y - Kick, r.Size.X - 32.0f, Kick);
		DrawRect(kick, new Color(Data.PropCrayon.R * 0.9f, Data.PropCrayon.G * 0.9f, Data.PropCrayon.B * 0.9f));

		var worktop = new Rect2(r.Position.X, r.Position.Y, r.Size.X, Top);
		DrawRect(worktop, Data.PlatformCrayon);
		CrayonBrush.PencilFill(this, worktop, new Color(Data.PlatformCrayon.R * 0.85f, Data.PlatformCrayon.G * 0.82f, Data.PlatformCrayon.B * 0.8f), seed + 3, 5.0f, 2.2f);
		OutlinePlatform(r, platform.OneWay, seed);
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 6.0f, seed + 77, 1.6f);
		CrayonBrush.InkLine(this, new Vector2(r.Position.X, r.Position.Y + Top), new Vector2(r.End.X, r.Position.Y + Top), Data.Ink, 3.0f, seed + 78, 1.2f);
	}

	/// <summary>
	/// A sink set into the worktop between two units. The rectangle is the cupboard under it, its
	/// top the floor of the basin; the steel basin is drawn from the worktop down to it, and the tap
	/// curves over it from the wall.
	/// </summary>
	void DrawSink(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		float rim = Data.GroundLine;
		var basin = new Rect2(r.Position.X, rim, r.Size.X, r.Position.Y - rim);
		DrawRect(basin, new Color(0.84f, 0.87f, 0.90f));
		CrayonBrush.PencilFill(this, basin, new Color(0.66f, 0.70f, 0.76f, 0.6f), seed, 6.0f, 2.2f);
		DrawRect(new Rect2(basin.Position.X + 20.0f, basin.Position.Y + 10.0f, 18.0f, basin.Size.Y - 20.0f), new Color(1.0f, 1.0f, 1.0f, 0.55f));

		// The tap, from the wall behind, curving over the basin.
		float tapX = r.GetCenter().X + r.Size.X * 0.2f;
		var tapColour = new Color(0.74f, 0.77f, 0.82f);
		CrayonBrush.InkLine(this, new Vector2(tapX, rim), new Vector2(tapX, rim - 170.0f), tapColour, 14.0f, seed + 1, 0.3f);
		DrawArc(new Vector2(tapX - 50.0f, rim - 170.0f), 50.0f, Mathf.Pi, Mathf.Tau, 16, tapColour, 14.0f, true);
		CrayonBrush.InkLine(this, new Vector2(tapX - 100.0f, rim - 170.0f), new Vector2(tapX - 100.0f, rim - 130.0f), tapColour, 14.0f, seed + 2, 0.3f);
		DrawCircle(new Vector2(tapX + 34.0f, rim - 40.0f), 14.0f, PlayRed);

		DrawCupboards(r, 180.0f, Data.PropFill, seed + 3);
		OutlinePlatform(r, platform.OneWay, seed);
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 5.0f, seed + 77, 1.4f);
	}

	/// <summary>A toaster: rounded and pale, two slots in its top, its lever and dial, on chrome feet. You stand on its top.</summary>
	void DrawToaster(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		var body = new Color(0.66f, 0.84f, 0.82f);
		DrawRect(r, body);
		CrayonBrush.PencilFill(this, r, new Color(0.50f, 0.70f, 0.70f, 0.6f), seed, 5.0f, 2.2f);
		DrawRect(new Rect2(r.Position.X + 16.0f, r.Position.Y + 18.0f, 20.0f, r.Size.Y - 50.0f), new Color(1.0f, 1.0f, 1.0f, 0.5f));
		foreach (float fx in new[] { 0.3f, 0.66f })
		{
			var slot = new Rect2(r.Position.X + r.Size.X * fx - 30.0f, r.Position.Y + 4.0f, 60.0f, 10.0f);
			DrawRect(slot, new Color(0.56f, 0.60f, 0.62f));
		}
		var lever = new Rect2(r.End.X - 4.0f, r.Position.Y + r.Size.Y * 0.35f, 26.0f, 16.0f);
		DrawRect(lever, Steel);
		CrayonBrush.InkRect(this, lever, Data.Ink, 2.0f, seed + 1, 0.6f);
		DrawCircle(new Vector2(r.GetCenter().X, r.End.Y - 40.0f), 14.0f, Steel);
		DrawArc(new Vector2(r.GetCenter().X, r.End.Y - 40.0f), 14.0f, 0.0f, Mathf.Tau, 16, Data.Ink, 2.0f, true);
		foreach (float x in new[] { r.Position.X + 20.0f, r.End.X - 44.0f })
		{
			DrawRect(new Rect2(x, r.End.Y - 6.0f, 24.0f, 6.0f), Steel);
		}
		OutlinePlatform(r, platform.OneWay, seed);
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 5.0f, seed + 77, 1.4f);
	}

	/// <summary>A pile of cookbooks lying flat: each its own colour, page edges along one side.</summary>
	void DrawBooks(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		Color[] covers = { PlayRed, PlayBlue, new Color(0.56f, 0.80f, 0.52f), Data.Accent };
		int books = Mathf.Max(2, Mathf.RoundToInt(r.Size.Y / 46.0f));
		float h = r.Size.Y / books;
		for (int b = 0; b < books; b++)
		{
			// Not quite squared up, the way a pile of books never is.
			float shift = CrayonBrush.Noise(seed, b) * 16.0f;
			var book = new Rect2(r.Position.X + 6.0f + shift, r.End.Y - (b + 1) * h, r.Size.X - 12.0f, h);
			DrawRect(book, covers[(b + seed) & 3]);
			var pages = new Rect2(book.End.X - 22.0f, book.Position.Y + 6.0f, 18.0f, h - 12.0f);
			DrawRect(pages, new Color(0.98f, 0.96f, 0.90f));
			CrayonBrush.InkRect(this, book, Data.Ink, 2.5f, seed + b, 1.0f);
		}
		OutlinePlatform(r, platform.OneWay, seed);
		CrayonBrush.InkLine(this, r.Position, new Vector2(r.End.X, r.Position.Y), Data.Ink, 5.0f, seed + 77, 1.4f);
	}

	/// <summary>
	/// A wall cupboard. Its top is the platform, dashed: you jump up through the cupboard to stand
	/// on it. The cupboard under it is pencilled, not inked - furniture on the wall, not a wall.
	/// </summary>
	void DrawWallCabinet(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		var body = new Rect2(r.Position.X + 12.0f, r.End.Y, r.Size.X - 24.0f, 220.0f);
		DrawCupboards(body, 200.0f, new Color(Data.PropFill, 0.9f), seed + 1);
		CrayonBrush.PencilRect(this, body, new Color(Data.PropCrayon.R * 0.75f, Data.PropCrayon.G * 0.75f, Data.PropCrayon.B * 0.75f), 3.0f, seed + 2);
		DrawRect(r, Data.PlatformCrayon);
		CrayonBrush.PencilFill(this, r, new Color(Data.PlatformCrayon.R * 0.85f, Data.PlatformCrayon.G * 0.82f, Data.PlatformCrayon.B * 0.8f), seed + 3, 5.0f, 2.2f);
		OutlinePlatform(r, platform.OneWay, seed);
	}

	/// <summary>A plank on two wall brackets.</summary>
	void DrawShelf(StagePlatform platform, int seed)
	{
		Rect2 r = platform.Rect;
		foreach (float x in new[] { r.Position.X + r.Size.X * 0.2f, r.End.X - r.Size.X * 0.2f })
		{
			DrawColoredPolygon(new[] { new Vector2(x - 6.0f, r.End.Y), new Vector2(x + 6.0f, r.End.Y), new Vector2(x + 6.0f, r.End.Y + 70.0f) }, Steel);
			CrayonBrush.InkLine(this, new Vector2(x, r.End.Y), new Vector2(x, r.End.Y + 70.0f), Steel, 5.0f, seed + Mathf.RoundToInt(x), 0.3f);
			CrayonBrush.InkLine(this, new Vector2(x, r.End.Y + 70.0f), new Vector2(x + 50.0f, r.End.Y), Steel, 4.0f, seed + Mathf.RoundToInt(x) + 1, 0.3f);
		}
		DrawRect(r, Wood);
		CrayonBrush.CrayonFill(this, r, new Color(0.76f, 0.54f, 0.32f, 0.5f), seed, 9.0f, 0.0f, 5.0f);
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

			case PropKind.Window:
			{
				// A window on the wall: sky through the glass, a pale frame and curtains, no ink -
				// it is on the wall behind the fight.
				var frame = new Color(Data.PropCrayon.R * 0.9f, Data.PropCrayon.G * 0.9f, Data.PropCrayon.B * 0.9f);
				DrawRect(r, new Color(0.98f, 0.97f, 0.94f));
				var glass = r.Grow(-16.0f);
				CrayonBrush.SkyBands(this, glass, new Color(0.62f, 0.80f, 0.95f), new Color(0.90f, 0.95f, 0.98f), 12);
				CrayonBrush.InkLine(this, new Vector2(glass.GetCenter().X, glass.Position.Y), new Vector2(glass.GetCenter().X, glass.End.Y), frame, 8.0f, prop.Seed, 0.6f);
				CrayonBrush.InkLine(this, new Vector2(glass.Position.X, glass.GetCenter().Y), new Vector2(glass.End.X, glass.GetCenter().Y), frame, 8.0f, prop.Seed + 1, 0.6f);
				CrayonBrush.PencilRect(this, r, frame, 3.0f, prop.Seed + 2);
				var curtain = new Color(Data.Accent.R, Data.Accent.G, Data.Accent.B, 0.55f);
				DrawColoredPolygon(new[] { r.Position + new Vector2(-30.0f, -10.0f), r.Position + new Vector2(r.Size.X * 0.22f, -10.0f),
					new Vector2(r.Position.X + r.Size.X * 0.08f, r.End.Y + 20.0f), new Vector2(r.Position.X - 40.0f, r.End.Y + 20.0f) }, curtain);
				DrawColoredPolygon(new[] { new Vector2(r.End.X + 30.0f, r.Position.Y - 10.0f), new Vector2(r.End.X - r.Size.X * 0.22f, r.Position.Y - 10.0f),
					new Vector2(r.End.X - r.Size.X * 0.08f, r.End.Y + 20.0f), new Vector2(r.End.X + 40.0f, r.End.Y + 20.0f) }, curtain);
				break;
			}

			case PropKind.Cone:
			{
				// A traffic cone: orange, a pale band round it, on a square foot. No ink - scenery.
				var orange = new Color(0.98f, 0.60f, 0.30f);
				var foot = new Rect2(r.Position.X, r.End.Y - r.Size.Y * 0.12f, r.Size.X, r.Size.Y * 0.12f);
				DrawRect(foot, orange);
				float half = r.Size.X * 0.36f;
				DrawColoredPolygon(new[] { new Vector2(centre.X - half, foot.Position.Y), new Vector2(centre.X + half, foot.Position.Y),
					new Vector2(centre.X + half * 0.18f, r.Position.Y), new Vector2(centre.X - half * 0.18f, r.Position.Y) }, orange);
				float bandY = r.Position.Y + r.Size.Y * 0.42f;
				DrawRect(new Rect2(centre.X - half * 0.62f, bandY, half * 1.24f, r.Size.Y * 0.14f), new Color(0.98f, 0.97f, 0.94f));
				break;
			}
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
