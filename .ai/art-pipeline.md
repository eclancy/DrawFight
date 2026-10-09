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

## Parts, and the parts he will not draw

A kid draws one arm and one leg clearly and no elbows. The pipeline fills the rest in
mechanically:

- **Back limbs** default to a **darkened copy** of the front limb (`modulate` to roughly 70%
  brightness), drawn behind the torso in the layer order. Not mirrored — in profile both arms
  point the same way, so a mirrored copy reads as a broken elbow. This is exactly the trick
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

**Fighters are drawn in side view, in profile, facing right.** Facing left is a horizontal flip
of the whole rig, which is exactly correct for a profile drawing — the character genuinely turns
around rather than appearing to moonwalk.

Side view is somewhat harder for a kid to draw than a front view, and it is worth it. In a
platform fighter the single most important thing to read at a glance is *which way a fighter is
pointing*, because that is what decides whether an attack is about to hit you. A front-facing
puppet is ambiguous about this in every frame. A profile is never ambiguous.

Side view also solves a rigging problem for free: in profile the near arm and far arm are
naturally offset from each other rather than symmetrically flanking the body, so a walking or
striding pose separates all four limbs without the drawing looking like a starfish.

**Always ask for the character facing right.** Consistency matters more than which direction —
a left-facing drawing has to be flipped on import, which flips any text, asymmetric details, or
handedness he drew deliberately.

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
  `"empty": true`: a blank, so a move can put a prop in his hand (Flambe's frying pan) the way Lug
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
- **Feet cut separately** (DoomBot's boots): a foot is an extra on its lower leg pinned at the
  ankle, marked `"foot": true` in rig.json. The rig keeps it level as far as the fighter is
  planted (`FighterRig.LevelFeet`) - an ankle, without a new bone - and, standing, solves thigh
  and shin between hip and a fixed ankle (`StandOnAnkles`), so he balances over his feet. In the
  air a foot follows its shin, so a kick still points its boot. And an ankle only bends 25
  degrees: a shin swung further takes its foot with it, so a leg stretched back in a lunge
  stands on its toe and a kick leads with its heel, rather than a level boot meeting a nearly
  flat shin side-on (Eric, 2026-10-09). A tall boot shaft belongs on the shin, not the foot, so
  it leans with it - Flambe's.
- **A shin with no flat sole**: a leg whose bottom is not a foot laid flat - Circy's front leg, a
  diagonal with a tick on the end - says so with `"soleSlope": 0` on its part, and stands as
  drawn. Without it the diagonal was read as a sole and laid flat on the floor. Floor contact, the sole a jet
  comes out of, stance width and portrait framing all count the feet.
- **Round joint caps** (DoomBot's legs): each half of a limb keeps a disc of the limb round the
  joint, and the thigh's disc round the hip is filled with leg cloned from just below it (hidden
  behind the body at rest) - so a leg swung far out turns one round end over another instead of
  opening a notch. A cut-script job (`canonical(..., cap=)` in `tools/art/cut_doombot.py`).
  Eric's call, 2026-10-04: his legs came visibly away from his body on his kicks.
- **An outer outline** - a dark ring round each piece's outside edge, drawn behind it - is the
  same kind of step for a drawing whose edge is faint pencil. DoomBot only, on his artist's own
  request (`ringed` in `tools/art/cut_doombot.py`): ring each whole shape before cutting it at a
  joint, so no line crosses an elbow or knee, and leave thin details (his antennae) out. Ask the
  artist before giving anyone else's drawing one.
