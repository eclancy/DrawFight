using System.Collections.Generic;
using Godot;

/// <summary>
/// The options (the gear on the main menu, OptionsScreen): fullscreen or a window, how loud the
/// music and the sounds are, the pad's button layout, how much the screen shakes, and how many
/// lives a versus fight starts with. Saved to user://settings.cfg beside the item settings, so
/// they stay set between games, and applied as the game starts.
/// </summary>
public static class GameOptions
{
	const string Path = "user://settings.cfg";
	const string Section = "options";

	public static bool Fullscreen = true;

	/// <summary>0 off, 1 quiet, 2 normal, 3 loud.</summary>
	public static int MusicVolume = 2;
	public static int SoundVolume = 2;

	/// <summary>Grab on LB and taunt on Back, as in Smash (GamepadLayout.Smash), instead of the standard layout.</summary>
	public static bool SmashLayout;

	/// <summary>0 off, 1 low, 2 full.</summary>
	public static int Shake = 2;

	/// <summary>Lives each fighter starts a versus fight with. A story sets its own.</summary>
	public static int Stocks = Tuning.DefaultStocks;

	public const int MinStocks = 1;

	/// <summary>How hard a story's fights are: 0 easy, 1 medium, 2 hard (GameRoot.GoStoryChapter).</summary>
	public static int StoryDifficulty = 1;
	public const int MaxStocks = 5;

	/// <summary>How much of a hit's screen shake is shown.</summary>
	public static float ShakeScale => Shake switch { 0 => 0.0f, 1 => 0.45f, _ => 1.0f };

	public static string DescribeVolume(int level) => level switch { 0 => "Off", 1 => "Quiet", 3 => "Loud", _ => "Normal" };
	public static string DescribeShake(int level) => level switch { 0 => "Off", 1 => "Low", _ => "Full" };

	public static void Load()
	{
		var config = new ConfigFile();
		if (config.Load(Path) != Error.Ok) return;
		Fullscreen = (bool)config.GetValue(Section, "fullscreen", Fullscreen);
		MusicVolume = Mathf.Clamp((int)config.GetValue(Section, "music", MusicVolume), 0, 3);
		SoundVolume = Mathf.Clamp((int)config.GetValue(Section, "sound", SoundVolume), 0, 3);
		SmashLayout = (bool)config.GetValue(Section, "smash_layout", SmashLayout);
		Shake = Mathf.Clamp((int)config.GetValue(Section, "shake", Shake), 0, 2);
		Stocks = Mathf.Clamp((int)config.GetValue(Section, "stocks", Stocks), MinStocks, MaxStocks);
		StoryDifficulty = Mathf.Clamp((int)config.GetValue(Section, "story_difficulty", StoryDifficulty), 0, 2);
	}

	public static void Save()
	{
		var config = new ConfigFile();
		config.Load(Path);
		config.SetValue(Section, "fullscreen", Fullscreen);
		config.SetValue(Section, "music", MusicVolume);
		config.SetValue(Section, "sound", SoundVolume);
		config.SetValue(Section, "smash_layout", SmashLayout);
		config.SetValue(Section, "shake", Shake);
		config.SetValue(Section, "stocks", Stocks);
		config.SetValue(Section, "story_difficulty", StoryDifficulty);
		config.Save(Path);
	}

	/// <summary>Puts the volumes and the button layout into effect - and the window, unless this run asked for one (--windowed, --shot).</summary>
	public static void Apply(bool applyWindow)
	{
		SetBusVolume(SfxPlayer.MusicBus, MusicVolume);
		SetBusVolume(SfxPlayer.SfxBus, SoundVolume);
		ControllerAssignment.Layout = SmashLayout ? GamepadLayout.Smash() : GamepadLayout.Standard();
		if (applyWindow && !DisplayServer.GetName().Equals("headless", System.StringComparison.OrdinalIgnoreCase))
		{
			DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
		}
	}

	static void SetBusVolume(string bus, int level)
	{
		int index = AudioServer.GetBusIndex(bus);
		if (index < 0) return;
		AudioServer.SetBusMute(index, level == 0);
		AudioServer.SetBusVolumeDb(index, level switch { 1 => -10.0f, 3 => 4.0f, _ => 0.0f });
	}
}

/// <summary>
/// The names people have given themselves on this computer (fighter select), most recent first,
/// so anyone can pick theirs again next time. Deleted from the options. Saved to user://settings.cfg.
/// </summary>
public static class PlayerNames
{
	const string Path = "user://settings.cfg";

	/// <summary>Long enough for a name, short enough to fit over a head in a four-player fight.</summary>
	public const int MaxLength = 10;

	public static readonly List<string> All = new List<string>();

	public static void Load()
	{
		All.Clear();
		var config = new ConfigFile();
		if (config.Load(Path) != Error.Ok) return;
		foreach (string name in (string[])config.GetValue("names", "list", new string[0]))
		{
			if (!string.IsNullOrWhiteSpace(name) && !All.Contains(name)) All.Add(name);
		}
	}

	public static void Save()
	{
		var config = new ConfigFile();
		config.Load(Path);
		config.SetValue("names", "list", All.ToArray());
		config.Save(Path);
	}

	/// <summary>A name used: to the top of the list, added if it is new.</summary>
	public static void Use(string name)
	{
		name = name.Trim();
		if (name.Length == 0) return;
		All.Remove(name);
		All.Insert(0, name);
		Save();
	}

	public static void Delete(string name)
	{
		if (All.Remove(name)) Save();
	}
}
