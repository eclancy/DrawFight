using Godot;

/// <summary>Another look for a fighter, earned with an achievement.</summary>
public class Skin
{
	public string Id = "";

	/// <summary>Whose it is, by story id.</summary>
	public string Fighter = "";

	public string Name = "";

	/// <summary>
	/// His colour turned to this one, the way a team fight turns it (TeamLook): only the colouring
	/// in changes, never a line. Unused by a skin that is a fighter of its own.
	/// </summary>
	public Color Colour;

	/// <summary>A fighter of its own wearing him, by catalog index - DoomBot Mk. II's armour - or -1.</summary>
	public int Becomes = -1;

	/// <summary>The achievement that earns it.</summary>
	public string Earned = "";
}

/// <summary>
/// Alternate skins (Eric, 2026-10-23), each earned by an achievement and picked on the fighter
/// select screen by picking the fighter again. DoomBot's is Mk. II's scrap armour, earned by
/// finishing his own story on Hard; the rest are recolourings of the fighters Eric drew and the
/// two we drew, done as a team fight colours them. The kids' own fighters have none yet: a new
/// colour on Elim's drawings is his to ask for.
/// </summary>
public static class Skins
{
	public static readonly Skin[] All =
	{
		new Skin { Id = "mk2", Fighter = "doombot", Name = "Mk. II", Becomes = FighterCatalog.StoryOnlyFrom, Earned = "story_doombot_hard" },
		new Skin { Id = "blueflame", Fighter = "flambe", Name = "Blue Flame", Colour = new Color(0.25f, 0.55f, 1.0f), Earned = "hot_stuff" },
		new Skin { Id = "safety", Fighter = "lugnut", Name = "Safety Green", Colour = new Color(0.45f, 0.85f, 0.25f), Earned = "ko_50" },
		new Skin { Id = "shadow", Fighter = "edgelord", Name = "Shadow", Colour = new Color(0.58f, 0.32f, 0.92f), Earned = "finale_flawless" },
	};

	public static Skin Find(string id) => System.Array.Find(All, s => s.Id == id);

	/// <summary>A fighter's skins that have been earned, in order.</summary>
	public static Skin[] EarnedFor(FighterData data)
	{
		string id = StoryScript.IdOf(data);
		return System.Array.FindAll(All, s => s.Fighter == id && Achievements.Earned(s.Earned));
	}

	/// <summary>The skin after this one for him, round to none ("") and on - only earned ones.</summary>
	public static string Next(FighterData data, string current)
	{
		Skin[] mine = EarnedFor(data);
		if (mine.Length == 0) return "";
		int at = System.Array.FindIndex(mine, s => s.Id == current);
		return at + 1 < mine.Length ? mine[at + 1].Id : "";
	}
}
