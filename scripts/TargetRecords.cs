using Godot;

/// <summary>
/// The best time on every targets course, for every fighter (Eric, 2026-10-29): kept in
/// user://targets.cfg, a section per course and a key per fighter's story id, as a frame count -
/// the clock counts frames like everything else, and is only turned into minutes and seconds to
/// be shown.
/// </summary>
public static class TargetRecords
{
	const string Path = "user://targets.cfg";

	static ConfigFile config;

	static ConfigFile Config()
	{
		if (config != null) return config;
		config = new ConfigFile();
		config.Load(Path);
		return config;
	}

	/// <summary>His best time on the course, in frames, or -1 for none yet.</summary>
	public static int Best(FighterData fighter, string course) =>
		fighter == null ? -1 : (int)Config().GetValue(course, StoryScript.IdOf(fighter), -1);

	/// <summary>A finished run: kept if it is his best on the course. True when it is.</summary>
	public static bool Submit(FighterData fighter, string course, int frames)
	{
		int was = Best(fighter, course);
		if (fighter == null || was >= 0 && was <= frames) return false;
		Config().SetValue(course, StoryScript.IdOf(fighter), frames);
		Config().Save(Path);
		return true;
	}

	/// <summary>A frame count as a stopwatch reads it: "0:23.45".</summary>
	public static string Clock(int frames)
	{
		frames = Mathf.Max(0, frames);
		int seconds = frames / 60;
		int hundredths = frames % 60 * 100 / 60;
		return $"{seconds / 60}:{seconds % 60:00}.{hundredths:00}";
	}
}
