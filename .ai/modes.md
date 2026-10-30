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
minutes) and **health** (100, 150 or 200). One button rather than three, because only one of the
three numbers ever applies. They are for versus and a doodle brawl; story mode and the other
modes set their own (`MatchManager.Rule`, set in `GameRoot.GoMatch`).

- **Time**: nobody runs out of lives (`TimedLives`). A KO is +1 to whoever hit him last - never a
  teammate - and -1 to whoever was KO'd, so falling off on your own costs a point too. The score
  shows where the lives do, and a clock counts down up top, red over the last ten seconds and
  ticking over the last five. At zero the best score wins (a team's is its fighters' added up);
  the rest are left standing with no lives, beaten on points. A tie is **sudden death**: everyone
  tied comes back at 300% with one life, everyone else is out, and it is last one standing.
- **Health**: one life each. Damage comes off the health, which shows where the percent does,
  reddening as it runs out; knockback still grows with the damage taken, exactly as percent does,
  so a hit late on still launches. At no health the KO blast goes off where he stands
  (`KnockedOutOfHealth`) - the blast zones still KO too.
- "Win on your last life at 150% or more" (`vs_clutch`) only counts on lives, where it means what
  it says.

`--rule=time`, `--rule=time:20` (a 20-second clock), `--rule=stamina`, `--rule=stamina:100` or
`--rule=stock` sets the rules for one run without saving them.

## Training

Smash's training mode is the model: you, a dummy to hit, and every hit shown. Fighter select
says "Training: pick your fighter" and "Press Start to train!", has no Teams or Items buttons, and
lets one person start on their own. A CPU seat picks the dummy's fighter; with none, a dummy sits
in the first empty seat anyway, as the next unlocked fighter after the first person's
(`TrainingMode.SeatDummy`, from `GameRoot.GoMatch`), and is still there to be changed on the way
back to fighter select. Locked fighters stay unpickable. Then stage select as usual.

The match (`MatchManager.Training`) is straight in - no 3-2-1, the stage's music from the first
frame - with items off and hitboxes on. A KO is the whole KO, blast, shake and all, but no life is
lost: he respawns at 0% and nobody wins. The stock dots are gone from the HUD (`MatchHud.NoStocks`).

What it shows (`TrainingMode`, a child of the match), lettered like the rest of the HUD - no boxes,
a thick paper outline:

- **The combo**, top left: "3 hit combo!" and its total damage under it. A combo is hits in a
  row while the one taking them is still in hitstun (`Fighter.IsLaunched`): free to act for even a
  frame, and the next hit starts a new one, so a "combo" he could have blocked never counts. It
  comes up from the second hit, lettered like the countdown - orange, an ink edge, landing with a
  punch and a tilt on each hit - and goes grey when the combo ends, fading after three seconds.
  Blocked hits are not combos.
- **The last hit**, top right: its damage ("(blocked)" if it was) and its knockback, the number
  the knockback formula gave (`fighting-design.md`).
- A small line top middle: that this is training, what the dummy is doing, and "Start for options".

**The pause menu** is where the settings are (`TrainingMode.PauseOptions`): what the dummy does,
Reset, hitboxes on or off, speed normal or slow, then Pick fighters and Main menu as ever. A
setting reads what it is set to and stays open when changed, A or left/right stepping it round
(`PauseMenu.Option`, `Stays`). Reset is the whole match rebuilt (`MatchManager.Restart`, which R
also does): everyone back on their spawn at 0%. Slow is a quarter speed, F2's speed, which F2 still
toggles. The dummy's behaviour and the speed last through a reset and back through fighter select
until the game is closed; hitboxes are on again for each new training match.

**The dummy** is a controller, `DummyInputSource`, so nothing in `Fighter` knows it is a dummy:
**Stand** (does nothing), **Walk** (paces up and down at a walk, turning at the end of whatever it
is on, at the edge of the world, or every two and a half seconds), **Jump** (a jump every 45 frames
on the ground), **Block** (holds block, so the shield wears and breaks), and **Fight** (the real
CPU, `CpuInputSource`, at the seat's difficulty). Whatever it is set to, knocked off the stage it
comes home the way the CPU does - by handing the CPU the controller until it is back - because a
dummy that fell to its death after every launch would spend the practice respawning.

**Nothing is earned in training** (Eric's call, 2026-10-29): no KO, taunt, throw or stage counts
toward an achievement. It is checked once, where everything is earned (`Achievements.Grant`,
`Count` and `Tick` do nothing while `GameRoot.Mode` is Training), not at each place that earns one.

To look at it: `--mode=training --match --fighters=3,1` is you as fighter 3 against a dummy as
fighter 1 (in training, `--match` makes every seat after the first the dummy); `--dummy=fight`
(or stand, walk, jump, block) starts the dummy doing that; and `--trainingmenu` opens the pause
menu once the match is going and writes it to `.shots/training_menu.png`, then quits - the pause
stops the frame count `--shots` goes by, so pass `--windowed` with it rather than `--shot`.

## Boss fight

Everyone together against a giant DoomBot (`BossFight`, `--mode=boss`). Fighter select takes one
to four seats, people and CPUs, all on one side: no Teams or Items buttons, one seat is enough, and
Start reads "Press Start to fight DoomBot!". It goes straight to the fight - no stage select
(`BossFight.Prepare` turns the versus match into the boss fight: DoomBot added last, on a team of his
own, no items).

- **One stage, Playground.** The big stage with one unbroken field: Building Site's pit would drop
  a giant in again and again, and the kitchen's toaster and cookbooks are walls to someone his size.
  The slide, monkey bars, swings and trees are where everyone else climbs to get above him.
- **The giant is twice DoomBot's size** - the magnifying glass's growing (`Fighter.giantScale`),
  held there (`RestScale`) instead of a second mechanism, so his drawing, body box, hurtbox and
  hits all grow together; his art is only scaled. He grows into it through the 3-2-1.
- **Everything a grown fighter does grows with him** (Eric, 2026-10-29) - anyone made big, the
  magnifying glass too, not only the boss. What he sends out passes through `MatchManager.SpawnHazard`,
  which swaps in a grown copy (`MoveData.GrownHazard`): drawn and hitting twice as big, flying twice as
  far in the same frames (speed and gravity both), a beam twice as long - his missile, the MiniBot and
  its blast, the eye laser. What it rains or bursts into is grown as that is spawned, so nothing grows
  twice. His own moves get a grown copy as they start (`MoveData.GrownBody`): arms and chains reach
  twice as far (the claw), what he holds is twice the size, a girder twice as wide, and rises and hops
  go twice as high (the square root on the speed, his gravity being his own). Hitboxes, smears,
  sparks, flames, the piston arm, where shots come out and how far a grab reaches all grow where they
  are used. Damage does not grow, and a dash's speed does not. A copy is made once per size and kept.
  At normal size nothing changes: `RegressionChecks` reads the same before and after.
- **Health, not percent.** A bar across the top (`BossHealthBar`): his name in his letters, glowing
  with his heat, over a bar coloured in with pencil on paper, outlined in indigo. A hit jolts it and
  what it took shows pale before it drains; a tick marks his last stand. His percent stays 0 -
  anything that would raise it, a burn and his own overheating included, comes off the bar
  (`Fighter.Percent`). **200, and 150 more for each fighter after the first** (one: 200, four: 650).
- **He is not launched.** A hit under 13 damage clangs off him - the damage counts, nothing else
  does; 13 or more (most smashes, strong specials, anything charged) **staggers** him for 30
  frames, a step back, never toward a drop. Then his armour holds against everything for 150
  frames, so four people cannot keep him reeling. A block is a block. He cannot be grabbed.
- **Falling off** brings him back from above like anyone, for 8% of his health. His lives are a
  formality (99); the bar decides it, and at 0 he goes up in a blast where he stands.
- **His last stand** on his last quarter is the story boss's: "CORE TEMPERATURE CRITICAL.", red
  flashing, full heat, a MiniBot every 220 frames, the music at 1.25. His own theme from FIGHT!.
- **The computer plays him at level 7** (one below Hard), his hits at DoomBot's own damage - not the
  magnifying glass's extra power. Watched with CPUs: at level 8 and 1.15 times the damage he beat
  four; at this he lost narrowly to three Medium CPUs and an idle player. Not yet played by a person.
- **Players** have their usual lives (`GameOptions.Stocks`). They win when the bar is empty -
  "You beat DoomBot!", everyone left celebrating - and lose when all of them are out ("DoomBot
  wins!"). No versus achievements are earned in it, and it adds none of its own.
- The CPU judges a giant's reach by his grown hitboxes and shots, looks for a wall at knee height as
  well as at his middle (the sandbox stopped him), and does not grab the boss. The camera frames a
  giant head to foot.

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

A versus fight whose items the players draw first (Eric's call, 2026-10-29) - the roadmap's "a
natural home for the kids' smaller drawings". Fighter select is versus's (people, CPUs, teams),
then stage select (no Items button: the items are about to be drawn), then the **doodle screen**
(`DoodleScreen`), then the fight.

- **Everyone playing draws one item, in turn.** "Player 1, draw your item!" on a band of the seat's
  colour (their name if they gave one), with their own controller's pointer or the mouse - one
  pointer at a time, as on the drawing screen (`OnePointer`). CPUs do not draw. The page is the
  drawing screen's paper and brushes (`Painter`, `DrawScreen.Palette`/`Sizes`): marker, pencil,
  bucket and eraser, undo, redo and "Start again", one page, one layer - a minute's doodle, not a
  fighter. Pencilled on the page and said again beside it: draw it standing up, you hold the
  bottom.
- **Done asks what it does: Throw it or Swing it** (or Keep drawing). A page left blank is
  skipped - nothing to ask about; if nobody draws anything, nothing drops.
- **A drawn item is a catalog item wearing the drawing** (`DoodleSet`), never a new kind of item:
  swung is the paintbrush (its swing, six swings, its cooldown, size 140), thrown is the
  scrunched-up drawing (bounces along hitting people, then goes), drawn at 80 rather than its 60 so
  the drawing reads. The same `MoveData`, not copies, so balance stays the catalog's - and
  picking up, the held pose (`Fighter.PlaceHeldItem`), swinging, throwing and the sounds
  (`SfxCatalog`) need nothing new.
- **The art is the drawing, cut out, never redrawn** (rule 1): cropped to the ink, made smaller
  if it is big (320 pixels at most, about twice what is ever shown), its see-through edges given
  the ink's colour so scaling leaves no dark fringe. Swung, it is held a tenth of the way up from
  the bottom, centred on whatever is drawn down there, and hits at the top; thrown, it is held in
  the middle. Each seat's goes by `doodle_1`...`doodle_4` (`ItemCatalog.AddDrawn`), so `Find` and
  the parade know it, but it is never in `ItemCatalog.All`: not on the item settings screen, never
  dropped in an ordinary fight.
- **Only the drawn items drop, often** (High), each in turn from a random one, so everyone's comes
  round (`DoodleItems`) - whatever the item settings say.
- **Kept for the session, never saved or sent.** The drawings live on `GameRoot.Doodles`: not in
  "Your drawings", not in the gallery, never posted to the intake. After the fight, Start goes
  back to fighter select as usual; next time round, each page opens on last time's drawing ("Done
  keeps it") with last time's pick lit, to keep with two presses or change.

`--doodle` boots to the doodle screen (`--doodle=choice` with Throw it / Swing it up). Nobody can
draw in a screenshot run, so **`--doodletest`** paints a test item on every seat's page through
the same brushes - a sword, a ball, a hammer, a star (`--doodletest=1` turns them round the seats,
so Player 1 has the ball) - and **`--doodlehold`** hands every fighter one as the fight starts.
Both are tests. A doodle fight to watch: `--mode=doodle --match --stage=5 --fighters=0,1,2,3
--doodletest --doodlehold`; a drawn swing: `--parade --film=item:doodle_1 --only=0 --doodletest`.
