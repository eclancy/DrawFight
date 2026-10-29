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

## Boss fight

## Targets

## Doodle brawl
