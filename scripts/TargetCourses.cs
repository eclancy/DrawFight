using Godot;

/// <summary>One course for Targets mode: a stage, built and drawn like any other, and its ten targets.</summary>
public sealed class TargetCourse
{
	public string Name;

	/// <summary>The key its best times are kept under (TargetRecords). Never renamed, or the times go.</summary>
	public string Id;

	public StageData Stage;

	/// <summary>Where each target hangs, its centre.</summary>
	public Vector2[] Targets;
}

/// <summary>
/// The targets courses. Each is a stage like the versus ones - a palette and a list of rectangles,
/// drawn by the one renderer - but kept here, not in StageCatalog: StageCatalog.Count is the versus
/// stage select, the stage achievements, F4 and the CPU, and a course is none of those.
/// </summary>
public static class TargetCourses
{
	public const int Count = 3;

	static readonly TargetCourse[] cache = new TargetCourse[Count];

	public static TargetCourse Get(int index)
	{
		int i = Mathf.Clamp(index, 0, Count - 1);
		return cache[i] ??= i switch
		{
			1 => Medium(),
			2 => Hard(),
			_ => Easy(),
		};
	}

	/// <summary>
	/// A sheet of squared paper on the fridge door, in the Fridge Door's colours. The blast zone
	/// sits well out past the targets at the sides and top, where nobody is launched, so its hint
	/// lines stay off the screen; the bottom is what a fall ends at.
	/// </summary>
	static StageData Sheet(string name, Rect2 blast)
	{
		return new StageData
		{
			DisplayName = name,
			Style = StageStyle.PinnedPages,
			Music = "battle",
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
			GroundLine = 0.0f,
			BlastZone = blast,
			CameraMinZoom = 0.5f,
		};
	}

	static StagePlatform Solid(float x, float y, float w, float h) =>
		new StagePlatform(new Rect2(x, y, w, h), false, PlatformLook.PencilLedge);

	static StagePlatform Soft(float x, float y, float w) =>
		new StagePlatform(new Rect2(x, y, w, 24.0f), true, PlatformLook.PencilLedge);

	// Every course is built round what the fighters can actually reach, measured (Eric's call that
	// every fighter can break every target, 2026-10-29). Standing, from the floor, to the soles of
	// the feet: a jump goes 188 (Circy) to 307 (the light three); jump and air jump 379 (Circy),
	// 480 (Lugnut, DoomBot), 620 and up; with the up special after them, 757 (Lugnut) and up. A
	// running jump, air jump and up special carries Lugnut 929 across and everyone else 1500 or
	// more. So: Easy never asks more than 300 up from where you stand, and has nothing to fall off;
	// Medium adds a pit, a target out over it and one past the end; Hard steps up 470 and 550, which
	// the up special has to finish for Circy, Lugnut and DoomBot, and hangs targets down in the gaps.
	// A target hangs at most 330 over what it is reached from - a jump and an up air for Circy.

	/// <summary>
	/// One big sheet with a floor all the way across and past both ends, as on Open Plains: nothing
	/// to fall off, and a step up is never more than one jump and the air jump for anyone.
	/// </summary>
	static TargetCourse Easy()
	{
		StageData data = Sheet("Easy", new Rect2(-1700.0f, -1400.0f, 3400.0f, 2000.0f));
		data.HasBottomBlastZone = false;
		data.Platforms = new[]
		{
			Solid(-2600.0f, 0.0f, 5200.0f, 250.0f),
			// A box on the floor: one hop.
			Solid(150.0f, -120.0f, 160.0f, 120.0f),
			Soft(-900.0f, -230.0f, 320.0f),
			Soft(-250.0f, -440.0f, 360.0f),
			Soft(450.0f, -230.0f, 320.0f),
			Soft(1000.0f, -440.0f, 300.0f),
		};
		data.Pages = new[] { new PinnedPage(new Rect2(-2500.0f, -1100.0f, 5000.0f, 1350.0f), -0.3f, PaperKind.Grid, 2, 41) };
		data.SpawnPoints = new[] { new Vector2(-1300.0f, -200.0f) };
		return new TargetCourse
		{
			Name = "Easy",
			Id = "easy",
			Stage = data,
			Targets = new[]
			{
				new Vector2(-1050.0f, -75.0f),
				new Vector2(-740.0f, -330.0f),
				new Vector2(-500.0f, -230.0f),
				new Vector2(-70.0f, -560.0f),
				new Vector2(-70.0f, -760.0f),
				new Vector2(230.0f, -195.0f),
				new Vector2(610.0f, -330.0f),
				new Vector2(760.0f, -70.0f),
				new Vector2(1150.0f, -560.0f),
				new Vector2(1500.0f, -330.0f),
			},
		};
	}

	/// <summary>
	/// Two sheets with a gap of fridge door between them to fall into, a third up over the gap with
	/// a bridge on it, a block to climb and a stair of ledges up each side. One target hangs in the
	/// gap and one out past the far end, over nothing.
	/// </summary>
	static TargetCourse Medium()
	{
		StageData data = Sheet("Medium", new Rect2(-2300.0f, -1800.0f, 4600.0f, 2500.0f));
		data.Platforms = new[]
		{
			Solid(-1500.0f, 0.0f, 1100.0f, 60.0f),
			Solid(-100.0f, 0.0f, 1600.0f, 60.0f),
			// A block to climb: a jump and the air jump for Circy, with the ledge to catch.
			Solid(300.0f, -300.0f, 200.0f, 300.0f),
			Soft(-1250.0f, -250.0f, 300.0f),
			Soft(-800.0f, -500.0f, 300.0f),
			Soft(-350.0f, -760.0f, 400.0f),
			Soft(150.0f, -980.0f, 260.0f),
			Soft(650.0f, -560.0f, 320.0f),
			Soft(1150.0f, -300.0f, 300.0f),
		};
		data.Pages = new[]
		{
			new PinnedPage(new Rect2(-1650.0f, -1000.0f, 1250.0f, 1200.0f), -0.8f, PaperKind.Grid, 2, 43),
			new PinnedPage(new Rect2(-100.0f, -1250.0f, 1750.0f, 1450.0f), 0.6f, PaperKind.Grid, 2, 44),
			// Over the gap, overlapping both: the bridge is drawn on it.
			new PinnedPage(new Rect2(-450.0f, -1000.0f, 560.0f, 380.0f), 1.6f, PaperKind.Plain, 1, 45),
		};
		data.SpawnPoints = new[] { new Vector2(-1350.0f, -200.0f) };
		return new TargetCourse
		{
			Name = "Medium",
			Id = "medium",
			Stage = data,
			Targets = new[]
			{
				new Vector2(-1400.0f, -75.0f),
				new Vector2(-1100.0f, -350.0f),
				new Vector2(-650.0f, -600.0f),
				// Down in the gap, just under the floor: jump out over it, hit it, jump back.
				new Vector2(-250.0f, 60.0f),
				new Vector2(-150.0f, -860.0f),
				new Vector2(280.0f, -1100.0f),
				new Vector2(400.0f, -400.0f),
				new Vector2(810.0f, -660.0f),
				new Vector2(1300.0f, -400.0f),
				// Out past the end, over nothing.
				new Vector2(1650.0f, -200.0f),
			},
		};
	}

	/// <summary>
	/// Three islands over the door. In the middle one a box you get into only by dropping through
	/// its lid, and a high ledge over it; on the far one a high perch. Both are 470 to 550 up - the
	/// up special finishes the climb for anyone whose jumps do not. Targets hang down in both gaps
	/// and out past the far end.
	/// </summary>
	static TargetCourse Hard()
	{
		StageData data = Sheet("Hard", new Rect2(-2400.0f, -1800.0f, 4800.0f, 2600.0f));
		data.Platforms = new[]
		{
			Solid(-1700.0f, 0.0f, 600.0f, 60.0f),
			Solid(-650.0f, 150.0f, 1300.0f, 60.0f),
			Solid(1100.0f, -150.0f, 600.0f, 60.0f),
			// The box: two walls and a lid to drop through - the only way in.
			Solid(-420.0f, -250.0f, 60.0f, 400.0f),
			Solid(360.0f, -250.0f, 60.0f, 400.0f),
			Soft(-360.0f, -250.0f, 720.0f),
			// High over the box, 550 above its lid. Wide, because Circy's grappling hook flings him
			// sideways as it lifts him.
			Soft(-300.0f, -800.0f, 600.0f),
			Soft(-700.0f, -560.0f, 220.0f),
			Soft(500.0f, -420.0f, 240.0f),
			// The far perch, 470 above its island - wide for the same reason.
			Soft(1150.0f, -620.0f, 500.0f),
		};
		data.Pages = new[]
		{
			new PinnedPage(new Rect2(-1800.0f, -700.0f, 800.0f, 900.0f), -1.2f, PaperKind.Grid, 1, 46),
			new PinnedPage(new Rect2(-750.0f, -1300.0f, 1500.0f, 1600.0f), 0.5f, PaperKind.Grid, 2, 47),
			new PinnedPage(new Rect2(1000.0f, -1250.0f, 800.0f, 1400.0f), 1.4f, PaperKind.Grid, 1, 48),
		};
		data.SpawnPoints = new[] { new Vector2(-1550.0f, -200.0f) };
		return new TargetCourse
		{
			Name = "Hard",
			Id = "hard",
			Stage = data,
			Targets = new[]
			{
				new Vector2(-1250.0f, -75.0f),
				// In the first gap, on the way across.
				new Vector2(-875.0f, 20.0f),
				// In the box.
				new Vector2(0.0f, 60.0f),
				new Vector2(0.0f, -900.0f),
				new Vector2(-590.0f, -660.0f),
				new Vector2(620.0f, -700.0f),
				// Down in the second gap, under the islands: hit it and climb back out.
				new Vector2(875.0f, 300.0f),
				new Vector2(1400.0f, -720.0f),
				new Vector2(1650.0f, -230.0f),
				// Out past the end, level with the island.
				new Vector2(1900.0f, -150.0f),
			},
		};
	}
}
