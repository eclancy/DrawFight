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
	readonly Key left, right, up, down, jump, attack, special, block, taunt;

	bool jumpWasDown, attackWasDown, specialWasDown, startWasDown, tauntWasDown;

	public KeyboardInputSource(
		Key left, Key right, Key up, Key down,
		Key jump, Key attack, Key special, Key block, Key taunt)
	{
		this.taunt = taunt;
		this.left = left;
		this.right = right;
		this.up = up;
		this.down = down;
		this.jump = jump;
		this.attack = attack;
		this.special = special;
		this.block = block;
	}

	/// <summary>Player 1: arrow keys to move; A jump, Q attack, W special, S block, E taunt.</summary>
	public static KeyboardInputSource Player1() => new KeyboardInputSource(
		Key.Left, Key.Right, Key.Up, Key.Down,
		Key.A, Key.Q, Key.W, Key.S, Key.E);

	/// <summary>
	/// Player 2, only when there is no second pad: all on the number pad, since player 1 has the
	/// arrows. 8/4/5/6 move; 1 jump, 2 attack, 3 special, 0 block, 7 taunt.
	/// </summary>
	public static KeyboardInputSource Player2() => new KeyboardInputSource(
		Key.Kp4, Key.Kp6, Key.Kp8, Key.Kp5,
		Key.Kp1, Key.Kp2, Key.Kp3, Key.Kp0, Key.Kp7);

	public InputState Poll()
	{
		bool jumpDown = Input.IsPhysicalKeyPressed(jump);
		bool attackDown = Input.IsPhysicalKeyPressed(attack);
		bool specialDown = Input.IsPhysicalKeyPressed(special);
		bool startDown = Input.IsPhysicalKeyPressed(Key.Enter) || Input.IsPhysicalKeyPressed(Key.Space);
		bool tauntDown = Input.IsPhysicalKeyPressed(taunt);

		var state = new InputState
		{
			Move = new Vector2(
				(Input.IsPhysicalKeyPressed(right) ? 1.0f : 0.0f) - (Input.IsPhysicalKeyPressed(left) ? 1.0f : 0.0f),
				(Input.IsPhysicalKeyPressed(down) ? 1.0f : 0.0f) - (Input.IsPhysicalKeyPressed(up) ? 1.0f : 0.0f)),
			JumpPressed = jumpDown && !jumpWasDown,
			AttackPressed = attackDown && !attackWasDown,
			AttackHeld = attackDown,
			SpecialPressed = specialDown && !specialWasDown,
			BlockHeld = Input.IsPhysicalKeyPressed(block),
			SpecialHeld = specialDown,
			StartPressed = startDown && !startWasDown,
			TauntPressed = tauntDown && !tauntWasDown,
		};

		jumpWasDown = jumpDown;
		attackWasDown = attackDown;
		specialWasDown = specialDown;
		startWasDown = startDown;
		tauntWasDown = tauntDown;
		return state;
	}
}
