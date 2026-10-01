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
	// LUG - heavy, construction site. He swaps a different heavy tool into his hand for most
	// attacks - a pipe wrench, a shovel, a pickaxe, a crowbar, a steel beam, a stop sign, a
	// jackhammer - and every one is about as long as his sledgehammer, which is what his reach is
	// built on. His specials are site tools too (see Specials.Construction).
	// =========================================================================

	public static MoveData LugJab()
	{
		MoveData jab = DefaultMoveset.HeavyJab();
		jab.MoveName = "Wrench Tap";
		jab.PropArt = "tool_pipewrench";
		jab.HitboxOffset = new Vector2(132.0f, -72.0f);
		jab.ComboNext.MoveName = "Wrench Bash";
		jab.ComboNext.PropArt = "tool_pipewrench";
		jab.ComboNext.HitboxOffset = new Vector2(132.0f, -80.0f);
		jab.ComboNext.Anim = AttackAnim.Lunge;
		return jab;
	}

	public static void Lug(MoveData[] moves)
	{
		// A shovel jabbed straight out, blade first: the longest poke in the game.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Shovel Jab"; m.Anim = AttackAnim.HeavyPunch; m.PropArt = "tool_shovel";
		m.HitboxOffset = new Vector2(136.0f, -72.0f); m.HitboxRadius = 50.0f;
		m.LaunchAngleDegrees = 30.0f;

		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Pickaxe Arc"; m.Anim = AttackAnim.Uppercut; m.PropArt = "tool_pickaxe";
		m.HitboxOffset = new Vector2(10.0f, -140.0f); m.HitboxRadius = 62.0f;
		m.LaunchAngleDegrees = 80.0f;

		// From a crouch, a crowbar swept low along the floor: slow, long, and it knocks them off
		// their feet.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Crowbar Sweep"; m.Anim = AttackAnim.LowSweep; m.PropArt = "tool_crowbar";
		m.HitboxOffset = new Vector2(122.0f, 50.0f); m.HitboxRadius = 52.0f;
		m.StartupFrames = 8; m.LaunchAngleDegrees = 22.0f; m.Damage = 9.0f;

		// Head down, hard hat on, straight through them.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Hard Hat Barge"; m.Anim = AttackAnim.Lunge; m.PropArt = "-"; m.ShowExtra = "hardhat";
		m.HitboxRadius = 58.0f;

		// The sledgehammer itself, brought all the way over and down.
		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Sledge Slam"; m.Anim = AttackAnim.OverheadSlam; m.PropArt = "tool_sledgehammer";
		m.HitboxOffset = new Vector2(128.0f, 34.0f); m.HitboxRadius = 66.0f;

		// A steel I-beam heaved from low behind him up and over his head. Huge and slow;
		// everyone can see it coming, and anyone above him who does not move takes all of it.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Beam Heave"; m.Anim = AttackAnim.OverheadArc; m.PropArt = "tool_beam";
		m.StartupFrames = 20; m.ActiveFrames = 8;
		m.Damage = 16.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 1.0f;
		m.HitboxOffset = new Vector2(-10.0f, -150.0f); m.HitboxRadius = 86.0f;

		// Sledgehammer Quake: slams the ground in front, and a shockwave runs out both ways along
		// the floor. Charging it powers up the waves too. They pop people up rather than away,
		// and stop where the ground does.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Sledgehammer Quake"; m.Anim = AttackAnim.OverheadSlam; m.PropArt = "tool_sledgehammer";
		// A long, obvious swing up over his head first - the whole point of a move this big is
		// that everyone can see it coming and has time to jump.
		m.StartupFrames = 17;
		// Telegraphed that much, it is allowed to hit like it: more damage and knockback than a
		// normal down smash, and the quake carries most of it. The price is reach - the quake
		// only runs a short way each side, so it punishes people standing close, not the stage.
		m.Damage = 16.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 1.0f;
		m.HitboxOffset = new Vector2(124.0f, 40.0f); m.HitboxRadius = 58.0f;
		m.LaunchAngleDegrees = 74.0f;
		m.Special = SpecialKind.Shockwave;
		m.ShockwavePower = 0.85f;
		m.SpecialSpeed = 950.0f; m.SpecialLifetime = 14;
		m.FxRadius = 34.0f; m.FxColor = new Color(0.98f, 0.70f, 0.22f);

		// A stop sign swung all the way round him.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Stop Sign Spin"; m.Anim = AttackAnim.Nair; m.PropArt = "tool_sign";
		m.HitboxOffset = new Vector2(20.0f, -60.0f); m.HitboxRadius = 84.0f;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Sledge Chop"; m.Anim = AttackAnim.Fair; m.PropArt = "tool_sledgehammer";
		m.HitboxOffset = new Vector2(70.0f, 50.0f); m.HitboxRadius = 56.0f;

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Shovel Back Swing"; m.Anim = AttackAnim.BackSlash; m.PropArt = "tool_shovel";
		m.HitboxOffset = new Vector2(-120.0f, -70.0f);

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Pick Swipe"; m.Anim = AttackAnim.OverheadArc; m.PropArt = "tool_pickaxe";
		m.HitboxOffset = new Vector2(-20.0f, -140.0f);

		// A jackhammer driven straight down under him. The heavy keeps the spike: slow to come
		// out, deadly off-stage.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Jackhammer"; m.Anim = AttackAnim.DownSwing; m.PropArt = "tool_jackhammer";
		m.HitboxOffset = new Vector2(24.0f, 90.0f);
	}

	// =========================================================================
	// EDGELORD - light, "infinite swords". Every normal draws a different weapon: a katana, a
	// claymore, a rapier... and some do not hold one at all, but summon them - daggers in a ring,
	// a shortsword flung from behind him, axes up out of the floor.
	// =========================================================================

	/// <summary>Cut, cut, thrust - with a shortsword.</summary>
	public static MoveData EdgeLordJab()
	{
		MoveData jab = DefaultMoveset.LightJab();
		jab.MoveName = "Cut";
		jab.HitboxOffset = new Vector2(70.0f, -18.0f);
		jab.PropArt = "sword_shortsword";
		jab.ComboNext.MoveName = "Back Cut";
		jab.ComboNext.HitboxOffset = new Vector2(72.0f, -18.0f);
		jab.ComboNext.PropArt = "sword_shortsword";
		jab.ComboNext.ComboNext.MoveName = "Thrust";
		jab.ComboNext.ComboNext.Anim = AttackAnim.PalmThrust;
		jab.ComboNext.ComboNext.HitboxOffset = new Vector2(92.0f, -16.0f);
		jab.ComboNext.ComboNext.PropArt = "sword_shortsword";
		return jab;
	}

	public static void EdgeLord(MoveData[] moves)
	{
		// A katana cut: long, quick and flat.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Katana Cut"; m.Anim = AttackAnim.HeavyPunch; m.PropArt = "sword_katana";
		m.HitboxOffset = new Vector2(112.0f, -20.0f); m.HitboxRadius = 48.0f;
		m.LaunchAngleDegrees = 34.0f;

		// He points up and forward, and a shortsword summoned from behind him flies the way he
		// points - he is directing it. Anti-air at an angle, and his hand is empty.
		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Called Blade"; m.Anim = AttackAnim.PointUp; m.PropArt = "-";
		m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.HitboxOffset = new Vector2(-40.0f, -70.0f);
		m.ShotAngles = new[] { 60.0f };
		m.SpecialSpeed = 1500.0f; m.SpecialLifetime = 26;
		m.LaunchAngleDegrees = 75.0f;
		m.FxRadius = 26.0f; m.FxColor = new Color(0.80f, 0.82f, 0.86f);
		m.FxTexture = EdgeFx("sword_shortsword"); m.FxArtSize = 150.0f; m.FxAlongFlight = true;

		// From a crouch, a longsword jabbed straight out along the floor.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Ground Thrust"; m.Anim = AttackAnim.LowThrust; m.PropArt = "sword_longsword";
		m.HitboxOffset = new Vector2(130.0f, 40.0f); m.HitboxRadius = 42.0f;
		m.LaunchAngleDegrees = 20.0f;

		// Super speed: a rapier lunge that comes out fast and carries.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Flash Lunge"; m.Anim = AttackAnim.PalmThrust; m.PropArt = "sword_rapier";
		m.CarriesMomentum = true;
		m.StartupFrames = 6; m.HitboxOffset = new Vector2(104.0f, -14.0f);

		// He draws a claymore and brings it over his head and down in front of him. The biggest
		// sword he has, so it is the slowest to come round, and it reaches furthest.
		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Claymore"; m.Anim = AttackAnim.OverheadSlam; m.PropArt = "sword_claymore";
		m.StartupFrames = 18;
		m.HitboxOffset = new Vector2(126.0f, -10.0f); m.HitboxRadius = 64.0f;

		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Sky Pierce"; m.Anim = AttackAnim.UpSmash; m.PropArt = "sword_greatsword";
		m.HitboxOffset = new Vector2(16.0f, -136.0f); m.HitboxRadius = 58.0f;

		// He throws his arms down and two axes burst up out of the floor, one either side of him,
		// then sink back. It covers both sides like every down smash, and hits upward. Charging
		// it makes the axes hit harder.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Axe Rise"; m.Anim = AttackAnim.SummonLow; m.PropArt = "-";
		m.StartupFrames = 14; m.ActiveFrames = 2; m.EndlagFrames = 28;
		m.Damage = 14.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 1.0f;
		m.LaunchAngleDegrees = 80.0f;
		m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.HitboxOffset = new Vector2(96.0f, 0.0f);
		m.Mirrored = true; m.FromGround = true;
		m.SpecialSpeed = 2100.0f; m.SpecialGravity = 6200.0f; m.SpecialLifetime = 46;
		m.FxRadius = 44.0f; m.FxColor = new Color(0.80f, 0.82f, 0.86f);
		m.FxTexture = EdgeFx("sword_axe"); m.FxArtSize = 190.0f;

		// He throws his arms and legs wide and eight daggers fly out from him in a ring. Covers
		// every direction at once, weakly - the get-off-me move.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Dagger Ring"; m.Anim = AttackAnim.Spread; m.PropArt = "-";
		m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.HitboxOffset = new Vector2(0.0f, -10.0f);
		m.ShotAngles = new[] { 0.0f, 45.0f, 90.0f, 135.0f, 180.0f, 225.0f, 270.0f, 315.0f };
		m.SpecialSpeed = 1300.0f; m.SpecialLifetime = 16;
		m.Damage = 8.0f; m.LaunchAngleDegrees = 50.0f;
		m.FxRadius = 18.0f; m.FxColor = new Color(0.80f, 0.82f, 0.86f);
		m.FxTexture = EdgeFx("sword_dagger"); m.FxArtSize = 80.0f; m.FxAlongFlight = true;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Scimitar Slash"; m.Anim = AttackAnim.Fair; m.PropArt = "sword_scimitar";
		m.HitboxOffset = new Vector2(104.0f, -10.0f);

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Reverse Katana"; m.Anim = AttackAnim.BackSlash; m.PropArt = "sword_katana";
		m.HitboxOffset = new Vector2(-96.0f, -14.0f);

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Saw Arc"; m.Anim = AttackAnim.OverheadArc; m.PropArt = "sword_edgeblade";
		m.HitboxOffset = new Vector2(20.0f, -112.0f);

		// A broadsword swung down to hang straight under him: anyone below is driven straight
		// down. His spike - slow to come out, deadly off the edge.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Broadsword Drop"; m.Anim = AttackAnim.DownSwing; m.PropArt = "sword_broadsword";
		m.StartupFrames = 12;
		m.Spikes = true; m.LaunchAngleDegrees = -90.0f;
		m.HitboxOffset = new Vector2(8.0f, 110.0f); m.HitboxRadius = 50.0f;
	}

	static Texture2D EdgeFx(string name)
	{
		string path = $"res://fighters/edgelord/poses/{name}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
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
