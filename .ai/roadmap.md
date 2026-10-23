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

**Status: built.** The skeleton, the animation library, the rig loader and the parade inspection
view all exist and run. The real-drawing proof is **Circy**, drawn by Elim and cut by
`tools/art/cut_circy.py`. It taught two things the importer (M3) must handle:

- **Kids draw whole poses, not just a fighter.** Elim sent ten drawings: the fighter, tall and
  small versions, a bomb sequence, an explosion, a laser and the pieces of a grappling hook. The
  rig carries these as **held poses** (shown instead of the puppet while a move holds still) and
  **effect art** on `MoveData.FxTexture`. The importer needs a place to put both.
- **Digital drawings arrive on a transparent canvas**, which makes cutting mechanical: the body
  is the filled shape, every other stroke is a limb. Thin digital lines survive the cut but read
  faint at match size.
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

The game's own drawing screen already produces the layered input in exactly this shape - whole-page
see-through PNGs, one named body part per layer - and sends it through the same intake as the
website (`.ai/art-pipeline.md`, "Drawn in the game").

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
- ~~title, main menu, character select, stage select~~ **done early.** The title animates two real rigs
  through the shared animation library rather than a bespoke title animation, so it improves on
  its own whenever the animations do or a real drawing lands. Results screen still outstanding.
- a training mode with infinite stocks and hitbox display — this doubles as the debug tool and
  as how he learns his characters

## M6 — Three and four players

Mostly camera and stage sizing plus gamepad handling, but it changes how the stage should be
built, so it wants to land before there are many stages.

**Status: built (2026-10-16).** Four seats on fighter select, any of them the computer; two or
more start a fight. Seats keep their number, colour and controller into the match (the keyboard
has room for two; seats three and four need pads). Tags over heads in fights of three or more.
Three big stages made for it - Playground, Building Site, Kitchen Counter - each with its own
camera range (`StageData.CameraMinZoom`). Teams too: red, blue and green, no friendly fire, picked
per seat on fighter select.

## M7 — CPU opponents

So he can play alone. A state-machine AI at "fun for a kid" quality, with a difficulty setting
— which mostly means reaction delay and how often it chooses to do nothing, not perfect play
with a handicap.

**Status: a basic CPU is built** — `CpuInputSource`, an `IInputSource` like a pad, so it plays
by exactly the same frame windows as a person. It decides every ~20 frames, idles 35% of the
time, swings at only 60% of openings, blocks some attacks, and recovers with its air jump then
its up special (aiming EdgeLord's air dash up and in). It swings only with a move that would
land - each move's hitbox, or the flight of what it throws, tested against the target's body the
way a real hit is - and otherwise keeps to the gap its own forward tilt reaches, walking the last
stretch so it can tilt, backing off slowly when a long weapon is too close, and using the air jump
to reach someone on a platform. It jumps a wall in its way (City Rooftops' buildings), jumps a
drop to someone standing across it rather than walking off, drops through or off a platform to
someone below it, never chases or dash-attacks toward a side blast zone (Open Plains' floor runs
on past them), and recovers out from under the stage before going up - with its whole body clear
of the corner first. Braking is a reflex, checked every frame rather than at a decision: sliding
at a drop or the side of the screen faster than it can stop, it pushes back at once - which is
what keeps Triguy, whose feet barely grip, from skidding off by himself. Against a standing target, every fighter now lands most of what it
throws; when it attacked anything within a fixed 115 pixels, Lugnut landed about one swing in
twenty. Added from the CPU button on fighter select, or `--cpu`. Still owed: the difficulty
setting (the constants at the top of the class are the knobs).

## M8 — More

More fighters, more stages, whatever he keeps drawing. The point of M3 and M4 is that this
milestone never really ends and never costs much.

**Unlockable fighters** - **built, 2026-10-23** (Eric's call). DoomBot and Lugnut start locked
(`FighterData.Unlockable`, `Unlocks`, saved in `user://unlocks.cfg`): a black silhouette and
"???" on both select screens, not to be picked - in versus or for a story - and never the
computer's pick. The first time a story fights one, a NEW CHALLENGER screen comes before the
scene (`ChallengerScreen`): the page gone dark, the words slammed down, his silhouette winding up
his own forward smash. Beat him and he is unlocked, with a screen in full colour, cheering, before
the story goes on into the scene after the fight. A save from before had its beaten fighters
unlocked for it. `--locked` locks them again for one run without saving; `--challenger=lugnut`
and `--unlocked=lugnut` show either screen on its own.

**Achievements** - **built, 2026-10-23** (Eric's call). Thirty (`Achievements.All`), saved in
`user://achievements.cfg`, each said as what to do: some hard (a story on Hard, every story, a win
with every fighter - a gold border), some fun, and many a clue to something a player might not
find (catching the palette in the air on its way back, the three taunts, dropping through a
dashed platform, a block worn until it breaks, a three-on-one team fight). Only a person playing
earns one - never the computer, never a story scene (`Achievements.ByPlayer`). One earned pops up
on a card at the top right over whatever is showing (`AchievementToast`). **The Achievements page**
(main menu, `AchievementsScreen`) lists them all with a gold star on the ones done and how far the
counted ones are - but not what they give, except that a fighter (or Mk. II) a tile earns shows
on it as a silhouette until it is done. Beating Lugnut and DoomBot in a story are two of them, and
unlock them. A save from before had its finished stories and beaten fighters carried over.

**Skins** - **built, 2026-10-23** (`Skins`). Four, each earned by an achievement and picked on the
fighter select screen by picking the fighter again: DoomBot's **Mk. II** armour (finish DoomBot's
own story on Hard - he plays as Mk. II), Flambé's **Blue Flame** (set fighters on fire 25 times),
Lugnut's **Safety Green** (50 KOs) and EdgeLord's **Shadow** (beat DoomBot at the end of a story
without losing a life). The colour skins recolour him the way a team fight does (`TeamLook`) -
never a line. Elim's fighters have none: a new colour on his drawings is his to ask for.
Testing: `--grant=ID` earns one for a run, `--skins=blueflame,,mk2` boots a `--match` in them,
`--achievements` opens the page, and `--locked` plays on an empty save that is not kept.

---

## Deliberately deferred

- **Online play.** Fighting-game netcode is its own project. The fixed-frame architecture from
  M1 leaves the door open; nothing else should assume it.
- **Non-humanoid fighters.** Build the single-sprite path when he actually draws one, not
  speculatively.
- ~~**Items.**~~ In, on Eric's call (2026-10-10): thirty art supplies, drawn by us for now - see
  Items in `.ai/fighting-design.md`. Still a natural home for the kids' smaller drawings.

## What is on the critical path right now

Nothing in this repo — **it is the drawings.** M1 can be built in parallel with him drawing,
but M2 onward blocks on having at least one good photograph of one good drawing. Send him
`for-nephew/how-to-draw-a-fighter.md` and `for-nephew/character-sheet.md` first; the quality of
that first drawing sets the ceiling on how well the whole pipeline works.
