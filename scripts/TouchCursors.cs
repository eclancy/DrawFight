using System.Collections.Generic;
using Godot;

/// <summary>
/// A cursor for every controller on a screen that anyone may use (the main menu, the options) -
/// but shown only once its controller is touched: its stick moved or a button pressed. Four pads
/// plugged in put four arrows on the menu that nobody was holding (Eric, 2026-10-17). Each one
/// shown takes the next player colour.
/// </summary>
public sealed class TouchCursors
{
	/// <summary>The cursors shown, in the order their controllers were first touched.</summary>
	public readonly List<MenuCursor> Shown = new List<MenuCursor>();

	readonly List<MenuCursor> waiting = new List<MenuCursor>();
	readonly Dictionary<MenuCursor, string> keyOf = new Dictionary<MenuCursor, string>();
	readonly Vector2 start;

	/// <param name="start">Where a cursor appears; each one after the first a little to the right.</param>
	/// <param name="already">A controller already in use - the one that opened this screen - shown from the start.</param>
	public TouchCursors(Vector2 start, string already = null)
	{
		this.start = start;
		foreach (string key in ControllerAssignment.Devices())
		{
			var cursor = new MenuCursor(ControllerAssignment.Open(key), -1, MenuTheme.Soft, start);
			keyOf[cursor] = key;
			if (key == already) Show(cursor);
			else waiting.Add(cursor);
		}
	}

	/// <summary>Which controller (ControllerAssignment.Devices) a cursor is.</summary>
	public string KeyOf(MenuCursor cursor) => keyOf.TryGetValue(cursor, out string key) ? key : null;

	/// <summary>Polls the controllers nobody has touched yet, and shows any that have been. Once a physics frame, before the shown ones update.</summary>
	public void Tick()
	{
		for (int i = waiting.Count - 1; i >= 0; i--)
		{
			MenuCursor touched = waiting[i];
			touched.Nav.Poll();
			if (touched.Nav.Stick.LengthSquared() < 0.09f && !touched.Nav.AnyPress) continue;
			waiting.RemoveAt(i);
			Show(touched);
		}
	}

	void Show(MenuCursor cursor)
	{
		cursor.Tint = MenuTheme.PlayerTint(Shown.Count);
		cursor.Position = start + new Vector2(Shown.Count * 200.0f, 0.0f);
		Shown.Add(cursor);
	}
}
