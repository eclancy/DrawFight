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

This is why the ten normal attacks are authored **once, at medium**, in `DefaultMoveset.cs` and
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
- **Down special** — defence or utility. A counter, a trap, a reflector, a stance.

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

Built so far: `Projectile`, `Dash`, `Recovery`, `Trap`, `Drop` (as `SpecialKind.Drop`, a falling
spike), `Resize` and `Bomb`. The rest are designs, not code.

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

And one is a **trait on `FighterData`**: `TumblesWhenHit`. A real hit knocks the fighter over into
a ball with no arms or legs, which rolls until it gets back up and can roll off the edge. Control
and both jumps come back when it ends, even off-stage, so it is dangerous without being a death
sentence.

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

`CommandGrab` is the one archetype with a standing restriction: **at most one fighter in the
roster may have one.** Universal grabs are out of scope (see `.ai/fighting-design.md`), and the
reason a single command grab is still fine is that it reads as that character's identity rather
than as a mechanic everyone must learn. Two of them and it is a mechanic again.

`Summon` deserves special attention: it is the slot where **his other drawings become content**.
A pet, a sidekick, a smaller monster he drew on the same page can be a summon, which means art
he made for fun finds its way into the game without needing a whole fighter built around it.

## The normal attacks

The ten non-special moves — five grounded, five aerial — do
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
