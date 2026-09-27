using Godot;

/// <summary>
/// Which physical button does what. Two presets ship; the fields exist so that M5's controls
/// screen has something to write into rather than needing this class rewritten.
/// </summary>
public sealed class GamepadLayout
{
	public JoyButton Attack;
	public JoyButton Special;
	public JoyButton Jump;
	public JoyButton AltJump;
	public JoyButton Block;

	/// <summary>Also accept the right trigger as block, so either shoulder input works.</summary>
	public bool BlockUsesRightTrigger = true;

	/// <summary>A one-line binding summary for the HUD.</summary>
	public string Describe()
	{
		string block = BlockUsesRightTrigger ? $"{Block}/RT block" : $"{Block} block";
		return $"{Jump}/{AltJump} jump   {Attack} attack   {Special} special   {block}";
	}

	/// <summary>Distinguishes the two presets without exposing every field.</summary>
	public bool IsSmashLayout => Jump == JoyButton.X;

	/// <summary>
	/// Matches Smash Ultimate's default pad layout: A attacks, B specials, X and Y both jump.
	/// This is the default because he almost certainly plays Smash, and muscle memory
	/// transferring from the game we are imitating is worth more than internal tidiness.
	/// </summary>
	public static GamepadLayout Smash()
	{
		return new GamepadLayout
		{
			Attack = JoyButton.A,
			Special = JoyButton.B,
			Jump = JoyButton.X,
			AltJump = JoyButton.Y,
			Block = JoyButton.RightShoulder,
		};
	}

	/// <summary>
	/// Jump on A, the way most platformers do it. Easier for someone who has never played
	/// Smash; worse for someone who has.
	/// </summary>
	public static GamepadLayout Platformer()
	{
		return new GamepadLayout
		{
			Attack = JoyButton.B,
			Special = JoyButton.X,
			Jump = JoyButton.A,
			AltJump = JoyButton.Y,
			Block = JoyButton.RightShoulder,
		};
	}
}

/// <summary>
/// Anything that can shove the player's hands around. Optional - the keyboard source does not
/// implement it, and callers check for it rather than assuming.
/// </summary>
public interface IHapticInputSource
{
	void Rumble(float strength, float seconds);
}

/// <summary>
/// An Xbox (or any SDL-mapped) gamepad driving one fighter. Polled directly rather than routed
/// through Godot's InputMap, for the same reason the keyboard source is: every buffer window in
/// the game is counted in frames from a button's rising edge, and polling makes that edge
/// unambiguous and keeps project.godot free of two dozen generated action entries.
/// </summary>
public class GamepadInputSource : IInputSource, IHapticInputSource
{
	/// <summary>
	/// Radial deadzone. Applied to the stick's magnitude, never per-axis - a per-axis deadzone
	/// carves a square hole out of a round stick and makes diagonals feel wrong, which matters
	/// here because diagonals are how directional attacks and DI are aimed.
	/// </summary>
	const float Deadzone = 0.22f;

	/// <summary>How far the analogue trigger must travel to count as a press.</summary>
	const float TriggerThreshold = 0.45f;

	readonly int device;
	readonly GamepadLayout layout;

	bool jumpWasDown, attackWasDown, specialWasDown;

	public GamepadInputSource(int device, GamepadLayout layout)
	{
		this.device = device;
		this.layout = layout;
	}

	public int Device => device;

	public bool IsConnected => Input.GetConnectedJoypads().Contains(device);

	public string DeviceName => Input.GetJoyName(device);

	public InputState Poll()
	{
		// A pad unplugged mid-match reports nothing rather than throwing or freezing the last
		// held direction, so the fighter just stands still until it comes back.
		if (!IsConnected)
		{
			jumpWasDown = attackWasDown = specialWasDown = false;
			return InputState.None;
		}

		bool jumpDown = Input.IsJoyButtonPressed(device, layout.Jump)
			|| Input.IsJoyButtonPressed(device, layout.AltJump);
		bool attackDown = Input.IsJoyButtonPressed(device, layout.Attack);
		bool specialDown = Input.IsJoyButtonPressed(device, layout.Special);

		bool blockDown = Input.IsJoyButtonPressed(device, layout.Block)
			|| (layout.BlockUsesRightTrigger
				&& Input.GetJoyAxis(device, JoyAxis.TriggerRight) > TriggerThreshold);

		var state = new InputState
		{
			Move = ReadDirection(),
			JumpPressed = jumpDown && !jumpWasDown,
			AttackPressed = attackDown && !attackWasDown,
			SpecialPressed = specialDown && !specialWasDown,
			BlockHeld = blockDown,
		};

		jumpWasDown = jumpDown;
		attackWasDown = attackDown;
		specialWasDown = specialDown;
		return state;
	}

	/// <summary>
	/// Stick and d-pad merged, whichever is pushed further. Magnitude is preserved rather than
	/// normalised, which gives analogue walking speed for free and gives DI something finer
	/// than eight directions to work with.
	/// </summary>
	Vector2 ReadDirection()
	{
		var raw = new Vector2(
			Input.GetJoyAxis(device, JoyAxis.LeftX),
			Input.GetJoyAxis(device, JoyAxis.LeftY));

		Vector2 stick = Vector2.Zero;
		float magnitude = raw.Length();
		if (magnitude > Deadzone)
		{
			// Rescale from the deadzone edge so the stick ramps from zero rather than snapping
			// straight to 0.22 the instant it leaves the dead area.
			float scaled = Mathf.Min(1.0f, (magnitude - Deadzone) / (1.0f - Deadzone));
			stick = raw / magnitude * scaled;
		}

		var dpad = new Vector2(
			(Input.IsJoyButtonPressed(device, JoyButton.DpadRight) ? 1.0f : 0.0f)
				- (Input.IsJoyButtonPressed(device, JoyButton.DpadLeft) ? 1.0f : 0.0f),
			(Input.IsJoyButtonPressed(device, JoyButton.DpadDown) ? 1.0f : 0.0f)
				- (Input.IsJoyButtonPressed(device, JoyButton.DpadUp) ? 1.0f : 0.0f));

		return dpad.LengthSquared() > stick.LengthSquared() ? dpad : stick;
	}

	/// <summary>
	/// Rumble on the victim's pad is the cheapest large win in the whole feel budget - it is
	/// hitlag you can hold. Kept short so that a fast exchange does not blur into one long buzz.
	/// </summary>
	public void Rumble(float strength, float seconds)
	{
		if (!IsConnected) return;

		float clamped = Mathf.Clamp(strength, 0.0f, 1.0f);
		Input.StartJoyVibration(device, clamped * 0.65f, clamped, seconds);
	}
}
