using System.Collections.Generic;
using Godot;

/// <summary>
/// One person, one pointer: on the screens for drawing a fighter - your drawings, the drawing
/// screen, sending it in - the mouse and a pad are never both pointing (Eric, 2026-10-16).
/// Whichever last did something is the pointer, and the only one shown. Using the mouse (or a pen,
/// or a finger), every pad's cursor is hidden and a pad does nothing but take over; using a pad,
/// the mouse pointer is hidden and touching the mouse takes it back. The touch that hands over
/// does nothing else, so a nudge of the stick never draws, clicks or undoes anything by itself.
///
/// Which one is in use is remembered from screen to screen, so the next screen starts with the
/// same. Select screens with a cursor for every player are not one person, and do not use this -
/// and nowhere else takes the mouse at all: its pointer is hidden off these screens.
/// </summary>
public sealed class OnePointer
{
	/// <summary>A pad is the pointer, not the mouse - the same on every screen that uses this.</summary>
	public static bool UsingPad { get; private set; }

	/// <summary>How far the stick has to go before a pad counts as touched, past its own deadzone.</summary>
	const float StickTouch = 0.3f;

	readonly List<MenuCursor> pads;

	/// <summary>The pad pointing, while a pad is the pointer - the one used most recently.</summary>
	public MenuCursor Current { get; private set; }

	/// <param name="pads">The screen's pad cursors; it may add more as pads are plugged in.</param>
	public OnePointer(List<MenuCursor> pads)
	{
		this.pads = pads;
		if (UsingPad && pads.Count > 0) Current = pads[0];
		Apply();
	}

	/// <summary>
	/// Which one the next of these screens starts with: whatever chose to go there - a pad's cursor
	/// on the main menu, or the mouse - so a pad never has to be touched once just to show up.
	/// </summary>
	public static void StartWith(bool pad) => UsingPad = pad;

	/// <summary>The mouse, a pen or a finger did something: it is the pointer now.</summary>
	public void MouseUsed()
	{
		if (!UsingPad) return;
		UsingPad = false;
		Apply();
	}

	/// <summary>
	/// Polls every pad, once a physics frame. Returns the pad cursor to act on this frame, moved -
	/// or null while the mouse is the pointer, or on the frame a pad takes over.
	/// </summary>
	public MenuCursor Tick(Vector2 viewport, Vector2 mouse)
	{
		MenuCursor touched = null;
		foreach (MenuCursor pad in pads)
		{
			pad.Nav.Poll();
			if (touched == null && Touched(pad.Nav)) touched = pad;
		}

		if (touched != null && (!UsingPad || touched != Current))
		{
			// It picks up where the pointer was - the mouse, or the other pad.
			Vector2 from = UsingPad && Current != null ? Current.Position : mouse;
			if (from.X >= 0.0f && from.Y >= 0.0f) touched.Position = from;
			Current = touched;
			UsingPad = true;
			Apply();
			return null;
		}

		if (UsingPad && Current == null && pads.Count > 0) Current = pads[0];
		Apply();
		if (!UsingPad || Current == null) return null;
		Current.Move(viewport);
		return Current;
	}

	/// <summary>Where the pointer in use is: the pad's cursor, or the mouse.</summary>
	public Vector2 At(Vector2 mouse) => UsingPad && Current != null ? Current.Position : mouse;

	/// <summary>The pad's cursor, while a pad is the pointer. The mouse draws its own.</summary>
	public void Draw(CanvasItem canvas)
	{
		if (UsingPad) Current?.Draw(canvas);
	}

	static bool Touched(MenuNav nav) => nav.Stick.LengthSquared() > StickTouch * StickTouch || nav.AnyPress || nav.Held;

	/// <summary>The mouse pointer shown only while it is the pointer - and never with no pad to stand in for it.</summary>
	void Apply()
	{
		if (UsingPad && pads.Count == 0) UsingPad = false;
		Input.MouseModeEnum want = UsingPad ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible;
		if (Input.MouseMode != want) Input.MouseMode = want;
	}

	/// <summary>Off a screen that uses this: the mouse pointer goes - the other screens have no use for it.</summary>
	public static void Leave()
	{
		if (Input.MouseMode != Input.MouseModeEnum.Hidden) Input.MouseMode = Input.MouseModeEnum.Hidden;
	}
}
