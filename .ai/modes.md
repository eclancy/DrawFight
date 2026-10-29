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
