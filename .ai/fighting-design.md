# Fighting design

The combat contract. Read before touching movement, hitboxes, knockback, or any move's numbers.

## Rules of a match

Smash rules, not health bars.

- Each fighter has a **percent** starting at 0, which only ever goes up during a stock.
- Getting hit adds damage to your percent and launches you. **The higher your percent, the
  farther you fly from the same hit.**
- You lose a **stock** (a life) by crossing a **blast zone** — the boundary past the top,
  bottom, left, or right of the stage. Not by running out of HP; there is no HP.
- Losing a stock resets your percent to 0 and respawns you on a platform above the stage,
  briefly invulnerable.
- Default match: 3 stocks, no time limit. Last fighter standing wins.

Why this and not health bars: percent-based knockback is enormously forgiving. Nobody dies to
a single mistake at low percent, comebacks happen constantly, and the dramatic moment of a
match is a launch, not a number hitting zero. For kids playing each other this matters — a
match should stay tense for everyone, including whoever is losing.

## Everything is measured in frames

The game logic runs at a **fixed 60 Hz in `_PhysicsProcess`**. Every duration in move data is
an integer frame count, never a float in seconds. Startup, active, endlag, hitstun, hitlag,
invulnerability, buffer windows — all frames.

This is not fussiness. Frame-exact logic is what makes a fighting game's moves feel consistent
and learnable, it makes balance discussions concrete ("it is 4 frames too fast" beats "it feels
fast"), and it is the only version of this that could ever be made deterministic if online play
ever happens. Retrofitting it later means rewriting every move.

## The knockback formula

This single formula is most of what makes the game feel like Smash. A simplified form of the
Smash Ultimate equation:

```
knockback = ((p / 10 + (p * d) / 20) * (200 / (w + 100)) * 1.4 + 18) * s + b
```

| symbol | meaning                                                          |
|--------|------------------------------------------------------------------|
| `p`    | victim's percent, **after** this hit's damage is applied          |
| `d`    | damage dealt by the move                                          |
| `w`    | victim's weight; 100 is average, higher flies less                |
| `s`    | the move's knockback growth — how hard it scales with percent     |
| `b`    | the move's base knockback — how hard it hits at 0%                |

The result becomes launch speed, applied at the move's **launch angle**.

The two knobs that define a move's character are `b` and `s`:

- **High base, low growth** — a reliable poke. Always pushes people away a fixed useful amount,
  never kills. Good for spacing and combo starters.
- **Low base, high growth** — a finisher. Does almost nothing at low percent and sends people
  to the moon at high percent. This is what a match ends on.
- High base *and* high growth is a move that is always the correct choice. Do not ship one.

**Hitstun** — how long the victim is helpless after being hit — is derived, not authored:

```
hitstunFrames = knockback * 0.25
```

Deriving it is what makes combos emerge naturally instead of being scripted. A weak hit gives
few enough frames that the victim recovers first; a strong hit gives enough that the attacker
can follow up. Nobody designs the combos; the formula does.

The coefficient is `0.25` rather than Smash's `0.4` because the knockback values this
formula produces run higher than Smash's do — at `0.4` a 7-damage jab froze the victim for 38
frames, which is a jab that combos into itself forever. Treat it as a calibration constant
paired with `LaunchSpeedPerKnockback`, not as a number copied from another game.

## Checking the numbers without playing

`RegressionChecks.RunAll()` runs at startup and prints, for each move against each fighter,
**the percent at which it starts KOing** — by simulating the launch, not by estimating a
distance.

It flies the victim from the edge of the stage under the real numbers: launch velocity decays
horizontally during hitstun, gravity pulls the whole time, air drag takes over afterwards. A
launch counts as a KO only if it carries the victim clean off the **side or the top**. Landing
back on the stage counts as survived, and so does falling below it — every hit eventually
pushes someone off the bottom if you simulate long enough and never let them recover, so
counting that made every move read as a 0% kill and told us nothing.

**Read it before and after changing any knockback number, and after changing gravity, fall
speed or stage size.** Two traps it exists to catch:

- Launch distance scales with the *square* of knockback, so a change that looks small in the
  move data is not small on the stage. The first pass of these placeholder moves killed at 50%.
- **How far a launch carries depends on how long the victim stays in the air.** When gravity
  nearly doubled to make the game less floaty, every KO percent moved up by roughly a third
  — the knockback numbers had not been touched at all. An earlier version of this table
  estimated horizontal distance and ignored gravity, so it printed identical numbers before and
  after that change. A number that does not move when the thing it measures does is worse than
  no number.

Current placeholder calibration: the jab effectively never KOs (230% and 295%), the heavy swing
KOs the light fighter at 110% and the heavy one at 145%, and the two dash attacks land at 85%
and 205%.

## Game feel: the part that is easy to skip and must not be

A correct knockback formula with none of the following feels like a physics demo. These are not
polish, they are the feature:

- **Hitlag (freeze frames).** On a connecting hit, *both* fighters freeze for
  `2 + damage * 0.3` frames while everything else keeps moving. This is the single largest
  contributor to a hit feeling like it has weight. Do not skip it. It was `3 + damage * 0.5`,
  which made every exchange stop dead; shorter lets people throw out an attack and keep moving.
- **Weaker moves are quicker.** Within a fighter, a move that does clearly less damage must be
  quicker to throw again (startup to the end of endlag) than one that does more, or nothing
  separates a poke from a finisher. `RegressionChecks.CheckWeakerIsFaster` warns on any pair that
  breaks it and prints every fighter's moves by length.
- **Screen shake** proportional to knockback, capped so that a big hit is dramatic and a jab
  is not nauseating.
- **Hit sparks** drawn at the exact contact point, scaled by damage.
- **Launch trails** on high-knockback hits, and a **KO blast** when someone crosses a blast
  zone: a huge burst in their colour at the edge of the screen where they went out, with rays
  firing back in across the stage. It is pinned to the world where they went out, not to the
  screen. It used to ride with the camera as the camera swung back to whoever was left, and
  that read as the explosion chasing the action rather than happening somewhere. The KO'd fighter then
  stays gone for 80 frames (`Tuning.RespawnDelayFrames`) before reappearing, so the blast has
  the screen to itself and everyone takes in what happened.
- **Dodges roll.** A roll or a directional air dodge curls the fighter up and turns a full
  somersault the way it travels; a spot dodge curls up in place.
- **Camera** that smoothly frames all living fighters, with a minimum zoom so two players
  standing together do not fill the screen, and a maximum so a far-flung player stays visible.
- **Telegraphs** for anything fast and strong. A move that crosses the stage in a few frames
  must spend its startup showing that it is coming, or it is unfair: EdgeLord's Blur Slash
  glints and flashes for 22 frames, then goes in 5.

**Burning** is the one damage-over-time effect: a move with `BurnFrames` sets whoever it hits
on fire, adding its `BurnDamage` in small ticks every quarter-second with flames licking off
them. It never stacks - burning again only tops it back up - and a blocked hit does not burn.
It adds percent, never knockback, so it softens someone up for the next hit rather than killing
them by itself.

**Telegraphs have a sound as well as a look**, because a warning you can hear works even when
you are not looking at the right fighter. Blur Slash's glint has a ring that rises through it,
peaks on the frame he goes, and is cut off sharply as he goes. See `.ai/audio-direction.md`.

## Movement

Platform-fighter movement — more controllable than a normal platformer, but deliberately **not**
floaty. An earlier pass was, and it made the game feel slow: jumps hung, top speed was low, and
acceleration was gentle enough that top speed barely mattered. Gravity, jump force, run speed
and acceleration all went up together. Raising top speed alone does nothing, because a fighter
that takes half a second to reach it never gets there in a fight.

- **Falling is gentler than rising.** Going up is full gravity, so jumps keep their height.
  Coming down, gravity starts at 45% and builds to full over 26 frames, and the top fall speed
  is 80% of each fighter's `MaxFallSpeed` (`Tuning.FallSpeedScale` and friends). The top of a
  jump hangs a moment, then speeds up, so falling never feels like being yanked down.
- **Gravity is 80% of every fighter's own number** (`Tuning.GravityScale`), so everyone hangs in
  the air a little longer and a fighter knocked off the stage has more time to get back. Jumps
  and launches are slowed by its square root (`Tuning.JumpScale`) and launch slowdown by the
  scale itself, so every jump reaches the same height and every launch flies the same path as
  before - the same KO percents, give or take 5% - only about 12% more slowly.
- **Feet stay on the floor.** On the ground, after every pose, the body is moved so the lowest
  point of the legs rests exactly on the floor (`FighterRig.SetPlanted`): a pose never floats a
  fighter or sinks him, and bending the legs really lowers him. The floor answers back with dust:
  a puff on landing (bigger the harder he came down), a little kicked back off each running step,
  and a cloud skidded out in front when he turns hard at speed.
- Ground: accelerate to a run speed, with a distinct initial-dash speed.
- **Two jumps** for everyone: the ground jump and one air jump. The air jump is refreshed on
  landing, on grabbing a ledge, and **on being hit** - a fighter knocked off the stage always
  has a jump to get back with once hitstun ends, even if they had spent it before the hit. A
  buffered jump press fires on the first actionable frame, so mashing jump during a launch works.
  The air jump goes about 10% higher than the ground jump.
- **EdgeLord air-dashes instead** (`FighterData.AirDashSpeed`): his air jump is a burst of 1500
  pixels a second for 14 frames, along whichever of eight directions the stick points (straight
  ahead if it is centred), with no gravity until it ends and a third of the speed carried after.
  It is still the one air jump - the same refreshes, the same once per trip - and attacking out
  of it keeps half the speed. Straight up it gains about what a jump does; diagonally it trades
  height for distance, which is the point.
- **A built platform** (Lug's girder, `SpecialKind.BuildPlatform`) is a soft platform you can
  stand on and jump off, but it is not the ground: standing on it gives back no air jump, no up
  special and no second build. Only real ground or a ledge does - otherwise build, jump, build
  would be a recovery that never ends.
- **Up special once per trip into the air.** Landing or grabbing a ledge gives it back; being
  hit does not. A second press in the air does nothing — it is discarded, not buffered, so it
  cannot come out as a neutral special when the stick leaves up.
- **Fast-fall**: tapping down while falling increases fall speed. Free expressiveness.
- **Air control** is strong — you can meaningfully steer your own trajectory mid-launch.
- **Directional influence (DI)**: holding a direction while in hitstun slightly angles your
  launch trajectory. This is how a good player survives a hit that should have killed them, and
  it rewards him for learning something real.
- **Ledges** can be grabbed, with a generous snap radius. Recovering from off-stage should feel
  possible, not punishing.
- **Soft platforms can be dropped through** by holding down and pressing jump.
- **Running into an attack gives a dash attack** rather than the standing jab.

Input forgiveness, all of it non-negotiable:

- **Coyote time** — ~5 frames of still being able to jump after walking off a ledge.
- **Jump buffering** — a jump pressed up to ~6 frames before landing fires on landing.
- **Input buffering** — an attack pressed during endlag fires when the move ends.

## Controls (age 10-12 target)

Xbox pads are the primary input. Keyboard exists so that development and a missing-controller
match both work, not as the intended way to play.

| Xbox pad           | result                                     |
|--------------------|--------------------------------------------|
| left stick / d-pad | move, fast-fall, DI                        |
| **A** or **Y**     | jump, double jump                          |
| **X**              | attack — neutral, or `←` `→` `↑` `↓` tilts |
| **X** in air       | aerials — neutral, forward, back, up, down |
| **X** while running| dash attack                                |
| **d-pad** + **X**  | smash attack (hold X to charge)            |
| **B**              | special — neutral, or `←` `→` `↑` `↓`      |
| **LT** or **RT**   | block — reduces damage and knockback       |
| **LT/RT** + stick  | dodge roll / spot dodge / air dodge        |
| **LB** or **RB**   | taunt (on the ground)                      |
| hold `↓` + **A**    | drop through a soft platform               |
| Start              | pause: resume, or back to fighter select   |

**This layout was specified, and it wins.** Earlier drafts argued first for platformer
convention and then for Smash Ultimate’s layout; both are now overruled. `GamepadLayout.Smash()`
survives only as an F3-swappable alternate for anyone whose hands already know that game.

The right trigger also blocks, as a bonus rather than a replacement — a kid who grabs for a
shoulder button under pressure should get a block rather than nothing.

Two details that are not optional on a pad:

- **The stick deadzone is radial, applied to the stick's magnitude, never per-axis.** A per-axis
  deadzone carves a square hole out of a round stick and makes diagonals unreliable, which
  matters here because diagonals are how directional attacks and DI are aimed. Past the
  deadzone the magnitude is rescaled from zero rather than snapping to the deadzone value.
- **The stick is a speed dial, not a switch.** Target speed is stick deflection times top
  speed, so a light push walks and a full push runs with everything in between. Two numbers
  protect that: the pad deadzone is small (0.15) and the movement threshold is smaller (0.08),
  because every degree of stick travel swallowed by either is walking speed the player cannot
  reach. Preserving magnitude also gives DI something finer than eight directions.

**Rumble is part of the feel budget, not a nicety.** It is hitlag you can hold, and it is the
cheapest large win available on a pad. The victim's pad gets intensity scaled by knockback
(maxing out around 150, roughly kill range) and the attacker's gets about a third of that, so a
hit confirms on both sides without the two buzzes competing. A KO gets the strongest rumble in
the game.

Keyboard fallback: P1 is the arrow keys + `A` jump, `Q` attack, `W` special, `S` block. P2 is
all number pad: `8` `4` `5` `6` to move, `1` jump, `2` attack, `3` special, `0` block.

## Dropping through platforms

Holding down and pressing jump falls through a soft platform. It is implemented by collision
layer rather than by any special case in the platform: **solid ground is layer 1, soft platforms
are layer 2**, and a drop-through masks layer 2 off for 12 frames. The same input on solid ground
therefore does nothing at all, rather than dropping a fighter out of the bottom of the stage.

It only fires when the surface underfoot is actually a soft platform, read from the slide
collisions that `MoveAndSlide` already computed. Pressing down and jump on solid ground gives a
normal jump, which is what a player expects.

## Dodging

Block plus a direction **rolls**, block on its own **spot-dodges**, and block in the air is an
**air dodge**. All three are vulnerable at the edges and invulnerable only in the middle
(roughly frames 4-17), which is what makes a dodge a read rather than a panic button: mistime it
and you are hit during the startup or the recovery.

## Ledges

Solid platform corners inside the blast zone are **grabbable**, with a generous snap radius,
because recovering from off-stage should feel possible rather than punishing. Grabbing refreshes
the air jump and grants a moment of invulnerability.

Which corners qualify is derived rather than authored: soft platforms are excluded because you
pass through them, and corners outside the blast zone are excluded because no fighter can be
there. **Open Plains therefore has no ledges at all without anything having to say so** - its
floor runs past both blast zones, so both corners fall outside and there is simply nothing to
grab. A stage gets ledges by having edges.

From a hang: up or toward the stage climbs back on, jump climbs on with height, and down or away
lets go. A short re-grab cooldown stops a fighter bouncing back onto the same ledge forever.

## Dash attacks

Pressing attack while running gives a different move from the standing jab. It is selected by
**actual speed** (above 55% of top speed) rather than by whether a direction is held, so a
fighter who has turned around and is still sliding backwards does not get one by accident.

A dash attack sets `CarriesMomentum`, which does two things: it sheds far less speed during the
move, so the attack slides the whole way through instead of stopping dead on startup, and it
selects the lunging attack pose so the move looks like a committed charge rather than a jab that
happens to be moving. A dash attack that stops on contact is just a slow jab.

**Nothing started on the ground slides you off it.** Any move begun on the ground - a dash
attack, a charge, a wheelbarrow - stops dead at the edge rather than carrying the fighter off
(`Fighter.StopAtLedge`). Sliding off the stage mid-attack is the game killing you, not the other
player. The one exception is a blink (EdgeLord's Blur Slash), whose whole point is crossing a gap.

## Jab combos

Tapping attack strings the jab combo, whose length depends on weight: three quick hits for a
light, three ending in a pop-up for a medium, two slow heavy punches for a heavy (see
`.ai/character-design.md`). Pressing attack during a move with a
`ComboNext` queues the next hit, and it comes out **the moment the current hitbox ends**, not
after its endlag — that is what turns taps into a fast string. The first two hits have low base
knockback and very low growth (about 20 and 0.25), so the target stays in reach and in hitstun
for the next; only the finisher pushes them away. It is a real combo: the target cannot act
between the hits. Stopping after one jab is still a quick poke.

## Crouching

Holding down on the ground crouches: no walking (the stick still turns you round), and the
hurtbox shrinks to the lower 55% of the body, so a jab or a tilt aimed at the chest passes over.
The crouch pose folds the body well forward over deeply bent knees, and with planted feet that is
what drops the body: a crouching fighter is visibly about half height, matching the hurtbox. Attacking from a
crouch is the down tilt, and **every fighter's down tilt works along the floor**: a sweep, or
something sent along it - Lug's traffic cone, kicked out to trip people, and DoomBot's MiniBot,
set down to walk at them. Never a move that hits high.
Down plus jump on a soft platform is still a drop-through, checked before crouching.

## Tilts and smashes

One attack button, told apart by the stick, as in Smash:

- **Tilt**: a direction already held, then attack.
- **Smash**: the stick **flicked** from centre to full within 4 frames of pressing attack. On a
  keyboard, pressing the arrow and attack together is a flick.

Smashes are the grounded finishers — slow to start, long to recover, so a miss is punished.
**Holding attack charges** one: it freezes 3 frames before the hit for up to 60 frames, for up to
40% more damage (knockback follows damage), and the fighter glows warmer the longer it is held
so the other player can see it coming. Uncharged, a medium's forward smash KOs a light fighter
from about 90%; read the calibration table before changing any smash number.

That is roughly **19 moves per fighter**: a 3-hit jab combo, 3 tilts, 3 smashes, a dash attack,
5 aerials, 4 specials, plus
block and dodge. It sounds like a lot, but most of them are shared behaviour driven by data —
see `.ai/character-design.md`.

**Grabs and throws are out of scope. This is settled, not deferred** — do not design around
them or leave hooks for them. They would add a whole mechanic layer (grab, pummel, four
directional throws, grab release, throw-based combos) for every fighter, and the block-grab
interaction they exist to create is not worth that cost here.

The `CommandGrab` special archetype is a separate matter and stays available — a single
fighter whose signature move is a grab is fine, because it is that character's identity rather
than a universal mechanic.

## Blocking reduces; it does not negate

Removing grabs removes Smash's answer to a turtle, so blocking cannot work the way Smash's
shield works. **A block reduces damage and knockback rather than cancelling them.** A blocked
hit still lands:

```
blocked damage    = damage    * 0.30
blocked knockback = knockback * 0.20
```

This is not a compromise version of a shield — it is a smaller system that produces a better
game here, and it is the reason the whole shield-health subsystem is out of scope.

**The cost of blocking is paid in the mechanic the game already has.** Chip damage raises your
percent, and a higher percent means the next clean hit launches you further. A player who
blocks everything does not win slowly; they lose slowly. There is no separate shield-health
resource to deplete, no regeneration rate, no shield-break stun state, no shrinking shield and
therefore no shield-poking edge cases, and no second balance knob that can be set wrong. The
percent system does all of it.

**The second cost is positional.** Blocked knockback is reduced, not removed, so blocking
still slides you backwards. Turtle near the edge and you push yourself off the stage. This is
extremely readable — a 10-12 year old will work out "do not block on the ledge" by dying to it
twice, which is the good kind of learning.

Supporting rules, each a single number rather than a system:

- You cannot attack or move while blocking.
- **Block-release lag**: ~5 frames after releasing block before you can act, so block-hit-block
  is not free.
- **Blockstun pushback** applies to the attacker too, so a blocked attack at point blank
  separates both fighters instead of stalemating.
- Blocking still holds up against **projectiles**, and chip from a projectile is low enough
  that a zoner cannot chip someone to death from across the stage. The answer to a zoner is to
  approach, which is what the `Dash` archetype exists for.

Keeping a block button at all — rather than making all defence movement-based — is a
deliberate concession to the audience. A kid under pressure wants something to hold, and
"press this and it hurts less" is the most intuitive defensive mechanic there is.

`MoveData` keeps a per-move `Unblockable` flag for the rare special that should ignore all of
this. Use it sparingly; it is the kind of thing one character has.

**Armour is the other one-character thing** (`MoveData.Armor`). Through a move's windup and
swing, any hit of that much damage or less still adds its percent but does not flinch him - no
knockback, no hitstun, just a short freeze and a clang - so a jab cannot stop Lug's
sledgehammer. It is not a shield: the damage counts, a real hit (more than 8%) still stops him,
a grab still takes him, and the hard hat that shows it is on comes off in the endlag, where he
is as open as anyone.

## Moves are data, not code

A move is a `MoveData` `.tres` Resource, following the same pattern as `SpellData` in
`WizardSurvivors`. It carries:

- frame counts: startup, active, endlag, plus landing lag for aerials
- one or more **hitboxes**, each with a shape, an offset, the frames it is live, damage,
  base knockback, knockback growth, and launch angle
- the animation name to play on the shared skeleton
- optional FX art and sound
- flags: whether it is `Unblockable`, whether it spikes, whether it is a projectile spawner

A fighter's whole moveset is a list of these. **New content is authored as resources, not as
new classes**, exactly as spells are in the sibling project. The exception is a special whose
behaviour genuinely does not exist yet — that is a new archetype script, and archetypes are
meant to be rare and reusable. See `.ai/character-design.md`.

## Stats that define a fighter

Each `FighterData` carries a small stat block. These numbers, not the moveset, are most of what
makes two fighters feel different:

`Weight` · `RunSpeed` · `InitialDashSpeed` · `AirSpeed` · `AirAcceleration` · `JumpForce` ·
`DoubleJumpForce` · `FallSpeed` · `FastFallSpeed` · `VisualScale`

A heavy fighter survives to high percent but is easy to hit and slow to recover. A light fast
one dies early but controls the pace. **Every fighter must be clearly bad at something**, and
the honest place to enforce that is here in the stat block — see the weakness rule in
`.ai/character-design.md`.

## Balance philosophy

He is 10-12. He will try to make every character the strongest, he will find whatever is
broken, and he will play the broken thing exclusively. Plan for it:

- Build a **training mode early** — infinite stocks, damage readout, hitbox visualisation.
  It doubles as the debugging tool and it is how he learns his own characters.
- When he finds something broken, treat it as a discovery to celebrate and then tune, not as
  a bug report. Getting him into the loop of *finding* imbalance is worth more than the balance.
- Tune numbers in `.tres` files so a balance change never needs a rebuild.
