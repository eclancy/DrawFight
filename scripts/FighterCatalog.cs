using Godot;

/// <summary>
/// The fighter roster, and the only place a fighter is named or listed. Same pattern as
/// <see cref="StageCatalog"/>.
///
/// Every entry here is currently generated test scaffolding - see fighters/README.md. When real
/// drawings arrive they are added here and the stick figures are deleted.
/// </summary>
public static class FighterCatalog
{
	public const int Count = 2;

	// Built once and kept. Each FighterData now carries fourteen MoveData Resources, so
	// rebuilding the roster on every lookup allocated hundreds of Godot Resources that were
	// never freed - which is what started printing "Leaked unsafe reference" on shutdown.
	// FighterData is read-only once built, so sharing an instance between players is safe.
	static readonly FighterData[] cache = new FighterData[Count];

	public static FighterData Get(int index)
	{
		int i = ((index % Count) + Count) % Count;
		if (cache[i] != null) return cache[i];

		cache[i] = i == 1 ? FighterData.PlaceholderHeavy() : FighterData.PlaceholderLight();
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
			default: return $"{weight}. Hits fast and light. Quick to move, easy to launch.";
		}
	}

	/// <summary>The four specials, for the select screen. This is what makes a fighter theirs.</summary>
	public static string SpecialsOf(int index)
	{
		FighterData data = Get(index);
		string[] names = new string[4];

		for (int i = 0; i < 4; i++)
		{
			MoveData move = data.Move(MoveSlot.NeutralSpecial + i);
			names[i] = move?.MoveName ?? "-";
		}

		return string.Join("   ", names);
	}
}
