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
	// SWIFT (FLAMBE) - light, fire. A fire-punk French chef: fast feet, flames on everything, and
	// a frying pan for his big hits. Every move has its own trick - combos that need a second
	// press, flurries of hits that end in a launch, a crepe flip, fire geysers - so no two of his
	// attacks feel the same.
	// =========================================================================

	/// <summary>Two flicker jabs, then "Flambe!" - a palm that bursts into flame and sets them alight.</summary>
	public static MoveData SwiftJab()
	{
		MoveData jab = DefaultMoveset.LightJab();
		jab.MoveName = "Flicker Jab";
		jab.EndlagFrames = 7;
		// On the fist, which the shared punch throws out and up to head height.
		jab.HitboxOffset = new Vector2(57.0f, -59.0f);
		jab.ComboNext.HitboxOffset = new Vector2(61.0f, -55.0f);
		jab.ComboNext.MoveName = "Flicker Jab 2";
		jab.ComboNext.EndlagFrames = 7;
		MoveData finish = jab.ComboNext.ComboNext;
		finish.MoveName = "Flambe!";
		finish.Anim = AttackAnim.PalmThrust;
		finish.StartupFrames = 4; finish.ActiveFrames = 3; finish.EndlagFrames = 14;
		finish.Damage = 4.0f; finish.BaseKnockback = 32.0f; finish.KnockbackGrowth = 0.8f;
		finish.LaunchAngleDegrees = 40.0f;
		finish.HitboxOffset = new Vector2(75.0f, -32.0f); finish.HitboxRadius = 46.0f;
		finish.ActiveFx = ActiveFx.Flame;
		finish.BurnFrames = 45; finish.BurnDamage = 2.0f;
		return jab;
	}

	public static void Swift(MoveData[] moves)
	{
		// Twin Roundhouse: a low roundhouse that only pushes - press again for a spinning heel
		// hook, on fire, that launches. The first kick alone is the safest poke he has.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Roundhouse"; m.Anim = AttackAnim.FrontKick;
		m.HitboxOffset = new Vector2(68.0f, 4.0f); m.HitboxRadius = 44.0f;
		m.Damage = 6.0f; m.BaseKnockback = 20.0f; m.KnockbackGrowth = 0.4f;
		m.LaunchAngleDegrees = 22.0f; m.EndlagFrames = 12;
		m.ComboNext = new MoveData
		{
			MoveName = "Heel Hook", Anim = AttackAnim.HeavyPunch,
			StartupFrames = 5, ActiveFrames = 3, EndlagFrames = 16,
			Damage = 7.0f, BaseKnockback = 30.0f, KnockbackGrowth = 0.95f, LaunchAngleDegrees = 42.0f,
			HitboxOffset = new Vector2(80.0f, -42.0f), HitboxRadius = 46.0f,
			ActiveFx = ActiveFx.Flame,
		};

		// Torch Flip: a flip kick that throws a fountain of sparks up off his heel - three quick
		// weak hits holding them in the air, then the last pops them up.
		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Torch Flip"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(-12.0f, -80.0f); m.HitboxRadius = 58.0f;
		m.StartupFrames = 5; m.ActiveFrames = 9; m.EndlagFrames = 12;
		m.RehitFrames = 3;
		m.Damage = 5.0f; m.LaunchAngleDegrees = 92.0f;
		m.LinkHit = new MoveData { MoveName = "Torch Flip (link)", Damage = 1.5f, BaseKnockback = 12.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 90.0f };
		m.ActiveFx = ActiveFx.Flame;

		// Crepe Flip: the pan slid flat along the floor under their feet and flipped - straight up,
		// head over heels, like a crepe. Low along the floor, so it is still a sweep, and it sets
		// up everything he has that hits above him.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Crêpe Flip"; m.Anim = AttackAnim.LowSweep; m.PropArt = "tool_pan";
		m.HitboxOffset = new Vector2(104.0f, 58.0f); m.HitboxRadius = 46.0f;
		m.StartupFrames = 7; m.EndlagFrames = 15;
		m.Damage = 6.0f; m.BaseKnockback = 50.0f; m.KnockbackGrowth = 0.45f;
		m.LaunchAngleDegrees = 88.0f;
		m.SpinVictim = true;

		// Swan Dive: he springs into a swan dive, flat out and head first, and corkscrews along
		// his length wrapped in fire - a string of little burns that carries them with him and a
		// last one that throws them off, still burning. Eric's call, 2026-10-04.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Swan Dive"; m.Anim = AttackAnim.SwanDive; m.Corkscrew = true;
		m.StartupFrames = 6; m.ActiveFrames = 15; m.EndlagFrames = 16;
		m.Special = SpecialKind.Dash; m.SpecialSpeed = 1150.0f;
		m.HitboxOffset = new Vector2(14.0f, -6.0f); m.HitboxRadius = 66.0f;
		m.ActiveFx = ActiveFx.Flame; m.Sound = "special_fire";
		m.RehitFrames = 3;
		m.LinkHit = new MoveData { MoveName = "Swan Dive (link)", Damage = 1.4f, BaseKnockback = 12.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 20.0f };
		m.Damage = 6.0f; m.BaseKnockback = 34.0f; m.KnockbackGrowth = 0.8f; m.LaunchAngleDegrees = 38.0f;
		m.BurnFrames = 45; m.BurnDamage = 2.0f;

		// Flambe Pan: the chef's big hit. He hauls a frying pan up over his head with the fire
		// roaring off it and brings it down in front of him. Slow, obvious, and it burns.
		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Flambe Pan"; m.Anim = AttackAnim.OverheadSlam; m.PropArt = "tool_pan";
		m.StartupFrames = 17;
		// On the pan's face where it lands, low in front of his feet (see the attack parade).
		m.HitboxOffset = new Vector2(121.0f, 40.0f); m.HitboxRadius = 60.0f;
		m.LaunchAngleDegrees = 38.0f;
		m.BurnFrames = 90; m.BurnDamage = 5.0f;
		m.ActiveFx = ActiveFx.Flame;

		// Fire Pillar: a column of fire roars up over him - four hits climbing it, the last one
		// throwing them out of the top.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Fire Pillar"; m.Anim = AttackAnim.UpSmash;
		m.ActiveFrames = 12; m.RehitFrames = 4;
		m.Damage = 14.0f; m.LaunchAngleDegrees = 90.0f;
		m.HitboxOffset = new Vector2(0.0f, -118.0f); m.HitboxRadius = 64.0f;
		m.LinkHit = new MoveData { MoveName = "Fire Pillar (link)", Damage = 2.0f, BaseKnockback = 14.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 90.0f };
		m.ActiveFx = ActiveFx.Flame;

		// Fire Geysers: he drops into the splits and two jets of fire burst up out of the floor,
		// one each side of him, then die down. Charging it makes them hit harder.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Fire Geysers"; m.Anim = AttackAnim.Split;
		m.HitboxOffset = new Vector2(110.0f, 30.0f); m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.Mirrored = true; m.FromGround = true; m.ShotAngles = new[] { 88.0f };
		m.SpecialSpeed = 1500.0f; m.SpecialGravity = 5200.0f; m.SpecialLifetime = 40;
		m.FxFlame = true; m.FxRadius = 38.0f; m.FxColor = new Color(0.98f, 0.52f, 0.20f);
		// Two projectiles at once reads as a volley to SfxCatalog, which is a ring of blades;
		// these are fire.
		m.Sound = "special_fire";
		// Up and away, not straight up: a down smash has to be able to finish off the side.
		m.LaunchAngleDegrees = 36.0f; m.BaseKnockback = 30.0f;
		m.BurnFrames = 45; m.BurnDamage = 2.0f;

		// Fire Wheel: he spins with fire streaming off him - a ring of weak hits all round that
		// carries them with him, and a last one that throws them off. Quick to come out of.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Fire Wheel"; m.Anim = AttackAnim.Nair;
		m.ActiveFrames = 12; m.RehitFrames = 4; m.EndlagFrames = 8;
		m.Damage = 4.0f;
		m.LinkHit = new MoveData { MoveName = "Fire Wheel (link)", Damage = 1.5f, BaseKnockback = 10.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 60.0f };
		m.ActiveFx = ActiveFx.Flame;

		// Double Axe: an axe kick that pops them up - press again for a second, harder one that
		// knocks them away.
		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Axe Kick"; m.Anim = AttackAnim.Fair;
		m.HitboxOffset = new Vector2(68.0f, 12.0f);
		m.Damage = 5.0f; m.BaseKnockback = 24.0f; m.KnockbackGrowth = 0.3f; m.LaunchAngleDegrees = 70.0f;
		m.EndlagFrames = 12;
		m.ComboNext = new MoveData
		{
			MoveName = "Second Axe", Anim = AttackAnim.Fair,
			StartupFrames = 6, ActiveFrames = 3, EndlagFrames = 16,
			Damage = 7.0f, BaseKnockback = 30.0f, KnockbackGrowth = 1.0f, LaunchAngleDegrees = 32.0f,
			HitboxOffset = new Vector2(68.0f, 12.0f), HitboxRadius = 48.0f,
			ActiveFx = ActiveFx.Flame,
		};

		// Backdraft: a back kick that blows a burst of fire out behind him. His surest finisher.
		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Backdraft"; m.Anim = AttackAnim.Bair;
		m.HitboxOffset = new Vector2(-72.0f, 0.0f);
		m.ActiveFx = ActiveFx.Flame;
		m.BurnFrames = 45; m.BurnDamage = 2.0f;

		// Bicycle Kick: legs pedalling overhead - three quick hits and a last that sends them up.
		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Bicycle Kick"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(-12.0f, -80.0f);
		m.ActiveFrames = 9; m.RehitFrames = 3;
		m.Damage = 5.0f;
		m.LinkHit = new MoveData { MoveName = "Bicycle Kick (link)", Damage = 1.5f, BaseKnockback = 12.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 90.0f };

		// Fire Drill: a burning drill kick straight down - four grinding hits, the last sending
		// them down and away. Not a spike: a light fighter with a fast air game and a spike would
		// be the whole edge-guarding game.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Fire Drill"; m.Anim = AttackAnim.Dair;
		m.HitboxOffset = new Vector2(-2.0f, 70.0f);
		m.Spikes = false; m.LaunchAngleDegrees = -35.0f;
		m.StartupFrames = 8; m.ActiveFrames = 9; m.RehitFrames = 3; m.EndlagFrames = 12;
		m.Damage = 5.0f;
		m.LinkHit = new MoveData { MoveName = "Fire Drill (link)", Damage = 1.2f, BaseKnockback = 10.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = -70.0f };
		m.ActiveFx = ActiveFx.Flame;
	}

	// =========================================================================
	// LUG - heavy, construction site. He swaps a different heavy tool into his hand for most
	// attacks - a pipe wrench, a shovel, a pickaxe, a crowbar, a steel beam, a stop sign, a
	// jackhammer - and every one is about as long as his sledgehammer, which is what his reach is
	// built on. His specials are site tools too (see Specials.Construction). He is built huge up
	// top and short in the leg, so he fights with his arms and his tools, not his feet (Eric's
	// call, 2026-10-08). Each hitbox sits on the head of its tool as his redrawn arms hold it.
	// =========================================================================

	/// <summary>
	/// Hard hat armour, on his three smashes and his barge: through the windup and the swing, a
	/// hit of this much damage or less bounces off - it still counts, but he does not flinch. A
	/// jab cannot stop a sledgehammer; a real hit, or a grab, still can.
	/// </summary>
	const float HardHatArmor = 8.0f;

	/// <summary>One of Lug's tool drawings, or null so the move falls back to a crayon shape.</summary>
	static Texture2D LugFx(string name)
	{
		string path = $"res://fighters/lug/poses/{name}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	public static MoveData LugJab()
	{
		MoveData jab = DefaultMoveset.HeavyJab();
		jab.MoveName = "Wrench Tap";
		jab.PropArt = "tool_pipewrench";
		jab.HitboxOffset = new Vector2(150.0f, -113.0f);
		jab.ComboNext.MoveName = "Wrench Bash";
		jab.ComboNext.PropArt = "tool_pipewrench";
		jab.ComboNext.HitboxOffset = new Vector2(148.0f, -108.0f);
		jab.ComboNext.Anim = AttackAnim.Lunge;
		return jab;
	}

	public static void Lug(MoveData[] moves)
	{
		// A shovel jabbed straight out, blade first: the longest poke in the game.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Shovel Jab"; m.Anim = AttackAnim.HeavyPunch; m.PropArt = "tool_shovel";
		m.HitboxOffset = new Vector2(134.0f, -100.0f); m.HitboxRadius = 50.0f;
		m.LaunchAngleDegrees = 30.0f;

		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Pickaxe Arc"; m.Anim = AttackAnim.Uppercut; m.PropArt = "tool_pickaxe";
		m.HitboxOffset = new Vector2(-40.0f, -170.0f); m.HitboxRadius = 62.0f;
		m.LaunchAngleDegrees = 80.0f;

		// Cone Shove: he bends down, plants a traffic cone and shoves it along the floor with both
		// hands. It skids to a stop and stays put, and the next one to run into it trips over it
		// and goes head over heels. Low along the ground, so it is still a sweep - one that leaves
		// something behind. It was a kick, his one attack with a foot, until he was redrawn with
		// little legs under a huge top half (Eric, 2026-10-08).
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Cone Shove"; m.Anim = AttackAnim.SetDown; m.PropArt = "-";
		m.HitboxOffset = new Vector2(80.0f, 52.0f); m.HitboxRadius = 44.0f;
		m.StartupFrames = 7; m.EndlagFrames = 10; m.LaunchAngleDegrees = 80.0f;
		m.Damage = 4.0f; m.BaseKnockback = 40.0f; m.KnockbackGrowth = 0.4f;
		m.SpinVictim = true;
		m.Special = SpecialKind.Trap; m.MaxOut = 1; m.SpentOnHit = true;
		m.SpecialSpeed = 820.0f; m.SlideFriction = 1600.0f; m.SpecialGravity = 0.0f; m.SpecialLifetime = 300;
		m.FxTexture = LugFx("cone"); m.FxArtSize = 70.0f; m.FxRadius = 28.0f;
		m.FxColor = new Color(0.93f, 0.49f, 0.22f);

		// Head down, hard hat on, sledgehammer on his shoulder, straight through them.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Hard Hat Barge"; m.Anim = AttackAnim.Lunge; m.CarryOnShoulder = true; m.ShowExtra = "hardhat";
		m.HitboxOffset = new Vector2(62.0f, -30.0f); m.HitboxRadius = 58.0f;
		m.Armor = HardHatArmor;

		// The sledgehammer itself, brought all the way over and down.
		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Sledge Slam"; m.Anim = AttackAnim.OverheadSlam; m.PropArt = "tool_sledgehammer";
		m.HitboxOffset = new Vector2(141.0f, 38.0f); m.HitboxRadius = 66.0f;
		m.Armor = HardHatArmor;

		// A steel I-beam heaved from low behind him up and over his head. Huge and slow;
		// everyone can see it coming, and anyone above him who does not move takes all of it.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Beam Heave"; m.Anim = AttackAnim.OverheadArc; m.PropArt = "tool_beam";
		m.StartupFrames = 20; m.ActiveFrames = 8;
		m.Damage = 16.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 1.0f;
		m.HitboxOffset = new Vector2(2.0f, -176.0f); m.HitboxRadius = 86.0f;
		m.Armor = HardHatArmor;

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
		m.HitboxOffset = new Vector2(141.0f, 38.0f); m.HitboxRadius = 58.0f;
		m.LaunchAngleDegrees = 74.0f;
		m.Special = SpecialKind.Shockwave;
		m.ShockwavePower = 0.85f;
		m.SpecialSpeed = 950.0f; m.SpecialLifetime = 14;
		m.FxRadius = 34.0f; m.FxColor = new Color(0.98f, 0.70f, 0.22f);
		m.Armor = HardHatArmor;

		// A stop sign held out at arm's length and swung all the way round him.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Stop Sign Spin"; m.Anim = AttackAnim.Spin; m.PropArt = "tool_sign";
		m.HitboxOffset = new Vector2(98.0f, -100.0f); m.HitboxRadius = 84.0f;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Sledge Chop"; m.Anim = AttackAnim.AirChop; m.PropArt = "tool_sledgehammer";
		m.HitboxOffset = new Vector2(108.0f, 37.0f); m.HitboxRadius = 56.0f;

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Shovel Back Swing"; m.Anim = AttackAnim.BackSlash; m.PropArt = "tool_shovel";
		m.HitboxOffset = new Vector2(-156.0f, -100.0f);

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Pick Swipe"; m.Anim = AttackAnim.OverheadArc; m.PropArt = "tool_pickaxe";
		m.HitboxOffset = new Vector2(2.0f, -184.0f);

		// He rides a jackhammer down - both hands on it, feet on it - and it hammers whatever is
		// under it: a run of quick hits that holds them there, and a last one that drives them
		// down. The heavy keeps the spike: slow to come out, deadly off-stage. Eric's call,
		// 2026-10-04: two hands, feet on it, and many hits.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Jackhammer"; m.Anim = AttackAnim.Jackhammer; m.PropArt = "tool_jackhammer";
		m.HitboxOffset = new Vector2(22.0f, 62.0f);
		m.ActiveFrames = 15; m.RehitFrames = 3; m.Damage = 9.0f;
		m.LinkHit = new MoveData { MoveName = "Jackhammer (link)", Damage = 1.6f, BaseKnockback = 10.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = -80.0f };
		m.ActiveFx = ActiveFx.Sparks;
	}

	// =========================================================================
	// DOOMBOT - heavy, a factory robot gone rogue. Steel boots, claw hands on long arms,
	// antennae that spark. Every swing winds up big - that is on his sheet as a weakness - so
	// most startups here are a few frames past the baseline before heavy scaling adds more.
	// The reach is the payoff: his arms are long and his piston punch goes further still.
	// =========================================================================

	/// <summary>
	/// "A single strong kick with his foot, some sparks come out, no combo." Authored at final
	/// numbers like every jab: one slow steel-toe kick that hits harder than anyone's jab.
	/// </summary>
	public static MoveData DoomBotJab() => new MoveData
	{
		MoveName = "Steel Toe",
		Anim = AttackAnim.FrontKick,
		StartupFrames = 10, ActiveFrames = 3, EndlagFrames = 24,
		Damage = 10.0f, BaseKnockback = 32.0f, KnockbackGrowth = 0.7f,
		LaunchAngleDegrees = 35.0f,
		HitboxOffset = new Vector2(84.0f, 10.0f), HitboxRadius = 46.0f,
		ActiveFx = ActiveFx.Sparks,
	};

	public static void DoomBot(MoveData[] moves)
	{
		// A piston punch: the arm shoots out to twice its length and snaps back. His longest
		// reach on the ground.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Piston Punch"; m.Anim = AttackAnim.HeavyPunch; m.PropArt = "-";
		m.StretchArm = true;
		// Low enough, once he is grown to his full size, to land on someone half his height - level
		// with his own chest it would go over every head.
		m.HitboxOffset = new Vector2(132.0f, 16.0f); m.HitboxRadius = 42.0f;
		m.StartupFrames = 10; m.LaunchAngleDegrees = 32.0f;

		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Claw Snap"; m.Anim = AttackAnim.Uppercut; m.PropArt = "-";
		m.HitboxOffset = new Vector2(16.0f, -118.0f); m.HitboxRadius = 54.0f;
		m.StartupFrames = 8; m.LaunchAngleDegrees = 85.0f;

		// MiniBot: he crouches and sets down a little copy of himself, which marches off along the
		// floor. It stops at an edge and waits; the moment it touches anyone it goes off in a
		// small furnace burst - or by itself after three seconds. One out at a time. It replaces a
		// sweep, so his down tilt is a summon, not a hit: the bot is the hit.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "MiniBot"; m.Anim = AttackAnim.SetDown; m.PropArt = "-";
		m.StartupFrames = 12; m.ActiveFrames = 2; m.EndlagFrames = 18;
		m.Damage = 0.0f; m.BaseKnockback = 0.0f; m.KnockbackGrowth = 0.0f;
		m.HitboxOffset = new Vector2(44.0f, 36.0f); m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Walker; m.MaxOut = 1;
		m.SpecialSpeed = 280.0f; m.SpecialGravity = 3000.0f; m.SpecialLifetime = 180;
		// About a third of his height: unmistakably him, and small enough to walk under a jump.
		m.FxArtSize = 100.0f; m.FxRadius = 40.0f;
		m.Burst = new MoveData
		{
			MoveName = "MiniBot Burst",
			Damage = 7.0f, BaseKnockback = 38.0f, KnockbackGrowth = 0.55f,
			LaunchAngleDegrees = 60.0f, LaunchAway = true,
			HitboxRadius = 0.0f,
			Special = SpecialKind.Vent, SpecialLifetime = 14,
			BurnFrames = 30, BurnDamage = 1.5f,
			FxFlame = true, FxColor = new Color(0.97f, 0.50f, 0.20f), FxRadius = 100.0f,
		};

		// "Spin his arms windmill style": both arms whirl round at the shoulders as he charges,
		// clipping whoever is in front four times - three light hits that carry them along, then
		// one that knocks them away.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Windmill"; m.Anim = AttackAnim.Windmill; m.PropArt = "-";
		m.StartupFrames = 8; m.ActiveFrames = 18; m.EndlagFrames = 22;
		m.RehitFrames = 5;
		m.Damage = 6.0f; m.BaseKnockback = 38.0f; m.KnockbackGrowth = 0.85f; m.LaunchAngleDegrees = 45.0f;
		m.HitboxOffset = new Vector2(52.0f, -24.0f); m.HitboxRadius = 64.0f;
		m.LinkHit = new MoveData
		{
			MoveName = "Windmill (link)",
			Damage = 2.5f, BaseKnockback = 16.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 60.0f,
		};

		// Hydraulic press: both claws raised high, then slammed down together in front of him.
		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Hydraulic Press"; m.Anim = AttackAnim.OverheadSlam; m.PropArt = "-";
		m.StartupFrames = 18;
		m.Damage = 16.0f; m.LaunchAngleDegrees = 40.0f;
		m.HitboxOffset = new Vector2(112.0f, 20.0f); m.HitboxRadius = 60.0f;
		m.ActiveFx = ActiveFx.Sparks;

		// "An electric shock from his antennae": lightning arcs off both antenna tips into the
		// air above him.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Antenna Shock"; m.Anim = AttackAnim.UpSmash; m.PropArt = "-";
		m.StartupFrames = 14; m.ActiveFrames = 7;
		m.HitboxOffset = new Vector2(0.0f, -146.0f); m.HitboxRadius = 66.0f;
		m.ActiveFx = ActiveFx.Electric;

		// One steel boot raised high and stamped down - the other stays planted - and a quake runs
		// out both ways along the floor.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Stomp Quake"; m.Anim = AttackAnim.Stomp; m.PropArt = "-";
		m.StartupFrames = 16;
		m.Damage = 15.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 0.95f;
		m.HitboxOffset = new Vector2(0.0f, 52.0f); m.HitboxRadius = 70.0f;
		m.LaunchAngleDegrees = 70.0f;
		m.Special = SpecialKind.Shockwave;
		m.ShockwavePower = 0.75f;
		m.SpecialSpeed = 900.0f; m.SpecialLifetime = 14;
		m.FxRadius = 30.0f; m.FxColor = new Color(0.80f, 0.76f, 0.70f);

		// "Neutral air makes an electricity field around him": a crackling ring that holds anyone
		// inside it for three zaps, then throws them out.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Static Field"; m.Anim = AttackAnim.Spread; m.PropArt = "-";
		m.StartupFrames = 8; m.ActiveFrames = 12; m.EndlagFrames = 12;
		m.RehitFrames = 5;
		m.Damage = 6.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 0.9f; m.LaunchAngleDegrees = 45.0f;
		m.HitboxOffset = new Vector2(0.0f, -10.0f); m.HitboxRadius = 80.0f;
		m.LinkHit = new MoveData
		{
			MoveName = "Static Field (link)",
			Damage = 2.0f, BaseKnockback = 10.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 60.0f,
		};
		m.ActiveFx = ActiveFx.Electric;

		// "Forward air shoots a low power missile": a missile out of the socket in his chest,
		// straight ahead, with his arms held back and his chest pushed out to fire it. Sized to
		// him - he is drawn 1.75 times everyone's size - and chip damage at range, nothing more.
		// Eric's call, 2026-10-04.
		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Pocket Missile"; m.Anim = AttackAnim.ChestOut; m.PropArt = "-";
		m.BeamFrom = "socket";
		// Chip damage, so it is quick: the one thing he can throw out without committing.
		m.StartupFrames = 7; m.EndlagFrames = 12;
		m.Damage = 5.0f; m.BaseKnockback = 22.0f; m.KnockbackGrowth = 0.5f; m.LaunchAngleDegrees = 30.0f;
		m.HitboxOffset = new Vector2(60.0f, -14.0f); m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.SpecialSpeed = 950.0f; m.SpecialGravity = 0.0f; m.SpecialLifetime = 55;
		m.FxMissile = true; m.FxRadius = 21.0f; m.FxColor = new Color(0.62f, 0.64f, 0.70f);

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Boot Kick"; m.Anim = AttackAnim.Bair;
		m.HitboxOffset = new Vector2(-74.0f, -2.0f); m.HitboxRadius = 50.0f;
		m.StartupFrames = 10;

		// Claws clapped together over his head.
		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Claw Clap"; m.Anim = AttackAnim.UpSmash; m.PropArt = "-";
		m.HitboxOffset = new Vector2(6.0f, -112.0f); m.HitboxRadius = 56.0f;
		m.StartupFrames = 8;

		// Dropped boots first. Slow to start, and a spike.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Steel Boots"; m.Anim = AttackAnim.Dair;
		m.HitboxOffset = new Vector2(6.0f, 96.0f); m.HitboxRadius = 48.0f;
		m.StartupFrames = 14;
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
		jab.Anim = AttackAnim.Slash;
		// On the blade, which a cut holds out level at chest height (see the attack parade).
		jab.HitboxOffset = new Vector2(138.0f, -4.0f);
		jab.PropArt = "sword_shortsword";
		jab.ComboNext.MoveName = "Back Cut";
		jab.ComboNext.Anim = AttackAnim.RisingCut;
		jab.ComboNext.HitboxOffset = new Vector2(137.0f, 10.0f);
		jab.ComboNext.PropArt = "sword_shortsword";
		jab.ComboNext.ComboNext.MoveName = "Thrust";
		jab.ComboNext.ComboNext.Anim = AttackAnim.Thrust;
		jab.ComboNext.ComboNext.HitboxOffset = new Vector2(141.0f, 17.0f);
		jab.ComboNext.ComboNext.PropArt = "sword_shortsword";
		return jab;
	}

	public static void EdgeLord(MoveData[] moves)
	{
		// A katana cut: long, quick and flat.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Katana Cut"; m.Anim = AttackAnim.Slash; m.PropArt = "sword_katana";
		m.HitboxOffset = new Vector2(184.0f, -10.0f); m.HitboxRadius = 48.0f;
		m.LaunchAngleDegrees = 34.0f;

		// He points up and forward, and a shortsword summoned from behind him flies the way he
		// points - he is directing it. Anti-air at an angle, and his hand is empty.
		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Called Blade"; m.Anim = AttackAnim.PointUp; m.PropArt = "-";
		m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.HitboxOffset = new Vector2(-40.0f, -70.0f);
		m.ShotAngles = new[] { 60.0f };
		// About 300 pixels: it used to fly twice that, which made an up tilt a long-range shot.
		m.SpecialSpeed = 1500.0f; m.SpecialLifetime = 12;
		m.LaunchAngleDegrees = 75.0f;
		m.FxRadius = 26.0f; m.FxColor = new Color(0.80f, 0.82f, 0.86f);
		m.FxTexture = EdgeFx("sword_shortsword"); m.FxArtSize = 150.0f; m.FxAlongFlight = true;

		// From a crouch, a longsword jabbed straight out along the floor.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Ground Thrust"; m.Anim = AttackAnim.LowThrust; m.PropArt = "sword_longsword";
		m.HitboxOffset = new Vector2(171.0f, 58.0f); m.HitboxRadius = 42.0f;
		m.LaunchAngleDegrees = 20.0f;

		// Super speed: a rapier lunge that comes out fast and carries.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Flash Lunge"; m.Anim = AttackAnim.Thrust; m.PropArt = "sword_rapier";
		m.CarriesMomentum = true;
		m.StartupFrames = 6; m.HitboxOffset = new Vector2(185.0f, 19.0f);

		// He draws a claymore and brings it over his head and down in front of him. The biggest
		// sword he has, so it is the slowest to come round, and it reaches furthest.
		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Claymore"; m.Anim = AttackAnim.Chop; m.PropArt = "sword_claymore";
		m.StartupFrames = 18;
		m.HitboxOffset = new Vector2(197.0f, 24.0f); m.HitboxRadius = 64.0f;

		// A wide, slow arc: the greatsword drawn right back behind him, then swept up over his head
		// and down in front. The hitbox travels the whole arc, so it covers behind, above and in
		// front - but it comes slowly and stays out a long time, so it is easy to see coming and
		// easy to punish.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Great Arc"; m.Anim = AttackAnim.WideArc; m.PropArt = "sword_greatsword";
		m.StartupFrames = 18; m.ActiveFrames = 12; m.EndlagFrames = 32;
		m.HitboxOffset = new Vector2(0.0f, -172.0f); m.HitboxRadius = 56.0f;
		m.SweepDegrees = 150.0f;
		m.LaunchAngleDegrees = 80.0f;

		// He throws his arms down and two axes burst up out of the floor, one either side of him:
		// up fast, stopping dead the moment they stand on the floor, then toppling outward and
		// smashing down, gone where they land (MoveData.Topple). Covers both sides like every down
		// smash; the rise hits upward. Charging it makes the axes hit harder.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Axe Rise"; m.Anim = AttackAnim.SummonLow; m.PropArt = "-";
		m.StartupFrames = 14; m.ActiveFrames = 2; m.EndlagFrames = 28;
		m.Damage = 14.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 1.0f;
		m.LaunchAngleDegrees = 80.0f;
		m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.HitboxOffset = new Vector2(96.0f, 0.0f);
		m.Mirrored = true; m.FromGround = true;
		m.Topple = true;
		m.SpecialSpeed = 2600.0f; m.SpecialGravity = 0.0f; m.SpecialLifetime = 70;
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
		// Close range and a real recovery: it is for when someone is on top of him, not a wall
		// of blades to throw out over and over. It used to fly ~350px and come out three times a
		// second; now the daggers stop at about a body length and it is about twice a second.
		m.SpecialSpeed = 1000.0f; m.SpecialLifetime = 11;
		// Still slower than his pokes, so it hits harder than them: a weak move is a quick one.
		m.StartupFrames = 8; m.EndlagFrames = 20;
		m.Damage = 11.0f; m.LaunchAngleDegrees = 50.0f;
		m.FxRadius = 18.0f; m.FxColor = new Color(0.80f, 0.82f, 0.86f);
		m.FxTexture = EdgeFx("sword_dagger"); m.FxArtSize = 80.0f; m.FxAlongFlight = true;

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Scimitar Slash"; m.Anim = AttackAnim.AirSlash; m.PropArt = "sword_scimitar";
		m.HitboxOffset = new Vector2(167.0f, -16.0f);

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Reverse Katana"; m.Anim = AttackAnim.BackCut; m.PropArt = "sword_katana";
		m.HitboxOffset = new Vector2(-119.0f, -26.0f);

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Saw Arc"; m.Anim = AttackAnim.OverheadArc; m.PropArt = "sword_edgeblade";
		m.HitboxOffset = new Vector2(45.0f, -160.0f);

		// A broadsword swung down to hang straight under him: anyone below is driven straight
		// down. His spike - slow to come out, deadly off the edge.
		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Broadsword Drop"; m.Anim = AttackAnim.DownSwing; m.PropArt = "sword_broadsword";
		m.StartupFrames = 12;
		m.Spikes = true; m.LaunchAngleDegrees = -90.0f;
		m.HitboxOffset = new Vector2(-3.0f, 88.0f); m.HitboxRadius = 50.0f;
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
		HitboxOffset = new Vector2(96.0f, -4.0f), HitboxRadius = 40.0f,
		Anim = AttackAnim.FrontKick,
		ComboNext = new MoveData
		{
			MoveName = "Low Kick",
			StartupFrames = 2, ActiveFrames = 2, EndlagFrames = 10,
			Damage = 3.0f, BaseKnockback = 22.0f, KnockbackGrowth = 0.25f,
			LaunchAngleDegrees = 20.0f,
			HitboxOffset = new Vector2(92.0f, 46.0f), HitboxRadius = 42.0f,
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
		// One leg shoots out long to the hit and snaps back: stretching is his thing, and his kicks
		// outreach everyone's - the payoff for his short jump.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Long Leg"; m.Anim = AttackAnim.FrontKick;
		m.HitboxOffset = new Vector2(140.0f, 2.0f); m.HitboxRadius = 42.0f;
		m.StretchLeg = true;

		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Leg Up"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(-4.0f, -106.0f); m.HitboxRadius = 52.0f;

		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Leg Sweep"; m.Anim = AttackAnim.LowKick;
		m.HitboxOffset = new Vector2(94.0f, 48.0f);

		// Tucks into his ball and rolls into them.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Rolling Tackle"; m.BallForm = true; m.Anim = AttackAnim.Lunge;
		m.ActiveFrames = 10; m.HitboxOffset = new Vector2(30.0f, 10.0f); m.HitboxRadius = 52.0f;
		m.EndlagFrames = 17;

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
		m.HitboxOffset = new Vector2(132.0f, -6.0f);
		m.StretchLeg = true;

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Donkey Kick"; m.Anim = AttackAnim.Bair;
		m.HitboxOffset = new Vector2(-104.0f, -20.0f);

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Flip"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(-6.0f, -110.0f);

		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Stomp"; m.Anim = AttackAnim.Dair;
	}

	// =========================================================================
	// TRIGUY - light, Elim's. A white triangle on stick legs, in a top hat, and the fastest thing
	// in the game. His normals are his stick limbs, his hat, the point of his triangle, and the
	// same things his specials are made of - tears, Elim's spikes, the crying - so the whole kit
	// is one character (Eric's call, 2026-10-04: make more of them interesting). The ones he runs
	// into slide, because he cannot stop.
	// =========================================================================

	/// <summary>Poke, poke, then the point of the triangle.</summary>
	public static MoveData TriguyJab()
	{
		MoveData jab = DefaultMoveset.LightJab();
		jab.MoveName = "Poke";
		// Both pokes are the front hand, which comes out of the point of the triangle; the
		// back hand starts behind him and never reaches past his own front.
		jab.HitboxOffset = new Vector2(86.0f, -17.0f);
		jab.ComboNext.MoveName = "Poke 2";
		jab.ComboNext.Anim = AttackAnim.Punch;
		jab.ComboNext.HitboxOffset = new Vector2(86.0f, -17.0f);
		MoveData finish = jab.ComboNext.ComboNext;
		finish.MoveName = "Tip";
		finish.Anim = AttackAnim.PointThrust;
		finish.HitboxOffset = new Vector2(72.0f, 14.0f);
		return jab;
	}

	public static void Triguy(MoveData[] moves)
	{
		// Tear Flick: he flicks a tear at them - a little blue drop that arcs a short way and
		// splashes on whoever it meets. A quick poke from outside their reach.
		MoveData m = M(moves, MoveSlot.ForwardTilt);
		m.MoveName = "Tear Flick"; m.Anim = AttackAnim.PalmThrust;
		m.StartupFrames = 7; m.ActiveFrames = 2; m.EndlagFrames = 16;
		m.Damage = 6.0f; m.BaseKnockback = 30.0f; m.KnockbackGrowth = 0.7f; m.LaunchAngleDegrees = 40.0f;
		m.HitboxOffset = new Vector2(90.0f, 18.0f); m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.SpecialSpeed = 1250.0f; m.SpecialGravity = 2400.0f; m.SpecialLifetime = 22; m.LaunchLift = 260.0f;
		m.FxColor = new Color(0.10f, 0.40f, 1.0f); m.FxRadius = 18.0f;
		// A flick, not a gunshot.
		m.Sound = "swing_light";

		// The hat, tipped up and over at whoever is above.
		m = M(moves, MoveSlot.UpTilt);
		m.MoveName = "Hat Tip"; m.Anim = AttackAnim.Uppercut;
		m.HitboxOffset = new Vector2(-10.0f, -96.0f); m.HitboxRadius = 50.0f;

		// A low kick that keeps sliding - his feet will not stop.
		m = M(moves, MoveSlot.DownTilt);
		m.MoveName = "Slide Kick"; m.Anim = AttackAnim.LowKick;
		m.HitboxOffset = new Vector2(86.0f, 50.0f); m.CarriesMomentum = true;

		// Nose Charge: running, he sticks his sharp nose out and charges - a burst forward faster
		// than his run, point first, the hit out the whole way, and then the long skid of someone
		// who cannot stop. Eric's call, 2026-10-04.
		m = M(moves, MoveSlot.DashAttack);
		m.MoveName = "Nose Charge"; m.Anim = AttackAnim.PointThrust;
		m.StartupFrames = 4; m.ActiveFrames = 12; m.EndlagFrames = 18;
		m.Damage = 10.0f; m.LaunchAngleDegrees = 38.0f;
		m.HitboxOffset = new Vector2(82.0f, 8.0f);
		m.Special = SpecialKind.Dash; m.SpecialSpeed = 1450.0f;

		m = M(moves, MoveSlot.ForwardSmash);
		m.MoveName = "Big Point"; m.Anim = AttackAnim.PointThrust;
		m.HitboxOffset = new Vector2(82.0f, 8.0f); m.HitboxRadius = 50.0f;

		// Hat Trick: the top hat spins up off his head - two bonks that carry them up with it, and
		// a third that pops them off the top.
		m = M(moves, MoveSlot.UpSmash);
		m.MoveName = "Hat Trick"; m.Anim = AttackAnim.UpSmash;
		m.HitboxOffset = new Vector2(-12.0f, -110.0f);
		m.ActiveFrames = 12; m.RehitFrames = 4;
		m.LinkHit = new MoveData { MoveName = "Hat Trick (link)", Damage = 2.0f, BaseKnockback = 14.0f, KnockbackGrowth = 0.1f, LaunchAngleDegrees = 90.0f };

		// Spike Splits: he drops into the splits and two of the spikes from his puddle - Elim's
		// drawing - shoot up out of the floor, one each side. Up and away, so it can finish off
		// the side.
		m = M(moves, MoveSlot.DownSmash);
		m.MoveName = "Spike Splits"; m.Anim = AttackAnim.Split;
		m.HitboxOffset = new Vector2(110.0f, 30.0f); m.HitboxRadius = 0.0f;
		m.Special = SpecialKind.Projectile;
		m.Mirrored = true; m.FromGround = true; m.ShotAngles = new[] { 88.0f };
		m.SpecialSpeed = 1500.0f; m.SpecialGravity = 7000.0f; m.SpecialLifetime = 40;
		m.FxTexture = Specials.TriguyFx("spike"); m.FxArtSize = 110.0f;
		m.FxColor = new Color(0.31f, 0.31f, 0.31f); m.FxRadius = 30.0f;
		m.LaunchAngleDegrees = 40.0f; m.BaseKnockback = 30.0f;

		// Burst Into Tears: in mid-air he bursts out crying (Elim's crying drawing) and the tears
		// splash out all round him, knocking anyone close away on whichever side they are.
		m = M(moves, MoveSlot.NeutralAir);
		m.MoveName = "Burst Into Tears"; m.Anim = AttackAnim.Spread;
		m.PoseArts = new[] { "crying" };
		m.HitboxOffset = new Vector2(0.0f, -10.0f); m.HitboxRadius = 72.0f;
		m.LaunchAway = true; m.LaunchAngleDegrees = 40.0f;
		m.FxColor = new Color(0.10f, 0.40f, 1.0f);
		m.Sound = "splash";

		m = M(moves, MoveSlot.ForwardAir);
		m.MoveName = "Point Dive"; m.Anim = AttackAnim.Fair;
		m.HitboxOffset = new Vector2(66.0f, 36.0f);

		m = M(moves, MoveSlot.BackAir);
		m.MoveName = "Back Kick"; m.Anim = AttackAnim.Bair;

		m = M(moves, MoveSlot.UpAir);
		m.MoveName = "Hat Flip"; m.Anim = AttackAnim.Uair;
		m.HitboxOffset = new Vector2(38.0f, -74.0f);

		m = M(moves, MoveSlot.DownAir);
		m.MoveName = "Point Drop"; m.Anim = AttackAnim.Dair;
		m.HitboxOffset = new Vector2(2.0f, 66.0f);
	}
}
