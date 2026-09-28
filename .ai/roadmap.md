# Roadmap

Build order. No deadline, so the ordering optimises for *never building on a bad foundation*
and for getting something he can play with his own drawing in it as early as that allows.

The governing principle: **M1 before any art.** If the fighting does not feel good with white
boxes, it will not feel good with his drawings, and by then the art will be sunk cost arguing
against fixing the physics.

---

## M0 — Scaffold

Godot 4.5.1 Mono project, C#, flat `scripts/` and `scenes/`, `.tres` resources at the repo root,
matching the `WizardSurvivors` layout. `.claude/settings.json` with `GODOT_BIN`. A `CLAUDE.md`
pointing at `.ai/`. `dotnet build` green on an empty project.

## M1 — The fight feels good *(the make-or-break milestone)*

White rectangles, one flat platform, two players on keyboard. No art, no menus, no characters.

- fixed 60 Hz `_PhysicsProcess` loop, everything in frames
- platform-fighter movement: run, jump, double jump, fast-fall, strong air control
- the knockback formula, derived hitstun, percent tracking
- hitboxes and hurtboxes, one placeholder attack
- blast zones, stocks, respawn
- **hitlag, screen shake, hit sparks** — these are part of this milestone, not polish later
- coyote time, jump buffering, input buffering
- a debug overlay: percent, knockback dealt, hitbox visualisation, frame counter

**Exit criterion:** two people can play with rectangles for twenty minutes and it is fun.
Not "it works" — fun. If it is not, stay here. Everything downstream is worthless otherwise.

## M2 — The skeleton and the animation library

- the shared `Skeleton2D` bone hierarchy from `.ai/art-pipeline.md`
- the full animation set authored once against it: idle, walk, run, jump, fall, land, block,
  dodge, hurt, tumble, and a pose for every move slot
- one test fighter, **cut up by hand** rather than by the tool, to prove the rig before
  investing in tooling

**Status: built, except for the part that needs him.** The skeleton, the animation library, the
rig loader and the parade inspection view all exist and run, driven by two *generated* stick
figures (`fighters/README.md`). What is still outstanding is the only thing that actually proves
the pipeline: a rig built from a real photographed drawing. Everything downstream is ready for
it.
- add **blocking and dodging** here, not in M1 — blocking is two multipliers on the damage
  pipeline (see `.ai/fighting-design.md`) and is not needed to answer the question M1 exists to
  answer

**This is the first milestone that needs a real drawing from him.** One is enough.

## M3 — The import tool

The `EditorPlugin` dock, with **two input paths** (see `.ai/art-pipeline.md`):

- **Layered art** from a tablet, one body part per layer, exported as separate PNGs. Skips
  background removal and cutting entirely, and the layer positions give the bone offsets for
  free. Build this path first - it is both simpler and better, and the drawing guide now
  steers tablet users toward it.
- **A flat image**, photographed or flattened: threshold the background, box the parts, place
  the pivots by hand.

Both converge on the same generate step. Re-import must preserve hand-tuned stats.

Built *after* M2 because M2 teaches us what the tool actually needs to produce. Building the
tool first means building it against a guess.

## M4 — Movesets as data

- `MoveData` and `FighterData` `.tres` resources
- the shared default moveset that every fighter starts from
- the special archetype scripts: `Projectile`, `Dash`, `Counter`, `Recovery` first — those four
  cover most of what a kid describes
- **two real fighters** built end to end from his drawings and his character sheets

**Exit criterion:** he plays as his own drawing, using his own signature move, against another
of his own drawings.

## M5 — A game around the fight

- ~~the stage proper~~ **done early**: `StageData` + `StageCatalog` + one renderer, and three
  stages (Fridge Door, Open Plains, City Rooftops). See `.ai/art-direction.md`. Grabbable
  ledges are still outstanding.
- HUD: portraits, percent, stock icons
- ~~title, character select, stage select~~ **done early.** The title animates two real rigs
  through the shared animation library rather than a bespoke title animation, so it improves on
  its own whenever the animations do or a real drawing lands. Results screen still outstanding.
- a training mode with infinite stocks and hitbox display — this doubles as the debug tool and
  as how he learns his characters

## M6 — Three and four players

Mostly camera and stage sizing plus gamepad handling, but it changes how the stage should be
built, so it wants to land before there are many stages.

## M7 — CPU opponents

So he can play alone. A state-machine AI at "fun for a kid" quality, with a difficulty setting
— which mostly means reaction delay and how often it chooses to do nothing, not perfect play
with a handicap.

## M8 — More

More fighters, more stages, whatever he keeps drawing. The point of M3 and M4 is that this
milestone never really ends and never costs much.

---

## Deliberately deferred

- **Online play.** Fighting-game netcode is its own project. The fixed-frame architecture from
  M1 leaves the door open; nothing else should assume it.
- **Non-humanoid fighters.** Build the single-sprite path when he actually draws one, not
  speculatively.
- **Items.** Fun, chaotic, kid-friendly, and a natural home for his smaller drawings. A good
  first thing to add after M5 if he wants more chaos.

## What is on the critical path right now

Nothing in this repo — **it is the drawings.** M1 can be built in parallel with him drawing,
but M2 onward blocks on having at least one good photograph of one good drawing. Send him
`for-nephew/how-to-draw-a-fighter.md` and `for-nephew/character-sheet.md` first; the quality of
that first drawing sets the ceiling on how well the whole pipeline works.
