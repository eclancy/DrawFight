using Godot;

/// <summary>
/// Fighters you have to earn (Eric, 2026-10-23): DoomBot and Lugnut start locked
/// (FighterData.Unlockable). A locked fighter is a silhouette on the select screens and cannot be
/// picked, in versus or for a story. The first time a story puts you up against one, a NEW
/// CHALLENGER screen comes before the scene (ChallengerScreen); beat them, and they are unlocked
/// for good, with a screen to say so. Saved in user://unlocks.cfg, by story id. Achievements may
/// unlock things later (.ai/roadmap.md).
/// </summary>
public static class Unlocks
{
	const string Path = "user://unlocks.cfg";

	/// <summary>
	/// Every unlockable treated as locked and nothing saved, for this run (--locked): to look at the
	/// locked screens and the unlocking without touching the real save.
	/// </summary>
	public static bool ForceLocked;

	static ConfigFile config;

	/// <summary>Unlocked and met this run - kept here too, so --locked, which saves nothing, still moves on.</summary>
	static readonly System.Collections.Generic.HashSet<string> earned = new System.Collections.Generic.HashSet<string>();
	static readonly System.Collections.Generic.HashSet<string> met = new System.Collections.Generic.HashSet<string>();

	static ConfigFile Config()
	{
		if (config != null) return config;
		config = new ConfigFile();
		config.Load(Path);
		if (!config.HasSectionKey("save", "version")) EarnedBefore();
		return config;
	}

	static void Save()
	{
		if (!ForceLocked) Config().Save(Path);
	}

	/// <summary>Whether this fighter still has to be earned.</summary>
	public static bool IsLocked(FighterData data) =>
		data != null && data.Unlockable && !earned.Contains(StoryScript.IdOf(data))
		&& (ForceLocked || !(bool)Config().GetValue("unlocked", StoryScript.IdOf(data), false));

	public static bool IsLocked(int catalogIndex) => IsLocked(FighterCatalog.Get(catalogIndex));

	/// <summary>Earned: theirs to pick from now on.</summary>
	public static void Unlock(FighterData data)
	{
		if (data == null) return;
		earned.Add(StoryScript.IdOf(data));
		Config().SetValue("unlocked", StoryScript.IdOf(data), true);
		Save();
	}

	/// <summary>Whether their NEW CHALLENGER screen has been shown - it is, once.</summary>
	public static bool ChallengerSeen(FighterData data) =>
		met.Contains(StoryScript.IdOf(data)) || !ForceLocked && (bool)Config().GetValue("challenger", StoryScript.IdOf(data), false);

	public static void SeeChallenger(FighterData data)
	{
		met.Add(StoryScript.IdOf(data));
		Config().SetValue("challenger", StoryScript.IdOf(data), true);
		Save();
	}

	/// <summary>
	/// The first locked fighter a story chapter puts you up against, or null. Only a fight counts:
	/// someone who only stands about in an opening or an ending has not been met.
	/// </summary>
	public static FighterData LockedIn(StoryChapter chapter)
	{
		if (chapter == null || !chapter.Fight) return null;
		foreach (string id in chapter.Opponents)
		{
			int index = StoryScript.IndexOf(id);
			if (index >= 0 && IsLocked(index)) return FighterCatalog.Get(index);
		}
		return null;
	}

	/// <summary>
	/// A save from before there was anything to unlock: whoever its stories have already beaten is
	/// unlocked - a story played past a chapter that fought them, or finished - so nobody loses a
	/// fighter they had.
	/// </summary>
	static void EarnedBefore()
	{
		for (int i = 0; i < FighterCatalog.Count; i++)
		{
			string story = StoryScript.IdOf(FighterCatalog.Get(i));
			StoryRun run = StoryRun.For(story);
			int reached = StoryProgress.Finished(story) ? run.Chapters.Count : StoryProgress.ChapterReached(story);
			for (int c = 0; c < reached && c < run.Chapters.Count; c++)
			{
				if (!run.Chapters[c].Fight) continue;
				foreach (string id in run.Chapters[c].Opponents)
				{
					config.SetValue("unlocked", id, true);
					config.SetValue("challenger", id, true);
				}
			}
		}
		config.SetValue("save", "version", 1);
		Save();
	}
}
