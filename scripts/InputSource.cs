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

	public bool JumpPressed;
	public bool AttackPressed;
	public bool SpecialPressed;
	public bool BlockHeld;

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
	readonly Key left, right, up, down, jump, attack, special, block;

	bool jumpWasDown, attackWasDown, specialWasDown;

	public KeyboardInputSource(
		Key left, Key right, Key up, Key down,
		Key jump, Key attack, Key special, Key block)
	{
		this.left = left;
		this.right = right;
		this.up = up;
		this.down = down;
		this.jump = jump;
		this.attack = attack;
		this.special = special;
		this.block = block;
	}

	/// <summary>Player 1: WASD to move, left hand cluster to act.</summary>
	public static KeyboardInputSource Player1() => new KeyboardInputSource(
		Key.A, Key.D, Key.W, Key.S,
		Key.G, Key.H, Key.J, Key.F);

	/// <summary>Player 2: arrow keys to move, numpad to act.</summary>
	public static KeyboardInputSource Player2() => new KeyboardInputSource(
		Key.Left, Key.Right, Key.Up, Key.Down,
		Key.Kp1, Key.Kp2, Key.Kp3, Key.Kp0);

	public InputState Poll()
	{
		bool jumpDown = Input.IsPhysicalKeyPressed(jump);
		bool attackDown = Input.IsPhysicalKeyPressed(attack);
		bool specialDown = Input.IsPhysicalKeyPressed(special);

		var state = new InputState
		{
			Move = new Vector2(
				(Input.IsPhysicalKeyPressed(right) ? 1.0f : 0.0f) - (Input.IsPhysicalKeyPressed(left) ? 1.0f : 0.0f),
				(Input.IsPhysicalKeyPressed(down) ? 1.0f : 0.0f) - (Input.IsPhysicalKeyPressed(up) ? 1.0f : 0.0f)),
			JumpPressed = jumpDown && !jumpWasDown,
			AttackPressed = attackDown && !attackWasDown,
			SpecialPressed = specialDown && !specialWasDown,
			BlockHeld = Input.IsPhysicalKeyPressed(block),
		};

		jumpWasDown = jumpDown;
		attackWasDown = attackDown;
		specialWasDown = specialDown;
		return state;
	}
}
