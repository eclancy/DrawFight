using System.Collections.Generic;
using Godot;

/// <summary>
/// The saved game - who is unlocked, which achievements are done, how far each story has got -
/// read and written through here. Played online, every copy plays on the host's (NetSession):
/// each one reads the host's files from memory instead of its own, and writes go back into that
/// memory, never to disk, so nobody's save is touched by playing on someone else's.
/// </summary>
public static class SaveFiles
{
	/// <summary>The host's files by path while playing online, or null.</summary>
	static Dictionary<string, string> shared;

	public static bool Shared => shared != null;

	/// <summary>The files a session shares: everything that decides what can be picked or played.</summary>
	public static readonly string[] SessionFiles = { "user://unlocks.cfg", "user://achievements.cfg", "user://story.cfg" };

	public static ConfigFile Load(string path)
	{
		var config = new ConfigFile();
		if (shared == null) config.Load(path);
		else if (shared.TryGetValue(path, out string text) && text != "") config.Parse(text);
		return config;
	}

	public static void Save(ConfigFile config, string path)
	{
		if (shared == null) config.Save(path);
		else shared[path] = config.EncodeToText();
	}

	/// <summary>This computer's own copy of a file, to share as the host.</summary>
	public static string Own(string path) => FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : "";

	public static void Share(Dictionary<string, string> files)
	{
		shared = new Dictionary<string, string>(files);
		Reloaded();
	}

	public static void Unshare()
	{
		shared = null;
		Reloaded();
	}

	/// <summary>Everything that keeps a file in memory reads it afresh.</summary>
	static void Reloaded()
	{
		Unlocks.Reload();
		Achievements.Reload();
	}
}
