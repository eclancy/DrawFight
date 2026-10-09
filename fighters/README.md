# fighters/

One directory per fighter. Each holds:

```
<name>/
  source/drawing.png   the photographed drawing - the master copy, never edited
  parts/*.png          sliced parts, generated - re-cut from source, never hand-edited
  rig.json             bone tree, offsets and pivots
```

## `swift/` and `lug/` are STAND-INS, not content

`swift/` and `lug/` are **drawn by scripts**, not by a kid. They began as generated stick figures,
so that the shared skeleton, the animation library and the fighting could be built and played
before a real drawing existed — which is what `.ai/roadmap.md` asks for at M2, so that the import
tool at M3 is built against a rig already known to work. Both are now cartoons we drew in SVG,
coloured in coloured pencil inside a black marker outline (`tools/art/drawn.py` is what they
share). Their tools and props are drawn the same way, in their style, each by its fighter's
script; the stick-figure generator is gone.

Regenerate them with the only Python on this machine (and `$GODOT_BIN`, which rasterises them):

```
python tools/art/flambe.py
python tools/art/lugnut.py
```

**Replace both once real drawings are in.** Leaving them in the roster once there is real art
would be the one thing this project is not for. Eric is going to draw Lugnut himself; his
drawing replaces `lug/` the way the photo of his robot became DoomBot.

Swift (Flambé in game) is a hotshot punk rocker, drawn by `tools/art/flambe.py` (Eric's call,
2026-10-08) - `swift/source/drawing.png` is the whole figure, a fist punched in the air: a flame
mohawk, a black leather jacket with silver spikes on the shoulders, studs on the lapel and flames
up its hem over a flame tee, a wallet chain, dark red skinny jeans and chunky buckled boots - and,
because he is somehow also a French chef, a red neckerchief. Confident and defiant: brows down,
eyes fixed ahead, a one-sided smirk, and no moustache (Eric, 2026-10-08). Lean and long
in the leg, because nearly everything he does is a kick. He is cut and rigged exactly as Lugnut
is (below): three-quarter view, near arm on top, far arm unshaded, boots as feet, coloured in
pencil, nothing finer than a brow or a stripe. He has two fire wings (`swift/poses/wings.png`,
one each side of a gap, hinged at his back so each flaps on its own) for his flying up special,
and a flaming frying pan (`swift/poses/tool_pan.png`) for his forward smash. He holds no weapon
of his own, so his hand is an empty prop part (`"empty": true`) that a move can fill - under his
fist, like Lugnut's tools.

Lug (Lugnut in game) is drawn by `tools/art/lugnut.py`, from an
example drawing Eric picked (2026-10-08) - `lug/source/drawing.png` is that drawing, flexing: a
cartoon muscle man of a construction worker in three-quarter view, with a hard hat, an open
hi-vis vest over a tank top, a tool belt, jeans, work gloves, steel-toe boots and a scowl. He is
huge up top and short in the leg, so he fights with his arms and his tools and almost never his
feet - his down tilt shoves its traffic cone along the floor with both hands. Every detail that
would vanish in coloured pencil or at match size was taken out on Eric's call; keep it that way.
The parts are SVG, rasterised by Godot (`tools/art/svg_raster.gd`, since PIL cannot draw a
curve), so the script needs `$GODOT_BIN`. His boots are cut as feet.

**He is coloured in coloured pencil** inside a solid black marker outline, the way the drawing
guide asks a kid to colour one: the colour goes down in slanted strokes, pressed harder and
softer, with the paper showing between them and through its tooth. His tools and props get the
same. The strokes are coarse - about a dozen pixels across - so nothing finer than a pixel
shimmers at match size. Seeded, so a re-cut is identical. Eric's call, 2026-10-08.

**His arms hang from his shoulders as drawn.** Facing right in three-quarter view, his near
side is on the left: the arm on the left shoulder is his front arm, drawn over everything, and
the one on the right is his far arm, behind his chest - and not shaded darker, as far limbs
usually are (`"darken": 1` on its bones), since coming from behind his chest is enough. A
forward swing from the near shoulder crosses in front of his face, as it would in three-quarter
view. Eric's calls, 2026-10-08, after trying them hung from the middle of his chest (they
hovered there) and the other way round.

**On the ground, his sledgehammer rests on his shoulder** - standing, running, crouching,
blocking, landing, and through his head-first barge (`FighterData.ShouldersProp`,
`FighterAnimations.CarryOnShoulder`): elbow down, fist in front of his chest, the handle on top of
his shoulder and its head behind his back. It comes off only to swing, or when he is in the air
or knocked about. Whatever he holds is drawn under his glove, so his fist closes over the grip
(`"behindParent"` on the prop bone), and that glove has no thumb line. Eric's calls, 2026-10-08.

**He always wears his hard hat.** The `hardhat` extra is the same hat lit up - a pale rim and a
sparkle - shown while he blocks and for as long as his smashes and barge are armoured, the way
the hat itself used to go on and come off.

He swaps a set of heavy tools into his hand - `lug/poses/tool_*.png`: a pipe wrench, shovel,
pickaxe, crowbar, steel beam, stop sign, jackhammer and nail gun - drawn in his style by
`lugnut.py`, as are the wheelbarrow, wrecking ball, girder and cone. Each keeps the canvas, grip
and length of the stick-figure drawing it replaced, because the game holds and measures a tool
by its canvas (Eric's call, 2026-10-08: every weapon redrawn to match its fighter). His reach
is built on how long those tools are and how far his arms hold them out, so if a tool or an arm
is redrawn longer or shorter, re-check the hitboxes with `--parade --attacks --only=1`. Every
hitbox was moved onto its tool's head when he was redrawn, and his reach came out about where
the stick figure's was (his jab and shovel jab hit at 134, against 130 and 136).

Note that neither `tools/art/flambe.py` nor `tools/art/lugnut.py` is **a model for the real
pipeline**. They draw each part directly in its canonical orientation, because they are
generating the art in the first place.
The real pipeline cuts parts out of a photograph, which is the whole job of the M3 import tool.
The `source/drawing.png` each also emits — the assembled figure on paper, with grain and uneven
lighting — exists precisely so that importer has something realistic to be tested against
before real drawings are at stake.

## `circy/` is Elim's — the first real fighter

Circy was designed **and drawn** by **Elim**: name, body, strengths, weakness and all four
specials come from Elim's character sheet, and every pixel of him comes from Elim's drawings in
`circy/source/`. Those ten PNGs are the master copy and are never edited.

Everything else in `circy/` is generated from them by `python tools/art/cut_circy.py`:

- **`parts/`** — the main drawing (`circytcircle.png`) cut into a rig. The ball, face and all, is
  the Torso; he has no separate head. Each limb stroke is rotated to hang straight down from its
  joint and split in half at the elbow or knee. Nothing is redrawn; every line is widened in place
  (see below).
- **`poses/`** — Elim's other drawings, trimmed of empty canvas and shown exactly as drawn:
  - **held poses** that replace the puppet while he holds still: `lookout` then `shrinking`
    before the bomb, `bomb` while the fuse burns, and `bash` while the grappling hook pulls him in
  - **effects** sized to the move: the `laser`, the `boom`, and the `rope`, `gun` and `hook`

  His drawings of himself tall and small (`circytcircletall.png`, `circytcirclesmall.png`) are
  no longer cut, and the main drawing is no longer shown whole while he stretches. He Stretches
  as the puppet instead - arms straight out, legs straight
  down, with arrows over his head and under his feet saying which way to push - so the change
  happens in his own legs. Eric's call, 2026-10-04. Tall, his run cycle plays at under a third
  of the speed (`SizeLevels.StrideRate`), so the long legs take slow, heavy strides.

Two things about Elim's art that the code works around rather than changes:

- **He drew Circy facing the viewer, not in side view.** A round face mirrors cleanly, so it is
  used as drawn; the limbs on the right of the page are treated as the front limbs.
- **The limbs are thin lines.** At match size they came out under one pixel wide and shimmered
  as he moved. Eric's call (2026-10-04): the cut widens every line of ink by the same amount, in
  place - `LINE_BOOST` in `tools/art/cut_circy.py`, done by `tools/art/linework.py` - so they
  come out about 2.5px wide. Every line keeps its path; nothing is moved, smoothed or redrawn,
  and `source/` is untouched. `LINE_BOOST = 0` and a re-cut gives Elim's line exactly as drawn.
  Triguy gets the same, with a larger boost because he is drawn on a bigger canvas.

## `edgelord/` - drawn by Eric, with stand-in colour and move art

EdgeLord ("stretchy arms, super speed, infinite swords" - the stretchy arms became a dagger on
a chain, since stretching is Circy's) plays in the game now, but most of what you see is a
**placeholder** until the finished drawings arrive:

- **`source/edgelord.webp`** is the photographed pencil drawing - the master copy, never edited.
- **`source/edgelord_colored.png`** is our colouring of it (`tools/art/color_edgelord.py`),
  done on request: thicker, darker lines, new glowing eyes, teeth, outlined scars.
- **`parts/`, `poses/`, `rig.json`** are cut from that colouring by
  `python tools/art/cut_edgelord.py` (about a minute). The V is the Torso, as Circy's ball is;
  the turning frames for his spin hang on the Torso as extras (`turn0`..`turn4`); every sword
  is ours (`tools/art/edgelord_extras.py`, which also writes review sheets to `draft/`).

When his own coloured drawing and move drawings arrive, they replace all of this. The cut
follows shapes traced from **this** drawing, so the new art needs its own cut - ideally sent
as layered PNGs, one part per layer.

**He is drawn facing left.** Fighters face right in their own space, so the cut mirrors the
whole drawing first (mirroring the rig instead would mirror every pose too, and he would run
backwards). After mirroring, the limbs on the right are the front ones.

**His arms curl, so each elbow and knee sits where the drawn limb crosses halfway** - not on the
straight line from shoulder to hand. On that line the joint was in empty paper beside the arm,
and his far forearm swung loose of the upper arm (Eric, 2026-10-04).

His normals each put a different weapon in his hand (`MoveData.PropArt`, naming a sword in
`poses/`), or empty it to summon one. Every weapon is drawn 1.4 times the size of the review
sheet, so it reads at match scale.

## `doombot/` - a robot drawn by Eric, cut from a photo of the drawing

`source/doombot.jpg` is the photo, the master copy. `python tools/art/cut_doombot.py` cuts it
into `parts/` and `rig.json`: each part is a traced shape with the paper removed by flooding in
from outside the shape, so the cut stops at his outline. He faces the viewer; the legs on the
right of the page are the front ones, and the arm on the LEFT of the page is the front arm (see
below).

**One thing is filled in.** His right arm (left of the page) is drawn across his body, coming
out of the socket on his chest. Cut free, it leaves a hole in the body where nothing was drawn,
so that patch is filled with grey cloned from the body just below it and the body's outline is
carried on through it - filling in what is missing, not replacing what is there.

**That arm is his front arm; the other is behind his body.** The arm out of the socket is the
near arm: it pivots at the middle of the socket, and the socket is lifted out of the body as an
extra (`"always": true`) drawn over the arm's root, so wherever it swings it comes out of the
hole. His left arm, out from his side, hangs behind his body like every far arm, darkened a
little. (Both arms once drew in front, which read as two front arms; then the wrong one was put
behind. Eric's calls, 2026-10-04.)

**His boots are feet.** Each boot is cut as its own piece and hung on the end of its shin at
the ankle (`"foot": true` extras), so the game keeps it flat on the floor while the knees bend
and he stands balanced over his feet. **His legs have round caps** at the hip and the knee - the
hip cap filled with leg cloned from just below it, hidden behind his body at rest - so a leg
swung out on a kick never shows a gap at the joint. Eric's call, 2026-10-04.

**He moves like a machine** (`FighterData.Robotic`): every animation plays in beats - smooth
motion, a complete stop, smooth motion again - and he walks with a stomp, the walk stopping on
each step: dust, a thud and a jolt with every footfall. Eric's calls, 2026-10-04.

**He cannot roll** (`FighterData.CanRoll`): block plus a direction keeps him blocking. Eric's
call, 2026-10-05.

**His pocket missile comes out of the socket** (`"socket"` in `"points"`), with his arms held back
and his chest pushed out, and is sized to him.

**His outline is thickened.** Every piece has a dark ring round its outside edge (`OUTLINE` in
the cut script, about two pixels on screen), so he stands out on any stage - Eric's call,
2026-10-04, on his own drawing. It is drawn round each whole shape before a limb is halved, so
no line crosses a knee or elbow, and never round the thin antennae. `OUTLINE = 0` and a re-cut
takes it off.

**His colours are levelled.** The photo is pencil under room light and he came out dull grey
next to everyone else, so the cut takes each piece's colour from a levelled copy of the photo -
black point, white point, the mid-greys lifted and a little more colour (`LEVELS_*` and
`SATURATION` in `tools/art/cut_doombot.py`). The paper is still found on the photo as taken, so
the cut does not move, and `source/` is untouched. Eric's call, 2026-10-04.

**Named spots.** `rig.json` also names three points on his head (`"points"`): his round grille
`eye`, which the eye laser comes out of, and the tips of his two antennae, which the up smash's
lightning arcs from. They ride on the head bone, so they follow every pose.

He is a heavy, built from Eric's own character sheet - `sheet.md` in this folder. He plays at
1.75 times the size of everyone else (`FighterData.DoomBot`): his body box grows with him, and
every hitbox is grown to match (`MoveData.ScaleReach`), so the hit stays on the claw or boot.
`rig.json` also marks where each claw starts on its forearm (`"hand"`), so the grab stretches his
arms but not his claws.

## `triguy/` - Elim's second fighter, sent in through ecec.dev/draw

The first fighter to arrive through the submission form, on 2026-10-01: eight drawings and the
answers, now `sheet.md` here. Elim drew him on a tablet, so `source/` holds the drawings
themselves, renamed from `photo-1.png`... to what each one is:

| file | what it is | used as |
|---|---|---|
| `triguy.png` | him, side-on, facing right | the rig: `parts/` and `rig.json` |
| `taunt_pose.png`, `taunt_surprised.png`, `taunt_pog.png` | his three taunts | held poses, picked at random - his taunt and his neutral special |
| `crying.png` | crying into a puddle | held pose for the down special |
| `spike.png` | a spike | what comes up out of the puddle |
| `trampoline.png` | his trampoline | the up special |
| `outline.png` | a jagged outline | drawn behind him while he is invincible |

`python tools/art/cut_triguy.py` cuts them. He is a white triangle with a black top hat on stick
limbs: the body is the white fill grown out to take in its outline and face, plus the hat (the
big stroke above it), plus the two sharp corners of the triangle that poke past the fill; the
four remaining strokes are the limbs, the two that reach lowest being the legs. Each limb hangs
from its top end, where it meets the triangle. Nothing is redrawn.

The taunt and crying drawings are front or three-quarter views of the whole figure, so they are
sized by height rather than by a body part: `figureHeight` in `rig.json`, on the rig and on each
pose, makes every pose stand as tall as the puppet (`FighterRig.PoseScale`).

## The canonical orientation contract

`rig.json` and `FighterRig.cs` agree on this, and nothing works if it is broken:

- every limb segment is stored pointing **straight down**, pivot at its top
- the torso points **up**, pivot at the hip
- the head sits **above** a pivot at the base of the neck
- a bone rotation of **zero** therefore means "hanging straight down"

That last line is the one that matters. Because rest is the same pose for every fighter, one
animation library drives all of them regardless of what pose each drawing was made in.

A far arm hangs from the Torso bone, and a child would normally draw over its parent's own
sprite - in front of the body. `FighterRig` moves every back-limb bone ahead of its parent's
sprite, so far arms always come from behind the body.

`bones` is listed in **back-to-front draw order**. Godot renders a node and then its children in
order, so the manifest's ordering is what puts the far limbs behind the torso and the near limbs
in front of it, with no z-index handling anywhere.
