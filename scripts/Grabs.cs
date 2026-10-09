using Godot;

/// <summary>
/// Everyone's grab and four throws. Block plus attack on the ground reaches out and catches
/// whoever is right in front - blocking or not, which is the whole point: a grab is the answer to
/// someone who just holds block. While he holds them the stick picks the throw - forward, back,
/// up or down - and with no choice he throws forward. Eric's call, 2026-10-04, replacing the old
/// "no grabs" rule (see .ai/fighting-design.md).
///
/// Every fighter's throws are made of the same things as the rest of their kit, so a throw is
/// part of who they are: Flambe's are fire and his pan, Lug's are site tools, Circy's are his
/// stretching ball, EdgeLord's are swords - his up throw sends a sword up after them for a second
/// hit - DoomBot's are pistons, lightning and rocket boots - his down throw pins them to the floor
/// and burns them with his jets - and Triguy's are his point, his skid and his tears.
///
/// A throw is an ordinary MoveData with no hitbox of its own: it lands on whoever is held, and
/// cannot be blocked. A throw with <see cref="MoveData.RehitFrames"/> and a link hit keeps them
/// held through the link hits and lets go on the last; <see cref="MoveData.FollowShot"/> fires
/// something after them as they go. Not scaled by weight, like specials.
/// </summary>
public static class Grabs
{
	/// <summary>
	/// The grab itself: a quick reach just past the front of the body at about the middle of an
	/// ordinary fighter (see Fighter.GrabReach), and a long, punishable recovery if it catches
	/// nobody - a missed grab is how a grab is beaten.
	/// </summary>
	public static MoveData Grab() => new MoveData
	{
		MoveName = "Grab",
		Anim = AttackAnim.PalmThrust,
		PropArt = "-",
		StartupFrames = 6, ActiveFrames = 4, EndlagFrames = 26,
		Damage = 0.0f, BaseKnockback = 0.0f, KnockbackGrowth = 0.0f,
		HitboxRadius = 40.0f,
		Special = SpecialKind.Grab,
		Sound = "swing_light",
	};

	static MoveData Throw(string name, AttackAnim anim, float damage, float baseKb, float growth, float angle) => new MoveData
	{
		MoveName = name,
		Anim = anim,
		StartupFrames = 8, ActiveFrames = 2, EndlagFrames = 20,
		Damage = damage, BaseKnockback = baseKb, KnockbackGrowth = growth,
		LaunchAngleDegrees = angle,
		HitboxRadius = 0.0f,
		Unblockable = true,
	};

	/// <summary>The weak hits a throw lands while it still holds them.</summary>
	static MoveData Link(string name, float damage) => new MoveData
	{
		MoveName = name, Damage = damage, BaseKnockback = 8.0f, KnockbackGrowth = 0.0f,
		LaunchAngleDegrees = 80.0f, Unblockable = true,
	};

	static Texture2D Art(string fighter, string name)
	{
		string path = $"res://fighters/{fighter}/poses/{name}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	// Each set is forward, back, up, down.

	/// <summary>Flambe: flame, his frying pan, and a fire column.</summary>
	public static MoveData[] Flambe()
	{
		MoveData f = Throw("Flambe Toss", AttackAnim.PalmThrust, 8.0f, 55.0f, 0.6f, 40.0f);
		f.ActiveFx = ActiveFx.Flame; f.BurnFrames = 40; f.BurnDamage = 2.0f; f.PropArt = "-";
		MoveData b = Throw("Pan Flip", AttackAnim.OverheadArc, 9.0f, 50.0f, 0.7f, 42.0f);
		b.PropArt = "tool_pan"; b.SpinVictim = true;
		MoveData u = Throw("Fire Lift", AttackAnim.Uppercut, 7.0f, 60.0f, 0.55f, 88.0f);
		u.ActiveFx = ActiveFx.Flame; u.BurnFrames = 40; u.BurnDamage = 2.0f; u.PropArt = "-";
		MoveData d = Throw("Hot Plate", AttackAnim.OverheadSlam, 6.0f, 70.0f, 0.3f, 72.0f);
		d.PropArt = "tool_pan"; d.ActiveFx = ActiveFx.Flame; d.BurnFrames = 60; d.BurnDamage = 2.0f;
		return new[] { f, b, u, d };
	}

	/// <summary>Lug: a shove, his shovel, a heave over his head, and his sledgehammer.</summary>
	public static MoveData[] Construction()
	{
		MoveData f = Throw("Barrow Dump", AttackAnim.Lunge, 10.0f, 55.0f, 0.75f, 35.0f);
		f.PropArt = "-";
		MoveData b = Throw("Shovel Fling", AttackAnim.BackSlash, 11.0f, 50.0f, 0.8f, 40.0f);
		b.PropArt = "tool_shovel";
		MoveData u = Throw("Heave", AttackAnim.UpSmash, 9.0f, 60.0f, 0.65f, 90.0f);
		u.PropArt = "-";
		// Two bonks with the sledgehammer while they are pinned, then one that pops them up.
		MoveData d = Throw("Sledge Pin", AttackAnim.OverheadSlam, 8.0f, 55.0f, 0.5f, 74.0f);
		d.PropArt = "tool_sledgehammer"; d.ActiveFrames = 12; d.RehitFrames = 4; d.LinkHit = Link("Sledge Pin (bonk)", 2.0f);
		return new[] { f, b, u, d };
	}

	/// <summary>Circy: a long kick, a roll over them, springing up tall, and sitting on them.</summary>
	public static MoveData[] Circy()
	{
		MoveData f = Throw("Long Kick", AttackAnim.FrontKick, 8.0f, 55.0f, 0.7f, 30.0f);
		f.StretchLeg = true; f.HitboxOffset = new Vector2(120.0f, 20.0f);
		MoveData b = Throw("Roll Over", AttackAnim.Nair, 9.0f, 50.0f, 0.7f, 45.0f);
		b.SpinVictim = true;
		MoveData u = Throw("Spring Up", AttackAnim.UpSmash, 8.0f, 60.0f, 0.7f, 88.0f);
		MoveData d = Throw("Ball Drop", AttackAnim.Dair, 7.0f, 60.0f, 0.4f, 80.0f);
		d.ActiveFrames = 9; d.RehitFrames = 3; d.LinkHit = Link("Ball Drop (bounce)", 1.5f);
		return new[] { f, b, u, d };
	}

	/// <summary>EdgeLord: swords. His up throw sends a sword up after them - a two-hit combo.</summary>
	public static MoveData[] EdgeLord()
	{
		MoveData f = Throw("Blade Fling", AttackAnim.Punch, 7.0f, 55.0f, 0.65f, 30.0f);
		f.PropArt = "sword_katana";
		MoveData b = Throw("Reverse Cut", AttackAnim.BackSlash, 10.0f, 50.0f, 0.75f, 40.0f);
		b.PropArt = "sword_katana";
		// Up into the air - and a shortsword thrown up after them, fast enough to catch them.
		MoveData u = Throw("Skyward Blade", AttackAnim.PointUp, 6.0f, 55.0f, 0.4f, 90.0f);
		u.PropArt = "-";
		u.FollowShot = new MoveData
		{
			MoveName = "Skyward Blade (sword)",
			Damage = 6.0f, BaseKnockback = 45.0f, KnockbackGrowth = 0.75f, LaunchAngleDegrees = 88.0f,
			Special = SpecialKind.Projectile, SpecialSpeed = 2300.0f, SpecialLifetime = 30,
			FxRadius = 26.0f, FxColor = new Color(0.80f, 0.82f, 0.86f),
			FxTexture = Art("edgelord", "sword_shortsword"), FxArtSize = 150.0f, FxAlongFlight = true,
		};
		// Pinned under the broadsword: two cuts, then one that pops them up.
		MoveData d = Throw("Pin Down", AttackAnim.DownSwing, 8.0f, 55.0f, 0.5f, 75.0f);
		d.PropArt = "sword_broadsword"; d.ActiveFrames = 12; d.RehitFrames = 4; d.LinkHit = Link("Pin Down (cut)", 2.0f);
		return new[] { f, b, u, d };
	}

	/// <summary>
	/// DoomBot: a piston shove, a windmill toss, lightning from his antennae - and his down throw
	/// pins them to the floor while his rocket boots burn them, then blasts them off.
	/// </summary>
	public static MoveData[] DoomBot()
	{
		MoveData f = Throw("Piston Shove", AttackAnim.PalmThrust, 11.0f, 60.0f, 0.75f, 35.0f);
		f.PropArt = "-";
		MoveData b = Throw("Windmill Toss", AttackAnim.BackSlash, 12.0f, 52.0f, 0.8f, 40.0f);
		b.PropArt = "-";
		MoveData u = Throw("Antenna Zap", AttackAnim.UpSmash, 10.0f, 60.0f, 0.7f, 88.0f);
		u.PropArt = "-"; u.ActiveFx = ActiveFx.Electric;
		MoveData d = Throw("Jet Burn", AttackAnim.Stomp, 6.0f, 60.0f, 0.5f, 70.0f);
		d.PropArt = "-"; d.ActiveFx = ActiveFx.Jets;
		d.ActiveFrames = 18; d.RehitFrames = 4; d.LinkHit = Link("Jet Burn (flame)", 2.0f);
		d.LinkHit.BurnFrames = 20; d.LinkHit.BurnDamage = 1.0f;
		d.BurnFrames = 45; d.BurnDamage = 2.0f;
		return new[] { f, b, u, d };
	}

	/// <summary>Triguy: his point, his skid, a bounce off his trampoline, and a dunk in his tears.</summary>
	public static MoveData[] Triguy()
	{
		MoveData f = Throw("Point Poke", AttackAnim.PointThrust, 8.0f, 55.0f, 0.65f, 35.0f);
		MoveData b = Throw("Skid Flip", AttackAnim.Bair, 9.0f, 50.0f, 0.7f, 40.0f);
		b.SpinVictim = true;
		MoveData u = Throw("Trampoline Toss", AttackAnim.Uppercut, 7.0f, 70.0f, 0.6f, 90.0f);
		MoveData d = Throw("Puddle Dunk", AttackAnim.BuildDown, 7.0f, 55.0f, 0.45f, 80.0f);
		d.ActiveFrames = 9; d.RehitFrames = 3; d.LinkHit = Link("Puddle Dunk (splash)", 1.5f);
		d.Sound = "splash";
		return new[] { f, b, u, d };
	}
}
