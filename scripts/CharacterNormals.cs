using Godot;

/// <summary>
/// Each fighter's own normal attacks - everything but the four specials.
///
/// A character does not author its normals from nothing. It is handed the shared medium
/// baseline from <see cref="DefaultMoveset"/> and makes each move its own: a name, an
/// animation, and a reshaped hitbox, angle or timing that fits who the fighter is. Staying
/// close to the baseline keeps every fighter in the same balance family; weight class still
/// scales the result afterwards, so a light fighter's moves stay fast and weak and a heavy's
/// slow and strong.
///
/// Read these as a list of "what makes this move different from the default one". Anything
/// not changed here is the baseline number.
/// </summary>
public static class CharacterNormals
{
	static MoveData M(MoveData[] moves, MoveSlot slot) => moves[(int)slot];

	// =========================================================================
	// SWIFT - light, fire. A quick martial artist: kicks, spins and a flaming palm.
	// =========================================================================

	public static MoveData SwiftJab() => DefaultMoveset.LightJab();

	public static void Swift(MoveData[] moves)
	{
		// A roundhouse: the longest reach he has on the ground, but a flatter, weaker launch.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Roundhouse"; m.Anim = AttackAnim.FrontKick;
		m.HitboxOffset = new Vector2(98.0f, -26.0f); m.HitboxRadius = 46.0f;
		m.LaunchAngleDegrees = 32.0f;

		// A flip kick that also covers a little behind him.
		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Flip Kick"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(-6.0f, -96.0f); m.HitboxRadius = 58.0f;
		m.StartupFrames = 5; m.LaunchAngleDegrees = 95.0f;

		// A sweep that pops them up rather than away - it starts air combos.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Sweep"; m.Anim = AttackAnim.LowKick;
		m.LaunchAngleDegrees = 78.0f; m.Damage = 6.0f;
		m.BaseKnockback = 30.0f; m.KnockbackGrowth = 0.7f;

		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Flying Knee"; m.Anim = AttackAnim.FlyingKick;
		m.StartupFrames = 7; m.LaunchAngleDegrees = 46.0f;

		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Blaze Palm"; m.Anim = AttackAnim.PalmThrust;
		m.HitboxOffset = new Vector2(86.0f, -26.0f); m.HitboxRadius = 58.0f;
		m.LaunchAngleDegrees = 36.0f;

		// Stays out longer than anyone's: hard to time, easy to hit with.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Rising Flame"; m.Anim = AttackAnim.UpSmash;
		m.ActiveFrames = 8;

		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Breaker Split"; m.Anim = AttackAnim.Split;

		// A long spin: weak, but out for ten frames, so it is his get-off-me move.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Flame Spin"; m.Anim = AttackAnim.Nair;
		m.ActiveFrames = 10; m.Damage = 7.0f;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Axe Kick"; m.Anim = AttackAnim.Fair;
		m.LaunchAngleDegrees = 30.0f;

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Back Kick"; m.Anim = AttackAnim.Bair;

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Bicycle Kick"; m.Anim = AttackAnim.Uair;

		// A drill kick that sends them down and away - not a spike, because a light fighter
		// with a spike and a fast air game would be the whole edge-guarding game.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Drill Kick"; m.Anim = AttackAnim.Dair;
		m.Spikes = false; m.LaunchAngleDegrees = -35.0f;
		m.StartupFrames = 8;
	}

	// =========================================================================
	// LUG - heavy, construction site. Every hit is his sledgehammer or his own bulk, and his
	// specials are site tools (see Specials.Construction).
	// =========================================================================

	public static MoveData LugJab()
	{
		MoveData jab = DefaultMoveset.HeavyJab();
		jab.MoveName = "Hammer Tap";
		jab.ComboNext.MoveName = "Hammer Bash";
		return jab;
	}

	public static void Lug(MoveData[] moves)
	{
		// The sledgehammer held straight out: the longest poke in the game.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Sledge Poke"; m.Anim = AttackAnim.HeavyPunch;
		m.HitboxOffset = new Vector2(108.0f, -24.0f); m.HitboxRadius = 50.0f;
		m.LaunchAngleDegrees = 30.0f;

		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Sledge Arc"; m.Anim = AttackAnim.Uppercut;
		m.HitboxOffset = new Vector2(40.0f, -100.0f); m.HitboxRadius = 64.0f;
		m.LaunchAngleDegrees = 80.0f;

		// From a crouch, a low sweep of the sledgehammer along the floor: slow, long, and it
		// knocks them off their feet low along the ground.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Sledge Sweep"; m.Anim = AttackAnim.LowSweep;
		m.HitboxOffset = new Vector2(96.0f, 34.0f); m.HitboxRadius = 54.0f;
		m.StartupFrames = 8; m.LaunchAngleDegrees = 22.0f; m.Damage = 9.0f;

		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Hard Hat Barge"; m.Anim = AttackAnim.Lunge;
		m.HitboxRadius = 58.0f;

		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Sledge Slam"; m.Anim = AttackAnim.OverheadSlam;
		m.HitboxOffset = new Vector2(100.0f, -4.0f); m.HitboxRadius = 64.0f;

		// A huge, slow swing from low behind him up and over his head. Everyone can see it coming;
		// anyone above him who does not move takes the whole thing.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Overhead Swing"; m.Anim = AttackAnim.OverheadArc;
		m.StartupFrames = 20; m.ActiveFrames = 8;
		m.Damage = 16.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 1.0f;
		m.HitboxOffset = new Vector2(10.0f, -118.0f); m.HitboxRadius = 84.0f;

		// Sledgehammer Quake: slams the ground in front, and a shockwave runs out both ways along
		// the floor. Charging it powers up the waves too. They pop people up rather than away,
		// and stop where the ground does.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Sledgehammer Quake"; m.Anim = AttackAnim.OverheadSlam;
		// A long, obvious swing up over his head first - the whole point of a move this big is
		// that everyone can see it coming and has time to jump.
		m.StartupFrames = 17;
		// Telegraphed that much, it is allowed to hit like it: more damage and knockback than a
		// normal down smash, and the quake carries most of it. The price is reach - the quake
		// only runs a short way each side, so it punishes people standing close, not the stage.
		m.Damage = 16.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 1.0f;
		m.HitboxOffset = new Vector2(72.0f, 34.0f); m.HitboxRadius = 58.0f;
		m.LaunchAngleDegrees = 74.0f;
		m.Special = SpecialKind.Shockwave;
		m.ShockwavePower = 0.85f;
		m.SpecialSpeed = 950.0f; m.SpecialLifetime = 14;
		m.FxRadius = 34.0f; m.FxColor = new Color(0.80f, 0.66f, 0.46f);

		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Toolbelt Spin"; m.Anim = AttackAnim.Nair;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Sledge Chop"; m.Anim = AttackAnim.Fair;
		m.HitboxOffset = new Vector2(92.0f, -10.0f);

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Back Swing"; m.Anim = AttackAnim.Bair;

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Sledge Swipe"; m.Anim = AttackAnim.Uppercut;
		m.HitboxOffset = new Vector2(30.0f, -96.0f);

		// The heavy keeps the spike: slow to come out, deadly off-stage.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Pile Driver"; m.Anim = AttackAnim.Dair;
	}

	// =========================================================================
	// EDGELORD - light, swords. Every normal is the longsword in his hand; the down smash is
	// the one that spins.
	// =========================================================================

	/// <summary>Cut, cut, thrust.</summary>
	public static MoveData EdgeLordJab()
	{
		MoveData jab = DefaultMoveset.LightJab();
		jab.MoveName = "Cut";
		jab.HitboxOffset = new Vector2(64.0f, -18.0f);
		jab.ComboNext.MoveName = "Back Cut";
		jab.ComboNext.HitboxOffset = new Vector2(66.0f, -18.0f);
		jab.ComboNext.ComboNext.MoveName = "Thrust";
		jab.ComboNext.ComboNext.Anim = AttackAnim.PalmThrust;
		jab.ComboNext.ComboNext.HitboxOffset = new Vector2(84.0f, -16.0f);
		return jab;
	}

	public static void EdgeLord(MoveData[] moves)
	{
		// A sword has reach: his tilts poke further than a fist would, and hit a little flatter.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Side Cut"; m.Anim = AttackAnim.HeavyPunch;
		m.HitboxOffset = new Vector2(104.0f, -20.0f); m.HitboxRadius = 46.0f;
		m.LaunchAngleDegrees = 34.0f;

		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Rising Cut"; m.Anim = AttackAnim.Uppercut;
		m.HitboxOffset = new Vector2(34.0f, -104.0f); m.HitboxRadius = 56.0f;
		m.LaunchAngleDegrees = 84.0f;

		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Ankle Cut"; m.Anim = AttackAnim.LowSweep;
		m.HitboxOffset = new Vector2(98.0f, 34.0f); m.HitboxRadius = 46.0f;
		m.LaunchAngleDegrees = 24.0f;

		// Super speed: a dash attack that comes out fast and carries.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Flash Step"; m.Anim = AttackAnim.Lunge;
		m.StartupFrames = 6; m.HitboxOffset = new Vector2(80.0f, -14.0f);

		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Cleave"; m.Anim = AttackAnim.OverheadSlam;
		m.HitboxOffset = new Vector2(104.0f, -8.0f); m.HitboxRadius = 58.0f;

		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Sky Pierce"; m.Anim = AttackAnim.UpSmash;
		m.HitboxOffset = new Vector2(16.0f, -116.0f); m.HitboxRadius = 52.0f;

		// Blade Blender: he spins on the spot with a sword held out either side. A slow wind-up
		// spin says it is coming; then the blades blur round, catching anyone near for a string of
		// small hits that hold them there, and the last turn throws them out. It covers both sides
		// at once, like every down smash, and reaches as far as the blades do.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Blade Blender"; m.Anim = AttackAnim.Spin;
		m.Spin = true;
		m.StartupFrames = 14; m.ActiveFrames = 20; m.EndlagFrames = 24;
		m.Damage = 9.0f; m.BaseKnockback = 34.0f; m.KnockbackGrowth = 1.0f;
		m.LaunchAngleDegrees = 40.0f;
		m.HitboxOffset = new Vector2(0.0f, -6.0f); m.HitboxRadius = 104.0f;
		m.RehitFrames = 4;
		m.LinkHit = new MoveData
		{
			MoveName = "Blade Blender (link)",
			Damage = 1.5f, BaseKnockback = 20.0f, KnockbackGrowth = 0.0f,
			LaunchAngleDegrees = 90.0f,
		};
		m.HeldArt = "sword_longsword"; m.HeldArtOffset = new Vector2(46.0f, -18.0f); m.HeldArtSize = 150.0f;

		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Whirl"; m.Anim = AttackAnim.Nair;
		m.HitboxRadius = 60.0f;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Air Slash"; m.Anim = AttackAnim.Fair;
		m.HitboxOffset = new Vector2(96.0f, -10.0f);

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Reverse Cut"; m.Anim = AttackAnim.Bair;

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Overhead Arc"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(20.0f, -100.0f);

		// A downward stab that sends them down and away. Not a spike: his grapple throw is
		// already his way of sending someone straight down.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Plunge"; m.Anim = AttackAnim.Dair;
		m.Spikes = false; m.LaunchAngleDegrees = -40.0f;
	}

	// =========================================================================
	// CIRCY - medium, Elim's. A ball on long stretchy legs: kicks with huge reach, and he
	// tucks into his ball and rolls - the rolling that Elim described for when he is hit.
	// =========================================================================

	/// <summary>Kick, kick, then a bonk with the whole ball.</summary>
	public static MoveData CircyJab() => new MoveData
	{
		MoveName = "Kick",
		StartupFrames = 3, ActiveFrames = 2, EndlagFrames = 10,
		Damage = 3.0f, BaseKnockback = 20.0f, KnockbackGrowth = 0.25f,
		LaunchAngleDegrees = 20.0f,
		HitboxOffset = new Vector2(62.0f, 10.0f), HitboxRadius = 40.0f,
		Anim = AttackAnim.FrontKick,
		ComboNext = new MoveData
		{
			MoveName = "Low Kick",
			StartupFrames = 2, ActiveFrames = 2, EndlagFrames = 10,
			Damage = 3.0f, BaseKnockback = 22.0f, KnockbackGrowth = 0.25f,
			LaunchAngleDegrees = 20.0f,
			HitboxOffset = new Vector2(66.0f, 26.0f), HitboxRadius = 42.0f,
			Anim = AttackAnim.LowKick,
			ComboNext = new MoveData
			{
				MoveName = "Bonk",
				StartupFrames = 4, ActiveFrames = 3, EndlagFrames = 16,
				Damage = 5.0f, BaseKnockback = 34.0f, KnockbackGrowth = 0.9f,
				LaunchAngleDegrees = 50.0f,
				HitboxOffset = new Vector2(50.0f, -44.0f), HitboxRadius = 50.0f,
				Anim = AttackAnim.Lunge,
				CarriesMomentum = true,
			},
		},
	};

	public static void Circy(MoveData[] moves)
	{
		// His legs stretch, so his kicks outreach everyone's - the payoff for his short jump.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Long Leg"; m.Anim = AttackAnim.FrontKick;
		m.HitboxOffset = new Vector2(112.0f, 0.0f); m.HitboxRadius = 42.0f;

		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Leg Up"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(12.0f, -112.0f); m.HitboxRadius = 52.0f;

		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Leg Sweep"; m.Anim = AttackAnim.LowKick;
		m.HitboxOffset = new Vector2(104.0f, 34.0f);

		// Tucks into his ball and rolls into them.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Rolling Tackle"; m.BallForm = true; m.Anim = AttackAnim.Lunge;
		m.ActiveFrames = 10; m.HitboxOffset = new Vector2(30.0f, 10.0f); m.HitboxRadius = 52.0f;

		// A headbutt with the whole ball.
		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Ball Bash"; m.Anim = AttackAnim.Lunge;
		m.HitboxOffset = new Vector2(78.0f, -40.0f); m.HitboxRadius = 64.0f;

		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Spring Up"; m.Anim = AttackAnim.UpSmash;

		// The splits, on legs that long, reach further both ways than anyone's.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Splits"; m.Anim = AttackAnim.Split;
		m.HitboxRadius = 106.0f;

		// Curls into his ball and spins.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Ball Spin"; m.BallForm = true; m.Anim = AttackAnim.Nair;
		m.ActiveFrames = 9; m.HitboxOffset = new Vector2(0.0f, 0.0f); m.HitboxRadius = 60.0f;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Stretch Kick"; m.Anim = AttackAnim.FrontKick;
		m.HitboxOffset = new Vector2(104.0f, -6.0f);

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Donkey Kick"; m.Anim = AttackAnim.Bair;

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Flip"; m.Anim = AttackAnim.Uair;

		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Stomp"; m.Anim = AttackAnim.Dair;
	}
}
