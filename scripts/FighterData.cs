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
	[Export] public float JumpForce { get; set; } = 1486.0f;
	[Export] public float AirJumpForce { get; set; } = 1560.0f;
	[Export] public int AirJumps { get; set; } = 1;

	[Export] public float Gravity { get; set; } = 4260.0f;
	[Export] public float MaxFallSpeed { get; set; } = 1630.0f;
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
	/// How much bigger than the shared library this fighter plays its animations. The library is
	/// authored once for everyone; a fighter whose drawing is stiff or whose character is wild
	/// can push every pose further from rest.
	/// </summary>
	[Export] public float AnimationDrama { get; set; } = 1.0f;

	/// <summary>
	/// The colour of this fighter's swing trails. Transparent means use PlaceholderColor, the
	/// fighter's own colour.
	/// </summary>
	[Export] public Color TrailColor { get; set; } = new Color(0.0f, 0.0f, 0.0f, 0.0f);

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
	/// All seventeen moves, indexed by <see cref="MoveSlot"/>. Built by
	/// <see cref="DefaultMoveset.Build"/>: the character's own normals (see CharacterNormals), plus four
	/// specials authored per character.
	/// </summary>
	public MoveData[] Moves = System.Array.Empty<MoveData>();

	/// <summary>
	/// What this fighter does when it taunts: a pose held for a moment, open to attack. Built by
	/// <see cref="Taunts"/>. Null means no taunt.
	/// </summary>
	public MoveData Taunt;

	/// <summary>What the fighter says in a speech bubble while taunting. Empty says nothing.</summary>
	public string TauntLine = "";

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
			// Flambe: a fire punk who is also, somehow, a French chef.
			DisplayName = "Flambé",
			PlaceholderColor = new Color(0.36f, 0.72f, 0.98f),
			Weight = WeightClass.Light,
			RunSpeed = 980.0f,
			AirSpeed = 790.0f,
			JumpForce = 1557.0f,
			AirJumpForce = 1633.0f,
			Gravity = 4100.0f,
			BodySize = new Vector2(64.0f, 118.0f),
			RigPath = "res://fighters/swift/rig.json",
			VisualScale = 1.06f,
			// Fire: every swing leaves a streak of flame.
			TrailColor = new Color(1.0f, 0.44f, 0.16f),
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Fire(),
			CharacterNormals.Swift, CharacterNormals.SwiftJab());
		data.Taunt = Taunts.Make(AttackAnim.HangUp, propArt: "");
		data.TauntLine = "Too hot for you!";
		return data;
	}

	public static FighterData PlaceholderHeavy()
	{
		var data = new FighterData
		{
			DisplayName = "Lugnut",
			PlaceholderColor = new Color(0.97f, 0.55f, 0.29f),
			Weight = WeightClass.Heavy,
			RunSpeed = 740.0f,
			AirSpeed = 620.0f,
			JumpForce = 1494.0f,
			AirJumpForce = 1554.0f,
			Gravity = 4840.0f,
			BodySize = new Vector2(84.0f, 142.0f),
			RigPath = "res://fighters/lug/rig.json",
			VisualScale = 1.12f,
			// Hi-vis yellow, like his tools and vest: every swing leaves a bright streak.
			TrailColor = new Color(1.0f, 0.74f, 0.12f),
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Construction(),
			CharacterNormals.Lug, CharacterNormals.LugJab());
		data.Taunt = Taunts.Make(AttackAnim.OverheadArc, propArt: "tool_sledgehammer", extra: "hardhat");
		data.TauntLine = "Break's over!";
		return data;
	}

	/// <summary>
	/// EdgeLord, drawn by Eric: "stretchy arms, super speed, infinite swords." Light and the fastest runner in the
	/// game, which is also his weakness - light is easy to launch, and his normals are ordinary
	/// sword swings, so his range comes from his specials. The art is a placeholder until his
	/// own coloured drawing arrives (see tools/art/cut_edgelord.py).
	/// </summary>
	public static FighterData EdgeLord()
	{
		var data = new FighterData
		{
			DisplayName = "EdgeLord",
			PlaceholderColor = new Color(0.81f, 0.15f, 0.15f),
			Weight = WeightClass.Light,
			RunSpeed = 1060.0f,
			AirSpeed = 800.0f,
			AirAcceleration = 4500.0f,
			JumpForce = 1557.0f,
			AirJumpForce = 1633.0f,
			Gravity = 4100.0f,
			BodySize = new Vector2(80.0f, 136.0f),
			RigPath = "res://fighters/edgelord/rig.json",
			VisualScale = 1.08f,
			// Wild and fast: every pose played further than the library's default.
			AnimationDrama = 1.35f,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.EdgeLord(),
			CharacterNormals.EdgeLord, CharacterNormals.EdgeLordJab());
		data.Taunt = Taunts.Make(AttackAnim.PointUp, propArt: "sword_claymore");
		data.TauntLine = "Try to keep up.";
		return data;
	}

	/// <summary>
	/// DoomBot, drawn by Eric: a boxy robot with claw hands, cut from the drawing by
	/// tools/art/cut_doombot.py. A heavy. The rest is placeholder until his character sheet
	/// arrives - the shared default normals, heavy movement numbers, and four stand-in specials -
	/// so he can be played and his rig checked in the meantime.
	/// </summary>
	public static FighterData DoomBot()
	{
		var data = new FighterData
		{
			DisplayName = "DoomBot",
			PlaceholderColor = new Color(0.86f, 0.28f, 0.26f),
			Weight = WeightClass.Heavy,
			// Heavy movement, like Lugnut's: slower on the ground and in the air, falls harder.
			RunSpeed = 750.0f,
			AirSpeed = 630.0f,
			JumpForce = 1494.0f,
			AirJumpForce = 1554.0f,
			Gravity = 4840.0f,
			BodySize = new Vector2(84.0f, 146.0f),
			RigPath = "res://fighters/doombot/rig.json",
			// Long legs and a narrow body read small at the standard height; a little bigger.
			VisualScale = 1.1f,
			TrailColor = new Color(0.95f, 0.30f, 0.28f),
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.DoomBotPlaceholder());
		data.Taunt = Taunts.Make(AttackAnim.Spread);
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
			// low jump force takes away. Solved for roughly 180px, and 200px for the air jump.
			JumpForce = 1032.0f,
			AirJumpForce = 1083.0f,
			Gravity = 2950.0f,
			MaxFallSpeed = 1100.0f,
			FastFallSpeed = 2200.0f,
			// Elim drew him with a wide ball on long legs, so the box is taller than the stand-in's.
			BodySize = new Vector2(84.0f, 150.0f),
			RigPath = "res://fighters/circy/rig.json",
			VisualScale = 1.0f,
			TumblesWhenHit = true,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Circy(),
			CharacterNormals.Circy, CharacterNormals.CircyJab());
		// Circy is Elim's: he does a star jump, and says nothing Elim did not give him to say.
		data.Taunt = Taunts.Make(AttackAnim.Spread, propArt: "");
		return data;
	}
}
