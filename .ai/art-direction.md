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

## Two styles

Both are crayon and marker. They differ in what the world is drawn *on*.

**`ExerciseBook`** is the house style and the default. Ruled school paper with a pink margin
rule, drawn over in blue biro and filled with crayon. Used by Fridge Door and City Rooftops.

**`OutsideTheLines`** is for nature stages. Plain paper, no rules, indigo outlines, and crayon
fill that deliberately **overshoots the line**. Used by Open Plains. The overshoot is the single
detail that reads as a child did this rather than a computer — keep it turned up.

Switching a stage between the two should be a one-line change, and it is: `StageData.Style`.

## Night is pale

City Rooftops is a night stage and its sky is **lavender, not black**. Two reasons, and the
first is not negotiable:

1. A dark sky erases the fighters. They are black ink; that is the whole point of them.
2. **It is how children actually draw night** — pale purple sky, a big moon, yellow windows.
   Nobody aged nine fills a page with black crayon.

All of a night stage's warmth comes from its **lit windows**, not from the sky. The sky stays
quiet and the accent colour does the work. Any future night or interior stage follows the same
rule: light substrate, warm accents, darkness implied rather than drawn.

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
| **Fridge Door** *(default)* | ExerciseBook | Where kids' drawings actually end up. Magnets are the platforms; taped-up pages are the scenery, which makes this the stage his non-fighter drawings move into. |
| **Open Plains** | OutsideTheLines | Flat, daytime, only trees and grass: the treetops are the platforms, and nothing on it is built. Ground runs past both blast zones, so **there is no bottom blast zone at all**. |
| **City Rooftops** | ExerciseBook, night | Separate rooftops with real gaps, one tall tower. Buildings extend past the bottom blast zone so the gaps are shafts, not pits. |

Open Plains and City Rooftops are deliberate opposites. On one, a missed jump costs nothing —
which is the right first stage for a younger or newer player, because losing a stock to a failed
recovery is the most demoralising way to lose. On the other, the floor itself is the hazard.
Keep one of each in the roster as it grows.
