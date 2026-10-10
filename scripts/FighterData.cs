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
	/// Who drew this fighter, credited on the select screen. Empty for the computer-drawn stand-ins.
	/// </summary>
	[Export] public string Artist { get; set; } = "";

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

	/// <summary>
	/// Builds up heat as it fights - attacking, landing hits, and a little from being hit - and
	/// cools slowly when it stops. Visible: the drawing glows hotter and smokes, and flashes at
	/// full heat. A <see cref="SpecialKind.Vent"/> move lets it all out at once; sitting at full
	/// heat too long instead overheats, stalling the fighter in a cloud of steam. Numbers in
	/// <see cref="Heat"/>. DoomBot's furnace.
	/// </summary>
	[Export] public bool HasHeat { get; set; } = false;

	/// <summary>
	/// Extra frames stuck at the end of an attack that hit nobody - joints that seize up after a
	/// missed swing. Hitting, or having the hit blocked, costs nothing extra. DoomBot's rusty
	/// joints; a reusable trait for anything clumsy.
	/// </summary>
	[Export] public int WhiffLagFrames { get; set; } = 0;

	/// <summary>
	/// Arms that reach out as machinery rather than stretching: steel rods slide out of the
	/// shoulder and the forearm rides out on the end at its own size. Stretching belongs to Circy;
	/// a robot's arm telescopes. DoomBot.
	/// </summary>
	[Export] public bool TelescopingArms { get; set; } = false;

	/// <summary>
	/// Past this much heat (0 to 1), every normal attack he lands also sets them burning a little,
	/// and his claws glow to say so. Zero never. DoomBot's glowing claws; needs HasHeat.
	/// </summary>
	[Export] public float HotHitsFrom { get; set; } = 0.0f;

	/// <summary>
	/// The air jump becomes a dash: this fast, for a fixed few frames, along one of eight
	/// directions - the way the stick points, or straight ahead with it centred - with no gravity
	/// while it lasts. Zero keeps an ordinary air jump. EdgeLord's super speed.
	/// </summary>
	[Export] public float AirDashSpeed { get; set; } = 0.0f;

	/// <summary>
	/// How much of a hit a block lets through, as a multiple of what a block usually lets through
	/// (Tuning.BlockDamageMultiplier and BlockKnockbackMultiplier). 1 is an ordinary block.
	/// Triguy's is weak: blocking is standing still, and he is terrible at that.
	/// </summary>
	[Export] public float BlockLeak { get; set; } = 1.0f;

	/// <summary>
	/// Moves like a machine: every animation held and snapped from pose to pose instead of
	/// flowing, joints settling on whole steps of angle, a faint servo judder, and a stomping walk
	/// that shakes the floor. DoomBot. See FighterRig.Robotic.
	/// </summary>
	[Export] public bool Robotic { get; set; } = false;

	/// <summary>
	/// Whether block plus a direction on the ground rolls. False for DoomBot: a factory robot does
	/// not tumble across the floor - he blocks, spot-dodges and air-dodges, but to get out of a
	/// corner he has to walk, jump or fight. Eric's call, 2026-10-05.
	/// </summary>
	[Export] public bool CanRoll { get; set; } = true;

	/// <summary>
	/// Carries the weapon in his hand on his shoulder (FighterAnimations.CarryOnShoulder) whenever
	/// he is on the ground and not attacking - standing, running, crouching, blocking, landing -
	/// and through any move marked MoveData.CarryOnShoulder. Lug and his sledgehammer: Eric's
	/// calls, 2026-10-08.
	/// </summary>
	[Export] public bool ShouldersProp { get; set; } = false;

	/// <summary>
	/// Stands about with a hand on his hip and a fist up (FighterAnimations.HandOnHip) rather than
	/// with his arms hanging - when he is just standing, nothing else. Flambe, a hotshot: Eric's
	/// call, 2026-10-08.
	/// </summary>
	[Export] public bool HandOnHip { get; set; } = false;

	// --- Moveset -------------------------------------------------------------
	// M1 ships one placeholder attack. M4 replaces this with the full 16-move set built from
	// the shared default moveset plus four specials.

	/// <summary>
	/// Weight is not only a survivability stat - it retunes the whole moveset. Heavy hits
	/// harder and slower, light faster and weaker. See .ai/character-design.md.
	/// </summary>
	[Export] public WeightClass Weight { get; set; } = WeightClass.Medium;

	/// <summary>
	/// How heavy his body is to move, for the rig's joint springs (FighterRig.Inertia): a
	/// heavyweight's limbs swing on further and settle later, a lightweight's snap round.
	/// </summary>
	public float Inertia => Weight == WeightClass.Heavy ? 1.3f : Weight == WeightClass.Light ? 0.85f : 1.0f;

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

	/// <summary>The grab (block plus attack on the ground) - see <see cref="Grabs"/>.</summary>
	public MoveData Grab = Grabs.Grab();

	/// <summary>The four throws a grab leads to: forward, back, up, down. Built by <see cref="Grabs"/>.</summary>
	public MoveData[] Throws = System.Array.Empty<MoveData>();

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
			// Standing about, a hand on his hip.
			HandOnHip = true,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Fire(),
			CharacterNormals.Swift, CharacterNormals.SwiftJab());
		data.Taunt = Taunts.Make(AttackAnim.HangUp, propArt: "");
		data.TauntLine = "Too hot for you!";
		data.Throws = Grabs.Flambe();
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
			// On the ground, the sledgehammer rests on his shoulder.
			ShouldersProp = true,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Construction(),
			CharacterNormals.Lug, CharacterNormals.LugJab());
		data.Taunt = Taunts.Make(AttackAnim.OverheadArc, propArt: "tool_sledgehammer", extra: "hardhat");
		data.TauntLine = "Break's over!";
		data.Throws = Grabs.Construction();
		return data;
	}

	/// <summary>
	/// EdgeLord, drawn by Eric: "stretchy arms, super speed, infinite swords." The stretchy arms
	/// went to Circy, whose thing stretching is; the super speed is the fastest run in the game
	/// and an air dash in place of a second jump. Light, which is also his weakness - easy to
	/// launch - and his normals are ordinary sword swings, so his range comes from his specials. The art is a placeholder until his
	/// own coloured drawing arrives (see tools/art/cut_edgelord.py).
	/// </summary>
	public static FighterData EdgeLord()
	{
		var data = new FighterData
		{
			DisplayName = "EdgeLord",
			Artist = "Eric",
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
			AirDashSpeed = 1500.0f,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.EdgeLord(),
			CharacterNormals.EdgeLord, CharacterNormals.EdgeLordJab());
		data.Taunt = Taunts.Make(AttackAnim.PointUp, propArt: "sword_claymore");
		data.TauntLine = "Try to keep up.";
		data.Throws = Grabs.EdgeLord();
		return data;
	}

	/// <summary>
	/// DoomBot, drawn and designed by Eric: "a factory robot that went rogue", cut from the drawing
	/// by tools/art/cut_doombot.py. His sheet is fighters/doombot/sheet.md. What he asked for,
	/// in stats:
	///   - "strong, hard to knock over": Heavy - the most damage and the most weight
	///   - "long reach": long arms on every normal, a piston punch, and a grab that crosses half the stage
	///   - "overheats": HasHeat - sit at full heat too long and he stalls in steam
	///   - "rusty joints": WhiffLagFrames - stuck for a moment after any swing that misses
	///   - "big windups": startups a few frames past the baseline, and a higher AnimationDrama so
	///     every windup is drawn big enough to see coming
	/// </summary>
	public static FighterData DoomBot()
	{
		const float Size = 1.75f;
		var data = new FighterData
		{
			DisplayName = "DoomBot",
			Artist = "Eric",
			PlaceholderColor = new Color(0.86f, 0.28f, 0.26f),
			Weight = WeightClass.Heavy,
			// Heavy movement, like Lugnut's: slower on the ground and in the air, falls harder.
			RunSpeed = 750.0f,
			AirSpeed = 630.0f,
			JumpForce = 1494.0f,
			AirJumpForce = 1554.0f,
			Gravity = 4840.0f,
			// 1.75 times the size he was drawn at against everyone else - a factory machine, not a
			// person - and the box you have to hit grows with him.
			BodySize = new Vector2(84.0f, 146.0f) * Size,
			RigPath = "res://fighters/doombot/rig.json",
			// Long legs and a narrow body read small at the standard height; a little bigger.
			VisualScale = 1.1f,
			AnimationDrama = 1.15f,
			TrailColor = new Color(0.95f, 0.30f, 0.28f),
			HasHeat = true,
			WhiffLagFrames = 10,
			TelescopingArms = true,
			HotHitsFrom = 0.5f,
			// "A factory robot that went rogue": he moves like one (Eric's call, 2026-10-04).
			Robotic = true,
			// No rolling: he stays in his block instead.
			CanRoll = false,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.DoomBot(),
			CharacterNormals.DoomBot, CharacterNormals.DoomBotJab());
		// His moves are authored at the size he used to be; every hitbox grows with him.
		foreach (MoveData move in data.Moves) move?.ScaleReach(Size);
		data.Taunt = Taunts.Make(AttackAnim.Spread);
		data.Throws = Grabs.DoomBot();
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
			Artist = "Elim",
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
		data.Throws = Grabs.Circy();
		return data;
	}

	/// <summary>
	/// Triguy, drawn and designed by Elim - his second fighter, after Circy - and cut from Elim's
	/// drawings by tools/art/cut_triguy.py. His sheet is fighters/triguy/sheet.md. "A triangle with
	/// limbs, a face, and a top hat." What he asked for, in stats:
	///   - "he is reeeaaaaally fast": Light, and the fastest runner in the game
	///   - "terrible at standing still": slippery feet - he skids a long way every time he stops
	///     (a very low GroundFriction) - and a weak block (BlockLeak), because blocking is
	///     standing still
	/// </summary>
	public static FighterData Triguy()
	{
		var data = new FighterData
		{
			DisplayName = "Triguy",
			Artist = "Elim",
			// The blue Elim drew his tears and his trampoline in.
			PlaceholderColor = new Color(0.10f, 0.40f, 1.0f),
			Weight = WeightClass.Light,
			RunSpeed = 1180.0f,
			AirSpeed = 820.0f,
			// Gets going as fast as anyone - it is the stopping he cannot do. Ordinary feet stop
			// in a few frames; his take most of a second and slide about a body length and a half.
			GroundFriction = 2600.0f,
			JumpForce = 1557.0f,
			AirJumpForce = 1633.0f,
			Gravity = 4100.0f,
			// The triangle is wide - its point sticks out a long way in front - so the box is too.
			BodySize = new Vector2(92.0f, 120.0f),
			RigPath = "res://fighters/triguy/rig.json",
			VisualScale = 1.0f,
			AnimationDrama = 1.2f,
			TrailColor = new Color(0.10f, 0.40f, 1.0f),
			BlockLeak = 1.8f,
		};
		data.Moves = DefaultMoveset.Build(data.Weight, Specials.Triguy(),
			CharacterNormals.Triguy, CharacterNormals.TriguyJab());
		// His three taunts, picked at random, exactly as Elim asked - only for show here. The same
		// three poses with invincibility are his neutral special (Specials.ShowOff).
		data.Taunt = Taunts.Posed(Specials.TriguyPoses);
		data.TauntLine = "That's, really kewl";
		data.Throws = Grabs.Triguy();
		return data;
	}
}
