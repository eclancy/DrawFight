# Project overview

A local-multiplayer platform fighter — Super Smash Bros. rules — where every fighter is a
drawing made by a real kid, cut up and puppeted so it moves.

## The pitch

My nephew draws a character on paper. We photograph it, slice it into body parts, hang those
parts on a shared skeleton, and it walks, jumps, and punches. Then we read his description of
the character and build it a moveset. Two to four of these drawings fight on a floating stage
until somebody gets launched off the screen.

The point is not that it is a good fighting game. The point is that he sees *his own drawing*
moving, and that the game gets better every time he draws something.

## Who it is for

One kid, age 10-12, plus whoever is on the couch. He is old enough for a near-complete Smash
control scheme — directional attacks, aerials, four specials, block, dodge — and old enough to
notice and complain when a character is unfair. That means real movesets and real balance
passes, not a two-button toy.

## The five pillars

1. **His linework survives.** No redrawing, no cleaning up, no "improving" his art. If a limb
   is lumpy, the limb is lumpy in the game. The whole emotional payload of this project is that
   it is unmistakably *his drawing*. Fill in what is missing; never replace what is there.

2. **The fighting has to feel good before any art exists.** A drawing flopping around in a
   game that feels bad is a worse experience than no game. Milestone 1 is white boxes with
   excellent knockback. Art comes after.

3. **One skeleton, shared by everyone.** Every fighter rigs to the same bone hierarchy, so
   every fighter inherits the same animation library for free. This is the decision that makes
   the project sustainable: adding a character is an afternoon of cutting up a photo, not a
   week of animating.

4. **Adding a fighter must be cheap enough to do on a whim.** If a new drawing takes a weekend
   to get in the game, he will draw three characters and stop. If it takes twenty minutes, he
   will keep drawing forever, and *that* is the actual win condition of this project.

5. **His constraints, not mine.** His description decides the moveset. His weakness answer
   decides the stats. When his idea is mechanically bad, we find the nearest version that works
   rather than substituting our own idea and telling him it was his.

## Scope

**In, eventually:** local versus for 2-4 players, CPU opponents, stocks and percent, a handful
of stages, a character-select screen, the in-editor import tool, as many fighters as he draws.

**Out, and staying out unless something changes:** online play (fighting-game netcode is a
project of its own), story mode, unlockables and progression, mobile, controller remapping UI
beyond a basic screen, anything resembling monetisation.

**The name is not a placeholder.** `Dusk` is his online handle, so `DuskFight` is named after
him. Do not propose renaming it.

## Engine and house style

Godot 4.5.1 Mono (.NET 9), C#. This mirrors the sibling project `WizardSurvivors` deliberately —
same engine version, same data-driven `.tres` Resource pattern, same flat `scripts/` and
`scenes/` layout, same `.ai/` design-doc convention. Anything unstated here should default to
however that project does it.

See `.ai/fighting-design.md` for the combat contract, `.ai/art-pipeline.md` for how a photo
becomes a rigged fighter, `.ai/character-design.md` for how a description becomes a moveset,
and `.ai/roadmap.md` for the build order.
