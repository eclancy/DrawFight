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

## Doodle brawl
