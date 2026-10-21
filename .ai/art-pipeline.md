# Art pipeline

How a photograph of a drawing on paper becomes a fighter that moves. Read before touching the
rig, the import tool, or anything under `fighters/`.

## The approach: cutout puppetry

The drawing is sliced into body parts and each part becomes a sprite hung on a bone. Animating
means rotating bones, the way a paper puppet with brass fasteners moves. Nothing is redrawn and
nothing is traced — the pixels in the game are the pixels he drew.

The alternative, hand-drawing every animation frame in his style, was rejected: a full moveset
is well over a hundred frames per character, which puts the cost of a new fighter far beyond
what a kid will sustain, and it means *we* end up drawing most of it in an imitation of his
style. Cutout puppetry is slightly stiffer and completely authentic, and stiffness reads as
charm here rather than as a defect.

## One skeleton for everybody

**Every fighter rigs to the same bone hierarchy.** This is the most important decision in the
project and everything else depends on it.

```
Root
└── Hip
    ├── Torso
    │   ├── Head
    │   ├── ArmBack_Upper  → ArmBack_Lower  → HandBack   (prop socket)
    │   └── ArmFront_Upper → ArmFront_Lower → HandFront  (prop socket)
    ├── LegBack_Upper  → LegBack_Lower  → FootBack
    └── LegFront_Upper → LegFront_Lower → FootFront
```

Because the skeleton is fixed, **the animation library is authored once and every fighter gets
all of it for free.** Animating a new character is not a task that exists. A new drawing
inherits idle, walk, run, jump, fall, land, block, dodge, hurt, tumble, and every attack
animation the moment its parts are on the bones.

The cost of this decision is that every fighter has a humanoid body plan. A drawing of a car or
a blob does not fit. That is an acceptable trade and is worth stating in the drawing guide up
front — but see "Non-humanoid fighters" below for the escape hatch.

## Moving like a body, not a puppet

Rigid cutout parts on a fixed skeleton look like a puppet when every joint does the same thing at
the same moment, and broken when a joint goes where a body cannot. Eric's calls, all 2026-10-10:
movement looked puppeteered and silly; feet left the floor on a jab; arms ended up facing
completely backwards and knees bent backwards; fire pointed every way; hits stopped at the end of
the limb. The rules below are what came of it. Follow them when adding or changing any pose,
clip, attack animation or effect drawn round a fighter; the rig enforces the ones it can.

### Rules for poses and animation

1. **Write the angle you want to see, divided by about 1.3.** Every limb angle is played bigger
   (`FighterRig.Drama`, and each fighter's `AnimationDrama` on top for the legs), and angles add
   up down the chain: the lean, plus the shoulder, plus the elbow. A windup leaning back with the
   arm up and the elbow bent points the forearm, and the weapon, straight backwards.
2. **Elbows fold the hand forward, knees fold the foot back, and neither goes past straight.**
   In pose terms: a forearm is 0 or negative, a shin 0 or positive (less than about 160 either
   way). The rig holds every pose, spring and solved limb to it, so a pose asking for more stops
   dead at straight - write the bend you mean.
3. **An arm never goes round behind.** At most 145 degrees behind straight down - out behind
   and up, as far as a body turning away takes a swing - and less for a fighter whose build
   says so (`FighterData.ArmReachBack`: Lug's is 70, because a back that broad does not let a big
   arm round it). A weapon gets overhead by going up the front (negative, on past -180 if it
   must), and an arm aimed by code wraps onto the front for straight up (`WideArcAt`). Swings
   for a fighter like that are written to stay in front of him: Lug's cross-body swings
   (`CrossSwing`, `CrossBackhand`), his two-handed thrust (`ToolThrust`).
4. **A limb pointing back faces back.** Do not fake it with a strange elbow or knee: the rig
   turns a limb on its own once it is swung far enough behind (Joints, below), and a turned
   limb's elbow or knee bends the other way. A star - Flambe's nair, the splits - is legs split
   and arms out both ways, each pointing its own way.
5. **Arms spread forward and back go a little below the shoulder.** Drawn three-quarter on, the
   near shoulder is on the far side of the chest, and an arm out level crosses the face.
6. **Feet stay on the floor unless the move is a kick.** A standard attack on the ground keeps
   both feet planted; a kick lifts only its kicking leg. A new kicking animation goes in the kick
   list (`FighterAnimations.UsesLegs`), or the solver will plant its feet.
7. **Punches go out straight.** An empty hand on a jab or tilt is carried from a guard to the hit
   on a line (`ThrowPunch`); do not pose a straightening arm by hand. A hand holding something
   keeps its swing, and the head of what it holds is what lands. A tool held in both hands says
   so (`MoveData.SecondHandAt`) and the back hand is solved onto it every frame - do not pose
   the second arm to meet it.
8. **Hitboxes sit on the business end** - ringed in `--parade --attacks`, which prints where the
   weapon head is. Never push one out to give a move range: a normal attack already reaches past
   its hitbox with its power, and its smear shows that reach (see Smears below).
9. **Keep the knees showing in a curl.** The rig draws the legs behind the torso; a thigh pulled
   tighter than the lean of the body disappears behind it.
10. **Fire rises.** Every procedural flame leans at most 50 degrees off straight up, whatever
    it is thrown off; a prop that burns never has its flame drawn on - it names a `flame` point
    and the game draws the flame rising from it. See `.ai/art-direction.md`.
11. **Leave the springs' damping cap at 1** (`FighterRig.Follow`). Past it a joint reverses every
    frame - a jitter - and a quick blend on a light fighter flung the body over.
12. **Look at it move.** A pose can look right and move wrong. `--parade --film=NAME --only=N`
    plays one thing frame by frame through the rig exactly as a match does, smears and all;
    `--film=ground`, `smash` or `air` shows each move's key moments; `--film=roll` films a dodge
    roll on a floor line; `--film=item:ID` a swung item in his fist. See CLAUDE.md.

### How the rig keeps them

**Springs and weight.**
- Joints follow on springs, from the middle out (`FighterRig.Follow`). Each joint chases its pose
  with its own stiffness: the hips and spine lead, the shoulder follows, the elbow after it and
  the hand last, each carried a little past where it stops and settling back. A strike's active
  frames still snap - hitlag freezes whatever is showing, so the hit must be fully out. A robot
  (DoomBot) snaps between held poses on purpose and does not use the springs.
- A heavyweight's springs are slower (`FighterData.Inertia`), so his limbs swing on further and
  settle later, and he breathes slower standing.
- Clips curve through their keys (`Pose.Curve`, Catmull-Rom) instead of easing in and out of
  each one, which stopped the whole body dead four times a stride.
- The coil before a hit and the carry after it are a little more of the same movement, capped
  at 16 degrees a joint (`Pose.Overshoot`). After the hit the limb keeps going, slowing, and is
  drawn back while still moving; nothing freezes at full stretch.

**Joints.**
- Elbows and knees are held to their range in the pose, against the springs, and again after
  anything solves a limb directly - a punch, a hand on a ledge or a handle
  (`FighterRig.BendOneWay`, `KeepBoneInRange`). Arms are held to `ArmBackLimit`, the fighter's
  own `ArmReachBack`, except while looking back on a back air.
- A limb solved onto a point (`ReachArm`) bends the way its elbow can: of the two ways to reach,
  the one the joint allows, then the one with the elbow lower. Elbow-down regardless bent a hand
  reaching for a tool in front of his chest backwards, and the joint straightened it off the tool.
  A second hand on a tool slides along it to a point an arm reaches comfortably
  (`FighterRig.SecondHand`): Lug's far shoulder sits right by his front fist.
- A limb turns - drawn mirrored about its own length, with the joint below, its foot and the
  front hand's prop carried across - when it swings more than 80 degrees behind straight down
  for an arm, or 65 for a leg in the air, and turns back once it comes forward past 55 (40), so
  a limb swinging near the line does not flicker (`FighterRig.TurnLimbs`, `ShowTurns`). On the
  floor a leg turns only past 95 - the splits - never for a lunge or a kneel, whose back foot
  points forward; a standing leg never does.

**On the floor.**
- Standard attacks stand: the legs are solved under him as if standing, the knees bending as
  the hips drop or shift into the hit (`FighterAnimations.StandsThrough`, `GroundAdjust`). A kick
  keeps its back leg standing (`KeepSupportLeg`), so a flip kick never hops.
- Punches: the fist travels from a guard in front of the shoulder toward the hit and lands on
  it, the shoulder and elbow solved to carry it, the other hand held up at the guard.

**Rolling.** A dodge roll curls into a ball (`Tuck`: back rounded, chin down, knees to the chest,
upper arms down his sides and forearms folded round the shins) and rolls along the floor: the
rig turns about the middle of the ball and sets it down each frame so whatever part of him is
lowest touches the floor (`FighterRig.SetRoll(angle, alongFloor: true)`). Turned about the
middle of the standing body, the ball swung up off the floor and back - a somersault in the air.
It turns in step with the ground it covers (`FighterAnimations.RollTurned`), so it rolls rather
than skids. A hammer on the shoulder stays there through a roll and counts as part of the ball.

**Smears.** Whatever strikes - a hand, a foot, the head of a weapon: whichever is moving fastest
as the swing comes through, then whichever is at the hit - is traced through the last few frames
of the windup and the hit, round the body as a swing goes, and the smear stretches on out of it
to the far side of the hit: wider, longer and bolder the stronger the move (`SmearTrail`), with
speed lines, and a shock of air off the front of a smash. The hit reaches as far as the smear
does (`SmearTrail.Reach`; see `.ai/fighting-design.md`). Specials, throws and a wheel of fire
round the body have none.

**Turning round** narrows the drawing to its edge and opens it out the other way over six frames
(`Fighter.TurnWidth`), instead of mirroring it in one.

**Squash and stretch are small** - a few percent. A cutout that stretches a fifth of its height
to jump is a rubber toy; the knees and the lean do that work.

## Parts, and the parts he will not draw

A kid draws one arm and one leg clearly and no elbows. The pipeline fills the rest in
mechanically:

- **Back limbs** default to a **darkened copy** of the front limb (`modulate` to roughly 70%
  brightness), drawn behind the torso in the layer order. Not mirrored — facing one way, both
  arms point the same way, so a mirrored copy reads as a broken elbow. This is exactly the trick
  traditional cutout animation uses, it costs nothing, and the darkening reads correctly as
  depth. If he did draw a distinct far arm, use it.
- **Elbows and knees** are almost never drawn. When a limb is boxed as a single piece, the tool
  **auto-splits it at its midpoint** into upper and lower segments. The seam is invisible
  because the two segments overlap slightly at the joint and the joint is where the pivot is.
- **Missing parts** — no neck, no hands, feet that are just the end of a leg — are fine. The
  rig does not require a sprite on every bone; an empty bone just has nothing drawn on it.

## Scale normalisation

Photographs will vary wildly in resolution and framing. On import, every fighter is scaled so
that **hip-to-top-of-head maps to a standard height** (target: 128 px), and the resulting
uniform fighters are then differentiated on purpose by `FighterData.VisualScale`. Without this
step, whether a character is big in-game is decided by how close he held the camera, which is
both arbitrary and unfixable later.

## Facing

**Fighters are drawn in three-quarter view, facing right** (Eric's call, 2026-10-21; until then
the guide asked for side view, in profile). The body turns a little toward us but clearly points
right: we see his front and his right side, both eyes on the face - the far one a little smaller
and nearer the edge - and the nose pointing right of centre, its tip just past the outline. It is
how Circy and EdgeLord were drawn, and the fighters already in the game stay exactly as they were
drawn; the rule is for new drawings. Facing left is still a horizontal flip of the whole rig,
which is correct for a drawing that points one way - the character genuinely turns around rather
than appearing to moonwalk.

In a platform fighter the single most important thing to read at a glance is *which way a
fighter is pointing*, because that is what decides whether an attack is about to hit you. A
front-facing puppet is ambiguous about this in every frame. A three-quarter drawing is not -
the eyes crowd toward the front, the nose breaks the outline on that side, the feet point that
way - and unlike a strict profile it shows the whole face, which a story scene and a win need
(see below). It is also closer to how kids draw a person anyway, so it asks less of them.

**What it means for cutting.** Facing right, his near side (his right) is on the left of the
picture and the far side on the right. The near arm and leg are in front of the body and cut
whole. The far arm and leg are **partly behind the body**: cut as much as shows and fill in the
hidden end (rule 1 - fill in what is missing, never replace what is there), and if a far limb
barely shows, fall back to the darkened copy of the near one (above). Both eyes are on the face,
so `tools/art/faces.py` lifts both out with the mouth (see "Faces"). This is why the guide still asks for the arms and legs spread apart, not drawn over the body: a
striding pose separates all four limbs, near pair in front and far pair behind, without the
drawing looking like a starfish.

**Always ask for the character facing right.** Consistency matters more than which direction —
a left-facing drawing has to be flipped on import, which flips any text, asymmetric details, or
handedness he drew deliberately.

**The face has to show.** A story has everyone talk, and a win has them celebrate, so the
whole face reads best turned three-quarter toward the camera while the body still points right
- the way Circy, Triguy and EdgeLord were drawn (Eric, 2026-10-15), and why it became the rule. The two we draw
ourselves,
Flambé and Lugnut, are drawn that way: the face across the front of the head, both eyes on it,
the ear at the back, and only the tip of the nose past the outline, which is what keeps which
way he faces unambiguous. DoomBot's head and body are drawn side on, so the cut turns them
three-quarters toward us (see "A side view turned toward us", below). His arms are drawn spread
wide. He fights side on as every rig does; for standing in a story scene and for celebrating he
stands with his arms out as drawn (`FighterData.FrontOn`, `FighterRig.SetFrontOn`): the far arm comes
out in front of the body unshaded, no limb turns to face back, an elbow bends either way across
the picture, and no pose is exaggerated, so his arms go where he drew them. Side-view story
poses are turned face on for him (`FighterAnimations.FaceOn`); the ones that fold an arm across
the body have their own.

## The import tool

An `EditorPlugin` dock written in C#, living in `addons/fighter_importer/`. Not a Python script:
this machine has only Python 2.7 with PIL and no numpy, which makes per-pixel work slow and
awkward, and keeping the tool in C# inside the editor means it can generate scenes and resources
directly and can eventually be handed to him to use himself.

**It has two input paths, and they are not equally good.** Which one a drawing arrives on is
decided by how it was made, and the drawing guide now pushes tablet users toward the good one.

### Path A: layered art (preferred)

A drawing made on a tablet with **one body part per layer**, exported as separate PNGs. Almost
every tablet app can do this - Procreate, Krita, Photoshop, Clip Studio all have some form of
"export layers as individual files".

This path **skips the two hardest and most error-prone steps entirely**:

- There is no background to remove, because a layer exported with transparency has none.
- There is no cutting, because the layers *are* the parts. Nothing can be cut in the wrong
  place, so the fighter comes out exactly as it was drawn.

It also hands us something unexpected for free. Apps export layers at **full canvas size** with
each part sitting where it was drawn, so the parts arrive already in their correct positions
relative to one another. The bone offsets can be computed from the layer contents rather than
measured by hand.

What the tool still has to do:

1. **Match layers to bones** by filename, case and punctuation insensitive, so `front arm.png`,
   `Front_Arm.png` and `armFront.png` all land on the same bone. Anything it cannot match gets a
   dropdown, because a nine-year-old naming layers is not a spec.
2. **Auto-crop** each layer to its own non-transparent pixels, recording the offset it trimmed.
3. **Guess the pivots**, which is newly possible: each part is already isolated, so the joint is
   the end of the part nearest its parent bone. Guessing then letting someone nudge is far less
   work than placing every pivot from scratch.
4. **Rotate each part to canonical orientation**, since a layer is drawn in a pose and the rig
   expects limbs pointing down. The angle comes from the part's own long axis.

**Do not make a PSD parser.** Godot reads PNG and not PSD, and a folder of exported PNGs is
both easier for a kid to produce and trivial to read. If a single-file format ever becomes
worth it, OpenRaster (`.ora`) is the one to add - it is a zip of PNGs plus an XML manifest, and
Godot has `ZIPReader`.

### Path B: a flat image

A photograph of paper, or a tablet drawing exported as one flattened picture. This is the
original flow and it stays, because paper is still how most of these will arrive.

The flow:

1. **Load the photo.** Any JPEG or PNG.
2. **Remove the background.** Luminance threshold plus a flood fill inward from the four
   corners, then a despeckle pass to kill paper texture and JPEG noise. A threshold slider with
   a live preview, because paper and lighting vary and no single value works everywhere. This
   is why the drawing guide insists on a dark marker on white paper — it turns background
   removal from a hard problem into a slider.
3. **Box the parts.** Drag a rectangle around each part and assign it a bone from a dropdown.
   Boxes may overlap; each part is masked to its own box.
4. **Place the pivots.** Click the joint location on each part — the shoulder on an upper arm,
   the elbow on a forearm. **This step decides whether the animation looks right**, and it is
   the one part of the process a machine cannot guess well. Budget UI effort here: show a live
   rotation preview so a bad pivot is obvious immediately.
5. **Generate.** Writes the sliced part PNGs and a `rig.json` manifest, plus a
   `FighterData_<Name>.tres` stub with default stats ready to be filled in.

### Drawn in the game

The game has its own drawing screen (`DrawScreen`), and what it sends in is Path A input already:

- **Every picture is the whole page** - 1400x1050, see-through where nothing is drawn - so the
  layers of a page line up exactly, the way a tablet app exports them.
- **Fighter-page layers are named by button**, not typed: Head, Body, Front arm, Back arm, Front
  leg, Back leg, Held thing. Those match bones by name with no guessing.
- **Other pages say what they are**: something they shoot or throw (drawn flying right),
  something they hold (drawn upright, handle at the bottom - the items' convention), or
  something else.
- It goes through the same intake as the website form, so it lands in `fighters/incoming/` like
  any other submission. Each picture is saved as `photo-N-` and its page and layer name
  (`photo-3-fighter-front-arm.png`), and the game appends a list to the "Anything else" answer
  saying what each page is for, so that is in `sheet.md` and `answers.json` too. The server takes
  **at most 24 pictures** (60 MB in all), so a kid can send any page as one flattened picture
  instead of one per layer.

The drawing screen never smooths, straightens or tidies a stroke - see rule 1.

**Both paths converge on step 5.** They differ only in how the parts and pivots are obtained, so
everything downstream - the manifest, the rig, the animation library - never learns which path a
fighter came in on.

## Feet, hands and empty hands

- **Planted feet.** On the ground the rig moves the body so the lowest point of the **ink** of
  either lower leg or forearm - a sole, a knee in a kneel, a hand hanging low in a crouch - sits
  exactly on the floor
  (`FighterRig.SetPlanted`, eased in and out). The ink, not the picture's corners: a boot turned
  up on its toe otherwise hovered by the empty paper behind its heel. The outline is read from
  the image once (`FighterRig.InkEdge`) and shared. Poses therefore never float a fighter, and a pose
  with bent legs is genuinely lower. The parade plants its standing columns the same way, so
  what it shows is what a match shows. `FighterRig.Sole` gives where a boot is right now - the
  rocket jets come out of it.
- **Hands that do not stretch.** A forearm part may carry `"hand": row` in rig.json: the texture
  row where the hand starts. A stretched arm then stretches only the arm above that row and
  draws the hand at its own size on the end. The cut script writes it from a pixel picked off
  the photo, like the joints. (DoomBot's arms no longer stretch at all - stretching is Circy's -
  they slide out on telescoping steel rods with the forearm unchanged on the end.)
- **One leg stretched.** `FighterRig.SetFrontLegReach` stretches the front leg alone, the way
  `SetLegStretch` stretches both, and `AimFrontLeg` points it at a spot over whatever the pose
  is doing - Circy's kicks reach their hitbox. Only scaled along its length, like every stretch.
- **Knees on the line.** A knee (or elbow) found by halving a limb goes where the drawn stroke
  crosses halfway down, not on the straight line from hip to foot. Circy's front leg is bowed;
  a knee on the straight line turned the lower half about empty paper and it came away from the
  upper half whenever the knee bent (`knee_on_stroke` in `tools/art/cut_circy.py`).
- **A second copy of a rig** is fine and cheap: DoomBot's MiniBot is his own rig, loaded again
  and normalised small, playing the run clip.
- **An empty hand.** A fighter with no weapon of its own can still have a `PropFront` part, marked
  `"empty": true`: a blank, so a move can put a prop in his hand (Flambe's lighter) the way Lug
  swaps his tools.

## The rig is a Node2D tree, not a Skeleton2D

An earlier draft of this document said `Skeleton2D` with `Bone2D`. That was wrong and the built
rig does not use it. `Skeleton2D` exists to **skin and deform** a `Polygon2D` — to bend a mesh.
Cutout parts are rigid: they rotate about a joint and never deform. A plain `Node2D` hierarchy
with a `Sprite2D` on each bone does exactly that, with less machinery and no skinning setup.

The trick that makes it work is the sprite offset. Each part's `Sprite2D` is `Centered = false`
with `Offset = -pivot`, which puts the joint at the bone's origin — so rotating the bone swings
the part about its joint rather than about its corner. Get that wrong and limbs pinwheel around
their own top-left corner, which is the most likely way a new fighter looks broken.

**Named spots.** A manifest may name points on the drawing that a move comes out of -
`"points": [{"name": "eye", "bone": "Head", "offset": [x, y]}]`, the offset in canonical units
from that bone's joint. `FighterRig.PointGlobal(name)` returns where one is right now, so it
follows the pose. DoomBot's eye laser fires from his `eye` and his up smash arcs from
`antenna_back` and `antenna_front`. A move that names a spot a drawing does not have falls back
to the hands or the head, so naming them is never required. The cut script writes them, from
pixels picked off the photo, exactly like the joints.

`FighterRig.cs` builds this tree from `rig.json`. It returns false rather than throwing when a
manifest is missing, so a fighter with broken art still plays as a rectangle instead of taking
the whole match down with it.

## Faces

A fighter's eyes, brows and mouth move: they blink, glance about, squint winding up a hit, open
as it lands, squeeze shut when hit and go wide when launched (`FighterFace.cs`), and talk in a
story. **Nothing is redrawn to do it.** Each feature is lifted out of the drawing whole, pixel
for pixel, and hung back exactly where it was as its own part (a rig.json extra with a `face`
kind: `eye`, `brow`, `mouth`, or `mouthOpen`), so at rest the face is precisely the drawing. The
game only squashes, tilts, nudges and stretches those marks (`FighterRig.SetFace`): a blink is
the eye squashed flat, a shout the mouth stretched open down from its top. Eric's call,
2026-10-11.

- **Lifting them** (`tools/art/faces.py`): each cut script's last step. A feature is found
  inside a box by what it is made of - dark ink, strong colour, a glow, teeth - taking only the
  marks wholly inside the box, or under the points it names, so an outline passing through stays
  on the body. The boxes are in `tools/art/face_specs.py`, in the pixels of the cut part.
- **What is left behind** is filled in, which is allowed (rule 1: fill in what is missing): the
  face's own flat colour, or, on a pencilled or hatched face, the texture grown in from round it.
  It only shows while a feature is squashed or stretched off its spot.
- **A mark joined to the outline stays.** Never cut a line apart to free a feature. (Triguy's
  front eye only touches his outline once the line is widened, so his face is lifted before it
  is - by `tools/art/cut_triguy.py` itself, as below.)
- **A face on a body that is turned** (Triguy's, Eric's call 2026-10-19): his body is drawn lying
  on its side and stood on end to make the rig (Tipped over, below), so his eyes and mouth are
  lifted off the drawing first, mark by mark, and hung back level - at rest the face reads as
  drawn, not on its side. The stood-up triangle has less room across it than the drawing had
  along it, so standing the marks are drawn a little closer together and shown smaller
  (an extra's `"scale"`, 75% for him); tipped back over into the drawing they are full size and
  exactly where he drew them.
- **A mouth opens only as far as the drawing has room** under it (`stretch`): Triguy's and
  DoomBot's barely, Circy's more.
- **Flambé and Lugnut are ours**, drawn in SVG, so their faces are drawn as separate parts in
  their own scripts (`FACE` in `flambe.py` and `lugnut.py`) - with an open mouth (`mouthOpen`),
  shown instead of the closed one when they shout or talk.
- **A new fighter gets a face** when they are cut: add their boxes to `face_specs.py` and check
  every expression with `--parade --film=face --only=N`.

## Inspecting a rig

A build cannot tell you whether a puppet is assembled correctly, and neither can squinting at a
120-pixel fighter mid-match. There is a parade view for this:

```
"$GODOT_BIN" --path . -- --parade --shot=70
```

It lays out every fighter in every animation the shared library contains, side by side and
large, and writes a screenshot to `.shots/`. A wrong pivot or a flipped rotation sign is obvious
there and invisible everywhere else. `--shot=N` works on the match too, and `F12` grabs a frame
while playing.

The tool should also be able to **re-import over an existing fighter**, preserving the hand-tuned
`FighterData` numbers, so that a better photo or a re-cut does not throw away balance work.

## The world is paper

**The background is cream, the platforms are ink outlines, and the HUD is dark.** This was not
a style choice made up front — it was forced by the first rigged fighter. His art is dark marker
on a white page, and on the dark stage the game originally had, both fighters simply vanished.

Turning the world into the page they were drawn on fixes the readability problem and is a better
idea than the thing it replaced: everything in this game is a drawing, so the world should look
drawn too. It also means new art needs no adaptation to be legible — whatever he hands us is
already the right value range against the background.

The consequence to remember: **anything added to the world has to be dark-on-light.** A dark
background element, a white particle, or a light-coloured UI label disappears. This has already
happened twice (stock icons and status text were invisible white-on-cream on their first pass).

## Effects art

Special-move effects — a fireball, a lightning bolt, a giant fist — are drawn **separately, on
their own page, large**. They import as plain sprites with no rigging and are referenced by
`MoveData`. This is deliberately the fun part of the art job: he gets to draw explosions instead
of idle poses, and it is where a character's signature move gets its personality.

Animated effects can be two or three drawings of the same thing played in sequence. Three frames
of a hand-drawn fireball looks better than any amount of procedural polish here.

## Non-humanoid fighters

He will eventually draw something that is not a person — a dragon, a car, a floating eyeball.
Rather than generalise the skeleton, which would cost the shared animation library, the escape
hatch is a **single-sprite fighter**: one unsliced image, animated by squash, stretch, rotation
and offset rather than by bones. It supports the same movesets and the same `MoveData`; it just
animates more simply. Defer building it until he actually draws one, then build it as its own
small path rather than bending the rig.

## Rules

- **Never hand-edit a sliced part PNG.** Re-cut from the source photo. Original photographs are
  kept under `fighters/<name>/source/` and are the master copy.
- **Never redraw, clean up, smooth, or "fix" his linework.** Fill in what is absent; do not
  replace what is present. This is pillar 1 of the project and it is the whole point.
- Colour correction is limited to what makes the art readable on the stage background — a
  levels nudge so pencil is visible, never a restyle.
- **Line weight is the one other readability step**, and only for a drawing whose line vanishes
  at match size (Elim's: under a pixel). The cut script widens every line of ink by the same
  amount, in place, with a round pen (`tools/art/linework.py`, `LINE_BOOST` in the cut script) -
  aiming for about 2.5px at match size. It keeps every line's path and touches only the generated
  parts and poses, never `source/`. Never use it to tidy, smooth or join anything. Eric's call,
  2026-10-04.
- **One redrawn foot: Circy's front one** (Eric, 2026-10-10, who allowed editing the drawing for
  this). Elim drew that leg bowing out and then hooking back in on a diagonal with a tick on the
  end - a foot angled down and back that never stood flat, however the rig turned it. The cut
  script takes off the hook below where his line stops coming straight down and draws the leg on
  down to the floor his back foot stands on, with a flat foot as long as that one, in the same
  black marker at his own line width (`redraw_front_foot` in `tools/art/cut_circy.py`). It works
  on the copy the parts are cut from; `source/` is untouched. It is the only redrawn line in any
  kid's fighter: do not take it as leave to redraw another without being asked for that one.
- **Legs of two lengths, evened** (`"evenLegs": true`, Circy's): his front leg is drawn longer
  than his back one, so standing on both it had to bend far forward at the knee, both feet ended
  up out ahead of his ball and he looked to be falling over backwards (Eric, 2026-10-15). The
  rig stretches the shorter leg along its own length (`FighterRig.EvenUpLegs`) - the way his
  Stretch does, no line redrawn - until both stand straight with their feet flat, under him.
- **Coloured in where the drawing is white** (Triguy's, Eric's call 2026-10-15): at match size
  his eyes, teeth and hat ran into the white of his body. `colour_in` in
  `tools/art/cut_triguy.py` colours the one big white shape in each drawing in white pencil - the
  stand-ins' coloured-pencil recipe (`drawn.pencil_coverage`), white strokes over a pale grey
  tooth, a shade deeper round its rim - so he stays white, as Elim drew him, but reads as a
  coloured-in body rather than a hole in the page (Eric, 2026-10-16; for a day it was light green); fills the gaps his mouth's own lines close in white, in the mouth piece,
  so the teeth stay white as it moves; and puts a sheen along the top edges of his hat - the
  biggest thick black shape, never a line - so it reads as a shiny top hat. Not one of his lines
  moves or changes: only what he left white or filled black is coloured, after the cut.
- **Team colours** (Eric's call, 2026-10-17): in a team fight a fighter's own colour turns to his
  team's, at match time, by a shader on his parts (`TeamLook`, `FighterData.TeamHue` and
  `TeamGrey*`): one hue turned, or a white or grey body tinted - never black, so no line changes,
  and on the team that is already his colour he stays exactly as drawn. Nothing under `fighters/`
  is touched.
- **EdgeLord's thickness** (Eric's own drawing, his call 2026-10-15): he drew the right-hand
  edge of the V doubled down to his arm, giving the body depth; `color_edgelord.py` carries a
  second line on down from there to the point of the V, at the same gap (`edge_depth_line`).
- **A leg cut flush with the body gets a cap**: EdgeLord's legs tuck up behind his V, so cut
  along its edge they pivoted on a corner of the cut and swung clean off him (Eric, 2026-10-15).
  Each now turns about the middle of where it meets the body, and is carried on up past the cut
  with leg cloned from just below into a rounded cap, hidden behind the body at rest
  (`capped` in `tools/art/cut_edgelord.py`) - DoomBot's round joint caps, for a diagonal leg.
- **Feet cut separately** (DoomBot's boots): a foot is an extra on its lower leg pinned at the
  ankle, marked `"foot": true` in rig.json. The rig keeps it level as far as the fighter is
  planted (`FighterRig.LevelFeet`) - an ankle, without a new bone - and, standing, solves thigh
  and shin between hip and a fixed ankle (`StandOnAnkles`), so he balances over his feet. In the
  air a foot follows its shin, so a kick still points its boot. And an ankle only bends 25
  degrees: a shin swung further takes its foot with it, so a leg stretched back in a lunge
  stands on its toe and a kick leads with its heel, rather than a level boot meeting a nearly
  flat shin side-on (Eric, 2026-10-09). A tall boot shaft belongs on the shin, not the foot, so
  it leans with it - Flambe's.
- **Legs out of the body** (`"legsOnBody": true`): legs that come out of the body's edge well
  away from the hip - Triguy's, out of the bottom of his triangle - have their roots carried round
  as the body tips, or the body swings away from them. Their swings are untouched; only where
  they start moves. A body turned round to look back (a back air) takes every limb's root round
  with it.
- **Tipped over, the drawing** (`"tipped"`; Triguy's, Eric's call 2026-10-19, from Elim:
  "triguys idle is STILL his stab", "he's supposed to be tall"): a fighter drawn in his attack
  rather than standing. Elim drew Triguy with the triangle on its side, point forward - his
  stab - and every drawing of him standing has it on end, point up, hat on the point. So
  `cut_triguy.py` stands the body on end (only turned), hangs the hat on the point (turned
  level), the legs from the bottom and the arms from the sides, and writes in `"tipped"` where
  each of them, the body and the face are on the drawing as drawn. As the body tips forward past
  `"from"` degrees, `FighterRig` slides each piece from where it hangs standing to its place on
  the drawing, and at `"angle"` (about 98 degrees, the turn that stood him up) he is exactly the
  drawing again - point out in front, hat on the back corner, legs out of the underside, face
  level. His point attacks (`AttackAnim.PointThrust`) tip him that far; ordinary leans stay
  below `"from"` and keep him standing. Nothing is redrawn - pieces are only turned and moved.
- **A shin with no flat sole**: a leg whose bottom is not a foot laid flat - a diagonal with a
  tick on the end, as Circy's front leg was before it was redrawn - says so with `"soleSlope": 0`
  on its part, and stands as drawn. Without it the diagonal is read as a sole and laid flat on
  the floor. Nobody uses it now. Floor contact, the sole a jet comes out of, stance width and
  portrait framing all count the feet.
- **Round joint caps** (DoomBot's legs): each half of a limb keeps a disc of the limb round the
  joint, and the thigh's disc round the hip is filled with leg cloned from just below it (hidden
  behind the body at rest) - so a leg swung far out turns one round end over another instead of
  opening a notch. A cut-script job (`canonical(..., cap=)` in `tools/art/cut_doombot.py`).
  Eric's call, 2026-10-04: his legs came visibly away from his body on his kicks.
- **A piece a move takes off him** - Triguy's top hat, which he throws - is masked off his
  drawing as its own part and hung back on as an extra on the body's own drawing
  (`"onPart": true` in rig.json), so it turns round with the body when he looks back and counts
  as body for framing. Its pixels are his; it is only kept apart, never redrawn. A copy goes in
  `poses/` for the thrown one.
- **An outfit** - something worn over a drawing by only some fighters who use it: DoomBot Mk. II
  is DoomBot's rig with scrap armour bolted on (Eric, 2026-10-15). The armour is our drawing,
  in separate pieces (`tools/art/mk2_armour.py`), each drawn about the joint of the part it goes
  on so it hangs at offset zero; rig.json extras with `"outfit": "mk2"` stay hidden until a
  fighter's data wears them (`FighterData.Outfit`, `FighterRig.Wear`). `"behind": true` puts one
  under the part it hangs on (his exhaust stacks, behind his body), and a rig point with an
  outfit (the stack tops, where his heat smoke comes out) is only there while it is worn. It
  covers part of the drawing it is worn over, so keep it off the face and anything a move comes
  out of - his armour skips the socket on his side - and never paint it into the artist's
  parts.
- **Specks are not part of a piece.** A fleck of pencil on the paper beside a cut piece, not
  joined to it, gets its own outline ring and shows as a dark nugget stuck to the part - DoomBot's
  boots and shins (Eric, 2026-10-10). `solid` in `tools/art/cut_doombot.py` drops any blot not
  joined to the piece and under 3% of its size before the ring is drawn. It only ever removes
  what is not attached; nothing of the drawing itself is touched.
- **An outer outline** - a dark ring round each piece's outside edge, drawn behind it - is the
  same kind of step for a drawing whose edge is faint pencil. DoomBot only, on his artist's own
  request (`ringed` in `tools/art/cut_doombot.py`): ring each whole shape before cutting it at a
  joint, so no line crosses an elbow or knee, and leave thin details (his antennae) out. Ask the
  artist before giving anyone else's drawing one.
- **A side view turned toward us** (DoomBot, Eric's call 2026-10-21, on his own drawing at his
  request; not a precedent). Eric drew DoomBot's head and body side on, which was misread twice.
  The red slash with the green tip is an antenna on the side of his head (his ear), the round
  grille eye is his right eye, and the teeth run to the front edge of his face. On the body, the
  near arm comes out of a shoulder socket on its side and the far arm comes off its front edge.
  A story needs the whole face, so `turn_head` and `turn_torso` in `tools/art/cut_doombot.py`
  turn each box three-quarters toward the camera, still facing right. Eric picked 60/40 from the
  three proportions in `tools/art/doombot_front_mockup.py`.
  - **His side, 40% of the turned width:** the drawing left of the corner, squeezed, with its
    back edge a little shorter. It keeps the ear, the socket and the hatch.
  - **A new front, 60%:** filled with his own grey pencil, tiled. The head's front carries his
    eye as drawn nearer the corner, a mirrored copy of it at 88% as the far eye, each with its
    brow, and his teeth (the left half, mirrored) as a mouth across it. The body's front carries
    his block of controls exactly as drawn.
  - **New lines:** only the edges of the new fronts and the corner, in his marker colour at his
    line width.

  The turned box is 12% wider than the drawn profile and as tall, so his size and hurtbox do not
  change. The cut moves everything that hangs on him with the turn: the near shoulder to the
  socket on the squeezed side, the far shoulder to the front's right edge, the hips spread
  across the whole turned body, and the neck under the middle of the head. The `eye` point
  (the laser) goes on the near eye, and the antenna tips go where the antennae now stand. Both
  eyes, both brows and the mouth are lifted out as face pieces (`EyeNear`, `BrowNear`, `EyeFar`,
  `BrowFar`, `Mouth` in `tools/art/face_specs.py`; the cut prints their boxes). The ear stays on
  the side. Mk. II's helmet band and belly plate go round the corner with it.
