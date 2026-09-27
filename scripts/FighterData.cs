using Godot;

/// <summary>
/// A fighter's stat block and moveset, as a .tres Resource. These numbers - not the moveset -
/// are most of what makes two fighters feel different, and they are where a character's
/// declared weakness actually gets enforced. See .ai/character-design.md.
///
/// THE ONE HARD BALANCE RULE: no fighter is above average in both Weight and RunSpeed. That
/// single constraint is what prevents the character who is simply better than everyone else.
/// </summary>
[GlobalClass]
public partial class FighterData : Resource
{
	[Export] public string DisplayName { get; set; } = "Fighter";

	/// <summary>M1 placeholder tint. Replaced by the rigged drawing at M2.</summary>
	[Export] public Color PlaceholderColor { get; set; } = new Color(0.9f, 0.9f, 0.9f);

	// --- Survivability -------------------------------------------------------

	/// <summary>100 is average. Heavier survives to higher percent but is easier to hit.</summary>
	[Export] public float Weight { get; set; } = 100.0f;

	// --- Ground movement -----------------------------------------------------

	[Export] public float RunSpeed { get; set; } = 620.0f;
	[Export] public float GroundAcceleration { get; set; } = 6000.0f;
	[Export] public float GroundFriction { get; set; } = 5200.0f;

	// --- Air movement --------------------------------------------------------

	[Export] public float AirSpeed { get; set; } = 500.0f;
	[Export] public float AirAcceleration { get; set; } = 2600.0f;
	[Export] public float JumpForce { get; set; } = 1180.0f;
	[Export] public float AirJumpForce { get; set; } = 1080.0f;
	[Export] public int AirJumps { get; set; } = 1;

	[Export] public float Gravity { get; set; } = 2900.0f;
	[Export] public float MaxFallSpeed { get; set; } = 1150.0f;
	[Export] public float FastFallSpeed { get; set; } = 1900.0f;

	// --- Presentation --------------------------------------------------------

	/// <summary>Body/hurtbox size in pixels. Deliberately a little tighter than the drawing.</summary>
	[Export] public Vector2 BodySize { get; set; } = new Vector2(72.0f, 128.0f);

	/// <summary>The rig manifest emitted by the import pipeline. Empty falls back to a rectangle.</summary>
	[Export] public string RigPath { get; set; } = "";

	/// <summary>
	/// Multiplies the normalised puppet height. Every fighter is scaled to one standard size
	/// on import, so this is where "this one is meant to look big" is deliberately stated
	/// rather than inherited from how close the camera was held.
	/// </summary>
	[Export] public float VisualScale { get; set; } = 1.0f;

	// --- Moveset -------------------------------------------------------------
	// M1 ships one placeholder attack. M4 replaces this with the full 16-move set built from
	// the shared default moveset plus four specials.

	[Export] public MoveData Jab { get; set; }

	/// <summary>
	/// Two fighters with deliberately different feel, so M1 can be judged on whether weight
	/// and speed actually read differently in the hand. Not balanced, and not meant to be.
	/// </summary>
	public static FighterData PlaceholderLight()
	{
		return new FighterData
		{
			DisplayName = "Swift",
			PlaceholderColor = new Color(0.36f, 0.72f, 0.98f),
			Weight = 82.0f,
			RunSpeed = 700.0f,
			AirSpeed = 560.0f,
			JumpForce = 1260.0f,
			AirJumpForce = 1160.0f,
			Gravity = 2750.0f,
			BodySize = new Vector2(64.0f, 118.0f),
			RigPath = "res://fighters/swift/rig.json",
			VisualScale = 1.06f,
			Jab = MoveData.PlaceholderJab(),
		};
	}

	public static FighterData PlaceholderHeavy()
	{
		return new FighterData
		{
			DisplayName = "Lug",
			PlaceholderColor = new Color(0.97f, 0.55f, 0.29f),
			Weight = 128.0f,
			RunSpeed = 500.0f,
			AirSpeed = 420.0f,
			JumpForce = 1080.0f,
			AirJumpForce = 980.0f,
			Gravity = 3150.0f,
			BodySize = new Vector2(84.0f, 142.0f),
			RigPath = "res://fighters/lug/rig.json",
			VisualScale = 1.12f,
			Jab = MoveData.PlaceholderHeavySwing(),
		};
	}
}
