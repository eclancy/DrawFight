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

	/// <summary>
	/// Slams the ground, and one earthquake spreads out BOTH ways along the floor from where the
	/// blow lands, as well as the move's own hitbox. Each side stops where the ground ends, so it
	/// never chases anyone off an edge. Works on a normal attack too - Lug's down smash.
	/// </summary>
	Shockwave,

	/// <summary>
	/// Builds a temporary platform under the user's own feet (see <see cref="BuiltPlatform"/>).
	/// They stand on it and can jump off it; after a while it falls and hits whatever it lands
	/// on. Usable once per trip into the air, and standing on it gives nothing back - otherwise
	/// build, jump, build is a recovery that never ends.
	/// </summary>
	BuildPlatform,

	/// <summary>
	/// A stance that lays out four moves around the fighter - up, forward, back and down - and
	/// the first way the stick is pushed picks which comes out (see <see cref="MoveData.Choices"/>). A
	/// null choice is "cancel": pushing that way puts the stance away with nothing thrown.
	/// Back turns the fighter round first. Nothing picked by the end of the window throws the
	/// forward one, so a tap still does something. EdgeLord's Infinite Swords.
	/// </summary>
	Choice,

	/// <summary>
	/// The CommandGrab archetype on its own, not riding on a recovery: the front arms stretch out
	/// along the ground through the startup and seize the first fighter they touch, reel them in
	/// through the active frames, then <see cref="MoveData.GrabThrow"/> plays as the follow-up -
	/// DoomBot's grab-and-kick. Goes through blocking. Catching nobody is a whiff: the arms come
	/// back empty. See .ai/character-design.md for how rare these are meant to stay.
	/// </summary>
	CommandGrab,

	/// <summary>
	/// Lets out all of a fighter's heat at once (see <see cref="FighterData.HasHeat"/>): a burst
	/// in every direction around them whose size, damage and knockback follow how hot they were.
	/// Cool, it is a puff of steam; at full heat it is a blast of flame. Heat goes back to zero.
	/// DoomBot's Furnace Blast.
	/// </summary>
	Vent,

	/// <summary>
	/// Forms a cloud at <see cref="MoveData.HitboxOffset"/> - well above the fighter - that hangs
	/// there for its lifetime dropping <see cref="MoveData.RainDrop"/> every
	/// <see cref="MoveData.RainInterval"/> frames, scattered across its width. The cloud itself
	/// never hits; the rain does. Flambe's fire cloud.
	/// </summary>
	Cloud,

	/// <summary>
	/// Sets down a little walker on the floor in front - a MiniBot - that marches the way the
	/// fighter faced at <see cref="MoveData.SpecialSpeed"/>, falls if there is nothing under it,
	/// and stops at an edge or a wall to wait. The moment it touches anyone, or when its lifetime
	/// runs out, it goes off as <see cref="MoveData.Burst"/>. It never hits by itself. Drawn as a
	/// small copy of the fighter's own rig, <see cref="MoveData.FxArtSize"/> tall. DoomBot's
	/// down tilt.
	/// </summary>
	Walker,
}

/// <summary>
/// Something drawn at a normal attack's hitbox while it is live - what the hit is made of, when
/// a swing trail alone would not say it. Drawn by Fighter, never hits anything itself.
/// </summary>
public enum ActiveFx
{
	None,

	/// <summary>A spray of sparks off the hitbox - steel striking.</summary>
	Sparks,

	/// <summary>
	/// Crackling electricity: bolts up from the head to a hitbox above it (antennae), or a
	/// crackling ring around a hitbox anywhere else (a field).
	/// </summary>
	Electric,

	/// <summary>Twin flame jets out of the bottom of both feet - rocket boots.</summary>
	Jets,

	/// <summary>A burst of flame tongues round the hitbox - a kick or a punch on fire.</summary>
	Flame,
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

	/// <summary>Which body animation the move plays - see <see cref="FighterAnimations.PosesFor"/>.</summary>
	[Export] public AttackAnim Anim { get; set; } = AttackAnim.Punch;

	/// <summary>
	/// The sound this move makes, by name, when the one picked from its data is wrong for it.
	/// Empty - nearly always - lets SfxCatalog.ForMove choose. See .ai/audio-direction.md.
	/// </summary>
	[Export] public string Sound { get; set; } = "";

	/// <summary>
	/// A hitbox that travels round an arc through the active frames instead of sitting still - a
	/// real swing. <see cref="HitboxOffset"/> is the MIDDLE of the arc; the hitbox starts this many
	/// degrees round from it, on the side behind him, and sweeps through to the same distance on
	/// the far side, round the swing pivot near the shoulders. Positive sweeps from behind, up and
	/// over, to the front. Zero is an ordinary hitbox. EdgeLord's up smash. Use with
	/// <see cref="AttackAnim.WideArc"/>, whose arms follow the hitbox round.
	/// </summary>
	[Export] public float SweepDegrees { get; set; } = 0.0f;

	/// <summary>What the move's hitbox is drawn as while live, on top of its swing trail.</summary>
	[Export] public ActiveFx ActiveFx { get; set; } = ActiveFx.None;

	// --- Burning -----------------------------------------------------------------

	/// <summary>
	/// Sets whoever it hits on fire for this many frames: they take <see cref="BurnDamage"/> in
	/// small ticks while flames lick off them. Zero does not burn. A blocked hit does not burn,
	/// and burning again only tops the burn back up - it never stacks.
	/// </summary>
	[Export] public int BurnFrames { get; set; } = 0;

	/// <summary>The percent a whole burn adds, spread over its ticks.</summary>
	[Export] public float BurnDamage { get; set; } = 0.0f;

	/// <summary>
	/// Launches away from the attacker on whichever side the victim is, instead of the way the
	/// attacker faces. For a blast in every direction - someone behind it flies backward.
	/// </summary>
	[Export] public bool LaunchAway { get; set; } = false;

	// --- Combos and charging ---------------------------------------------------

	/// <summary>
	/// The next hit in a combo. Pressing attack again during this move queues it, and it comes
	/// out the moment this move's hitbox is done - how tapping attack becomes a jab-jab-finisher.
	/// Early hits in a chain need low knockback, so the target is still in reach for the next.
	/// </summary>
	[Export] public MoveData ComboNext { get; set; }

	/// <summary>Holding attack during the windup charges this move for more damage. Smash attacks.</summary>
	[Export] public bool Chargeable { get; set; } = false;
	// A chargeable Dash goes nowhere while it charges, then goes on release - faster and further
	// the longer it was held, by up to ChargeSize times its SpecialSpeed (Flambe's fireball roll).

	/// <summary>
	/// The fighter tucks into a ball and spins for the whole move, drawn as its body part alone
	/// with the limbs hidden - Circy rolling into someone. Only for a fighter whose body part
	/// reads as the whole fighter.
	/// </summary>
	[Export] public bool BallForm { get; set; } = false;

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

	/// <summary>
	/// A beam instead of a flying projectile: it stays attached to the fighter's hands and grows
	/// outward at <see cref="SpecialSpeed"/> up to <see cref="Reach"/>, hitting anything along
	/// its length. Because it moves with the fighter, it works the same on the ground or in the air.
	/// </summary>
	[Export] public bool Beam { get; set; } = false;

	/// <summary>How far a <see cref="Beam"/> reaches, in pixels.</summary>
	[Export] public float Reach { get; set; } = 0.0f;

	/// <summary>
	/// A projectile fired as a burst: this many shots, <see cref="BurstInterval"/> frames apart,
	/// starting on the first active frame. Each shot hits on its own - a nail gun.
	/// </summary>
	[Export] public int BurstCount { get; set; } = 1;
	[Export] public int BurstInterval { get; set; } = 4;

	/// <summary>Draw a projectile as a short streak along its path, like a nail, not a ball.</summary>
	[Export] public bool Streak { get; set; } = false;

	/// <summary>
	/// A drawing from the fighter's own art held in front of them for the whole move - Lug's
	/// wheelbarrow. Named like a pose in rig.json; offset is from the body centre, facing right.
	/// </summary>
	[Export] public string HeldArt { get; set; } = "";
	[Export] public Vector2 HeldArtOffset { get; set; } = Vector2.Zero;
	[Export] public float HeldArtSize { get; set; } = 100.0f;

	/// <summary>
	/// A drawing swung on a chain from the hand - Lug's wrecking ball. It sweeps from behind him
	/// up and over through the startup, then leads the way for the rest of the move.
	/// </summary>
	[Export] public string SwingArt { get; set; } = "";

	/// <summary>How long a built platform holds still before it falls, in frames.</summary>
	[Export] public int PlatformHoldFrames { get; set; } = 70;

	/// <summary>How wide a built platform is, in pixels.</summary>
	[Export] public float PlatformWidth { get; set; } = 190.0f;

	/// <summary>How hard a <see cref="SpecialKind.Shockwave"/>'s quake hits, as a share of the slam.</summary>
	[Export] public float ShockwavePower { get; set; } = 0.7f;
	[Export] public float SwingLength { get; set; } = 110.0f;
	[Export] public float SwingArtSize { get; set; } = 70.0f;

	/// <summary>
	/// For a <see cref="SpecialKind.Choice"/>: the four moves on offer, in the order up, forward,
	/// back, down. Each is an ordinary move that plays in full once picked. Not exported, like
	/// <see cref="FighterData.Moves"/>: it is built in code.
	/// </summary>
	public MoveData[] Choices = System.Array.Empty<MoveData>();

	/// <summary>
	/// A Dash so fast it reads as a teleport. The startup is spent standing still, glinting - the
	/// warning - and the whole distance is crossed in the active frames, after which he stops.
	/// </summary>
	[Export] public bool Blink { get; set; } = false;

	/// <summary>
	/// Usable once per trip into the air, like the up special. For any special that moves the
	/// fighter far enough that chaining it would be a second recovery.
	/// </summary>
	[Export] public bool OncePerAirtime { get; set; } = false;

	/// <summary>
	/// A <see cref="DelayedLaunch"/> recovery whose tether GRABS the first fighter it touches:
	/// they are reeled in through the startup, then thrown with this move as the launch fires.
	/// The recovery still happens either way - grabbing never costs the way home.
	/// </summary>
	public MoveData GrabThrow;

	/// <summary>The tether is the fighter's own front arm, stretched, instead of a rope.</summary>
	[Export] public bool StretchArm { get; set; } = false;

	/// <summary>
	/// A drawing from the fighter's rig (a pose name) on the end of a tether, pointing the way it
	/// flies, with the tether drawn as a chain - EdgeLord's dagger on a chain. Empty keeps the
	/// rope, the drawn rope-and-hook, or the stretched arm.
	/// </summary>
	[Export] public string TetherArt { get; set; } = "";

	/// <summary>
	/// The kicking leg - the front one - stretches out to the hitbox through the windup and back
	/// after, on its own, at whatever size the fighter is. Circy's long kicks: stretching is his
	/// thing.
	/// </summary>
	[Export] public bool StretchLeg { get; set; } = false;

	/// <summary>
	/// The hit turns its victim head over heels through their hitstun - flipped like a crepe, or
	/// tripped over a cone. Only the drawing turns; the launch is the move's own.
	/// </summary>
	[Export] public bool SpinVictim { get; set; } = false;

	/// <summary>
	/// Through its windup and swing, the move shrugs off any hit doing this much damage or less:
	/// the damage still counts, but there is no flinch and no knockback. Lug's hard hat. Bigger
	/// hits get through, and so does a grab. Zero is no armour.
	/// </summary>
	[Export] public float Armor { get; set; } = 0.0f;

	/// <summary>
	/// A trap that is kicked rather than set down: it leaves along the floor at SpecialSpeed and
	/// skids to a stop, losing this many pixels per second every second, and drops off any edge it
	/// slides over. Lug's traffic cone. Zero is a trap that stays where it is put.
	/// </summary>
	[Export] public float SlideFriction { get; set; } = 0.0f;

	/// <summary>What a <see cref="SpecialKind.Walker"/> goes off as - a hazard left where it stood.</summary>
	[Export] public MoveData Burst { get; set; }

	/// <summary>
	/// A blink that goes to one of the fighter's own traps - a planted blade - when one is ahead
	/// and close enough, instead of straight ahead, and pulls it out when it gets there.
	/// EdgeLord's Blur Slash.
	/// </summary>
	[Export] public bool BlinkToTrap { get; set; } = false;

	/// <summary>
	/// How many of this move's hazards can be out at once. Making another removes the oldest.
	/// Zero means no limit.
	/// </summary>
	[Export] public int MaxOut { get; set; } = 0;

	/// <summary>
	/// The fighter spins on the spot for the move: drawn with its turning frames (the extras named
	/// turn0 to turn4 in rig.json) in place of the body and arms, and with <see cref="HeldArt"/>
	/// as two blades held out either side, going round with it. Not used by anyone at present.
	/// </summary>
	[Export] public bool Spin { get; set; } = false;

	/// <summary>
	/// A multi-hit: the hitbox can hit the same fighter again every this many active frames.
	/// Every hit but those in the last window is <see cref="LinkHit"/> - weak, and holding them
	/// in place - and the last is this move, the launcher.
	/// </summary>
	[Export] public int RehitFrames { get; set; } = 0;
	public MoveData LinkHit;

	/// <summary>Upward speed a thrown projectile starts with, px/s. Zero means a small toss.</summary>
	[Export] public float LaunchLift { get; set; } = 0.0f;

	/// <summary>How big <see cref="FxTexture"/> is drawn, in pixels along its longest side. Zero fits it to the hitbox.</summary>
	[Export] public float FxArtSize { get; set; } = 0.0f;

	/// <summary>Degrees per frame the effect art turns as it flies - a sword tumbling end over end.</summary>
	[Export] public float FxSpin { get; set; } = 0.0f;

	/// <summary>The effect art points the way it is flying, as a thrown blade does. Art is drawn tip-up.</summary>
	[Export] public bool FxAlongFlight { get; set; } = false;

	/// <summary>
	/// A drawing from the fighter's rig (a pose name) put in their hand for this move, in place
	/// of the prop they normally hold - EdgeLord drawing a different sword for each attack. "-"
	/// means empty-handed. Empty keeps the usual prop.
	/// </summary>
	[Export] public string PropArt { get; set; } = "";

	/// <summary>
	/// A projectile that fires several at once, one per angle in degrees (0 is forward, 90 is
	/// straight up, measured toward the facing). EdgeLord's ring of daggers. Empty fires one,
	/// forward.
	/// </summary>
	public float[] ShotAngles = System.Array.Empty<float>();

	/// <summary>Also fires a mirror image of every shot on the other side of the fighter.</summary>
	[Export] public bool Mirrored { get; set; } = false;

	/// <summary>
	/// The projectile comes up out of the floor under the fighter instead of from the hands: it
	/// starts buried, rises at <see cref="SpecialSpeed"/>, and sinks back as gravity takes it.
	/// Only the part above the floor is drawn. Summoned axes.
	/// </summary>
	[Export] public bool FromGround { get; set; } = false;

	/// <summary>A lingering hazard that is used up by its first hit - a planted blade.</summary>
	[Export] public bool SpentOnHit { get; set; } = false;

	/// <summary>
	/// A chargeable move that charges while SPECIAL is held rather than attack - Swift's fireball.
	/// Needs <see cref="Chargeable"/>. Works in the air. A projectile grows with the charge (see
	/// <see cref="ChargeSize"/>).
	/// </summary>
	[Export] public bool ChargeWithSpecial { get; set; } = false;

	/// <summary>Damage (and so knockback) multiplier at a full charge. Smashes use 1.4.</summary>
	[Export] public float ChargeDamage { get; set; } = 1.4f;

	/// <summary>How much bigger a charged projectile is at a full charge.</summary>
	[Export] public float ChargeSize { get; set; } = 1.0f;

	/// <summary>
	/// A Recovery that flies instead of launching: through the active frames the fighter rises
	/// steadily at <see cref="SpecialRise"/> px/s and can steer left and right. Swift's fire
	/// wings. <see cref="HeldArt"/>, if set, is drawn behind the fighter and flaps.
	/// </summary>
	[Export] public bool Flight { get; set; } = false;

	/// <summary>Draw a projectile with no art as a ball of fire with a flickering tail.</summary>
	[Export] public bool FxFlame { get; set; } = false;

	/// <summary>What a <see cref="SpecialKind.Cloud"/> drops. Not exported, like GrabThrow: built in code.</summary>
	public MoveData RainDrop;

	/// <summary>Frames between drops from a <see cref="SpecialKind.Cloud"/>.</summary>
	[Export] public int RainInterval { get; set; } = 8;

	/// <summary>Draw a projectile with no art as a little missile with fins and an exhaust flame.</summary>
	[Export] public bool FxMissile { get; set; } = false;

	/// <summary>
	/// A named spot on the fighter's drawing (a "points" entry in rig.json) that a
	/// <see cref="Beam"/> comes out of and stays attached to. Empty, or a drawing without that
	/// spot, means the hands. DoomBot's eye laser comes out of his "eye".
	/// </summary>
	[Export] public string BeamFrom { get; set; } = "";

	/// <summary>An extra drawing from the rig shown for this move - Lug's hard hat on a barge.</summary>
	[Export] public string ShowExtra { get; set; } = "";

	/// <summary>
	/// For a <see cref="SwingArt"/> recovery: the drawing swings up to hang directly ABOVE him,
	/// and he dangles from its rope with both hands while it hauls him up - rather than it
	/// leading forward from one hand.
	/// </summary>
	[Export] public bool HangFromArt { get; set; } = false;

	/// <summary>
	/// Let go of at the end of the active frames: the swung drawing drops away as this move - a
	/// <see cref="SpecialKind.Drop"/>, falling from where it was and hitting whatever is under it.
	/// </summary>
	public MoveData ReleaseDrop;

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
			Anim = Anim,
			Sound = Sound,
			ActiveFx = ActiveFx,
			SweepDegrees = SweepDegrees,
			RainDrop = RainDrop,
			RainInterval = RainInterval,
			BurnFrames = BurnFrames,
			BurnDamage = BurnDamage * scale.Damage,
			LaunchAway = LaunchAway,
			FxMissile = FxMissile,
			BeamFrom = BeamFrom,
			ComboNext = ComboNext?.Scaled(scale),
			Chargeable = Chargeable,
			BallForm = BallForm,
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
			Reach = Reach,
			BurstCount = BurstCount,
			BurstInterval = BurstInterval,
			Streak = Streak,
			HeldArt = HeldArt,
			HeldArtOffset = HeldArtOffset,
			HeldArtSize = HeldArtSize,
			SwingArt = SwingArt,
			ShockwavePower = ShockwavePower,
			PlatformHoldFrames = PlatformHoldFrames,
			PlatformWidth = PlatformWidth,
			SwingLength = SwingLength,
			SwingArtSize = SwingArtSize,
			Choices = Choices,
			Blink = Blink,
			OncePerAirtime = OncePerAirtime,
			GrabThrow = GrabThrow,
			StretchArm = StretchArm,
			TetherArt = TetherArt,
			StretchLeg = StretchLeg,
			SpinVictim = SpinVictim,
			Armor = Armor,
			SlideFriction = SlideFriction,
			Burst = Burst?.Scaled(scale),
			BlinkToTrap = BlinkToTrap,
			MaxOut = MaxOut,
			Spin = Spin,
			RehitFrames = RehitFrames,
			LinkHit = LinkHit?.Scaled(scale),
			LaunchLift = LaunchLift,
			FxArtSize = FxArtSize,
			FxSpin = FxSpin,
			FxAlongFlight = FxAlongFlight,
			PropArt = PropArt,
			ShotAngles = ShotAngles,
			Mirrored = Mirrored,
			FromGround = FromGround,
			SpentOnHit = SpentOnHit,
			ShowExtra = ShowExtra,
			ChargeWithSpecial = ChargeWithSpecial,
			ChargeDamage = ChargeDamage,
			ChargeSize = ChargeSize,
			Flight = Flight,
			FxFlame = FxFlame,
			HangFromArt = HangFromArt,
			ReleaseDrop = ReleaseDrop,
			FxColor = FxColor,
			FxRadius = FxRadius,
			FxTexture = FxTexture,
		};
	}

	/// <summary>
	/// Grows this move's hitbox in place - where it is and how big - for a fighter drawn bigger
	/// than everyone else, so the hit stays on the claw or boot that throws it. Follows the move's
	/// combo, link hit and grab follow-up. Tether lengths and effect sizes are left to the
	/// character: a long arm is a choice, not a consequence of being big.
	/// </summary>
	public void ScaleReach(float k)
	{
		HitboxOffset *= k;
		HitboxRadius *= k;
		ComboNext?.ScaleReach(k);
		LinkHit?.ScaleReach(k);
		GrabThrow?.ScaleReach(k);
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
