using System.Collections.Generic;
using Godot;

/// <summary>
/// One step of a story: a fight, with the scene before it and the scene after - or a scene with
/// no fight, an opening or an ending. Read from the "== chapters" list in a fighter's story.txt.
/// </summary>
public class StoryChapter
{
	/// <summary>What the chapter list said: "fight edgelord triguy at rooftops".</summary>
	public string Line = "";

	/// <summary>Shown on the story map: "Opening", "Lugnut", "EdgeLord + Triguy", "DoomBot", "Ending".</summary>
	public string Title = "";

	public int Stage;

	/// <summary>Who else is in the match, by story id. In a fight, every one of them is against the player.</summary>
	public string[] Opponents = System.Array.Empty<string>();

	/// <summary>A fight, or just a scene (an opening, an ending).</summary>
	public bool Fight;

	/// <summary>The scene before the fight, as "owner/scene" - or the whole of a chapter with no fight.</summary>
	public string Before = "";

	/// <summary>The scene once the fight is won, or "" to go straight on.</summary>
	public string After = "";

	/// <summary>How well the other side plays, 1 to 9 (CpuInputSource.Level).</summary>
	public int Level = 4;

	/// <summary>The boss's story id - DoomBot, Mk. II - whose last stock is a last stand; "" for none.</summary>
	public string Boss = "";
}

/// <summary>
/// One player's way through a story: whose story it is, the chapters, how far they have got.
/// Kept by GameRoot, so a lost fight is retried with the same chapter and a won one moves on.
/// </summary>
public class StoryRun
{
	public string PlayerId = "";
	public string Title = "";
	public readonly List<StoryChapter> Chapters = new List<StoryChapter>();
	public int At;

	/// <summary>How many times the current chapter has been tried, so a retry can say so.</summary>
	public int Tries;

	/// <summary>Open on the scene after the fight, for looking at it (--story=WHO:N:after).</summary>
	public bool StartAfter;

	public bool Done => At >= Chapters.Count;
	public StoryChapter Chapter => Done ? null : Chapters[At];

	/// <summary>A fighter's story, as their chapter list lays it out. Empty if they have none.</summary>
	public static StoryRun For(string playerId)
	{
		var run = new StoryRun { PlayerId = playerId };
		List<string> problems = new List<string>();
		foreach (StoryChapter chapter in StoryChapters.Read(playerId, problems, out run.Title)) run.Chapters.Add(chapter);
		foreach (string problem in problems) GD.PushError($"StoryRun: {playerId}: {problem}");
		return run;
	}
}

/// <summary>
/// Reads a fighter's "== chapters" list. Each line is a chapter:
///
///   title The Headliner                    the story's name, shown on its map
///   opening [with WHO ...] [at STAGE]      a scene only: "== opening" in their own story.txt
///   fight WHO [WHO] [at STAGE] [level N] [boss]
///   finale                                 DoomBot, on the fridge: the shared finale and its end
///   ending [with WHO ...] [at STAGE]       a scene only: "== ending"
///
/// A fight's scene before is the player's own "== vs WHO [WHO]" if they wrote one, or else the
/// opponent's "== challenger" (or, for two, "== team"); its scene after is the player's
/// "== beat WHO [WHO]", or else the opponent's "== defeated" (or "== team defeated"). So a new
/// fighter joins every story with their challenger scene, without anyone else's being rewritten.
/// A player's own "== won", if they have one, plays after any fight they have no "beat" scene for
/// - DoomBot's "YOU HAVE BEEN... PROCESSED."
/// The fights get harder down the list, from level 2 to 5, and the boss plays at 6.
/// </summary>
public static class StoryChapters
{
	public static readonly string[] Stages = { "fridge", "plains", "rooftops", "bigshow" };

	public static List<StoryChapter> Read(string playerId, List<string> problems, out string title)
	{
		title = "";
		var chapters = new List<StoryChapter>();
		StoryScene list = StoryScript.Scene(playerId, "chapters");
		if (list == null) return chapters;

		foreach (string raw in list.Raw)
		{
			string[] words = raw.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
			if (words.Length == 0) continue;
			if (words[0] == "title")
			{
				title = raw.Substring(5).Trim();
				continue;
			}
			StoryChapter chapter = Parse(playerId, words, problems);
			if (chapter == null) continue;
			chapter.Line = raw;
			chapters.Add(chapter);
		}

		// Gentle to hard down the list; the boss at the top of it.
		int fights = 0;
		foreach (StoryChapter c in chapters) if (c.Fight && c.Boss == "") fights++;
		int i = 0;
		foreach (StoryChapter c in chapters)
		{
			if (!c.Fight || c.Level != 0) continue;
			c.Level = c.Boss != "" ? 6 : 2 + Mathf.RoundToInt(3.0f * i++ / Mathf.Max(1, fights - 1));
		}
		return chapters;
	}

	static StoryChapter Parse(string player, string[] words, List<string> problems)
	{
		var chapter = new StoryChapter { Level = 0 };
		var who = new List<string>();
		string kind = words[0];
		for (int i = 1; i < words.Length; i++)
		{
			switch (words[i])
			{
				case "at":
					string stage = i + 1 < words.Length ? words[++i] : "";
					chapter.Stage = System.Array.IndexOf(Stages, stage);
					if (chapter.Stage < 0)
					{
						problems.Add($"\"{string.Join(" ", words)}\": no stage \"{stage}\" (try {string.Join(", ", Stages)})");
						chapter.Stage = 0;
					}
					break;
				case "level":
					chapter.Level = i + 1 < words.Length ? words[++i].ToInt() : 4;
					break;
				case "boss":
					chapter.Boss = "?";
					break;
				case "with":
					break;
				default:
					if (StoryScript.IndexOf(words[i]) < 0) problems.Add($"\"{string.Join(" ", words)}\": nobody called \"{words[i]}\"");
					else who.Add(words[i]);
					break;
			}
		}
		chapter.Opponents = who.ToArray();

		switch (kind)
		{
			case "opening":
			case "ending":
				chapter.Before = $"{player}/{kind}";
				chapter.Title = kind == "opening" ? "Opening" : "Ending";
				break;
			case "finale":
				chapter.Fight = true;
				chapter.Opponents = new[] { "doombot" };
				chapter.Stage = 0;
				chapter.Before = "doombot/finale";
				chapter.After = "doombot/beaten";
				chapter.Boss = "doombot";
				chapter.Title = "DoomBot";
				break;
			case "fight":
				if (who.Count == 0)
				{
					problems.Add($"\"{string.Join(" ", words)}\": a fight with nobody");
					return null;
				}
				chapter.Fight = true;
				if (chapter.Boss == "?") chapter.Boss = who[0];
				chapter.Title = string.Join(" + ", who.ConvertAll(Name));
				string them = string.Join(" ", who);
				chapter.Before = First(problems, words, $"{player}/vs {them}",
					who.Count == 1 ? $"{who[0]}/challenger" : null,
					who.Count > 1 ? $"{who[0]}/team" : null,
					who.Count > 1 ? $"{who[1]}/team" : null);
				chapter.After = First(null, words, $"{player}/beat {them}", $"{player}/won",
					who.Count == 1 ? $"{who[0]}/defeated" : $"{who[0]}/team defeated");
				break;
			default:
				problems.Add($"\"{string.Join(" ", words)}\": a chapter is opening, fight, finale or ending");
				return null;
		}
		if (!chapter.Fight && !StoryScript.Has(player, chapter.Before.Split('/')[1]))
		{
			problems.Add($"\"{string.Join(" ", words)}\": no \"== {chapter.Before.Split('/')[1]}\" in {player}'s story");
		}
		return chapter;
	}

	/// <summary>The first of these "owner/scene" names that exists; "" (and a problem, if one is asked for) if none does.</summary>
	static string First(List<string> problems, string[] words, params string[] names)
	{
		foreach (string name in names)
		{
			if (name == null) continue;
			string[] parts = name.Split('/');
			if (StoryScript.Has(parts[0], parts[1])) return name;
		}
		problems?.Add($"\"{string.Join(" ", words)}\": no scene to start it - write \"== {names[0].Split('/')[1]}\" in the player's story, or a challenger or team scene for them");
		return "";
	}

	static string Name(string id)
	{
		int index = StoryScript.IndexOf(id);
		return index < 0 ? id : FighterCatalog.Get(index).DisplayName;
	}

	/// <summary>Index into StoryChapters.Stages - and StageCatalog - by name.</summary>
	public static int StageIndex(string name) => System.Array.IndexOf(Stages, name);
}

/// <summary>
/// How far each story has got, saved between games in user://story.cfg: the next chapter, and
/// whether it has ever been finished (a gold star on "Whose story?").
/// </summary>
public static class StoryProgress
{
	const string Path = "user://story.cfg";

	static ConfigFile Load()
	{
		var config = new ConfigFile();
		config.Load(Path);
		return config;
	}

	public static int ChapterReached(string id) => (int)Load().GetValue(id, "at", 0);

	public static bool Finished(string id) => (bool)Load().GetValue(id, "finished", false);

	/// <summary>Remembers that a story got this far - or, past its last chapter, that it was finished.</summary>
	public static void Save(string id, int at, int chapters)
	{
		ConfigFile config = Load();
		if (at >= chapters)
		{
			config.SetValue(id, "finished", true);
			at = 0;
		}
		config.SetValue(id, "at", at);
		config.Save(Path);
	}
}
