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
