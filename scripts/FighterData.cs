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

	// --- Ground movement -----------------------------------------------------

	[Export] public float RunSpeed { get; set; } = 880.0f;
	[Export] public float GroundAcceleration { get; set; } = 9500.0f;
	[Export] public float GroundFriction { get; set; } = 7400.0f;

	// --- Air movement --------------------------------------------------------

	[Export] public float AirSpeed { get; set; } = 720.0f;
	[Export] public float AirAcceleration { get; set; } = 4300.0f;
	[Export] public float JumpForce { get; set; } = 1640.0f;
	[Export] public float AirJumpForce { get; set; } = 1500.0f;
	[Export] public int AirJumps { get; set; } = 1;

	[Export] public float Gravity { get; set; } = 5200.0f;
	[Export] public float MaxFallSpeed { get; set; } = 1850.0f;
	[Export] public float FastFallSpeed { get; set; } = 2900.0f;

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

	/// <summary>
	/// Knocked over by a real hit: rolls along the ground as a ball with no arms or legs until
	/// he gets back up, and can roll right off the stage. Circy's declared weakness, and a
	/// reusable trait for any round or top-heavy fighter.
	/// </summary>
	[Export] public bool TumblesWhenHit { get; set; } = false;

	// --- Moveset -------------------------------------------------------------
	// M1 ships one placeholder attack. M4 replaces this with the full 16-move set built from
	// the shared default moveset plus four specials.

	/// <summary>
	/// Weight is not only a survivability stat - it retunes the whole moveset. Heavy hits
	/// harder and slower, light faster and weaker. See .ai/character-design.md.
	/// </summary>
	[Export] public WeightClass Weight { get; set; } = WeightClass.Medium;

	/// <summary>
	/// All fourteen moves, indexed by <see cref="MoveSlot"/>. Built by
	/// <see cref="DefaultMoveset.Build"/>: ten shared normals retuned by weight, plus four
	/// specials authored per character.
	/// </summary>
	public MoveData[] Moves = System.Array.Empty<MoveData>();

	public MoveData Move(MoveSlot slot)
	{
		int i = (int)slot;
		return i >= 0 && i < Moves.Length ? Moves[i] : null;
	}

	/// <summary>Body weight for the knockback formula, derived from the weight class.</summary>
	public float BodyWeight => WeightProfiles.BodyWeight(Weight);

	/// <summary>
	/// Two fighters with deliberately different feel, so M1 can be judged on whether weight
	/// and speed actually read differently in the hand. Not balanced, and not meant to be.
	/// </summary>
	public static FighterData PlaceholderLight()
	{
		var data = new FighterData
		{
			DisplayName = "Swift",
			PlaceholderColor = new Color(0.36f, 0.72f, 0.98f),
			Weight = WeightClass.Light,
			RunSpeed = 980.0f,
			AirSpeed = 790.0f,
			JumpForce = 1720.0f,
			AirJumpForce = 1570.0f,
			Gravity = 5000.0f,
			BodySize = new Vector2(64.0f, 118.0f),
			RigPath = "res://fighters/swift/rig.json",
			VisualScale = 1.06f,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Fire());
		return data;
	}

	public static FighterData PlaceholderHeavy()
	{
		var data = new FighterData
		{
			DisplayName = "Lug",
			PlaceholderColor = new Color(0.97f, 0.55f, 0.29f),
			Weight = WeightClass.Heavy,
			RunSpeed = 740.0f,
			AirSpeed = 620.0f,
			JumpForce = 1650.0f,
			AirJumpForce = 1480.0f,
			Gravity = 5900.0f,
			BodySize = new Vector2(84.0f, 142.0f),
			RigPath = "res://fighters/lug/rig.json",
			VisualScale = 1.12f,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.HeavyWeapons());
		return data;
	}

	/// <summary>
	/// Circy, drawn and designed by Elim - the first fighter made by a kid rather than generated
	/// as scaffolding. The art is cut from Elim's own drawings by tools/art/cut_circy.py.
	///
	/// Medium, because his weaknesses already do a lot of work: a light fighter who also rolls
	/// off the stage when hit and cannot jump would just lose. What he asked for, in stats:
	///   - "falls slowly": low gravity and a low fall-speed cap
	///   - "terrible at jumping": a jump about 60% the height of anyone else's, both jumps
	///   - "easy to knock over": TumblesWhenHit
	/// </summary>
	public static FighterData Circy()
	{
		var data = new FighterData
		{
			DisplayName = "Circy",
			PlaceholderColor = new Color(0.98f, 0.86f, 0.30f),
			Weight = WeightClass.Medium,
			RunSpeed = 860.0f,
			AirSpeed = 700.0f,
			AirAcceleration = 4000.0f,
			// Jump height is v^2 / 2g, so the low gravity would hand back most of the height a
			// low jump force takes away. These are solved for roughly 180px and 150px.
			JumpForce = 1140.0f,
			AirJumpForce = 1040.0f,
			Gravity = 3600.0f,
			MaxFallSpeed = 1250.0f,
			FastFallSpeed = 2200.0f,
			// Elim drew him with a wide ball on long legs, so the box is taller than the stand-in's.
			BodySize = new Vector2(84.0f, 150.0f),
			RigPath = "res://fighters/circy/rig.json",
			VisualScale = 1.0f,
			TumblesWhenHit = true,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Circy());
		return data;
	}
}
