using System.Collections.Generic;
using Godot;

/// <summary>
/// The thirty items, all about drawing and painting (Eric's call, 2026-10-10), in the families
/// Smash has: things you swing, things you throw, things you hold and use, traps, a container,
/// a summon and boosts. Built once and cached, like the fighter and stage catalogs.
///
/// The art is in assets/items/, drawn by tools/art/items.py, with items.json saying where the
/// hand holds each one and where its business end is.
/// </summary>
public static class ItemCatalog
{
	static List<ItemData> all;
	static Dictionary<string, ItemArt> arts;

	public static IReadOnlyList<ItemData> All => all ??= Build();

	public static int Count => All.Count;

	public static ItemData Get(int index) => All[index];

	public static int IndexOf(string id)
	{
		for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return i;
		return -1;
	}

	static List<ItemData> storyProps;

	/// <summary>
	/// Things a story puts in someone's hand that are not items: never dropped in a match and not
	/// on the item settings screen. Flambe's guitar, for his show (Eric, 2026-10-22).
	/// </summary>
	static List<ItemData> StoryProps => storyProps ??= new List<ItemData>
	{
		new ItemData
		{
			Id = "guitar", DisplayName = "Guitar", Use = ItemUse.Swing, Size = 140.0f, Swings = 1, SpawnWeight = 0.0f,
			Swing = Swing("Power Chord", AttackAnim.Slash, 8, 3, 16, 0.0f, 0.0f, 0.0f, 45.0f, 30.0f),
			Hit = Hit("Thrown Guitar", 0.0f, 0.0f, 0.0f, 45.0f),
		},
	};

	/// <summary>An item, or a story prop, by id - what a story's "hold" puts in a hand. Null if there is none.</summary>
	public static ItemData Find(string id)
	{
		int i = IndexOf(id);
		if (i >= 0) return All[i];
		foreach (ItemData prop in StoryProps) if (prop.Id == id) return prop;
		return id != null && drawn.TryGetValue(id, out ItemData doodle) ? doodle : null;
	}

	static readonly Dictionary<string, ItemData> drawn = new Dictionary<string, ItemData>();

	/// <summary>
	/// An item a player drew for a doodle brawl (DoodleSet), with its drawing: found by id like
	/// any other, but never in All - so never on the item settings screen, never dropped in an
	/// ordinary fight, and gone when the game closes. Drawn again, it replaces the one before.
	/// </summary>
	public static void AddDrawn(ItemData data, ItemArt art)
	{
		arts ??= LoadArt();
		drawn[data.Id] = data;
		arts[data.Id] = art;
	}

	/// <summary>An item's drawing, or a piece of one ("crayon_red", "pencil_stub"); null if missing.</summary>
	public static ItemArt Art(string id)
	{
		arts ??= LoadArt();
		return id != null && arts.TryGetValue(id, out ItemArt art) ? art : null;
	}

	static Dictionary<string, ItemArt> LoadArt()
	{
		var found = new Dictionary<string, ItemArt>();
		const string Dir = "res://assets/items/";
		using FileAccess file = FileAccess.Open(Dir + "items.json", FileAccess.ModeFlags.Read);
		if (file == null) return found;
		if (Json.ParseString(file.GetAsText()).AsGodotDictionary() is not Godot.Collections.Dictionary root) return found;
		foreach (var pair in root)
		{
			var entry = pair.Value.AsGodotDictionary();
			var texture = GD.Load<Texture2D>(Dir + (string)entry["texture"]);
			if (texture == null) continue;
			Vector2 Point(string key)
			{
				if (!entry.ContainsKey(key)) return texture.GetSize() * 0.5f;
				var a = (Godot.Collections.Array)entry[key];
				return new Vector2((float)a[0], (float)a[1]);
			}
			found[(string)pair.Key] = new ItemArt { Texture = texture, Grip = Point("grip"), Head = Point("head"), Size = texture.GetSize() };
		}
		return found;
	}

	/// <summary>The crayons that fly out of a crayon box, one of each.</summary>
	public static readonly string[] Crayons = { "crayon_red", "crayon_blue", "crayon_green", "crayon_yellow", "crayon_red" };

	// --- Building ---------------------------------------------------------------------

	static MoveData Hit(string name, float damage, float baseKb, float growth, float angle) => new MoveData
	{
		MoveName = name,
		Damage = damage, BaseKnockback = baseKb, KnockbackGrowth = growth,
		LaunchAngleDegrees = angle,
	};

	static MoveData Swing(string name, AttackAnim anim, int startup, int active, int endlag,
		float damage, float baseKb, float growth, float angle, float radius) => new MoveData
	{
		MoveName = name,
		Anim = anim,
		StartupFrames = startup, ActiveFrames = active, EndlagFrames = endlag,
		Damage = damage, BaseKnockback = baseKb, KnockbackGrowth = growth,
		LaunchAngleDegrees = angle,
		HitboxRadius = radius,
		// Where the item's business end is, not a fixed spot: it lands where the drawing is.
		AtItemHead = true,
		PropArt = "-",
	};

	/// <summary>
	/// The eraser's swing: heaved up overhead for most of a second (AttackAnim.EraserHeave), then
	/// slammed down in front, and it rubs out whoever it meets. The long startup is the counterplay.
	/// </summary>
	static MoveData EraserSwing()
	{
		MoveData m = Swing("Rub Out", AttackAnim.EraserHeave, 52, 5, 30, 0.0f, 0.0f, 0.0f, 40.0f, 62.0f);
		m.Erases = true;
		m.Unblockable = true;
		return m;
	}

	static readonly Color PaintBlue = new Color(0.30f, 0.52f, 0.96f);
	static readonly Color PaintOrange = new Color(0.98f, 0.55f, 0.22f);
	static readonly Color PaintPurple = new Color(0.66f, 0.40f, 0.92f);
	static readonly Color PaintGreen = new Color(0.36f, 0.78f, 0.40f);
	static readonly Color InkBlue = new Color(0.22f, 0.27f, 0.62f);

	static List<ItemData> Build() => new List<ItemData>
	{
		// --- Swung ------------------------------------------------------------------
		new ItemData
		{
			Id = "pencil", DisplayName = "Giant Pencil", Use = ItemUse.Swing, Size = 150.0f, Uses = 5, Swings = 8, Cooldown = 18,
			Blurb = "Jab with the point. It sharpens down with every hit, to a stub, then it is gone.",
			Swing = Swing("Pencil Jab", AttackAnim.Thrust, 6, 3, 14, 9.0f, 30.0f, 0.7f, 35.0f, 34.0f),
			Hit = Hit("Thrown Pencil", 8.0f, 30.0f, 0.5f, 35.0f),
		},
		new ItemData
		{
			// Rare: a huge, slow, obvious wind-up, and whoever it touches is rubbed out - the stock
			// is gone (Eric, 2026-10-15). Three swings, the fewest of the swung items, because each
			// one that lands takes a stock; it is spent on the first that does, and a miss keeps it
			// (Eric, 2026-10-20: it was gone after one swing, hit or miss).
			Id = "eraser", DisplayName = "Big Pink Eraser", Use = ItemUse.Swing, Size = 110.0f, Swings = 3, Cooldown = 30, SpawnWeight = 0.2f,
			Blurb = "Rare. Heave it up and slam it down: whoever it touches is rubbed off the page. Three tries - used up on the one that hits.",
			Swing = EraserSwing(),
			Hit = Hit("Thrown Eraser", 6.0f, 30.0f, 0.4f, 40.0f),
		},
		new ItemData
		{
			Id = "roller", DisplayName = "Paint Roller", Use = ItemUse.Swing, Size = 140.0f, Uses = 240, Swings = 8, Cooldown = 24,
			Blurb = "A wide swing. Run with it and it rolls a slippery stripe of wet paint.",
			Swing = Swing("Roller Sweep", AttackAnim.LowSweep, 8, 4, 16, 8.0f, 32.0f, 0.6f, 30.0f, 48.0f),
			Hit = Hit("Thrown Roller", 7.0f, 30.0f, 0.4f, 35.0f),
			Tint = PaintBlue, Radius = 70.0f, Duration = 240,
		},
		new ItemData
		{
			// Picked up, it stands beside him and shoots up taller than he is - and he grows to
			// measure up, for ten seconds (Eric, 2026-10-15).
			Id = "metre_stick", DisplayName = "Metre Stick", Use = ItemUse.Swing, Size = 175.0f, Swings = 4, Cooldown = 40, Duration = 600,
			Blurb = "Measures you up: it shoots up beside you and you grow to match. A huge, slow, wound-up swing that knocks people right off the stage.",
			Swing = Swing("Home Run", AttackAnim.SmashSwing, 22, 4, 26, 22.0f, 42.0f, 1.25f, 38.0f, 52.0f),
			Hit = Hit("Thrown Metre Stick", 9.0f, 34.0f, 0.5f, 35.0f),
		},
		new ItemData
		{
			Id = "paintbrush", DisplayName = "Paintbrush", Use = ItemUse.Swing, Size = 140.0f, Swings = 6, Cooldown = 40,
			Blurb = "Every swing flicks a few blobs of paint on ahead, so it hits close and far.",
			Swing = Swing("Brush Flick", AttackAnim.Slash, 6, 3, 14, 7.0f, 28.0f, 0.55f, 45.0f, 40.0f),
			Hit = Hit("Thrown Paintbrush", 6.0f, 28.0f, 0.4f, 40.0f),
			Shot = new MoveData
			{
				MoveName = "Paint Blob",
				Damage = 3.0f, BaseKnockback = 16.0f, KnockbackGrowth = 0.2f, LaunchAngleDegrees = 30.0f,
				Special = SpecialKind.Projectile, SpecialSpeed = 900.0f, SpecialGravity = 900.0f, SpecialLifetime = 40,
				FxColor = PaintOrange, FxRadius = 11.0f, FxPaint = true,
			},
			Tint = PaintOrange,
		},
		new ItemData
		{
			Id = "scissors", DisplayName = "Safety Scissors", Use = ItemUse.Swing, Size = 95.0f, Uses = 3, Swings = 5, Cooldown = 30,
			Blurb = "Snip! Whoever it catches is cut out flat as paper and flutters, stunned.",
			Swing = Swing("Snip", AttackAnim.Thrust, 5, 2, 12, 5.0f, 18.0f, 0.15f, 60.0f, 36.0f),
			Hit = Hit("Thrown Scissors", 6.0f, 28.0f, 0.4f, 40.0f),
		},
		new ItemData
		{
			Id = "stamp", DisplayName = "Giant Rubber Stamp", Use = ItemUse.Stamp, Size = 110.0f, Duration = 480, Cooldown = 14,
			Blurb = "For eight seconds all you can do is stamp - press attack - and every stamp hits hard.",
			Swing = Swing("Stamp", AttackAnim.StampDown, 10, 3, 12, 12.0f, 36.0f, 0.75f, 70.0f, 50.0f),
			Hit = Hit("Thrown Stamp", 8.0f, 30.0f, 0.5f, 40.0f),
		},

		// --- Thrown -----------------------------------------------------------------
		new ItemData
		{
			Id = "paint_bucket", DisplayName = "Paint Bucket", Use = ItemUse.Throw, Impact = ItemImpact.Splash, Size = 80.0f,
			Blurb = "Bursts in a big splash of paint and leaves a slippery puddle on the floor.",
			Hit = Hit("Paint Splash", 10.0f, 40.0f, 0.6f, 70.0f), Radius = 125.0f, Duration = 300, Tint = PaintBlue,
		},
		new ItemData
		{
			Id = "crayon_box", DisplayName = "Crayon Box", Use = ItemUse.Throw, Impact = ItemImpact.Crayons, Size = 80.0f,
			Blurb = "The lid pops off and five crayons fan out like darts.",
			Hit = Hit("Crayon Box", 5.0f, 24.0f, 0.3f, 40.0f),
			Shot = new MoveData
			{
				MoveName = "Crayon",
				Damage = 4.0f, BaseKnockback = 20.0f, KnockbackGrowth = 0.3f, LaunchAngleDegrees = 30.0f,
				Special = SpecialKind.Projectile, SpecialSpeed = 1000.0f, SpecialGravity = 300.0f, SpecialLifetime = 55,
				FxRadius = 14.0f, FxArtSize = 60.0f, FxColor = new Color(0.9f, 0.3f, 0.3f),
			},
		},
		new ItemData
		{
			Id = "glue_bottle", DisplayName = "Glue Bottle", Use = ItemUse.Throw, Impact = ItemImpact.Glue, Size = 80.0f,
			Blurb = "Leaves a sticky puddle. Anyone in it is stuck: they can barely shuffle or hop.",
			Hit = Hit("Glue Bottle", 4.0f, 22.0f, 0.3f, 40.0f), Radius = 150.0f, Duration = 360,
			Tint = new Color(0.97f, 0.97f, 0.94f),
		},
		new ItemData
		{
			Id = "ink_bottle", DisplayName = "Ink Bottle", Use = ItemUse.Throw, Impact = ItemImpact.Ink, Size = 70.0f,
			Blurb = "Smashes into a big cloud of blue ink that hides everyone inside it.",
			Hit = Hit("Ink Bottle", 4.0f, 22.0f, 0.3f, 40.0f), Radius = 330.0f, Duration = 360, Tint = InkBlue,
		},
		new ItemData
		{
			Id = "paper_airplane", DisplayName = "Paper Airplane", Use = ItemUse.Throw, Impact = ItemImpact.Glide, Size = 90.0f,
			Blurb = "Glides a long way, curving toward the nearest opponent.",
			Hit = Hit("Paper Airplane", 6.0f, 30.0f, 0.4f, 40.0f), ThrowSpeed = 650.0f, Gravity = 40.0f, Duration = 160,
		},
		new ItemData
		{
			Id = "paper_ball", DisplayName = "Scrunched-Up Drawing", Use = ItemUse.Throw, Impact = ItemImpact.Bounce, Size = 60.0f,
			Blurb = "Bounces along the floor and off walls, hitting whoever it bounces into.",
			Hit = Hit("Paper Ball", 7.0f, 34.0f, 0.5f, 45.0f), ThrowSpeed = 1050.0f, Gravity = 2000.0f, Duration = 240,
		},
		new ItemData
		{
			Id = "palette", DisplayName = "Paint Palette", Use = ItemUse.Throw, Impact = ItemImpact.Boomerang, Size = 90.0f,
			Blurb = "Thrown like a frisbee: it flies out and comes back, hitting both ways. Press attack to catch it.",
			Hit = Hit("Palette", 8.0f, 30.0f, 0.5f, 40.0f), ThrowSpeed = 1100.0f, Duration = 120,
		},

		// --- Held and used -----------------------------------------------------------
		new ItemData
		{
			Id = "spray_can", DisplayName = "Spray Paint Can", Use = ItemUse.Spray, Size = 75.0f, Uses = 120, Cooldown = 40,
			Blurb = "Hold attack to spray a cone of paint - lots of tiny hits, until it runs out.",
			Swing = new MoveData
			{
				MoveName = "Spray", Anim = AttackAnim.Aim,
				StartupFrames = 4, ActiveFrames = 120, EndlagFrames = 10, RehitFrames = 5,
				Damage = 1.4f, BaseKnockback = 10.0f, KnockbackGrowth = 0.05f, LaunchAngleDegrees = 30.0f,
				HitboxOffset = new Vector2(120.0f, -20.0f), HitboxRadius = 60.0f, PropArt = "-",
			},
			Hit = Hit("Thrown Spray Can", 6.0f, 28.0f, 0.4f, 40.0f), Tint = PaintPurple,
		},
		new ItemData
		{
			Id = "paint_tube", DisplayName = "Paint Tube Blaster", Use = ItemUse.ChargeShot, Size = 85.0f, Uses = 4, Cooldown = 40,
			Blurb = "Squeeze and hold to charge, then let go to fire a big paint blob.",
			Shot = new MoveData
			{
				MoveName = "Paint Shot",
				Damage = 4.0f, BaseKnockback = 22.0f, KnockbackGrowth = 0.4f, LaunchAngleDegrees = 30.0f,
				Special = SpecialKind.Projectile, SpecialSpeed = 1250.0f, SpecialGravity = 0.0f, SpecialLifetime = 60,
				FxColor = PaintGreen, FxRadius = 14.0f, FxPaint = true,
			},
			Hit = Hit("Thrown Paint Tube", 6.0f, 28.0f, 0.4f, 40.0f), Tint = PaintGreen,
		},
		new ItemData
		{
			Id = "airbrush", DisplayName = "Airbrush", Use = ItemUse.Gust, Size = 85.0f, Uses = 300, Cooldown = 20,
			Blurb = "Hold attack and it blows: everyone ahead pushed back - right off the edge - and anything thrown or shot blown away.",
			Hit = Hit("Thrown Airbrush", 6.0f, 28.0f, 0.4f, 40.0f), Radius = 340.0f, Duration = 26,
			Tint = new Color(0.62f, 0.86f, 0.98f),
		},
		new ItemData
		{
			Id = "marker", DisplayName = "Magic Marker", Use = ItemUse.Draw, Size = 85.0f, Uses = 3, Cooldown = 45,
			Blurb = "Draw your own platform - for five seconds. Dashed, so you can drop through.",
			Hit = Hit("Thrown Marker", 6.0f, 28.0f, 0.4f, 40.0f), Radius = 230.0f, Duration = 300, Tint = PaintPurple,
		},
		new ItemData
		{
			Id = "foil_shield", DisplayName = "Tinfoil Shield", Use = ItemUse.Shield, Size = 125.0f,
			Blurb = "Hold attack (or block) to crouch behind it. Hits from the front are blocked, and anything shot at you bounces straight back.",
			Hit = Hit("Thrown Shield", 7.0f, 30.0f, 0.45f, 40.0f),
		},

		// --- Traps and explosives ----------------------------------------------------
		new ItemData
		{
			Id = "glitter_bomb", DisplayName = "Glitter Bomb", Use = ItemUse.Throw, Impact = ItemImpact.Blast, Size = 70.0f,
			Blurb = "Explodes in a huge sparkly blast. Left lying about, it goes off by itself.",
			Hit = Hit("Glitter Blast", 18.0f, 45.0f, 0.85f, 50.0f), Radius = 150.0f, Duration = 480,
			Tint = new Color(0.98f, 0.55f, 0.85f),
		},
		new ItemData
		{
			Id = "paint_balloon", DisplayName = "Paint Balloon", Use = ItemUse.Throw, Impact = ItemImpact.Mine, Size = 65.0f,
			Blurb = "Sits where it lands and bursts in a splash when anyone comes near.",
			Hit = Hit("Paint Balloon", 12.0f, 38.0f, 0.7f, 75.0f), Radius = 110.0f, Duration = 600,
			Tint = new Color(0.96f, 0.36f, 0.26f),
		},
		new ItemData
		{
			Id = "thumbtacks", DisplayName = "Thumbtacks", Use = ItemUse.Throw, Impact = ItemImpact.Tacks, Size = 65.0f,
			Blurb = "Scatter across the floor. Anyone who walks on them hops and yelps.",
			Hit = Hit("Thumbtack", 4.0f, 34.0f, 0.2f, 88.0f), Radius = 110.0f, Duration = 360,
		},
		new ItemData
		{
			Id = "rinse_cup", DisplayName = "Rinse-Water Cup", Use = ItemUse.Throw, Impact = ItemImpact.Spill, Size = 70.0f,
			Blurb = "Spills on the floor. Run into the puddle and you slip and fall over.",
			Hit = new MoveData
			{
				MoveName = "Slipped", Damage = 2.0f, BaseKnockback = 50.0f, KnockbackGrowth = 0.05f,
				LaunchAngleDegrees = 88.0f, SpinVictim = true, Unblockable = true,
			},
			Radius = 95.0f, Duration = 420, Tint = new Color(0.66f, 0.82f, 0.90f),
		},
		new ItemData
		{
			Id = "popup_book", DisplayName = "Pop-Up Book", Use = ItemUse.Throw, Impact = ItemImpact.Spring, Size = 90.0f,
			Blurb = "Opens on the floor into one of three huge pop-ups: a bouncy castle, a snapping crocodile, or a storm cloud.",
			Hit = Hit("Pop-Up Book", 5.0f, 24.0f, 0.3f, 40.0f), Duration = 420, Amount = 1750.0f,
		},

		// --- Container and summon ------------------------------------------------------
		new ItemData
		{
			Id = "pencil_case", DisplayName = "Pencil Case", Use = ItemUse.Open, Size = 90.0f,
			Blurb = "Press attack next to it and it spills out two or three items.",
		},
		new ItemData
		{
			Id = "canvas", DisplayName = "Blank Canvas", Use = ItemUse.Consume, Boost = ItemBoost.Canvas, Size = 100.0f, Duration = 480,
			Blurb = "A drawing climbs out and fights on your side for eight seconds.",
			Hit = Hit("Drawing", 7.0f, 34.0f, 0.55f, 45.0f),
		},

		// --- Boosts ---------------------------------------------------------------------
		new ItemData
		{
			Id = "gold_star", DisplayName = "Gold Star Sticker", Use = ItemUse.Consume, Boost = ItemBoost.Star, Size = 70.0f,
			Duration = 480, TouchToCollect = true,
			Blurb = "Hops about. Catch it and nothing can hurt you for eight seconds.",
		},
		new ItemData
		{
			Id = "white_out", DisplayName = "White-Out", Use = ItemUse.Consume, Boost = ItemBoost.Heal, Size = 65.0f, Amount = 25.0f,
			Blurb = "Paints over your mistakes: takes 25% of your damage away.",
		},
		new ItemData
		{
			Id = "magnifying_glass", DisplayName = "Magnifying Glass", Use = ItemUse.Consume, Boost = ItemBoost.Giant, Size = 85.0f,
			Duration = 600,
			Blurb = "You are drawn giant for ten seconds: bigger, heavier, and you hit harder.",
		},
		new ItemData
		{
			Id = "tracing_paper", DisplayName = "Tracing Paper", Use = ItemUse.Consume, Boost = ItemBoost.Ghost, Size = 90.0f,
			Duration = 480,
			Blurb = "You go see-through for eight seconds: still knocked about, but you take no damage.",
		},
	};
}

/// <summary>How often items turn up in a match.</summary>
public enum ItemFrequency { Off, Low, Medium, High }

/// <summary>
/// The item settings: how often they appear, and which of them can. Kept on GameRoot with the
/// other choices and saved to user://settings.cfg, so they stay set between games.
/// </summary>
public sealed class ItemSettings
{
	public ItemFrequency Frequency = ItemFrequency.Medium;
	public readonly bool[] Enabled;

	const string Path = "user://settings.cfg";

	public ItemSettings()
	{
		Enabled = new bool[ItemCatalog.Count];
		for (int i = 0; i < Enabled.Length; i++) Enabled[i] = true;
	}

	public int EnabledCount
	{
		get
		{
			int n = 0;
			foreach (bool on in Enabled) if (on) n++;
			return n;
		}
	}

	/// <summary>Frames between items appearing, and how many can be out at once.</summary>
	// Lowered across the board (Eric, 2026-10-15): items are a seasoning on the fight, not the
	// fight - medium now drops one about every thirteen seconds.
	public int SpawnInterval => Frequency switch
	{
		ItemFrequency.Low => 1320,
		ItemFrequency.High => 420,
		_ => 780,
	};

	public int MaxOnStage => Frequency switch
	{
		ItemFrequency.Low => 1,
		ItemFrequency.High => 3,
		_ => 2,
	};

	public static string Describe(ItemFrequency frequency) => frequency switch
	{
		ItemFrequency.Off => "Off",
		ItemFrequency.Low => "Low",
		ItemFrequency.High => "High",
		_ => "Medium",
	};

	public void Load()
	{
		var config = new ConfigFile();
		if (config.Load(Path) != Error.Ok) return;
		Frequency = (ItemFrequency)(int)config.GetValue("items", "frequency", (int)ItemFrequency.Medium);
		for (int i = 0; i < Enabled.Length; i++)
		{
			Enabled[i] = (bool)config.GetValue("items", ItemCatalog.Get(i).Id, true);
		}
	}

	public void Save()
	{
		// Online, the item settings are the host's for the session.
		if (NetSession.Playing) return;
		var config = new ConfigFile();
		config.Load(Path);
		config.SetValue("items", "frequency", (int)Frequency);
		for (int i = 0; i < Enabled.Length; i++) config.SetValue("items", ItemCatalog.Get(i).Id, Enabled[i]);
		config.Save(Path);
	}
}
