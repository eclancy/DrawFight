using Godot;

/// <summary>
/// The stage roster, and the only place a stage is named or laid out. Following the sibling
/// project, where StageCatalog replaced four disagreeing copies of the same list.
/// </summary>
public static class StageCatalog
{
	public const int Count = 3;

	/// <summary>Cached for the same reason the fighter roster is: a StageData is a Resource.</summary>
	static readonly StageData[] cache = new StageData[Count];

	public static StageData Get(int index)
	{
		int i = ((index % Count) + Count) % Count;
		if (cache[i] != null) return cache[i];

		cache[i] = i == 1 ? OpenPlains() : i == 2 ? CityRooftops() : FridgeDoor();
		return cache[i];
	}

	public static string NameOf(int index)
	{
		switch (((index % Count) + Count) % Count)
		{
			case 1: return "Open Plains";
			case 2: return "City Rooftops";
			default: return "Fridge Door";
		}
	}

	// =========================================================================
	// 0 - FRIDGE DOOR (default)
	// =========================================================================

	/// <summary>
	/// Where kids' drawings actually end up. The stage is a drawing on a sheet of paper taped to
	/// a steel fridge door, and the door round it - brushed metal, the handle, magnets holding up
	/// other drawings - is the backdrop. All of it in coloured pencil. The other pages are where
	/// his non-fighter art has somewhere to live.
	/// </summary>
	public static StageData FridgeDoor()
	{
		var data = new StageData
		{
			DisplayName = "Fridge Door",
			Style = StageStyle.TapedPage,

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

		// Only just bigger than the stage, so the door shows round it.
		data.PageRect = new Rect2(-960.0f, -640.0f, 1920.0f, 840.0f);

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
			// Runs past both blast zones, so there is no edge and no ledge.
			new StagePlatform(new Rect2(-2000.0f, 0.0f, 4000.0f, 460.0f), false, PlatformLook.Ledge),

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
}
