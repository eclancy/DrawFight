/// <summary>
/// The three sizes a fighter with a <see cref="SpecialKind.Resize"/> special can be, and what
/// each one costs. First built for Circy, who stretches his legs to get tall and squashes them to
/// get short.
///
/// Every size has to be a trade, never an upgrade, or the right answer is to sit in one size
/// all match and the special stops being a choice:
///   - TALL: a bigger, harder-hitting beam - but slower, and a much bigger target
///   - SHORT: faster, and a smaller target - but a thin, weak beam
/// </summary>
public static class SizeLevels
{
	public const int Short = -1;
	public const int Normal = 0;
	public const int Tall = 1;

	/// <summary>Leg length, as a multiple of the drawn legs.</summary>
	public static float LegStretch(int level) => level < 0 ? 0.5f : level > 0 ? 1.8f : 1.0f;

	/// <summary>Run and air speed multiplier.</summary>
	public static float Speed(int level) => level < 0 ? 1.22f : level > 0 ? 0.85f : 1.0f;

	/// <summary>Projectile radius multiplier.</summary>
	public static float ProjectileSize(int level) => level < 0 ? 0.7f : level > 0 ? 1.6f : 1.0f;

	/// <summary>Projectile damage multiplier.</summary>
	public static float ProjectileDamage(int level) => level < 0 ? 0.8f : level > 0 ? 1.3f : 1.0f;

	public static string Describe(int level) => level < 0 ? "short" : level > 0 ? "tall" : "normal";
}
