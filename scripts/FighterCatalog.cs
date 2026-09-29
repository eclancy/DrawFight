using Godot;

/// <summary>
/// The fighter roster, and the only place a fighter is named or listed. Same pattern as
/// <see cref="StageCatalog"/>.
///
/// Swift and Lug are generated test scaffolding - see fighters/README.md - and are deleted once
/// real drawings replace them. Circy is the first kid-designed fighter, from Elim's sheet.
/// </summary>
public static class FighterCatalog
{
	public const int Count = 3;

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
			default: cache[i] = FighterData.PlaceholderLight(); break;
		}
		return cache[i];
	}

	public static string NameOf(int index) => Get(index).DisplayName;

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
			case 1: return $"{weight}. Hits hard and slow. Hard to launch, hard to move.";
			case 2: return $"{weight}. Stretches tall or short. Falls slowly, jumps badly, rolls when hit.";
			default: return $"{weight}. Hits fast and light. Quick to move, easy to launch.";
		}
	}
}
