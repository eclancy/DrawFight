# Character design

How "he shoots fire and he can fly" becomes a balanced, buildable fighter. Read before
designing any character's moveset or stats.

## The intake

Every fighter starts from a filled-in character sheet — see `for-nephew/character-sheet.md`.
It asks five things:

1. What is your fighter's **name**?
2. What **is** it? (a robot, a wizard, a shark with legs)
3. What is it **amazing** at?
4. What is it **terrible** at?
5. What is its **coolest move**, and what does that move look like?

Question 4 is the important one and it is doing balance work while disguised as a fun question.
A kid asked to design a fighter will make it fast *and* strong *and* tough. A kid asked what his
fighter is terrible at will cheerfully invent a weakness, because weaknesses are characterful
and he knows that from every cartoon he has ever watched. **Always ask it before anything is
built, never after** — retroactively nerfing his character is a fight, but a weakness he
authored himself is canon.

## Weight class comes first

Every fighter is **Light, Medium or Heavy**, and the weight is not only a survivability stat -
it retunes the whole moveset:

| weight | attacks | survives |
|--------|---------|----------|
| Light  | fast, weak, short reach | dies early |
| Medium | the baseline everything is authored at | average |
| Heavy  | slow, hard-hitting, long reach | dies late |

Scaling numbers is not enough on its own — it made every fighter feel like the same fighter at
different speeds. So **every character has its own normals** (`CharacterNormals.cs`), each one
an edit of the shared baseline: its own name, animation, and a reshaped hitbox, angle or timing
that fits who the character is. Weight still scales them afterwards, and the jab combo's length
follows weight — light fighters get quick multi-hit strings, heavies get two slow hard hits.

| | Swift (light, fire) | Circy (medium, Elim's) | Lug (heavy, construction) |
|---|---|---|---|
| theme | a hotshot punk rocker who is also a French chef: flame mohawk, spiked and studded leather jacket, wallet chain, chef's neckerchief, a defiant smirk, flames on everything; lean and long in the leg, so nearly every normal is a kick; a slow charged fireball, a charged fireball roll, two flapping fire wings, a cloud that rains fire; every normal has its own trick | ball on long stretchy legs: tall for big, slow hits, short for small, quick ones | a construction worker in a hi-vis vest: normals with a different heavy tool each, site-tool specials (nail gun, wheelbarrow, wrecking ball, and a steel girder he builds under his own feet, stands on, and that falls); his hard hat glints while he blocks, and while it is armour through his smashes and barge; huge up top and short in the leg, so almost nothing he does uses his feet |
| jab | two flicker jabs, then "Flambe!" - a burning palm | kick, low kick, ball bonk | two slow pipe-wrench bashes |
| tilts | roundhouse (press again: heel hook), torch flip (a flurry that pops them up), crêpe flip (the pan slid under them flips them head over heels) | kicks that stretch the kicking leg out to the hit | shovel jab, pickaxe arc, cone shove (a traffic cone shoved along the floor with both hands; whoever runs into it trips) |
| dash | swan dive (flat out, corkscrewing, wrapped in fire) | rolls into them as a ball | hard-hat barge |
| smashes | flambe pan (a flaming frying pan, slow, burns), fire pillar (four climbing hits), fire geysers (out of the floor both sides) | ball headbutt, spring up, splits | sledge slam, beam heave (a steel I-beam; big, slow, telegraphed), sledgehammer quake (shockwave both ways along the floor) |
| aerials | fire wheel (a quick flurry all round), double axe kick, backdraft (burns), bicycle flurry, fire drill (no spike) | ball spin, stretch kick, donkey kick, flip, stomp | stop sign spin, sledge chop, shovel back swing, pick swipe, jackhammer (ridden down, many hits, a spike) |

This is why the normal attacks are authored **once, at medium**, in `DefaultMoveset.cs` and
scaled per weight by `WeightProfiles`. A balance change to a tilt then lands on all three
weights at once instead of being fixed in three places.

The scaling is not symmetrical, on purpose. Heavy pays more in endlag than it gains in startup,
so a heavy fighter is *punishable when it misses* rather than merely slower to start.

### Knockback growth scales DOWN for heavies

The one that is easy to get wrong, and did get wrong. Knockback already depends on damage, so
raising a heavy fighter's damage raises its knockback for free. Scaling knockback growth up as
well double-dips, and the two **compound**: the first pass gave the heavy fighter a back air
that KO'd at 50%.

So `MoveScale.Growth` moves opposite to `MoveScale.Damage` - heavies trade growth away to pay
for the damage they gained. Current result: the heavy KOs the light fighter from about 75-95%,
and the light KOs the heavy from about 145%. Heavy kills early and dies late; light kills late
and dies early. Read the calibration table after touching any of it.

## Description to stats

Answers 3 and 4 map onto the `FighterData` stat block from `.ai/fighting-design.md`. The
mapping is loose and deliberately so; the job is to make the stat block *disagree with itself*
somewhere.

| he says                     | stats                                                        |
|-----------------------------|--------------------------------------------------------------|
| big, strong, tough, a tank  | high `Weight`, low `RunSpeed`, low `JumpForce`, slow moves    |
| fast, quick, zippy          | high `RunSpeed`/`AirSpeed`, low `Weight` — dies early         |
| can fly, floaty, a ghost    | low `FallSpeed`, high `AirAcceleration`, weak on the ground   |
| heavy, slow, falls hard     | high `FallSpeed` — hits hard from the air, recovers poorly    |
| small, sneaky               | small `VisualScale` and hurtbox, low `Weight`, short reach    |

The **one hard rule**: no fighter is above average in both `Weight` and `RunSpeed`. That single
constraint prevents the character that is simply better than everyone else, which is the failure
mode that ends couch multiplayer.

## The four specials

Every fighter gets exactly four, one per direction. The slots have fixed jobs:

- **Neutral special** — the **signature**. This is answer 5 from the sheet, built as literally
  as it can be built. Whatever he said the coolest move was, this is that move. Do not
  compromise this slot for balance; compromise the numbers instead.
- **Side special** — approach or burst. Closes distance, or punishes someone who is far away.
- **Up special** — **the recovery, always.** It must provide real vertical distance, because a
  fighter who cannot get back to the stage from below is unplayable regardless of how good the
  rest of the kit is. This constraint is non-negotiable and it is the first thing to check on
  any new character. It can have a second identity on top — a recovery that also attacks is
  normal — but recovery comes first.
- **Down special** — defence or utility. A counter, a trap, a reflector, a stance. Like any
  slot it can carry the signature instead when the sheet asks for it there: DoomBot's coolest
  move, the Furnace Blast, is his down special because Eric said so, and his neutral special is
  the eye laser.

## The archetype library

Specials are built from a small set of reusable behaviour scripts. A new special is almost
always **an existing archetype plus different numbers plus his hand-drawn effect art**, not new
code. New archetypes should be rare; if two characters want the same novel behaviour, that is
the signal to build one.

| archetype       | what it does                                         | typical slot   |
|-----------------|------------------------------------------------------|----------------|
| `Projectile`    | spawns a travelling hitbox                            | neutral, side  |
| `Charge`        | hold to power up, release for a scaled version        | neutral        |
| `Dash`          | move fast with a hitbox on your own body              | side, up       |
| `Counter`       | brief window; if hit, negate and retaliate harder     | down           |
| `Recovery`      | launches the user in a chosen direction               | up             |
| `Trap`          | leaves a lingering hazard on the stage                | down           |
| `Summon`        | spawns a temporary helper that acts on its own        | neutral, down  |
| `Buff`          | temporary stat change with a visible tell             | down           |
| `Reflect`       | turns projectiles around                              | down           |
| `Multihit`      | a sustained move that hits repeatedly then launches   | neutral, side  |
| `CommandGrab`   | unblockable; seizes the victim and throws them        | neutral, side  |
| `Resize`        | held stance; stick up grows, down shrinks. Each size is a trade (`SizeLevels`) | neutral |
| `Bomb`          | a counter that explodes: hit it or wait out the fuse, it hits everyone near, user takes `SelfDamage` | down |
| `Choice`        | a stance showing moves round the fighter; the stick picks one (`Choices`: up, forward, back, down). A null slot is a cancel, drawn as a cross: EdgeLord's back puts the swords away | neutral |
| `Vent`          | lets a fighter's heat out at once: a burst all round them, sized by how hot they were (`Heat.VentAt`) | down |
| `Cloud`         | forms a cloud high above that hangs there raining `RainDrop` every `RainInterval` frames; the cloud never hits, the rain does | down |

Built so far: `Projectile`, `Dash`, `Recovery`, `Trap`, `Drop` (as `SpecialKind.Drop`, a falling
spike), `Resize`, `Bomb`, `Shockwave`, `BuildPlatform`, `Choice`, `CommandGrab`, `Vent` and `Cloud`.
`CommandGrab` exists twice: as its own kind (DoomBot's Claw Grab - arms stretch out along the
ground, catch, reel in, then `GrabThrow` plays as a follow-up move) and as a flag on a tether
recovery (`GrabThrow`, below). The rest are designs, not code.

Some behaviour is a **flag on `MoveData`** rather than a whole archetype, because it bolts onto
any of them:

- `SelfDamage` — the user takes this much percent (never knockback) when the move connects.
  How "it hurts him too" is honoured without making the move useless.
- `DelayedLaunch` + `TetherLength` — a `Recovery` that hangs while a hook flies out, then yanks
  the user along it. A grappling hook. It never needs to catch on anything: a recovery that can
  miss loses stocks for reasons a player cannot see.
- `Beam` + `Reach` — a projectile that does not fly: it grows out from the user's hands, far
  end first, and stays attached to them, so it works the same fired in the air. Give it a real
  charge-up (startup) — a long beam with no warning is the only move worth using.

- `Blink` - a `Dash` so fast it reads as a teleport: it stands and glints through a long startup,
  then crosses the whole distance in its active frames. The glint is the price of the speed.
- `OncePerAirtime` - usable once until landing, like every up special. Any special that moves
  a fighter far must have it, or chaining it becomes a second recovery.
- `GrabThrow` - on a `DelayedLaunch` recovery, the tether catches the first fighter it touches,
  reels them in, and throws them with this move as the launch fires. The recovery happens
  either way. One of the roster's two command grabs (EdgeLord's Chain Blade); see below.
- `TetherArt` - a tether drawn as a chain with one of the fighter's drawings on the end, pointing
  the way it flies, instead of a rope or a stretched arm. EdgeLord's dagger on a chain.
- `MaxOut` - how many of a move's hazards can exist at once; another removes the oldest.
- `Spin` + `RehitFrames` + `LinkHit` - a spin on the spot drawn with the fighter's turning
  frames, hitting repeatedly with a weak link hit and launching on the last window. Built for
  EdgeLord's first down smash; nothing uses it now, but his turning frames are still cut.
- `SpentOnHit` - a lingering trap that is used up by the first hit it lands.
- `ShowExtra` - shows one of the rig's extras for the move (the glint on Lug's hard hat through a barge).
- `ChargeWithSpecial` + `ChargeDamage` + `ChargeSize` - a chargeable special: hold the special
  button and it charges (in the air too), then fires bigger and harder. Swift's fireball.
- `Flight` - a recovery that flies: a steady slow rise through a long active window, steered
  left and right, with `HeldArt` drawn behind as flapping wings. Checked by the height it gains
  rather than by launch speed. Swift's fire wings.
- `FxFlame` - a projectile with no art drawn as a ball of fire with a tail. `FxMissile` draws
  one as a little finned missile with an exhaust flame (DoomBot's forward air).
- `BurnFrames` + `BurnDamage` - sets whoever it hits on fire: small ticks of damage over the
  burn, flames on them, never stacking. A blocked hit does not burn. DoomBot's rocket boots and
  furnace.
- `LaunchAway` - launches away from the attacker on whichever side the victim is, for a blast
  in every direction.
- `Chargeable` on a **Dash** - it charges in place (with `ChargeWithSpecial`, while special is
  held) and only goes on release, up to `ChargeSize` times as fast. With `BallForm` and `FxFlame`
  he is curled up inside a fireball that swells with the charge and trails fire as it rolls -
  Flambe's Fireball Roll.
- `ComboNext` on **any** normal, not just the jab: press attack again during the move and the
  follow-up comes out as soon as the first hit is done. Flambe's tilts, dash attack and forward
  air are two-part this way. The speed check counts a combo string as one move.
- `ActiveFx` - what a hit is drawn as while live: `Sparks` (steel), `Electric` (bolts from the
  antennae to a hitbox overhead, or a crackling ring round one anywhere else), `Jets` (rocket
  flames out of both soles, wherever the feet are in the pose) and `Flame` (a burst of flame
  tongues round the hit). Electric also picks the `zap` sound.
- `BeamFrom` - a beam that comes out of a named spot on the drawing rather than the hands
  (`"points"` in rig.json - see `.ai/art-pipeline.md`). DoomBot's eye laser.
- `StretchArm` on a **normal** - the front arm shoots out to the hitbox for its active frames, a
  piston punch. On a tether recovery it is the tether.
- `SweepDegrees` + `AttackAnim.WideArc` - a real swing: the hitbox travels round an arc through
  the active frames (centred on `HitboxOffset`, round a pivot near the shoulders) and the arms and
  weapon follow it, with the trail growing along the arc. EdgeLord's Great Arc sweeps 150 degrees
  from behind him, over his head, to the front - wide and slow, so it covers a lot and is easy
  to punish.
- `HangFromArt` + `ReleaseDrop` - a swung-art recovery that ends up directly above the
  fighter, who hangs from its rope while it hauls him up, then lets go of it: it drops away as
  a `Drop` hazard onto whoever is below. Lug's wrecking ball.
- `PropArt` - a pose drawing put in the fighter's hand for one move, or `"-"` for an empty
  hand. How one fighter swings a katana, a claymore and a rapier without three rigs - and how
  Lug gets a shovel, a pickaxe and a jackhammer. **A move's reach should match its weapon**:
  check the hitbox against the weapon head in the attack parade.
- `ShotAngles` + `Mirrored` + `FromGround` - a projectile that fires several at once: one per
  angle, optionally mirrored to the other side, optionally rising up out of the floor under
  the fighter (only the part above the floor is drawn or hits). These work on **normals** too:
  EdgeLord's ring of daggers, the sword he sends up from behind him, and the axes of his down
  smash are all projectiles in normal-attack slots.
- `StretchLeg` - the front leg alone stretches out to the hitbox and points at it through the
  windup, then comes back. Circy's long kicks.
- `SpinVictim` - whoever it hits turns one somersault through their hitstun. Only the drawing
  turns. Flambe's crepe flip and Lug's traffic cone.
- `Armor` - through the windup and swing, a hit doing this much damage or less still counts but
  does not flinch: no knockback, no hitstun, a clang. Bigger hits and grabs get through. Lug's
  hard hat (8%, on his smashes and barge), which glints for exactly as long as it is on.
- `SlideFriction` - a `Trap` that is shoved along the floor instead of set down, skidding to a
  stop and dropping off any edge it slides over. Lug's traffic cone.
- `Walker` + `Burst` - a summon: a small copy of the fighter's own rig that walks along the floor,
  stops at an edge, and goes off as `Burst` when it touches someone or runs out of time. It never
  hits by itself. DoomBot's MiniBot.
- `PoseArts` - whole drawings of the fighter, one picked at random each time, shown in place of
  the puppet for the whole move. Triguy's three taunts (his taunt and his neutral special) and
  his crying.
- `InvincibleFrames` + `AuraArt` - nothing can hit or catch him for that many frames from the end
  of the startup, with that drawing standing behind him. Triguy's Show Off: keep the window short
  and the recovery after it long enough to punish - the counterplay is waiting it out.
- `Bounce` - a recovery on a trampoline: it appears under him (on the floor he hops up onto it),
  stops his fall through the startup, throws him up on the first active frame, and he steers
  after. Its hitbox bounces up anyone on it.
- `Topple` on a `FromGround` drawing - it comes up fast, stops dead standing on the floor,
  then falls over outward like a felled tree and is gone where it lands, in a smash of dust and a
  shake. Hits rising and with its head coming down. EdgeLord's axes. A drawing never fades out
  (traps included): it is there, then gone.
- `Corkscrew` - a dive: the whole puppet tips flat, head first, through the startup and spins
  along its length while live (`FighterRig.SetSpinWidth` narrowing the drawing to an edge and
  back, mirrored). With `ActiveFx.Flame` he is wrapped in fire. Flambe's Swan Dive dash attack.
- `DashAfterStartup` on a `Dash` - he stops and gets set through the startup and only goes on
  the first active frame, so it is telegraphed and does not run straight past someone standing
  next to him. Lug's wheelbarrow.
- `BeamFrom` on a plain projectile - it is fired from that named spot on the drawing, as a beam
  is. DoomBot's pocket missile, out of the socket in his chest.
- `CancelIntoAttacks` on a recovery - attack or special straight out of it once it is going, or
  down to drop out; it never leaves him spent. Flambe's wings.
- `RunningLegs` - the legs run the run cycle under the move's pose. Lug driving his wheelbarrow.
- `LooksBack` - his head turns round to look behind him through the move (`FighterRig.SetLookingBack`;
  a fighter whose face is drawn on his body turns the body drawing instead). Every back air, from
  the shared baseline (Eric's call, 2026-10-09).
- `HeldArtWheel` on a held drawing - pushed along on a wheel, held by its handles: the grip in his
  near hand, the wheel on the floor out ahead, tilting on it as his hands rise and fall, and his
  far hand reaching for the grip further along (`Fighter.PlaceHeldArt`). Lug's wheelbarrow, held
  by the handles at arm's length rather than by the middle (Eric's call, 2026-10-09).
- **Throws** (`Grabs.cs`, `FighterData.Throws`): four per fighter, forward, back, up, down, each
  built from the same things as the rest of that fighter's kit. Link hits hold the victim
  (`RehitFrames` + `LinkHit`); `FollowShot` fires a projectile after them.
- `SpecialKind.Puddle` - a puddle that falls to the floor, spreads both ways to its size or the
  floor's end, makes everyone else slip (`Fighter.Slip`), and sends `RainDrop` up out of it every
  `RainInterval` frames. Triguy's crying.
- `BlinkToTrap` - a blink that goes exactly to one of the fighter's own traps when one is ahead
  and within about 700 pixels, up or down, and pulls it out. EdgeLord's planted blades are
  anchors for his Blur Slash.

Some are **traits on `FighterData`**, because they are about the fighter rather than one move:

- `TumblesWhenHit` - a real hit knocks the fighter over into a ball with no arms or legs, which
  rolls until it gets back up and can roll off the edge. Control and both jumps come back when it
  ends, even off-stage, so it is dangerous without being a death sentence. Circy.
- `HasHeat` - heat builds as the fighter attacks, lands hits and (a little) takes them, and cools
  after two idle seconds. It shows: the drawing warms from grey to a dull red to orange, smokes,
  and pulses at full heat, and a thermometer sits beside their percent. A `Vent` move spends it.
  Sitting at full heat for five seconds overheats them instead - over a second stalled in steam
  with no control, keeping only a third of the heat. Numbers in `Heat.cs`. DoomBot's "overheats",
  and the loop his whole kit turns on: build it up, then pick the moment.
- `WhiffLagFrames` - extra frames stuck at the end of any swing or grab that met nobody, with a
  creak and a rusty tint. A hit, even a blocked one, costs nothing extra. DoomBot's "rusty
  joints": a heavy with long reach is only fair if missing with it is a real risk.
- `HotHitsFrom` - past this much heat, every normal the fighter lands also burns a little
  (`Heat.HotBurnFrames`), every swing bursts into flame, and the claws glow. DoomBot's glowing
  claws: a reason to sit at high heat other than the vent.
- `TelescopingArms` - an arm that reaches out (a piston punch, a command grab) slides out on
  steel rods with the forearm at its own size on the end, instead of stretching. DoomBot.
- `Robotic` - animated like a machine: he snaps from key pose to key pose - a few frames at one
  constant speed, starting and stopping dead - and holds each one perfectly still. Idle poses
  come every 12 frames with every joint on a whole 15 degrees, so they are sharp angles; attacks
  snap to the coil, the strike and back (`Fighter.RobotAttackKey`); the walk stops on each key
  pose and every footfall is a stomp, dust and a thud. Never jitter: an eased bob every few
  frames, beats that shortened as he walked faster and a camera jolt on every step all read as
  shaking, and are gone. DoomBot, "a factory robot that went rogue" (Eric's calls, 2026-10-04
  and 2026-10-09).
- `StickAimed` on a tether recovery - thrown wherever the stick points, from about 30 degrees
  above level on either side over the top, steerable through the startup. EdgeLord's Chain Blade.
- `ShouldersProp` - the weapon in his hand rides on his shoulder whenever he is on the ground
  and not swinging - standing, running, crouching, blocking, landing - and through any move
  marked `MoveData.CarryOnShoulder` (Lug's barge). `FighterAnimations.CarryOnShoulder` lays the
  arm and the weapon over whatever pose is playing; it is the one pose that turns the prop bone.
  Lug's sledgehammer (Eric's calls, 2026-10-08).
- `HandOnHip` - standing about, he plants one hand on his hip and holds the other fist up, leaning
  back with his chin up (`FighterAnimations.HandOnHip`, laid over the idle). Flambe, a hotshot
  (Eric's call, 2026-10-08). Only when he is just standing.
- `CanRoll` - false and block plus a direction on the ground does not roll; he stays in his
  block (and block at a ledge just climbs). DoomBot: a factory robot does not tumble across the
  floor (Eric's call, 2026-10-05). Spot dodge and air dodge are unchanged.
- `BlockLeak` - how much more than usual a block lets through. Triguy's 1.8: blocking is standing
  still, which he is terrible at.
- `AirDashSpeed` - the air jump becomes an eight-way dash: 14 frames, no gravity, then a glide.
  EdgeLord's super speed. The CPU aims it up and in when it recovers.

### Worked example: Circy

Circy, by Elim, is the first fighter built from a real sheet. Worth reading
`Specials.Circy()` and `FighterData.Circy()` as the pattern. Three calls a future sheet will
need again:

- **A direction clash.** The coolest move was "special with the stick up grows, down shrinks" -
  but up and down special are the recovery and the defence. Resolved by making it neutral
  special, held, then steered: the same gesture one beat later, and the recovery stays where the
  rules need it.
- **Low gravity hands jump height back.** "Falls slowly" and "terrible at jumping" fight each
  other, because jump height is v² / 2g. Solve the jump force for the height you want *after*
  setting gravity.
- **Weaknesses stack.** He is Medium, not Light, because rolling off the stage when hit and a
  short jump already cost him a lot. Floaty fighters still die early off the side and top - the
  calibration table has him dying to Lug about as early as Swift does - so watch his KO
  percents first if he plays too weak.
- **Two sizes should feel like two fighters.** At first tall and short only changed his beam and
  his speed a little, and nobody could tell. Now (`SizeLevels`) tall is 2.4 times the leg, slow
  to move and to swing, with big, hard normals; short is a ball on stubs, nearly half again as
  fast, with small, weak, quick ones. Normals are resized from the feet, so a low kick stays
  low at any height.
- **Stretching is his.** EdgeLord's sheet said "stretchy arms" too. A power two fighters share
  stops being either one's, so on 2026-10-04 EdgeLord's arm became a dagger on a chain and
  DoomBot's long arms telescope instead. Give a new fighter a different way to reach.
- **A ball bounces.** Knocked into a ball and dropped hard on the floor - or spiked into it - he
  bounces nearly half as high, squashing as he lands. While he is rolling, the stick leans on the
  roll a little either way but never stops it.

`CommandGrab` is the one archetype with a standing restriction: **a few, and rare.** There are
two: EdgeLord's Chain Blade and DoomBot's Claw Grab. The rule was "at most one" until Eric chose
a real grab for DoomBot over a blockable hook (2026-10-02). Universal grabs are still out of
scope (see `.ai/fighting-design.md`). A command grab is fine while it reads as that character's
identity rather than a mechanic everyone must learn, so each one has to look and play
differently: EdgeLord's is a recovery that throws down, DoomBot's is a slow, long ground reach
that is punished hard when it misses. A third should be a conversation, not a default.

### Worked example: DoomBot

Eric's sheet (`fighters/doombot/sheet.md`) asked for **three** strengths and **three**
weaknesses, and filled in most of the moveset himself. What that took:

- **Three strengths need three weaknesses that bite.** Strong and hard to knock over are just
  Heavy. Long reach is long hitboxes and a piston punch. Each weakness is a different kind of
  cost: big windups are paid on every move, rusty joints only when he misses, and overheating
  only when he is greedy with his heat.
- **The coolest move was in the down slot**, and it needed a resource. Heat became a reusable
  trait rather than a DoomBot special case, so a future fighter can have a meter too.
- **A full-heat vent first KO'd at 60%**, by far the strongest hit in the game. Tuned to 75%,
  with the burn on top, it stays the best thing he can do without being the only thing.

`Summon` deserves special attention: it is the slot where **his other drawings become content**.
A pet, a sidekick, a smaller monster he drew on the same page can be a summon, which means art
he made for fun finds its way into the game without needing a whole fighter built around it.

## The normal attacks

The thirteen non-special moves — a jab combo, three tilts, three smashes, a dash attack and five
aerials — do
**not** need bespoke design per character. Start every fighter from a **shared default moveset**
that is retuned rather than re-authored:

- a fast weak jab, a forward tilt with reach, an up tilt that pops people upward, a down tilt
  that is low and fast, a slow strong smash attack
- a quick neutral aerial, a strong forward aerial, a back aerial that is the reliable finisher,
  an up aerial for juggling, a down aerial that spikes

Retuning means adjusting damage, frame counts, knockback and reach to match the stat block — a
heavy fighter's jab is slower and hits harder — and swapping the animation for one that suits
the character. It does not mean designing eleven moves from scratch, which is how a character
takes a month instead of an afternoon.

**A big fighter's hitboxes have to come down to everyone else.** DoomBot is drawn 1.75 times
everyone's size and his moves grow with him, which put his piston punch and his grab at his own
chest height - clean over the head of anyone normal-sized. Aim a giant's mid-height moves at the
middle of an ordinary fighter (a command grab's hands go out and down to 70 pixels off the
floor), and check with the attack parade and a match, not the KO table, which ignores height.

**A taunt never does anything.** It is a pose and a line, nothing else: no hit, no summon, no
buff, no heat. When a fun idea needs a home, it goes in a move slot - DoomBot's MiniBot is his
down tilt and Lug's traffic cone is his, not their taunts.

## Working with him on this

- **Build the signature move first and show it to him early**, before the rest of the kit
  exists. Seeing his fireball come out of his drawing is the payoff that keeps him drawing, and
  it is worth reordering work to get there sooner.
- **When an idea is mechanically impossible, find the nearest version that works and name it
  as his.** "He can turn invisible and never be hit" becomes a down special that grants a few
  frames of invulnerability. The answer is never a flat no, and it is never quietly replacing
  his idea with ours and calling it his.
- **The sheet asks for descriptions of the specials, not names.** What a move does and looks
  like is what we build from; a blank row of name slots is one more thing to fill in before a
  kid can send the sheet. We name each special from his description, taking any name he used
  in it, and he can rename it whenever he likes.
- **Show him the numbers.** A 10-12 year old can absolutely understand "this one does 12 damage
  and this one does 18 but it is slower". Letting him turn the knobs in a `.tres` file is a
  genuinely good way to teach him what balance is.
