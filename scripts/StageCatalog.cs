using Godot;

/// <summary>
/// The stage roster, and the only place a stage is named or laid out. Following the sibling
/// project, where StageCatalog replaced four disagreeing copies of the same list.
/// </summary>
public static class StageCatalog
{
	public const int Count = 7;

	/// <summary>Cached for the same reason the fighter roster is: a StageData is a Resource.</summary>
	static readonly StageData[] cache = new StageData[Count];

	public static StageData Get(int index)
	{
		int i = ((index % Count) + Count) % Count;
		if (cache[i] != null) return cache[i];

		cache[i] = i switch
		{
			1 => OpenPlains(),
			2 => CityRooftops(),
			3 => BigShow(),
			4 => Playground(),
			5 => BuildingSite(),
			6 => KitchenCounter(),
			_ => FridgeDoor(),
		};
		return cache[i];
	}

	public static string NameOf(int index)
	{
		switch (((index % Count) + Count) % Count)
		{
			case 1: return "Open Plains";
			case 2: return "City Rooftops";
			case 3: return "Big Show";
			case 4: return "Playground";
			case 5: return "Building Site";
			case 6: return "Kitchen Counter";
			default: return "Fridge Door";
		}
	}

	// =========================================================================
	// 0 - FRIDGE DOOR (default)
	// =========================================================================

	/// <summary>
	/// Where kids' drawings actually end up. The stage is drawn across several sheets of paper
	/// stuck to a steel fridge door with magnets - a ruled page, a squared page, plain scraps
	/// under the little side platforms - overlapping and a little crooked, the way a fridge door
	/// really looks. The door round them - brushed metal, the handle, more drawings held up
	/// higher - is the backdrop. All of it in coloured pencil. The other pages are where his
	/// non-fighter art has somewhere to live.
	/// </summary>
	public static StageData FridgeDoor()
	{
		var data = new StageData
		{
			DisplayName = "Fridge Door",
			Style = StageStyle.PinnedPages,

			// The door: pale brushed steel, a little cooler at the bottom. Light, like every
			// backdrop - the fighters own the dark end of the value ladder.
			SkyTop = new Color(0.86f, 0.88f, 0.90f),
			SkyBottom = new Color(0.76f, 0.79f, 0.83f),
			RuleLine = new Color(0.78f, 0.855f, 0.90f),
			MarginLine = new Color(0.91f, 0.66f, 0.72f),
			Ink = new Color(0.19f, 0.31f, 0.61f),
			GroundFill = new Color(0.96f, 0.95f, 0.91f),
			GroundCrayon = new Color(0.72f, 0.80f, 0.86f),
			PlatformCrayon = new Color(0.93f, 0.62f, 0.45f),
			PropFill = new Color(0.97f, 0.96f, 0.92f),
			PropCrayon = new Color(0.72f, 0.83f, 0.72f),
			Accent = new Color(0.96f, 0.80f, 0.36f),
			Celestial = new Color(0.97f, 0.88f, 0.55f),
			Night = false,
			GroundLine = 0.0f,
		};

		data.Platforms = new[]
		{
			// The main ground, drawn along the page.
			new StagePlatform(new Rect2(-620.0f, 0.0f, 1240.0f, 48.0f), false, PlatformLook.PencilLedge),

			new StagePlatform(new Rect2(-430.0f, -250.0f, 300.0f, 28.0f), true, PlatformLook.PencilLedge),
			new StagePlatform(new Rect2(130.0f, -250.0f, 300.0f, 28.0f), true, PlatformLook.PencilLedge),
			new StagePlatform(new Rect2(-150.0f, -480.0f, 300.0f, 28.0f), true, PlatformLook.PencilLedge),

			// Two small ones out past the ground, so the ledges are worth fighting over.
			new StagePlatform(new Rect2(-870.0f, -170.0f, 170.0f, 26.0f), true, PlatformLook.PencilLedge),
			new StagePlatform(new Rect2(700.0f, -170.0f, 170.0f, 26.0f), true, PlatformLook.PencilLedge),
		};

		// The pages, back to front: the top one is tucked behind the two big ones it overlaps,
		// and each little side platform has a scrap of its own. Every platform sits on paper.
		data.Pages = new[]
		{
			new PinnedPage(new Rect2(-270.0f, -650.0f, 540.0f, 280.0f), 0.8f, PaperKind.Plain, 1, 3),
			new PinnedPage(new Rect2(-700.0f, -420.0f, 720.0f, 560.0f), -1.4f, PaperKind.Ruled, 2, 1),
			new PinnedPage(new Rect2(-40.0f, -450.0f, 740.0f, 590.0f), 1.2f, PaperKind.Grid, 2, 2),
			new PinnedPage(new Rect2(-940.0f, -300.0f, 300.0f, 230.0f), 2.5f, PaperKind.Plain, 1, 4),
			new PinnedPage(new Rect2(650.0f, -290.0f, 300.0f, 220.0f), -2.2f, PaperKind.Ruled, 1, 5),
		};

		data.Props = new[]
		{
			// Other drawings on the door, held up by magnets, above and beside the sheet.
			new StageProp(PropKind.TapedDrawing, new Rect2(-900.0f, -1180.0f, 320.0f, 260.0f), 11),
			new StageProp(PropKind.FridgeMagnet, new Rect2(-765.0f, -1205.0f, 50.0f, 50.0f), 12),
			new StageProp(PropKind.TapedDrawing, new Rect2(420.0f, -1220.0f, 340.0f, 280.0f), 23),
			new StageProp(PropKind.FridgeMagnet, new Rect2(565.0f, -1245.0f, 50.0f, 50.0f), 24),
			new StageProp(PropKind.FridgeMagnet, new Rect2(-140.0f, -1010.0f, 60.0f, 60.0f), 31),
			new StageProp(PropKind.FridgeHandle, new Rect2(1180.0f, -1100.0f, 56.0f, 1700.0f), 5),
		};

		data.SpawnPoints = new[]
		{
			new Vector2(-340.0f, -300.0f), new Vector2(340.0f, -300.0f),
			new Vector2(-120.0f, -560.0f), new Vector2(120.0f, -560.0f),
		};
		data.RespawnPoint = new Vector2(0.0f, -640.0f);
		return data;
	}

	// =========================================================================
	// 1 - OPEN PLAINS
	// =========================================================================

	/// <summary>
	/// Flat, open, pleasant, and impossible to fall out of the bottom of: the ground runs the
	/// entire width of the play area, so the only way to lose a stock is to be launched off a
	/// side or over the top.
	///
	/// That makes it the friendly stage. Nobody dies to a missed recovery, which is the single
	/// most demoralising way for a younger player to lose, so this is the right default for a
	/// first match with someone who has not played before.
	///
	/// Nature stage, so it uses the colouring-book treatment rather than the exercise book - and
	/// nothing built on it: only trees and grass, and the treetops are the platforms.
	/// </summary>
	public static StageData OpenPlains()
	{
		var data = new StageData
		{
			DisplayName = "Open Plains",
			Style = StageStyle.OutsideTheLines,

			SkyTop = new Color(0.62f, 0.80f, 0.92f),
			SkyBottom = new Color(0.94f, 0.95f, 0.88f),
			RuleLine = new Color(0.80f, 0.86f, 0.90f),
			MarginLine = new Color(0.91f, 0.66f, 0.72f),
			Ink = new Color(0.22f, 0.29f, 0.44f),
			GroundFill = new Color(0.86f, 0.91f, 0.74f),
			GroundCrayon = new Color(0.60f, 0.78f, 0.47f),
			PlatformCrayon = new Color(0.82f, 0.56f, 0.36f),
			PropFill = new Color(0.96f, 0.93f, 0.85f),
			PropCrayon = new Color(0.78f, 0.62f, 0.42f),
			Accent = new Color(0.97f, 0.82f, 0.40f),
			Celestial = new Color(0.99f, 0.89f, 0.48f),
			Night = false,

			// No bottom blast zone: the ground is the floor of the world here.
			HasBottomBlastZone = false,
			GroundLine = 0.0f,
		};

		data.Platforms = new[]
		{
			// Runs past both blast zones and out past the edge of the painted sky, so there is no
			// edge, no ledge, and no end to the ground however far the camera pulls back.
			new StagePlatform(new Rect2(-2600.0f, 0.0f, 5200.0f, 460.0f), false, PlatformLook.Ledge),

			// Treetops - the leaves are the soft platforms. No buildings on the plains.
			new StagePlatform(new Rect2(-680.0f, -200.0f, 310.0f, 26.0f), true, PlatformLook.TreeTop),
			new StagePlatform(new Rect2(370.0f, -200.0f, 310.0f, 26.0f), true, PlatformLook.TreeTop),
			new StagePlatform(new Rect2(-165.0f, -410.0f, 330.0f, 26.0f), true, PlatformLook.TreeTop),
		};

		data.Props = new[]
		{
			new StageProp(PropKind.Sun, new Rect2(820.0f, -880.0f, 190.0f, 190.0f), 3),
			new StageProp(PropKind.Hill, new Rect2(-1500.0f, -180.0f, 1100.0f, 190.0f), 9),
			new StageProp(PropKind.Hill, new Rect2(200.0f, -150.0f, 1300.0f, 160.0f), 14),
			new StageProp(PropKind.Cloud, new Rect2(-900.0f, -780.0f, 300.0f, 120.0f), 21),
			new StageProp(PropKind.Cloud, new Rect2(120.0f, -900.0f, 250.0f, 100.0f), 28),
			new StageProp(PropKind.Cloud, new Rect2(-300.0f, -640.0f, 200.0f, 80.0f), 33),
			new StageProp(PropKind.Bush, new Rect2(-1050.0f, -90.0f, 160.0f, 90.0f), 41),
			new StageProp(PropKind.Bush, new Rect2(860.0f, -80.0f, 140.0f, 80.0f), 47),
			new StageProp(PropKind.Tree, new Rect2(-1320.0f, -420.0f, 260.0f, 420.0f), 55),
			new StageProp(PropKind.Tree, new Rect2(1060.0f, -380.0f, 230.0f, 380.0f), 58),
			new StageProp(PropKind.Tree, new Rect2(-980.0f, -300.0f, 180.0f, 300.0f), 61),
			new StageProp(PropKind.Grass, new Rect2(-1500.0f, -34.0f, 3000.0f, 34.0f), 64),
		};

		data.SpawnPoints = new[]
		{
			new Vector2(-360.0f, -320.0f), new Vector2(360.0f, -320.0f),
			new Vector2(-120.0f, -520.0f), new Vector2(120.0f, -520.0f),
		};
		data.RespawnPoint = new Vector2(0.0f, -620.0f);
		return data;
	}

	// =========================================================================
	// 2 - CITY ROOFTOPS
	// =========================================================================

	/// <summary>
	/// Separate rooftops with real gaps between them, and a street at the bottom with traffic.
	/// Miss a jump and you land in the road, where cars come by at random and knock you flying -
	/// jump over them or get out of the street. The opposite of Open Plains on purpose: this is
	/// the stage where the ground itself is the hazard, and it exists so the roster has one of each.
	///
	/// Evening: a clear blue sky paling toward the horizon, a moon coming up and the first
	/// windows lit. Light, like every sky here - a dark one would swallow the fighters, who are
	/// drawn in black marker. The buildings are pale cement, not coloured paper.
	/// </summary>
	/// <summary>Where City Rooftops' street is: the bottom of every building, and the top of the road.</summary>
	const float Street = 350.0f;

	public static StageData CityRooftops()
	{
		var data = new StageData
		{
			DisplayName = "City Rooftops",
			Style = StageStyle.ExerciseBook,

			SkyTop = new Color(0.42f, 0.64f, 0.94f),
			SkyBottom = new Color(0.84f, 0.91f, 0.98f),
			RuleLine = new Color(0.72f, 0.80f, 0.92f),
			MarginLine = new Color(0.90f, 0.62f, 0.66f),
			Ink = new Color(0.20f, 0.24f, 0.46f),
			// Unlit windows: pale glass reflecting the sky.
			GroundFill = new Color(0.84f, 0.89f, 0.96f),
			GroundCrayon = new Color(0.70f, 0.74f, 0.82f),
			PlatformCrayon = new Color(0.76f, 0.76f, 0.80f),
			// Cement: warm off-white, shaded in pale grey.
			PropFill = new Color(0.95f, 0.94f, 0.91f),
			PropCrayon = new Color(0.80f, 0.79f, 0.76f),
			Accent = new Color(0.99f, 0.85f, 0.45f),
			Celestial = new Color(0.99f, 0.96f, 0.82f),
			Night = true,
			GroundLine = Street,
			// The street runs the whole width, so there is nothing to fall out of the bottom of.
			HasBottomBlastZone = false,
			Traffic = true,
		};

		data.Platforms = new[]
		{
			// Each rectangle is the whole building, standing on the street.
			new StagePlatform(new Rect2(-1500.0f, 40.0f, 300.0f, Street - 40.0f), false, PlatformLook.Building),
			new StagePlatform(new Rect2(-1080.0f, -40.0f, 470.0f, Street + 40.0f), false, PlatformLook.Building),
			new StagePlatform(new Rect2(-340.0f, -210.0f, 430.0f, Street + 210.0f), false, PlatformLook.Building),
			new StagePlatform(new Rect2(320.0f, -70.0f, 420.0f, Street + 70.0f), false, PlatformLook.Building),
			new StagePlatform(new Rect2(1180.0f, -160.0f, 300.0f, Street + 160.0f), false, PlatformLook.Building),

			// The tall one.
			new StagePlatform(new Rect2(830.0f, -760.0f, 210.0f, Street + 760.0f), false, PlatformLook.Tower),

			// The street: past both side blast zones, so it has no corners to grab and no end.
			new StagePlatform(new Rect2(-2600.0f, Street, 5200.0f, 420.0f), false, PlatformLook.Road),

			// A building site in the first gap: a tower crane whose jib you can stand on, with a
			// steel beam hanging off its hook - both soft, so you can jump up through them.
			new StagePlatform(new Rect2(-760.0f, -380.0f, 470.0f, 22.0f), true, PlatformLook.CraneJib),
			new StagePlatform(new Rect2(-425.0f, -380.0f + StageData.CraneDrop, 170.0f, 20.0f), true, PlatformLook.HangingBeam),

			// Scaffolding up the side of the second gap: two levels, a way to climb back out of
			// the street before the next car comes.
			new StagePlatform(new Rect2(100.0f, 40.0f, 210.0f, 18.0f), true, PlatformLook.Scaffold),
			new StagePlatform(new Rect2(100.0f, 190.0f, 210.0f, 18.0f), true, PlatformLook.Scaffold),

			// A billboard gantry, so the middle of the map is contestable in the air.
			new StagePlatform(new Rect2(-80.0f, -560.0f, 300.0f, 24.0f), true, PlatformLook.Ledge),
		};

		data.Props = new[]
		{
			new StageProp(PropKind.Moon, new Rect2(-900.0f, -1000.0f, 170.0f, 170.0f), 2),
			// The rest of the city, far off behind the rooftops.
			new StageProp(PropKind.Skyline, new Rect2(-2600.0f, -640.0f, 5200.0f, 640.0f + Street), 71),
			new StageProp(PropKind.Star, new Rect2(-400.0f, -1080.0f, 26.0f, 26.0f), 6),
			new StageProp(PropKind.Star, new Rect2(120.0f, -960.0f, 20.0f, 20.0f), 8),
			new StageProp(PropKind.Star, new Rect2(560.0f, -1120.0f, 24.0f, 24.0f), 12),
			new StageProp(PropKind.Star, new Rect2(-660.0f, -820.0f, 18.0f, 18.0f), 16),
			new StageProp(PropKind.Star, new Rect2(980.0f, -1040.0f, 22.0f, 22.0f), 19),
			new StageProp(PropKind.Cloud, new Rect2(200.0f, -880.0f, 280.0f, 90.0f), 24),
		};

			data.SpawnPoints = new[]
		{
			new Vector2(-850.0f, -340.0f), new Vector2(520.0f, -370.0f),
			new Vector2(-130.0f, -520.0f), new Vector2(930.0f, -1060.0f),
		};
		data.RespawnPoint = new Vector2(-130.0f, -900.0f);
		return data;
	}

	// =========================================================================
	// 3 - BIG SHOW
	// =========================================================================

	/// <summary>
	/// A concert stage, drawn on a big page stuck to the fridge door - Flambé's, for his story,
	/// where Lugnut has put a lighting rig over it (.ai/story-mode.md). A wooden stage deck with
	/// the fall off either end, an amp stack at each side, and a red steel girder across the top
	/// exactly as wide as the deck, its spotlights shining down. The amps are the way up for anyone
	/// whose jump will not reach the girder on its own (Circy).
	/// </summary>
	public static StageData BigShow()
	{
		var data = new StageData
		{
			DisplayName = "Big Show",
			Style = StageStyle.PinnedPages,

			// The fridge door round the page, as on Fridge Door.
			SkyTop = new Color(0.86f, 0.88f, 0.90f),
			SkyBottom = new Color(0.76f, 0.79f, 0.83f),
			RuleLine = new Color(0.78f, 0.855f, 0.90f),
			MarginLine = new Color(0.91f, 0.66f, 0.72f),
			Ink = new Color(0.19f, 0.31f, 0.61f),
			GroundFill = new Color(0.96f, 0.95f, 0.91f),
			GroundCrayon = new Color(0.72f, 0.80f, 0.86f),
			PlatformCrayon = new Color(0.93f, 0.62f, 0.45f),
			PropFill = new Color(0.97f, 0.96f, 0.92f),
			PropCrayon = new Color(0.72f, 0.83f, 0.72f),
			Accent = new Color(0.98f, 0.66f, 0.32f),
			Celestial = new Color(0.99f, 0.93f, 0.62f),
			Night = false,
			GroundLine = 0.0f,
		};

		// A big stage: wider and taller than the others, with the blast zones pushed out to match
		// (Eric, 2026-10-11: it should be larger).
		const float Width = 2200.0f;
		data.BlastZone = new Rect2(-2000.0f, -1600.0f, 4000.0f, 2800.0f);
		data.Platforms = new[]
		{
			new StagePlatform(new Rect2(-Width * 0.5f, 0.0f, Width, 52.0f), false, PlatformLook.StageDeck),
			new StagePlatform(new Rect2(-900.0f, -250.0f, 240.0f, 26.0f), true, PlatformLook.AmpStack),
			new StagePlatform(new Rect2(660.0f, -250.0f, 240.0f, 26.0f), true, PlatformLook.AmpStack),
			new StagePlatform(new Rect2(-380.0f, -250.0f, 240.0f, 26.0f), true, PlatformLook.AmpStack),
			new StagePlatform(new Rect2(140.0f, -250.0f, 240.0f, 26.0f), true, PlatformLook.AmpStack),
			// The girder: the same width as the deck under it.
			new StagePlatform(new Rect2(-Width * 0.5f, -560.0f, Width, 32.0f), true, PlatformLook.Girder),
		};

		data.Pages = new[]
		{
			new PinnedPage(new Rect2(-1300.0f, -840.0f, 2600.0f, 1100.0f), -0.5f, PaperKind.Plain, 2, 7),
		};

		data.Props = new[]
		{
			new StageProp(PropKind.TapedDrawing, new Rect2(-1560.0f, -1400.0f, 320.0f, 260.0f), 85),
			new StageProp(PropKind.FridgeMagnet, new Rect2(-1425.0f, -1425.0f, 50.0f, 50.0f), 86),
			new StageProp(PropKind.FridgeHandle, new Rect2(1560.0f, -1300.0f, 56.0f, 2000.0f), 87),
		};

		data.SpawnPoints = new[]
		{
			new Vector2(-500.0f, -160.0f), new Vector2(500.0f, -160.0f),
			new Vector2(-200.0f, -660.0f), new Vector2(200.0f, -660.0f),
		};
		data.RespawnPoint = new Vector2(0.0f, -760.0f);
		return data;
	}

	// =========================================================================
	// The big stages, for three and four players (Eric, 2026-10-16). Each about half as wide again
	// as Big Show, with room for four to spread out, and its own camera range to see them all.
	// =========================================================================

	/// <summary>
	/// The blast zone round a big stage: as far past its edges and over its top platform as Big
	/// Show's, so a hit KOs at about the same percent - the stage is wider, not the launch longer.
	/// </summary>
	static readonly Rect2 BigBlast = new Rect2(-3000.0f, -1900.0f, 6000.0f, 3100.0f);

	const float BigCameraMinZoom = 0.32f;

	// =========================================================================
	// 4 - PLAYGROUND
	// =========================================================================

	/// <summary>
	/// Recess. A playing field with a slide, monkey bars, a sandbox and a swing set across it, and a
	/// tree at each end of the middle - outdoors, so in the colouring-book style, like Open Plains.
	/// Unlike the plains the field ends, at both sides: a big stage that can still be fallen off.
	/// </summary>
	public static StageData Playground()
	{
		var data = new StageData
		{
			DisplayName = "Playground",
			Style = StageStyle.OutsideTheLines,

			SkyTop = new Color(0.64f, 0.82f, 0.95f),
			SkyBottom = new Color(0.95f, 0.96f, 0.88f),
			RuleLine = new Color(0.80f, 0.86f, 0.90f),
			MarginLine = new Color(0.91f, 0.66f, 0.72f),
			Ink = new Color(0.22f, 0.29f, 0.44f),
			GroundFill = new Color(0.84f, 0.91f, 0.70f),
			GroundCrayon = new Color(0.56f, 0.78f, 0.44f),
			PlatformCrayon = new Color(0.95f, 0.52f, 0.42f),
			PropFill = new Color(0.96f, 0.93f, 0.85f),
			PropCrayon = new Color(0.78f, 0.62f, 0.42f),
			Accent = new Color(0.98f, 0.80f, 0.32f),
			Celestial = new Color(0.99f, 0.89f, 0.48f),
			GroundLine = 0.0f,
			BlastZone = BigBlast,
			CameraMinZoom = BigCameraMinZoom,
			Large = true,
		};

		data.Platforms = new[]
		{
			// The field: it ends a stage's width short of each side, so there is an edge to fight at.
			new StagePlatform(new Rect2(-1900.0f, 0.0f, 3800.0f, 420.0f), false, PlatformLook.Ledge),

			new StagePlatform(new Rect2(-1600.0f, -300.0f, 280.0f, 26.0f), true, PlatformLook.SlideTower),
			new StagePlatform(new Rect2(-780.0f, -400.0f, 520.0f, 22.0f), true, PlatformLook.MonkeyBars),
			new StagePlatform(new Rect2(-150.0f, -720.0f, 300.0f, 26.0f), true, PlatformLook.TreeTop),
			new StagePlatform(new Rect2(180.0f, -70.0f, 440.0f, 70.0f), false, PlatformLook.Sandbox),
			new StagePlatform(new Rect2(840.0f, -430.0f, 600.0f, 24.0f), true, PlatformLook.SwingSet),
			new StagePlatform(new Rect2(1470.0f, -720.0f, 300.0f, 26.0f), true, PlatformLook.TreeTop),
		};

		data.Props = new[]
		{
			new StageProp(PropKind.Sun, new Rect2(-1250.0f, -1350.0f, 200.0f, 200.0f), 3),
			new StageProp(PropKind.Cloud, new Rect2(-400.0f, -1300.0f, 320.0f, 120.0f), 21),
			new StageProp(PropKind.Cloud, new Rect2(900.0f, -1150.0f, 280.0f, 100.0f), 28),
			new StageProp(PropKind.Cloud, new Rect2(1900.0f, -1400.0f, 240.0f, 90.0f), 33),
			new StageProp(PropKind.Hill, new Rect2(-2600.0f, -200.0f, 2000.0f, 210.0f), 9),
			new StageProp(PropKind.Hill, new Rect2(400.0f, -170.0f, 2300.0f, 180.0f), 14),
			new StageProp(PropKind.Tree, new Rect2(-2500.0f, -460.0f, 280.0f, 460.0f), 55),
			new StageProp(PropKind.Tree, new Rect2(2250.0f, -420.0f, 250.0f, 420.0f), 58),
			// The school fence along the back of the field.
			new StageProp(PropKind.Fence, new Rect2(-1880.0f, -110.0f, 3760.0f, 110.0f), 62),
			new StageProp(PropKind.Bush, new Rect2(-1150.0f, -80.0f, 150.0f, 80.0f), 41),
			new StageProp(PropKind.Bush, new Rect2(1600.0f, -80.0f, 150.0f, 80.0f), 47),
			new StageProp(PropKind.Grass, new Rect2(-1890.0f, -34.0f, 3780.0f, 34.0f), 64),
		};

		data.SpawnPoints = new[]
		{
			new Vector2(-1460.0f, -420.0f), new Vector2(1140.0f, -560.0f),
			new Vector2(-520.0f, -520.0f), new Vector2(400.0f, -200.0f),
		};
		data.RespawnPoint = new Vector2(0.0f, -1000.0f);
		return data;
	}

	// =========================================================================
	// 5 - BUILDING SITE
	// =========================================================================

	/// <summary>
	/// Lugnut's world: a building site with a pit dug in the middle of it - fall in and you are out
	/// - a scaffold plank bridging it, a tower crane each side with a steel beam hanging off it out
	/// over the pit, a site office to climb on, and scaffolding up the far side. Day, in the
	/// exercise-book style, with the city behind.
	/// </summary>
	public static StageData BuildingSite()
	{
		var data = new StageData
		{
			DisplayName = "Building Site",
			Style = StageStyle.ExerciseBook,

			SkyTop = new Color(0.68f, 0.82f, 0.96f),
			SkyBottom = new Color(0.94f, 0.95f, 0.93f),
			RuleLine = new Color(0.74f, 0.82f, 0.92f),
			MarginLine = new Color(0.90f, 0.62f, 0.66f),
			Ink = new Color(0.22f, 0.26f, 0.44f),
			// Dug earth: sandy, coloured in browner.
			GroundFill = new Color(0.91f, 0.82f, 0.64f),
			GroundCrayon = new Color(0.76f, 0.60f, 0.42f),
			PlatformCrayon = new Color(0.98f, 0.74f, 0.32f),
			PropFill = new Color(0.95f, 0.94f, 0.90f),
			PropCrayon = new Color(0.78f, 0.78f, 0.76f),
			Accent = new Color(0.99f, 0.80f, 0.32f),
			Celestial = new Color(0.99f, 0.89f, 0.50f),
			GroundLine = 0.0f,
			BlastZone = BigBlast,
			CameraMinZoom = BigCameraMinZoom,
			Large = true,
		};

		data.Platforms = new[]
		{
			// Two pads of earth, the pit between them.
			new StagePlatform(new Rect2(-2000.0f, 0.0f, 1460.0f, 520.0f), false, PlatformLook.Dirt),
			new StagePlatform(new Rect2(540.0f, 0.0f, 1460.0f, 520.0f), false, PlatformLook.Dirt),

			// A scaffold plank across the pit, its poles standing on the edge of each side.
			new StagePlatform(new Rect2(-540.0f, -210.0f, 1080.0f, 18.0f), true, PlatformLook.Scaffold),

			// A tower crane each side, the jibs reaching out over the pit, a beam hanging off each.
			new StagePlatform(new Rect2(-1200.0f, -720.0f, 900.0f, 22.0f), true, PlatformLook.CraneJib),
			new StagePlatform(new Rect2(300.0f, -720.0f, 900.0f, 22.0f), true, PlatformLook.CraneJib),
			new StagePlatform(new Rect2(-470.0f, -720.0f + StageData.CraneDrop, 170.0f, 20.0f), true, PlatformLook.HangingBeam),
			new StagePlatform(new Rect2(300.0f, -720.0f + StageData.CraneDrop, 170.0f, 20.0f), true, PlatformLook.HangingBeam),

			// The site office, on the left.
			new StagePlatform(new Rect2(-1850.0f, -210.0f, 400.0f, 210.0f), false, PlatformLook.SiteCabin),

			// Scaffolding up the right: two levels.
			new StagePlatform(new Rect2(1400.0f, -240.0f, 300.0f, 18.0f), true, PlatformLook.Scaffold),
			new StagePlatform(new Rect2(1400.0f, -480.0f, 300.0f, 18.0f), true, PlatformLook.Scaffold),
		};

		data.Props = new[]
		{
			new StageProp(PropKind.Sun, new Rect2(1500.0f, -1450.0f, 200.0f, 200.0f), 3),
			new StageProp(PropKind.Skyline, new Rect2(-3900.0f, -760.0f, 7800.0f, 760.0f), 71),
			new StageProp(PropKind.Cloud, new Rect2(-900.0f, -1350.0f, 320.0f, 110.0f), 24),
			new StageProp(PropKind.Cloud, new Rect2(500.0f, -1250.0f, 260.0f, 90.0f), 26),
			// The hoarding round the site, along the back.
			new StageProp(PropKind.Fence, new Rect2(-1980.0f, -150.0f, 1420.0f, 150.0f), 62),
			new StageProp(PropKind.Fence, new Rect2(560.0f, -150.0f, 1420.0f, 150.0f), 63),
			// Cones along the edge of the pit.
			new StageProp(PropKind.Cone, new Rect2(-680.0f, -76.0f, 56.0f, 76.0f), 81),
			new StageProp(PropKind.Cone, new Rect2(-860.0f, -76.0f, 56.0f, 76.0f), 82),
			new StageProp(PropKind.Cone, new Rect2(624.0f, -76.0f, 56.0f, 76.0f), 83),
			new StageProp(PropKind.Cone, new Rect2(804.0f, -76.0f, 56.0f, 76.0f), 84),
		};

		data.SpawnPoints = new[]
		{
			new Vector2(-1650.0f, -330.0f), new Vector2(1550.0f, -600.0f),
			new Vector2(-900.0f, -160.0f), new Vector2(900.0f, -160.0f),
		};
		data.RespawnPoint = new Vector2(0.0f, -1000.0f);
		return data;
	}

	// =========================================================================
	// 6 - KITCHEN COUNTER
	// =========================================================================

	/// <summary>
	/// The kitchen the fridge is in - where story mode happens - drawn on a big sheet stuck to the
	/// fridge door. The counter runs most of the way across, a sink set into it to drop into and
	/// climb out of, a toaster and a pile of cookbooks on top, a shelf and two wall cupboards above,
	/// and a window over the sink. Fall off either end of the counter and you are out.
	/// </summary>
	public static StageData KitchenCounter()
	{
		var data = new StageData
		{
			DisplayName = "Kitchen Counter",
			Style = StageStyle.PinnedPages,

			// The fridge door round the page, as on Fridge Door.
			SkyTop = new Color(0.86f, 0.88f, 0.90f),
			SkyBottom = new Color(0.76f, 0.79f, 0.83f),
			RuleLine = new Color(0.78f, 0.855f, 0.90f),
			MarginLine = new Color(0.91f, 0.66f, 0.72f),
			Ink = new Color(0.19f, 0.31f, 0.61f),
			GroundFill = new Color(0.96f, 0.95f, 0.91f),
			GroundCrayon = new Color(0.72f, 0.80f, 0.86f),
			// A wooden worktop over pale blue cupboards.
			PlatformCrayon = new Color(0.93f, 0.78f, 0.58f),
			PropFill = new Color(0.90f, 0.94f, 0.97f),
			PropCrayon = new Color(0.66f, 0.78f, 0.88f),
			Accent = new Color(0.98f, 0.72f, 0.40f),
			Celestial = new Color(0.99f, 0.93f, 0.62f),
			GroundLine = 0.0f,
			BlastZone = BigBlast,
			CameraMinZoom = BigCameraMinZoom,
			Large = true,
		};

		data.Platforms = new[]
		{
			// The units, the sink between them, its basin floor a step down.
			new StagePlatform(new Rect2(-1900.0f, 0.0f, 1100.0f, 440.0f), false, PlatformLook.Countertop),
			new StagePlatform(new Rect2(-800.0f, 170.0f, 380.0f, 270.0f), false, PlatformLook.Sink),
			new StagePlatform(new Rect2(-420.0f, 0.0f, 2320.0f, 440.0f), false, PlatformLook.Countertop),

			// On the counter.
			new StagePlatform(new Rect2(-1620.0f, -140.0f, 250.0f, 140.0f), false, PlatformLook.Books),
			new StagePlatform(new Rect2(880.0f, -170.0f, 270.0f, 170.0f), false, PlatformLook.Toaster),

			// On the wall.
			new StagePlatform(new Rect2(-260.0f, -470.0f, 420.0f, 20.0f), true, PlatformLook.Shelf),
			new StagePlatform(new Rect2(1300.0f, -440.0f, 340.0f, 20.0f), true, PlatformLook.Shelf),
			new StagePlatform(new Rect2(-1780.0f, -760.0f, 640.0f, 26.0f), true, PlatformLook.WallCabinet),
			new StagePlatform(new Rect2(420.0f, -760.0f, 720.0f, 26.0f), true, PlatformLook.WallCabinet),
		};

		data.Pages = new[]
		{
			new PinnedPage(new Rect2(-2150.0f, -1250.0f, 4300.0f, 1850.0f), -0.4f, PaperKind.Plain, 2, 9),
		};

		// The window over the sink, drawn on the page.
		data.OnPage = new[]
		{
			new StageProp(PropKind.Window, new Rect2(-850.0f, -680.0f, 480.0f, 380.0f), 91),
		};

		data.Props = new[]
		{
			// Other drawings on the door round the page, and the handle.
			new StageProp(PropKind.TapedDrawing, new Rect2(-2000.0f, -1900.0f, 360.0f, 280.0f), 85),
			new StageProp(PropKind.FridgeMagnet, new Rect2(-1845.0f, -1925.0f, 50.0f, 50.0f), 86),
			new StageProp(PropKind.TapedDrawing, new Rect2(1500.0f, -1850.0f, 320.0f, 260.0f), 88),
			new StageProp(PropKind.FridgeMagnet, new Rect2(1635.0f, -1875.0f, 50.0f, 50.0f), 89),
			new StageProp(PropKind.FridgeHandle, new Rect2(2450.0f, -1700.0f, 56.0f, 2300.0f), 87),
		};

		data.SpawnPoints = new[]
		{
			new Vector2(-1500.0f, -260.0f), new Vector2(1400.0f, -160.0f),
			new Vector2(-50.0f, -580.0f), new Vector2(500.0f, -160.0f),
		};
		data.RespawnPoint = new Vector2(0.0f, -1050.0f);
		return data;
	}
}
