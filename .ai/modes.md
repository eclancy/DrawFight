# Modes

Everything there is to play besides versus and story mode. **Read before changing a mode, or
adding one.** Versus is "Rules of a match" in `fighting-design.md`; story mode is `story-mode.md`.

All of these went in on Eric's call (2026-10-29). The main menu's fourth card, **More modes**
(`ModesScreen`), opens four of them; each sets `GameRoot.Mode` and goes on to fighter select,
which picks for that mode. The main menu puts the mode back to versus. `--modes` boots to the
More modes screen and `--mode=training|boss|targets|doodle` makes fighter select and a `--match`
that mode's.

## Versus rules

The options' **Versus rules** button steps round three ways to win a versus fight
(`GameOptions.Rule`, `StepRules`): **lives** (1 to 5, the default, as before), **time** (2, 3 or 5
minutes) and **health** (100, 150 or 200).

## Training

## Boss fight

## Targets

Break the targets, with Smash's Break the Targets as the model: one fighter, ten targets on a
course, and a clock running from GO.

- **Picking.** Fighter select picks for one player, the story's one-player page
  (`CharacterSelectScreen.ForTargets`, set with `ForStory`): any unlocked fighter, no difficulty, and
  Start goes on to the courses (`TargetCourseScreen`). Each course card shows it laid out small - its
  sheets, what you stand on, the ten targets - and that fighter's best time on it. B goes back a
  step each time: courses to fighter select, fighter select to More modes.
- **The courses** are Easy, Medium and Hard (`TargetCourses`), stages like any other - a palette
  and a list of rectangles, drawn by the one renderer - on sheets of squared paper on the fridge
  door. They live in `TargetCourses`, not `StageCatalog`, so they are never in versus stage select,
  the stage achievements, F4 or the CPU's stages. Easy is one sheet with a floor past both ends,
  as on Open Plains: nothing to fall off. Medium adds a gap of door to fall into, a target in it
  and one out past the far end. Hard is three islands over the door: a box you get into only by
  dropping through its dashed lid, high ledges, and targets down in the gaps.
- **Every fighter can break every target.** The courses are built round what each fighter
  reaches, measured, not guessed (each standing, jump / jump and air jump / and up special, to the
  feet): Circy 188 / 379 / 824, Lugnut 242 / 480 / 757, DoomBot 242 / 480 / 805, Flambé and Triguy
  307 / 620 / 1100+, EdgeLord 307 / 695 / 1301. Running, with both jumps and the up special, Lugnut
  goes 929 across and everyone else 1500 or more. So Easy never steps up more than 300 (Circy's two
  jumps); Medium's steps are 300 at most with gaps of 300; Hard steps 470 and 550, which the up
  special finishes for Circy, Lugnut and DoomBot, over gaps of 450. A target hangs at most 330 over
  what it is reached from, or within an up air of the top of a double jump. Circy's grappling hook
  flings him a long way sideways as it lifts him, so what he must land on after it is wide (500
  and 600). DoomBot, 255 tall, fits everywhere: the Hard box is 376 inside.
- **A target** is a disc of card coloured in red crayon round and round, inked round its edge - it
  is a real thing to hit - and held to the door by a fridge magnet, so nothing floats
  (`TargetsRun.DrawTarget`; the More modes card draws the same one). It breaks to anything the
  fighter hits with, the way it would reach a fighter: an attack's capsule out past the limb, a
  shot or anything else he sets loose (`Hazard.Reaches`, used up by it as on a fighter -
  `BrokeTarget`) and a thrown item. A grab, a size change or a bomb's fuse does not; the bomb's
  blast does. Breaking one gives a pale flash, the card in wedges and its magnet dropping off - an
  effect, so none of it is inked - a small shake, a tap of rumble and `target_break`.
- **The run.** 3-2-1-GO!, and the clock counts frames from GO, shown as a stopwatch ("0:23.45") over
  a row of ten little targets, ticked off as they go. The camera keeps every target left and the
  fighter in view, closing in as they go. One go and no items, no CPU: off the bottom is "Try again!"
  (Easy has no bottom). All ten stops the clock: "Complete!", the time, and "New best!" or the best,
  with the camera in on him celebrating under the words. Bests are kept per fighter per course in
  `user://targets.cfg` (`TargetRecords`), in frames. After a run Start goes again and B goes to the
  courses; the pause during one offers Try again, Pick fighter and Main menu.
- `--mode=targets --match --fighters=N --course=0|1|2` boots straight into a run, and `--courses`
  boots to the course picker (for the fighter in `--fighters`).

## Doodle brawl
