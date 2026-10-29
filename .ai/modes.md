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

## Targets

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
