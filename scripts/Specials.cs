using Godot;

/// <summary>
/// The four specials for each fighter.
///
/// Swift's and Lug's are placeholders built to the shape a real kid's answers would take, and go
/// away with the stick figures. Circy's are the first real ones, written from Elim's character
/// sheet - see .ai/character-design.md for how a description becomes a special.
///
/// Two rules hold whatever a kid asks for:
///   - the UP special must give real vertical recovery, always, or the fighter is unplayable
///   - the NEUTRAL special is the signature, built as literally as it can be built
/// </summary>
public static class Specials
{
	// =========================================================================
	// SWIFT - fire
	// =========================================================================

	public static MoveData[] Fire()
	{
		return new[] { Fireball(), FlameDash(), FlareJump(), EmberTrap() };
	}

	/// <summary>Neutral: the signature. A fireball, built as literally as it can be built.</summary>
	static MoveData Fireball() => new MoveData
	{
		MoveName = "Fireball",
		Anim = AttackAnim.Punch,
		StartupFrames = 12, ActiveFrames = 2, EndlagFrames = 22,
		Damage = 9.0f, BaseKnockback = 26.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = 44.0f,
		HitboxOffset = new Vector2(60.0f, -20.0f), HitboxRadius = 36.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 1050.0f, SpecialGravity = 240.0f, SpecialLifetime = 78,
		FxColor = new Color(0.97f, 0.52f, 0.20f), FxRadius = 34.0f,
	};

	/// <summary>Side: closes distance wrapped in flame. Approach tool.</summary>
	static MoveData FlameDash() => new MoveData
	{
		MoveName = "Flame Dash",
		Anim = AttackAnim.Lunge,
		StartupFrames = 10, ActiveFrames = 8, EndlagFrames = 24,
		Damage = 10.0f, BaseKnockback = 34.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 48.0f,
		HitboxOffset = new Vector2(44.0f, -16.0f), HitboxRadius = 56.0f,
		CarriesMomentum = true,
		Special = SpecialKind.Dash,
		SpecialSpeed = 1450.0f,
		FxColor = new Color(0.99f, 0.66f, 0.24f), FxRadius = 40.0f,
	};

	/// <summary>
	/// Up: the recovery. Rides a jet of fire upward. Non-negotiable that this gets you back
	/// to the stage - a fighter who cannot recover from below is unplayable whatever else it has.
	/// </summary>
	static MoveData FlareJump() => new MoveData
	{
		MoveName = "Flare Jump",
		Anim = AttackAnim.UpSmash,
		StartupFrames = 7, ActiveFrames = 10, EndlagFrames = 20,
		Damage = 8.0f, BaseKnockback = 28.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = 76.0f,
		HitboxOffset = new Vector2(10.0f, -30.0f), HitboxRadius = 52.0f,
		Special = SpecialKind.Recovery,
		SpecialRise = 1750.0f, SpecialSpeed = 340.0f,
		FxColor = new Color(0.99f, 0.78f, 0.30f), FxRadius = 42.0f,
	};

	/// <summary>Down: leaves a patch of fire burning on the ground. Zoning and ledge control.</summary>
	static MoveData EmberTrap() => new MoveData
	{
		MoveName = "Ember Trap",
		Anim = AttackAnim.LowKick,
		StartupFrames = 14, ActiveFrames = 2, EndlagFrames = 26,
		Damage = 6.0f, BaseKnockback = 40.0f, KnockbackGrowth = 0.6f,
		LaunchAngleDegrees = 70.0f,
		HitboxOffset = new Vector2(48.0f, 20.0f), HitboxRadius = 44.0f,
		Special = SpecialKind.Trap,
		SpecialLifetime = 260,
		FxColor = new Color(0.95f, 0.42f, 0.22f), FxRadius = 46.0f,
	};

	// =========================================================================
	// CIRCY - drawn and described by Elim
	//
	// "A yellow sphere with limbs, eyes and a mouth." Amazing at stretching, falling slowly
	// and self-destructing; terrible at jumping, and rolls over when he gets hit.
	// =========================================================================

	public static MoveData[] Circy()
	{
		return new[] { Stretch(), LaserBeam(), GrapplingHook(), CircyBomb() };
	}

	/// <summary>One of Elim's effect drawings, or null so the move falls back to a crayon circle.</summary>
	static Texture2D CircyFx(string name)
	{
		string path = $"res://fighters/circy/poses/{name}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	/// <summary>
	/// Neutral: the signature, and Elim's coolest move. Hold special and push the stick up to
	/// grow his legs long, down to squash them short. Tall makes the laser bigger; short makes
	/// him faster. See <see cref="SizeLevels"/> for what each size costs.
	///
	/// On the sheet it was "special with the stick up / down", but up-special and down-special
	/// are the grappling hook and the bomb. So you press special on its own and THEN steer with
	/// the stick - the same gesture, one beat later, and the recovery stays where it must be.
	/// </summary>
	static MoveData Stretch() => new MoveData
	{
		MoveName = "Stretch",
		// The active window is how long the stance lasts while the button is held. Letting go
		// ends it early, so a quick change costs only a few frames.
		StartupFrames = 4, ActiveFrames = 60, EndlagFrames = 6,
		Damage = 0.0f, BaseKnockback = 0.0f, KnockbackGrowth = 0.0f,
		HitboxRadius = 0.0f,
		Special = SpecialKind.Resize,
		FxColor = new Color(0.98f, 0.86f, 0.30f), FxRadius = 0.0f,
	};

	/// <summary>
	/// Side: "a move for reaching someone far away - he can shoot a laser beam." He charges it at
	/// his hands for a third of a second, then it shoots out from them, burst end first, and
	/// stays attached to him - so it works just as well fired in the air. Long-range and light: a
	/// poke that stops people camping at a distance, not a finisher. Its thickness and damage
	/// follow his height, which is the payoff for being tall.
	///
	/// The charge is the price of the reach. A beam this long with no wind-up would be the only
	/// move worth using.
	/// </summary>
	static MoveData LaserBeam() => new MoveData
	{
		MoveName = "Laser Beam",
		Anim = AttackAnim.Punch,
		StartupFrames = 20, ActiveFrames = 2, EndlagFrames = 24,
		Damage = 7.0f, BaseKnockback = 30.0f, KnockbackGrowth = 0.8f,
		LaunchAngleDegrees = 30.0f,
		HitboxOffset = new Vector2(58.0f, -30.0f), HitboxRadius = 20.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 2600.0f, SpecialGravity = 0.0f, SpecialLifetime = 30,
		Beam = true, Reach = 760.0f,
		FxColor = new Color(0.95f, 0.30f, 0.36f), FxRadius = 20.0f,
		FxTexture = CircyFx("laser"),
	};

	/// <summary>
	/// Up: the recovery. "The grappling hook fires diagonally based on the direction you're
	/// facing. Whether it hits anything or not, it launches Circy toward the hook. He hits anyone
	/// in the way, and takes some damage too."
	///
	/// He hangs in the air while the hook flies out, then is pulled up and forward at 56 degrees.
	/// The hook never needs to catch on anything, because a recovery that can miss is a recovery
	/// that loses stocks for reasons a kid cannot see.
	/// </summary>
	static MoveData GrapplingHook() => new MoveData
	{
		MoveName = "Grappling Hook",
		Anim = AttackAnim.Uppercut,
		StartupFrames = 12, ActiveFrames = 14, EndlagFrames = 18,
		Damage = 9.0f, BaseKnockback = 34.0f, KnockbackGrowth = 0.9f,
		LaunchAngleDegrees = 60.0f,
		HitboxOffset = new Vector2(18.0f, -10.0f), HitboxRadius = 58.0f,
		Special = SpecialKind.Recovery,
		DelayedLaunch = true, TetherLength = 420.0f,
		SpecialRise = 1500.0f, SpecialSpeed = 1000.0f,
		SelfDamage = 3.0f,
		FxColor = new Color(0.42f, 0.44f, 0.52f), FxRadius = 18.0f,
	};

	/// <summary>
	/// Down: "a move that protects you." He stands still, shrinks down and turns into a bomb.
	/// When someone attacks him, or the fuse runs out, he explodes: the other fighter takes the
	/// damage and the knockback, and Circy takes less damage and no knockback.
	///
	/// It is a counter with a price. Hit it and it goes off in your face; wait it out and he has
	/// spent two seconds standing still for nothing, and still pays the self-damage. Hitting it
	/// from a distance with a projectile is the answer, and the reason it is not unbeatable.
	/// </summary>
	static MoveData CircyBomb() => new MoveData
	{
		MoveName = "Circy Bomb",
		StartupFrames = 18, ActiveFrames = 120, EndlagFrames = 22,
		Damage = 16.0f, BaseKnockback = 42.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 58.0f,
		HitboxOffset = Vector2.Zero, HitboxRadius = 0.0f,
		Special = SpecialKind.Bomb,
		SpecialLifetime = 9,
		SelfDamage = 5.0f,
		FxColor = new Color(0.98f, 0.62f, 0.22f), FxRadius = 128.0f,
		FxTexture = CircyFx("boom"),
	};

	// =========================================================================
	// EDGELORD - light, swords. "Stretchy arms, super speed, infinite swords."
	//
	// His art here is a placeholder: every sword and effect is ours, standing in until his own
	// drawings of the moves arrive (see tools/art/cut_edgelord.py).
	// =========================================================================

	public static MoveData[] EdgeLord()
	{
		return new[] { InfiniteSwords(), BlurSlash(), GrappleArm(), PlantedBlade() };
	}

	static Texture2D EdgeFx(string name)
	{
		string path = $"res://fighters/edgelord/poses/{name}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	/// <summary>
	/// Neutral: the signature. Press special and four swords appear around him - up, forward,
	/// back and down - and pushing the stick throws that one. Each is a different tool, so the
	/// choice is the skill: the greatsword for someone above, the rapier for someone far away, the
	/// daggers for someone behind, the saw-blade for someone on the ground or below.
	/// </summary>
	static MoveData InfiniteSwords() => new MoveData
	{
		MoveName = "Infinite Swords",
		// The active window is how long the swords stay up to choose from. Letting it run out
		// throws the forward one, so a tap is never wasted.
		StartupFrames = 3, ActiveFrames = 40, EndlagFrames = 8,
		Damage = 0.0f, BaseKnockback = 0.0f, KnockbackGrowth = 0.0f,
		HitboxRadius = 0.0f,
		Special = SpecialKind.Choice,
		Choices = new[] { ThrownGreatsword(), ThrownRapier(), ThrownDaggers(), ThrownSawblade() },
		FxColor = new Color(0.80f, 0.82f, 0.86f), FxRadius = 0.0f,
	};

	/// <summary>Up: a greatsword lobbed high and forward, tumbling. Slow, heavy, and it kills.</summary>
	static MoveData ThrownGreatsword() => new MoveData
	{
		MoveName = "Greatsword",
		Anim = AttackAnim.Uppercut,
		StartupFrames = 16, ActiveFrames = 2, EndlagFrames = 24,
		Damage = 14.0f, BaseKnockback = 30.0f, KnockbackGrowth = 1.0f,
		LaunchAngleDegrees = 62.0f,
		HitboxOffset = new Vector2(30.0f, -60.0f), HitboxRadius = 34.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 520.0f, LaunchLift = 1250.0f, SpecialGravity = 2300.0f, SpecialLifetime = 100,
		FxColor = new Color(0.80f, 0.82f, 0.86f), FxRadius = 34.0f,
		FxTexture = EdgeFx("sword_greatsword"), FxArtSize = 150.0f, FxSpin = 17.0f,
	};

	/// <summary>Forward: a rapier, point first, faster and further than anything. Weak - it is for reach.</summary>
	static MoveData ThrownRapier() => new MoveData
	{
		MoveName = "Rapier",
		Anim = AttackAnim.Punch,
		StartupFrames = 9, ActiveFrames = 2, EndlagFrames = 16,
		Damage = 6.0f, BaseKnockback = 24.0f, KnockbackGrowth = 0.6f,
		LaunchAngleDegrees = 24.0f,
		HitboxOffset = new Vector2(64.0f, -24.0f), HitboxRadius = 16.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 1900.0f, SpecialGravity = 0.0f, SpecialLifetime = 34,
		FxColor = new Color(0.80f, 0.82f, 0.86f), FxRadius = 16.0f,
		FxTexture = EdgeFx("sword_rapier"), FxArtSize = 116.0f, FxAlongFlight = true,
	};

	/// <summary>Back: he turns round and fans three daggers. Quick and short - a get-off-my-back.</summary>
	static MoveData ThrownDaggers() => new MoveData
	{
		MoveName = "Daggers",
		Anim = AttackAnim.PunchBack,
		StartupFrames = 7, ActiveFrames = 12, EndlagFrames = 16,
		Damage = 3.0f, BaseKnockback = 16.0f, KnockbackGrowth = 0.35f,
		LaunchAngleDegrees = 30.0f,
		HitboxOffset = new Vector2(56.0f, -22.0f), HitboxRadius = 12.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 1450.0f, SpecialGravity = 0.0f, SpecialLifetime = 22,
		BurstCount = 3, BurstInterval = 5,
		FxColor = new Color(0.80f, 0.82f, 0.86f), FxRadius = 12.0f,
		FxTexture = EdgeFx("sword_dagger"), FxArtSize = 58.0f, FxAlongFlight = true,
	};

	/// <summary>
	/// Down: the saw-toothed edgeblade, spun low along the floor like a buzzsaw. Slow, lingers
	/// in the way, and pops whoever it catches up into the air for a follow-up.
	/// </summary>
	static MoveData ThrownSawblade() => new MoveData
	{
		MoveName = "Sawblade",
		Anim = AttackAnim.LowSweep,
		StartupFrames = 12, ActiveFrames = 2, EndlagFrames = 20,
		Damage = 8.0f, BaseKnockback = 40.0f, KnockbackGrowth = 0.6f,
		LaunchAngleDegrees = 82.0f,
		HitboxOffset = new Vector2(50.0f, 40.0f), HitboxRadius = 26.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 640.0f, SpecialGravity = 0.0f, SpecialLifetime = 70,
		FxColor = new Color(0.80f, 0.82f, 0.86f), FxRadius = 26.0f,
		FxTexture = EdgeFx("sword_edgeblade"), FxArtSize = 96.0f, FxSpin = 26.0f,
	};

	/// <summary>
	/// Side: Blur Slash. He stands and his blade glints - a star of light that grows and flares -
	/// then he is simply on the other side of you, cutting through on the way. The glint is the
	/// price: it is a long, obvious warning, because a hit this fast with no warning would be
	/// unfair. Once per trip into the air, or it would be a second recovery.
	///
	/// SOUND, when there is any: this is the move that most needs it. A rising ring through the
	/// glint that peaks on the frame he goes, then a sharp cut.
	/// </summary>
	static MoveData BlurSlash() => new MoveData
	{
		MoveName = "Blur Slash",
		Anim = AttackAnim.Lunge,
		StartupFrames = 22, ActiveFrames = 5, EndlagFrames = 22,
		Damage = 11.0f, BaseKnockback = 36.0f, KnockbackGrowth = 0.85f,
		LaunchAngleDegrees = 40.0f,
		HitboxOffset = new Vector2(24.0f, -10.0f), HitboxRadius = 60.0f,
		Special = SpecialKind.Dash,
		Blink = true, OncePerAirtime = true,
		// About 350px in five frames.
		SpecialSpeed = 4200.0f,
		FxColor = new Color(0.97f, 0.93f, 0.70f), FxRadius = 40.0f,
	};

	/// <summary>
	/// Up: the recovery. His arm stretches up and forward and yanks him after it. If it catches
	/// someone on the way out, he reels them in and throws them down as he goes up - one pull
	/// that sends them down and him up. Grabbing never costs the recovery: he rises either way.
	/// This is the roster's one command grab (see .ai/character-design.md).
	/// </summary>
	static MoveData GrappleArm() => new MoveData
	{
		MoveName = "Grapple Arm",
		Anim = AttackAnim.Uppercut,
		StartupFrames = 14, ActiveFrames = 14, EndlagFrames = 18,
		Damage = 7.0f, BaseKnockback = 32.0f, KnockbackGrowth = 0.85f,
		LaunchAngleDegrees = 70.0f,
		HitboxOffset = new Vector2(18.0f, -20.0f), HitboxRadius = 54.0f,
		Special = SpecialKind.Recovery,
		DelayedLaunch = true, TetherLength = 400.0f, StretchArm = true,
		SpecialRise = 1520.0f, SpecialSpeed = 640.0f,
		GrabThrow = new MoveData
		{
			MoveName = "Grapple Throw",
			Damage = 9.0f, BaseKnockback = 44.0f, KnockbackGrowth = 0.6f,
			LaunchAngleDegrees = -84.0f,
			Spikes = true, Unblockable = true,
		},
		FxColor = new Color(0.49f, 0.06f, 0.09f), FxRadius = 18.0f,
	};

	/// <summary>
	/// Down: he stabs a sword into the ground in front of him and leaves it. Anyone who runs into
	/// it is cut and popped up. Two can be out; a third pulls up the oldest. Put down in the air,
	/// it falls point-first until it sticks in whatever is below.
	/// </summary>
	static MoveData PlantedBlade() => new MoveData
	{
		MoveName = "Planted Blade",
		Anim = AttackAnim.BuildDown,
		StartupFrames = 14, ActiveFrames = 2, EndlagFrames = 20,
		Damage = 9.0f, BaseKnockback = 38.0f, KnockbackGrowth = 0.7f,
		LaunchAngleDegrees = 72.0f,
		// Low enough that, on the ground, the point is in the floor.
		HitboxOffset = new Vector2(62.0f, 13.0f), HitboxRadius = 30.0f,
		Special = SpecialKind.Trap,
		SpecialLifetime = 420, SpecialGravity = 3000.0f, MaxOut = 2,
		FxColor = new Color(0.80f, 0.82f, 0.86f), FxRadius = 30.0f,
		FxTexture = EdgeFx("planted"), FxArtSize = 110.0f,
	};

	// =========================================================================
	// LUG - heavy, construction site. Every special is a tool you would find on a building
	// site: a nail gun, a wheelbarrow, a wrecking ball and a steel beam.
	// =========================================================================

	public static MoveData[] Construction()
	{
		return new[] { NailGun(), WheelbarrowCharge(), WreckingBallSwing(), SteelBeamDrop() };
	}

	/// <summary>One of Lug's tool drawings, or null so the move falls back to a crayon shape.</summary>
	static Texture2D LugFx(string name)
	{
		string path = $"res://fighters/lug/poses/{name}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	/// <summary>
	/// Neutral: the signature. A burst of four nails, straight ahead and fast. Each one is weak
	/// and barely pushes, so it is a way to chip at someone who stays out of reach of his
	/// sledgehammer - the one thing a heavy is otherwise bad at.
	/// </summary>
	static MoveData NailGun() => new MoveData
	{
		MoveName = "Nail Gun",
		Anim = AttackAnim.Punch,
		StartupFrames = 12, ActiveFrames = 14, EndlagFrames = 20,
		Damage = 2.5f, BaseKnockback = 12.0f, KnockbackGrowth = 0.35f,
		LaunchAngleDegrees = 20.0f,
		HitboxOffset = new Vector2(70.0f, -26.0f), HitboxRadius = 10.0f,
		Special = SpecialKind.Projectile,
		SpecialSpeed = 1700.0f, SpecialGravity = 0.0f, SpecialLifetime = 32,
		BurstCount = 4, BurstInterval = 4,
		Streak = true,
		FxColor = new Color(0.30f, 0.32f, 0.38f), FxRadius = 10.0f,
	};

	/// <summary>
	/// Side: charges forward behind a wheelbarrow that scoops up whoever is in the way and
	/// dumps them up and forward. Slow to get going, hard to stop once it is.
	/// </summary>
	static MoveData WheelbarrowCharge() => new MoveData
	{
		MoveName = "Wheelbarrow Charge",
		Anim = AttackAnim.Lunge,
		StartupFrames = 14, ActiveFrames = 12, EndlagFrames = 30,
		Damage = 14.0f, BaseKnockback = 40.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 68.0f,
		HitboxOffset = new Vector2(96.0f, 10.0f), HitboxRadius = 60.0f,
		CarriesMomentum = true,
		Special = SpecialKind.Dash,
		SpecialSpeed = 1100.0f,
		HeldArt = "wheelbarrow", HeldArtOffset = new Vector2(92.0f, 22.0f), HeldArtSize = 130.0f,
		FxColor = new Color(0.92f, 0.49f, 0.22f), FxRadius = 46.0f,
	};

	/// <summary>
	/// Up: the recovery. He swings the wrecking ball up and over his head, and it hauls him up
	/// after it. The ball hits anyone above him on the way. A heavy fighter still needs a real
	/// way home; this is a slower one, since he waits for the swing before he rises.
	/// </summary>
	static MoveData WreckingBallSwing() => new MoveData
	{
		MoveName = "Wrecking Ball",
		Anim = AttackAnim.UpSmash,
		StartupFrames = 10, ActiveFrames = 12, EndlagFrames = 26,
		Damage = 13.0f, BaseKnockback = 32.0f, KnockbackGrowth = 0.95f,
		LaunchAngleDegrees = 78.0f,
		HitboxRadius = 58.0f,
		Special = SpecialKind.Recovery,
		DelayedLaunch = true,
		SpecialRise = 1560.0f, SpecialSpeed = 360.0f,
		SwingArt = "wreckingball", SwingLength = 120.0f, SwingArtSize = 78.0f,
		FxColor = new Color(0.30f, 0.30f, 0.34f), FxRadius = 40.0f,
	};

	/// <summary>
	/// Down: he leans down and hammers a steel girder into place under his own feet, and stands
	/// on it. After a moment it shakes and falls - he can jump off it before it goes, and anyone
	/// underneath when it drops is spiked. On the stage it is a step up; off it, it is a place to
	/// stand. Once per trip into the air, and standing on it gives back no jumps.
	/// </summary>
	static MoveData SteelBeamDrop() => new MoveData
	{
		MoveName = "Steel Beam",
		Anim = AttackAnim.BuildDown,
		StartupFrames = 12, ActiveFrames = 3, EndlagFrames = 16,
		Damage = 13.0f, BaseKnockback = 30.0f, KnockbackGrowth = 0.8f,
		LaunchAngleDegrees = -80.0f,
		HitboxRadius = 0.0f,
		Spikes = true,
		Special = SpecialKind.BuildPlatform,
		PlatformHoldFrames = 80, PlatformWidth = 200.0f,
		FxColor = new Color(0.70f, 0.26f, 0.22f), FxRadius = 40.0f,
		FxTexture = LugFx("girder"),
	};
}
