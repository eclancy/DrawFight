/// <summary>
/// Where every random number that can change what happens comes from - which item drops, a
/// taunt, the computer's pick. Played online, every copy of the game has to roll the same numbers
/// in the same order (NetSession, .ai/online.md), so there each new generator is seeded from the
/// session's seed, in turn; on one computer it is as random as ever. Purely visual randomness
/// (spark scatter, a portrait's facing) may use anything.
/// </summary>
public static class GameRandom
{
	static System.Random seeds;

	/// <summary>Every generator from now on follows <paramref name="seed"/> - the same on every copy in a session.</summary>
	public static void Seed(int seed) => seeds = new System.Random(seed);

	/// <summary>Back to unseeded, after a session.</summary>
	public static void Unseed() => seeds = null;

	/// <summary>A new generator: the session's next, or an unseeded one.</summary>
	public static System.Random New() => seeds != null ? new System.Random(seeds.Next()) : new System.Random();
}
