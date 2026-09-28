using Godot;

/// <summary>
/// One attack, as a .tres Resource. New content is authored as resources, not as new classes -
/// the same pattern SpellData uses in the sibling project.
///
/// EVERY DURATION HERE IS AN INTEGER FRAME COUNT at a fixed 60 Hz, never a float in seconds.
/// See .ai/fighting-design.md; retrofitting that rule later means rewriting every move.
/// </summary>
/// <summary>
/// What a special move actually does. Ordinary attacks use None and are pure hitboxes.
/// </summary>
public enum SpecialKind
{
	None,

	/// <summary>Spawns a travelling hazard that carries the move's damage.</summary>
	Projectile,

	/// <summary>Launches the user forward with a hitbox on their own body.</summary>
	Dash,

	/// <summary>Launches the user upward. Every up-special is one of these.</summary>
	Recovery,

	/// <summary>Leaves a stationary hazard behind for a while.</summary>
	Trap,

	/// <summary>Spawns a hazard that falls, and spikes whatever it lands on.</summary>
	Drop,

	/// <summary>
	/// A stance that changes the fighter's size while the button is held: stick up grows, stick
	/// down shrinks. Big and small are real trade-offs - see <see cref="SizeLevels"/>.
	/// </summary>
	Resize,

	/// <summary>
	/// Turns the fighter into a bomb for the active window. Being hit, or the fuse running out,
	/// sets it off: the explosion carries this move's damage and knockback to everyone nearby,
	/// and the fighter pays <see cref="MoveData.SelfDamage"/> with no knockback. The hit that set
	/// it off does nothing, which is what makes it a counter rather than just a trap.
	/// </summary>
	Bomb,
}

[GlobalClass]
public partial class MoveData : Resource
{
	[Export] public string MoveName { get; set; } = "Attack";

	// --- Timing, in frames ---------------------------------------------------

	/// <summary>Wind-up before the hitbox exists. This is the move's "speed".</summary>
	[Export] public int StartupFrames { get; set; } = 5;

	/// <summary>How long the hitbox is live.</summary>
	[Export] public int ActiveFrames { get; set; } = 3;

	/// <summary>Recovery after the hitbox vanishes. This is the move's risk.</summary>
	[Export] public int EndlagFrames { get; set; } = 14;

	public int TotalFrames => StartupFrames + ActiveFrames + EndlagFrames;

	// --- Damage and knockback ------------------------------------------------

	[Export] public float Damage { get; set; } = 8.0f;

	/// <summary>How hard it hits at 0%. High base = a reliable poke that pushes people away.</summary>
	[Export] public float BaseKnockback { get; set; } = 32.0f;

	/// <summary>How hard it scales with percent. High growth = a finisher.</summary>
	[Export] public float KnockbackGrowth { get; set; } = 1.0f;

	/// <summary>Degrees from +X, counter-clockwise. 45 is a standard diagonal launch.</summary>
	[Export] public float LaunchAngleDegrees { get; set; } = 45.0f;

	// --- Hitbox --------------------------------------------------------------
	// M1 supports one circular hitbox per move, positioned relative to the fighter and
	// mirrored by facing. M4 generalises this to a list of shapes with per-frame windows.

	/// <summary>Offset from the fighter's centre. +X is forward; mirrored automatically.</summary>
	[Export] public Vector2 HitboxOffset { get; set; } = new Vector2(62.0f, -20.0f);

	[Export] public float HitboxRadius { get; set; } = 44.0f;

	// --- Flags ---------------------------------------------------------------

	/// <summary>Ignores block reduction entirely. Use sparingly - this is a one-character thing.</summary>
	[Export] public bool Unblockable { get; set; } = false;

	/// <summary>Launches downward with no recovery window. Reserved for down-aerials at M4.</summary>
	[Export] public bool Spikes { get; set; } = false;

	/// <summary>
	/// The move keeps the run speed that started it instead of shedding it. Set on dash
	/// attacks, which are supposed to slide: a dash attack that stops dead on startup is just
	/// a slow jab. It also selects the lunging attack pose, because a move that carries
	/// momentum should look like it is leaning into it.
	/// </summary>
	[Export] public bool CarriesMomentum { get; set; } = false;

	// --- Special behaviour ---------------------------------------------------
	// A special is an archetype plus numbers plus the kid's own effect art. New archetypes
	// should be rare and reusable; see the library in .ai/character-design.md.

	[Export] public SpecialKind Special { get; set; } = SpecialKind.None;

	/// <summary>Projectile launch speed, px/s. Also the dash speed for a Dash special.</summary>
	[Export] public float SpecialSpeed { get; set; } = 900.0f;

	/// <summary>Gravity on a spawned hazard. Zero flies straight; high arcs or drops.</summary>
	[Export] public float SpecialGravity { get; set; } = 0.0f;

	/// <summary>Frames a spawned hazard lives for.</summary>
	[Export] public int SpecialLifetime { get; set; } = 90;

	/// <summary>Upward launch for a Recovery special. Every up-special must have a real one.</summary>
	[Export] public float SpecialRise { get; set; } = 1500.0f;

	/// <summary>
	/// Percent the USER takes when the move connects (or, for a bomb, when it goes off). Never
	/// knockback. This is how "it hurts him too" gets honoured without making the move useless.
	/// </summary>
	[Export] public float SelfDamage { get; set; } = 0.0f;

	/// <summary>
	/// A Recovery that waits: the fighter hangs in the air through startup while a tether flies
	/// out, and the launch happens on the first active frame. A grappling hook rather than a
	/// jump - slower to start, and readable, because the line shows where it is going.
	/// </summary>
	[Export] public bool DelayedLaunch { get; set; } = false;

	/// <summary>How far the drawn tether reaches for a <see cref="DelayedLaunch"/> recovery.</summary>
	[Export] public float TetherLength { get; set; } = 0.0f;

	/// <summary>Draw the projectile as a long beam rather than a ball. Presentation only.</summary>
	[Export] public bool Beam { get; set; } = false;

	/// <summary>Colour of the hand-drawn effect. Crayon-bright; see .ai/art-direction.md.</summary>
	[Export] public Color FxColor { get; set; } = new Color(0.97f, 0.62f, 0.25f);

	/// <summary>Size of the drawn effect, in pixels.</summary>
	[Export] public float FxRadius { get; set; } = 34.0f;

	/// <summary>
	/// The kid's own drawing of the effect - Circy's laser, his explosion. Drawn instead of the
	/// crayon circle when present, sized to <see cref="FxRadius"/> so the art always matches
	/// what actually hits.
	/// </summary>
	[Export] public Texture2D FxTexture { get; set; }

	/// <summary>
	/// A copy of this move retuned for a weight class. The shared default moveset is authored
	/// at medium and every other weight is derived, so a balance change to a tilt lands on all
	/// three weights at once instead of being fixed in three places.
	/// </summary>
	public MoveData Scaled(MoveScale scale)
	{
		return new MoveData
		{
			MoveName = MoveName,
			StartupFrames = Mathf.Max(1, Mathf.RoundToInt(StartupFrames * scale.Startup)),
			ActiveFrames = ActiveFrames,
			EndlagFrames = Mathf.Max(1, Mathf.RoundToInt(EndlagFrames * scale.Endlag)),
			Damage = Damage * scale.Damage,
			BaseKnockback = BaseKnockback * scale.Knockback,
			KnockbackGrowth = KnockbackGrowth * scale.Growth,
			LaunchAngleDegrees = LaunchAngleDegrees,
			HitboxOffset = HitboxOffset * scale.Range,
			HitboxRadius = HitboxRadius * scale.Range,
			Unblockable = Unblockable,
			Spikes = Spikes,
			CarriesMomentum = CarriesMomentum,
			Special = Special,
			SpecialSpeed = SpecialSpeed,
			SpecialGravity = SpecialGravity,
			SpecialLifetime = SpecialLifetime,
			SpecialRise = SpecialRise,
			SelfDamage = SelfDamage,
			DelayedLaunch = DelayedLaunch,
			TetherLength = TetherLength,
			Beam = Beam,
			FxColor = FxColor,
			FxRadius = FxRadius,
			FxTexture = FxTexture,
		};
	}

	/// <summary>
	/// A copy of a projectile resized for a fighter's current size: a bigger beam that hits
	/// harder when tall, a thinner weaker one when short. Knockback growth is left alone so
	/// size never turns a zoning tool into a finisher.
	/// </summary>
	public MoveData Sized(float size, float damage)
	{
		MoveData copy = Scaled(new MoveScale(1.0f, 1.0f, damage, 1.0f, 1.0f, 1.0f));
		copy.FxRadius = FxRadius * size;
		return copy;
	}

	/// <summary>
	/// A fast, weak poke. High-ish base and low growth means it repositions people reliably
	/// and never kills - the safe default shape for a jab.
	/// </summary>
	public static MoveData PlaceholderJab()
	{
		return new MoveData
		{
			MoveName = "Jab",
			StartupFrames = 4,
			ActiveFrames = 3,
			EndlagFrames = 11,
			Damage = 7.0f,
			BaseKnockback = 30.0f,
			KnockbackGrowth = 0.85f,
			LaunchAngleDegrees = 42.0f,
			HitboxOffset = new Vector2(58.0f, -18.0f),
			HitboxRadius = 42.0f,
		};
	}

	/// <summary>
	/// The attack you get for pressing attack while already running. Slower and more committal
	/// than a jab, hits harder, and slides the whole way through - the reward for having built
	/// up speed, and the risk of not being able to stop.
	/// </summary>
	public static MoveData PlaceholderDashAttack()
	{
		return new MoveData
		{
			MoveName = "Dash Attack",
			StartupFrames = 8,
			ActiveFrames = 4,
			EndlagFrames = 21,
			Damage = 11.0f,
			BaseKnockback = 42.0f,
			KnockbackGrowth = 0.95f,
			LaunchAngleDegrees = 52.0f,
			HitboxOffset = new Vector2(70.0f, -16.0f),
			HitboxRadius = 50.0f,
			CarriesMomentum = true,
		};
	}

	/// <summary>A heavier shoulder-charge version of the same idea.</summary>
	public static MoveData PlaceholderHeavyDashAttack()
	{
		return new MoveData
		{
			MoveName = "Barge",
			StartupFrames = 12,
			ActiveFrames = 5,
			EndlagFrames = 27,
			Damage = 16.0f,
			BaseKnockback = 46.0f,
			KnockbackGrowth = 1.0f,
			LaunchAngleDegrees = 48.0f,
			HitboxOffset = new Vector2(80.0f, -20.0f),
			HitboxRadius = 62.0f,
			CarriesMomentum = true,
		};
	}

	/// <summary>
	/// Slow, committal, and scales hard with percent - the shape of a finisher. Deliberately
	/// paired with the heavy placeholder fighter so M1 can be judged on whether a slow strong
	/// move and a fast weak one actually feel different.
	/// </summary>
	public static MoveData PlaceholderHeavySwing()
	{
		return new MoveData
		{
			MoveName = "Swing",
			StartupFrames = 11,
			ActiveFrames = 4,
			EndlagFrames = 24,
			Damage = 15.0f,
			BaseKnockback = 22.0f,
			KnockbackGrowth = 1.0f,
			LaunchAngleDegrees = 48.0f,
			HitboxOffset = new Vector2(76.0f, -24.0f),
			HitboxRadius = 58.0f,
		};
	}
}
