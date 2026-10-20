# Art direction

The visual contract for everything the kids do **not** draw: stages, backdrops, menus, effects.
Read before adding any art, any stage, or any UI colour.

For how their drawings themselves are processed, see `.ai/art-pipeline.md`. That is a separate
concern and its rules win wherever the two touch.

## The one law: characters own black

Their drawings are black marker on white paper. That is the highest-contrast thing a sheet of
paper can hold, which is lucky, because it means the fighters win the fight for attention
automatically — **so long as nothing else in the game is ever allowed to be black.**

So colour is not the risk here. **Value** is. A saturated sky is harmless. A dark tree is not.

| band | who lives there | rule |
|------|-----------------|------|
| near-black | fighters, and only fighters | reserved; nothing else enters it |
| dark indigo | stage outlines | never black, and thinner than a character line |
| light, low saturation | every background | **no outlines at all**; depth comes from value |

**Nothing outside a character may be darker than roughly 30% value.** That single line is the
whole art direction; everything below is consequences of it.

### The corollary that keeps biting

Because the palette is light, **light-coloured UI disappears**. White-on-cream has shipped
invisible twice: stock icons, then status text. Every HUD label now carries a thick
paper-coloured outline, so it reads on any stage without borrowing whatever the stage happens to
be — a stage palette is free to be grass or brickwork and the HUD is not free to become
unreadable. (It first used translucent paper bands behind the readouts; those covered too much
of the stage and were replaced by the outlines.)

## Outlines are grammar, not decoration

Because backgrounds carry no outlines at all, an outline means something:

- **If it has an outline, it is real** — you can stand on it, or it can hit you.
- **A solid outline is solid ground.**
- **A dashed outline is a soft platform** you can jump up through and drop down through.

That last one turns a mechanic into something visible instead of something discovered by dying.
Keep it. A soft platform drawn with a solid outline is a bug, not a style choice.

## Three styles

They differ in what the world is drawn *on*.

**`ExerciseBook`** is the house style. Ruled school paper with a pink margin rule, drawn over in
blue biro and filled with crayon. Used by City Rooftops.

**`TapedPage`** puts that page *on something*. The stage is drawn on a sheet of ruled paper
(`StageData.PageRect`) taped to a surface, and the surface is drawn round it - all in
**coloured pencil**: fine close strokes that stay inside the lines, gone over twice for an
outline (`CrayonBrush.PencilFill` / `PencilRect`). The sky colours become the surface. Keep the
sheet only a little bigger than the stage, so the thing it is taped to actually shows. Nothing
uses it at the moment.

**`PinnedPages`** is several sheets instead of one: `StageData.Pages`, each a rectangle with its
own paper (ruled with a margin, squared, or plain), a tilt of a degree or two, a soft shadow,
and one or two round fridge magnets along its top edge. They overlap, back to front, the way a
real fridge door does, and the stage is drawn across them - every platform sits on paper, with
the door showing in the gaps. Used by Fridge Door, and by Big Show, where one big sheet has a
concert stage drawn on it: a wooden deck with a pleated skirt, amp stacks, and a red steel
girder across the top hung with spotlights (`PlatformLook.StageDeck`, `AmpStack`, `Girder`).
Its spotlights are pale and see-through, so they light a fighter rather than hide one. Below the
deck, in front of its skirt, stands the audience (`PropKind.Crowd`, `Crowd`; Eric, 2026-10-20):
two rows of people seen from behind - the back of a head, ears, shoulders - cut off by the bottom
of the page the way a kid draws a crowd. They are scenery, so **no ink**: pale coloured pencil
with a soft edge in a darker shade of their own colour, the back row faded further into the paper
than the front, so they sit under the HUD and never compete with a fighter. Their hair (a bun,
pigtails, a punk crest, a bobble hat, a cap on backwards), their colours, their height and what
their arms do (pump a fist, wave, clap overhead, swing a glow stick, hold up a sign with a heart
or a star) are hashed from the prop's seed. They are drawn by their own node over the stage and
under the fighters, so a raised hand never hides anyone on the deck.

**`OutsideTheLines`** is for nature stages. Plain paper, no rules, indigo outlines, and crayon
fill that deliberately **overshoots the line**. Used by Open Plains, and by Playground. The overshoot is the single
detail that reads as a child did this rather than a computer — keep it turned up.

Switching a stage between the two should be a one-line change, and it is: `StageData.Style`.

## The big stages

Three stages made for three and four players (Eric, 2026-10-16), each about half as wide again as
Big Show, built from platform looks like every other stage - no stage has drawing code of its own
(its speakers and its crowd are the moving parts. The crowd bounces on the speakers' beat - a
bob for most, a pogo for some, each a frame or two off it so it ripples - and roars, everyone
jumping with both hands up, at a KO (`Stage.Cheer`) and at a speaker blast. The speakers are
`StageData.Speakers`, `SpeakerBlast` - each stack one tall
cabinet in dark grille cloth, a big woofer under a small tweeter in bright metal rims, metal
corners, a gold-panelled amp head, because grey boxes of round windows read as ovens (Elim,
2026-10-19); the cones always buzz a little, thump on the beat with arcs of sound either side, and every nine to fifteen seconds one stack swells for a second and blasts a ring of sound
that shoves whoever is near it back, 5% and a long push):

- **Playground** (`OutsideTheLines`): recess on a playing field that ends at both sides - a slide
  (`SlideTower`: the deck is the platform; the slide is coloured in with no ink, because you
  cannot stand on it), monkey bars seen end on (`MonkeyBars`), a sandbox (`Sandbox`), a swing set
  whose swings are thin and pass-through (`SwingSet`), and two trees.
- **Building Site** (`ExerciseBook`, day): Lugnut's world - two pads of dug earth (`Dirt`) with a
  pit between them you can fall into, a scaffold plank across it, tower cranes with beams hanging
  over it, a site office (`SiteCabin`, hazard stripes in yellow and pale grey, never black), cones.
  Each beam hangs on two cables from a hook under a trolley that runs in and out along its jib
  (`StagePlatform.Travel`, a moving platform that carries whoever stands on it); the counterweight
  is on the back end, away from the load. The cranes themselves are scenery (`PropKind.TowerCrane`,
  no outline) - only the beams are stood on (Eric, 2026-10-20).
- **Kitchen Counter** (`PinnedPages`): the kitchen the story happens in, drawn on one big sheet on
  the fridge - kitchen units (`Countertop`: the worktop is what you stand on), a sink set between
  them to drop into and climb out of (`Sink`), a toaster and a pile of cookbooks on the worktop,
  shelves and wall cupboards above (`WallCabinet`: the top is a soft platform and the cupboard
  under it is pencilled, not inked - furniture on a wall, not a wall). A window over the sink is
  drawn on the page (`StageData.OnPage`, props drawn over the sheets rather than behind them).

Each player has a colour - blue, orange, green, pink (`MenuTheme.PlayerTint`) - light, like
everything outside a fighter, on their cursor, their HUD tag, their tag over their head and their
shield.

## Night is pale

City Rooftops is an evening stage - a clear blue sky, a moon coming up, the first windows lit,
and pale cement buildings - and any night stage's sky is **lavender, not black**. Two reasons,
and the first is not negotiable:

1. A dark sky erases the fighters. They are black ink; that is the whole point of them.
2. **It is how children actually draw night** — pale purple sky, a big moon, yellow windows.
   Nobody aged nine fills a page with black crayon.

All of a night stage's warmth comes from its **lit windows**, not from the sky. The sky stays
quiet and the accent colour does the work. Any future night or interior stage follows the same
rule: light substrate, warm accents, darkness implied rather than drawn.

## Effects drawn round a fighter

Smears and fire are drawn by the game, not by the kids, so this contract covers them.

- **A smear is motion, not a thing**: no outline - an outline means something real you can
  stand on or that hits you - and soft at its edges and its tail, in the fighter's own trail
  colour with a pale core, never darker than the 30% line. It is drawn behind the fighter.
  The harder the hit, the bigger and bolder it is; a jab's is a flick.
- **Fire is tongues, and fire rises.** Every flame is curved teardrop tongues - round at the
  root, bending as they rise, flickering to a point; red outside, orange, a yellow heart - so
  they run together into one fire (`FlameFx`). However a flame is thrown - off a spinning wheel
  or trailing a dive - it leans no more than 50 degrees off straight up. Never a
  ring of spikes, never a star, never pointing down (Eric, 2026-10-10: it looked like an orb with
  spikes, then pointed every way).
- **A fire on the move trails behind its motion, and rises as it trails.** A fireball, a
  burning drop or Flambe's fireball roll is a comet (`FlameFx.Comet`): a white-hot heart in a
  yellow and orange ball with a red edge, and curved, licking tongues streaming straight back
  from the way it is going - longest dead behind, shorter round the sides - each one rippling
  and curling up as it trails, with embers shed off the tail. The tongues follow the motion,
  never the spin: a rolling ball's flames stay behind it, not wheeling round with it. Going
  up, they stream out sideways no more than 30 degrees below level rather than pour down like
  exhaust; standing still, the same ball burns straight up. Ball and tongues are laid down a
  colour at a time - every red, every orange, every yellow - so they are one fire, not a ball
  with flames stuck on (Eric, 2026-10-20: the fireballs' flames should angle directly back,
  with more curves). Any other fast-moving fire uses `FlameFx.Lick`.
- **Jets are the one fire that points down**, because they are thrust, not a burning thing:
  DoomBot's boot jets come straight out of his soles along his legs, and a burst of them (his
  Jet Blast) blooms into a ball of heat at its end.
- **A burning prop is drawn without its flame.** It names a `flame` point and `flameHeight` in
  rig.json, and the game draws the flame rising from it whichever way it is held (Flambe's
  lighter). A flame drawn on the prop turns upside down with the hand.
- Every flicker and wobble is hashed from a frame count, never random.

## Every name in its own letters

A fighter's name is lettered the way they would write it (Eric, 2026-10-20): on the select
screen's cards and seats, over the percent in a match, on the winner's banner, in the corner of a
speech bubble and on a story's map. One drawer (`NameLettering`, and `NameTag` where a Label went)
and a style per fighter as data (`FighterData.Lettering`, a `NameStyle`) - colour, edge, shading,
lean, stretch, boldness, spacing, case, motion and one decoration - never drawing code per fighter.

- **Legible first.** Every name keeps the paper halo every HUD label has, so it reads on any
  stage; a style's edge is a deep colour, not black - letters are UI, and the 30% line holds. Each
  was checked at 30px on a bubble and at 34px on grass, brick and a night sky (`--names`).
- **Motion is a whisper**: a bob, a flicker, a glint every few seconds - counted in frames and
  hashed, never random. Player names (P1, CPU, a chosen name) stay plain, in the player's colour.

- **Flambé**: hot orange running to yellow, leaning into it, a tongue of flame rising off every letter, flickering.
- **Lugnut**: chunky wide capitals in hi-vis yellow with a hard shadow, on a yellow-and-pale-grey hazard bar bolted at both ends.
- **Circy**: fat round yellow letters in a scrawled marker edge, each bobbing and rocking on its own beat.
- **EdgeLord**: narrow red italics leaning hard over a sword laid hilt to point, a glint running down it now and then.
- **DoomBot**: bevelled steel capitals in fixed cells, rust round the edges, on a riveted strip; a letter judders now and then, and they glow red-hot with his heat wherever it is known.
- **DoomBot Mk. II**: DoomBot's letters in newer blue steel, each one a different bit of scrap bolted on crooked.
- **Triguy**: white letters edged in Elim's blue, leaning right forward, speed lines streaming behind and his top hat on the first letter.

## Items

Items are drawn by us, not by the kids, so they follow the stand-ins: **coloured pencil with
indigo outlines** (`tools/art/items.py`, through `drawn.py`), which keeps them in the world of
the page and clear of the characters' black. An item hits you, so it gets a solid outline.

- **A tool points along the arm; a carried thing stays upright.** A pencil, a brush or an
  airbrush points out of the fist along the forearm. A bucket, a bottle, a box or a book is
  held upright whichever way the arm points - a bucket that followed the arm lay on its side.
- **Held in the fist, not behind the body.** A held item is drawn inside the front forearm,
  under its drawing (`FighterRig.HeldLayer`): in front of the chest, behind the hand. Drawn
  behind the whole fighter, anything smaller than a pencil vanished behind his chest.
- **On the floor, things lie as they would.** Something long and thin lies on its side; the
  rest sit upright on their bottoms. Nothing an item does goes through the floor - a swing
  scrapes along it, splashes land on it, a cloud of ink sits on it and billows up.
- **What an item leaves behind is flat on the floor**: a puddle is a glossy ellipse with an
  edge a shade darker than itself, so white glue still reads on a white page. The marker's
  platform is **dashed**, because you can drop through it - the outline grammar holds.
- **Effects are the material, not a generic blast**: a glitter bomb is a starburst and glitter
  drifting down, ink is a billowing cloud, the rubber stamp prints its star on the floor. The
  stage's orange explosion flash is for bombs that are bombs.

## The drawing screen

The drawing screen is the kid's page, so it is the one place outside a fighter where black
belongs: its palette has black in it, because what is drawn there *is* a character. Everything
round the page - the desk, the panels, the buttons - keeps to the menus' look and the 30% line.

- **The page is lined paper**, like every menu, with a pencilled reminder in its corner ("Side
  view, facing right"). The paper is never sent: every picture goes see-through.
- **The coloured pencil has the game's grain.** Its tooth is the same recipe as
  `CrayonBrush.PencilFill` - close strokes at a slant, a lighter cross-hatch - fixed to the page,
  so a kid's colouring in and the game's own pencil art look like the same pencils. Going over
  it again fills it in; a light pencil over a dark line barely shows, so colouring in never
  hides an outline.
- **The paint bucket fills in pencil** by default - or solid if asked, and solid is still
  coloured pencil, pressed on, with the same subtle unevenness as the stand-ins' colouring
  (`Painter.BuildColouring`, the recipe of `tools/art/drawn.py`), never flat colour (Eric,
  2026-10-16) - and tucks a couple of pixels
  under the lines it fills, so there is no pale fringe between colour and line.
- **The marker is for outlines**, even all the way along however slowly it is drawn.

## A stage is data, not a renderer

There is **one** stage renderer (`Stage.cs`) and there should stay one. A stage is a palette,
a list of rectangles, and a list of props, all declared in `StageCatalog.cs`.

If adding a stage seems to need new drawing code, the data model is wrong, not the stage. The
escape hatch is a new `PlatformLook` or `PropKind` — a *reusable* shape that more than one
stage could want — never a per-stage special case.

`CrayonBrush.cs` holds the mark-making: wobbled ink lines, hatched crayon fill that overshoots,
scribbles, and banded skies. **Every wobble is hashed from a seed, never randomised.** A stage
redraws on window resize and on debug toggles, and geometry that re-rolls its jitter each time
shimmers — which reads as a rendering bug rather than as hand-drawn.

## The roster

| stage | style | shape of it |
|-------|-------|-------------|
| **Fridge Door** *(default)* | PinnedPages | Where kids' drawings actually end up. The stage is a pencil drawing across five sheets held up on a steel fridge door by magnets - a ruled page, a squared page, plain scraps under the side platforms; more drawings held up higher are the scenery, which makes this the stage his non-fighter drawings move into. |
| **Open Plains** | OutsideTheLines | Flat, daytime, only trees and grass: the treetops are the platforms, and nothing on it is built. Ground runs past both blast zones, so **there is no bottom blast zone at all**. |
| **City Rooftops** | ExerciseBook, evening | Six separate rooftops with real gaps, one tall tower, a hazy skyline behind. Every building has windows in a centred grid and a pair of doors at the foot. A building site fills two of the gaps: a tower crane whose jib you can stand on, a steel beam hanging from its hook, and two levels of scaffolding to climb out of the street (`CraneJib`, `HangingBeam`, `Scaffold`, all soft). The cement buildings stand on a street that runs the whole width, with the pavement drawn behind the road surface you stand on. Cars drive along it every few seconds at random (`StageData.Traffic`, `StreetTraffic`): fall into a gap and you are in the road, where a car smashes you up and away - a KO from about 60% - unless you jump it or get out. Headlights reach well ahead of each car, so it is seen coming. No bottom blast zone. |

Open Plains and City Rooftops are deliberate opposites. On one, a missed jump costs nothing —
which is the right first stage for a younger or newer player, because losing a stock to a failed
recovery is the most demoralising way to lose. On the other, the floor itself is the hazard -
not a bottomless pit any more, but a street with traffic in it.
Keep one of each in the roster as it grows.
