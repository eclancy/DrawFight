using System.Collections.Generic;
using Godot;

/// <summary>
/// Hands controllers out to player slots, for menus and matches alike.
///
/// Pads go first in connection order, then the keyboard and mouse fills the next seat. There is
/// one keyboard player, never two (Eric, 2026-10-20): everyone else needs a pad.
///
/// Menus and the fight share this on purpose. A pad that works in a match has to work on the
/// character select screen without a second mapping to keep in step.
/// </summary>
public static class ControllerAssignment
{
	/// <summary>The layout every pad uses. F3 swaps it; see .ai/fighting-design.md.</summary>
	public static GamepadLayout Layout = GamepadLayout.Standard();

	/// <summary>
	/// Every controller that can play, each by a name that stays the same from screen to screen:
	/// each pad plugged in ("pad:0"), then the keyboard and mouse, "keys:1". A seat remembers the
	/// name of whoever sat in it.
	/// </summary>
	public static List<string> Devices()
	{
		var list = new List<string>();
		foreach (int pad in Input.GetConnectedJoypads()) list.Add($"pad:{pad}");
		list.Add("keys:1");
		return list;
	}

	/// <summary>A controller by its name (Devices), or null for none.</summary>
	public static IInputSource Open(string key)
	{
		if (key == "keys:1") return KeyboardInputSource.Player1();
		if (key != null && key.StartsWith("pad:")) return new GamepadInputSource(key.Substring(4).ToInt(), Layout);
		return null;
	}

	/// <summary>Whether that controller is here: the keyboard always is; a pad while it is plugged in.</summary>
	public static bool IsConnected(string key) =>
		key != null && (key.StartsWith("keys:") || key.StartsWith("pad:") && Input.GetConnectedJoypads().Contains(key.Substring(4).ToInt()));

	public static IInputSource[] ForPlayers(int count)
	{
		Godot.Collections.Array<int> pads = Input.GetConnectedJoypads();
		var sources = new IInputSource[count];
		int keyboardsUsed = 0;

		for (int i = 0; i < count; i++)
		{
			if (i < pads.Count)
			{
				sources[i] = new GamepadInputSource(pads[i], Layout);
				continue;
			}

			// One keyboard player. Past that, a seat with no pad has no controller at all - two
			// seats on the same keys would join, pick and fight as one.
			sources[i] = keyboardsUsed++ == 0 ? KeyboardInputSource.Player1() : null;
		}

		return sources;
	}

	public static string Describe(IInputSource source)
	{
		return source switch
		{
			GamepadInputSource pad => $"pad {pad.Device + 1}",
			KeyboardInputSource => "keyboard",
			CpuInputSource => "CPU",
			_ => "none",
		};
	}

	public static List<string> DescribeAll(IEnumerable<IInputSource> sources)
	{
		var labels = new List<string>();
		foreach (IInputSource source in sources) labels.Add(Describe(source));
		return labels;
	}
}
