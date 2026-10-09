using Godot;

/// <summary>
/// The non-special attacks every fighter starts from - a jab combo, tilts, smashes, a dash
/// attack and aerials.
///
/// Most are authored once at MEDIUM weight and retuned per fighter by <see cref="WeightProfiles"/>,
/// because designing thirteen moves from scratch is how a character takes a month instead of an
/// afternoon. But scaling numbers alone made every weight feel the same, so each weight class
/// also differs in SHAPE where it shows most:
///   - the jab combo is authored per weight: light is three quick weak hits, medium is three
///     ending in a pop-up, heavy is two slow heavy punches that are easy to see coming
///   - the forward tilt and forward smash use a different animation per weight: light kicks,
///     medium swings, heavy slams
/// The four specials are still where a character's own identity lives.
/// </summary>
public static class DefaultMoveset
{
	/// <summary>
	/// Builds a full moveset for a weight class.
	///
	/// <paramref name="character"/> is how a fighter makes its normals its own: it is handed the
	/// medium baseline moves (before weight scaling) and renames, re-animates and reshapes them -
	/// see <see cref="CharacterNormals"/>. Because it edits the baseline rather than replacing it,
	/// a character's moves stay in the same balance family as everyone else's, and weight still
	/// scales them afterwards. <paramref name="jab"/> is the character's jab combo, authored at
	/// its final numbers. Specials are passed in because they come from the kid's description.
	/// </summary>
	public static MoveData[] Build(WeightClass weight, MoveData[] specials,
		System.Action<MoveData[]> character = null, MoveData jab = null)
	{
		MoveScale scale = WeightProfiles.ScaleFor(weight);
		var moves = new MoveData[(int)MoveSlot.Count];

		moves[(int)MoveSlot.ForwardTilt] = ForwardTilt();
		moves[(int)MoveSlot.UpTilt] = UpTilt();
		moves[(int)MoveSlot.DownTilt] = DownTilt();
		moves[(int)MoveSlot.DashAttack] = DashAttack();
		moves[(int)MoveSlot.NeutralAir] = NeutralAir();
		moves[(int)MoveSlot.ForwardAir] = ForwardAir();
		moves[(int)MoveSlot.BackAir] = BackAir();
		moves[(int)MoveSlot.UpAir] = UpAir();
		moves[(int)MoveSlot.DownAir] = DownAir();
		moves[(int)MoveSlot.ForwardSmash] = ForwardSmash();
		moves[(int)MoveSlot.UpSmash] = UpSmash();
		moves[(int)MoveSlot.DownSmash] = DownSmash();

		character?.Invoke(moves);

		for (var slot = MoveSlot.ForwardTilt; slot < MoveSlot.NeutralSpecial; slot++)
		{
			moves[(int)slot] = moves[(int)slot].Scaled(scale);
		}

		// Authored at final numbers, so not scaled again.
		moves[(int)MoveSlot.Jab] = jab ?? JabFor(weight);

		// Specials are NOT scaled. They are authored per character from the kid's own
		// description, so scaling them would quietly overrule what he asked for.
		for (int i = 0; i < 4 && i < specials.Length; i++)
		{
			moves[(int)MoveSlot.NeutralSpecial + i] = specials[i];
		}

		return moves;
	}

	static MoveData JabFor(WeightClass weight)
	{
		switch (weight)
		{
			case WeightClass.Light: return LightJab();
			case WeightClass.Heavy: return HeavyJab();
			default: return Jab();
		}
	}

	// --- Grounded ------------------------------------------------------------

	/// <summary>
	/// Light: three quick, weak hits - punch, punch, kick. Fast enough to mash, and weak enough
	/// that the reward for landing it is position rather than damage.
	/// </summary>
	public static MoveData LightJab() => new MoveData
	{
		MoveName = "Jab",
		StartupFrames = 2, ActiveFrames = 2, EndlagFrames = 8,
		Damage = 2.0f, BaseKnockback = 18.0f, KnockbackGrowth = 0.2f,
		LaunchAngleDegrees = 20.0f,
		HitboxOffset = new Vector2(48.0f, -18.0f), HitboxRadius = 36.0f,
		Anim = AttackAnim.Punch,
		ComboNext = new MoveData
		{
			MoveName = "Jab 2",
			StartupFrames = 2, ActiveFrames = 2, EndlagFrames = 8,
			Damage = 2.0f, BaseKnockback = 19.0f, KnockbackGrowth = 0.2f,
			LaunchAngleDegrees = 20.0f,
			HitboxOffset = new Vector2(50.0f, -18.0f), HitboxRadius = 36.0f,
			Anim = AttackAnim.PunchBack,
			ComboNext = new MoveData
			{
				MoveName = "Jab Kick",
				StartupFrames = 3, ActiveFrames = 3, EndlagFrames = 14,
				Damage = 4.0f, BaseKnockback = 32.0f, KnockbackGrowth = 0.8f,
				LaunchAngleDegrees = 40.0f,
				HitboxOffset = new Vector2(56.0f, -6.0f), HitboxRadius = 40.0f,
				Anim = AttackAnim.FrontKick,
			},
		},
	};

	/// <summary>
	/// Heavy: only two punches, both slow and both hard. The first has enough push that at any
	/// real percent the second can be dodged - a heavy jab is a commitment, not a string.
	/// </summary>
	public static MoveData HeavyJab() => new MoveData
	{
		MoveName = "Heavy Jab",
		StartupFrames = 7, ActiveFrames = 3, EndlagFrames = 14,
		Damage = 7.0f, BaseKnockback = 30.0f, KnockbackGrowth = 0.4f,
		LaunchAngleDegrees = 30.0f,
		HitboxOffset = new Vector2(68.0f, -20.0f), HitboxRadius = 48.0f,
		Anim = AttackAnim.HeavyPunch,
		ComboNext = new MoveData
		{
			MoveName = "Heavy Jab 2",
			StartupFrames = 9, ActiveFrames = 4, EndlagFrames = 24,
			Damage = 10.0f, BaseKnockback = 38.0f, KnockbackGrowth = 0.85f,
			LaunchAngleDegrees = 38.0f,
			HitboxOffset = new Vector2(78.0f, -20.0f), HitboxRadius = 56.0f,
			Anim = AttackAnim.PunchBack,
			CarriesMomentum = true,
		},
	};

	/// <summary>
	/// Tap attack for a three-hit combo: two quick jabs, then a finisher. The first two barely
	/// push, so the target is still in reach and still in hitstun when the next one lands; only
	/// the finisher knocks them away. Stopping after one jab is still a fast poke.
	/// </summary>
	static MoveData Jab() => new MoveData
	{
		MoveName = "Jab",
		StartupFrames = 3, ActiveFrames = 2, EndlagFrames = 10,
		Damage = 3.0f, BaseKnockback = 20.0f, KnockbackGrowth = 0.25f,
		LaunchAngleDegrees = 20.0f,
		HitboxOffset = new Vector2(54.0f, -18.0f), HitboxRadius = 40.0f,
		Anim = AttackAnim.Punch,
		ComboNext = Jab2(),
	};

	static MoveData Jab2() => new MoveData
	{
		MoveName = "Jab 2",
		StartupFrames = 2, ActiveFrames = 2, EndlagFrames = 10,
		Damage = 3.0f, BaseKnockback = 22.0f, KnockbackGrowth = 0.25f,
		LaunchAngleDegrees = 20.0f,
		HitboxOffset = new Vector2(58.0f, -18.0f), HitboxRadius = 42.0f,
		Anim = AttackAnim.PunchBack,
		ComboNext = JabFinisher(),
	};

	/// <summary>
	/// The end of the medium jab combo: an uppercut that pops them up rather than away, which
	/// sets up an up air. High base, low growth - it never kills.
	/// </summary>
	static MoveData JabFinisher() => new MoveData
	{
		MoveName = "Jab Uppercut",
		StartupFrames = 4, ActiveFrames = 3, EndlagFrames = 16,
		Damage = 5.0f, BaseKnockback = 34.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = 72.0f,
		HitboxOffset = new Vector2(50.0f, -40.0f), HitboxRadius = 46.0f,
		Anim = AttackAnim.Uppercut,
	};

	/// <summary>The spacing tool. Longest reach of the grounded normals.</summary>
	static MoveData ForwardTilt() => new MoveData
	{
		MoveName = "Forward Tilt",
		Anim = AttackAnim.PunchBack,
		StartupFrames = 8, ActiveFrames = 3, EndlagFrames = 16,
		Damage = 10.0f, BaseKnockback = 34.0f, KnockbackGrowth = 1.0f,
		LaunchAngleDegrees = 38.0f,
		HitboxOffset = new Vector2(86.0f, -22.0f), HitboxRadius = 48.0f,
	};

	/// <summary>Pops people straight up, which is how a juggle starts.</summary>
	static MoveData UpTilt() => new MoveData
	{
		MoveName = "Up Tilt",
		Anim = AttackAnim.Uppercut,
		StartupFrames = 6, ActiveFrames = 4, EndlagFrames = 14,
		Damage = 8.0f, BaseKnockback = 28.0f, KnockbackGrowth = 1.05f,
		LaunchAngleDegrees = 84.0f,
		HitboxOffset = new Vector2(22.0f, -92.0f), HitboxRadius = 52.0f,
	};

	/// <summary>Low and fast, hits people trying to climb back onto the stage.</summary>
	static MoveData DownTilt() => new MoveData
	{
		MoveName = "Down Tilt",
		Anim = AttackAnim.LowKick,
		StartupFrames = 5, ActiveFrames = 3, EndlagFrames = 12,
		Damage = 7.5f, BaseKnockback = 26.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 24.0f,
		HitboxOffset = new Vector2(66.0f, 26.0f), HitboxRadius = 44.0f,
	};

	/// <summary>Committal, slides the whole way through, rewards building up speed.</summary>
	static MoveData DashAttack() => new MoveData
	{
		MoveName = "Dash Attack",
		Anim = AttackAnim.Lunge,
		StartupFrames = 8, ActiveFrames = 4, EndlagFrames = 21,
		Damage = 11.0f, BaseKnockback = 42.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 52.0f,
		HitboxOffset = new Vector2(70.0f, -16.0f), HitboxRadius = 50.0f,
		CarriesMomentum = true,
	};

	// --- Smash attacks --------------------------------------------------------
	// Flick the stick and press attack together; hold attack to charge. These are the
	// finishers - slow to start and slow to recover, so a miss is punished.

	static MoveData ForwardSmash() => new MoveData
	{
		MoveName = "Forward Smash",
		Anim = AttackAnim.SmashSwing,
		StartupFrames = 15, ActiveFrames = 3, EndlagFrames = 30,
		Damage = 15.0f, BaseKnockback = 26.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 38.0f,
		HitboxOffset = new Vector2(92.0f, -20.0f), HitboxRadius = 56.0f,
		Chargeable = true, CarriesMomentum = true,
	};

	static MoveData UpSmash() => new MoveData
	{
		MoveName = "Up Smash",
		Anim = AttackAnim.UpSmash,
		StartupFrames = 12, ActiveFrames = 5, EndlagFrames = 28,
		Damage = 14.0f, BaseKnockback = 30.0f, KnockbackGrowth = 1.08f,
		LaunchAngleDegrees = 88.0f,
		HitboxOffset = new Vector2(10.0f, -100.0f), HitboxRadius = 62.0f,
		Chargeable = true,
	};

	/// <summary>Hits both sides at once, low - the answer to being surrounded or edge-guarded.</summary>
	static MoveData DownSmash() => new MoveData
	{
		MoveName = "Down Smash",
		Anim = AttackAnim.Split,
		StartupFrames = 10, ActiveFrames = 4, EndlagFrames = 30,
		Damage = 13.0f, BaseKnockback = 26.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = 28.0f,
		HitboxOffset = new Vector2(0.0f, 30.0f), HitboxRadius = 92.0f,
		Chargeable = true,
	};

	// --- Aerials -------------------------------------------------------------

	/// <summary>Quick, all-round, comes out fast enough to get you out of trouble.</summary>
	static MoveData NeutralAir() => new MoveData
	{
		MoveName = "Neutral Air",
		Anim = AttackAnim.Nair,
		StartupFrames = 5, ActiveFrames = 6, EndlagFrames = 12,
		Damage = 8.0f, BaseKnockback = 26.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 45.0f,
		HitboxOffset = new Vector2(16.0f, -14.0f), HitboxRadius = 62.0f,
	};

	static MoveData ForwardAir() => new MoveData
	{
		MoveName = "Forward Air",
		Anim = AttackAnim.Fair,
		StartupFrames = 9, ActiveFrames = 3, EndlagFrames = 16,
		Damage = 11.0f, BaseKnockback = 30.0f, KnockbackGrowth = 1.05f,
		LaunchAngleDegrees = 40.0f,
		HitboxOffset = new Vector2(82.0f, -18.0f), HitboxRadius = 48.0f,
	};

	/// <summary>
	/// The reliable finisher. Low base, high growth - does nothing until it kills. It hits behind
	/// him without turning him round, so it launches behind him too: 144 degrees is the usual 36
	/// up and away, pointed back. At 36 every back air threw people forward, through the one who
	/// hit them (Eric, 2026-10-09).
	/// </summary>
	static MoveData BackAir() => new MoveData
	{
		MoveName = "Back Air",
		Anim = AttackAnim.Bair,
		StartupFrames = 8, ActiveFrames = 3, EndlagFrames = 18,
		Damage = 12.0f, BaseKnockback = 20.0f, KnockbackGrowth = 1.22f,
		LaunchAngleDegrees = 144.0f,
		HitboxOffset = new Vector2(-78.0f, -18.0f), HitboxRadius = 48.0f,
	};

	static MoveData UpAir() => new MoveData
	{
		MoveName = "Up Air",
		Anim = AttackAnim.Uair,
		StartupFrames = 6, ActiveFrames = 4, EndlagFrames = 13,
		Damage = 9.0f, BaseKnockback = 24.0f, KnockbackGrowth = 1.15f,
		LaunchAngleDegrees = 88.0f,
		HitboxOffset = new Vector2(8.0f, -86.0f), HitboxRadius = 54.0f,
	};

	/// <summary>
	/// Spikes: launches straight down with no recovery window. The most dangerous move in the
	/// game to be hit by off-stage, and the most committal to throw out.
	/// </summary>
	static MoveData DownAir() => new MoveData
	{
		MoveName = "Down Air",
		Anim = AttackAnim.Dair,
		StartupFrames = 11, ActiveFrames = 3, EndlagFrames = 22,
		Damage = 13.0f, BaseKnockback = 28.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = -78.0f,
		HitboxOffset = new Vector2(14.0f, 72.0f), HitboxRadius = 50.0f,
		Spikes = true,
	};
}
