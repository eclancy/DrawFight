using Godot;

/// <summary>
/// One frame of intent from a controlling entity. Deliberately a plain struct with no idea
/// where it came from - a keyboard, a gamepad, or the CPU AI at M7 all produce these, and
/// <see cref="Fighter"/> cannot tell the difference. That seam is the whole point of this file.
/// </summary>
public struct InputState
{
	/// <summary>Raw stick/d-pad direction. Not normalised; magnitude is meaningful for DI.</summary>
	public Vector2 Move;

	/// <summary>
	/// The direction came from the d-pad rather than the stick. A d-pad direction with attack is
	/// always a smash: a d-pad cannot be tilted gently, and flicking it on the same frame as the
	/// button is too fiddly - so the stick does tilts and the d-pad does smashes.
	/// </summary>
	public bool MoveFromDpad;

	public bool JumpPressed;

	/// <summary>The jump button's level. Only the drawing screen reads it: A held down draws.</summary>
	public bool JumpHeld;

	public bool AttackPressed;

	/// <summary>The attack button's level, not its edge. Holding it charges a smash attack.</summary>
	public bool AttackHeld;
	public bool SpecialPressed;
	public bool BlockHeld;

	/// <summary>
	/// The special button's level rather than its edge. Only a special that is steered while
	/// held reads it - Circy's Stretch keeps going for as long as the button stays down.
	/// </summary>
	public bool SpecialHeld;

	/// <summary>Start / Enter. Menus only; the fight itself never reads it.</summary>
	public bool StartPressed;

	/// <summary>The taunt button's edge. A taunt does nothing but show off.</summary>
	public bool TauntPressed;

	/// <summary>
	/// The grab button's edge - one button that grabs, the way Smash's shoulder buttons do, as
	/// well as block plus attack. Holding an item, it throws it (Eric, 2026-10-10).
	/// </summary>
	public bool GrabPressed;

	public static InputState None => new InputState { Move = Vector2.Zero };
}

/// <summary>
/// Anything that can drive a fighter. Implementations must report PRESSED as a one-frame edge,
/// not a held state, because every buffer window in the game is counted in frames from the edge.
/// </summary>
public interface IInputSource
{
	InputState Poll();
}

/// <summary>
/// M1 keyboard control for local players. Gamepad support lands at M6 alongside 3-4 players;
/// it implements this same interface and nothing in <see cref="Fighter"/> changes.
/// </summary>
public class KeyboardInputSource : IInputSource
{
	readonly Key[] left, right, up, down, jump, attack, special, block, taunt, grab;

	/// <summary>The mouse buttons this keyboard's player also fights with: left attacks, right specials, middle grabs.</summary>
	readonly bool mouse;

	/// <summary>
	/// The mouse fights only in a match (MatchManager sets this), never in a menu: on the drawing
	/// screens the mouse draws, and a right click there must not count as "back".
	/// </summary>
	public static bool MouseInMatch;

	bool jumpWasDown, attackWasDown, specialWasDown, startWasDown, tauntWasDown, grabWasDown, escapeWasDown;

	/// <summary>
	/// Read once already. A button that is down the first time it is read was pressed before we
	/// were listening - the press that left the last screen - so it is held, not pressed: every
	/// press is a button going down while this is watching (Eric, 2026-10-16).
	/// </summary>
	bool primed;

	public KeyboardInputSource(
		Key[] left, Key[] right, Key[] up, Key[] down,
		Key[] jump, Key[] attack, Key[] special, Key[] block, Key[] taunt, Key[] grab, bool mouse = false)
	{
		this.mouse = mouse;
		this.taunt = taunt;
		this.grab = grab;
		this.left = left;
		this.right = right;
		this.up = up;
		this.down = down;
		this.jump = jump;
		this.attack = attack;
		this.special = special;
		this.block = block;
	}

	/// <summary>
	/// Player 1, keyboard and mouse, as a PC game plays (Elim, 2026-10-19: not everyone has a
	/// controller): WASD or the arrows move, Space jumps, left click or J attacks, right click or
	/// K specials, Shift or L blocks, E or middle click grabs, Q taunts. Enter pauses; Escape goes
	/// back - B in a menu, the pause in a match. The same
	/// list is shown under Options > Controls (OptionsScreen.KeyboardControls).
	/// </summary>
	public static KeyboardInputSource Player1() => new KeyboardInputSource(
		new[] { Key.A, Key.Left }, new[] { Key.D, Key.Right }, new[] { Key.W, Key.Up }, new[] { Key.S, Key.Down },
		new[] { Key.Space }, new[] { Key.J }, new[] { Key.K }, new[] { Key.Shift, Key.L }, new[] { Key.Q }, new[] { Key.E },
		mouse: true);

	static bool Down(Key[] keys)
	{
		foreach (Key key in keys) if (Input.IsPhysicalKeyPressed(key)) return true;
		return false;
	}

	bool Clicked(MouseButton button) => mouse && MouseInMatch && Input.IsMouseButtonPressed(button);

	public InputState Poll()
	{
		bool jumpDown = Down(jump);
		bool attackDown = Down(attack) || Clicked(MouseButton.Left);
		bool specialDown = Down(special) || Clicked(MouseButton.Right);
		// Enter only: Space is player 1's jump.
		bool startDown = Input.IsPhysicalKeyPressed(Key.Enter) || Input.IsPhysicalKeyPressed(Key.KpEnter);
		bool tauntDown = Down(taunt);
		bool grabDown = Down(grab) || Clicked(MouseButton.Middle);
		// Escape is "back" (Eric, 2026-10-20: it used to quit the game): B in a menu, and in a
		// match the pause, which is the way back out of one.
		bool escapeDown = Input.IsPhysicalKeyPressed(Key.Escape);
		if (!primed)
		{
			primed = true;
			jumpWasDown = jumpDown;
			attackWasDown = attackDown;
			specialWasDown = specialDown;
			startWasDown = startDown;
			tauntWasDown = tauntDown;
			grabWasDown = grabDown;
			escapeWasDown = escapeDown;
		}
		bool escapePressed = escapeDown && !escapeWasDown;

		var state = new InputState
		{
			Move = new Vector2(
				(Down(right) ? 1.0f : 0.0f) - (Down(left) ? 1.0f : 0.0f),
				(Down(down) ? 1.0f : 0.0f) - (Down(up) ? 1.0f : 0.0f)),
			JumpPressed = jumpDown && !jumpWasDown,
			JumpHeld = jumpDown,
			AttackPressed = attackDown && !attackWasDown,
			AttackHeld = attackDown,
			SpecialPressed = specialDown && !specialWasDown || escapePressed && !MouseInMatch,
			BlockHeld = Down(block),
			SpecialHeld = specialDown,
			StartPressed = startDown && !startWasDown || escapePressed && MouseInMatch,
			TauntPressed = tauntDown && !tauntWasDown,
			GrabPressed = grabDown && !grabWasDown,
		};

		grabWasDown = grabDown;
		escapeWasDown = escapeDown;
		jumpWasDown = jumpDown;
		attackWasDown = attackDown;
		specialWasDown = specialDown;
		startWasDown = startDown;
		tauntWasDown = tauntDown;
		return state;
	}
}
