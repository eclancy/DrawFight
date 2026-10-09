using Godot;

/// <summary>
/// The fighter roster, and the only place a fighter is named or listed. Same pattern as
/// <see cref="StageCatalog"/>.
///
/// Swift and Lug are generated test scaffolding - see fighters/README.md - and are deleted once
/// real drawings replace them. Circy is the first kid-designed fighter, from Elim's sheet.
/// EdgeLord plays with placeholder colouring and move art until his own drawings arrive.
/// Triguy is Elim's second, from a sheet sent in through ecec.dev/draw.
/// </summary>
public static class FighterCatalog
{
	public const int Count = 6;

	// Built once and kept. Each FighterData now carries seventeen MoveData Resources, so
	// rebuilding the roster on every lookup allocated hundreds of Godot Resources that were
	// never freed - which is what started printing "Leaked unsafe reference" on shutdown.
	// FighterData is read-only once built, so sharing an instance between players is safe.
	static readonly FighterData[] cache = new FighterData[Count];

	public static FighterData Get(int index)
	{
		int i = ((index % Count) + Count) % Count;
		if (cache[i] != null) return cache[i];

		switch (i)
		{
			case 1: cache[i] = FighterData.PlaceholderHeavy(); break;
			case 2: cache[i] = FighterData.Circy(); break;
			case 3: cache[i] = FighterData.EdgeLord(); break;
			case 4: cache[i] = FighterData.DoomBot(); break;
			case 5: cache[i] = FighterData.Triguy(); break;
			default: cache[i] = FighterData.PlaceholderLight(); break;
		}
		return cache[i];
	}

	public static string NameOf(int index) => Get(index).DisplayName;

	/// <summary>
	/// Who drew this fighter, shown beside its name on the select screen: the drawing is the point
	/// of the whole game, and the kid who drew it should see their name on it (Eric's call,
	/// 2026-10-04).
	/// </summary>
	public static string CreditOf(int index)
	{
		FighterData data = Get(index);
		return string.IsNullOrEmpty(data.Artist) ? "a computer stand-in" : $"drawn by {data.Artist}";
	}

	/// <summary>
	/// A one-line description of what this fighter is good and bad at, for the select screen.
	/// Every fighter is clearly bad at something - see the weakness rule in
	/// .ai/character-design.md - and the select screen is where a player finds that out.
	/// </summary>
	public static string BlurbOf(int index)
	{
		FighterData data = Get(index);
		string weight = WeightProfiles.Describe(data.Weight);

		switch (((index % Count) + Count) % Count)
		{
			case 1: return $"{weight}. A construction worker with site tools. Big swings shrug off small hits; hard to launch.";
			case 2: return $"{weight}. Stretches tall for big slow hits, or short for small fast ones. Rolls when hit.";
			case 3: return $"{weight}. Super fast, with endless swords. Air-dashes instead of a second jump. Easy to launch.";
			case 4: return $"{weight}. A giant rogue factory robot. Heats up as he fights, then lets it all out.";
			case 5: return $"{weight}. A really fast triangle in a top hat. Can't stop: he skids, and his block is weak.";
			default: return $"{weight}. A fire-punk chef: fireballs, a fireball roll, wings of flame and fire rain. Quick, easy to launch.";
		}
	}
}
