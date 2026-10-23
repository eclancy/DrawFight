using System.Collections.Generic;
using Godot;

/// <summary>One thing to do, and what it earns - if anything.</summary>
public class Achievement
{
	public string Id = "";
	public string Title = "";

	/// <summary>What to do, said so it is also a clue - to a move, a mode, a trick.</summary>
	public string Task = "";

	/// <summary>
	/// Hard ones are for someone who has played a lot: they get a gold border. The rest are for
	/// anyone who plays.
	/// </summary>
	public bool Hard;

	/// <summary>The fighter it unlocks (story id), or "".</summary>
	public string Fighter = "";

	/// <summary>The skin it unlocks (Skins), or "".</summary>
	public string Skin = "";

	/// <summary>Counted up toward, rather than done at once: how many, and what is counted.</summary>
	public int Goal;
	public string Counter = "";
}

/// <summary>
/// Thirty achievements (Eric, 2026-10-23): some hard - a story on Hard - some just fun, and many
/// a clue to something a player might never find on their own: catching the palette on its way
/// back, the three taunts, dropping through a dashed platform, a three-on-one. Listed on the
/// Achievements page (AchievementsScreen) with what to do, but not what they give - except that a
/// fighter or a skin still to be earned shows as a silhouette. A few unlock things: beating Lugnut
/// and DoomBot in a story unlocks them (Unlocks), and some unlock a skin (Skins).
///
/// Only a person playing earns one - never the computer, never a story scene acting for them.
/// Saved in user://achievements.cfg; --locked plays this run on an empty save, and saves nothing.
/// </summary>
public static class Achievements
{
	public static readonly Achievement[] All =
	{
		// --- Stories ---
		new Achievement { Id = "story_first", Title = "Fridge Famous", Task = "Finish a story." },
		new Achievement { Id = "story_all", Title = "Every Drawing's Story", Task = "Finish every fighter's story.", Hard = true },
		new Achievement { Id = "story_hard", Title = "Hard as Nails", Task = "Finish a story on Hard.", Hard = true },
		new Achievement { Id = "story_doombot_hard", Title = "Factory Reset", Task = "Finish DoomBot's own story on Hard.", Hard = true, Skin = "mk2" },
		new Achievement { Id = "beat_lugnut", Title = "Break's Over", Task = "Beat Lugnut in a story.", Fighter = "lugnut" },
		new Achievement { Id = "beat_doombot", Title = "Off the Fridge", Task = "Beat DoomBot in a story.", Fighter = "doombot" },
		new Achievement { Id = "finale_flawless", Title = "Not a Scratch", Task = "Beat DoomBot at the end of a story without losing a life.", Hard = true, Skin = "shadow" },
		new Achievement { Id = "two_on_one", Title = "Buddy System Busted", Task = "Win a two-on-one fight in a story without losing a life." },

		// --- Versus ---
		new Achievement { Id = "vs_win", Title = "First Win", Task = "Win a versus fight." },
		new Achievement { Id = "vs_four", Title = "Full House", Task = "Play a versus fight with four fighters." },
		new Achievement { Id = "vs_3v1", Title = "Odd One Out", Task = "Play a team fight, three against one." },
		new Achievement { Id = "vs_team_win", Title = "Teamwork", Task = "Win a team fight." },
		new Achievement { Id = "vs_all_stages", Title = "Tour of the Fridge", Task = "Fight on every stage.", Counter = "stages" },
		new Achievement { Id = "vs_all_fighters", Title = "Know Your Roster", Task = "Win a versus fight with every fighter.", Hard = true, Counter = "won_with" },
		new Achievement { Id = "vs_hard_cpu", Title = "Beat the Machine", Task = "Beat a Hard CPU in versus." },
		new Achievement { Id = "vs_clutch", Title = "Clutch", Task = "Win on your last life with 150% or more.", Hard = true },

		// --- Things to find out ---
		new Achievement { Id = "catch_palette", Title = "Boomerang", Task = "Throw the palette and catch it in the air on its way back." },
		new Achievement { Id = "shield_break", Title = "Crack!", Task = "Keep hitting someone's block until it breaks." },
		new Achievement { Id = "ledge_grab", Title = "Hang On!", Task = "Grab the edge of the stage." },
		new Achievement { Id = "drop_through", Title = "Through the Floor", Task = "Drop down through a dashed platform." },
		new Achievement { Id = "meteor", Title = "Meteor", Task = "Knock someone off the bottom of the stage." },
		new Achievement { Id = "full_charge", Title = "Fully Charged", Task = "Hit with a smash attack held until fully charged." },
		new Achievement { Id = "all_throws", Title = "Grab Bag", Task = "Throw someone forward, back, up and down.", Counter = "throws", Goal = 4 },
		new Achievement { Id = "eraser", Title = "Rubbed Out", Task = "Rub someone off the page with the Big Pink Eraser." },
		new Achievement { Id = "taunts", Title = "Show-Off", Task = "Do all three of a fighter's taunts: up, sideways and down.", Counter = "taunts" },
		new Achievement { Id = "overheat", Title = "Meltdown", Task = "Keep DoomBot at full heat until he overheats." },
		new Achievement { Id = "circy_tall", Title = "Growth Spurt", Task = "Stretch Circy as tall as he goes." },
		new Achievement { Id = "hot_stuff", Title = "Hot Stuff", Task = "Set fighters on fire 25 times.", Counter = "burns", Goal = 25, Skin = "blueflame" },
		new Achievement { Id = "ko_50", Title = "Demolition Crew", Task = "Knock out 50 fighters.", Hard = true, Counter = "kos", Goal = 50, Skin = "safety" },

		// --- Making things ---
		new Achievement { Id = "draw_send", Title = "Fridge Artist", Task = "Draw something in the game and send it in." },
	};

	const string Path = "user://achievements.cfg";

	static ConfigFile config;

	static ConfigFile Config()
	{
		if (config != null) return config;
		config = new ConfigFile();
		if (!Unlocks.ForceLocked) config.Load(Path);
		// --locked plays on an empty save: nothing carried over from the stories either.
		if (!config.HasSectionKey("save", "version") && !Unlocks.ForceLocked) EarnedBefore();
		return config;
	}

	static void Save()
	{
		if (!Unlocks.ForceLocked) Config().Save(Path);
	}

	public static Achievement Find(string id) => System.Array.Find(All, a => a.Id == id);

	public static bool Earned(string id) => (bool)Config().GetValue("earned", id, false);

	public static int EarnedCount => System.Array.FindAll(All, a => Earned(a.Id)).Length;

	/// <summary>Done: saved, its fighter unlocked, and a note on screen to say so. Once.</summary>
	public static void Grant(string id)
	{
		Achievement a = Find(id);
		if (a == null || Earned(id)) return;
		Config().SetValue("earned", id, true);
		Save();
		if (a.Fighter != "")
		{
			int index = StoryScript.IndexOf(a.Fighter);
			if (index >= 0) Unlocks.Unlock(FighterCatalog.Get(index));
		}
		GameRoot.Instance?.ToastAchievement(a);
	}

	/// <summary>One more toward a counted achievement - granted when the count reaches its goal.</summary>
	public static void Count(string id, int by = 1)
	{
		Achievement a = Find(id);
		if (a == null || Earned(id)) return;
		int now = (int)Config().GetValue("count", a.Counter, 0) + by;
		Config().SetValue("count", a.Counter, now);
		Save();
		if (now >= a.Goal) Grant(id);
	}

	/// <summary>
	/// One more of a set ticked off - stages fought on, fighters won with, throws done - granted
	/// once the set has <paramref name="goal"/> in it.
	/// </summary>
	public static void Tick(string id, string item, int goal)
	{
		Achievement a = Find(id);
		if (a == null || Earned(id)) return;
		var set = new HashSet<string>(((string)Config().GetValue("sets", a.Counter, "")).Split(',', System.StringSplitOptions.RemoveEmptyEntries));
		if (set.Add(item))
		{
			Config().SetValue("sets", a.Counter, string.Join(",", set));
			Save();
		}
		if (set.Count >= goal) Grant(id);
	}

	/// <summary>Whether every one of <paramref name="items"/> has been ticked off toward it.</summary>
	public static bool TickedAll(string id, string[] items)
	{
		Achievement a = Find(id);
		if (a == null) return false;
		var set = new HashSet<string>(((string)Config().GetValue("sets", a.Counter, "")).Split(',', System.StringSplitOptions.RemoveEmptyEntries));
		return System.Array.TrueForAll(items, set.Contains);
	}

	/// <summary>How far along a counted or ticked achievement is, as "12 / 25", or "".</summary>
	public static string Progress(Achievement a)
	{
		if (Earned(a.Id) || a.Counter == "" || a.Counter == "taunts") return "";
		if (a.Goal > 0 && a.Counter != "throws")
		{
			return $"{(int)Config().GetValue("count", a.Counter, 0)} / {a.Goal}";
		}
		string ticked = (string)Config().GetValue("sets", a.Counter, "");
		int have = ticked.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Length;
		int goal = a.Counter switch { "stages" => StageCatalog.Count, "won_with" => FighterCatalog.Count, _ => a.Goal };
		return $"{have} / {goal}";
	}

	// --- Who counts ------------------------------------------------------------------------

	/// <summary>A person, playing - not the computer, and not a story scene moving them about.</summary>
	public static bool ByPlayer(Fighter fighter) =>
		fighter != null && fighter.Match != null && !fighter.Match.InStoryScene && fighter.Match.IsHuman(fighter.PlayerIndex);

	/// <summary>
	/// A save from before there were achievements: the stories it has already finished, and the
	/// fighters they beat, count - so nobody loses what they had done.
	/// </summary>
	static void EarnedBefore()
	{
		int finished = 0, withStory = 0;
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			string story = StoryScript.IdOf(FighterCatalog.Get(i));
			if (StoryRun.For(story).Chapters.Count == 0) continue;
			withStory++;
			if (StoryProgress.Finished(story)) finished++;
		}
		if (finished > 0) config.SetValue("earned", "story_first", true);
		if (withStory > 0 && finished == withStory) config.SetValue("earned", "story_all", true);
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			FighterData data = FighterCatalog.Get(i);
			if (!data.Unlockable || Unlocks.IsLocked(data)) continue;
			config.SetValue("earned", "beat_" + StoryScript.IdOf(data), true);
		}
		config.SetValue("save", "version", 1);
		Save();
	}
}
