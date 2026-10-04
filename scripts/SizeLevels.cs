/// <summary>
/// The three sizes a fighter with a <see cref="SpecialKind.Resize"/> special can be, and what
/// each one costs. First built for Circy, who stretches his legs to get tall and squashes them to
/// get short.
///
/// Every size has to be a trade, never an upgrade, or the right answer is to sit in one size
/// all match and the special stops being a choice. The two ends are meant to feel like two
/// different fighters, so the gaps are big:
///   - TALL: big, hard-hitting attacks and a big beam - but slow to move and slow to swing, and
///     a huge target
///   - SHORT: fast on his feet and quick to swing, and a small target - but small, weak attacks
///     and a thin beam
/// </summary>
public static class SizeLevels
{
	public const int Short = -1;
	public const int Normal = 0;
	public const int Tall = 1;

	/// <summary>Leg length, as a multiple of the drawn legs.</summary>
	public static float LegStretch(int level) => level < 0 ? 0.35f : level > 0 ? 2.4f : 1.0f;

	/// <summary>Run and air speed multiplier.</summary>
	public static float Speed(int level) => level < 0 ? 1.45f : level > 0 ? 0.72f : 1.0f;

	/// <summary>Projectile radius multiplier.</summary>
	public static float ProjectileSize(int level) => level < 0 ? 0.6f : level > 0 ? 1.9f : 1.0f;

	/// <summary>Projectile damage multiplier.</summary>
	public static float ProjectileDamage(int level) => level < 0 ? 0.7f : level > 0 ? 1.4f : 1.0f;

	/// <summary>
	/// How far a normal attack reaches and how big its hitbox is. Measured up from the feet, so a
	/// low kick stays at the floor and a high one goes over a tall head.
	/// </summary>
	public static float AttackReach(int level) => level < 0 ? 0.6f : level > 0 ? 1.55f : 1.0f;

	/// <summary>A normal attack's damage - and with it, its knockback.</summary>
	public static float AttackDamage(int level) => level < 0 ? 0.7f : level > 0 ? 1.35f : 1.0f;

	/// <summary>A normal attack's startup and endlag: quicker short, slower tall.</summary>
	public static float AttackFrames(int level) => level < 0 ? 0.8f : level > 0 ? 1.25f : 1.0f;

	public static string Describe(int level) => level < 0 ? "short" : level > 0 ? "tall" : "normal";
}
